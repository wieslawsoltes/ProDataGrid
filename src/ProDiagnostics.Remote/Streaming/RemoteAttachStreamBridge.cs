using System;
using System.Threading.Tasks;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Forwards source stream payloads into session fan-out queues.
/// </summary>
public sealed class RemoteAttachStreamBridge : IDisposable, IAsyncDisposable
{
    private readonly RemoteStreamSessionHub _hub;
    private IDisposable? _subscription;
    private bool _isDisposed;

    public RemoteAttachStreamBridge(RemoteStreamSessionHub hub, IRemoteStreamSource source)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _subscription = source.Subscribe(OnPayload);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _subscription?.Dispose();
        _subscription = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void OnPayload(RemoteStreamPayload payload)
    {
        if (_isDisposed)
        {
            return;
        }

        try
        {
            _hub.Publish(payload);
        }
        catch (ObjectDisposedException)
        {
            Dispose();
        }
    }
}
