using System;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public class MqttClientService : IMqttService, IDisposable
    {
        private IManagedMqttClient? _managedMqttClient;
        private readonly MqttOptions _options;

        public event Action<string, string>? MessageReceived;
        public event Action<bool>? ConnectionStatusChanged;

        public bool IsConnected => _managedMqttClient?.IsConnected ?? false;

        public MqttClientService(IOptions<MqttOptions> options)
        {
            _options = options.Value;
            var factory = new MqttFactory();
            _managedMqttClient = factory.CreateManagedMqttClient();

            _managedMqttClient.ConnectedAsync += async e =>
            {
                ConnectionStatusChanged?.Invoke(true);
                // Birth message: announce this client online (retained); pairs with the LWT set in ConnectAsync.
                var birth = new MqttApplicationMessageBuilder()
                    .WithTopic(MqttTopics.StatusOnline(_options.ClientNode))
                    .WithPayload("{\"online\":true}")
                    .WithRetainFlag(true)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await _managedMqttClient!.EnqueueAsync(birth);
            };

            _managedMqttClient.DisconnectedAsync += async e =>
            {
                ConnectionStatusChanged?.Invoke(false);
                await Task.CompletedTask;
            };

            _managedMqttClient.ApplicationMessageReceivedAsync += async e =>
            {
                var topic = e.ApplicationMessage.Topic;
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                MessageReceived?.Invoke(topic, payload);
                await Task.CompletedTask;
            };
        }

        public async Task ConnectAsync()
        {
            if (_managedMqttClient == null) return;

            var port = _options.UseTls ? _options.TlsPort : _options.Port;

            var clientOptionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(_options.ClientId)
                .WithTcpServer(_options.Host, port)
                .WithWillTopic(MqttTopics.StatusOnline(_options.ClientNode))
                .WithWillPayload("{\"online\":false}")
                .WithWillRetain(true)
                .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                clientOptionsBuilder.WithCredentials(_options.Username, _options.Password);
            }

            if (_options.UseTls)
            {
                X509Certificate2? caCert =
                    !string.IsNullOrWhiteSpace(_options.CaCertPath) && File.Exists(_options.CaCertPath)
                        ? new X509Certificate2(_options.CaCertPath)
                        : null;

                clientOptionsBuilder.WithTlsOptions(o =>
                {
                    o.UseTls(true);
                    o.WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13);

                    if (caCert != null)
                    {
                        // Trust ONLY the Mosquitto self-signed CA (scoped to this connection, no machine-wide trust).
                        o.WithCertificateValidationHandler(ctx => ValidateAgainstCa(ctx, caCert));
                    }
                    else if (_options.AllowUntrustedCertificates)
                    {
                        // DEV ONLY fallback — disables broker authentication (MITM risk); never enable in production.
                        o.WithAllowUntrustedCertificates(true);
                        o.WithIgnoreCertificateChainErrors(true);
                    }
                });
            }

            var clientOptions = clientOptionsBuilder.Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(clientOptions)
                .Build();

            await _managedMqttClient.StartAsync(managedOptions);
        }

        public async Task DisconnectAsync()
        {
            if (_managedMqttClient != null)
            {
                await _managedMqttClient.StopAsync();
            }
        }

        public async Task PublishAsync(string topic, string payload, bool retain = false)
        {
            if (_managedMqttClient == null || !IsConnected) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(retain)
                .Build();

            await _managedMqttClient.EnqueueAsync(message);
        }

        public async Task SubscribeAsync(string topic)
        {
            if (_managedMqttClient == null) return;

            await _managedMqttClient.SubscribeAsync(topic);
        }

        public async Task UnsubscribeAsync(string topic)
        {
            if (_managedMqttClient == null) return;

            await _managedMqttClient.UnsubscribeAsync(topic);
        }

        private static bool ValidateAgainstCa(MqttClientCertificateValidationEventArgs ctx, X509Certificate2 ca)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.ExtraStore.Add(ca);
            using var serverCert = new X509Certificate2(ctx.Certificate);
            return chain.Build(serverCert);
        }

        public void Dispose()
        {
            _managedMqttClient?.Dispose();
            _managedMqttClient = null;
        }
    }
}
