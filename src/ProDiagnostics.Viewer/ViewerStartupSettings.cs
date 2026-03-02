namespace ProDiagnostics.Viewer;

internal static class ViewerStartupSettings
{
    public static int? PortOverride { get; private set; }

    public static bool StartListening { get; private set; } = true;

    public static string? TargetAppName { get; private set; }

    public static string? TargetProcessName { get; private set; }

    public static int? TargetProcessId { get; private set; }

    public static void ApplyFromArgs(string[]? args)
    {
        PortOverride = null;
        StartListening = true;
        TargetAppName = null;
        TargetProcessName = null;
        TargetProcessId = null;

        if (args is null || args.Length == 0)
        {
            return;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (argument.Equals("--no-listen", System.StringComparison.OrdinalIgnoreCase))
            {
                StartListening = false;
                continue;
            }

            if (argument.Equals("--listen", System.StringComparison.OrdinalIgnoreCase))
            {
                StartListening = true;
                continue;
            }

            if (TryReadTextOption(argument, "--app", args, ref i, out var appName) ||
                TryReadTextOption(argument, "--app-name", args, ref i, out appName))
            {
                TargetAppName = NormalizeText(appName);
                continue;
            }

            if (TryReadTextOption(argument, "--process", args, ref i, out var processName) ||
                TryReadTextOption(argument, "--process-name", args, ref i, out processName))
            {
                TargetProcessName = NormalizeText(processName);
                continue;
            }

            if (TryReadIntOption(argument, "--pid", args, ref i, out var processId) ||
                TryReadIntOption(argument, "--process-id", args, ref i, out processId))
            {
                TargetProcessId = processId > 0 ? processId : null;
                continue;
            }

            if (argument.StartsWith("--port=", System.StringComparison.OrdinalIgnoreCase))
            {
                TryApplyPort(argument["--port=".Length..]);
                continue;
            }

            if (argument.Equals("--port", System.StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                TryApplyPort(args[++i]);
            }
        }
    }

    private static void TryApplyPort(string? value)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return;
        }

        if (parsed is < 1 or > 65535)
        {
            return;
        }

        PortOverride = parsed;
    }

    private static bool TryReadTextOption(string argument, string optionName, string[] args, ref int index, out string? value)
    {
        value = null;

        var prefix = optionName + "=";
        if (argument.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            return true;
        }

        if (!argument.Equals(optionName, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return true;
        }

        value = args[++index];
        return true;
    }

    private static bool TryReadIntOption(string argument, string optionName, string[] args, ref int index, out int value)
    {
        value = 0;
        if (!TryReadTextOption(argument, optionName, args, ref index, out var text))
        {
            return false;
        }

        if (int.TryParse(text, out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }

        value = 0;
        return true;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
