namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// No-op diagnostics logger used when structured backend logging is disabled.
/// </summary>
public sealed class NoOpRemoteDiagnosticsLogger : IRemoteDiagnosticsLogger
{
    private NoOpRemoteDiagnosticsLogger()
    {
    }

    public static NoOpRemoteDiagnosticsLogger Instance { get; } = new();

    public void Log(in RemoteDiagnosticsLogEntry entry)
    {
    }
}
