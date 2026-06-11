using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    /// <summary>
    /// App-lifetime singleton that tracks ESP32 node online/offline state via retained MQTT LWT
    /// (smarthome/status/&lt;node&gt;/online) plus a heartbeat-timeout. Updates EspNode.Status/LastSeenAt,
    /// raises a DEVICE_OFFLINE Alert on the online-&gt;offline transition, and exposes NodePresenceChanged.
    /// Lives on the singleton (not the transient DashboardViewModel) so the timer/state survive navigation.
    /// </summary>
    public class NodePresenceService : IDisposable
    {
        private readonly IMqttService _mqtt;
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly NodePresenceOptions _options;
        private readonly Dictionary<string, (bool Online, DateTime LastSeen)> _state = new();
        private DispatcherTimer? _timer;
        private bool _started;

        /// <summary>(nodeCode, isOnline) — raised on the UI thread when a node transitions.</summary>
        public event Action<string, bool>? NodePresenceChanged;

        public NodePresenceService(IMqttService mqtt, IDbContextFactory<AppDbContext> dbFactory, IOptions<NodePresenceOptions> options)
        {
            _mqtt = mqtt;
            _dbFactory = dbFactory;
            _options = options.Value;
            foreach (var code in _options.NodeCodes)
            {
                _state[code] = (false, DateTime.MinValue);
            }
        }

        public bool IsOnline(string nodeCode) => _state.TryGetValue(nodeCode, out var s) && s.Online;

        /// <summary>Must be called on the UI thread (DispatcherTimer requirement).</summary>
        public void Start()
        {
            if (_started) return;
            _started = true;

            _mqtt.MessageReceived += OnMessage;
            _mqtt.ConnectionStatusChanged += OnConnectionChanged;
            if (_mqtt.IsConnected)
            {
                _ = SubscribeAsync();
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1, _options.TimerTickSeconds)) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnConnectionChanged(bool isConnected)
        {
            if (isConnected)
            {
                _ = SubscribeAsync();
            }
        }

        private async Task SubscribeAsync()
        {
            foreach (var node in _options.NodeCodes)
            {
                await _mqtt.SubscribeAsync(_options.PresenceTopicPattern.Replace("{node}", node));
            }
        }

        private void OnMessage(string topic, string payload)
        {
            var node = _options.NodeCodes.FirstOrDefault(n => topic == _options.PresenceTopicPattern.Replace("{node}", n));
            if (node == null) return;
            _ = ApplyAsync(node, ParseOnline(payload), payload);
        }

        private static bool ParseOnline(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return false;
            var p = payload.Trim();
            if (p.Contains("\"online\""))
            {
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, bool>>(p);
                    if (data != null && data.TryGetValue("online", out var b)) return b;
                }
                catch { /* fall through to plain parsing */ }
            }
            p = p.Trim('"');
            return p.Equals("online", StringComparison.OrdinalIgnoreCase) || p == "true" || p == "1";
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var cutoff = TimeSpan.FromSeconds(
                Math.Max(1, _options.HeartbeatIntervalSeconds) * Math.Max(1, _options.MissedHeartbeatsBeforeOffline));
            foreach (var entry in _state.ToList())
            {
                if (entry.Value.Online && DateTime.UtcNow - entry.Value.LastSeen > cutoff)
                {
                    _ = ApplyAsync(entry.Key, false, null);
                }
            }
        }

        private async Task ApplyAsync(string nodeCode, bool online, string? rawPayload)
        {
            var wasOnline = _state.TryGetValue(nodeCode, out var prev) && prev.Online;
            var lastSeen = online ? DateTime.UtcNow : (prev.LastSeen == default ? DateTime.UtcNow : prev.LastSeen);
            _state[nodeCode] = (online, lastSeen);

            try
            {
                await using var ctx = await _dbFactory.CreateDbContextAsync();
                var node = await ctx.EspNodes.FirstOrDefaultAsync(x => x.NodeCode == nodeCode);
                if (node != null)
                {
                    node.Status = online ? "online" : "offline";
                    if (online) node.LastSeenAt = DateTime.UtcNow;
                    node.UpdatedAt = DateTime.UtcNow;

                    // Raise a DEVICE_OFFLINE alert only on the online -> offline transition (avoids spam).
                    if (!online && wasOnline)
                    {
                        ctx.Alerts.Add(new Alert
                        {
                            AlertType = "DEVICE_OFFLINE",
                            Level = "Critical",
                            NodeId = node.Id,
                            Message = $"Node {node.NodeName} ({nodeCode}) mất kết nối.",
                            RawPayload = rawPayload,
                            IsResolved = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    await ctx.SaveChangesAsync();
                }
            }
            catch
            {
                // DB unavailable: keep in-memory state and the UI indicator; do not crash presence tracking.
            }

            if (wasOnline != online)
            {
                App.Current.Dispatcher.Invoke(() => NodePresenceChanged?.Invoke(nodeCode, online));
            }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }
            _mqtt.MessageReceived -= OnMessage;
            _mqtt.ConnectionStatusChanged -= OnConnectionChanged;
        }
    }
}
