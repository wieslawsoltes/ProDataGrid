using System;
using System.Net;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines HTTP transport settings for remote attach server.
/// </summary>
public readonly record struct HttpAttachServerOptions(
    int Port,
    HttpAttachBindingMode BindingMode,
    IPAddress? BindAddress,
    string Path,
    TimeSpan ReceiveTimeout,
    AttachServerOptions ServerOptions,
    RemoteAccessPolicyOptions AccessPolicy)
{
    /// <summary>
    /// Gets default HTTP attach options.
    /// </summary>
    public static HttpAttachServerOptions Default =>
        new(
            Port: 29414,
            BindingMode: HttpAttachBindingMode.Localhost,
            BindAddress: IPAddress.Loopback,
            Path: "/attach",
            ReceiveTimeout: TimeSpan.FromSeconds(30),
            ServerOptions: RemoteProtocol.DefaultServerOptions,
            AccessPolicy: RemoteAccessPolicyOptions.Default);

    /// <summary>
    /// Returns normalized options safe for runtime usage.
    /// </summary>
    public static HttpAttachServerOptions Normalize(in HttpAttachServerOptions options)
    {
        var port = options.Port is > 0 and <= 65535
            ? options.Port
            : Default.Port;

        var path = NormalizePath(options.Path);
        var receiveTimeout = options.ReceiveTimeout <= TimeSpan.Zero
            ? Default.ReceiveTimeout
            : options.ReceiveTimeout;

        var bindingMode = options.BindingMode;
        var bindAddress = options.BindAddress;
        if (bindingMode == HttpAttachBindingMode.ExplicitAddress && bindAddress is null)
        {
            bindingMode = HttpAttachBindingMode.Localhost;
            bindAddress = IPAddress.Loopback;
        }

        if (bindingMode == HttpAttachBindingMode.Localhost && bindAddress is null)
        {
            bindAddress = IPAddress.Loopback;
        }

        return new HttpAttachServerOptions(
            Port: port,
            BindingMode: bindingMode,
            BindAddress: bindAddress,
            Path: path,
            ReceiveTimeout: receiveTimeout,
            ServerOptions: AttachServerOptions.Normalize(options.ServerOptions),
            AccessPolicy: RemoteAccessPolicyOptions.Normalize(options.AccessPolicy));
    }

    /// <summary>
    /// Builds an <see cref="HttpListener"/> prefix for current options.
    /// </summary>
    public string BuildListenerPrefix()
    {
        var trimmed = TrimPathSlashes(Path);
        var pathPart = string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed + "/";
        return BindingMode switch
        {
            HttpAttachBindingMode.Localhost => "http://127.0.0.1:" + Port + "/" + pathPart,
            HttpAttachBindingMode.ExplicitAddress => "http://" + BindAddress + ":" + Port + "/" + pathPart,
            HttpAttachBindingMode.Any => "http://+:" + Port + "/" + pathPart,
            _ => "http://127.0.0.1:" + Port + "/" + pathPart,
        };
    }

    /// <summary>
    /// Builds a WebSocket URI suitable for local client connections.
    /// </summary>
    public Uri BuildClientWebSocketUri()
    {
        var host = BindingMode switch
        {
            HttpAttachBindingMode.ExplicitAddress when BindAddress is not null => BindAddress.ToString(),
            _ => "127.0.0.1",
        };

        var path = Path.EndsWith("/", StringComparison.Ordinal)
            ? Path
            : Path + "/";
        return new Uri("ws://" + host + ":" + Port + path, UriKind.Absolute);
    }

    internal bool MatchesRequestPath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        return string.Equals(
            NormalizePath(absolutePath),
            NormalizePath(Path),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Default.Path;
        }

        var normalized = path.Trim();
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static string TrimPathSlashes(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Trim('/');
    }
}
