namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Identifies protocol monitor event categories emitted by remote attach infrastructure.
/// </summary>
public enum RemoteProtocolEventType
{
    ConnectionAccepted = 1,
    ConnectionRejected = 2,
    ConnectionClosed = 3,
    MessageSent = 4,
    MessageReceived = 5,
    SendFailure = 6,
    ReceiveFailure = 7,
    StreamDropped = 8,
    StreamDispatchFailure = 9,
}
