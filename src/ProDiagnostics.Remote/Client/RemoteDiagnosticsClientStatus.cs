namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Connection lifecycle state of <see cref="IRemoteDiagnosticsClient"/>.
/// </summary>
public enum RemoteDiagnosticsClientStatus
{
    Offline = 0,
    Connecting = 1,
    Online = 2,
    Disconnecting = 3,
}
