namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Represents an access-policy evaluation result.
/// </summary>
public readonly record struct RemoteAccessDecision(
    bool IsAllowed,
    RemoteAccessDecisionCode Code,
    string Message)
{
    /// <summary>
    /// Gets an allow decision.
    /// </summary>
    public static RemoteAccessDecision Allow(string message = "Allowed") =>
        new(true, RemoteAccessDecisionCode.Allowed, message);

    /// <summary>
    /// Gets a forbidden decision.
    /// </summary>
    public static RemoteAccessDecision Forbid(string message) =>
        new(false, RemoteAccessDecisionCode.Forbidden, message);

    /// <summary>
    /// Gets an unauthorized decision.
    /// </summary>
    public static RemoteAccessDecision Unauthorized(string message) =>
        new(false, RemoteAccessDecisionCode.Unauthorized, message);
}
