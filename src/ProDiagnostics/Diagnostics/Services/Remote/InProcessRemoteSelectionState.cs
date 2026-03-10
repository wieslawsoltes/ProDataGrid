using System;
using System.Collections.Generic;
using Avalonia.Diagnostics.Remote;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteSelectionState
{
    private const int SnapshotVersion = 1;

    private readonly object _gate = new();
    private readonly Dictionary<string, SelectionState> _states = new(StringComparer.Ordinal);
    private long _generation;

    public event Action<RemoteSelectionSnapshot>? SelectionChanged;

    public RemoteSelectionSnapshot GetSnapshot(string? scope)
    {
        var normalizedScope = NormalizeScope(scope);
        lock (_gate)
        {
            if (_states.TryGetValue(normalizedScope, out var state))
            {
                return state.ToSnapshot(normalizedScope);
            }

            return new RemoteSelectionSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: _generation,
                Scope: normalizedScope,
                NodeId: null,
                NodePath: null,
                Target: null,
                TargetType: null);
        }
    }

    public RemoteSelectionSnapshot SetSelection(
        string? scope,
        string? nodeId,
        string? nodePath,
        string? target,
        string? targetType)
    {
        var normalizedScope = NormalizeScope(scope);
        RemoteSelectionSnapshot snapshot;
        Action<RemoteSelectionSnapshot>? changedHandlers = null;

        lock (_gate)
        {
            _states.TryGetValue(normalizedScope, out var currentState);
            var changed =
                !string.Equals(currentState.NodeId, nodeId, StringComparison.Ordinal) ||
                !string.Equals(currentState.NodePath, nodePath, StringComparison.Ordinal) ||
                !string.Equals(currentState.Target, target, StringComparison.Ordinal) ||
                !string.Equals(currentState.TargetType, targetType, StringComparison.Ordinal);

            if (changed)
            {
                _generation++;
                currentState = new SelectionState(
                    Generation: _generation,
                    NodeId: nodeId,
                    NodePath: nodePath,
                    Target: target,
                    TargetType: targetType);
                _states[normalizedScope] = currentState;
                changedHandlers = SelectionChanged;
            }

            snapshot = currentState.ToSnapshot(normalizedScope);
        }

        changedHandlers?.Invoke(snapshot);
        return snapshot;
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.Equals(scope, "logical", StringComparison.OrdinalIgnoreCase))
        {
            return "logical";
        }

        if (string.Equals(scope, "visual", StringComparison.OrdinalIgnoreCase))
        {
            return "visual";
        }

        return "combined";
    }

    private readonly record struct SelectionState(
        long Generation,
        string? NodeId,
        string? NodePath,
        string? Target,
        string? TargetType)
    {
        public RemoteSelectionSnapshot ToSnapshot(string scope)
        {
            return new RemoteSelectionSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: Generation,
                Scope: scope,
                NodeId: NodeId,
                NodePath: NodePath,
                Target: Target,
                TargetType: TargetType);
        }
    }
}
