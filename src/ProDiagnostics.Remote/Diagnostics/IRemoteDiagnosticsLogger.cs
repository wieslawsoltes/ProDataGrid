namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Structured diagnostics logger used by remote attach backend internals.
/// </summary>
public interface IRemoteDiagnosticsLogger
{
    void Log(in RemoteDiagnosticsLogEntry entry);
}
