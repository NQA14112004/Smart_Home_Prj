using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Models;
using Smart_Home.Service;
using Xunit;

namespace Smart_Home.Tests
{
    public class DeviceServiceTests
    {
        private static async Task<(InMemoryDbContextFactory Factory, int NodeId)> SeedNodeAsync()
        {
            var factory = TestData.NewFactory();
            await using var ctx = factory.CreateDbContext();
            var node = new EspNode { NodeCode = "esp32-home", NodeName = "Home", NodeType = "esp32", Status = "online" };
            ctx.EspNodes.Add(node);
            await ctx.SaveChangesAsync();
            return (factory, node.Id);
        }

        private static async Task AddDeviceAsync(InMemoryDbContextFactory factory, int nodeId, string code, string status, string? cmdTopic = null)
        {
            await using var ctx = factory.CreateDbContext();
            ctx.Devices.Add(new Device
            {
                DeviceCode = code,
                DeviceName = code,
                DeviceType = "light",
                NodeId = nodeId,
                Status = status,
                MqttCommandTopic = cmdTopic,
                IsControllable = true
            });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task GetDeviceStatusesAsync_MapsOnToTrue_OffToFalse()
        {
            var (factory, nodeId) = await SeedNodeAsync();
            await AddDeviceAsync(factory, nodeId, "home.light.living_room", "on");
            await AddDeviceAsync(factory, nodeId, "home.fan.living_room", "off");
            var service = new DeviceService(factory, new FakeMqttService());

            var statuses = await service.GetDeviceStatusesAsync();
            Assert.True(statuses["home.light.living_room"]);
            Assert.False(statuses["home.fan.living_room"]);
        }

        [Fact]
        public async Task ToggleDeviceAsync_DeviceMissing_ReturnsNotFound()
        {
            var (factory, _) = await SeedNodeAsync();
            var service = new DeviceService(factory, new FakeMqttService { IsConnected = true });
            var result = await service.ToggleDeviceAsync("missing.device", true);
            Assert.Equal(DeviceToggleOutcome.NotFound, result.Outcome);
            Assert.False(result.Applied);
        }

        [Fact]
        public async Task ToggleDeviceAsync_Connected_PublishesAndMarksSent()
        {
            var (factory, nodeId) = await SeedNodeAsync();
            await AddDeviceAsync(factory, nodeId, "home.light.living_room", "off");
            var mqtt = new FakeMqttService { IsConnected = true };
            var service = new DeviceService(factory, mqtt);

            var result = await service.ToggleDeviceAsync("home.light.living_room", true);

            Assert.Equal(DeviceToggleOutcome.Sent, result.Outcome);
            Assert.True(result.Applied);
            var published = Assert.Single(mqtt.Published);
            Assert.Equal("smarthome/home/light/living_room/set", published.Topic);
            Assert.Contains("\"on\"", published.Payload);

            await using var ctx = factory.CreateDbContext();
            Assert.Equal("on", (await ctx.Devices.SingleAsync()).Status);
            Assert.Equal("sent", (await ctx.DeviceCommands.SingleAsync()).Status);
        }

        [Fact]
        public async Task ToggleDeviceAsync_UsesDeviceCommandTopicOverride()
        {
            var (factory, nodeId) = await SeedNodeAsync();
            await AddDeviceAsync(factory, nodeId, "home.light.living_room", "off", cmdTopic: "custom/topic/set");
            var mqtt = new FakeMqttService { IsConnected = true };
            var service = new DeviceService(factory, mqtt);

            await service.ToggleDeviceAsync("home.light.living_room", true);
            Assert.Equal("custom/topic/set", mqtt.Published.Single().Topic);
        }

        [Fact]
        public async Task ToggleDeviceAsync_Disconnected_QueuesAndRevertsStatus()
        {
            var (factory, nodeId) = await SeedNodeAsync();
            await AddDeviceAsync(factory, nodeId, "home.light.living_room", "off");
            var mqtt = new FakeMqttService { IsConnected = false };
            var service = new DeviceService(factory, mqtt);

            var result = await service.ToggleDeviceAsync("home.light.living_room", true);

            Assert.Equal(DeviceToggleOutcome.Queued, result.Outcome);
            Assert.False(result.Applied);
            Assert.Empty(mqtt.Published);

            await using var ctx = factory.CreateDbContext();
            Assert.Equal("off", (await ctx.Devices.SingleAsync()).Status);          // reverted
            Assert.Equal("pending", (await ctx.DeviceCommands.SingleAsync()).Status); // command kept queued
        }
    }

    public class AccessLogServiceTests
    {
        private static async Task<(IAccessLogService Service, InMemoryDbContextFactory Factory, int NodeId)> CreateAsync()
        {
            var factory = TestData.NewFactory();
            await using var ctx = factory.CreateDbContext();
            var node = new EspNode { NodeCode = "esp32-door", NodeName = "Door", Status = "online" };
            ctx.EspNodes.Add(node);
            await ctx.SaveChangesAsync();
            return (new AccessLogService(factory), factory, node.Id);
        }

