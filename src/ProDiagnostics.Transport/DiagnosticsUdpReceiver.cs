using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProDiagnostics.Transport;

public sealed class DiagnosticsUdpReceiver : IDisposable
{
    private readonly UdpClient _client;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public DiagnosticsUdpReceiver(int port)
    {
        _client = new UdpClient(port);
    }

    public event Action<TelemetryPacket, IPEndPoint>? PacketReceived;

    public IPEndPoint LocalEndPoint
        => _client.Client.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0);

    public void Start()
    {
        if (_loop != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ReceiveLoop(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _client.Dispose();
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _client.ReceiveAsync(token).ConfigureAwait(false);
                if (TelemetryPacketReader.TryRead(result.Buffer, out var packet) && packet != null)
                {
                    PacketReceived?.Invoke(packet, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Swallow malformed packets and transient socket errors.
            }
        }
    }
}
