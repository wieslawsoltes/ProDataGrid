namespace Avalonia.Diagnostics.Services;

internal interface IExternalDiagnosticsViewerLauncher
{
    bool TryLaunch(string command, string arguments, out string status);
}
