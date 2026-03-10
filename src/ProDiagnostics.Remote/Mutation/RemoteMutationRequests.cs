using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Request to inspect control currently hovered in inspected app.
/// </summary>
public sealed record class RemoteInspectHoveredRequest
{
    public string Scope { get; init; } = "combined";
    public bool RequireInspectGesture { get; init; } = true;
    public bool IncludeDevTools { get; init; }
}

/// <summary>
/// Request to update canonical diagnostics selection.
/// </summary>
public sealed record class RemoteSetSelectionRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
}

/// <summary>
/// Request to pause or resume preview capture/stream emission.
/// </summary>
public sealed record class RemoteSetPreviewPausedRequest
{
    public bool IsPaused { get; init; }
}

/// <summary>
/// Request to update preview capture settings.
/// </summary>
public sealed record class RemoteSetPreviewSettingsRequest
{
    public string? Transport { get; init; }
    public int? MaxWidth { get; init; }
    public int? MaxHeight { get; init; }
    public double? Scale { get; init; }
    public int? TargetFps { get; init; }
    public bool? EnableDiff { get; init; }
    public bool? IncludeFrameData { get; init; }
}

/// <summary>
/// Request to inject input event into preview target.
/// </summary>
public sealed record class RemotePreviewInputRequest
{
    public string EventType { get; init; } = "pointer_move";
    public double X { get; init; }
    public double Y { get; init; }
    public int? FrameWidth { get; init; }
    public int? FrameHeight { get; init; }
    public string? Button { get; init; }
    public int ClickCount { get; init; } = 1;
    public double DeltaX { get; init; }
    public double DeltaY { get; init; }
    public string? Key { get; init; }
    public string? Text { get; init; }
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }
    public bool Meta { get; init; }
}

/// <summary>
/// Request to set an Avalonia or CLR property value on a target control/object.
/// </summary>
public sealed record class RemoteSetPropertyRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public string PropertyName { get; init; } = string.Empty;
    public string? PropertyKind { get; init; }
    public string? PropertyDeclaringType { get; init; }
    public string? ValueText { get; init; }
    public bool ValueIsNull { get; init; }
    public bool ClearValue { get; init; }
}

/// <summary>
/// Request to set pseudo-class state on a target styled element.
/// </summary>
public sealed record class RemoteSetPseudoClassRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public string PseudoClass { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

/// <summary>
/// Request to scope Elements 3D view to a selected visual target.
/// </summary>
public sealed record class RemoteSetElements3DRootRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
}

/// <summary>
/// Request to update Elements 3D filters and projection settings.
/// </summary>
public sealed record class RemoteSetElements3DFiltersRequest
{
    public bool? ShowInvisibleNodes { get; init; }
    public bool? ShowExploded3DView { get; init; }
    public bool? ShowAllLayersInGrid { get; init; }
    public double? DepthSpacing { get; init; }
    public int? Flat2DMaxLayersPerRow { get; init; }
    public double? Tilt { get; init; }
    public double? Zoom { get; init; }
    public double? OrbitYaw { get; init; }
    public double? OrbitPitch { get; init; }
    public double? OrbitRoll { get; init; }
    public int? MinVisibleDepth { get; init; }
    public int? MaxVisibleDepth { get; init; }
    public int? MaxVisibleElements { get; init; }
    public bool ResetProjectionView { get; init; }
    public bool ResetLayerVisibilityFilters { get; init; }
}

/// <summary>
/// Request to update visual inspection overlay options.
/// </summary>
public sealed record class RemoteSetOverlayOptionsRequest
{
    public bool? VisualizeMarginPadding { get; init; }
    public bool? ShowInfo { get; init; }
    public bool? ShowRulers { get; init; }
    public bool? ShowExtensionLines { get; init; }
    public bool? HighlightElements { get; init; }
    public bool? ClipToTargetBounds { get; init; }
}

/// <summary>
/// Request to enable or disable live-hover overlay behavior.
/// </summary>
public sealed record class RemoteSetOverlayLiveHoverRequest
{
    public bool IsEnabled { get; init; }
}

/// <summary>
/// Request to add a property breakpoint for a target object.
/// </summary>
public sealed record class RemoteAddPropertyBreakpointRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public string PropertyName { get; init; } = string.Empty;
}

/// <summary>
/// Request to add an event breakpoint globally or for a target object.
/// </summary>
public sealed record class RemoteAddEventBreakpointRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string? EventOwnerType { get; init; }
    public bool IsGlobal { get; init; }
}

/// <summary>
/// Request to remove one breakpoint entry by identifier.
/// </summary>
public sealed record class RemoteRemoveBreakpointRequest
{
    public string BreakpointId { get; init; } = string.Empty;
}

/// <summary>
/// Request to toggle or set one breakpoint entry enabled state.
/// </summary>
public sealed record class RemoteToggleBreakpointRequest
{
    public string BreakpointId { get; init; } = string.Empty;
    public bool? IsEnabled { get; init; }
}

/// <summary>
/// Request to toggle all existing breakpoints enabled/disabled.
/// </summary>
public sealed record class RemoteSetBreakpointsEnabledRequest
{
    public bool IsEnabled { get; init; }
}

/// <summary>
/// Request to set one event node enabled/disabled by stable id or event key.
/// </summary>
public sealed record class RemoteSetEventEnabledRequest
{
    public string EventId { get; init; } = string.Empty;
    public string? EventName { get; init; }
    public string? EventOwnerType { get; init; }
    public bool IsEnabled { get; init; }
}

/// <summary>
/// Request to update logs-page level filters and retention.
/// </summary>
public sealed record class RemoteSetLogLevelsRequest
{
    public bool? ShowVerbose { get; init; }
    public bool? ShowDebug { get; init; }
    public bool? ShowInformation { get; init; }
    public bool? ShowWarning { get; init; }
    public bool? ShowError { get; init; }
    public bool? ShowFatal { get; init; }
    public int? MaxEntries { get; init; }
}

/// <summary>
/// Request to pause or resume remote stream emission for a diagnostics domain.
/// </summary>
public sealed record class RemoteSetPausedRequest
{
    public bool IsPaused { get; init; }
}

/// <summary>
/// Request to update per-session stream topic demand hints.
/// </summary>
public sealed record class RemoteSetStreamDemandRequest
{
    public IReadOnlyList<string>? Topics { get; init; }
}

/// <summary>
/// Request to update metrics stream/snapshot settings.
/// </summary>
public sealed record class RemoteSetMetricsSettingsRequest
{
    public int? MaxRetainedMeasurements { get; init; }
    public int? MaxSeries { get; init; }
    public int? MaxSamplesPerSeries { get; init; }
}

/// <summary>
/// Request to update profiler stream/snapshot settings.
/// </summary>
public sealed record class RemoteSetProfilerSettingsRequest
{
    public int? MaxRetainedSamples { get; init; }
    public int? SampleIntervalMs { get; init; }
}

/// <summary>
/// Request to open source document in editor at specific location.
/// </summary>
public sealed record class RemoteCodeDocumentOpenRequest
{
    public string FilePath { get; init; } = string.Empty;
    public int Line { get; init; } = 1;
    public int Column { get; init; }
    public string? MethodName { get; init; }
}

/// <summary>
/// Empty request payload.
/// </summary>
public sealed record class RemoteEmptyMutationRequest
{
}