        private static async Task AddLogAsync(InMemoryDbContextFactory factory, int nodeId, DateTime createdAt)
        {
            await using var ctx = factory.CreateDbContext();
            ctx.AccessLogs.Add(new AccessLog { NodeId = nodeId, Method = "RFID", Result = "success", CreatedAt = createdAt });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task GetLogsAsync_NoFilter_ReturnsAllNewestFirst()
        {
            var (service, factory, nodeId) = await CreateAsync();
            await AddLogAsync(factory, nodeId, DateTime.UtcNow.AddDays(-1));
            await AddLogAsync(factory, nodeId, DateTime.UtcNow);

            var logs = await service.GetLogsAsync(null, null);
            Assert.Equal(2, logs.Count);
            Assert.True(logs[0].CreatedAt >= logs[1].CreatedAt);
        }

        [Fact]
        public async Task GetLogsAsync_StartDateFilter_ExcludesOld()
        {
            var (service, factory, nodeId) = await CreateAsync();
            await AddLogAsync(factory, nodeId, DateTime.UtcNow);
            await AddLogAsync(factory, nodeId, DateTime.UtcNow.AddDays(-100));

            var logs = await service.GetLogsAsync(DateTime.Today.AddDays(-7), null);
            Assert.Single(logs);
        }

        [Fact]
        public async Task GetLogsAsync_IncludesNode()
        {
            var (service, factory, nodeId) = await CreateAsync();
            await AddLogAsync(factory, nodeId, DateTime.UtcNow);
            var log = Assert.Single(await service.GetLogsAsync(null, null));
            Assert.NotNull(log.Node);
        }
    }

    public class AlertServiceTests
    {
        private static async Task AddAlertAsync(InMemoryDbContextFactory factory, DateTime createdAt)
        {
            await using var ctx = factory.CreateDbContext();
            ctx.Alerts.Add(new Alert { AlertType = "GAS_HIGH", Level = "Warning", Message = "m", CreatedAt = createdAt });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public async Task GetAlertsAsync_NoFilter_ReturnsAll()
        {
            var factory = TestData.NewFactory();
            var service = new AlertService(factory);
            await AddAlertAsync(factory, DateTime.UtcNow);
            await AddAlertAsync(factory, DateTime.UtcNow.AddDays(-2));
            Assert.Equal(2, (await service.GetAlertsAsync(null, null)).Count);
        }

        [Fact]
        public async Task GetAlertsAsync_EndDateFilter_ExcludesRecent()
        {
            var factory = TestData.NewFactory();
            var service = new AlertService(factory);
            await AddAlertAsync(factory, DateTime.UtcNow);
            await AddAlertAsync(factory, DateTime.UtcNow.AddDays(-100));

            var alerts = await service.GetAlertsAsync(null, DateTime.Today.AddDays(-50));
            Assert.Single(alerts);
        }
    }

    public class DashboardServiceTests
    {
        [Fact]
        public async Task GetSnapshotAsync_EmptyDb_ReturnsDefaults()
        {
            var snapshot = await new DashboardService(TestData.NewFactory()).GetSnapshotAsync();
            Assert.False(snapshot.DoorNodeOnline);
            Assert.False(snapshot.HomeNodeOnline);
            Assert.Equal("Unknown", snapshot.DoorStatus);
            Assert.Equal("Không có cảnh báo nào.", snapshot.LatestAlertMessage);
        }

        [Fact]
        public async Task GetSnapshotAsync_PopulatedDb_ReflectsState()
        {
            var factory = TestData.NewFactory();
            await using (var ctx = factory.CreateDbContext())
            {
                var home = new EspNode { NodeCode = "esp32-home", NodeName = "Home", Status = "online" };
                var door = new EspNode { NodeCode = "esp32-door", NodeName = "Door", Status = "offline" };
                ctx.EspNodes.AddRange(home, door);
                await ctx.SaveChangesAsync();

                ctx.SensorReadings.Add(new SensorReading
                {
                    NodeId = home.Id, Temperature = 30.5, Humidity = 70, GasValue = 120, LightValue = 200, CreatedAt = DateTime.UtcNow
                });
                ctx.SensorReadings.Add(new SensorReading
                {
                    NodeId = door.Id, DoorStatus = "closed", LockStatus = "locked", CreatedAt = DateTime.UtcNow
                });
                ctx.Devices.Add(new Device
                {
                    DeviceCode = "home.light.living_room", DeviceName = "LR", DeviceType = "light", NodeId = home.Id, Status = "on"
                });
                ctx.Alerts.Add(new Alert { AlertType = "GAS_HIGH", Level = "Critical", Message = "Gas!", CreatedAt = DateTime.UtcNow });
                await ctx.SaveChangesAsync();
            }

            var snapshot = await new DashboardService(factory).GetSnapshotAsync();
            Assert.True(snapshot.HomeNodeOnline);
            Assert.False(snapshot.DoorNodeOnline);
            Assert.Equal(30.5, snapshot.Temperature);
            Assert.Equal("closed", snapshot.DoorStatus);
            Assert.Equal("locked", snapshot.LockStatus);
            Assert.True(snapshot.LivingRoomLightOn);
            Assert.Equal("Gas!", snapshot.LatestAlertMessage);
            Assert.Equal("Critical", snapshot.LatestAlertLevel);
        }
    }
}
