using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Avalonia.Diagnostics.Services
{
    internal static class SourceLocationOpenCommand
    {
        public static ICommand Instance { get; } = new DelegateCommand(Execute, CanExecute);

        private static bool CanExecute(object? parameter)
        {
            return TryParse(parameter, out _);
        }

        private static void Execute(object? parameter)
        {
            if (!TryParse(parameter, out var location))
            {
                return;
            }

            var filePath = location.FilePath;
            var line = location.Line;
            var column = location.Column;

            if (TryOpenWithCommand("code", BuildCodeArguments(filePath, line, column)))
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

        private static bool TryParse(object? parameter, out SourceDocumentLocation location)
        {
            location = default!;

            if (parameter is SourceDocumentLocation parsedLocation &&
                !string.IsNullOrWhiteSpace(parsedLocation.FilePath))
            {
                location = parsedLocation;
                return true;
            }

            if (parameter is string text && SourceLocationTextParser.TryParse(text, out var parsedTextLocation))
            {
                location = parsedTextLocation;
                return true;
            }

            return false;
        }

        private static string BuildCodeArguments(string filePath, int line, int column)
        {
            return line > 0 && column > 0
                ? "--goto \"" + filePath + ":" + line + ":" + column + "\""
                : line > 0
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
