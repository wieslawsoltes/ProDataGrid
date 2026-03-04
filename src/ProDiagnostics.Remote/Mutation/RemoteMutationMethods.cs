namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Defines method names for mutable diagnostics remote API.
/// </summary>
public static class RemoteMutationMethods
{
    public const string InspectHovered = "diagnostics.inspect.hovered";
    public const string SelectionSet = "diagnostics.selection.set";
    public const string PreviewPausedSet = "diagnostics.preview.paused.set";
    public const string PreviewSettingsSet = "diagnostics.preview.settings.set";
    public const string PreviewInputInject = "diagnostics.preview.input.inject";

    public const string PropertiesSet = "diagnostics.properties.set";
    public const string PseudoClassSet = "diagnostics.state.pseudoclass.set";
    public const string Elements3DRootSet = "diagnostics.elements3d.root.set";
    public const string Elements3DRootReset = "diagnostics.elements3d.root.reset";
    public const string Elements3DFiltersSet = "diagnostics.elements3d.filters.set";
    public const string OverlayOptionsSet = "diagnostics.overlay.options.set";
    public const string OverlayLiveHoverSet = "diagnostics.overlay.live-hover.set";
    public const string CodeDocumentOpen = "diagnostics.code.document.open";

    public const string BreakpointsPropertyAdd = "diagnostics.breakpoints.property.add";
    public const string BreakpointsEventAdd = "diagnostics.breakpoints.event.add";
    public const string BreakpointsRemove = "diagnostics.breakpoints.remove";
    public const string BreakpointsToggle = "diagnostics.breakpoints.toggle";
    public const string BreakpointsClear = "diagnostics.breakpoints.clear";
    public const string BreakpointsEnabledSet = "diagnostics.breakpoints.enabled.set";

    public const string EventsClear = "diagnostics.events.clear";
    public const string EventsNodeEnabledSet = "diagnostics.events.node.enabled.set";
    public const string EventsDefaultsEnable = "diagnostics.events.defaults.enable";
    public const string EventsDisableAll = "diagnostics.events.disable-all";

    public const string LogsClear = "diagnostics.logs.clear";
    public const string LogsLevelsSet = "diagnostics.logs.levels.set";

    public const string StreamDemandSet = "diagnostics.stream.demand.set";
    public const string MetricsPausedSet = "diagnostics.metrics.paused.set";
    public const string MetricsSettingsSet = "diagnostics.metrics.settings.set";
    public const string ProfilerPausedSet = "diagnostics.profiler.paused.set";
    public const string ProfilerSettingsSet = "diagnostics.profiler.settings.set";
}
