namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Enumerates protocol message kinds exchanged over remote attach channels.
/// </summary>
public enum RemoteMessageKind : byte
{
    Hello = 1,
    HelloAck = 2,
    HelloReject = 3,
    KeepAlive = 4,
    Disconnect = 5,
    Request = 6,
    Response = 7,
    Stream = 8,
    Error = 9,
}
