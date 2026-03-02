namespace Avalonia.Diagnostics.ViewModels;

internal sealed class StyleResolutionTraceEntryViewModel
{
    public StyleResolutionTraceEntryViewModel(
        int order,
        int hostLevel,
        string host,
        string hostKind,
        string propagationScope,
        int logicalDistance,
        int visualDistance,
        bool stylesInitialized,
        string style,
        string styleKind,
        string selector,
        string path,
        int appliedCount,
        int activeCount,
        string notes)
    {
        Order = order;
        HostLevel = hostLevel;
        Host = host;
        HostKind = hostKind;
        PropagationScope = propagationScope;
        LogicalDistance = logicalDistance;
        VisualDistance = visualDistance;
        StylesInitialized = stylesInitialized;
        Style = style;
        StyleKind = styleKind;
        Selector = selector;
        Path = path;
        AppliedCount = appliedCount;
        ActiveCount = activeCount;
        Notes = notes;
    }

    public int Order { get; }

    public int HostLevel { get; }

    public string Host { get; }

    public string HostKind { get; }

    public string PropagationScope { get; }

    public int LogicalDistance { get; }

    public int VisualDistance { get; }

    public bool StylesInitialized { get; }

    public string Style { get; }

    public string StyleKind { get; }

    public string Selector { get; }

    public string Path { get; }

    public int AppliedCount { get; }

    public int ActiveCount { get; }

    public string AppliedSummary => ActiveCount + "/" + AppliedCount;

    public string Notes { get; }
}
