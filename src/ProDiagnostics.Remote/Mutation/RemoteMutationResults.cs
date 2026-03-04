namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Result payload for mutation/control commands.
/// </summary>
public sealed record class RemoteMutationResult(
    string Operation,
    bool Changed,
    string Message,
    string? Target = null,
    string? TargetNodePath = null,
    int? AffectedCount = null);
