using System;
using Avalonia;
using Avalonia.Diagnostics;

namespace ProDiagnostics.DevTools;

internal static class DevToolsStartupSettings
{
    private static readonly Uri DefaultEndpoint = new("ws://127.0.0.1:29414/attach/", UriKind.Absolute);

    public static Uri RemoteEndpoint { get; private set; } = DefaultEndpoint;

    public static bool ConnectOnStartup { get; private set; } = true;

    public static DevToolsViewKind LaunchView { get; private set; } = DevToolsViewKind.CombinedTree;

    public static string? ApplicationName { get; private set; }

    private static double? Width { get; set; }

    private static double? Height { get; set; }

    public static double? StartupWidth => Width;

    public static double? StartupHeight => Height;

    public static void ApplyFromArgs(string[]? args)
    {
        RemoteEndpoint = DefaultEndpoint;
        ConnectOnStartup = true;
        LaunchView = DevToolsViewKind.CombinedTree;
        ApplicationName = null;
        Width = null;
        Height = null;

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

            if (argument.Equals("--connect", StringComparison.OrdinalIgnoreCase))
            {
                ConnectOnStartup = true;
                continue;
            }

            if (argument.Equals("--no-connect", StringComparison.OrdinalIgnoreCase))
            {
                ConnectOnStartup = false;
                continue;
            }

            if (TryReadTextOption(argument, "--endpoint", args, ref i, out var endpoint) ||
                TryReadTextOption(argument, "--url", args, ref i, out endpoint) ||
                TryReadTextOption(argument, "--ws", args, ref i, out endpoint))
            {
                if (TryParseEndpoint(endpoint, out var uri))
                {
                    RemoteEndpoint = uri;
                }

                continue;
            }

            if (TryReadTextOption(argument, "--app", args, ref i, out var appName) ||
                TryReadTextOption(argument, "--app-name", args, ref i, out appName))
            {
                ApplicationName = NormalizeText(appName);
                continue;
            }

            if (TryReadDoubleOption(argument, "--width", args, ref i, out var width))
            {
                Width = width > 0 ? width : null;
                continue;
            }

            if (TryReadDoubleOption(argument, "--height", args, ref i, out var height))
            {
                Height = height > 0 ? height : null;
                continue;
            }

            if (TryReadTextOption(argument, "--size", args, ref i, out var sizeText))
            {
                ApplySizeText(sizeText);
                continue;
            }

            if (TryReadTextOption(argument, "--view", args, ref i, out var viewText) &&
                TryParseView(viewText, out var view))
            {
                LaunchView = view;
            }
        }
    }

    public static DevToolsOptions CreateOptions()
    {
        return CreateOptions(
            RemoteEndpoint,
            ApplicationName,
            LaunchView,
            Width,
            Height,
            connectOnStartup: ConnectOnStartup);
    }

    public static DevToolsOptions CreateOptions(
        Uri remoteEndpoint,
        string? applicationName,
        DevToolsViewKind launchView,
        double? width,
        double? height,
        bool connectOnStartup)
    {
        var options = new DevToolsOptions
        {
            UseRemoteRuntime = true,
            RemoteRuntimeEndpoint = remoteEndpoint,
            ConnectOnStartup = connectOnStartup,
            ShowAsChildWindow = false,
            EnableRemoteGesture = false,
            LaunchView = launchView,
            ApplicationName = NormalizeText(applicationName),
            DisableLocalFallbackInRemoteRuntime = true,
        };

        if (width is > 0 && height is > 0)
        {
            options.Size = new Size(width.Value, height.Value);
        }

        return options;
    }

    public static bool TryParseEndpointText(string? text, out Uri endpoint)
    {
        return TryParseEndpoint(text, out endpoint);
    }

    private static bool TryParseView(string? text, out DevToolsViewKind view)
    {
        view = DevToolsViewKind.CombinedTree;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Enum.TryParse(text.Trim(), ignoreCase: true, out view);
    }

    private static void ApplySizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var separators = new[] { 'x', 'X', ',', ';' };
        var parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return;
        }

        if (double.TryParse(parts[0], out var width) && width > 0)
        {
            Width = width;
        }

        if (double.TryParse(parts[1], out var height) && height > 0)
        {
            Height = height;
        }
    }

    private static bool TryParseEndpoint(string? text, out Uri uri)
    {
        uri = DefaultEndpoint;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "ws://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var builder = new UriBuilder(parsed);
        if (builder.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = "ws";
            if (builder.Port == 80)
            {
                builder.Port = 80;
            }
        }
        else if (builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            builder.Scheme = "wss";
            if (builder.Port == 443)
            {
                builder.Port = 443;
            }
        }

        if (!builder.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) &&
            !builder.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(builder.Path) || builder.Path == "/")
        {
            builder.Path = "/attach/";
        }

        uri = builder.Uri;
        return true;
    }

    private static bool TryReadTextOption(string argument, string optionName, string[] args, ref int index, out string? value)
    {
        value = null;

        var prefix = optionName + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..];
            return true;
        }

        if (!argument.Equals(optionName, StringComparison.OrdinalIgnoreCase))
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

    private static bool TryReadDoubleOption(string argument, string optionName, string[] args, ref int index, out double value)
    {
        value = 0;
        if (!TryReadTextOption(argument, optionName, args, ref index, out var text))
        {
            return false;
        }

        if (double.TryParse(text, out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }

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
