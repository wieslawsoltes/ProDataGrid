using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Avalonia.Diagnostics.Remote;

namespace ProDiagnostics.Remote.Protocol.Benchmarks;

[MemoryDiagnoser]
public class RemoteStreamSessionHubBenchmarks
{
    [Params(1, 5, 20)]
    public int SessionCount { get; set; }

    [Params(1, 50)]
    public int MessagesPerOperation { get; set; }

    private RemoteStreamSessionHub _hub = null!;
    private List<FakeAttachConnection> _connections = null!;

    [GlobalSetup]
    public void Setup()
    {
        _hub = new RemoteStreamSessionHub(
            new RemoteStreamSessionHubOptions
            {
                MaxQueueLengthPerSession = 1024,
                MaxDispatchBatchSize = 512,
            });
        _connections = new List<FakeAttachConnection>(SessionCount);

        for (var i = 0; i < SessionCount; i++)
        {
            var connection = new FakeAttachConnection("benchmark-" + i);
            _connections.Add(connection);
            _hub.RegisterSession(Guid.NewGuid(), connection);
        }
    }

    [Benchmark]
    public void Publish_FanoutBurst()
    {
        for (var i = 0; i < MessagesPerOperation; i++)
        {
            _hub.Publish(
                RemoteStreamTopics.Logs,
                "{\"timestampUtc\":\"2026-03-04T00:00:00Z\",\"level\":\"Info\",\"message\":\"bench\"}");
        }
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _hub.DisposeAsync();
    }

    private sealed class FakeAttachConnection : IAttachConnection
    {
        private readonly ConcurrentQueue<IRemoteMessage> _messages = new();

        public FakeAttachConnection(string id)
        {
            ConnectionId = Guid.NewGuid();
            RemoteEndpoint = id;
        }

        public Guid ConnectionId { get; }

        public string? RemoteEndpoint { get; }

        public bool IsOpen => true;

        public int SentMessages => _messages.Count;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            _messages.Enqueue(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return ValueTask.FromResult<AttachReceiveResult?>(null);
        }

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
        {
            _ = reason;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
