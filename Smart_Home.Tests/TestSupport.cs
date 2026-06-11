using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Service;

namespace Smart_Home.Tests
{
    /// <summary>
    /// IDbContextFactory backed by a single EF Core InMemory store, shared across every context it
    /// creates, so a service's writes are visible to later reads within the same test. Each instance
    /// owns its own uniquely-named store, so tests are isolated from one another.
    /// </summary>
    internal sealed class InMemoryDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public InMemoryDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        // Explicit async override (EF Core's default interface impl already delegates here) — keeps the
        // fake self-contained and robust to future EF changes; production services call this overload.
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    /// <summary>Deterministic IPasswordHasher fake — no BCrypt cost, output is assertable in tests.</summary>
    internal sealed class FakePasswordHasher : IPasswordHasher
    {
        public const string Prefix = "hashed:";
        public string Hash(string raw) => Prefix + raw;
        public bool Verify(string raw, string hash) => hash == Prefix + raw;
    }

    /// <summary>IMqttService fake that records publishes and lets tests toggle IsConnected / raise events.</summary>
    internal sealed class FakeMqttService : IMqttService
    {
        public bool IsConnected { get; set; }
        public List<(string Topic, string Payload, bool Retain)> Published { get; } = new();

        public event Action<string, string>? MessageReceived;
        public event Action<bool>? ConnectionStatusChanged;

        public Task ConnectAsync() => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public Task PublishAsync(string topic, string payload, bool retain = false)
        {
            Published.Add((topic, payload, retain));
            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topic) => Task.CompletedTask;
        public Task UnsubscribeAsync(string topic) => Task.CompletedTask;

        // Exposed so tests can exercise subscribers (and so the events are not flagged unused).
        public void RaiseMessage(string topic, string payload) => MessageReceived?.Invoke(topic, payload);
        public void RaiseConnection(bool connected) => ConnectionStatusChanged?.Invoke(connected);
    }

    internal static class TestData
    {
        /// <summary>A fresh, isolated in-memory factory (unique store name per call).</summary>
        public static InMemoryDbContextFactory NewFactory() => new(Guid.NewGuid().ToString());
    }
}
