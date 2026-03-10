using System;
using System.Diagnostics;

namespace Avalonia.Diagnostics.Services;

internal sealed class ExternalDiagnosticsViewerProcessLauncher : IExternalDiagnosticsViewerLauncher
{
    public bool TryLaunch(string command, string arguments, out string status)
    {
        var executable = (command ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(executable))
        {
            status = "Viewer launch failed: command is empty.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                status = "Viewer launch failed: process start returned null.";
                return false;
            }

            status = "Viewer launched (" + executable + ").";
            return true;
        }
        catch (Exception ex)
        {
            status = "Viewer launch failed: " + ex.Message;
            return false;
        }
    }
}
