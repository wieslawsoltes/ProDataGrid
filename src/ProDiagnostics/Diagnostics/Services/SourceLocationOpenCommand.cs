using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace Avalonia.Diagnostics.Services
{
    internal static class SourceLocationOpenCommand
    {
        public static ICommand Instance { get; } = new DelegateCommand(Execute, CanExecute);

        private static bool CanExecute(object? parameter)
        {
            return TryParse(parameter, out _, out _);
        }

        private static void Execute(object? parameter)
        {
            if (!TryParse(parameter, out var filePath, out var line))
            {
                return;
            }

            if (TryOpenWithCommand("code", BuildCodeArguments(filePath, line)))
            {
                return;
            }

            if (TryOpenWithCommand("rider", BuildRiderArguments(filePath, line)))
            {
                return;
            }

            if (TryOpenWithCommand("idea", BuildRiderArguments(filePath, line)))
            {
                return;
            }

            TryOpenWithShell(filePath);
        }

        private static bool TryParse(object? parameter, out string filePath, out int line)
        {
            filePath = string.Empty;
            line = 0;

            if (parameter is not string text || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();

            // Strip optional labels like "XAML: " / "C#: ".
            var labelSeparator = trimmed.IndexOf(": ", StringComparison.Ordinal);
            if (labelSeparator > 0)
            {
                var prefix = trimmed.Substring(0, labelSeparator);
                if (prefix.IndexOf(Path.DirectorySeparatorChar) < 0 &&
                    prefix.IndexOf(Path.AltDirectorySeparatorChar) < 0)
                {
                    trimmed = trimmed.Substring(labelSeparator + 2);
                }
            }

            var methodMarker = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
            var locationPart = methodMarker > 0 ? trimmed.Substring(0, methodMarker) : trimmed;
            locationPart = locationPart.Trim();

            var end = locationPart.Length - 1;
            while (end >= 0 && char.IsWhiteSpace(locationPart[end]))
            {
                end--;
            }

            var cursor = end;
            while (cursor >= 0 && char.IsDigit(locationPart[cursor]))
            {
                cursor--;
            }

            if (cursor >= 0 && cursor < end && locationPart[cursor] == ':')
            {
                if (!int.TryParse(locationPart.Substring(cursor + 1, end - cursor), out line))
                {
                    line = 0;
                }

                filePath = locationPart.Substring(0, cursor).Trim();
            }
            else
            {
                filePath = locationPart;
            }

            filePath = filePath.Trim('"');

            if (filePath.Length == 0)
            {
                return false;
            }

            if (!Path.IsPathRooted(filePath) && !OperatingSystem.IsWindows())
            {
                var rootedCandidate = "/" + filePath.TrimStart('/');
                if (File.Exists(rootedCandidate))
                {
                    filePath = rootedCandidate;
                }
            }

            return true;
        }

        private static string BuildCodeArguments(string filePath, int line)
        {
            return line > 0
                ? "--goto \"" + filePath + ":" + line + "\""
                : "\"" + filePath + "\"";
        }

        private static string BuildRiderArguments(string filePath, int line)
        {
            return line > 0
                ? "--line " + line + " \"" + filePath + "\""
                : "\"" + filePath + "\"";
        }

        private static bool TryOpenWithCommand(string command, string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return process is not null;
            }
            catch
            {
                return false;
            }
        }

        private static void TryOpenWithShell(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Best-effort open only; ignore failures.
            }
        }

        private sealed class DelegateCommand : ICommand
        {
            private readonly Action<object?> _execute;
            private readonly Func<object?, bool> _canExecute;

            public DelegateCommand(Action<object?> execute, Func<object?, bool> canExecute)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object? parameter)
            {
                return _canExecute(parameter);
            }

            public void Execute(object? parameter)
            {
                _execute(parameter);
            }

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}
