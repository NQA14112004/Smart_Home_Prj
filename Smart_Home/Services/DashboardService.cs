using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;

namespace Smart_Home.Service
{
    /// <summary>DB-derived dashboard state. MQTT/realtime + node presence stay in the ViewModel.</summary>
    public class DashboardSnapshot
    {
        public bool DoorNodeOnline { get; set; }
        public bool HomeNodeOnline { get; set; }
        public string DoorStatus { get; set; } = "Unknown";
        public string LockStatus { get; set; } = "Unknown";
        public double? Temperature { get; set; }
        public double? Humidity { get; set; }
        public int? GasValue { get; set; }
        public int? LightValue { get; set; }
        public bool LivingRoomLightOn { get; set; }
        public bool Bedroom1LightOn { get; set; }
        public bool Bedroom2LightOn { get; set; }
        public bool FanOn { get; set; }
        public bool OutdoorLedOn { get; set; }
        public string LatestAlertMessage { get; set; } = "Không có cảnh báo nào.";
        public string LatestAlertLevel { get; set; } = "Info";
    }

    public interface IDashboardService
    {
        Task<DashboardSnapshot> GetSnapshotAsync();
    }

    public class DashboardService : IDashboardService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public DashboardService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<DashboardSnapshot> GetSnapshotAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var snapshot = new DashboardSnapshot();

            var nodes = await ctx.EspNodes.AsNoTracking().ToListAsync();
            snapshot.DoorNodeOnline = nodes.FirstOrDefault(n => n.NodeCode == "esp32-door")?.Status == "online";
            snapshot.HomeNodeOnline = nodes.FirstOrDefault(n => n.NodeCode == "esp32-home")?.Status == "online";

            var latestHome = await ctx.SensorReadings
                .Include(sr => sr.Node).AsNoTracking()
                .Where(sr => sr.Node != null && sr.Node.NodeCode == "esp32-home")
                .OrderByDescending(sr => sr.CreatedAt).FirstOrDefaultAsync();
            if (latestHome != null)
            {
                snapshot.Temperature = latestHome.Temperature;
                snapshot.Humidity = latestHome.Humidity;
                snapshot.GasValue = latestHome.GasValue;
                snapshot.LightValue = latestHome.LightValue;
            }

            var latestDoor = await ctx.SensorReadings
                .Include(sr => sr.Node).AsNoTracking()
                .Where(sr => sr.Node != null && sr.Node.NodeCode == "esp32-door")
                .OrderByDescending(sr => sr.CreatedAt).FirstOrDefaultAsync();
            if (latestDoor != null)
            {
                snapshot.DoorStatus = latestDoor.DoorStatus ?? "Unknown";
                snapshot.LockStatus = latestDoor.LockStatus ?? "Unknown";
            }

            var devices = await ctx.Devices.AsNoTracking().ToListAsync();
            snapshot.LivingRoomLightOn = devices.FirstOrDefault(d => d.DeviceCode == "home.light.living_room")?.Status == "on";
            snapshot.Bedroom1LightOn = devices.FirstOrDefault(d => d.DeviceCode == "home.light.bedroom_1")?.Status == "on";
            snapshot.Bedroom2LightOn = devices.FirstOrDefault(d => d.DeviceCode == "home.light.bedroom_2")?.Status == "on";
            snapshot.FanOn = devices.FirstOrDefault(d => d.DeviceCode == "home.fan.living_room")?.Status == "on";
            snapshot.OutdoorLedOn = devices.FirstOrDefault(d => d.DeviceCode == "home.light.outdoor_led")?.Status == "on";

            var latestAlert = await ctx.Alerts.AsNoTracking().OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
            if (latestAlert != null)
            {
                snapshot.LatestAlertMessage = latestAlert.Message;
                snapshot.LatestAlertLevel = latestAlert.Level;
            }

            return snapshot;
        }
    }
}
