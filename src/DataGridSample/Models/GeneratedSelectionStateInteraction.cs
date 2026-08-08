// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace DataGridSample.Models;

/// <summary>Identifies an operation handled at the generated view boundary.</summary>
public enum GeneratedSelectionStateOperation
{
    /// <summary>Captures the current version of the complete grid state.</summary>
    Capture,

    /// <summary>Captures a payload shaped like the legacy version-one schema.</summary>
    CaptureLegacyV1,

    /// <summary>Changes state so that restoration can be demonstrated.</summary>
    Scramble,

    /// <summary>Restores a previously captured payload.</summary>
    Restore
}

/// <summary>Requests a generated selection/state view operation.</summary>
public sealed record GeneratedSelectionStateRequest(
    GeneratedSelectionStateOperation Operation,
    string? Payload = null);

/// <summary>Reports the result of a generated selection/state view operation.</summary>
public sealed record GeneratedSelectionStateResult(
    string? Payload,
    string Message,
    int ColumnCount,
    int SelectedKeyCount);
