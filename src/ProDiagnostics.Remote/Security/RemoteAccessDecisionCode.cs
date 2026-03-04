namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Classifies access-policy decisions for transport-specific rejection handling.
/// </summary>
public enum RemoteAccessDecisionCode
{
    Allowed = 0,
    Forbidden = 1,
    Unauthorized = 2,
}
