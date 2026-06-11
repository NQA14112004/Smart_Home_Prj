using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Smart_Home.Service;

namespace Smart_Home.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly IDashboardService _dashboardService;
        private readonly IMqttService _mqttService;
        private readonly NodePresenceService _nodePresence;
        private bool _disposed;

        [ObservableProperty]
        private string _doorStatus = "Unknown";

        [ObservableProperty]
        private string _lockStatus = "Unknown";

        [ObservableProperty]
        private bool _doorNodeOnline;

        [ObservableProperty]
        private bool _homeNodeOnline;

        [ObservableProperty]
        private double? _temperature = 0;

        [ObservableProperty]
        private double? _humidity = 0;

        [ObservableProperty]
        private int? _gasValue = 0;

        [ObservableProperty]
        private int? _lightValue = 0;

        [ObservableProperty]
        private bool _livingRoomLightOn;

        [ObservableProperty]
        private bool _bedroom1LightOn;

        [ObservableProperty]
        private bool _bedroom2LightOn;

        [ObservableProperty]
        private bool _fanOn;

        [ObservableProperty]
        private bool _outdoorLedOn;

        [ObservableProperty]
        private string _latestAlertMessage = "Đang tải...";

        [ObservableProperty]
        private string _latestAlertLevel = "Info";

        [ObservableProperty]
        private bool _isMqttConnected;

        public ISeries[] TemperatureSeries { get; set; } =
        {
            new LineSeries<double>
            {
                Values = new double[] { 0 },
                Fill = null,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 }
            }
        };

        public ISeries[] HumiditySeries { get; set; } =
        {
            new LineSeries<double>
            {
                Values = new double[] { 0 },
                Fill = null,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(SKColors.MediumSpringGreen) { StrokeThickness = 3 }
            }
        };

        public DashboardViewModel(IDashboardService dashboardService, IMqttService mqttService, NodePresenceService nodePresence)
        {
            _dashboardService = dashboardService;
            _mqttService = mqttService;
            _nodePresence = nodePresence;

            _mqttService.ConnectionStatusChanged += OnMqttConnectionStatusChanged;
            _mqttService.MessageReceived += OnMqttMessageReceived;
            _nodePresence.NodePresenceChanged += OnNodePresenceChanged;

            // Seed live node state from the always-on presence service.
            DoorNodeOnline = _nodePresence.IsOnline("esp32-door");
            HomeNodeOnline = _nodePresence.IsOnline("esp32-home");

            IsMqttConnected = _mqttService.IsConnected;
            if (IsMqttConnected)
            {
                _ = SubscribeDashboardTopicsAsync();
            }

            _ = LoadDataAsync();
        }

        private void OnNodePresenceChanged(string nodeCode, bool isOnline)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (nodeCode == "esp32-door") DoorNodeOnline = isOnline;
                else if (nodeCode == "esp32-home") HomeNodeOnline = isOnline;
            });
        }

        private void OnMqttConnectionStatusChanged(bool isConnected)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                IsMqttConnected = isConnected;
                if (isConnected)
                {
                    _ = SubscribeDashboardTopicsAsync();
                }
            });
        }

        private async Task SubscribeDashboardTopicsAsync()
        {
            await _mqttService.SubscribeAsync(MqttTopics.SensorWildcard);
            await _mqttService.SubscribeAsync(MqttTopics.StatusWildcard);
            await _mqttService.SubscribeAsync(MqttTopics.DoorBreach);
        }

        private void OnMqttMessageReceived(string topic, string payload)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (topic == MqttTopics.SensorHome)
                    {
                        var data = JsonSerializer.Deserialize<Dictionary<string, double>>(payload);
                        if (data != null)
                        {
                            if (data.TryGetValue("temperature", out var temp)) Temperature = temp;
                            if (data.TryGetValue("humidity", out var hum)) Humidity = hum;
                            if (data.TryGetValue("gas", out var gas)) GasValue = (int)gas;
                            if (data.TryGetValue("light", out var light)) LightValue = (int)light;
                        }
                    }
                    else if (topic == MqttTopics.StatusDoor)
                    {
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(payload);
                        if (data != null)
                        {
                            if (data.TryGetValue("door", out var door)) DoorStatus = door;
                            if (data.TryGetValue("lock", out var lockStatus)) LockStatus = lockStatus;
                        }
                    }
                    else if (topic == MqttTopics.DoorBreach)
                    {
                        LatestAlertLevel = "Critical";
                        LatestAlertMessage = "Phát hiện cạy/phá cửa! " + payload;
                    }
                }
                catch (Exception ex)
                {
                    LatestAlertMessage = "Lỗi xử lý MQTT: " + ex.Message;
                    LatestAlertLevel = "Warning";
                }
            });
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var snapshot = await _dashboardService.GetSnapshotAsync();

                DoorNodeOnline = snapshot.DoorNodeOnline;
                HomeNodeOnline = snapshot.HomeNodeOnline;
                DoorStatus = snapshot.DoorStatus;
                LockStatus = snapshot.LockStatus;
                // Keep the seeded defaults when the DB has no reading yet (matches pre-refactor behavior).
                Temperature = snapshot.Temperature ?? Temperature;
                Humidity = snapshot.Humidity ?? Humidity;
                GasValue = snapshot.GasValue ?? GasValue;
                LightValue = snapshot.LightValue ?? LightValue;
                LivingRoomLightOn = snapshot.LivingRoomLightOn;
                Bedroom1LightOn = snapshot.Bedroom1LightOn;
                Bedroom2LightOn = snapshot.Bedroom2LightOn;
                FanOn = snapshot.FanOn;
                OutdoorLedOn = snapshot.OutdoorLedOn;
                LatestAlertMessage = snapshot.LatestAlertMessage;
                LatestAlertLevel = snapshot.LatestAlertLevel;
            }
            catch (Exception ex)
            {
                LatestAlertMessage = "Lỗi kết nối CSDL: " + ex.Message;
                LatestAlertLevel = "Critical";
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _mqttService.ConnectionStatusChanged -= OnMqttConnectionStatusChanged;
            _mqttService.MessageReceived -= OnMqttMessageReceived;
            _nodePresence.NodePresenceChanged -= OnNodePresenceChanged;
            _disposed = true;
        }
    }
}
