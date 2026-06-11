using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public enum DeviceToggleOutcome { Sent, Queued, NotFound, Error }

    /// <summary>Result of a device toggle so the VM can revert UI / show the right message.</summary>
    public class DeviceToggleResult
    {
        public DeviceToggleOutcome Outcome { get; init; }
        public string? Message { get; init; }
        public bool Applied => Outcome == DeviceToggleOutcome.Sent;
    }

    public interface IDeviceService
    {
        Task<Dictionary<string, bool>> GetDeviceStatusesAsync();
        Task<DeviceToggleResult> ToggleDeviceAsync(string deviceCode, bool desiredOn);
    }

    public class DeviceService : IDeviceService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IMqttService _mqtt;

        public DeviceService(IDbContextFactory<AppDbContext> factory, IMqttService mqtt)
        {
            _factory = factory;
            _mqtt = mqtt;
        }

        public async Task<Dictionary<string, bool>> GetDeviceStatusesAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var devices = await ctx.Devices.AsNoTracking().ToListAsync();
            return devices.ToDictionary(d => d.DeviceCode, d => d.Status == "on");
        }

        public async Task<DeviceToggleResult> ToggleDeviceAsync(string deviceCode, bool desiredOn)
        {
            try
            {
                await using var ctx = await _factory.CreateDbContextAsync();
                var device = await ctx.Devices.FirstOrDefaultAsync(d => d.DeviceCode == deviceCode);
                if (device == null)
                    return new DeviceToggleResult { Outcome = DeviceToggleOutcome.NotFound, Message = $"Không tìm thấy thiết bị: {deviceCode}" };

                var commandValue = desiredOn ? "on" : "off";
                var previousStatus = device.Status;
                var payload = JsonSerializer.Serialize(new { status = commandValue });

                device.Status = commandValue;
                device.UpdatedAt = DateTime.UtcNow;

                var command = new DeviceCommand
                {
                    DeviceId = device.Id,
                    NodeId = device.NodeId,
                    Command = "set_status",
                    Payload = payload,
                    Status = "pending",
                    Source = "wpf",
                    RequestedAt = DateTime.UtcNow
                };
                ctx.DeviceCommands.Add(command);
                await ctx.SaveChangesAsync();

                if (_mqtt.IsConnected)
                {
                    var topic = device.MqttCommandTopic ?? MqttTopics.DeviceCommand(deviceCode);
                    await _mqtt.PublishAsync(topic, payload);
                    command.Status = "sent";
                    command.SentAt = DateTime.UtcNow;
                    await ctx.SaveChangesAsync();
                    return new DeviceToggleResult { Outcome = DeviceToggleOutcome.Sent };
                }

                // MQTT offline: keep the queued command but revert the device status + UI.
                device.Status = previousStatus;
                await ctx.SaveChangesAsync();
                return new DeviceToggleResult
                {
                    Outcome = DeviceToggleOutcome.Queued,
                    Message = "MQTT chưa kết nối. Lệnh đã được lưu vào hàng đợi."
                };
            }
            catch (Exception ex)
            {
                return new DeviceToggleResult { Outcome = DeviceToggleOutcome.Error, Message = "Lỗi khi gửi lệnh thiết bị: " + ex.Message };
            }
        }
    }
}
