namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Query for preview capability retrieval.
/// </summary>
public sealed record class RemotePreviewCapabilitiesRequest
{
}

/// <summary>
/// Query for live preview frame retrieval.
/// </summary>
public sealed record class RemotePreviewSnapshotRequest
{
    public string Transport { get; init; } = "svg";
    public string? PreviousFrameHash { get; init; }
    public bool IncludeFrameData { get; init; } = true;
    public bool EnableDiff { get; init; } = true;
    public int MaxWidth { get; init; } = 1920;
    public int MaxHeight { get; init; } = 1080;
    public double Scale { get; init; } = 1d;
}

/// <summary>
/// Query for tree snapshot retrieval.
/// </summary>
public sealed record class RemoteTreeSnapshotRequest
{
    public string Scope { get; init; } = "combined";
    public bool IncludeSourceLocations { get; init; }
    public bool IncludeVisualDetails { get; init; }
}

/// <summary>
/// Query for canonical diagnostics selection retrieval.
/// </summary>
public sealed record class RemoteSelectionSnapshotRequest
{
    public string Scope { get; init; } = "combined";
}

/// <summary>
/// Query for property/value-frame snapshot retrieval.
/// </summary>
public sealed record class RemotePropertiesSnapshotRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public bool IncludeClrProperties { get; init; } = true;
}

/// <summary>
/// Query for Elements 3D diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteElements3DSnapshotRequest
{
    public bool IncludeNodes { get; init; } = true;
    public bool IncludeVisibleNodeIds { get; init; } = true;
    public bool IncludeSvgSnapshot { get; init; }
    public int SvgWidth { get; init; } = 1600;
    public int SvgHeight { get; init; } = 900;
    public int MaxSvgNodes { get; init; } = 2000;
}

/// <summary>
/// Query for overlay options snapshot retrieval.
/// </summary>
public sealed record class RemoteOverlayOptionsSnapshotRequest
{
}

/// <summary>
/// Query for code/XAML source documents of inspected target.
/// </summary>
public sealed record class RemoteCodeDocumentsRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
}

/// <summary>
/// Query to resolve tree node from source caret position.
/// </summary>
public sealed record class RemoteCodeResolveNodeRequest
{
    public string Scope { get; init; } = "combined";
    public string FilePath { get; init; } = string.Empty;
    public int Line { get; init; } = 1;
    public int Column { get; init; }
}

/// <summary>
/// Query for view-model/bindings diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteBindingsSnapshotRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
}

/// <summary>
/// Query for styles diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteStylesSnapshotRequest
{
    public string Scope { get; init; } = "combined";
    public string? NodeId { get; init; }
    public string? NodePath { get; init; }
    public string? ControlName { get; init; }
    public bool IncludeTreeEntries { get; init; } = true;
    public bool IncludeFrames { get; init; } = true;
    public bool IncludeSetters { get; init; } = true;
    public bool IncludeResolution { get; init; } = true;
}

/// <summary>
/// Query for resources diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteResourcesSnapshotRequest
{
    public bool IncludeEntries { get; init; } = true;
}

/// <summary>
/// Query for assets diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteAssetsSnapshotRequest
{
}

/// <summary>
/// Query for events diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteEventsSnapshotRequest
{
    public string Scope { get; init; } = "combined";
    public bool IncludeRecordedEvents { get; init; } = true;
}

/// <summary>
/// Query for breakpoints diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteBreakpointsSnapshotRequest
{
    public string Scope { get; init; } = "combined";
}

/// <summary>
/// Query for logs diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteLogsSnapshotRequest
{
    public bool IncludeEntries { get; init; } = true;
}

/// <summary>
/// Query for metrics diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteMetricsSnapshotRequest
{
    public bool IncludeSeries { get; init; } = true;
    public bool IncludeMeasurements { get; init; } = true;
}

/// <summary>
/// Query for profiler diagnostics snapshot retrieval.
/// </summary>
public sealed record class RemoteProfilerSnapshotRequest
{
    public bool IncludeSamples { get; init; } = true;
}
