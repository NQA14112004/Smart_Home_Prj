namespace Smart_Home.Service
{
    /// <summary>
    /// Single source of truth for MQTT topics. The "smarthome/" prefix matches the existing WPF code.
    /// Doc-only "home/..." aliases (PHAN_TICH_VA_LO_TRINH_PHAT_TRIEN.md, Phụ lục A) are shown for traceability.
    /// </summary>
    public static class MqttTopics
    {
        public const string Prefix = "smarthome";

        // Exact topics
        public const string SensorHome = "smarthome/sensor/home";   // ESP32 home -> (alias home/sensor/status)
        public const string StatusDoor = "smarthome/status/door";   // ESP32 door -> (alias home/door/status)
        public const string DoorControl = "smarthome/door/control"; // -> ESP32 door (alias home/door/control)
        public const string DoorRfid = "smarthome/door/rfid";       // ESP32 door -> (alias home/door/rfid)
        public const string DoorKeypad = "smarthome/door/keypad";   // ESP32 door -> (alias home/door/keypad)
        public const string DoorBreach = "smarthome/door/breach";   // ESP32 door -> forced-entry alert
        public const string AlarmGas = "smarthome/alarm/gas";       // ESP32 home -> gas alarm
        public const string FaceResult = "smarthome/face/result";   // Raspberry Pi 5 -> face recognition result

        // Wildcard subscriptions
        public const string SensorWildcard = "smarthome/sensor/#";
        public const string StatusWildcard = "smarthome/status/#";

        /// <summary>Command topic for a device, e.g. "home.light.living_room" -> "smarthome/home/light/living_room/set".</summary>
        public static string DeviceCommand(string deviceCode) => $"{Prefix}/{deviceCode.Replace('.', '/')}/set";

        /// <summary>Base status topic for a node, e.g. "esp32-door" -> "smarthome/status/esp32-door".</summary>
        public static string Status(string node) => $"{Prefix}/status/{node}";

        /// <summary>Retained presence/LWT topic for a node, e.g. "esp32-door" -> "smarthome/status/esp32-door/online".</summary>
        public static string StatusOnline(string node) => $"{Prefix}/status/{node}/online";
    }
}
