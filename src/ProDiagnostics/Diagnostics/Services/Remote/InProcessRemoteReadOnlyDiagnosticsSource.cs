using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.LogicalTree;
using Avalonia.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteReadOnlyDiagnosticsSource : IRemoteReadOnlyDiagnosticsSource
{
    private const int SnapshotVersion = 1;
    private const int ProjectionYieldInterval = 256;
    private const int TreeSnapshotCacheTtlMilliseconds = 750;
    private const int TreeLookupCacheTtlMilliseconds = 750;
    private const int PropertiesSnapshotCacheCapacity = 128;
    private const int StylesSnapshotCacheCapacity = 96;
    private const int ResourcesSnapshotCacheCapacity = 32;
    private const int AssetsSnapshotCacheCapacity = 32;
    private const int SourceTypeCacheCapacity = 512;
    private const int SourceObjectCacheCapacity = 2048;

    private static readonly RemoteSourceLocationSnapshot s_emptySourceSnapshot = new(
        Xaml: null,
        Code: null,
        Status: string.Empty);

    private readonly AvaloniaObject _root;
    private readonly ISourceLocationService _sourceLocationService;
    private readonly ITemplateVisualTreeProvider _templateProvider;
    private readonly ITreeNodeProvider _combinedTreeProvider;
    private readonly ITreeNodeProvider _logicalTreeProvider;
    private readonly ITreeNodeProvider _visualTreeProvider;
    private readonly IResourceTreeNodeProvider _resourceTreeProvider;
    private readonly LruCache<Type, RemoteSourceLocationSnapshot> _treeNodeSourceCache =
        new(SourceTypeCacheCapacity);
    private readonly LruCache<object, RemoteSourceLocationSnapshot> _sourceInstanceCache =
        new(SourceObjectCacheCapacity, ReferenceEqualityComparer.Instance);
    private readonly LruCache<string, RemoteSourceLocationSnapshot> _sourceDocumentCache =
        new(SourceObjectCacheCapacity);
    private readonly InProcessRemoteNodeIdentityProvider _nodeIdentityProvider;
    private readonly InProcessRemoteSelectionState _selectionState;
    private readonly IRemoteStreamPauseController? _streamPauseController;
    private readonly BreakpointService? _breakpointService;
    private readonly EventsPageViewModel? _eventsPageViewModel;
    private readonly LogsPageViewModel? _logsPageViewModel;
    private readonly Elements3DPageViewModel _elements3DPageViewModel;
    private readonly InProcessRemoteOverlayState _overlayState;
    private readonly ViewModelsBindingsPageViewModel _bindingsPageViewModel;
    private readonly StylesDiagnosticsPageViewModel _stylesPageViewModel;
    private readonly object _treeCacheSync = new();
    private readonly object _elements3DSvgCacheSync = new();
    private readonly Dictionary<string, TreeSnapshotCacheEntry> _treeSnapshotCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TreeLookupCacheEntry> _treeLookupCache = new(StringComparer.Ordinal);
    private readonly LruCache<string, RemotePropertiesSnapshot> _propertiesSnapshotCache =
        new(PropertiesSnapshotCacheCapacity);
    private readonly LruCache<string, RemoteStylesSnapshot> _stylesSnapshotCache =
        new(StylesSnapshotCacheCapacity);
    private readonly LruCache<string, RemoteResourcesSnapshot> _resourcesSnapshotCache =
        new(ResourcesSnapshotCacheCapacity);
    private readonly LruCache<string, RemoteAssetsSnapshot> _assetsSnapshotCache =
        new(AssetsSnapshotCacheCapacity);
    private long _treeCacheGeneration = 1;
    private long _treeCacheTimestampTicks;
    private long _elements3DSnapshotRequestVersion;
    private Elements3DSvgCacheEntry? _elements3DSvgCache;
    private AvaloniaObject? _bindingsInspectionTarget;
    private StyledElement? _stylesInspectionTarget;

    public InProcessRemoteReadOnlyDiagnosticsSource(
        AvaloniaObject root,
        ISourceLocationService? sourceLocationService = null,
        BreakpointService? breakpointService = null,
        EventsPageViewModel? eventsPageViewModel = null,
        LogsPageViewModel? logsPageViewModel = null,
        IRemoteStreamPauseController? streamPauseController = null,
        Elements3DPageViewModel? elements3DPageViewModel = null,
        InProcessRemoteOverlayState? overlayState = null,
        InProcessRemoteNodeIdentityProvider? nodeIdentityProvider = null,
        InProcessRemoteSelectionState? selectionState = null)
    {
        _root = root;
        _sourceLocationService = sourceLocationService ?? new PortablePdbSourceLocationService();
        _nodeIdentityProvider = nodeIdentityProvider ?? new InProcessRemoteNodeIdentityProvider();
        _selectionState = selectionState ?? new InProcessRemoteSelectionState();
        _templateProvider = new TemplateVisualTreeProvider();
        _combinedTreeProvider = new CombinedTreeNodeProvider(_templateProvider);
        _logicalTreeProvider = new LogicalTreeNodeProvider();
        _visualTreeProvider = new VisualTreeNodeProvider();
        var formatter = new ResourceNodeFormatter();
        _resourceTreeProvider = new ResourceTreeNodeProvider(new ResourceTreeNodeFactory(formatter));
        _breakpointService = breakpointService;
        _eventsPageViewModel = eventsPageViewModel;
        _logsPageViewModel = logsPageViewModel;
        _streamPauseController = streamPauseController;
        _elements3DPageViewModel = elements3DPageViewModel ?? new Elements3DPageViewModel(root, selectedObjectAccessor: null);
        _overlayState = overlayState ?? new InProcessRemoteOverlayState();
        _bindingsPageViewModel = new ViewModelsBindingsPageViewModel(() => _bindingsInspectionTarget ?? _root);
        _stylesPageViewModel = new StylesDiagnosticsPageViewModel(
            mainView: null,
            selectedObjectAccessor: () => _stylesInspectionTarget,
            sourceLocationService: _sourceLocationService);
        RemoteRuntimeMetrics.SetSnapshotCacheEntries(_treeSnapshotCache.Count);
    }

    public ValueTask<RemoteTreeSnapshot> GetTreeSnapshotAsync(
        RemoteTreeSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureAndProjectSnapshotAsync(
            domain: "tree",
            scope: scope,
            captureOperation: (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                var generation = GetSnapshotGeneration();
                var cacheKey = BuildTreeSnapshotCacheKey(
                    scope,
                    request.IncludeSourceLocations,
                    request.IncludeVisualDetails,
                    generation);
                if (TryGetCachedTreeSnapshot(cacheKey, generation, out var cached))
                {
                    context.Cache = "hit";
                    return TreeSnapshotUiCapture.ForCached(cached);
                }

                var nodes = BuildFastTreeNodeCaptureEntries(
                    scope,
                    request.IncludeSourceLocations,
                    request.IncludeVisualDetails,
                    token);
                context.Cache = "miss";
                return TreeSnapshotUiCapture.ForCapture(
                    generation,
                    scope,
                    cacheKey,
                    request.IncludeSourceLocations,
                    nodes);
            },
            projectOperation: (capture, _, token) =>
                capture.CachedSnapshot is not null
                    ? new ValueTask<RemoteTreeSnapshot>(capture.CachedSnapshot)
                    : ProjectTreeSnapshotAsync(capture, token),
            cancellationToken);
    }

    public ValueTask<RemoteSelectionSnapshot> GetSelectionSnapshotAsync(
        RemoteSelectionSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "tree",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _selectionState.GetSnapshot(request.Scope);
        },
            cancellationToken);
    }

    public ValueTask<RemotePropertiesSnapshot> GetPropertiesSnapshotAsync(
        RemotePropertiesSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "properties",
            scope: scope,
            operation: context =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = GetSnapshotGeneration();
            var cacheKey = BuildPropertiesSnapshotCacheKey(scope, request, generation);
            if (_propertiesSnapshotCache.TryGetValue(cacheKey, out var cached))
            {
                context.Cache = "hit";
                return cached;
            }

            var targetLookup = BuildTreeLookup(scope);
            try
            {
                var target = ResolveTarget(request, targetLookup) ?? _root;
                var targetNodePath = ResolveTargetNodePath(request, targetLookup, target);
                var targetNodeId = targetLookup.FindNodeId(target);
                var properties = BuildPropertySnapshots(target, includeClrProperties: request.IncludeClrProperties);
                var frames = BuildValueFrameSnapshots(target, cancellationToken);
                var source = ResolveSourceLocationSnapshot(target);

                var snapshot =
                new RemotePropertiesSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: generation,
                    Scope: scope,
                    Target: DescribeTarget(target),
                    TargetType: target.GetType().FullName ?? target.GetType().Name,
                    TargetNodeId: targetNodeId,
                    TargetNodePath: targetNodePath,
                    Properties: properties,
                    Frames: frames,
                    Source: source);
                _propertiesSnapshotCache[cacheKey] = snapshot;
                context.Cache = "miss";
                return snapshot;
            }
            finally
            {
                targetLookup.Dispose();
            }
        },
            cancellationToken);
    }

    public ValueTask<RemoteElements3DSnapshot> GetElements3DSnapshotAsync(
        RemoteElements3DSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureAndProjectSnapshotAsync(
            domain: "elements3d",
            scope: "none",
            captureOperation: (context, token) =>
            {
                token.ThrowIfCancellationRequested();
                var viewModel = _elements3DPageViewModel;
                var includeNodes = request.IncludeNodes;
                var includeVisibleNodeIds = request.IncludeVisibleNodeIds;
                var requestVersion = Interlocked.Increment(ref _elements3DSnapshotRequestVersion);
                var sceneRevision = viewModel.SnapshotRevision;
                var rootPaths = includeNodes
                    ? BuildVisualPathLookup(viewModel.CurrentRootVisual)
                    : null;

                var visibleNodeIdSet = includeNodes || includeVisibleNodeIds
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : null;
                if (visibleNodeIdSet is not null)
                {
                    for (var i = 0; i < viewModel.VisibleNodes.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        visibleNodeIdSet.Add(_nodeIdentityProvider.GetNodeId(viewModel.VisibleNodes[i].Visual));
                    }
                }

                var selectedNodeId = viewModel.SelectedNode is null
                    ? null
                    : _nodeIdentityProvider.GetNodeId(viewModel.SelectedNode.Visual);
                var capturedNodes = includeNodes
                    ? new CapturedElements3DNode[viewModel.Nodes.Count]
                    : Array.Empty<CapturedElements3DNode>();
                if (includeNodes)
                {
                    for (var i = 0; i < viewModel.Nodes.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        var node = viewModel.Nodes[i];
                        var nodeId = _nodeIdentityProvider.GetNodeId(node.Visual);
                        rootPaths!.TryGetValue(node.Visual, out var nodePath);
                        capturedNodes[i] = new CapturedElements3DNode(
                            NodeId: nodeId,
                            NodePath: nodePath,
                            Depth: node.Depth,
                            Node: node.Node,
                            ZIndex: node.ZIndex,
                            BoundsX: node.BoundsRect.X,
                            BoundsY: node.BoundsRect.Y,
                            BoundsWidth: node.BoundsRect.Width,
                            BoundsHeight: node.BoundsRect.Height,
                            IsVisible: node.IsVisible,
                            Opacity: node.Opacity,
                            IsRendered: visibleNodeIdSet is not null && visibleNodeIdSet.Contains(nodeId),
                            IsSelected: string.Equals(nodeId, selectedNodeId, StringComparison.Ordinal));
                    }
                }

                var visibleIds = includeVisibleNodeIds && visibleNodeIdSet is not null
                    ? visibleNodeIdSet.ToArray()
                    : Array.Empty<string>();

                (string svgSnapshot, string svgViewBox)? svg = null;
                if (request.IncludeSvgSnapshot &&
                    requestVersion == Volatile.Read(ref _elements3DSnapshotRequestVersion))
                {
                    var svgWidth = Math.Clamp(request.SvgWidth, 320, 8192);
                    var svgHeight = Math.Clamp(request.SvgHeight, 240, 8192);
                    if (TryGetCachedElements3DSvgSnapshot(
                            sceneRevision,
                            svgWidth,
                            svgHeight,
                            request.MaxSvgNodes,
                            out var cachedSvg))
                    {
                        svg = cachedSvg;
                        context.Cache = "hit";
                    }
                    else
                    {
                        var renderedSvg = BuildElements3DSvgSnapshot(
                            viewModel,
                            svgWidth,
                            svgHeight,
                            request.MaxSvgNodes);
                        if (requestVersion == Volatile.Read(ref _elements3DSnapshotRequestVersion))
                        {
                            svg = renderedSvg;
                            if (renderedSvg is { } produced)
                            {
                                StoreCachedElements3DSvgSnapshot(
                                    sceneRevision,
                                    svgWidth,
                                    svgHeight,
                                    request.MaxSvgNodes,
                                    produced);
                            }

                            context.Cache = "miss";
                        }
                    }
                }

                return new Elements3DSnapshotUiCapture(
                    Generation: sceneRevision,
                    InspectedRoot: viewModel.InspectedRoot,
                    MainRootNodeId: viewModel.MainRootVisual is null
                        ? null
                        : _nodeIdentityProvider.GetNodeId(viewModel.MainRootVisual),
                    CurrentRootNodeId: viewModel.CurrentRootVisual is null
                        ? null
                        : _nodeIdentityProvider.GetNodeId(viewModel.CurrentRootVisual),
                    ScopedSelectionNodeId: viewModel.ScopedSelectionVisual is null
                        ? null
                        : _nodeIdentityProvider.GetNodeId(viewModel.ScopedSelectionVisual),
                    SelectedNodeId: selectedNodeId,
                    IsScopedToSelectionBranch: viewModel.IsScopedToSelectionBranch,
                    NodeCount: viewModel.NodeCount,
                    VisibleNodeCount: viewModel.VisibleNodeCount,
                    ShowInvisibleNodes: viewModel.ShowInvisibleNodes,
                    ShowExploded3DView: viewModel.ShowExploded3DView,
                    ShowAllLayersInGrid: viewModel.ShowAllLayersInGrid,
                    DepthSpacing: viewModel.DepthSpacing,
                    Flat2DMaxLayersPerRow: viewModel.Flat2DMaxLayersPerRow,
                    Tilt: viewModel.Tilt,
                    Zoom: viewModel.Zoom,
                    OrbitYaw: viewModel.OrbitYaw,
                    OrbitPitch: viewModel.OrbitPitch,
                    OrbitRoll: viewModel.OrbitRoll,
                    AvailableMinDepth: viewModel.AvailableMinDepth,
                    AvailableMaxDepth: viewModel.AvailableMaxDepth,
                    MinVisibleDepth: viewModel.MinVisibleDepth,
                    MaxVisibleDepth: viewModel.MaxVisibleDepth,
                    MaxVisibleElements: viewModel.MaxVisibleElements,
                    SvgSnapshot: svg?.svgSnapshot,
                    SvgViewBox: svg?.svgViewBox,
                    Nodes: capturedNodes,
                    VisibleNodeIds: visibleIds);
            },
            projectOperation: ProjectElements3DSnapshotAsync,
            cancellationToken);
    }

    private static (string svgSnapshot, string svgViewBox)? BuildElements3DSvgSnapshot(
        Elements3DPageViewModel viewModel,
        int requestedWidth,
        int requestedHeight,
        int maxSvgNodes)
    {
        return Elements3DSvgVectorExporter.Export(
            viewModel,
            requestedWidth,
            requestedHeight,
            maxSvgNodes);
    }

    private bool TryGetCachedElements3DSvgSnapshot(
        long sceneRevision,
        int svgWidth,
        int svgHeight,
        int maxSvgNodes,
        out (string svgSnapshot, string svgViewBox) svg)
    {
        lock (_elements3DSvgCacheSync)
        {
            if (_elements3DSvgCache is
                {
                    SceneRevision: var revision,
                    SvgWidth: var width,
                    SvgHeight: var height,
                    MaxSvgNodes: var maxNodes,
                    SvgSnapshot: var snapshot,
                    SvgViewBox: var viewBox
                } &&
                revision == sceneRevision &&
                width == svgWidth &&
                height == svgHeight &&
                maxNodes == maxSvgNodes)
            {
                svg = (snapshot, viewBox);
                return true;
            }
        }

        svg = default;
        return false;
    }

    private void StoreCachedElements3DSvgSnapshot(
        long sceneRevision,
        int svgWidth,
        int svgHeight,
        int maxSvgNodes,
        (string svgSnapshot, string svgViewBox) snapshot)
    {
        lock (_elements3DSvgCacheSync)
        {
            _elements3DSvgCache = new Elements3DSvgCacheEntry(
                SceneRevision: sceneRevision,
                SvgWidth: svgWidth,
                SvgHeight: svgHeight,
                MaxSvgNodes: maxSvgNodes,
                SvgSnapshot: snapshot.svgSnapshot,
                SvgViewBox: snapshot.svgViewBox);
        }
    }

    public ValueTask<RemoteOverlayOptionsSnapshot> GetOverlayOptionsSnapshotAsync(
        RemoteOverlayOptionsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "overlay",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _overlayState.GetSnapshot();
        },
            cancellationToken);
    }

    public ValueTask<RemoteCodeDocumentsSnapshot> GetCodeDocumentsSnapshotAsync(
        RemoteCodeDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "code",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetLookup = BuildTreeLookup(scope);
            try
            {
                var target = ResolveTarget(request, targetLookup) ?? _root;
                var targetNodePath = ResolveTargetNodePath(request, targetLookup, target);
                var targetNodeId = targetLookup.FindNodeId(target);
                var sourceInfo = _sourceLocationService.ResolveObject(target);
                var documents = BuildCodeDocumentSnapshots(sourceInfo);
                return new RemoteCodeDocumentsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Scope: scope,
                    Target: DescribeTarget(target),
                    TargetType: target.GetType().FullName ?? target.GetType().Name,
                    TargetNodeId: targetNodeId,
                    TargetNodePath: targetNodePath,
                    Status: sourceInfo.Status,
                    Documents: documents);
            }
            finally
            {
                targetLookup.Dispose();
            }
        },
            cancellationToken);
    }

    public ValueTask<RemoteCodeResolveNodeSnapshot> ResolveCodeNodeAsync(
        RemoteCodeResolveNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "code",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filePath = request.FilePath?.Trim() ?? string.Empty;
            if (filePath.Length == 0)
            {
                return new RemoteCodeResolveNodeSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Scope: scope,
                    FilePath: string.Empty,
                    Line: request.Line <= 0 ? 1 : request.Line,
                    Column: Math.Max(0, request.Column),
                    Found: false,
                    NodeId: null,
                    NodePath: null,
                    Target: null,
                    TargetType: null,
                    MatchKind: string.Empty,
                    Status: "FilePath is required.");
            }

            using var lookup = BuildTreeLookup(scope);
            var normalizedPath = SourceLocationTextParser.NormalizePath(filePath);
            var line = request.Line <= 0 ? 1 : request.Line;
            var column = Math.Max(0, request.Column);
            var found = TryResolveNodeByCodeLocation(
                lookup,
                normalizedPath,
                line,
                column,
                out var target,
                out var nodePath,
                out var nodeId,
                out var matchKind);

            return new RemoteCodeResolveNodeSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: SnapshotGeneration,
                Scope: scope,
                FilePath: filePath,
                Line: line,
                Column: column,
                Found: found,
                NodeId: nodeId,
                NodePath: nodePath,
                Target: target is null ? null : DescribeTarget(target),
                TargetType: target?.GetType().FullName ?? target?.GetType().Name,
                MatchKind: matchKind,
                Status: found ? "Node resolved from source location." : "No matching node found.");
        },
            cancellationToken);
    }

    public ValueTask<RemoteBindingsSnapshot> GetBindingsSnapshotAsync(
        RemoteBindingsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "bindings",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetLookup = BuildTreeLookup(scope);
            try
            {
                var target = ResolveTarget(request, targetLookup) ?? _root;
                var targetNodePath = ResolveTargetNodePath(request, targetLookup, target);
                var targetNodeId = targetLookup.FindNodeId(target);
                _bindingsInspectionTarget = target;
                _bindingsPageViewModel.InspectSelection();

                var viewModels = _bindingsPageViewModel.ViewModelEntriesView
                    .Cast<ViewModelContextEntryViewModel>()
                    .Select(entry => MapViewModelEntry(entry, targetLookup))
                    .ToArray();
                var bindings = _bindingsPageViewModel.BindingEntriesView
                    .Cast<BindingDiagnosticEntryViewModel>()
                    .Select(entry => MapBindingEntry(entry, targetLookup))
                    .ToArray();

                return
                new RemoteBindingsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Scope: scope,
                    InspectedElement: _bindingsPageViewModel.InspectedElement,
                    InspectedElementType: _bindingsPageViewModel.InspectedElementType,
                    InspectedNodeId: targetNodeId,
                    InspectedNodePath: targetNodePath,
                    ShowOnlyBindingErrors: _bindingsPageViewModel.ShowOnlyBindingErrors,
                    ViewModels: viewModels,
                    Bindings: bindings);
            }
            finally
            {
                targetLookup.Dispose();
            }
        },
            cancellationToken);
    }

    public ValueTask<RemoteStylesSnapshot> GetStylesSnapshotAsync(
        RemoteStylesSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "styles",
            scope: scope,
            operation: context =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = GetSnapshotGeneration();
            var cacheKey = BuildStylesSnapshotCacheKey(scope, request, generation);
            if (_stylesSnapshotCache.TryGetValue(cacheKey, out var cached))
            {
                context.Cache = "hit";
                return cached;
            }

            var targetLookup = BuildTreeLookup(scope);
            try
            {
                var target = ResolveTarget(request, targetLookup) as StyledElement;
                if (target is null)
                {
                    var empty =
                    new RemoteStylesSnapshot(
                        SnapshotVersion: SnapshotVersion,
                        Generation: generation,
                        Scope: scope,
                        InspectedRoot: "(none)",
                        InspectedRootType: string.Empty,
                        InspectedRootNodeId: null,
                        TreeEntries: Array.Empty<RemoteStyleTreeEntrySnapshot>(),
                        Frames: Array.Empty<RemoteValueFrameSnapshot>(),
                        Setters: Array.Empty<RemoteSetterSnapshot>(),
                        Resolution: Array.Empty<RemoteStyleResolutionSnapshot>());
                    _stylesSnapshotCache[cacheKey] = empty;
                    context.Cache = "miss";
                    return empty;
                }

                _stylesInspectionTarget = target;
                _stylesPageViewModel.InspectSelection();

                var includeTreeEntries = request.IncludeTreeEntries;
                var includeFrames = request.IncludeFrames;
                var includeSetters = request.IncludeSetters;
                var includeResolution = request.IncludeResolution;
                var shouldLoadTreeEntries = includeTreeEntries || includeFrames || includeSetters || includeResolution;

                var treeEntries = shouldLoadTreeEntries
                    ? _stylesPageViewModel.TreeEntriesView
                        .Cast<StylesTreeEntryViewModel>()
                        .ToArray()
                    : Array.Empty<StylesTreeEntryViewModel>();

                if (shouldLoadTreeEntries)
                {
                    var selectedTreeEntry = treeEntries.FirstOrDefault(entry => ReferenceEquals(entry.SourceObject, target))
                                            ?? treeEntries.FirstOrDefault();
                    if (selectedTreeEntry is not null)
                    {
                        _stylesPageViewModel.SelectedTreeEntry = selectedTreeEntry;
                    }
                }

                var frames = includeFrames
                    ? MapFramesWithCancellation(
                        _stylesPageViewModel.FramesView
                            .Cast<ValueFrameViewModel>()
                            .ToArray(),
                        context: "styles-frames",
                        cancellationToken)
                    : Array.Empty<RemoteValueFrameSnapshot>();
                var setters = includeSetters
                    ? MapSettersWithCancellation(
                        _stylesPageViewModel.SettersView
                            .Cast<SetterViewModel>()
                            .ToArray(),
                        context: "styles-setters",
                        cancellationToken)
                    : Array.Empty<RemoteSetterSnapshot>();
                var resolution = includeResolution
                    ? MapStyleResolutionEntriesWithCancellation(
                        _stylesPageViewModel.ResolutionEntriesView
                            .Cast<StyleResolutionTraceEntryViewModel>()
                            .ToArray(),
                        cancellationToken)
                    : Array.Empty<RemoteStyleResolutionSnapshot>();

                var snapshot =
                new RemoteStylesSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: generation,
                    Scope: scope,
                    InspectedRoot: _stylesPageViewModel.InspectedRoot,
                    InspectedRootType: _stylesPageViewModel.InspectedRootType,
                    InspectedRootNodeId: targetLookup.FindNodeId(target),
                    TreeEntries: includeTreeEntries
                        ? MapStyleTreeEntries(treeEntries, targetLookup, cancellationToken)
                        : Array.Empty<RemoteStyleTreeEntrySnapshot>(),
                    Frames: frames,
                    Setters: setters,
                    Resolution: resolution);
                _stylesSnapshotCache[cacheKey] = snapshot;
                context.Cache = "miss";
                return snapshot;
            }
            finally
            {
                targetLookup.Dispose();
            }
        },
            cancellationToken);
    }

    public ValueTask<RemoteResourcesSnapshot> GetResourcesSnapshotAsync(
        RemoteResourcesSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "resources",
            scope: "none",
            operation: context =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = GetSnapshotGeneration();
            var cacheKey = BuildResourcesSnapshotCacheKey(request, generation);
            if (_resourcesSnapshotCache.TryGetValue(cacheKey, out var cached))
            {
                context.Cache = "hit";
                return cached;
            }

            var roots = _resourceTreeProvider.Create(_root);
            try
            {
                var nodes = new List<RemoteResourceNodeSnapshot>();
                var entries = new List<RemoteResourceEntrySnapshot>();
                for (var i = 0; i < roots.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FlattenResourceNode(
                        roots[i],
                        nodePath: i.ToString(),
                        parentNodePath: null,
                        depth: 0,
                        includeEntries: request.IncludeEntries,
                        cancellationToken,
                        nodes,
                        entries);
                }

                var snapshot =
                new RemoteResourcesSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: generation,
                    Nodes: nodes,
                    Entries: entries);
                _resourcesSnapshotCache[cacheKey] = snapshot;
                context.Cache = "miss";
                return snapshot;
            }
            finally
            {
                DisposeResourceNodes(roots);
            }
        },
            cancellationToken);
    }

    public ValueTask<RemoteAssetsSnapshot> GetAssetsSnapshotAsync(
        RemoteAssetsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "assets",
            scope: "none",
            operation: context =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var generation = GetSnapshotGeneration();
            var cacheKey = BuildAssetsSnapshotCacheKey(generation);
            if (_assetsSnapshotCache.TryGetValue(cacheKey, out var cached))
            {
                context.Cache = "hit";
                return cached;
            }

            var assets = CollectAssets(cancellationToken)
                .Select(asset =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = ResolveAssetSourceLocationSnapshot(asset.assembly, asset.assetPath, asset.name);
                    return new RemoteAssetSnapshot(
                        Id: CreateStableId("asset", asset.assemblyName, asset.assetPath),
                        AssemblyName: asset.assemblyName,
                        AssetPath: asset.assetPath,
                        Name: asset.name,
                        Kind: asset.kind,
                        Uri: asset.uri.ToString(),
                        SourceLocation: GetSourceLocationText(source),
                        Source: source);
                })
                .ToArray();

            var snapshot = new RemoteAssetsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: generation,
                Assets: assets);
            _assetsSnapshotCache[cacheKey] = snapshot;
            context.Cache = "miss";
            return snapshot;
        },
            cancellationToken);
    }

    public ValueTask<RemoteEventsSnapshot> GetEventsSnapshotAsync(
        RemoteEventsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "events",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventsPage = _eventsPageViewModel;
            if (eventsPage is null)
            {
                return new RemoteEventsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Scope: scope,
                    Status: "Events service unavailable.",
                    IncludeBubbleRoutes: true,
                    IncludeTunnelRoutes: true,
                    IncludeDirectRoutes: true,
                    IncludeHandledEvents: true,
                    IncludeUnhandledEvents: true,
                    MaxRecordedEvents: 0,
                    AutoScrollToLatest: false,
                    TotalRecordedEvents: 0,
                    VisibleRecordedEvents: 0,
                    Nodes: Array.Empty<RemoteEventNodeSnapshot>(),
                    RecordedEvents: Array.Empty<RemoteRecordedEventSnapshot>());
            }

            using var lookup = BuildTreeLookup(scope);
            var nodes = FlattenEventNodes(eventsPage.Nodes);
            var records = request.IncludeRecordedEvents
                ? eventsPage.RecordedEventsView
                    .Cast<FiredEvent>()
                    .Select(record => MapRecordedEvent(record, lookup))
                    .ToArray()
                : Array.Empty<RemoteRecordedEventSnapshot>();
            return new RemoteEventsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: SnapshotGeneration,
                Scope: scope,
                Status: "ok",
                IncludeBubbleRoutes: eventsPage.IncludeBubbleRoutes,
                IncludeTunnelRoutes: eventsPage.IncludeTunnelRoutes,
                IncludeDirectRoutes: eventsPage.IncludeDirectRoutes,
                IncludeHandledEvents: eventsPage.IncludeHandledEvents,
                IncludeUnhandledEvents: eventsPage.IncludeUnhandledEvents,
                MaxRecordedEvents: eventsPage.MaxRecordedEvents,
                AutoScrollToLatest: eventsPage.AutoScrollToLatest,
                TotalRecordedEvents: eventsPage.TotalRecordedEvents,
                VisibleRecordedEvents: eventsPage.VisibleRecordedEvents,
                Nodes: nodes,
                RecordedEvents: records);
        },
            cancellationToken);
    }

    public ValueTask<RemoteBreakpointsSnapshot> GetBreakpointsSnapshotAsync(
        RemoteBreakpointsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = NormalizeScope(request.Scope);
        return CaptureSnapshotAsync(
            domain: "breakpoints",
            scope: scope,
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var breakpoints = _breakpointService;
            if (breakpoints is null)
            {
                return new RemoteBreakpointsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Scope: scope,
                    Status: "Breakpoints service unavailable.",
                    BreakpointCount: 0,
                    Breakpoints: Array.Empty<RemoteBreakpointSnapshot>());
            }

            using var lookup = BuildTreeLookup(scope);
            var items = breakpoints.Entries
                .Select(entry => MapBreakpoint(entry, lookup))
                .ToArray();
            return new RemoteBreakpointsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: SnapshotGeneration,
                Scope: scope,
                Status: "ok",
                BreakpointCount: breakpoints.Entries.Count,
                Breakpoints: items);
        },
            cancellationToken);
    }

    public ValueTask<RemoteLogsSnapshot> GetLogsSnapshotAsync(
        RemoteLogsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "logs",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logsPage = _logsPageViewModel;
            if (logsPage is null)
            {
                return new RemoteLogsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Status: "Logs service unavailable.",
                    CollectorName: string.Empty,
                    ShowVerbose: false,
                    ShowDebug: false,
                    ShowInformation: false,
                    ShowWarning: false,
                    ShowError: false,
                    ShowFatal: false,
                    MaxEntries: 0,
                    EntryCount: 0,
                    VisibleEntryCount: 0,
                    FilterText: string.Empty,
                    Entries: Array.Empty<RemoteLogEntrySnapshot>());
            }

            var items = request.IncludeEntries
                ? logsPage.EntriesView
                    .Cast<LogEntryViewModel>()
                    .Select(MapLogEntry)
                    .ToArray()
                : Array.Empty<RemoteLogEntrySnapshot>();
            return new RemoteLogsSnapshot(
                SnapshotVersion: SnapshotVersion,
                Generation: SnapshotGeneration,
                Status: "ok",
                CollectorName: logsPage.CollectorName,
                ShowVerbose: logsPage.ShowVerbose,
                ShowDebug: logsPage.ShowDebug,
                ShowInformation: logsPage.ShowInformation,
                ShowWarning: logsPage.ShowWarning,
                ShowError: logsPage.ShowError,
                ShowFatal: logsPage.ShowFatal,
                MaxEntries: logsPage.MaxEntries,
                EntryCount: logsPage.EntryCount,
                VisibleEntryCount: logsPage.VisibleEntryCount,
                FilterText: logsPage.LogsFilter.FilterString ?? string.Empty,
                Entries: items);
        },
            cancellationToken);
    }

    public ValueTask<RemotePreviewCapabilitiesSnapshot> GetPreviewCapabilitiesSnapshotAsync(
        RemotePreviewCapabilitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "preview",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_streamPauseController is null)
            {
                return new RemotePreviewCapabilitiesSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Status: "Preview service unavailable.",
                    DefaultTransport: "svg",
                    SupportedTransports: new[] { "svg", "png" },
                    SupportsInput: false,
                    SupportsDiff: true,
                    IsPaused: true,
                    StreamEnabled: false,
                    TargetFps: 0,
                    MaxWidth: 0,
                    MaxHeight: 0,
                    MaxScale: 1d);
            }

            return _streamPauseController.GetPreviewCapabilitiesSnapshot(request);
        },
            cancellationToken);
    }

    public ValueTask<RemotePreviewSnapshot> GetPreviewSnapshotAsync(
        RemotePreviewSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "preview",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_streamPauseController is null)
            {
                var transport = string.Equals(request.Transport, "png", StringComparison.OrdinalIgnoreCase)
                    ? "png"
                    : "svg";
                return new RemotePreviewSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Status: "Preview service unavailable.",
                    Transport: transport,
                    MimeType: string.Empty,
                    Width: 0,
                    Height: 0,
                    Scale: request.Scale,
                    RenderScaling: 1d,
                    CapturedAtUtc: DateTimeOffset.UtcNow,
                    IsPaused: true,
                    IsDelta: false,
                    HasChanges: false,
                    FrameHash: string.Empty,
                    PreviousFrameHash: request.PreviousFrameHash,
                    DiffKind: "none",
                    ChangedRegions: Array.Empty<RemotePreviewRectSnapshot>(),
                    FrameData: null);
            }

            return _streamPauseController.GetPreviewSnapshot(request);
        },
            cancellationToken);
    }

    public ValueTask<RemoteMetricsSnapshot> GetMetricsSnapshotAsync(
        RemoteMetricsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "metrics",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_streamPauseController is null)
            {
                return new RemoteMetricsSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Status: "Metrics service unavailable.",
                    IsPaused: true,
                    MaxRetainedMeasurements: 0,
                    MaxSeries: 0,
                    MaxSamplesPerSeries: 0,
                    TotalMeasurements: 0,
                    DroppedMeasurements: 0,
                    MeasurementCount: 0,
                    SeriesCount: 0,
                    Series: Array.Empty<RemoteMetricSeriesSnapshot>(),
                    Measurements: Array.Empty<RemoteMetricMeasurementSnapshot>());
            }

            return _streamPauseController.GetMetricsSnapshot(request);
        },
            cancellationToken);
    }

    public ValueTask<RemoteProfilerSnapshot> GetProfilerSnapshotAsync(
        RemoteProfilerSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        return CaptureSnapshotAsync(
            domain: "profiler",
            scope: "none",
            operation: _ =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_streamPauseController is null)
            {
                return new RemoteProfilerSnapshot(
                    SnapshotVersion: SnapshotVersion,
                    Generation: SnapshotGeneration,
                    Status: "Profiler service unavailable.",
                    IsPaused: true,
                    MaxRetainedSamples: 0,
                    SampleIntervalMs: 0,
                    TotalSamples: 0,
                    DroppedSamples: 0,
                    SampleCount: 0,
                    CurrentCpuPercent: 0,
                    PeakCpuPercent: 0,
                    CurrentWorkingSetMb: 0,
                    PeakWorkingSetMb: 0,
                    CurrentManagedHeapMb: 0,
                    CurrentActivityDurationMs: 0,
                    PeakActivityDurationMs: 0,
                    Samples: Array.Empty<RemoteProfilerSampleSnapshot>());
            }

            return _streamPauseController.GetProfilerSnapshot(request);
        },
            cancellationToken);
    }

    private async ValueTask<TSnapshot> CaptureSnapshotAsync<TSnapshot>(
        string domain,
        string scope,
        Func<SnapshotCaptureContext, TSnapshot> operation,
        CancellationToken cancellationToken)
    {
        var context = new SnapshotCaptureContext();
        var snapshotStarted = Stopwatch.GetTimestamp();
        var snapshotStatus = "ok";
        try
        {
            var snapshot = await InvokeOnUiThreadAsync(() =>
            {
                var uiThreadStarted = Stopwatch.GetTimestamp();
                var uiThreadStatus = "ok";
                try
                {
                    return operation(context);
                }
                catch (OperationCanceledException)
                {
                    uiThreadStatus = "cancelled";
                    throw;
                }
                catch
                {
                    uiThreadStatus = "error";
                    throw;
                }
                finally
                {
                    RemoteRuntimeMetrics.RecordUiThreadCaptureDuration(
                        domain: domain,
                        scope: scope,
                        status: uiThreadStatus,
                        durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(uiThreadStarted),
                        cache: context.Cache);
                }
            }, cancellationToken);
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            snapshotStatus = "cancelled";
            throw;
        }
        catch
        {
            snapshotStatus = "error";
            throw;
        }
        finally
        {
            RemoteRuntimeMetrics.RecordSnapshotDuration(
                domain: domain,
                scope: scope,
                status: snapshotStatus,
                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(snapshotStarted),
                cache: context.Cache);
        }
    }

    private async ValueTask<TSnapshot> CaptureAndProjectSnapshotAsync<TCapture, TSnapshot>(
        string domain,
        string scope,
        Func<SnapshotCaptureContext, CancellationToken, TCapture> captureOperation,
        Func<TCapture, SnapshotCaptureContext, CancellationToken, ValueTask<TSnapshot>> projectOperation,
        CancellationToken cancellationToken)
    {
        var context = new SnapshotCaptureContext();
        var snapshotStarted = Stopwatch.GetTimestamp();
        var snapshotStatus = "ok";
        try
        {
            var captured = await InvokeOnUiThreadAsync(() =>
            {
                var uiThreadStarted = Stopwatch.GetTimestamp();
                var uiThreadStatus = "ok";
                try
                {
                    return captureOperation(context, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    uiThreadStatus = "cancelled";
                    throw;
                }
                catch
                {
                    uiThreadStatus = "error";
                    throw;
                }
                finally
                {
                    RemoteRuntimeMetrics.RecordUiThreadCaptureDuration(
                        domain: domain,
                        scope: scope,
                        status: uiThreadStatus,
                        durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(uiThreadStarted),
                        cache: context.Cache);
                }
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (Dispatcher.UIThread.CheckAccess())
            {
                return await Task.Run(
                    async () => await AwaitValueTask(projectOperation(captured, context, cancellationToken)).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }

            return await AwaitValueTask(projectOperation(captured, context, cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            snapshotStatus = "cancelled";
            throw;
        }
        catch
        {
            snapshotStatus = "error";
            throw;
        }
        finally
        {
            RemoteRuntimeMetrics.RecordSnapshotDuration(
                domain: domain,
                scope: scope,
                status: snapshotStatus,
                durationMs: RemoteRuntimeMetrics.ElapsedMilliseconds(snapshotStarted),
                cache: context.Cache);
        }
    }

    private static ValueTask<T> AwaitValueTask<T>(ValueTask<T> valueTask)
    {
        return valueTask.IsCompletedSuccessfully
            ? new ValueTask<T>(valueTask.Result)
            : valueTask;
    }

    private sealed class SnapshotCaptureContext
    {
        public string Cache { get; set; } = "bypass";
    }

    private sealed class TreeSnapshotUiCapture
    {
        private TreeSnapshotUiCapture(
            RemoteTreeSnapshot? cachedSnapshot,
            long generation,
            string scope,
            string cacheKey,
            bool includeSourceLocations,
            IReadOnlyList<CapturedTreeNode> nodes)
        {
            CachedSnapshot = cachedSnapshot;
            Generation = generation;
            Scope = scope;
            CacheKey = cacheKey;
            IncludeSourceLocations = includeSourceLocations;
            Nodes = nodes;
        }

        public RemoteTreeSnapshot? CachedSnapshot { get; }

        public long Generation { get; }

        public string Scope { get; }

        public string CacheKey { get; }

        public bool IncludeSourceLocations { get; }

        public IReadOnlyList<CapturedTreeNode> Nodes { get; }

        public static TreeSnapshotUiCapture ForCached(RemoteTreeSnapshot cachedSnapshot)
        {
            return new TreeSnapshotUiCapture(
                cachedSnapshot,
                generation: cachedSnapshot.Generation,
                scope: cachedSnapshot.Scope,
                cacheKey: string.Empty,
                includeSourceLocations: false,
                nodes: Array.Empty<CapturedTreeNode>());
        }

        public static TreeSnapshotUiCapture ForCapture(
            long generation,
            string scope,
            string cacheKey,
            bool includeSourceLocations,
            IReadOnlyList<CapturedTreeNode> nodes)
        {
            return new TreeSnapshotUiCapture(
                cachedSnapshot: null,
                generation: generation,
                scope: scope,
                cacheKey: cacheKey,
                includeSourceLocations: includeSourceLocations,
                nodes: nodes);
        }
    }

    private readonly record struct CapturedTreeNode(
        string NodeId,
        string NodePath,
        string? ParentNodePath,
        int Depth,
        string Type,
        string? ElementName,
        string Classes,
        string DisplayName,
        Type? SourceType,
        string RelationshipKind,
        bool IsVisible,
        double Opacity,
        int ZIndex,
        RemoteRectSnapshot? Bounds);

    private sealed record Elements3DSnapshotUiCapture(
        long Generation,
        string InspectedRoot,
        string? MainRootNodeId,
        string? CurrentRootNodeId,
        string? ScopedSelectionNodeId,
        string? SelectedNodeId,
        bool IsScopedToSelectionBranch,
        int NodeCount,
        int VisibleNodeCount,
        bool ShowInvisibleNodes,
        bool ShowExploded3DView,
        bool ShowAllLayersInGrid,
        double DepthSpacing,
        int Flat2DMaxLayersPerRow,
        double Tilt,
        double Zoom,
        double OrbitYaw,
        double OrbitPitch,
        double OrbitRoll,
        int AvailableMinDepth,
        int AvailableMaxDepth,
        int MinVisibleDepth,
        int MaxVisibleDepth,
        int MaxVisibleElements,
        string? SvgSnapshot,
        string? SvgViewBox,
        CapturedElements3DNode[] Nodes,
        string[] VisibleNodeIds);

    private readonly record struct CapturedElements3DNode(
        string NodeId,
        string? NodePath,
        int Depth,
        string Node,
        int ZIndex,
        double BoundsX,
        double BoundsY,
        double BoundsWidth,
        double BoundsHeight,
        bool IsVisible,
        double Opacity,
        bool IsRendered,
        bool IsSelected);

    private ITreeNodeProvider ResolveTreeProvider(string scope)
    {
        return scope switch
        {
            "logical" => _logicalTreeProvider,
            "visual" => _visualTreeProvider,
            _ => _combinedTreeProvider,
        };
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

    private static void DisposeTreeNodes(IReadOnlyList<TreeNode> roots)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            roots[i].Dispose();
        }
    }

    private static void DisposeResourceNodes(IReadOnlyList<ResourceTreeNode> roots)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            roots[i].Dispose();
        }
    }

    private List<RemoteTreeNodeSnapshot> BuildTreeNodeSnapshots(
        IReadOnlyList<TreeNode> roots,
        bool includeSourceLocations)
    {
        var result = new List<RemoteTreeNodeSnapshot>(256);
        for (var i = 0; i < roots.Count; i++)
        {
            FlattenTreeNode(
                roots[i],
                i.ToString(),
                null,
                0,
                includeSourceLocations,
                result);
        }

        return result;
    }

    private void FlattenTreeNode(
        TreeNode node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeSourceLocations,
        List<RemoteTreeNodeSnapshot> output)
    {
        var source = includeSourceLocations
            ? ResolveTreeNodeSourceSnapshot(node.Visual)
            : s_emptySourceSnapshot;
        output.Add(
            new RemoteTreeNodeSnapshot(
                NodeId: _nodeIdentityProvider.GetNodeId(node.Visual),
                NodePath: nodePath,
                ParentNodePath: parentNodePath,
                Depth: depth,
                Type: node.Type,
                ElementName: node.ElementName,
                Classes: node.Classes,
                DisplayName: DescribeTreeNode(node),
                Source: source));

        for (var i = 0; i < node.Children.Count; i++)
        {
            FlattenTreeNode(
                node.Children[i],
                nodePath + "/" + i,
                nodePath,
                depth + 1,
                includeSourceLocations,
                output);
        }
    }

    private List<CapturedTreeNode> BuildFastTreeNodeCaptureEntries(
        string scope,
        bool includeSourceLocations,
        bool includeVisualDetails,
        CancellationToken cancellationToken)
    {
        var result = new List<CapturedTreeNode>(512);
        var visited = new HashSet<AvaloniaObject>(ReferenceEqualityComparer.Instance);

        switch (scope)
        {
            case "visual":
                FlattenVisualNodeFast(_root, "0", null, 0, includeSourceLocations, includeVisualDetails, "visual", visited, result, cancellationToken);
                break;
            case "logical":
                FlattenLogicalNodeFast(_root, "0", null, 0, includeSourceLocations, includeVisualDetails, "logical", visited, result, cancellationToken);
                break;
            default:
                FlattenCombinedNodeFast(_root, "0", null, 0, includeSourceLocations, includeVisualDetails, "combined-logical", visited, result, cancellationToken);
                break;
        }

        if (result.Count == 0)
        {
            result.Add(CreateFallbackRootCapture(scope, includeSourceLocations, includeVisualDetails));
        }

        return result;
    }

    private void FlattenVisualNodeFast(
        AvaloniaObject node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeSourceLocations,
        bool includeVisualDetails,
        string relationshipKind,
        HashSet<AvaloniaObject> visited,
        List<CapturedTreeNode> output,
        CancellationToken cancellationToken)
    {
        if (ShouldStopTraversal(cancellationToken, visited, node))
        {
            return;
        }

        AddFastNodeCapture(node, nodePath, parentNodePath, depth, includeSourceLocations, includeVisualDetails, relationshipKind, output);

        if (node is not Visual visual)
        {
            return;
        }

        for (var i = 0; i < visual.VisualChildren.Count; i++)
        {
            if (visual.VisualChildren[i] is not AvaloniaObject child)
            {
                continue;
            }

            FlattenVisualNodeFast(
                child,
                nodePath + "/" + i,
                nodePath,
                depth + 1,
                includeSourceLocations,
                includeVisualDetails,
                relationshipKind,
                visited,
                output,
                cancellationToken);
        }
    }

    private void FlattenLogicalNodeFast(
        AvaloniaObject node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeSourceLocations,
        bool includeVisualDetails,
        string relationshipKind,
        HashSet<AvaloniaObject> visited,
        List<CapturedTreeNode> output,
        CancellationToken cancellationToken)
    {
        if (ShouldStopTraversal(cancellationToken, visited, node))
        {
            return;
        }

        AddFastNodeCapture(node, nodePath, parentNodePath, depth, includeSourceLocations, includeVisualDetails, relationshipKind, output);

        if (node is not ILogical logical)
        {
            return;
        }

        var childIndex = 0;
        foreach (var logicalChild in logical.LogicalChildren)
        {
            if (logicalChild is not AvaloniaObject child)
            {
                continue;
            }

            FlattenLogicalNodeFast(
                child,
                nodePath + "/" + childIndex,
                nodePath,
                depth + 1,
                includeSourceLocations,
                includeVisualDetails,
                relationshipKind,
                visited,
                output,
                cancellationToken);
            childIndex++;
        }
    }

    private void FlattenCombinedNodeFast(
        AvaloniaObject node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeSourceLocations,
        bool includeVisualDetails,
        string relationshipKind,
        HashSet<AvaloniaObject> visited,
        List<CapturedTreeNode> output,
        CancellationToken cancellationToken)
    {
        if (ShouldStopTraversal(cancellationToken, visited, node))
        {
            return;
        }

        AddFastNodeCapture(node, nodePath, parentNodePath, depth, includeSourceLocations, includeVisualDetails, relationshipKind, output);

        var childIndex = 0;
        if (node is ILogical logical)
        {
            foreach (var logicalChild in logical.LogicalChildren)
            {
                if (logicalChild is not AvaloniaObject child)
                {
                    continue;
                }

                FlattenCombinedNodeFast(
                    child,
                    nodePath + "/" + childIndex,
                    nodePath,
                    depth + 1,
                    includeSourceLocations,
                    includeVisualDetails,
                    "combined-logical",
                    visited,
                    output,
                    cancellationToken);
                childIndex++;
            }
        }

        if (node is not Control control)
        {
            return;
        }

        var templateRoots = _templateProvider.GetTemplateRoots(control);
        if (templateRoots.Count == 0)
        {
            return;
        }

        var templatePath = nodePath + "/" + childIndex;
        output.Add(
            new CapturedTreeNode(
                NodeId: _nodeIdentityProvider.GetNodeId(control),
                NodePath: templatePath,
                ParentNodePath: nodePath,
                Depth: depth + 1,
                Type: "/template/",
                ElementName: null,
                Classes: string.Empty,
                DisplayName: "/template/",
                SourceType: includeSourceLocations ? control.GetType() : null,
                RelationshipKind: "template-host",
                IsVisible: !includeVisualDetails || control.IsVisible,
                Opacity: includeVisualDetails ? control.Opacity : 1d,
                ZIndex: includeVisualDetails ? GetVisualZIndex(control) : 0,
                Bounds: includeVisualDetails ? TryGetVisualBounds(control) : null));

        for (var i = 0; i < templateRoots.Count; i++)
        {
            FlattenVisualNodeFast(
                templateRoots[i],
                templatePath + "/" + i,
                templatePath,
                depth + 2,
                includeSourceLocations,
                includeVisualDetails,
                "template-visual",
                visited,
                output,
                cancellationToken);
        }
    }

    private static bool ShouldStopTraversal(
        CancellationToken cancellationToken,
        HashSet<AvaloniaObject> visited,
        AvaloniaObject node)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visited.Add(node))
        {
            return true;
        }

        return false;
    }

    private void AddFastNodeCapture(
        AvaloniaObject node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeSourceLocations,
        bool includeVisualDetails,
        string relationshipKind,
        List<CapturedTreeNode> output)
    {
        var type = node.GetType().Name;
        var elementName = node is INamed named ? named.Name : null;
        var classes = node is StyledElement { Classes.Count: > 0 } styleable
            ? "(" + string.Join(" ", styleable.Classes) + ")"
            : string.Empty;
        var displayName = type
                          + (string.IsNullOrWhiteSpace(elementName) ? string.Empty : "#" + elementName)
                          + (classes.Length == 0 ? string.Empty : " " + classes);
        var isVisible = true;
        var opacity = 1d;
        var zIndex = 0;
        RemoteRectSnapshot? bounds = null;
        if (includeVisualDetails && node is Visual visual)
        {
            isVisible = visual.IsVisible;
            opacity = visual.Opacity;
            zIndex = GetVisualZIndex(visual);
            bounds = TryGetVisualBounds(visual);
        }

        output.Add(
            new CapturedTreeNode(
                NodeId: _nodeIdentityProvider.GetNodeId(node),
                NodePath: nodePath,
                ParentNodePath: parentNodePath,
                Depth: depth,
                Type: type,
                ElementName: string.IsNullOrWhiteSpace(elementName) ? null : elementName,
                Classes: classes,
                DisplayName: displayName,
                SourceType: includeSourceLocations ? node.GetType() : null,
                RelationshipKind: relationshipKind,
                IsVisible: isVisible,
                Opacity: opacity,
                ZIndex: zIndex,
                Bounds: bounds));
    }

    private async ValueTask<RemoteTreeSnapshot> ProjectTreeSnapshotAsync(
        TreeSnapshotUiCapture capture,
        CancellationToken cancellationToken)
    {
        var count = capture.Nodes.Count;
        var projected = new RemoteTreeNodeSnapshot[count];
        Dictionary<Type, RemoteSourceLocationSnapshot>? projectedSourceCache = null;
        if (capture.IncludeSourceLocations)
        {
            projectedSourceCache = new Dictionary<Type, RemoteSourceLocationSnapshot>();
        }

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldYieldProjection(i))
            {
                await Task.Yield();
            }

            var node = capture.Nodes[i];
            var source = capture.IncludeSourceLocations && node.SourceType is not null
                ? ResolveProjectedTreeSourceSnapshot(node.SourceType, projectedSourceCache!)
                : s_emptySourceSnapshot;
            projected[i] = new RemoteTreeNodeSnapshot(
                NodeId: node.NodeId,
                NodePath: node.NodePath,
                ParentNodePath: node.ParentNodePath,
                Depth: node.Depth,
                Type: node.Type,
                ElementName: node.ElementName,
                Classes: node.Classes,
                DisplayName: node.DisplayName,
                Source: source,
                RelationshipKind: node.RelationshipKind,
                IsVisible: node.IsVisible,
                Opacity: node.Opacity,
                ZIndex: node.ZIndex,
                Bounds: node.Bounds);
        }

        var snapshot = new RemoteTreeSnapshot(
            SnapshotVersion: SnapshotVersion,
            Generation: capture.Generation,
            Scope: capture.Scope,
            Nodes: projected);

        await InvokeOnUiThreadAsync(() =>
        {
            StoreTreeSnapshot(capture.CacheKey, capture.Generation, snapshot);
            return 0;
        }, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private RemoteSourceLocationSnapshot ResolveProjectedTreeSourceSnapshot(
        Type sourceType,
        Dictionary<Type, RemoteSourceLocationSnapshot> projectedCache)
    {
        if (projectedCache.TryGetValue(sourceType, out var cached))
        {
            return cached;
        }

        var info = _sourceLocationService.Resolve(sourceType);
        var snapshot = new RemoteSourceLocationSnapshot(
            Xaml: info.XamlLocation?.DisplayText,
            Code: info.CodeLocation?.DisplayText,
            Status: info.Status,
            XamlLocation: ToRemoteSourceDocument(info.XamlLocation),
            CodeLocation: ToRemoteSourceDocument(info.CodeLocation));
        projectedCache[sourceType] = snapshot;
        return snapshot;
    }

    private static bool ShouldYieldProjection(int index)
    {
        return index > 0 && (index % ProjectionYieldInterval) == 0;
    }

    private async ValueTask<RemoteElements3DSnapshot> ProjectElements3DSnapshotAsync(
        Elements3DSnapshotUiCapture capture,
        SnapshotCaptureContext _,
        CancellationToken cancellationToken)
    {
        var projectedNodes = capture.Nodes.Length == 0
            ? Array.Empty<RemoteElements3DNodeSnapshot>()
            : new RemoteElements3DNodeSnapshot[capture.Nodes.Length];
        for (var i = 0; i < capture.Nodes.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldYieldProjection(i))
            {
                await Task.Yield();
            }

            var node = capture.Nodes[i];
            projectedNodes[i] = new RemoteElements3DNodeSnapshot(
                NodeId: node.NodeId,
                NodePath: node.NodePath,
                Depth: node.Depth,
                Node: node.Node,
                ZIndex: node.ZIndex,
                Bounds: new RemoteRectSnapshot(
                    X: node.BoundsX,
                    Y: node.BoundsY,
                    Width: node.BoundsWidth,
                    Height: node.BoundsHeight),
                IsVisible: node.IsVisible,
                Opacity: node.Opacity,
                IsRendered: node.IsRendered,
                IsSelected: node.IsSelected);
        }

        return new RemoteElements3DSnapshot(
            SnapshotVersion: SnapshotVersion,
            Generation: capture.Generation,
            Status: "ok",
            InspectedRoot: capture.InspectedRoot,
            MainRootNodeId: capture.MainRootNodeId,
            CurrentRootNodeId: capture.CurrentRootNodeId,
            ScopedSelectionNodeId: capture.ScopedSelectionNodeId,
            SelectedNodeId: capture.SelectedNodeId,
            IsScopedToSelectionBranch: capture.IsScopedToSelectionBranch,
            NodeCount: capture.NodeCount,
            VisibleNodeCount: capture.VisibleNodeCount,
            ShowInvisibleNodes: capture.ShowInvisibleNodes,
            ShowExploded3DView: capture.ShowExploded3DView,
            ShowAllLayersInGrid: capture.ShowAllLayersInGrid,
            DepthSpacing: capture.DepthSpacing,
            Flat2DMaxLayersPerRow: capture.Flat2DMaxLayersPerRow,
            Tilt: capture.Tilt,
            Zoom: capture.Zoom,
            OrbitYaw: capture.OrbitYaw,
            OrbitPitch: capture.OrbitPitch,
            OrbitRoll: capture.OrbitRoll,
            AvailableMinDepth: capture.AvailableMinDepth,
            AvailableMaxDepth: capture.AvailableMaxDepth,
            MinVisibleDepth: capture.MinVisibleDepth,
            MaxVisibleDepth: capture.MaxVisibleDepth,
            MaxVisibleElements: capture.MaxVisibleElements,
            SvgSnapshot: capture.SvgSnapshot,
            SvgViewBox: capture.SvgViewBox,
            Nodes: projectedNodes,
            VisibleNodeIds: capture.VisibleNodeIds);
    }

    private CapturedTreeNode CreateFallbackRootCapture(
        string scope,
        bool includeSourceLocations,
        bool includeVisualDetails)
    {
        var elementName = _root is INamed named ? named.Name : null;
        return new CapturedTreeNode(
            NodeId: _nodeIdentityProvider.GetNodeId(_root),
            NodePath: "0",
            ParentNodePath: null,
            Depth: 0,
            Type: _root.GetType().Name,
            ElementName: string.IsNullOrWhiteSpace(elementName) ? null : elementName,
            Classes: string.Empty,
            DisplayName: DescribeTarget(_root),
            SourceType: includeSourceLocations ? _root.GetType() : null,
            RelationshipKind: scope + "-root",
            IsVisible: !includeVisualDetails || _root is not Visual visual || visual.IsVisible,
            Opacity: includeVisualDetails && _root is Visual opacityVisual ? opacityVisual.Opacity : 1d,
            ZIndex: includeVisualDetails && _root is Visual zVisual ? GetVisualZIndex(zVisual) : 0,
            Bounds: includeVisualDetails && _root is Visual boundsVisual ? TryGetVisualBounds(boundsVisual) : null);
    }

    private static Dictionary<Visual, string> BuildVisualPathLookup(Visual? root)
    {
        var result = new Dictionary<Visual, string>(ReferenceEqualityComparer.Instance);
        if (root is null)
        {
            return result;
        }

        Traverse(root, "0");
        return result;

        void Traverse(Visual visual, string path)
        {
            result[visual] = path;
            for (var i = 0; i < visual.VisualChildren.Count; i++)
            {
                if (visual.VisualChildren[i] is Visual child)
                {
                    Traverse(child, path + "/" + i);
                }
            }
        }
    }

    private TreeLookup BuildTreeLookup(string scope)
    {
        var generation = GetSnapshotGeneration();
        if (TryGetCachedTreeLookup(scope, generation, out var cached))
        {
            return cached;
        }

        var lookup = BuildTreeLookupCore(scope);
        StoreTreeLookup(scope, generation, lookup);
        return lookup;
    }

    private TreeLookup BuildTreeLookupCore(string scope)
    {
        var nodesByPath = new Dictionary<string, AvaloniaObject>(StringComparer.Ordinal);
        var nodesById = new Dictionary<string, AvaloniaObject>(StringComparer.Ordinal);
        switch (scope)
        {
            case "visual":
                FillVisualLookupFast(_root, "0", nodesByPath, nodesById);
                break;
            case "logical":
                FillLogicalLookupFast(_root, "0", nodesByPath, nodesById);
                break;
            default:
                FillCombinedLookupFast(_root, "0", nodesByPath, nodesById);
                break;
        }

        if (nodesByPath.Count == 0)
        {
            nodesByPath["0"] = _root;
            nodesById[_nodeIdentityProvider.GetNodeId(_root)] = _root;
        }

        return new TreeLookup(nodesByPath, nodesById, Array.Empty<TreeNode>());
    }

    private void FillTreeLookup(
        TreeNode node,
        string nodePath,
        Dictionary<string, AvaloniaObject> nodesByPath,
        Dictionary<string, AvaloniaObject> nodesById)
    {
        nodesByPath[nodePath] = node.Visual;
        nodesById[_nodeIdentityProvider.GetNodeId(node.Visual)] = node.Visual;
        for (var i = 0; i < node.Children.Count; i++)
        {
            FillTreeLookup(node.Children[i], nodePath + "/" + i, nodesByPath, nodesById);
        }
    }

    private void FillVisualLookupFast(
        AvaloniaObject node,
        string nodePath,
        Dictionary<string, AvaloniaObject> nodesByPath,
        Dictionary<string, AvaloniaObject> nodesById)
    {
        nodesByPath[nodePath] = node;
        nodesById[_nodeIdentityProvider.GetNodeId(node)] = node;
        if (node is not Visual visual)
        {
            return;
        }

        for (var i = 0; i < visual.VisualChildren.Count; i++)
        {
            if (visual.VisualChildren[i] is not AvaloniaObject child)
            {
                continue;
            }

            FillVisualLookupFast(child, nodePath + "/" + i, nodesByPath, nodesById);
        }
    }

    private void FillLogicalLookupFast(
        AvaloniaObject node,
        string nodePath,
        Dictionary<string, AvaloniaObject> nodesByPath,
        Dictionary<string, AvaloniaObject> nodesById)
    {
        nodesByPath[nodePath] = node;
        nodesById[_nodeIdentityProvider.GetNodeId(node)] = node;
        if (node is not ILogical logical)
        {
            return;
        }

        var childIndex = 0;
        foreach (var logicalChild in logical.LogicalChildren)
        {
            if (logicalChild is not AvaloniaObject child)
            {
                continue;
            }

            FillLogicalLookupFast(child, nodePath + "/" + childIndex, nodesByPath, nodesById);
            childIndex++;
        }
    }

    private void FillCombinedLookupFast(
        AvaloniaObject node,
        string nodePath,
        Dictionary<string, AvaloniaObject> nodesByPath,
        Dictionary<string, AvaloniaObject> nodesById)
    {
        nodesByPath[nodePath] = node;
        nodesById[_nodeIdentityProvider.GetNodeId(node)] = node;

        var childIndex = 0;
        if (node is ILogical logical)
        {
            foreach (var logicalChild in logical.LogicalChildren)
            {
                if (logicalChild is not AvaloniaObject child)
                {
                    continue;
                }

                FillCombinedLookupFast(child, nodePath + "/" + childIndex, nodesByPath, nodesById);
                childIndex++;
            }
        }

        if (node is not Control control)
        {
            return;
        }

        var templateRoots = _templateProvider.GetTemplateRoots(control);
        if (templateRoots.Count == 0)
        {
            return;
        }

        var templatePath = nodePath + "/" + childIndex;
        // Template pseudo node maps to the owning control to match local DevTools behavior.
        nodesByPath[templatePath] = node;
        nodesById[_nodeIdentityProvider.GetNodeId(node)] = node;

        for (var i = 0; i < templateRoots.Count; i++)
        {
            FillVisualLookupFast(templateRoots[i], templatePath + "/" + i, nodesByPath, nodesById);
        }
    }

    private AvaloniaObject? ResolveTarget(RemotePropertiesSnapshotRequest request, TreeLookup lookup)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeId) &&
            lookup.TryGetByNodeId(request.NodeId!, out var byNodeId))
        {
            return byNodeId;
        }

        if (!string.IsNullOrWhiteSpace(request.NodePath) &&
            lookup.TryGet(request.NodePath!, out var byPath))
        {
            return byPath;
        }

        if (!string.IsNullOrWhiteSpace(request.ControlName))
        {
            return lookup.FindByControlName(request.ControlName!);
        }

        return null;
    }

    private AvaloniaObject? ResolveTarget(RemoteStylesSnapshotRequest request, TreeLookup lookup)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeId) &&
            lookup.TryGetByNodeId(request.NodeId!, out var byNodeId))
        {
            return byNodeId;
        }

        if (!string.IsNullOrWhiteSpace(request.NodePath) &&
            lookup.TryGet(request.NodePath!, out var byPath))
        {
            return byPath;
        }

        if (!string.IsNullOrWhiteSpace(request.ControlName))
        {
            return lookup.FindByControlName(request.ControlName!);
        }

        return null;
    }

    private AvaloniaObject? ResolveTarget(RemoteCodeDocumentsRequest request, TreeLookup lookup)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeId) &&
            lookup.TryGetByNodeId(request.NodeId!, out var byNodeId))
        {
            return byNodeId;
        }

        if (!string.IsNullOrWhiteSpace(request.NodePath) &&
            lookup.TryGet(request.NodePath!, out var byPath))
        {
            return byPath;
        }

        if (!string.IsNullOrWhiteSpace(request.ControlName))
        {
            return lookup.FindByControlName(request.ControlName!);
        }

        return null;
    }

    private AvaloniaObject? ResolveTarget(RemoteBindingsSnapshotRequest request, TreeLookup lookup)
    {
        if (!string.IsNullOrWhiteSpace(request.NodeId) &&
            lookup.TryGetByNodeId(request.NodeId!, out var byNodeId))
        {
            return byNodeId;
        }

        if (!string.IsNullOrWhiteSpace(request.NodePath) &&
            lookup.TryGet(request.NodePath!, out var byPath))
        {
            return byPath;
        }

        if (!string.IsNullOrWhiteSpace(request.ControlName))
        {
            return lookup.FindByControlName(request.ControlName!);
        }

        return null;
    }

    private static string? ResolveTargetNodePath(
        RemotePropertiesSnapshotRequest request,
        TreeLookup lookup,
        AvaloniaObject target)
    {
        if (!string.IsNullOrWhiteSpace(request.NodePath))
        {
            return request.NodePath;
        }

        return lookup.FindPath(target);
    }

    private static string? ResolveTargetNodePath(
        RemoteBindingsSnapshotRequest request,
        TreeLookup lookup,
        AvaloniaObject target)
    {
        if (!string.IsNullOrWhiteSpace(request.NodePath))
        {
            return request.NodePath;
        }

        return lookup.FindPath(target);
    }

    private static string? ResolveTargetNodePath(
        RemoteCodeDocumentsRequest request,
        TreeLookup lookup,
        AvaloniaObject target)
    {
        if (!string.IsNullOrWhiteSpace(request.NodePath))
        {
            return request.NodePath;
        }

        return lookup.FindPath(target);
    }

    private IReadOnlyList<RemotePropertySnapshot> BuildPropertySnapshots(AvaloniaObject target, bool includeClrProperties)
    {
        var properties = new List<PropertyViewModel>();
        properties.AddRange(
            AvaloniaPropertyRegistry.Instance.GetRegistered(target)
                .Union(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(target.GetType()))
                .Select(property => new AvaloniaPropertyViewModel(target, property)));

        if (includeClrProperties)
        {
            properties.AddRange(
                target.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(static property => property.GetIndexParameters().Length == 0)
                    .Select(property => new ClrPropertyViewModel(target, property)));
        }

        properties.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        return properties
            .Select(MapPropertySnapshot)
            .ToArray();
    }

    private RemotePropertySnapshot MapPropertySnapshot(PropertyViewModel property)
    {
        var propertyType = property.PropertyType;
        var editorKind = PropertyValueEditorTypeHelper.GetEditorKind(propertyType).ToString().ToLowerInvariant();
        var enumOptions = PropertyValueEditorTypeHelper.TryGetEnumType(propertyType, out var enumType)
            ? PropertyValueEditorTypeHelper.GetEnumValues(enumType).Cast<object>().Select(static value => value.ToString() ?? string.Empty).ToArray()
            : Array.Empty<string>();
        var canSetNull = !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) is not null;
        var priority = property.Priority ?? string.Empty;
        var value = property.Value;
        var hasValidationError = value is Exception;
        var validationStatus = hasValidationError
            ? "error: " + ((Exception)value).GetBaseException().Message
            : "ok";

        var source = ResolveSourceLocationSnapshot(property.DeclaringType ?? property.AssignedType);
        return new RemotePropertySnapshot(
            Name: property.Name,
            Group: property.Group,
            Type: property.Type,
            AssignedType: property.AssignedType.FullName ?? property.AssignedType.Name,
            PropertyType: property.PropertyType.FullName ?? property.PropertyType.Name,
            DeclaringType: property.DeclaringType?.FullName,
            Priority: priority,
            IsAttached: property.IsAttached,
            IsReadOnly: property.IsReadonly,
            ValueText: value?.ToString(),
            EditorKind: editorKind,
            EnumOptions: enumOptions,
            CanClearValue: property is AvaloniaPropertyViewModel && !property.IsReadonly,
            CanSetNull: canSetNull,
            IsLocal: priority.IndexOf("Local", StringComparison.OrdinalIgnoreCase) >= 0,
            IsStyle: priority.IndexOf("Style", StringComparison.OrdinalIgnoreCase) >= 0,
            IsInherited: priority.IndexOf("Inherited", StringComparison.OrdinalIgnoreCase) >= 0,
            CoercionStatus: "unknown",
            ValidationStatus: validationStatus,
            Source: source);
    }

    private IReadOnlyList<RemoteValueFrameSnapshot> BuildValueFrameSnapshots(
        AvaloniaObject target,
        CancellationToken cancellationToken)
    {
        if (target is not StyledElement styledElement)
        {
            return Array.Empty<RemoteValueFrameSnapshot>();
        }

        var clipboard = (target as Visual) is Visual visual
            ? TopLevel.GetTopLevel(visual)?.Clipboard
            : null;
        var diagnostics = styledElement.GetValueStoreDiagnostic();
        var frames = diagnostics.AppliedFrames
            .OrderBy(static frame => frame.Priority)
            .ToArray();
        if (frames.Length == 0)
        {
            return Array.Empty<RemoteValueFrameSnapshot>();
        }

        var context = styledElement.GetType().FullName ?? styledElement.GetType().Name;
        var mapped = new RemoteValueFrameSnapshot[frames.Length];
        for (var i = 0; i < frames.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var viewModel = new ValueFrameViewModel(styledElement, frames[i], clipboard, _sourceLocationService);
            mapped[i] = MapFrame(
                viewModel,
                parentId: null,
                index: i,
                context: context,
                cancellationToken);
        }

        return mapped;
    }

    private static IReadOnlyList<RemoteCodeDocumentSnapshot> BuildCodeDocumentSnapshots(SourceLocationInfo sourceInfo)
    {
        var documents = new List<RemoteCodeDocumentSnapshot>(2);
        if (sourceInfo.XamlLocation is not null)
        {
            documents.Add(CreateCodeDocumentSnapshot("xaml", sourceInfo.XamlLocation));
        }

        if (sourceInfo.CodeLocation is not null)
        {
            documents.Add(CreateCodeDocumentSnapshot("code", sourceInfo.CodeLocation));
        }

        return documents;
    }

    private static RemoteCodeDocumentSnapshot CreateCodeDocumentSnapshot(string kind, SourceDocumentLocation location)
    {
        var filePath = location.FilePath ?? string.Empty;
        var text = string.Empty;
        var exists = false;
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            exists = true;
            try
            {
                text = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                text = "Unable to load source file: " + filePath + Environment.NewLine + ex.GetBaseException().Message;
            }
        }
        else if (!string.IsNullOrWhiteSpace(filePath))
        {
            text = "Unable to load source file: " + filePath;
        }

        return new RemoteCodeDocumentSnapshot(
            Kind: kind,
            FilePath: filePath,
            DisplayName: Path.GetFileName(filePath),
            LocationText: location.DisplayText,
            Line: location.Line,
            Column: location.Column,
            MethodName: location.MethodName,
            Exists: exists,
            Text: text);
    }

    private bool TryResolveNodeByCodeLocation(
        TreeLookup lookup,
        string normalizedPath,
        int line,
        int column,
        out AvaloniaObject? target,
        out string? nodePath,
        out string? nodeId,
        out string matchKind)
    {
        AvaloniaObject? bestTarget = null;
        string? bestNodePath = null;
        string? bestNodeId = null;
        var bestMatchKind = string.Empty;
        var bestScore = int.MaxValue;

        foreach (var entry in lookup.EnumerateNodesByPath())
        {
            var sourceInfo = _sourceLocationService.ResolveObject(entry.Value);
            EvaluateCandidate("xaml", sourceInfo.XamlLocation);
            EvaluateCandidate("code", sourceInfo.CodeLocation);

            void EvaluateCandidate(string candidateKind, SourceDocumentLocation? location)
            {
                if (location is null || string.IsNullOrWhiteSpace(location.FilePath))
                {
                    return;
                }

                if (!SourceLocationTextParser.IsSameDocument(location.FilePath, normalizedPath))
                {
                    return;
                }

                var lineDelta = Math.Abs(location.Line - line);
                var columnDelta = column > 0 && location.Column > 0
                    ? Math.Abs(location.Column - column)
                    : 0;
                var score = lineDelta * 1000 + columnDelta;
                if (score > bestScore)
                {
                    return;
                }

                if (score == bestScore && string.Equals(candidateKind, "code", StringComparison.Ordinal))
                {
                    // Prefer xaml when scores are equal because tree nodes are usually defined in markup.
                    return;
                }

                bestScore = score;
                bestTarget = entry.Value;
                bestNodePath = entry.Key;
                bestNodeId = lookup.FindNodeId(entry.Value);
                bestMatchKind = candidateKind;
            }
        }

        target = bestTarget;
        nodePath = bestNodePath;
        nodeId = bestNodeId;
        matchKind = bestMatchKind;
        return bestTarget is not null;
    }

    private static RemoteValueFrameSnapshot MapFrame(
        ValueFrameViewModel frame,
        string? parentId,
        int index,
        string context,
        CancellationToken cancellationToken = default)
    {
        var source = ParseSourceLocation(frame.SourceLocation);
        var frameId = CreateStableId(
            "frame",
            context,
            index.ToString(),
            frame.Description,
            frame.SourceLocation);
        var setters = frame.Setters;
        var mappedSetters = setters.Count == 0
            ? Array.Empty<RemoteSetterSnapshot>()
            : new RemoteSetterSnapshot[setters.Count];
        for (var setterIndex = 0; setterIndex < setters.Count; setterIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mappedSetters[setterIndex] = MapSetter(
                setters[setterIndex],
                frameId: frameId,
                index: setterIndex,
                context: context);
        }

        return new RemoteValueFrameSnapshot(
            Id: frameId,
            Description: frame.Description ?? string.Empty,
            IsActive: frame.IsActive,
            SourceLocation: frame.SourceLocation,
            Setters: mappedSetters,
            Source: source,
            ParentId: parentId);
    }

    private static RemoteSetterSnapshot MapSetter(
        SetterViewModel setter,
        string? frameId,
        int index,
        string context)
    {
        var source = ParseSourceLocation(setter.SourceLocation);
        var setterId = CreateStableId(
            "setter",
            context,
            frameId,
            index.ToString(),
            setter.Name,
            setter.SourceLocation,
            setter.Value?.ToString());
        return new RemoteSetterSnapshot(
            Id: setterId,
            Name: setter.Name,
            ValueText: setter.Value?.ToString(),
            IsActive: setter.IsActive,
            SourceLocation: setter.SourceLocation,
            Source: source,
            FrameId: frameId);
    }

    private static RemoteValueFrameSnapshot[] MapFramesWithCancellation(
        IReadOnlyList<ValueFrameViewModel> frames,
        string context,
        CancellationToken cancellationToken)
    {
        if (frames.Count == 0)
        {
            return Array.Empty<RemoteValueFrameSnapshot>();
        }

        var mapped = new RemoteValueFrameSnapshot[frames.Count];
        for (var i = 0; i < frames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapped[i] = MapFrame(
                frames[i],
                parentId: null,
                index: i,
                context: context,
                cancellationToken);
        }

        return mapped;
    }

    private static RemoteSetterSnapshot[] MapSettersWithCancellation(
        IReadOnlyList<SetterViewModel> setters,
        string context,
        CancellationToken cancellationToken)
    {
        if (setters.Count == 0)
        {
            return Array.Empty<RemoteSetterSnapshot>();
        }

        var mapped = new RemoteSetterSnapshot[setters.Count];
        for (var i = 0; i < setters.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapped[i] = MapSetter(
                setters[i],
                frameId: null,
                index: i,
                context: context);
        }

        return mapped;
    }

    private static RemoteStyleResolutionSnapshot[] MapStyleResolutionEntriesWithCancellation(
        IReadOnlyList<StyleResolutionTraceEntryViewModel> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<RemoteStyleResolutionSnapshot>();
        }

        var mapped = new RemoteStyleResolutionSnapshot[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mapped[i] = MapStyleResolutionEntry(entries[i], i);
        }

        return mapped;
    }

    private RemoteViewModelContextSnapshot MapViewModelEntry(ViewModelContextEntryViewModel entry, TreeLookup lookup)
    {
        var sourceObject = entry.SourceObject;
        var source = ResolveSourceLocationSnapshot(sourceObject);
        var sourceLocation = GetSourceLocationText(source);
        var nodeId = sourceObject is AvaloniaObject avaloniaNode
            ? lookup.FindNodeId(avaloniaNode)
            : null;
        var nodePath = sourceObject is AvaloniaObject avaloniaObject
            ? lookup.FindPath(avaloniaObject)
            : null;
        return new RemoteViewModelContextSnapshot(
            Id: CreateStableId(
                "binding-context",
                nodeId,
                nodePath,
                entry.Level.ToString(),
                entry.Element,
                entry.Priority,
                entry.ViewModelType),
            Level: entry.Level,
            Element: entry.Element,
            Priority: entry.Priority,
            ViewModelType: entry.ViewModelType,
            ValuePreview: entry.ValuePreview,
            IsCurrent: entry.IsCurrent,
            SourceLocation: sourceLocation,
            Source: source,
            NodeId: nodeId,
            NodePath: nodePath);
    }

    private RemoteBindingDiagnosticSnapshot MapBindingEntry(BindingDiagnosticEntryViewModel entry, TreeLookup lookup)
    {
        var sourceObject = entry.SourceObject;
        var source = ResolveSourceLocationSnapshot(sourceObject);
        var sourceLocation = GetSourceLocationText(source);
        var nodeId = sourceObject is AvaloniaObject avaloniaNode
            ? lookup.FindNodeId(avaloniaNode)
            : null;
        var nodePath = sourceObject is AvaloniaObject avaloniaObject
            ? lookup.FindPath(avaloniaObject)
            : null;
        return new RemoteBindingDiagnosticSnapshot(
            Id: CreateStableId(
                "binding",
                nodeId,
                nodePath,
                entry.PropertyName,
                entry.OwnerType,
                entry.Priority,
                entry.BindingDescription),
            PropertyName: entry.PropertyName,
            OwnerType: entry.OwnerType,
            Priority: entry.Priority,
            Status: entry.Status,
            BindingDescription: entry.BindingDescription,
            ValuePreview: entry.ValuePreview,
            ValueType: entry.ValueType,
            Diagnostic: entry.Diagnostic,
            HasError: entry.HasError,
            SourceLocation: sourceLocation,
            Source: source,
            NodeId: nodeId,
            NodePath: nodePath);
    }

    private RemoteEventNodeSnapshot[] FlattenEventNodes(IReadOnlyList<EventTreeNodeBase> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<RemoteEventNodeSnapshot>();
        }

        var result = new List<RemoteEventNodeSnapshot>(Math.Max(32, nodes.Count * 2));
        for (var i = 0; i < nodes.Count; i++)
        {
            FlattenEventNode(nodes[i], parentId: null, depth: 0, result);
        }

        return result.ToArray();
    }

    private void FlattenEventNode(
        EventTreeNodeBase node,
        string? parentId,
        int depth,
        List<RemoteEventNodeSnapshot> output)
    {
        var (nodeId, nodeKind, ownerType, eventName) = node switch
        {
            EventTreeNode eventNode => (
                BuildEventNodeId(eventNode.Event),
                "event",
                eventNode.Event.OwnerType.FullName ?? eventNode.Event.OwnerType.Name,
                eventNode.Event.Name),
            EventOwnerTreeNode => (
                CreateStableId("event-owner", node.Text),
                "owner",
                node.Text,
                null),
            _ => (
                CreateStableId("event-node", node.Text),
                "unknown",
                null,
                null)
        };

        output.Add(new RemoteEventNodeSnapshot(
            Id: nodeId,
            ParentId: parentId,
            Depth: depth,
            NodeKind: nodeKind,
            Text: node.Text,
            IsEnabled: node.IsEnabled,
            IsVisible: node.IsVisible,
            IsExpanded: node.IsExpanded,
            OwnerType: ownerType,
            EventName: eventName));

        if (node.Children is null)
        {
            return;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            FlattenEventNode(node.Children[i], nodeId, depth + 1, output);
        }
    }

    private RemoteRecordedEventSnapshot MapRecordedEvent(FiredEvent firedEvent, TreeLookup lookup)
    {
        string? sourceNodeId = null;
        string? sourceNodePath = null;
        var sourceText = "(null)";
        if (firedEvent.Source is { } source)
        {
            sourceText = DescribeTarget(source);
            sourceNodeId = lookup.FindNodeId(source);
            sourceNodePath = lookup.FindPath(source);
        }

        var handledBy = firedEvent.HandledBy?.HandlerName;
        return new RemoteRecordedEventSnapshot(
            Id: CreateStableId(
                "event-record",
                firedEvent.TriggerTime.ToString("O"),
                firedEvent.Event.Name,
                sourceText,
                firedEvent.Originator.HandlerName,
                handledBy),
            TriggerTime: new DateTimeOffset(firedEvent.TriggerTime),
            EventName: firedEvent.Event.Name,
            Source: sourceText,
            Originator: firedEvent.Originator.HandlerName,
            HandledBy: handledBy,
            ObservedRoutes: firedEvent.ObservedRoutes.ToString(),
            IsHandled: firedEvent.IsHandled,
            SourceNodeId: sourceNodeId,
            SourceNodePath: sourceNodePath);
    }

    private RemoteBreakpointSnapshot MapBreakpoint(BreakpointEntry entry, TreeLookup lookup)
    {
        AvaloniaObject? target = null;
        if (entry.Target?.TryGetTarget(out var resolvedTarget) == true)
        {
            target = resolvedTarget;
        }

        var source = ResolveSourceLocationSnapshot(target);
        var nodeId = target is null ? null : lookup.FindNodeId(target);
        var nodePath = target is null ? null : lookup.FindPath(target);
        return new RemoteBreakpointSnapshot(
            Id: entry.Id,
            Kind: entry.Kind.ToString(),
            Name: entry.Name,
            TargetDescription: entry.TargetDescription,
            IsEnabled: entry.IsEnabled,
            HitCount: entry.HitCount,
            TriggerAfterHits: entry.TriggerAfterHits,
            SuspendExecution: entry.SuspendExecution,
            LogMessage: entry.LogMessage,
            RemoveOnceHit: entry.RemoveOnceHit,
            LastHitAt: entry.LastHitAt,
            LastHitDetails: entry.LastHitDetails,
            NodeId: nodeId,
            NodePath: nodePath,
            SourceLocation: GetSourceLocationText(source),
            Source: source);
    }

    private static RemoteLogEntrySnapshot MapLogEntry(LogEntryViewModel entry)
    {
        return new RemoteLogEntrySnapshot(
            Id: CreateStableId(
                "log-entry",
                entry.Timestamp.ToString("O"),
                entry.Level.ToString(),
                entry.Area,
                entry.Source,
                entry.Message),
            Timestamp: entry.Timestamp,
            Level: entry.Level.ToString(),
            Area: entry.Area,
            Source: entry.Source,
            Message: entry.Message);
    }

    private RemoteStyleTreeEntrySnapshot[] MapStyleTreeEntries(
        IReadOnlyList<StylesTreeEntryViewModel> entries,
        TreeLookup lookup,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<RemoteStyleTreeEntrySnapshot>();
        }

        var result = new RemoteStyleTreeEntrySnapshot[entries.Count];
        var parentStack = new Stack<(int depth, string id)>();
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            while (parentStack.Count > 0 && parentStack.Peek().depth >= entry.Depth)
            {
                parentStack.Pop();
            }

            var parentId = parentStack.Count > 0 ? parentStack.Peek().id : null;
            var mapped = MapStyleTreeEntry(entry, lookup, i, parentId);
            result[i] = mapped;
            parentStack.Push((entry.Depth, mapped.Id));
        }

        return result;
    }

    private RemoteStyleTreeEntrySnapshot MapStyleTreeEntry(
        StylesTreeEntryViewModel entry,
        TreeLookup lookup,
        int index,
        string? parentId)
    {
        var sourceObject = entry.SourceObject as AvaloniaObject;
        var source = ParseSourceLocation(entry.SourceLocation);
        var nodeId = sourceObject is null ? null : lookup.FindNodeId(sourceObject);
        var nodePath = sourceObject is null ? null : lookup.FindPath(sourceObject);
        return new RemoteStyleTreeEntrySnapshot(
            Id: CreateStableId(
                "style-tree",
                nodeId,
                nodePath,
                entry.Depth.ToString(),
                entry.Element,
                entry.ElementType,
                index.ToString()),
            Depth: entry.Depth,
            Element: entry.Element,
            ElementType: entry.ElementType,
            Classes: entry.Classes,
            PseudoClasses: entry.PseudoClasses,
            FrameCount: entry.FrameCount,
            ActiveFrameCount: entry.ActiveFrameCount,
            SourceLocation: entry.SourceLocation,
            Source: source,
            NodeId: nodeId,
            NodePath: nodePath,
            ParentId: parentId);
    }

    private static RemoteStyleResolutionSnapshot MapStyleResolutionEntry(
        StyleResolutionTraceEntryViewModel entry,
        int index)
    {
        return new RemoteStyleResolutionSnapshot(
            Id: CreateStableId(
                "style-resolution",
                index.ToString(),
                entry.Order.ToString(),
                entry.Host,
                entry.Style,
                entry.Selector,
                entry.Path),
            Order: entry.Order,
            HostLevel: entry.HostLevel,
            Host: entry.Host,
            HostKind: entry.HostKind,
            PropagationScope: entry.PropagationScope,
            LogicalDistance: entry.LogicalDistance,
            VisualDistance: entry.VisualDistance,
            StylesInitialized: entry.StylesInitialized,
            Style: entry.Style,
            StyleKind: entry.StyleKind,
            Selector: entry.Selector,
            Path: entry.Path,
            SourceLocation: entry.SourceLocation,
            AppliedCount: entry.AppliedCount,
            ActiveCount: entry.ActiveCount,
            Notes: entry.Notes,
            Source: ParseSourceLocation(entry.SourceLocation));
    }

    private void FlattenResourceNode(
        ResourceTreeNode node,
        string nodePath,
        string? parentNodePath,
        int depth,
        bool includeEntries,
        CancellationToken cancellationToken,
        List<RemoteResourceNodeSnapshot> nodes,
        List<RemoteResourceEntrySnapshot> entries)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = ResolveSourceLocationSnapshot(node.Source);
        var sourceLocation = GetSourceLocationText(source);
        var nodeSource = node.Source as AvaloniaObject;
        nodes.Add(
            new RemoteResourceNodeSnapshot(
                Id: CreateStableId("resource-node", nodePath),
                NodeId: nodeSource is null ? null : _nodeIdentityProvider.GetNodeId(nodeSource),
                NodePath: nodePath,
                ParentNodePath: parentNodePath,
                Depth: depth,
                Kind: node.GetType().Name,
                Name: node.Name,
                SecondaryText: node.SecondaryText,
                ValueType: node.ValueType,
                ValuePreview: node.ValuePreview,
                SourceLocation: sourceLocation,
                Source: source));

        if (includeEntries)
        {
            switch (node)
            {
                case ResourceEntryNode entry:
                    var entrySource = ResolveSourceLocationSnapshot(entry.Value);
                    entries.Add(
                        new RemoteResourceEntrySnapshot(
                            Id: CreateStableId("resource-entry", nodePath, entry.KeyDisplay),
                            NodeId: nodeSource is null ? null : _nodeIdentityProvider.GetNodeId(nodeSource),
                            NodePath: nodePath,
                            KeyDisplay: entry.KeyDisplay,
                            KeyType: entry.KeyTypeName,
                            ValueType: entry.ValueTypeName,
                            ValuePreview: entry.ValuePreviewText,
                            IsDeferred: entry.IsDeferred,
                            SourceLocation: GetSourceLocationText(entrySource),
                            Source: entrySource));
                    break;
                case ResourceEntryProviderNode providerEntry:
                    var providerSource = ResolveSourceLocationSnapshot(providerEntry.Provider);
                    entries.Add(
                        new RemoteResourceEntrySnapshot(
                            Id: CreateStableId("resource-entry-provider", nodePath, providerEntry.KeyDisplay),
                            NodeId: nodeSource is null ? null : _nodeIdentityProvider.GetNodeId(nodeSource),
                            NodePath: nodePath,
                            KeyDisplay: providerEntry.KeyDisplay,
                            KeyType: providerEntry.KeyTypeName,
                            ValueType: providerEntry.ValueTypeName,
                            ValuePreview: providerEntry.ValuePreviewText,
                            IsDeferred: providerEntry.IsDeferred,
                            SourceLocation: GetSourceLocationText(providerSource),
                            Source: providerSource));
                    break;
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            FlattenResourceNode(
                node.Children[i],
                nodePath + "/" + i,
                nodePath,
                depth + 1,
                includeEntries,
                cancellationToken,
                nodes,
                entries);
        }
    }

    private IEnumerable<(Uri uri, Assembly assembly, string assemblyName, string assetPath, string name, string kind)> CollectAssets(
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(Uri uri, Assembly assembly, string assemblyName, string assetPath, string name, string kind)>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (assembly.IsDynamic)
            {
                continue;
            }

            var assemblyName = assembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                continue;
            }

            var baseUri = new Uri("avares://" + assemblyName + "/");
            IEnumerable<Uri> assets;
            try
            {
                assets = AssetLoader.GetAssets(baseUri, null!);
            }
            catch
            {
                continue;
            }

            foreach (var assetUri in assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uriText = assetUri.ToString();
                if (!seen.Add(uriText))
                {
                    continue;
                }

                var assetPath = Uri.UnescapeDataString(assetUri.AbsolutePath).TrimStart('/');
                var resolvedAssemblyName = string.IsNullOrWhiteSpace(assetUri.Host) ? assemblyName : assetUri.Host;
                var name = System.IO.Path.GetFileName(assetPath);
                results.Add((assetUri, assembly, resolvedAssemblyName, assetPath, name, ClassifyAssetKind(assetPath)));
            }
        }

        results.Sort(static (left, right) =>
        {
            var byAssembly = string.Compare(left.assemblyName, right.assemblyName, StringComparison.OrdinalIgnoreCase);
            return byAssembly != 0
                ? byAssembly
                : string.Compare(left.assetPath, right.assetPath, StringComparison.OrdinalIgnoreCase);
        });

        return results;
    }

    private static string ClassifyAssetKind(string assetPath)
    {
        var extension = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();
        if (extension.Length == 0)
        {
            return "Other";
        }

        if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".ico")
        {
            return "Image";
        }

        if (extension is ".ttf" or ".otf" or ".ttc" or ".otc" or ".woff" or ".woff2")
        {
            return "Font";
        }

        if (extension is ".axaml" or ".xaml" or ".xml" or ".txt" or ".json" or ".md" or ".csv" or ".yaml" or ".yml" or ".ini" or ".config")
        {
            return "Text";
        }

        return "Other";
    }

    private RemoteSourceLocationSnapshot ResolveAssetSourceLocationSnapshot(Assembly assembly, string assetPath, string assetName)
    {
        var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "assembly";
        var cacheKey = "asset|" + assemblyName + "|" + assetPath + "|" + assetName;
        if (_sourceDocumentCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var byPath = _sourceLocationService.ResolveDocument(assembly, assetPath, assetPath);
        if (byPath is not null)
        {
            var snapshot = new RemoteSourceLocationSnapshot(
                Xaml: byPath.DisplayText,
                Code: null,
                Status: "ok",
                XamlLocation: ToRemoteSourceDocument(byPath),
                CodeLocation: null);
            _sourceDocumentCache[cacheKey] = snapshot;
            return snapshot;
        }

        var byName = _sourceLocationService.ResolveDocument(assembly, assetName, assetName);
        if (byName is not null)
        {
            var snapshot = new RemoteSourceLocationSnapshot(
                Xaml: byName.DisplayText,
                Code: null,
                Status: "ok",
                XamlLocation: ToRemoteSourceDocument(byName),
                CodeLocation: null);
            _sourceDocumentCache[cacheKey] = snapshot;
            return snapshot;
        }

        _sourceDocumentCache[cacheKey] = s_emptySourceSnapshot;
        return s_emptySourceSnapshot;
    }

    private RemoteSourceLocationSnapshot ResolveSourceLocationSnapshot(object? source)
    {
        if (source is null)
        {
            return s_emptySourceSnapshot;
        }

        if (source is Type sourceType)
        {
            return ResolveSourceLocationSnapshot(sourceType);
        }

        if (_sourceInstanceCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var info = _sourceLocationService.ResolveObject(source);
        var snapshot = new RemoteSourceLocationSnapshot(
            Xaml: info.XamlLocation?.DisplayText,
            Code: info.CodeLocation?.DisplayText,
            Status: info.Status,
            XamlLocation: ToRemoteSourceDocument(info.XamlLocation),
            CodeLocation: ToRemoteSourceDocument(info.CodeLocation));
        _sourceInstanceCache[source] = snapshot;
        return snapshot;
    }

    private RemoteSourceLocationSnapshot ResolveSourceLocationSnapshot(Type? sourceType)
    {
        if (sourceType is null)
        {
            return s_emptySourceSnapshot;
        }

        if (_treeNodeSourceCache.TryGetValue(sourceType, out var cached))
        {
            return cached;
        }

        var info = _sourceLocationService.Resolve(sourceType);
        var snapshot = new RemoteSourceLocationSnapshot(
            Xaml: info.XamlLocation?.DisplayText,
            Code: info.CodeLocation?.DisplayText,
            Status: info.Status,
            XamlLocation: ToRemoteSourceDocument(info.XamlLocation),
            CodeLocation: ToRemoteSourceDocument(info.CodeLocation));
        _treeNodeSourceCache[sourceType] = snapshot;
        return snapshot;
    }

    private string ResolveSourceLocationText(object? source)
    {
        var snapshot = ResolveSourceLocationSnapshot(source);
        return snapshot.Xaml
               ?? snapshot.Code
               ?? string.Empty;
    }

    private RemoteSourceLocationSnapshot ResolveTreeNodeSourceSnapshot(AvaloniaObject source)
    {
        return ResolveSourceLocationSnapshot(source.GetType());
    }

    private static RemoteSourceLocationSnapshot ParseSourceLocation(string? locationText)
    {
        if (!SourceLocationTextParser.TryParse(locationText, out var location))
        {
            return s_emptySourceSnapshot;
        }

        return new RemoteSourceLocationSnapshot(
            Xaml: location.DisplayText,
            Code: null,
            Status: string.Empty,
            XamlLocation: ToRemoteSourceDocument(location),
            CodeLocation: null);
    }

    private static string GetSourceLocationText(RemoteSourceLocationSnapshot source)
    {
        return source.Xaml ?? source.Code ?? string.Empty;
    }

    private static string CreateStableId(string prefix, params string?[] parts)
    {
        var builder = new StringBuilder(prefix.Length + 32);
        builder.Append(prefix).Append('|');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append(parts[i] ?? string.Empty);
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return prefix + "-" + Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }

    private static string BuildEventNodeId(RoutedEvent routedEvent)
    {
        return CreateStableId(
            "event-node",
            routedEvent.OwnerType.FullName ?? routedEvent.OwnerType.Name,
            routedEvent.Name);
    }

    private static RemoteSourceDocumentSnapshot? ToRemoteSourceDocument(SourceDocumentLocation? location)
    {
        if (location is null || string.IsNullOrWhiteSpace(location.FilePath))
        {
            return null;
        }

        return new RemoteSourceDocumentSnapshot(
            FilePath: location.FilePath,
            Line: location.Line,
            Column: location.Column,
            MethodName: location.MethodName,
            DisplayText: location.DisplayText);
    }

    private static string DescribeTreeNode(TreeNode node)
    {
        var name = string.IsNullOrWhiteSpace(node.ElementName) ? string.Empty : "#" + node.ElementName;
        var classes = string.IsNullOrWhiteSpace(node.Classes) ? string.Empty : " " + node.Classes;
        return node.Type + name + classes;
    }

    private static string DescribeTarget(AvaloniaObject target)
    {
        if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return target.GetType().Name + "#" + named.Name;
        }

        return target.GetType().Name;
    }

    private static RemoteRectSnapshot? TryGetVisualBounds(Visual visual)
    {
        var bounds = visual.Bounds;
        if (double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
        {
            return null;
        }

        if (TopLevel.GetTopLevel(visual) is Visual rootVisual &&
            visual.TranslatePoint(default, rootVisual) is { } topLeft)
        {
            return new RemoteRectSnapshot(
                X: topLeft.X,
                Y: topLeft.Y,
                Width: bounds.Width,
                Height: bounds.Height);
        }

        return new RemoteRectSnapshot(
            X: bounds.X,
            Y: bounds.Y,
            Width: bounds.Width,
            Height: bounds.Height);
    }

    private static int GetVisualZIndex(Visual visual)
    {
        return visual.GetValue(Panel.ZIndexProperty);
    }

    private RemoteTreeNodeSnapshot CreateFallbackRootNode(
        string scope,
        bool includeSourceLocations,
        bool includeVisualDetails)
    {
        var source = includeSourceLocations
            ? ResolveTreeNodeSourceSnapshot(_root)
            : s_emptySourceSnapshot;
        var elementName = _root is INamed named ? named.Name : null;
        return new RemoteTreeNodeSnapshot(
            NodeId: _nodeIdentityProvider.GetNodeId(_root),
            NodePath: "0",
            ParentNodePath: null,
            Depth: 0,
            Type: _root.GetType().Name,
            ElementName: string.IsNullOrWhiteSpace(elementName) ? null : elementName,
            Classes: string.Empty,
            DisplayName: DescribeTarget(_root),
            Source: source,
            RelationshipKind: scope + "-root",
            IsVisible: !includeVisualDetails || _root is not Visual visual || visual.IsVisible,
            Opacity: includeVisualDetails && _root is Visual opacityVisual ? opacityVisual.Opacity : 1d,
            ZIndex: includeVisualDetails && _root is Visual zVisual ? GetVisualZIndex(zVisual) : 0,
            Bounds: includeVisualDetails && _root is Visual boundsVisual ? TryGetVisualBounds(boundsVisual) : null);
    }

    private static async ValueTask<T> InvokeOnUiThreadAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess())
        {
            return operation();
        }

        return await Dispatcher.UIThread.InvokeAsync(operation, DispatcherPriority.Background, cancellationToken);
    }

    private long SnapshotGeneration => Volatile.Read(ref _treeCacheGeneration);

    private long GetSnapshotGeneration()
    {
        EnsureSnapshotCacheGeneration();
        return SnapshotGeneration;
    }

    private void EnsureSnapshotCacheGeneration()
    {
        lock (_treeCacheSync)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            if (_treeCacheTimestampTicks == 0)
            {
                _treeCacheTimestampTicks = nowTicks;
                return;
            }

            var ageMs = (nowTicks - _treeCacheTimestampTicks) * 1000d / Stopwatch.Frequency;
            if (ageMs <= TreeSnapshotCacheTtlMilliseconds)
            {
                return;
            }

            InvalidateDerivedSnapshotCaches(nowTicks);
        }
    }

    private void InvalidateDerivedSnapshotCaches(long timestampTicks)
    {
        lock (_treeCacheSync)
        {
            _treeSnapshotCache.Clear();
            _treeLookupCache.Clear();
            _propertiesSnapshotCache.Clear();
            _stylesSnapshotCache.Clear();
            _resourcesSnapshotCache.Clear();
            _assetsSnapshotCache.Clear();
            Interlocked.Increment(ref _treeCacheGeneration);
            _treeCacheTimestampTicks = timestampTicks;
        }

        RemoteRuntimeMetrics.SetSnapshotCacheEntries(0);
    }

    private static string BuildTreeSnapshotCacheKey(
        string scope,
        bool includeSourceLocations,
        bool includeVisualDetails,
        long generation)
    {
        return scope
               + '|'
               + (includeSourceLocations ? "1" : "0")
               + '|'
               + (includeVisualDetails ? "1" : "0")
               + '|'
               + generation.ToString();
    }

    private static string BuildPropertiesSnapshotCacheKey(string scope, RemotePropertiesSnapshotRequest request, long generation)
    {
        return "properties|"
               + generation.ToString()
               + '|'
               + scope
               + '|'
               + BuildTargetCacheIdentity(request.NodeId, request.NodePath, request.ControlName)
               + '|'
               + (request.IncludeClrProperties ? "1" : "0");
    }

    private static string BuildStylesSnapshotCacheKey(string scope, RemoteStylesSnapshotRequest request, long generation)
    {
        return "styles|"
               + generation.ToString()
               + '|'
               + scope
               + '|'
               + BuildTargetCacheIdentity(request.NodeId, request.NodePath, request.ControlName)
               + '|'
               + (request.IncludeTreeEntries ? "1" : "0")
               + '|'
               + (request.IncludeFrames ? "1" : "0")
               + '|'
               + (request.IncludeSetters ? "1" : "0")
               + '|'
               + (request.IncludeResolution ? "1" : "0");
    }

    private static string BuildResourcesSnapshotCacheKey(RemoteResourcesSnapshotRequest request, long generation)
    {
        return "resources|" + generation.ToString() + "|" + (request.IncludeEntries ? "1" : "0");
    }

    private static string BuildAssetsSnapshotCacheKey(long generation)
    {
        return "assets|" + generation.ToString();
    }

    private static string BuildTargetCacheIdentity(string? nodeId, string? nodePath, string? controlName)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            return "id:" + nodeId;
        }

        if (!string.IsNullOrWhiteSpace(nodePath))
        {
            return "path:" + nodePath;
        }

        if (!string.IsNullOrWhiteSpace(controlName))
        {
            return "name:" + controlName;
        }

        return "root";
    }

    private bool TryGetCachedTreeSnapshot(string cacheKey, long generation, out RemoteTreeSnapshot snapshot)
    {
        lock (_treeCacheSync)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            if (_treeSnapshotCache.TryGetValue(cacheKey, out var entry))
            {
                var ageMs = (nowTicks - entry.TimestampTicks) * 1000d / Stopwatch.Frequency;
                if (entry.Generation == generation && ageMs <= TreeSnapshotCacheTtlMilliseconds)
                {
                    snapshot = entry.Snapshot;
                    return true;
                }

                _treeSnapshotCache.Remove(cacheKey);
                RemoteRuntimeMetrics.SetSnapshotCacheEntries(_treeSnapshotCache.Count);
            }
        }

        snapshot = default!;
        return false;
    }

    private void StoreTreeSnapshot(string cacheKey, long generation, RemoteTreeSnapshot snapshot)
    {
        lock (_treeCacheSync)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            _treeSnapshotCache[cacheKey] = new TreeSnapshotCacheEntry(snapshot, generation, nowTicks);
            _treeCacheTimestampTicks = nowTicks;
            RemoteRuntimeMetrics.SetSnapshotCacheEntries(_treeSnapshotCache.Count);
        }
    }

    private bool TryGetCachedTreeLookup(string scope, long generation, out TreeLookup lookup)
    {
        lock (_treeCacheSync)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            if (_treeLookupCache.TryGetValue(scope, out var entry))
            {
                var ageMs = (nowTicks - entry.TimestampTicks) * 1000d / Stopwatch.Frequency;
                if (entry.Generation == generation && ageMs <= TreeLookupCacheTtlMilliseconds)
                {
                    lookup = new TreeLookup(entry.NodesByPath, entry.NodesById, Array.Empty<TreeNode>());
                    return true;
                }

                _treeLookupCache.Remove(scope);
            }
        }

        lookup = default!;
        return false;
    }

    private void StoreTreeLookup(string scope, long generation, TreeLookup lookup)
    {
        lock (_treeCacheSync)
        {
            var nowTicks = Stopwatch.GetTimestamp();
            _treeLookupCache[scope] = new TreeLookupCacheEntry(
                NodesByPath: lookup.NodesByPath,
                NodesById: lookup.NodesById,
                Generation: generation,
                TimestampTicks: nowTicks);
            _treeCacheTimestampTicks = nowTicks;
        }
    }

    private readonly record struct TreeSnapshotCacheEntry(RemoteTreeSnapshot Snapshot, long Generation, long TimestampTicks);
    private readonly record struct Elements3DSvgCacheEntry(
        long SceneRevision,
        int SvgWidth,
        int SvgHeight,
        int MaxSvgNodes,
        string SvgSnapshot,
        string SvgViewBox);
    private readonly record struct TreeLookupCacheEntry(
        Dictionary<string, AvaloniaObject> NodesByPath,
        Dictionary<string, AvaloniaObject> NodesById,
        long Generation,
        long TimestampTicks);

    private sealed class TreeLookup : IDisposable
    {
        private readonly Dictionary<string, AvaloniaObject> _nodesByPath;
        private readonly Dictionary<string, AvaloniaObject> _nodesById;
        private readonly TreeNode[] _roots;

        public TreeLookup(
            Dictionary<string, AvaloniaObject> nodesByPath,
            Dictionary<string, AvaloniaObject> nodesById,
            TreeNode[] roots)
        {
            _nodesByPath = nodesByPath;
            _nodesById = nodesById;
            _roots = roots;
        }

        public bool TryGet(string nodePath, out AvaloniaObject value) => _nodesByPath.TryGetValue(nodePath, out value!);

        public bool TryGetByNodeId(string nodeId, out AvaloniaObject value) =>
            _nodesById.TryGetValue(nodeId, out value!);

        public Dictionary<string, AvaloniaObject> NodesByPath => _nodesByPath;

        public Dictionary<string, AvaloniaObject> NodesById => _nodesById;

        public IEnumerable<KeyValuePair<string, AvaloniaObject>> EnumerateNodesByPath()
        {
            return _nodesByPath;
        }

        public AvaloniaObject? FindByControlName(string controlName)
        {
            foreach (var node in _nodesByPath.Values)
            {
                if (node is Control control &&
                    string.Equals(control.Name, controlName, StringComparison.Ordinal))
                {
                    return control;
                }
            }

            return null;
        }

        public string? FindNodeId(AvaloniaObject target)
        {
            foreach (var pair in _nodesById)
            {
                if (ReferenceEquals(pair.Value, target))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        public string? FindPath(AvaloniaObject target)
        {
            foreach (var pair in _nodesByPath)
            {
                if (ReferenceEquals(pair.Value, target))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        public void Dispose()
        {
            DisposeTreeNodes(_roots);
        }
    }

    private sealed class LruCache<TKey, TValue>
        where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<LruEntry>> _lookup;
        private readonly LinkedList<LruEntry> _entries = new();

        public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
        {
            _capacity = Math.Max(1, capacity);
            _lookup = new Dictionary<TKey, LinkedListNode<LruEntry>>(_capacity, comparer);
        }

        public int Count => _lookup.Count;

        public TValue this[TKey key]
        {
            set => Set(key, value);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_lookup.TryGetValue(key, out var node))
            {
                _entries.Remove(node);
                _entries.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default!;
            return false;
        }

        public void Clear()
        {
            _lookup.Clear();
            _entries.Clear();
        }

        private void Set(TKey key, TValue value)
        {
            if (_lookup.TryGetValue(key, out var existing))
            {
                existing.Value = new LruEntry(key, value);
                _entries.Remove(existing);
                _entries.AddFirst(existing);
                return;
            }

            var node = new LinkedListNode<LruEntry>(new LruEntry(key, value));
            _entries.AddFirst(node);
            _lookup[key] = node;

            while (_lookup.Count > _capacity)
            {
                var tail = _entries.Last;
                if (tail is null)
                {
                    break;
                }

                _entries.RemoveLast();
                _lookup.Remove(tail.Value.Key);
            }
        }

        private readonly record struct LruEntry(TKey Key, TValue Value);
    }
}
