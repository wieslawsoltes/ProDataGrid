using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class Elements3DPageViewModel : ViewModelBase
{
    private const double DefaultDepthSpacing = 24;
    private const int DefaultFlat2DMaxLayersPerRow = 0;
    private const double DefaultTilt = 0.55;
    private const double DefaultZoom = 1;
    private const double DefaultOrbitYaw = 0;
    private const double DefaultOrbitPitch = 0;
    private const double DefaultOrbitRoll = 0;

    private readonly AvaloniaObject _root;
    private readonly Func<AvaloniaObject?>? _selectedObjectAccessor;
    private readonly AvaloniaList<Elements3DNodeViewModel> _nodes = new();
    private readonly AvaloniaList<Elements3DNodeViewModel> _visibleNodes = new();
    private readonly DataGridCollectionView _allNodesView;
    private readonly DataGridCollectionView _nodesView;
    private Visual? _mainRootVisual;
    private Visual? _currentRootVisual;
    private Visual? _scopedSelectionVisual;
    private bool _isScopedToSelectionBranch;
    private string _inspectedRoot = "(none)";
    private Elements3DNodeViewModel? _selectedNode;
    private bool _showInvisibleNodes = true;
    private bool _showExploded3DView = true;
    private bool _showAllLayersInGrid;
    private double _depthSpacing = DefaultDepthSpacing;
    private int _flat2DMaxLayersPerRow = DefaultFlat2DMaxLayersPerRow;
    private double _tilt = DefaultTilt;
    private double _zoom = DefaultZoom;
    private double _orbitYaw = DefaultOrbitYaw;
    private double _orbitPitch = DefaultOrbitPitch;
    private double _orbitRoll = DefaultOrbitRoll;
    private int _availableMinDepth;
    private int _availableMaxDepth;
    private int _minVisibleDepth;
    private int _maxVisibleDepth = int.MaxValue;
    private int _maxVisibleElements;
    private HashSet<Elements3DNodeViewModel>? _limitedVisibleNodes;
    private IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
    private Func<(string Scope, string? NodePath, string? ControlName)>? _remoteContextAccessor;
    private long _remoteRefreshVersion;
    private bool _isApplyingRemoteSnapshot;

    public Elements3DPageViewModel(AvaloniaObject root, Func<AvaloniaObject?>? selectedObjectAccessor)
    {
        _root = root;
        _selectedObjectAccessor = selectedObjectAccessor;

        Filter = new FilterViewModel();
        Filter.RefreshFilter += (_, _) => Refresh();

        _nodesView = new DataGridCollectionView(_nodes)
        {
            Filter = FilterNode
        };
        _allNodesView = new DataGridCollectionView(_nodes)
        {
            Filter = FilterNodeForAllLayersGrid
        };
        ApplyDefaultGridSort(_nodesView);
        ApplyDefaultGridSort(_allNodesView);

        InspectRoot();
    }

    public FilterViewModel Filter { get; }

    public DataGridCollectionView NodesView => _nodesView;

    public DataGridCollectionView GridNodesView => _showAllLayersInGrid ? _allNodesView : _nodesView;

    public AvaloniaList<Elements3DNodeViewModel> Nodes => _nodes;

    public AvaloniaList<Elements3DNodeViewModel> VisibleNodes => _visibleNodes;

    public int NodeCount => _nodes.Count;

    public int VisibleNodeCount => _visibleNodes.Count;

    internal Visual? MainRootVisual => _mainRootVisual;

    internal Visual? CurrentRootVisual => _currentRootVisual;

    internal Visual? ScopedSelectionVisual => _scopedSelectionVisual;

    internal bool IsScopedToSelectionBranch => _isScopedToSelectionBranch;

    public string InspectedRoot
    {
        get => _inspectedRoot;
        private set => RaiseAndSetIfChanged(ref _inspectedRoot, value);
    }

    public Elements3DNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (RaiseAndSetIfChanged(ref _selectedNode, value))
            {
                RaisePropertyChanged(nameof(SelectedNodeSummary));
                RaisePropertyChanged(nameof(CanScopeSelectedNodeAsRoot));
            }
        }
    }

    public string SelectedNodeSummary
        => SelectedNode is null
            ? "(none)"
            : SelectedNode.Node.TrimStart() + " | Depth " + SelectedNode.Depth + " | Z " + SelectedNode.ZIndex;

    public bool CanScopeSelectedNodeAsRoot
    {
        get
        {
            if (SelectedNode?.Visual is not Visual selected)
            {
                return false;
            }

            if (_isScopedToSelectionBranch && ReferenceEquals(selected, _scopedSelectionVisual))
            {
                return false;
            }

            return !_isScopedToSelectionBranch || !ReferenceEquals(selected, _currentRootVisual);
        }
    }

    public bool CanResetToMainRoot
        => _isScopedToSelectionBranch
           || (_mainRootVisual is not null && !ReferenceEquals(_currentRootVisual, _mainRootVisual));

    public bool ShowInvisibleNodes
    {
        get => _showInvisibleNodes;
        set
        {
            if (RaiseAndSetIfChanged(ref _showInvisibleNodes, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowExploded3DView
    {
        get => _showExploded3DView;
        set => RaiseAndSetIfChanged(ref _showExploded3DView, value);
    }

    public bool ShowAllLayersInGrid
    {
        get => _showAllLayersInGrid;
        set
        {
            if (RaiseAndSetIfChanged(ref _showAllLayersInGrid, value))
            {
                RaisePropertyChanged(nameof(GridNodesView));
            }
        }
    }

    public double DepthSpacing
    {
        get => _depthSpacing;
        set => RaiseAndSetIfChanged(ref _depthSpacing, Math.Clamp(value, 0, 400));
    }

    public int Flat2DMaxLayersPerRow
    {
        get => _flat2DMaxLayersPerRow;
        set => RaiseAndSetIfChanged(ref _flat2DMaxLayersPerRow, Math.Clamp(value, 0, 512));
    }

    public double Tilt
    {
        get => _tilt;
        set => RaiseAndSetIfChanged(ref _tilt, Math.Clamp(value, 0, 1));
    }

    public double Zoom
    {
        get => _zoom;
        set => RaiseAndSetIfChanged(ref _zoom, Math.Clamp(value, 0.25, 24));
    }

    public double OrbitYaw
    {
        get => _orbitYaw;
        set => RaiseAndSetIfChanged(ref _orbitYaw, Math.Clamp(value, -180, 180));
    }

    public double OrbitPitch
    {
        get => _orbitPitch;
        set => RaiseAndSetIfChanged(ref _orbitPitch, Math.Clamp(value, -180, 180));
    }

    public double OrbitRoll
    {
        get => _orbitRoll;
        set => RaiseAndSetIfChanged(ref _orbitRoll, Math.Clamp(value, -180, 180));
    }

    public int AvailableMinDepth
    {
        get => _availableMinDepth;
        private set => RaiseAndSetIfChanged(ref _availableMinDepth, value);
    }

    public int AvailableMaxDepth
    {
        get => _availableMaxDepth;
        private set => RaiseAndSetIfChanged(ref _availableMaxDepth, value);
    }

    public int MinVisibleDepth
    {
        get => _minVisibleDepth;
        set
        {
            var clamped = Math.Clamp(value, AvailableMinDepth, AvailableMaxDepth);
            var minChanged = RaiseAndSetIfChanged(ref _minVisibleDepth, clamped);
            var maxChanged = false;
            if (_maxVisibleDepth < clamped)
            {
                maxChanged = RaiseAndSetIfChanged(ref _maxVisibleDepth, clamped);
            }

            if (minChanged || maxChanged)
            {
                Refresh();
            }
        }
    }

    public int MaxVisibleDepth
    {
        get => _maxVisibleDepth;
        set
        {
            var clamped = Math.Clamp(value, AvailableMinDepth, AvailableMaxDepth);
            var maxChanged = RaiseAndSetIfChanged(ref _maxVisibleDepth, clamped);
            var minChanged = false;
            if (_minVisibleDepth > clamped)
            {
                minChanged = RaiseAndSetIfChanged(ref _minVisibleDepth, clamped);
            }

            if (maxChanged || minChanged)
            {
                Refresh();
            }
        }
    }

    public int MaxVisibleElements
    {
        get => _maxVisibleElements;
        set
        {
            var clamped = Math.Clamp(value, 0, 100_000);
            if (RaiseAndSetIfChanged(ref _maxVisibleElements, clamped))
            {
                Refresh();
            }
        }
    }

    public void InspectSelection()
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        InspectControl(_selectedObjectAccessor?.Invoke());
    }

    public void InspectRoot()
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        BuildFrom(_root, preferTopLevelRoot: true, trackAsMainRoot: true);
    }

    internal void InspectControl(AvaloniaObject? target)
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        if (target is Visual selectedVisual)
        {
            BuildSelectionScope(selectedVisual);
            return;
        }

        BuildFrom(target, preferTopLevelRoot: false, trackAsMainRoot: false);
    }

    public void ScopeSelectedNodeAsRoot()
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        if (SelectedNode?.Visual is not Visual selected)
        {
            return;
        }

        BuildSelectionScope(selected);
    }

    public void ResetToMainRoot()
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        if (_mainRootVisual is null)
        {
            return;
        }

        if (!_isScopedToSelectionBranch && ReferenceEquals(_currentRootVisual, _mainRootVisual))
        {
            return;
        }

        BuildFrom(_mainRootVisual, preferTopLevelRoot: false, trackAsMainRoot: false);
    }

    public void Refresh()
    {
        if (_remoteReadOnly is not null && !_isApplyingRemoteSnapshot)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        RebuildLimitedVisibleNodeSet();
        _allNodesView.Refresh();
        _nodesView.Refresh();
        RebuildVisibleNodes();
        if (SelectedNode is not null && !_nodes.Contains(SelectedNode))
        {
            SelectedNode = null;
        }

        RaisePropertyChanged(nameof(NodeCount));
        RaisePropertyChanged(nameof(VisibleNodeCount));
    }

    public void ResetProjectionView()
    {
        DepthSpacing = DefaultDepthSpacing;
        Flat2DMaxLayersPerRow = DefaultFlat2DMaxLayersPerRow;
        Tilt = DefaultTilt;
        Zoom = DefaultZoom;
        OrbitYaw = DefaultOrbitYaw;
        OrbitPitch = DefaultOrbitPitch;
        OrbitRoll = DefaultOrbitRoll;
    }

    public void ResetLayerVisibilityFilters()
    {
        if (_remoteReadOnly is not null)
        {
            _ = RefreshFromRemoteAsync();
            return;
        }

        var minChanged = RaiseAndSetIfChanged(ref _minVisibleDepth, AvailableMinDepth);
        var maxChanged = RaiseAndSetIfChanged(ref _maxVisibleDepth, AvailableMaxDepth);
        var countChanged = RaiseAndSetIfChanged(ref _maxVisibleElements, 0);
        if (minChanged || maxChanged || countChanged)
        {
            Refresh();
        }
    }

    internal void SetRemoteReadOnlySource(
        IRemoteReadOnlyDiagnosticsDomainService? readOnly,
        Func<(string Scope, string? NodePath, string? ControlName)>? contextAccessor,
        bool refreshNow = true)
    {
        _remoteReadOnly = readOnly;
        _remoteContextAccessor = contextAccessor;
        if (refreshNow)
        {
            _ = RefreshFromRemoteAsync();
        }
    }

    private async Task RefreshFromRemoteAsync()
    {
        var readOnly = _remoteReadOnly;
        if (readOnly is null)
        {
            return;
        }

        var refreshVersion = Interlocked.Increment(ref _remoteRefreshVersion);
        try
        {
            var snapshot = await readOnly.GetElements3DSnapshotAsync(
                new RemoteElements3DSnapshotRequest
                {
                    IncludeNodes = true,
                    IncludeVisibleNodeIds = true
                }).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (refreshVersion != _remoteRefreshVersion)
                {
                    return;
                }

                ApplyRemoteSnapshot(snapshot);
            });
        }
        catch
        {
            // Keep previous state on remote read failures.
        }
    }

    private void ApplyRemoteSnapshot(RemoteElements3DSnapshot snapshot)
    {
        _isApplyingRemoteSnapshot = true;
        try
        {
            _mainRootVisual = null;
            _currentRootVisual = null;
            _scopedSelectionVisual = null;
            _isScopedToSelectionBranch = snapshot.IsScopedToSelectionBranch;
            RaiseAndSetIfChanged(ref _showInvisibleNodes, snapshot.ShowInvisibleNodes, nameof(ShowInvisibleNodes));
            RaiseAndSetIfChanged(ref _showExploded3DView, snapshot.ShowExploded3DView, nameof(ShowExploded3DView));
            if (RaiseAndSetIfChanged(ref _showAllLayersInGrid, snapshot.ShowAllLayersInGrid, nameof(ShowAllLayersInGrid)))
            {
                RaisePropertyChanged(nameof(GridNodesView));
            }
            RaiseAndSetIfChanged(ref _depthSpacing, snapshot.DepthSpacing, nameof(DepthSpacing));
            RaiseAndSetIfChanged(ref _flat2DMaxLayersPerRow, snapshot.Flat2DMaxLayersPerRow, nameof(Flat2DMaxLayersPerRow));
            RaiseAndSetIfChanged(ref _tilt, snapshot.Tilt, nameof(Tilt));
            RaiseAndSetIfChanged(ref _zoom, snapshot.Zoom, nameof(Zoom));
            RaiseAndSetIfChanged(ref _orbitYaw, snapshot.OrbitYaw, nameof(OrbitYaw));
            RaiseAndSetIfChanged(ref _orbitPitch, snapshot.OrbitPitch, nameof(OrbitPitch));
            RaiseAndSetIfChanged(ref _orbitRoll, snapshot.OrbitRoll, nameof(OrbitRoll));
            RaiseAndSetIfChanged(ref _availableMinDepth, snapshot.AvailableMinDepth, nameof(AvailableMinDepth));
            RaiseAndSetIfChanged(ref _availableMaxDepth, snapshot.AvailableMaxDepth, nameof(AvailableMaxDepth));
            RaiseAndSetIfChanged(ref _minVisibleDepth, snapshot.MinVisibleDepth, nameof(MinVisibleDepth));
            RaiseAndSetIfChanged(ref _maxVisibleDepth, snapshot.MaxVisibleDepth, nameof(MaxVisibleDepth));
            RaiseAndSetIfChanged(ref _maxVisibleElements, snapshot.MaxVisibleElements, nameof(MaxVisibleElements));
            InspectedRoot = string.IsNullOrWhiteSpace(snapshot.InspectedRoot) ? "(none)" : snapshot.InspectedRoot;

            _nodes.Clear();
            Elements3DNodeViewModel? selected = null;
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                var bounds = new Rect(node.Bounds.X, node.Bounds.Y, node.Bounds.Width, node.Bounds.Height);
                var nodeViewModel = new Elements3DNodeViewModel(
                    depth: node.Depth,
                    node: node.Node,
                    zIndex: node.ZIndex,
                    boundsRect: bounds,
                    isVisible: node.IsVisible,
                    opacity: node.Opacity,
                    visual: null,
                    nodeId: node.NodeId,
                    nodePath: node.NodePath,
                    isRendered: node.IsRendered);
                _nodes.Add(nodeViewModel);

                if (string.Equals(node.NodeId, snapshot.SelectedNodeId, StringComparison.Ordinal))
                {
                    selected = nodeViewModel;
                }
            }

            SelectedNode = selected ?? (_nodes.Count > 0 ? _nodes[0] : null);
            Refresh();
            RaiseRootScopeStateChanged();
        }
        finally
        {
            _isApplyingRemoteSnapshot = false;
        }
    }

    private bool FilterNode(object item)
    {
        if (item is not Elements3DNodeViewModel node)
        {
            return true;
        }

        if (!PassesNodeFiltersExceptMax(node))
        {
            return false;
        }

        return _limitedVisibleNodes is null || _limitedVisibleNodes.Contains(node);
    }

    private bool FilterNodeForAllLayersGrid(object item)
    {
        if (item is not Elements3DNodeViewModel node)
        {
            return true;
        }

        if (!ShowInvisibleNodes && !node.IsVisible)
        {
            return false;
        }

        if (Filter.Filter(node.Node))
        {
            return true;
        }

        if (Filter.Filter(node.Bounds))
        {
            return true;
        }

        if (Filter.Filter(node.Depth.ToString()))
        {
            return true;
        }

        return Filter.Filter(node.ZIndex.ToString());
    }

    private bool PassesNodeFiltersExceptMax(Elements3DNodeViewModel node)
    {
        if (!ShowInvisibleNodes && !node.IsVisible)
        {
            return false;
        }

        if (node.Depth < MinVisibleDepth || node.Depth > MaxVisibleDepth)
        {
            return false;
        }

        if (Filter.Filter(node.Node))
        {
            return true;
        }

        if (Filter.Filter(node.Bounds))
        {
            return true;
        }

        return Filter.Filter(node.ZIndex.ToString());
    }

    private void BuildFrom(
        AvaloniaObject? rootCandidate,
        bool preferTopLevelRoot,
        bool trackAsMainRoot,
        Visual? scopedSelection = null)
    {
        _nodes.Clear();

        if (TryResolveRootVisual(rootCandidate, preferTopLevelRoot, out var rootVisual))
        {
            if (trackAsMainRoot || _mainRootVisual is null)
            {
                _mainRootVisual = rootVisual;
            }

            _currentRootVisual = rootVisual;
            var effectiveScopedSelection = scopedSelection is not null && IsDescendantOrSelf(rootVisual, scopedSelection)
                ? scopedSelection
                : null;
            _scopedSelectionVisual = effectiveScopedSelection;
            _isScopedToSelectionBranch = effectiveScopedSelection is not null;

            Traverse(rootVisual, rootVisual, depth: 0, effectiveScopedSelection);
            InspectedRoot = effectiveScopedSelection is null
                ? DescribeVisual(rootVisual)
                : DescribeVisual(rootVisual) + " -> " + DescribeVisual(effectiveScopedSelection);
            SelectedNode = FindNodeForVisual(effectiveScopedSelection)
                ?? (_nodes.Count > 0 ? _nodes[0] : null);
        }
        else
        {
            _currentRootVisual = null;
            _scopedSelectionVisual = null;
            _isScopedToSelectionBranch = false;
            InspectedRoot = "(none)";
            SelectedNode = null;
        }

        UpdateLayerFilterBounds();
        Refresh();
        RaiseRootScopeStateChanged();
    }

    private void RaiseRootScopeStateChanged()
    {
        RaisePropertyChanged(nameof(CanScopeSelectedNodeAsRoot));
        RaisePropertyChanged(nameof(CanResetToMainRoot));
    }

    private void UpdateLayerFilterBounds()
    {
        var previousAvailableMin = AvailableMinDepth;
        var previousAvailableMax = AvailableMaxDepth;
        var wasFullRange = _minVisibleDepth <= previousAvailableMin && _maxVisibleDepth >= previousAvailableMax;

        if (_nodes.Count == 0)
        {
            AvailableMinDepth = 0;
            AvailableMaxDepth = 0;
            RaiseAndSetIfChanged(ref _minVisibleDepth, 0);
            RaiseAndSetIfChanged(ref _maxVisibleDepth, 0);
            return;
        }

        var minDepth = int.MaxValue;
        var maxDepth = int.MinValue;
        for (var i = 0; i < _nodes.Count; i++)
        {
            var depth = _nodes[i].Depth;
            if (depth < minDepth)
            {
                minDepth = depth;
            }

            if (depth > maxDepth)
            {
                maxDepth = depth;
            }
        }

        AvailableMinDepth = minDepth;
        AvailableMaxDepth = maxDepth;

        var clampedMin = wasFullRange
            ? minDepth
            : Math.Clamp(_minVisibleDepth, minDepth, maxDepth);
        var clampedMax = wasFullRange
            ? maxDepth
            : Math.Clamp(_maxVisibleDepth, minDepth, maxDepth);
        if (clampedMax < clampedMin)
        {
            clampedMax = clampedMin;
        }

        RaiseAndSetIfChanged(ref _minVisibleDepth, clampedMin);
        RaiseAndSetIfChanged(ref _maxVisibleDepth, clampedMax);
    }

    private void RebuildLimitedVisibleNodeSet()
    {
        if (MaxVisibleElements <= 0)
        {
            _limitedVisibleNodes = null;
            return;
        }

        var limited = new HashSet<Elements3DNodeViewModel>();
        foreach (var node in _nodes
                     .OrderBy(x => x.Depth)
                     .ThenByDescending(x => x.ZIndex))
        {
            if (!PassesNodeFiltersExceptMax(node))
            {
                continue;
            }

            limited.Add(node);
            if (limited.Count >= MaxVisibleElements)
            {
                break;
            }
        }

        _limitedVisibleNodes = limited;
    }

    private void RebuildVisibleNodes()
    {
        _visibleNodes.Clear();
        foreach (var node in _nodesView)
        {
            if (node is Elements3DNodeViewModel typedNode)
            {
                _visibleNodes.Add(typedNode);
            }
        }
    }

    private static void ApplyDefaultGridSort(DataGridCollectionView view)
    {
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(DataGridSortDescription.FromComparer(DepthThenZIndexComparer.Instance, ListSortDirection.Ascending));
    }

    private Visual ResolveScopeBaseRoot(Visual selected)
    {
        var selectedTopMostAncestor = GetTopMostAncestor(selected);

        if (_mainRootVisual is not null
            && IsDescendantOrSelf(_mainRootVisual, selected)
            && (!ReferenceEquals(_mainRootVisual, selected) || ReferenceEquals(selectedTopMostAncestor, selected)))
        {
            return _mainRootVisual;
        }

        if (_currentRootVisual is not null
            && IsDescendantOrSelf(_currentRootVisual, selected)
            && (!ReferenceEquals(_currentRootVisual, selected) || ReferenceEquals(selectedTopMostAncestor, selected)))
        {
            return _currentRootVisual;
        }

        return selectedTopMostAncestor;
    }

    private void BuildSelectionScope(Visual selected)
    {
        var baseRoot = ResolveScopeBaseRoot(selected);

        if (ReferenceEquals(baseRoot, selected))
        {
            BuildFrom(selected, preferTopLevelRoot: false, trackAsMainRoot: false);
            return;
        }

        BuildFrom(baseRoot, preferTopLevelRoot: false, trackAsMainRoot: false, scopedSelection: selected);
    }

    private static Visual GetTopMostAncestor(Visual selected)
    {
        // Fallback to the top-most ancestor in the selected visual tree to avoid
        // collapsing to a single detached node when root tracking becomes stale.
        var topMost = selected;
        var foundVisualParent = false;
        for (var current = selected.GetVisualParent(); current is not null; current = current.GetVisualParent())
        {
            topMost = current;
            foundVisualParent = true;
        }

        if (foundVisualParent)
        {
            return topMost;
        }

        // Unattached controls may still have logical ancestry available.
        if (selected is ILogical logical)
        {
            for (ILogical? current = logical.LogicalParent; current is not null; current = current.LogicalParent)
            {
                if (current is Visual visual)
                {
                    topMost = visual;
                }
            }
        }

        return topMost;
    }

    private Elements3DNodeViewModel? FindNodeForVisual(Visual? visual)
    {
        if (visual is null)
        {
            return null;
        }

        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (ReferenceEquals(node.Visual, visual))
            {
                return node;
            }
        }

        return null;
    }

    private static bool TryResolveRootVisual(
        AvaloniaObject? rootCandidate,
        bool preferTopLevelRoot,
        out Visual rootVisual)
    {
        switch (rootCandidate)
        {
            case Visual visual:
                rootVisual = preferTopLevelRoot
                    ? TopLevel.GetTopLevel(visual) ?? visual
                    : visual;
                return true;

            case TopLevelGroup group when group.Items.Count > 0:
                rootVisual = group.Items[0];
                return true;

            default:
                rootVisual = null!;
                return false;
        }
    }

    private void Traverse(Visual visual, Visual rootVisual, int depth, Visual? scopedSelection)
    {
        if (!ShouldIncludeVisualInScope(visual, scopedSelection))
        {
            return;
        }

        var bounds = GetBoundsInRoot(visual, rootVisual);
        _nodes.Add(new Elements3DNodeViewModel(
            depth,
            new string(' ', depth * 2) + DescribeVisual(visual),
            visual.GetValue(Panel.ZIndexProperty),
            bounds,
            visual.IsVisible,
            visual.Opacity,
            visual));

        foreach (var child in visual.GetVisualChildren())
        {
            Traverse(child, rootVisual, depth + 1, scopedSelection);
        }
    }

    private static bool ShouldIncludeVisualInScope(Visual visual, Visual? scopedSelection)
    {
        if (scopedSelection is null)
        {
            return true;
        }

        // Keep the ancestor path to selected and the whole selected subtree.
        return IsDescendantOrSelf(visual, scopedSelection) || IsDescendantOrSelf(scopedSelection, visual);
    }

    private static bool IsDescendantOrSelf(Visual ancestor, Visual visual)
    {
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static Rect GetBoundsInRoot(Visual visual, Visual rootVisual)
    {
        if (visual == rootVisual)
        {
            return visual.Bounds;
        }

        var topLeft = visual.TranslatePoint(default, rootVisual);
        var bottomRight = visual.TranslatePoint(new Point(visual.Bounds.Width, visual.Bounds.Height), rootVisual);
        if (topLeft is { } start && bottomRight is { } end)
        {
            var minX = Math.Min(start.X, end.X);
            var minY = Math.Min(start.Y, end.Y);
            var width = Math.Abs(end.X - start.X);
            var height = Math.Abs(end.Y - start.Y);
            return new Rect(minX, minY, width, height);
        }

        if (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
        {
            return default;
        }

        return new Rect(0, 0, visual.Bounds.Width, visual.Bounds.Height);
    }

    private static string DescribeVisual(Visual visual)
    {
        var typeName = visual.GetType().Name;
        if (visual is StyledElement { Name: { Length: > 0 } name })
        {
            return typeName + "#" + name;
        }

        return typeName;
    }

    private sealed class DepthThenZIndexComparer : IComparer
    {
        public static readonly DepthThenZIndexComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            var left = x as Elements3DNodeViewModel;
            var right = y as Elements3DNodeViewModel;
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            var depthResult = right.Depth.CompareTo(left.Depth);
            if (depthResult != 0)
            {
                return depthResult;
            }

            var zIndexResult = right.ZIndex.CompareTo(left.ZIndex);
            if (zIndexResult != 0)
            {
                return zIndexResult;
            }

            return StringComparer.Ordinal.Compare(left.Node, right.Node);
        }
    }
}
