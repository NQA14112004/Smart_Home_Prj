namespace Smart_Home.Models
{
    public class MqttOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public int TlsPort { get; set; } = 8883;
        public string ClientId { get; set; } = "WpfSmartHomeClient";
        public string ClientNode { get; set; } = "wpf";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseTls { get; set; } = false;
        public string? CaCertPath { get; set; }
        public bool AllowUntrustedCertificates { get; set; } = false;
    }
}
