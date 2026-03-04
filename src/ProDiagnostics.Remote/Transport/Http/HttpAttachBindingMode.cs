namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Describes host binding behavior for HTTP attach server.
/// </summary>
public enum HttpAttachBindingMode
{
    /// <summary>
    /// Bind only loopback endpoint (127.0.0.1).
    /// </summary>
    Localhost = 0,

    /// <summary>
    /// Bind an explicit IP address.
    /// </summary>
    ExplicitAddress = 1,

    /// <summary>
    /// Bind all interfaces.
    /// </summary>
    Any = 2,
}
