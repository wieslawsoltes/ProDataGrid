using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class StylesDiagnosticsPageViewModel : ViewModelBase, IDisposable
{
    private readonly AvaloniaList<StylesTreeEntryViewModel> _treeEntries = new();
    private readonly AvaloniaList<ValueFrameViewModel> _frames = new();
    private readonly AvaloniaList<SetterViewModel> _setters = new();
    private readonly AvaloniaList<PseudoClassViewModel> _pseudoClasses = new();
    private readonly AvaloniaList<StyleResolutionTraceEntryViewModel> _resolutionEntries = new();
    private readonly DataGridCollectionView _treeEntriesView;
    private readonly DataGridCollectionView _framesView;
    private readonly DataGridCollectionView _settersView;
    private readonly DataGridCollectionView _resolutionEntriesView;
    private readonly Func<AvaloniaObject?>? _selectedObjectAccessor;
    private AvaloniaObject? _inspectedObject;
    private StylesTreeEntryViewModel? _selectedTreeEntry;
    private ValueFrameViewModel? _selectedFrame;
    private string _inspectedRoot = "(none)";
    private string _inspectedRootType = string.Empty;
    private bool _showInactiveFrames = true;
    private bool _snapshotFrames;
    private string _framesStatus = "Value Frames (0/0 active)";
    private string _resolutionTraceStatus = "Style resolution trace (0/0 visible)";

    public StylesDiagnosticsPageViewModel()
        : this(mainView: null, selectedObjectAccessor: null)
    {
    }

    internal StylesDiagnosticsPageViewModel(Func<AvaloniaObject?>? selectedObjectAccessor)
        : this(mainView: null, selectedObjectAccessor)
    {
    }

    internal StylesDiagnosticsPageViewModel(MainViewModel? mainView, Func<AvaloniaObject?>? selectedObjectAccessor)
    {
        MainView = mainView;
        _selectedObjectAccessor = selectedObjectAccessor;

        TreeFilter = new FilterViewModel();
        TreeFilter.RefreshFilter += (_, _) => RefreshTreeEntries();

        FramesFilter = new FilterViewModel();
        FramesFilter.RefreshFilter += (_, _) => RefreshFrames();

        SettersFilter = new FilterViewModel();
        SettersFilter.RefreshFilter += (_, _) => RefreshSetters();

        ResolutionFilter = new FilterViewModel();
        ResolutionFilter.RefreshFilter += (_, _) => RefreshResolutionEntries();

        _treeEntriesView = new DataGridCollectionView(_treeEntries)
        {
            Filter = FilterTreeEntry
        };

        _framesView = new DataGridCollectionView(_frames)
        {
            Filter = FilterFrame
        };
        _framesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(ValueFrameViewModel.IsActive),
            ListSortDirection.Descending));
        _framesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(ValueFrameViewModel.Description),
            ListSortDirection.Ascending));

        _settersView = new DataGridCollectionView(_setters)
        {
            Filter = FilterSetter
        };
        _settersView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(SetterViewModel.Name),
            ListSortDirection.Ascending));

        _resolutionEntriesView = new DataGridCollectionView(_resolutionEntries)
        {
            Filter = FilterResolutionEntry
        };
        _resolutionEntriesView.SortDescriptions.Add(DataGridSortDescription.FromPath(
            nameof(StyleResolutionTraceEntryViewModel.Order),
            ListSortDirection.Ascending));
    }

    public MainViewModel? MainView { get; }

    public FilterViewModel TreeFilter { get; }

    public FilterViewModel FramesFilter { get; }

    public FilterViewModel SettersFilter { get; }

    public FilterViewModel ResolutionFilter { get; }

    public DataGridCollectionView TreeEntriesView => _treeEntriesView;

    public DataGridCollectionView FramesView => _framesView;

    public DataGridCollectionView SettersView => _settersView;

    public DataGridCollectionView ResolutionEntriesView => _resolutionEntriesView;

    public AvaloniaList<PseudoClassViewModel> PseudoClasses => _pseudoClasses;

    public string InspectedRoot
    {
        get => _inspectedRoot;
        private set => RaiseAndSetIfChanged(ref _inspectedRoot, value);
    }

    public string InspectedRootType
    {
        get => _inspectedRootType;
        private set => RaiseAndSetIfChanged(ref _inspectedRootType, value);
    }

    public StylesTreeEntryViewModel? SelectedTreeEntry
    {
        get => _selectedTreeEntry;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedTreeEntry, value))
            {
                LoadStyleInspector(value);
            }
        }
    }

    public ValueFrameViewModel? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedFrame, value))
            {
                RebuildSetterEntries();
            }
        }
    }

    public bool ShowInactiveFrames
    {
        get => _showInactiveFrames;
        set
        {
            if (RaiseAndSetIfChanged(ref _showInactiveFrames, value))
            {
                RefreshFrames();
            }
        }
    }

    public bool SnapshotFrames
    {
        get => _snapshotFrames;
        set => RaiseAndSetIfChanged(ref _snapshotFrames, value);
    }

    public string FramesStatus
    {
        get => _framesStatus;
        private set => RaiseAndSetIfChanged(ref _framesStatus, value);
    }

    public string ResolutionTraceStatus
    {
        get => _resolutionTraceStatus;
        private set => RaiseAndSetIfChanged(ref _resolutionTraceStatus, value);
    }

    public int TreeNodeCount => _treeEntries.Count;

    public int VisibleTreeNodeCount => _treeEntriesView.Count;

    public int FrameCount => _frames.Count;

    public int VisibleFrameCount => _framesView.Count;

    public int SetterCount => _setters.Count;

    public int VisibleSetterCount => _settersView.Count;

    public int PseudoClassCount => _pseudoClasses.Count;

    public int ResolutionEntryCount => _resolutionEntries.Count;

    public int VisibleResolutionEntryCount => _resolutionEntriesView.Count;

    public void Dispose()
    {
        _treeEntries.Clear();
        _frames.Clear();
        _setters.Clear();
        _pseudoClasses.Clear();
        _resolutionEntries.Clear();
    }

    public void InspectSelection()
    {
        InspectControlCore(_selectedObjectAccessor?.Invoke(), forceRefresh: true);
    }

    public void Refresh()
    {
        InspectControlCore(_inspectedObject, forceRefresh: true);
    }

    public void Clear()
    {
        _inspectedObject = null;
        SelectedTreeEntry = null;
        _treeEntries.Clear();
        _frames.Clear();
        _setters.Clear();
        _pseudoClasses.Clear();
        _resolutionEntries.Clear();
        InspectedRoot = "(none)";
        InspectedRootType = string.Empty;
        FramesStatus = "Value Frames (0/0 active)";
        RefreshTreeEntries();
        RefreshFrames();
        RefreshSetters();
        RefreshResolutionEntries();
        RaisePropertyChanged(nameof(PseudoClassCount));
    }

    internal void InspectControl(AvaloniaObject? target)
    {
        if (SnapshotFrames && _inspectedObject is not null && !ReferenceEquals(target, _inspectedObject))
        {
            return;
        }

        InspectControlCore(target, forceRefresh: false);
    }

    private void InspectControlCore(AvaloniaObject? target, bool forceRefresh)
    {
        if (!forceRefresh && SnapshotFrames && _inspectedObject is not null && !ReferenceEquals(target, _inspectedObject))
        {
            return;
        }

        _inspectedObject = target;
        _treeEntries.Clear();
        SelectedTreeEntry = null;

        if (target is null)
        {
            InspectedRoot = "(none)";
            InspectedRootType = string.Empty;
            RefreshTreeEntries();
            _resolutionEntries.Clear();
            RefreshResolutionEntries();
            return;
        }

        var root = ResolveInspectionRoot(target);
        if (root is null)
        {
            InspectedRoot = DescribeElement(target);
            InspectedRootType = target.GetType().FullName ?? target.GetType().Name;
            RefreshTreeEntries();
            return;
        }

        InspectedRoot = DescribeElement(root);
        InspectedRootType = root.GetType().FullName ?? root.GetType().Name;

        BuildTreeEntries(root);
        RefreshTreeEntries();

        var preferred = FindPreferredEntry(target, root);
        SelectedTreeEntry = preferred ?? (_treeEntries.Count > 0 ? _treeEntries[0] : null);
    }

    private void BuildTreeEntries(AvaloniaObject root)
    {
        if (root is Visual rootVisual)
        {
            var stack = new Stack<(Visual Node, int Depth)>();
            stack.Push((rootVisual, 0));
            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();

                if (node is StyledElement styled && ShouldIncludeNode(styled))
                {
                    _treeEntries.Add(CreateTreeEntry(styled, depth));
                }

                var children = node.GetVisualChildren().ToArray();
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    stack.Push((children[i], depth + 1));
                }
            }

            return;
        }

        if (root is StyledElement styledRoot)
        {
            _treeEntries.Add(CreateTreeEntry(styledRoot, 0));
        }
    }

    private void LoadStyleInspector(StylesTreeEntryViewModel? entry)
    {
        _frames.Clear();
        _setters.Clear();
        _pseudoClasses.Clear();
        SelectedFrame = null;

        if (entry is null)
        {
            FramesStatus = "Value Frames (0/0 active)";
            RefreshFrames();
            RefreshSetters();
            _resolutionEntries.Clear();
            RefreshResolutionEntries();
            RaisePropertyChanged(nameof(PseudoClassCount));
            return;
        }

        var styledElement = entry.SourceObject;
        var clipboard = TopLevel.GetTopLevel(styledElement as Visual)?.Clipboard;
        var diagnostics = styledElement.GetValueStoreDiagnostic();
        var appliedFrames = diagnostics.AppliedFrames.ToArray();
        foreach (var frame in appliedFrames.OrderBy(s => s.Priority))
        {
            _frames.Add(new ValueFrameViewModel(styledElement, frame, clipboard));
        }

        BuildPseudoClasses(styledElement);
        BuildResolutionTrace(styledElement, appliedFrames);
        UpdateStyles();
        RefreshFrames();
        SelectedFrame = _frames.Count > 0 ? _frames[0] : null;
    }

    private void BuildPseudoClasses(StyledElement styledElement)
    {
        var added = new HashSet<string>(StringComparer.Ordinal);
        var pseudoClassAttributes = styledElement.GetType().GetCustomAttributes(typeof(PseudoClassesAttribute), true);
        for (var i = 0; i < pseudoClassAttributes.Length; i++)
        {
            if (pseudoClassAttributes[i] is not PseudoClassesAttribute attribute)
            {
                continue;
            }

            for (var j = 0; j < attribute.PseudoClasses.Count; j++)
            {
                var name = attribute.PseudoClasses[j];
                if (added.Add(name))
                {
                    _pseudoClasses.Add(new PseudoClassViewModel(name, styledElement));
                }
            }
        }

        foreach (var className in styledElement.Classes)
        {
            if (!className.StartsWith(":", StringComparison.Ordinal))
            {
                continue;
            }

            if (added.Add(className))
            {
                _pseudoClasses.Add(new PseudoClassViewModel(className, styledElement));
            }
        }

        RaisePropertyChanged(nameof(PseudoClassCount));
    }

    private void UpdateStyles()
    {
        var activeCount = 0;
        for (var i = 0; i < _frames.Count; i++)
        {
            var frame = _frames[i];
            frame.Update();
            if (frame.IsActive)
            {
                activeCount++;
            }
        }

        var propertyBuckets = new Dictionary<AvaloniaProperty, List<SetterViewModel>>();
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            var frame = _frames[i];
            if (!frame.IsActive)
            {
                continue;
            }

            for (var j = 0; j < frame.Setters.Count; j++)
            {
                var setter = frame.Setters[j];
                if (!propertyBuckets.TryGetValue(setter.Property, out var setters))
                {
                    setters = new List<SetterViewModel>();
                    propertyBuckets.Add(setter.Property, setters);
                }
                else
                {
                    for (var k = 0; k < setters.Count; k++)
                    {
                        setters[k].IsActive = false;
                    }
                }

                setter.IsActive = true;
                setters.Add(setter);
            }
        }

        for (var i = 0; i < _pseudoClasses.Count; i++)
        {
            _pseudoClasses[i].Update();
        }

        FramesStatus = "Value Frames (" + activeCount + "/" + _frames.Count + " active)";
    }

    private void RebuildSetterEntries()
    {
        _setters.Clear();
        if (SelectedFrame is { } selectedFrame)
        {
            for (var i = 0; i < selectedFrame.Setters.Count; i++)
            {
                _setters.Add(selectedFrame.Setters[i]);
            }
        }

        RefreshSetters();
    }

    private void RefreshTreeEntries()
    {
        _treeEntriesView.Refresh();
        RaisePropertyChanged(nameof(TreeNodeCount));
        RaisePropertyChanged(nameof(VisibleTreeNodeCount));
    }

    private void RefreshFrames()
    {
        _framesView.Refresh();
        RaisePropertyChanged(nameof(FrameCount));
        RaisePropertyChanged(nameof(VisibleFrameCount));
    }

    private void RefreshSetters()
    {
        _settersView.Refresh();
        RaisePropertyChanged(nameof(SetterCount));
        RaisePropertyChanged(nameof(VisibleSetterCount));
    }

    private void RefreshResolutionEntries()
    {
        _resolutionEntriesView.Refresh();
        RaisePropertyChanged(nameof(ResolutionEntryCount));
        RaisePropertyChanged(nameof(VisibleResolutionEntryCount));
        ResolutionTraceStatus = "Style resolution trace (" + _resolutionEntriesView.Count + "/" + _resolutionEntries.Count + " visible)";
    }

    private bool FilterTreeEntry(object item)
    {
        if (item is not StylesTreeEntryViewModel entry)
        {
            return true;
        }

        if (TreeFilter.Filter(entry.Element))
        {
            return true;
        }

        if (TreeFilter.Filter(entry.ElementType))
        {
            return true;
        }

        if (TreeFilter.Filter(entry.Classes))
        {
            return true;
        }

        if (TreeFilter.Filter(entry.PseudoClasses))
        {
            return true;
        }

        return TreeFilter.Filter(entry.ActiveSummary);
    }

    private bool FilterFrame(object item)
    {
        if (item is not ValueFrameViewModel frame)
        {
            return true;
        }

        if (!ShowInactiveFrames && !frame.IsActive)
        {
            return false;
        }

        if (FramesFilter.Filter(frame.Description))
        {
            return true;
        }

        if (FramesFilter.Filter(frame.IsActive ? "active" : "inactive"))
        {
            return true;
        }

        return FramesFilter.Filter(frame.Setters.Count.ToString());
    }

    private bool FilterSetter(object item)
    {
        if (item is not SetterViewModel setter)
        {
            return true;
        }

        if (SettersFilter.Filter(setter.Name))
        {
            return true;
        }

        if (SettersFilter.Filter(setter.Value?.ToString() ?? string.Empty))
        {
            return true;
        }

        return SettersFilter.Filter(GetSetterKind(setter));
    }

    private bool FilterResolutionEntry(object item)
    {
        if (item is not StyleResolutionTraceEntryViewModel entry)
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.Host))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.Style))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.Selector))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.StyleKind))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.PropagationScope))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.Notes))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.Path))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.AppliedSummary))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.LogicalDistance.ToString()))
        {
            return true;
        }

        if (ResolutionFilter.Filter(entry.VisualDistance.ToString()))
        {
            return true;
        }

        return ResolutionFilter.Filter(entry.HostLevel.ToString());
    }

    private static string GetSetterKind(SetterViewModel setter)
    {
        return setter switch
        {
            BindingSetterViewModel => "Binding",
            ResourceSetterViewModel => "Resource",
            _ => "Value"
        };
    }

    private void BuildResolutionTrace(StyledElement target, IReadOnlyList<IValueFrameDiagnostic> appliedFrames)
    {
        _resolutionEntries.Clear();

        var frameStats = BuildStyleFrameStats(appliedFrames);
        var seenStyles = new HashSet<IStyle>(ReferenceEqualityComparer.Instance);
        var recursionGuard = new HashSet<IStyle>(ReferenceEqualityComparer.Instance);
        var hosts = BuildStyleHostChain(target);

        var order = 1;
        for (var hostLevel = 0; hostLevel < hosts.Count; hostLevel++)
        {
            var host = hosts[hostLevel];
            var hostLabel = DescribeStyleHost(host);
            var hostKind = DescribeStyleHostKind(host);
            var propagationScope = DescribePropagationScope(target, host, out var logicalDistance, out var visualDistance);
            if (!host.IsStylesInitialized)
            {
                _resolutionEntries.Add(new StyleResolutionTraceEntryViewModel(
                    order++,
                    hostLevel,
                    hostLabel,
                    hostKind,
                    propagationScope,
                    logicalDistance,
                    visualDistance,
                    stylesInitialized: false,
                    style: "(styles not initialized)",
                    styleKind: "Host",
                    selector: string.Empty,
                    path: string.Empty,
                    appliedCount: 0,
                    activeCount: 0,
                    notes: "Host styles were not initialized."));
                continue;
            }

            var styles = host.Styles;
            if (styles.Count == 0)
            {
                _resolutionEntries.Add(new StyleResolutionTraceEntryViewModel(
                    order++,
                    hostLevel,
                    hostLabel,
                    hostKind,
                    propagationScope,
                    logicalDistance,
                    visualDistance,
                    stylesInitialized: true,
                    style: "(no host styles)",
                    styleKind: "Host",
                    selector: string.Empty,
                    path: string.Empty,
                    appliedCount: 0,
                    activeCount: 0,
                    notes: string.Empty));
                continue;
            }

            for (var i = 0; i < styles.Count; i++)
            {
                var path = (i + 1).ToString();
                AppendStyleTraceEntries(
                    styles[i],
                    hostLevel,
                    hostLabel,
                    hostKind,
                    propagationScope,
                    logicalDistance,
                    visualDistance,
                    path,
                    styleDepth: 0,
                    frameStats,
                    seenStyles,
                    recursionGuard,
                    ref order);
            }
        }

        foreach (var kvp in frameStats)
        {
            if (seenStyles.Contains(kvp.Key))
            {
                continue;
            }

            var selector = DescribeStyleSelector(kvp.Key);
            var styleDisplay = DescribeStyle(kvp.Key);
            _resolutionEntries.Add(new StyleResolutionTraceEntryViewModel(
                order++,
                hostLevel: -1,
                host: "(external)",
                hostKind: "Applied Frame Source",
                propagationScope: "External frame source",
                logicalDistance: -1,
                visualDistance: -1,
                stylesInitialized: true,
                style: styleDisplay,
                styleKind: DescribeStyleKind(kvp.Key),
                selector: selector,
                path: string.Empty,
                appliedCount: kvp.Value.AppliedCount,
                activeCount: kvp.Value.ActiveCount,
                notes: "Applied style source was not found in the styling-parent host chain."));
        }

        RefreshResolutionEntries();
    }

    private static Dictionary<IStyle, StyleFrameStats> BuildStyleFrameStats(IReadOnlyList<IValueFrameDiagnostic> appliedFrames)
    {
        var stats = new Dictionary<IStyle, StyleFrameStats>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < appliedFrames.Count; i++)
        {
            var frame = appliedFrames[i];
            if (frame.Source is not IStyle style)
            {
                continue;
            }

            if (!stats.TryGetValue(style, out var current))
            {
                current = default;
            }

            current = current with
            {
                AppliedCount = current.AppliedCount + 1,
                ActiveCount = current.ActiveCount + (frame.IsActive ? 1 : 0)
            };
            stats[style] = current;
        }

        return stats;
    }

    private static List<IStyleHost> BuildStyleHostChain(StyledElement target)
    {
        var hosts = new List<IStyleHost>();
        for (IStyleHost? current = target; current is not null; current = current.StylingParent)
        {
            hosts.Add(current);
        }

        hosts.Reverse();
        return hosts;
    }

    private void AppendStyleTraceEntries(
        IStyle style,
        int hostLevel,
        string hostLabel,
        string hostKind,
        string propagationScope,
        int logicalDistance,
        int visualDistance,
        string path,
        int styleDepth,
        IReadOnlyDictionary<IStyle, StyleFrameStats> frameStats,
        ISet<IStyle> seenStyles,
        ISet<IStyle> recursionGuard,
        ref int order)
    {
        if (!recursionGuard.Add(style))
        {
            _resolutionEntries.Add(new StyleResolutionTraceEntryViewModel(
                order++,
                hostLevel,
                hostLabel,
                hostKind,
                propagationScope,
                logicalDistance,
                visualDistance,
                stylesInitialized: true,
                style: new string(' ', styleDepth * 2) + "(cycle detected)",
                styleKind: "Style",
                selector: string.Empty,
                path: path,
                appliedCount: 0,
                activeCount: 0,
                notes: "Recursive style reference detected."));
            return;
        }

        seenStyles.Add(style);
        frameStats.TryGetValue(style, out var stats);
        var selector = DescribeStyleSelector(style);
        var styleDisplay = (styleDepth > 0 ? new string(' ', styleDepth * 2) : string.Empty) + DescribeStyle(style);
        var notes = stats.AppliedCount == 0
            ? string.Empty
            : stats.ActiveCount == stats.AppliedCount
                ? "All matched frames are active."
                : "Some matched frames are inactive.";
        _resolutionEntries.Add(new StyleResolutionTraceEntryViewModel(
            order++,
            hostLevel,
            hostLabel,
            hostKind,
            propagationScope,
            logicalDistance,
            visualDistance,
            stylesInitialized: true,
            style: styleDisplay,
            styleKind: DescribeStyleKind(style),
            selector: selector,
            path: path,
            appliedCount: stats.AppliedCount,
            activeCount: stats.ActiveCount,
            notes: notes));

        var children = style.Children;
        for (var i = 0; i < children.Count; i++)
        {
            var childPath = path + "." + (i + 1);
            AppendStyleTraceEntries(
                children[i],
                hostLevel,
                hostLabel,
                hostKind,
                propagationScope,
                logicalDistance,
                visualDistance,
                childPath,
                styleDepth + 1,
                frameStats,
                seenStyles,
                recursionGuard,
                ref order);
        }

        recursionGuard.Remove(style);
    }

    private static string DescribeStyleHost(IStyleHost host)
    {
        return host switch
        {
            StyledElement styled => DescribeElement(styled),
            Avalonia.Application application => application.GetType().Name,
            _ => host.GetType().Name
        };
    }

    private static string DescribeStyleHostKind(IStyleHost host)
    {
        return host switch
        {
            StyledElement => "StyledElement",
            Avalonia.Application => "Application",
            _ => host.GetType().Name
        };
    }

    private static string DescribePropagationScope(
        StyledElement target,
        IStyleHost host,
        out int logicalDistance,
        out int visualDistance)
    {
        logicalDistance = GetLogicalDistance(target, host);
        visualDistance = GetVisualDistance(target, host);

        if (host is Avalonia.Application)
        {
            return "Application scope";
        }

        if (logicalDistance == 0)
        {
            return "Self";
        }

        if (logicalDistance > 0)
        {
            return "Logical ancestor +" + logicalDistance;
        }

        if (visualDistance == 0)
        {
            return "Self (visual)";
        }

        if (visualDistance > 0)
        {
            return "Visual ancestor +" + visualDistance;
        }

        if (host is StyledElement)
        {
            return "Styling parent chain";
        }

        return "Style host";
    }

    private static int GetLogicalDistance(StyledElement target, IStyleHost host)
    {
        if (host is not StyledElement styledHost)
        {
            return -1;
        }

        var distance = 0;
        for (StyledElement? current = target; current is not null; current = (current as ILogical)?.LogicalParent as StyledElement)
        {
            if (ReferenceEquals(current, styledHost))
            {
                return distance;
            }

            distance++;
        }

        return -1;
    }

    private static int GetVisualDistance(StyledElement target, IStyleHost host)
    {
        if (target is not Visual targetVisual || host is not Visual hostVisual)
        {
            return -1;
        }

        var distance = 0;
        for (Visual? current = targetVisual; current is not null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, hostVisual))
            {
                return distance;
            }

            distance++;
        }

        return -1;
    }

    private static string DescribeStyle(IStyle style)
    {
        return style switch
        {
            Style selectorStyle => selectorStyle.Selector?.ToString()?.Replace("^", string.Empty) ?? "Style",
            ControlTheme theme => "ControlTheme " + (theme.TargetType?.Name ?? "(unknown)"),
            Styles => "Styles Collection",
            _ => style.GetType().Name
        };
    }

    private static string DescribeStyleKind(IStyle style)
    {
        return style switch
        {
            Style => "Style",
            ControlTheme => "ControlTheme",
            Styles => "Styles",
            _ => style.GetType().Name
        };
    }

    private static string DescribeStyleSelector(IStyle style)
    {
        return style switch
        {
            Style selectorStyle => selectorStyle.Selector?.ToString()?.Replace("^", string.Empty) ?? string.Empty,
            ControlTheme theme => theme.TargetType?.Name ?? string.Empty,
            _ => string.Empty
        };
    }

    private readonly record struct StyleFrameStats(
        int AppliedCount,
        int ActiveCount);

    private static AvaloniaObject? ResolveInspectionRoot(AvaloniaObject target)
    {
        if (target is StyledElement styledElement)
        {
            return styledElement;
        }

        if (target is Visual visual)
        {
            return visual;
        }

        return null;
    }

    private StylesTreeEntryViewModel? FindPreferredEntry(AvaloniaObject target, AvaloniaObject root)
    {
        if (target is StyledElement styledTarget)
        {
            for (var i = 0; i < _treeEntries.Count; i++)
            {
                if (ReferenceEquals(_treeEntries[i].SourceObject, styledTarget))
                {
                    return _treeEntries[i];
                }
            }
        }

        if (root is StyledElement styledRoot)
        {
            for (var i = 0; i < _treeEntries.Count; i++)
            {
                if (ReferenceEquals(_treeEntries[i].SourceObject, styledRoot))
                {
                    return _treeEntries[i];
                }
            }
        }

        return null;
    }

    private static StylesTreeEntryViewModel CreateTreeEntry(StyledElement styled, int depth)
    {
        var diagnostics = styled.GetValueStoreDiagnostic();
        var totalFrames = 0;
        var activeFrames = 0;
        foreach (var frame in diagnostics.AppliedFrames)
        {
            totalFrames++;
            if (frame.IsActive)
            {
                activeFrames++;
            }
        }

        var classes = string.Join(" ", styled.Classes.Where(c => !c.StartsWith(":", StringComparison.Ordinal)));
        var pseudoClasses = string.Join(" ", styled.Classes.Where(c => c.StartsWith(":", StringComparison.Ordinal)));

        return new StylesTreeEntryViewModel(
            styled,
            depth,
            DescribeElement(styled),
            styled.GetType().Name,
            totalFrames,
            activeFrames,
            classes,
            pseudoClasses);
    }

    private static string DescribeElement(AvaloniaObject target)
    {
        var typeName = target.GetType().Name;
        if (target is StyledElement { Name: { Length: > 0 } name })
        {
            return typeName + "#" + name;
        }

        return typeName;
    }

    private static bool ShouldIncludeNode(StyledElement styledElement)
    {
        if (styledElement is not Visual visual)
        {
            return true;
        }

        // Unattached visuals have no TopLevel and should still be inspectable in tests/runtime.
        if (TopLevel.GetTopLevel(visual) is null)
        {
            return true;
        }

        return !visual.DoesBelongToDevTool();
    }
}
