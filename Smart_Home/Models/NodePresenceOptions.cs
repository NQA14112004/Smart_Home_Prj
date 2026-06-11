namespace Smart_Home.Models
{
    /// <summary>
    /// Configuration for node online/offline tracking (LWT + heartbeat timeout). Bound from appsettings.json
    /// "NodePresenceOptions" via IOptions&lt;NodePresenceOptions&gt;.
    /// </summary>
    public class NodePresenceOptions
    {
        /// <summary>Expected interval (seconds) between node heartbeat/birth publishes.</summary>
        public int HeartbeatIntervalSeconds { get; set; } = 30;

        /// <summary>Number of missed heartbeat intervals before a node is flipped offline.</summary>
        public int MissedHeartbeatsBeforeOffline { get; set; } = 3;

        /// <summary>How often (seconds) the timeout check runs.</summary>
        public int TimerTickSeconds { get; set; } = 5;

        /// <summary>Retained presence topic pattern; "{node}" is replaced with each node code.</summary>
        public string PresenceTopicPattern { get; set; } = "smarthome/status/{node}/online";

        /// <summary>Node codes to track (must match esp_nodes.node_code).</summary>
        public string[] NodeCodes { get; set; } = new[] { "esp32-door", "esp32-home" };
    }
}
