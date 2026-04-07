using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Avalonia.Diagnostics.Remote;

namespace ProDiagnostics.Remote.Protocol.Benchmarks;

[MemoryDiagnoser]
public class RemoteMessageSerializerBenchmarks
{
    [Params(256, 4096, 16384)]
    public int PayloadBytes { get; set; }

    private RemoteHelloMessage _helloMessage = null!;
    private RemoteRequestMessage _requestMessage = null!;
    private byte[] _helloFrame = null!;
    private byte[] _requestFrame = null!;

    [GlobalSetup]
    public void Setup()
    {
        var payload = new string('x', PayloadBytes);
        _helloMessage = new RemoteHelloMessage(
            SessionId: Guid.NewGuid(),
            ProcessId: Environment.ProcessId,
            ProcessName: "bench-process",
            ApplicationName: "bench-app",
            MachineName: "bench-machine",
            RuntimeVersion: Environment.Version.ToString(),
            ClientName: "bench-client",
            RequestedFeatures: new[] { "read-only", "mutation", "stream" });
        _requestMessage = new RemoteRequestMessage(
            SessionId: Guid.NewGuid(),
            RequestId: 123,
            Method: RemoteReadOnlyMethods.TreeSnapshotGet,
            PayloadJson: "{\"scope\":\"combined\",\"includeSourceLocations\":false,\"padding\":\"" + payload + "\"}");

        _helloFrame = RemoteMessageSerializer.Serialize(_helloMessage).ToArray();
        _requestFrame = RemoteMessageSerializer.Serialize(_requestMessage).ToArray();
    }

    [Benchmark]
    public byte[] Serialize_Hello()
    {
        return RemoteMessageSerializer.Serialize(_helloMessage).ToArray();
    }

    [Benchmark]
    public byte[] Serialize_Request()
    {
        return RemoteMessageSerializer.Serialize(_requestMessage).ToArray();
    }

    [Benchmark]
    public IRemoteMessage Deserialize_Hello()
    {
        _ = RemoteMessageSerializer.TryDeserialize(_helloFrame, out var message);
        return message!;
    }

    [Benchmark]
    public IRemoteMessage Deserialize_Request()
    {
        _ = RemoteMessageSerializer.TryDeserialize(_requestFrame, out var message);
        return message!;
    }
}
