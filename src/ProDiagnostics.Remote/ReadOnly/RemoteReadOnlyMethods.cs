namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines method names for read-only diagnostics remote API.
/// </summary>
public static class RemoteReadOnlyMethods
{
    public const string PreviewCapabilitiesGet = "diagnostics.preview.capabilities.get";
    public const string PreviewSnapshotGet = "diagnostics.preview.snapshot.get";
    public const string TreeSnapshotGet = "diagnostics.tree.snapshot.get";
    public const string SelectionGet = "diagnostics.selection.get";
    public const string PropertiesSnapshotGet = "diagnostics.properties.snapshot.get";
    public const string Elements3DSnapshotGet = "diagnostics.elements3d.snapshot.get";
    public const string OverlayOptionsGet = "diagnostics.overlay.options.get";
    public const string CodeDocumentsGet = "diagnostics.code.documents.get";
    public const string CodeResolveNode = "diagnostics.code.resolve-node";
    public const string BindingsSnapshotGet = "diagnostics.bindings.snapshot.get";
    public const string StylesSnapshotGet = "diagnostics.styles.snapshot.get";
    public const string ResourcesSnapshotGet = "diagnostics.resources.snapshot.get";
    public const string AssetsSnapshotGet = "diagnostics.assets.snapshot.get";
    public const string EventsSnapshotGet = "diagnostics.events.snapshot.get";
    public const string BreakpointsSnapshotGet = "diagnostics.breakpoints.snapshot.get";
    public const string LogsSnapshotGet = "diagnostics.logs.snapshot.get";
    public const string MetricsSnapshotGet = "diagnostics.metrics.snapshot.get";
    public const string ProfilerSnapshotGet = "diagnostics.profiler.snapshot.get";
}
