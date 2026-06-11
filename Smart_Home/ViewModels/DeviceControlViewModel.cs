using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Smart_Home.Service;

namespace Smart_Home.ViewModels
{
    public partial class DeviceControlViewModel : ObservableObject
    {
        private readonly IDeviceService _deviceService;

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

        public DeviceControlViewModel(IDeviceService deviceService)
        {
            _deviceService = deviceService;
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var statuses = await _deviceService.GetDeviceStatusesAsync();
                LivingRoomLightOn = statuses.GetValueOrDefault("home.light.living_room");
                Bedroom1LightOn = statuses.GetValueOrDefault("home.light.bedroom_1");
                Bedroom2LightOn = statuses.GetValueOrDefault("home.light.bedroom_2");
                FanOn = statuses.GetValueOrDefault("home.fan.living_room");
                OutdoorLedOn = statuses.GetValueOrDefault("home.light.outdoor_led");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi tải trạng thái thiết bị: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task ToggleDeviceAsync(string deviceCode)
        {
            // The bound ToggleButton has already flipped the UI bool; read the desired state from it.
            var desiredOn = GetUiState(deviceCode);
            var result = await _deviceService.ToggleDeviceAsync(deviceCode, desiredOn);

            if (!result.Applied)
            {
                // Command not actually sent (offline / not-found / error): revert the optimistic toggle.
                SetUiState(deviceCode, !desiredOn);
                if (!string.IsNullOrEmpty(result.Message))
                {
                    MessageBox.Show(result.Message);
                }
            }
        }

        private bool GetUiState(string deviceCode) => deviceCode switch
        {
            "home.light.living_room" => LivingRoomLightOn,
            "home.light.bedroom_1" => Bedroom1LightOn,
            "home.light.bedroom_2" => Bedroom2LightOn,
            "home.fan.living_room" => FanOn,
            "home.light.outdoor_led" => OutdoorLedOn,
            _ => false
        };

        private void SetUiState(string deviceCode, bool isOn)
        {
            switch (deviceCode)
            {
                case "home.light.living_room":
                    LivingRoomLightOn = isOn;
                    break;
                case "home.light.bedroom_1":
                    Bedroom1LightOn = isOn;
                    break;
                case "home.light.bedroom_2":
                    Bedroom2LightOn = isOn;
                    break;
                case "home.fan.living_room":
                    FanOn = isOn;
                    break;
                case "home.light.outdoor_led":
                    OutdoorLedOn = isOn;
                    break;
            }
        }
    }
}
