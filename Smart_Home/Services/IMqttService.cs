using System;
using System.Threading.Tasks;

namespace Smart_Home.Service
{
    public interface IMqttService
    {
        event Action<string, string> MessageReceived;
        event Action<bool> ConnectionStatusChanged;

        Task ConnectAsync();
        Task DisconnectAsync();
        Task PublishAsync(string topic, string payload, bool retain = false);
        Task SubscribeAsync(string topic);
        Task UnsubscribeAsync(string topic);
        bool IsConnected { get; }
    }
}
