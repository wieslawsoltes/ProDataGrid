using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines named-pipe transport settings for remote attach server.
/// </summary>
public readonly record struct NamedPipeAttachServerOptions(
    string PipeName,
    int MaxServerInstances,
    TimeSpan ReceiveTimeout,
    bool CurrentUserOnly,
    AttachServerOptions ServerOptions,
    RemoteAccessPolicyOptions AccessPolicy)
{
    /// <summary>
    /// Gets default named-pipe attach options.
    /// </summary>
    public static NamedPipeAttachServerOptions Default =>
        new(
            PipeName: "prodiagnostics.attach",
            MaxServerInstances: 16,
            ReceiveTimeout: TimeSpan.FromSeconds(30),
            CurrentUserOnly: true,
            ServerOptions: RemoteProtocol.DefaultServerOptions,
            AccessPolicy: RemoteAccessPolicyOptions.Default);

    /// <summary>
    /// Gets a value indicating whether the current platform supports named pipes.
    /// </summary>
    public static bool IsPlatformSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Returns normalized options safe for runtime usage.
    /// </summary>
    public static NamedPipeAttachServerOptions Normalize(in NamedPipeAttachServerOptions options)
    {
        var pipeName = NormalizePipeName(options.PipeName);
        var maxServerInstances = options.MaxServerInstances <= 0
            ? Default.MaxServerInstances
            : Math.Min(options.MaxServerInstances, 254);
        var receiveTimeout = options.ReceiveTimeout <= TimeSpan.Zero
            ? Default.ReceiveTimeout
            : options.ReceiveTimeout;
        var currentUserOnly = options.CurrentUserOnly;

        return new NamedPipeAttachServerOptions(
            PipeName: pipeName,
            MaxServerInstances: maxServerInstances,
            ReceiveTimeout: receiveTimeout,
            CurrentUserOnly: currentUserOnly,
            ServerOptions: AttachServerOptions.Normalize(options.ServerOptions),
            AccessPolicy: RemoteAccessPolicyOptions.Normalize(options.AccessPolicy));
    }

    private static string NormalizePipeName(string? pipeName)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return Default.PipeName;
        }

        var normalized = pipeName.Trim();
        if (normalized.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
        {
            throw new ArgumentException("Pipe name must not contain path separator or colon characters.", nameof(pipeName));
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var maxNameLength = GetMaxUnixPipeNameLength();
            if (normalized.Length > maxNameLength)
            {
                throw new ArgumentException(
                    "Pipe name exceeds Unix domain socket path constraints for current environment. " +
                    "Maximum supported length is " + maxNameLength + " characters.",
                    nameof(pipeName));
            }
        }

        return normalized;
    }

    private static int GetMaxUnixPipeNameLength()
    {
        const int unixSocketPathLimit = 104;
        const int socketPrefixLength = 11; // "CoreFxPipe_"
        var tempPathLength = Path.GetTempPath().Length;
        var available = unixSocketPathLimit - socketPrefixLength - tempPathLength;
        return Math.Max(1, available);
    }
}
