using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Transport;

public class DiagnosticsUdpReceiverTests
{
    [Fact]
    public async Task DiagnosticsUdpReceiver_Receives_Metric()
    {
        using var receiver = new DiagnosticsUdpReceiver(0);
        var port = receiver.LocalEndPoint.Port;
        var tcs = new TaskCompletionSource<TelemetryPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.PacketReceived += (packet, _) => tcs.TrySetResult(packet);
        receiver.Start();

        var metric = new TelemetryMetric(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Meter",
            "Counter",
            "Description",
            "count",
            "Counter`1",
            TelemetryMetricValue.FromLong(5),
            Array.Empty<TelemetryTag>());
        var writer = new TelemetryPacketWriter();
        var payload = writer.Write(metric, 0).ToArray();

        using var sender = new UdpClient();
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));

        var packet = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var read = Assert.IsType<TelemetryMetric>(packet);
        Assert.Equal(metric.InstrumentName, read.InstrumentName);
    }

    [Fact]
    public async Task DiagnosticsUdpReceiver_Ignores_Malformed_Packets()
    {
        using var receiver = new DiagnosticsUdpReceiver(0);
        var port = receiver.LocalEndPoint.Port;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.PacketReceived += (_, _) => tcs.TrySetResult(true);
        receiver.Start();

        using var sender = new UdpClient();
        var payload = new byte[] { 0xFF, 0x01, 0x02 };
        await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));

        await Task.Delay(200);
        Assert.False(tcs.Task.IsCompleted);
    }

    // Port selection handled by DiagnosticsUdpReceiver(0).
}
