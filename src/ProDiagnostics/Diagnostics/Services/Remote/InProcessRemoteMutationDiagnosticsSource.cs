using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Services;

internal sealed class InProcessRemoteMutationDiagnosticsSource : IRemoteMutationDiagnosticsSource, IDisposable
{
    private const KeyModifiers InspectHoveredModifiers = KeyModifiers.Control | KeyModifiers.Shift;
    private const string AvaloniaPropertyKind = "avalonia";
    private const string ClrPropertyKind = "clr";

    private readonly AvaloniaObject _root;
    private readonly BreakpointService? _breakpointService;
    private readonly EventsPageViewModel? _eventsPageViewModel;
    private readonly LogsPageViewModel? _logsPageViewModel;
    private readonly IRemoteStreamPauseController? _streamPauseController;
    private readonly InProcessRemoteNodeIdentityProvider _nodeIdentityProvider;
    private readonly InProcessRemoteSelectionState _selectionState;
    private readonly Elements3DPageViewModel _elements3DPageViewModel;
    private readonly InProcessRemoteOverlayState _overlayState;
    private readonly ITreeNodeProvider _combinedTreeProvider;
    private readonly ITreeNodeProvider _logicalTreeProvider;
    private readonly ITreeNodeProvider _visualTreeProvider;
    private readonly IDisposable? _inputSubscription;
    private IDisposable? _remoteOverlayAdorner;
    private Visual? _remoteOverlayTargetVisual;
    private OverlayDisplayOptions _remoteOverlayDisplayOptions;
    private TopLevel? _lastPointerRoot;
    private PixelPoint _lastPointerPosition;
    private KeyModifiers _lastKeyModifiers;
    private bool _hasPointerPosition;

    public InProcessRemoteMutationDiagnosticsSource(
        AvaloniaObject root,
        BreakpointService? breakpointService = null,
        EventsPageViewModel? eventsPageViewModel = null,
        LogsPageViewModel? logsPageViewModel = null,
        IRemoteStreamPauseController? streamPauseController = null,
        Elements3DPageViewModel? elements3DPageViewModel = null,
        InProcessRemoteOverlayState? overlayState = null,
        InProcessRemoteNodeIdentityProvider? nodeIdentityProvider = null,
        InProcessRemoteSelectionState? selectionState = null)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _breakpointService = breakpointService;
        _eventsPageViewModel = eventsPageViewModel;
        _logsPageViewModel = logsPageViewModel;
        _streamPauseController = streamPauseController;
        _nodeIdentityProvider = nodeIdentityProvider ?? new InProcessRemoteNodeIdentityProvider();
        _selectionState = selectionState ?? new InProcessRemoteSelectionState();
        _elements3DPageViewModel = elements3DPageViewModel ?? new Elements3DPageViewModel(root, selectedObjectAccessor: null);
        _overlayState = overlayState ?? new InProcessRemoteOverlayState();
        _combinedTreeProvider = new CombinedTreeNodeProvider(new TemplateVisualTreeProvider());
        _logicalTreeProvider = new LogicalTreeNodeProvider();
        _visualTreeProvider = new VisualTreeNodeProvider();
        _inputSubscription = InputManager.Instance?.Process.Subscribe(OnRawInput);
    }

    public ValueTask<RemoteMutationResult> InspectHoveredAsync(
        RemoteInspectHoveredRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (request.RequireInspectGesture && _lastKeyModifiers != InspectHoveredModifiers)
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.InspectHovered,
                    Changed: false,
                    Message: "Inspect gesture is inactive.");
            }

            var topLevel = ResolveInspectionRoot();
            if (topLevel is null)
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.InspectHovered,
                    Changed: false,
                    Message: "Inspection root is unavailable.");
            }

            var control = FindHoveredControl(topLevel);
            if (control is null)
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.InspectHovered,
                    Changed: false,
                    Message: "No hovered control found.");
            }

            if (!request.IncludeDevTools && control.DoesBelongToDevTool())
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.InspectHovered,
                    Changed: false,
                    Message: "Hovered control belongs to DevTools.");
            }

            var scope = NormalizeScope(request.Scope);
            using var lookup = BuildTreeLookup(scope);
            var target = ResolveLookupTarget(lookup, control);
            var targetPath = target is null ? null : lookup.FindPath(target);
            var targetNodeId = target is null ? null : lookup.FindNodeId(target);
            if (target is null || string.IsNullOrWhiteSpace(targetPath))
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.InspectHovered,
                    Changed: false,
                    Message: "Hovered control is outside inspected tree.");
            }

            _selectionState.SetSelection(
                scope: scope,
                nodeId: targetNodeId,
                nodePath: targetPath,
                target: DescribeTarget(target),
                targetType: target.GetType().FullName ?? target.GetType().Name);
            RefreshRemoteOverlayAdorner(scope);

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.InspectHovered,
                Changed: true,
                Message: "Inspected hovered control.",
                Target: DescribeTarget(target),
                TargetNodePath: targetPath);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetSelectionAsync(
        RemoteSetSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var scope = NormalizeScope(request.Scope);
            var previous = _selectionState.GetSnapshot(scope);
            using var lookup = BuildTreeLookup(scope);

            if (string.IsNullOrWhiteSpace(request.NodeId) &&
                string.IsNullOrWhiteSpace(request.NodePath) &&
                string.IsNullOrWhiteSpace(request.ControlName))
            {
                var cleared = _selectionState.SetSelection(scope, null, null, null, null);
                RefreshRemoteOverlayAdorner(scope);
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.SelectionSet,
                    Changed: cleared.Generation != previous.Generation,
                    Message: cleared.Generation != previous.Generation ? "Selection cleared." : "Selection unchanged.");
            }

            var target = ResolveRequiredSelectionTarget(
                lookup,
                request.NodeId,
                request.NodePath,
                request.ControlName,
                out var targetNodeId,
                out var targetPath);
            var description = DescribeTarget(target);
            var targetType = target.GetType().FullName ?? target.GetType().Name;
            var selection = _selectionState.SetSelection(scope, targetNodeId, targetPath, description, targetType);
            RefreshRemoteOverlayAdorner(scope);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.SelectionSet,
                Changed: selection.Generation != previous.Generation,
                Message: selection.Generation != previous.Generation
                    ? "Selection updated."
                    : "Selection unchanged.",
                Target: description,
                TargetNodePath: targetPath);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetPropertyAsync(
        RemoteSetPropertyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.PropertyName))
            {
                throw new RemoteMutationValidationException("PropertyName is required.");
            }

            if (request.ClearValue && request.ValueIsNull)
            {
                throw new RemoteMutationValidationException("ClearValue and ValueIsNull cannot both be true.");
            }

            var scope = NormalizeScope(request.Scope);
            using var lookup = BuildTreeLookup(scope);
            var target = ResolveRequiredTarget(lookup, request.NodeId, request.NodePath, request.ControlName, out var targetPath);
            var property = ResolvePropertyOrThrow(target, request.PropertyName, request.PropertyKind, request.PropertyDeclaringType);

            if (property.IsReadOnly)
            {
                throw new RemoteMutationValidationException("Property '" + property.DisplayName + "' is read-only.");
            }

            var before = property.GetValue(target);
            if (request.ClearValue)
            {
                if (!property.CanClearValue)
                {
                    throw new RemoteMutationValidationException(
                        "Property '" + property.DisplayName + "' does not support ClearValue.");
                }

                property.ClearValue(target);
            }
            else
            {
                var converted = ConvertPropertyValue(request, property);
                property.SetValue(target, converted);
            }

            var after = property.GetValue(target);
            var changed = !Equals(before, after);
            var valueText = FormatValue(after);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.PropertiesSet,
                Changed: changed,
                Message: "Property '" + property.DisplayName + "' set to '" + valueText + "'.",
                Target: DescribeTarget(target),
                TargetNodePath: targetPath);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetPseudoClassAsync(
        RemoteSetPseudoClassRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.PseudoClass))
            {
                throw new RemoteMutationValidationException("PseudoClass is required.");
            }

            var scope = NormalizeScope(request.Scope);
            using var lookup = BuildTreeLookup(scope);
            var target = ResolveRequiredTarget(lookup, request.NodeId, request.NodePath, request.ControlName, out var targetPath);
            if (target is not StyledElement styledElement)
            {
                throw new RemoteMutationValidationException("Target is not a StyledElement.");
            }

            var stateToken = NormalizeStateToken(request.PseudoClass);
            var before = styledElement.Classes.Contains(stateToken);
            try
            {
                styledElement.Classes.Set(stateToken, request.IsActive);
            }
            catch (Exception ex) when (stateToken.StartsWith(":", StringComparison.Ordinal))
            {
                throw new RemoteMutationValidationException(
                    "Pseudo-class '" + stateToken + "' may only be changed by the control itself: " + ex.GetBaseException().Message);
            }

            var after = styledElement.Classes.Contains(stateToken);

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.PseudoClassSet,
                Changed: before != after,
                Message: "State '" + stateToken + "' is now " + (after ? "active" : "inactive") + ".",
                Target: DescribeTarget(styledElement),
                TargetNodePath: targetPath);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetElements3DRootAsync(
        RemoteSetElements3DRootRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var scope = NormalizeScope(request.Scope);
            using var lookup = BuildTreeLookup(scope);
            var target = ResolveRequiredTarget(lookup, request.NodeId, request.NodePath, request.ControlName, out var targetPath);
            if (target is not Visual visual)
            {
                throw new RemoteMutationValidationException("Elements3D root target must be a visual.");
            }

            var viewModel = _elements3DPageViewModel;
            var beforeRoot = viewModel.InspectedRoot;
            var beforeSelectedNode = viewModel.SelectedNode;
            viewModel.InspectControl(visual);
            var changed = !string.Equals(beforeRoot, viewModel.InspectedRoot, StringComparison.Ordinal)
                          || !ReferenceEquals(beforeSelectedNode, viewModel.SelectedNode);

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.Elements3DRootSet,
                Changed: changed,
                Message: changed
                    ? "Scoped Elements3D root to selected visual."
                    : "Elements3D root unchanged.",
                Target: DescribeTarget(visual),
                TargetNodePath: targetPath,
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> ResetElements3DRootAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            _ = request;
            var viewModel = _elements3DPageViewModel;
            var beforeRoot = viewModel.InspectedRoot;
            var beforeSelectedNode = viewModel.SelectedNode;
            viewModel.ResetToMainRoot();
            var changed = !string.Equals(beforeRoot, viewModel.InspectedRoot, StringComparison.Ordinal)
                          || !ReferenceEquals(beforeSelectedNode, viewModel.SelectedNode);

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.Elements3DRootReset,
                Changed: changed,
                Message: changed
                    ? "Reset Elements3D root to main root."
                    : "Elements3D root unchanged.",
                Target: viewModel.InspectedRoot,
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetElements3DFiltersAsync(
        RemoteSetElements3DFiltersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (request.ShowInvisibleNodes is null &&
                request.ShowExploded3DView is null &&
                request.ShowAllLayersInGrid is null &&
                request.DepthSpacing is null &&
                request.Flat2DMaxLayersPerRow is null &&
                request.Tilt is null &&
                request.Zoom is null &&
                request.OrbitYaw is null &&
                request.OrbitPitch is null &&
                request.OrbitRoll is null &&
                request.MinVisibleDepth is null &&
                request.MaxVisibleDepth is null &&
                request.MaxVisibleElements is null &&
                !request.ResetProjectionView &&
                !request.ResetLayerVisibilityFilters)
            {
                throw new RemoteMutationValidationException("At least one Elements3D setting must be specified.");
            }

            if (request.MinVisibleDepth is { } minDepth && request.MaxVisibleDepth is { } maxDepth && minDepth > maxDepth)
            {
                throw new RemoteMutationValidationException("MinVisibleDepth cannot be greater than MaxVisibleDepth.");
            }

            var viewModel = _elements3DPageViewModel;
            var changed = 0;

            if (request.ResetProjectionView)
            {
                var beforeDepthSpacing = viewModel.DepthSpacing;
                var beforeFlat2D = viewModel.Flat2DMaxLayersPerRow;
                var beforeTilt = viewModel.Tilt;
                var beforeZoom = viewModel.Zoom;
                var beforeYaw = viewModel.OrbitYaw;
                var beforePitch = viewModel.OrbitPitch;
                var beforeRoll = viewModel.OrbitRoll;
                viewModel.ResetProjectionView();
                if (!AreClose(beforeDepthSpacing, viewModel.DepthSpacing) ||
                    beforeFlat2D != viewModel.Flat2DMaxLayersPerRow ||
                    !AreClose(beforeTilt, viewModel.Tilt) ||
                    !AreClose(beforeZoom, viewModel.Zoom) ||
                    !AreClose(beforeYaw, viewModel.OrbitYaw) ||
                    !AreClose(beforePitch, viewModel.OrbitPitch) ||
                    !AreClose(beforeRoll, viewModel.OrbitRoll))
                {
                    changed++;
                }
            }

            if (request.ResetLayerVisibilityFilters)
            {
                var beforeMin = viewModel.MinVisibleDepth;
                var beforeMax = viewModel.MaxVisibleDepth;
                var beforeCount = viewModel.MaxVisibleElements;
                viewModel.ResetLayerVisibilityFilters();
                if (beforeMin != viewModel.MinVisibleDepth ||
                    beforeMax != viewModel.MaxVisibleDepth ||
                    beforeCount != viewModel.MaxVisibleElements)
                {
                    changed++;
                }
            }

            if (request.ShowInvisibleNodes is { } showInvisibleNodes && viewModel.ShowInvisibleNodes != showInvisibleNodes)
            {
                viewModel.ShowInvisibleNodes = showInvisibleNodes;
                changed++;
            }

            if (request.ShowExploded3DView is { } showExploded3DView && viewModel.ShowExploded3DView != showExploded3DView)
            {
                viewModel.ShowExploded3DView = showExploded3DView;
                changed++;
            }

            if (request.ShowAllLayersInGrid is { } showAllLayersInGrid && viewModel.ShowAllLayersInGrid != showAllLayersInGrid)
            {
                viewModel.ShowAllLayersInGrid = showAllLayersInGrid;
                changed++;
            }

            if (request.DepthSpacing is { } depthSpacing)
            {
                var before = viewModel.DepthSpacing;
                viewModel.DepthSpacing = depthSpacing;
                if (!AreClose(before, viewModel.DepthSpacing))
                {
                    changed++;
                }
            }

            if (request.Flat2DMaxLayersPerRow is { } flat2DMaxLayersPerRow)
            {
                var before = viewModel.Flat2DMaxLayersPerRow;
                viewModel.Flat2DMaxLayersPerRow = flat2DMaxLayersPerRow;
                if (before != viewModel.Flat2DMaxLayersPerRow)
                {
                    changed++;
                }
            }

            if (request.Tilt is { } tilt)
            {
                var before = viewModel.Tilt;
                viewModel.Tilt = tilt;
                if (!AreClose(before, viewModel.Tilt))
                {
                    changed++;
                }
            }

            if (request.Zoom is { } zoom)
            {
                var before = viewModel.Zoom;
                viewModel.Zoom = zoom;
                if (!AreClose(before, viewModel.Zoom))
                {
                    changed++;
                }
            }

            if (request.OrbitYaw is { } orbitYaw)
            {
                var before = viewModel.OrbitYaw;
                viewModel.OrbitYaw = orbitYaw;
                if (!AreClose(before, viewModel.OrbitYaw))
                {
                    changed++;
                }
            }

            if (request.OrbitPitch is { } orbitPitch)
            {
                var before = viewModel.OrbitPitch;
                viewModel.OrbitPitch = orbitPitch;
                if (!AreClose(before, viewModel.OrbitPitch))
                {
                    changed++;
                }
            }

            if (request.OrbitRoll is { } orbitRoll)
            {
                var before = viewModel.OrbitRoll;
                viewModel.OrbitRoll = orbitRoll;
                if (!AreClose(before, viewModel.OrbitRoll))
                {
                    changed++;
                }
            }

            if (request.MinVisibleDepth is { } minVisibleDepth)
            {
                var before = viewModel.MinVisibleDepth;
                viewModel.MinVisibleDepth = minVisibleDepth;
                if (before != viewModel.MinVisibleDepth)
                {
                    changed++;
                }
            }

            if (request.MaxVisibleDepth is { } maxVisibleDepth)
            {
                var before = viewModel.MaxVisibleDepth;
                viewModel.MaxVisibleDepth = maxVisibleDepth;
                if (before != viewModel.MaxVisibleDepth)
                {
                    changed++;
                }
            }

            if (request.MaxVisibleElements is { } maxVisibleElements)
            {
                var before = viewModel.MaxVisibleElements;
                viewModel.MaxVisibleElements = maxVisibleElements;
                if (before != viewModel.MaxVisibleElements)
                {
                    changed++;
                }
            }

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.Elements3DFiltersSet,
                Changed: changed > 0,
                Message: changed > 0
                    ? "Updated Elements3D settings."
                    : "Elements3D settings unchanged.",
                Target: viewModel.InspectedRoot,
                AffectedCount: changed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetOverlayOptionsAsync(
        RemoteSetOverlayOptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (request.VisualizeMarginPadding is null &&
                request.ShowInfo is null &&
                request.ShowRulers is null &&
                request.ShowExtensionLines is null &&
                request.HighlightElements is null &&
                request.ClipToTargetBounds is null)
            {
                throw new RemoteMutationValidationException("At least one overlay option must be specified.");
            }

            var changed = _overlayState.ApplyOptions(request, out var snapshot);
            RefreshRemoteOverlayAdorner(ResolveOverlaySelectionScope());
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.OverlayOptionsSet,
                Changed: changed > 0,
                Message: changed > 0
                    ? "Updated overlay options."
                    : "Overlay options unchanged.",
                Target: snapshot.Status,
                AffectedCount: changed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetOverlayLiveHoverAsync(
        RemoteSetOverlayLiveHoverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var changed = _overlayState.SetLiveHoverEnabled(request.IsEnabled, out _);
            RefreshRemoteOverlayAdorner(ResolveOverlaySelectionScope());
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.OverlayLiveHoverSet,
                Changed: changed,
                Message: request.IsEnabled
                    ? "Enabled live-hover overlay behavior."
                    : "Disabled live-hover overlay behavior.",
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    private void RefreshRemoteOverlayAdorner(string scope)
    {
        var overlaySnapshot = _overlayState.GetSnapshot();
        if (!overlaySnapshot.HighlightElements)
        {
            ClearRemoteOverlayAdorner(dispatchIfRequired: false);
            return;
        }

        var overlayDisplayOptions = BuildOverlayDisplayOptions(overlaySnapshot);
        var targetVisual = ResolveRemoteOverlayTarget(scope, overlaySnapshot);
        if (targetVisual is null)
        {
            ClearRemoteOverlayAdorner(dispatchIfRequired: false);
            return;
        }

        if (_remoteOverlayAdorner is not null &&
            ReferenceEquals(targetVisual, _remoteOverlayTargetVisual) &&
            overlayDisplayOptions == _remoteOverlayDisplayOptions)
        {
            return;
        }

        ClearRemoteOverlayAdorner(dispatchIfRequired: false);
        _remoteOverlayAdorner = ControlHighlightAdorner.Add(targetVisual, overlayDisplayOptions);
        _remoteOverlayTargetVisual = _remoteOverlayAdorner is null ? null : targetVisual;
        _remoteOverlayDisplayOptions = overlayDisplayOptions;
    }

    private Visual? ResolveRemoteOverlayTarget(string scope, RemoteOverlayOptionsSnapshot overlaySnapshot)
    {
        if (overlaySnapshot.LiveHoverEnabled &&
            ResolveHoveredOverlayTarget() is { } hoveredTarget)
        {
            return hoveredTarget;
        }

        return ResolveSelectedOverlayTarget(scope);
    }

    private Visual? ResolveHoveredOverlayTarget()
    {
        var topLevel = ResolveInspectionRoot();
        if (topLevel is null)
        {
            return null;
        }

        var hoveredControl = FindHoveredControl(topLevel);
        return hoveredControl is Visual visual && !visual.DoesBelongToDevTool()
            ? visual
            : null;
    }

    private Visual? ResolveSelectedOverlayTarget(string scope)
    {
        var selectionSnapshot = _selectionState.GetSnapshot(scope);
        if (string.IsNullOrWhiteSpace(selectionSnapshot.NodeId) &&
            string.IsNullOrWhiteSpace(selectionSnapshot.NodePath))
        {
            return null;
        }

        using var lookup = BuildTreeLookup(scope);
        AvaloniaObject? target = null;
        if (!string.IsNullOrWhiteSpace(selectionSnapshot.NodeId) &&
            lookup.TryGetByNodeId(selectionSnapshot.NodeId, out var targetByNodeId))
        {
            target = targetByNodeId;
        }
        else if (!string.IsNullOrWhiteSpace(selectionSnapshot.NodePath) &&
                 lookup.TryGet(selectionSnapshot.NodePath, out var targetByPath))
        {
            target = targetByPath;
        }

        return target is Visual visual && !visual.DoesBelongToDevTool()
            ? visual
            : null;
    }

    private static OverlayDisplayOptions BuildOverlayDisplayOptions(RemoteOverlayOptionsSnapshot overlaySnapshot)
    {
        return new OverlayDisplayOptions(
            VisualizeMarginPadding: overlaySnapshot.VisualizeMarginPadding,
            ShowInfo: overlaySnapshot.ShowInfo,
            ShowRulers: overlaySnapshot.ShowRulers,
            ShowExtensionLines: overlaySnapshot.ShowExtensionLines);
    }

    private string ResolveOverlaySelectionScope()
    {
        foreach (var scope in new[] { "combined", "visual", "logical" })
        {
            var selection = _selectionState.GetSnapshot(scope);
            if (!string.IsNullOrWhiteSpace(selection.NodeId) ||
                !string.IsNullOrWhiteSpace(selection.NodePath))
            {
                return scope;
            }
        }

        return "combined";
    }

    public ValueTask<RemoteMutationResult> OpenCodeDocumentAsync(
        RemoteCodeDocumentOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.FilePath))
            {
                throw new RemoteMutationValidationException("FilePath is required.");
            }

            var line = request.Line <= 0 ? 1 : request.Line;
            var column = Math.Max(0, request.Column);
            var location = new SourceDocumentLocation(
                request.FilePath.Trim(),
                line,
                request.MethodName ?? string.Empty,
                column);
            var command = SourceLocationOpenCommand.Instance;
            if (!command.CanExecute(location))
            {
                return new RemoteMutationResult(
                    Operation: RemoteMutationMethods.CodeDocumentOpen,
                    Changed: false,
                    Message: "Source location is invalid.");
            }

            command.Execute(location);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.CodeDocumentOpen,
                Changed: true,
                Message: "Requested source location open.",
                Target: location.DisplayText);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> AddPropertyBreakpointAsync(
        RemoteAddPropertyBreakpointRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.PropertyName))
            {
                throw new RemoteMutationValidationException("PropertyName is required.");
            }

            var breakpointService = GetBreakpointServiceOrThrow();
            var scope = NormalizeScope(request.Scope);
            using var lookup = BuildTreeLookup(scope);
            var target = ResolveRequiredTarget(lookup, request.NodeId, request.NodePath, request.ControlName, out var targetPath);
            var property = ResolvePropertyOrThrow(target, request.PropertyName, AvaloniaPropertyKind, propertyDeclaringType: null)
                .GetAvaloniaPropertyOrThrow();

            breakpointService.AddPropertyBreakpoint(property, target, DescribeTarget(target));
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsPropertyAdd,
                Changed: true,
                Message: "Added property breakpoint for '" + property.Name + "'.",
                Target: DescribeTarget(target),
                TargetNodePath: targetPath,
                AffectedCount: 1);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> AddEventBreakpointAsync(
        RemoteAddEventBreakpointRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.EventName))
            {
                throw new RemoteMutationValidationException("EventName is required.");
            }

            var breakpointService = GetBreakpointServiceOrThrow();
            var routedEvent = ResolveRoutedEventOrThrow(request.EventName, request.EventOwnerType);

            AvaloniaObject? target = null;
            string? targetPath = null;
            var targetDescription = "(global)";
            if (!request.IsGlobal)
            {
                var scope = NormalizeScope(request.Scope);
                using var lookup = BuildTreeLookup(scope);
                target = ResolveRequiredTarget(lookup, request.NodeId, request.NodePath, request.ControlName, out targetPath);
                targetDescription = DescribeTarget(target);
            }

            breakpointService.AddEventBreakpoint(routedEvent, target, targetDescription);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsEventAdd,
                Changed: true,
                Message: "Added event breakpoint for '" + routedEvent.Name + "'.",
                Target: targetDescription,
                TargetNodePath: targetPath,
                AffectedCount: 1);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> RemoveBreakpointAsync(
        RemoteRemoveBreakpointRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.BreakpointId))
            {
                throw new RemoteMutationValidationException("BreakpointId is required.");
            }

            var breakpointService = GetBreakpointServiceOrThrow();
            var entry = breakpointService.Entries
                .FirstOrDefault(candidate => string.Equals(candidate.Id, request.BreakpointId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                throw new RemoteMutationNotFoundException("Breakpoint '" + request.BreakpointId + "' was not found.");
            }

            breakpointService.Remove(entry);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsRemove,
                Changed: true,
                Message: "Removed breakpoint '" + entry.Name + "'.",
                Target: entry.TargetDescription,
                AffectedCount: 1);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> ToggleBreakpointAsync(
        RemoteToggleBreakpointRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.BreakpointId))
            {
                throw new RemoteMutationValidationException("BreakpointId is required.");
            }

            var breakpointService = GetBreakpointServiceOrThrow();
            var entry = breakpointService.Entries
                .FirstOrDefault(candidate => string.Equals(candidate.Id, request.BreakpointId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                throw new RemoteMutationNotFoundException("Breakpoint '" + request.BreakpointId + "' was not found.");
            }

            var isEnabled = request.IsEnabled ?? !entry.IsEnabled;
            var changed = entry.IsEnabled != isEnabled;
            entry.IsEnabled = isEnabled;
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsToggle,
                Changed: changed,
                Message: "Breakpoint '" + entry.Name + "' is now " + (entry.IsEnabled ? "enabled." : "disabled."),
                Target: entry.TargetDescription,
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> ClearBreakpointsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            var breakpointService = GetBreakpointServiceOrThrow();
            var removed = breakpointService.Entries.Count;
            breakpointService.Clear();
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsClear,
                Changed: removed > 0,
                Message: "Cleared breakpoints.",
                AffectedCount: removed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetBreakpointsEnabledAsync(
        RemoteSetBreakpointsEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var breakpointService = GetBreakpointServiceOrThrow();
            var updated = 0;
            for (var i = 0; i < breakpointService.Entries.Count; i++)
            {
                var entry = breakpointService.Entries[i];
                if (entry.IsEnabled == request.IsEnabled)
                {
                    continue;
                }

                entry.IsEnabled = request.IsEnabled;
                updated++;
            }

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.BreakpointsEnabledSet,
                Changed: updated > 0,
                Message: "Breakpoints are now " + (request.IsEnabled ? "enabled." : "disabled."),
                AffectedCount: updated);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> ClearEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            var eventsPage = GetEventsPageOrThrow();
            var removed = eventsPage.RecordedEvents.Count;
            eventsPage.Clear();
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.EventsClear,
                Changed: removed > 0,
                Message: "Cleared recorded events.",
                AffectedCount: removed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetEventEnabledAsync(
        RemoteSetEventEnabledRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(request.EventId) &&
                string.IsNullOrWhiteSpace(request.EventName))
            {
                throw new RemoteMutationValidationException("EventId or EventName is required.");
            }

            var eventsPage = GetEventsPageOrThrow();
            var eventNode = ResolveEventNodeOrThrow(eventsPage, request);
            var before = eventNode.IsEnabled == true;
            eventNode.IsEnabled = request.IsEnabled;
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.EventsNodeEnabledSet,
                Changed: before != request.IsEnabled,
                Message: "Event '" + eventNode.Event.Name + "' is now " + (request.IsEnabled ? "enabled." : "disabled.") + ".",
                Target: eventNode.Event.OwnerType.Name + "." + eventNode.Event.Name,
                AffectedCount: before != request.IsEnabled ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> EnableDefaultEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            var eventsPage = GetEventsPageOrThrow();
            var before = CountEnabledEventNodes(eventsPage.Nodes);
            eventsPage.EnableDefault();
            var after = CountEnabledEventNodes(eventsPage.Nodes);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.EventsDefaultsEnable,
                Changed: before != after,
                Message: "Enabled default event subscriptions.",
                AffectedCount: after);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> DisableAllEventsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            var eventsPage = GetEventsPageOrThrow();
            var before = CountEnabledEventNodes(eventsPage.Nodes);
            eventsPage.DisableAll();
            var after = CountEnabledEventNodes(eventsPage.Nodes);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.EventsDisableAll,
                Changed: before != after,
                Message: "Disabled all event subscriptions.",
                AffectedCount: after);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> ClearLogsAsync(
        RemoteEmptyMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        return InvokeOnUiThreadAsync(() =>
        {
            var logsPage = GetLogsPageOrThrow();
            var removed = logsPage.EntryCount;
            logsPage.Clear();
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.LogsClear,
                Changed: removed > 0,
                Message: "Cleared log entries.",
                AffectedCount: removed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetLogLevelsAsync(
        RemoteSetLogLevelsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var logsPage = GetLogsPageOrThrow();
            if (request.ShowVerbose is null &&
                request.ShowDebug is null &&
                request.ShowInformation is null &&
                request.ShowWarning is null &&
                request.ShowError is null &&
                request.ShowFatal is null &&
                request.MaxEntries is null)
            {
                throw new RemoteMutationValidationException("At least one logs setting must be specified.");
            }

            var changed = 0;
            if (request.ShowVerbose is { } showVerbose && logsPage.ShowVerbose != showVerbose)
            {
                logsPage.ShowVerbose = showVerbose;
                changed++;
            }

            if (request.ShowDebug is { } showDebug && logsPage.ShowDebug != showDebug)
            {
                logsPage.ShowDebug = showDebug;
                changed++;
            }

            if (request.ShowInformation is { } showInformation && logsPage.ShowInformation != showInformation)
            {
                logsPage.ShowInformation = showInformation;
                changed++;
            }

            if (request.ShowWarning is { } showWarning && logsPage.ShowWarning != showWarning)
            {
                logsPage.ShowWarning = showWarning;
                changed++;
            }

            if (request.ShowError is { } showError && logsPage.ShowError != showError)
            {
                logsPage.ShowError = showError;
                changed++;
            }

            if (request.ShowFatal is { } showFatal && logsPage.ShowFatal != showFatal)
            {
                logsPage.ShowFatal = showFatal;
                changed++;
            }

            if (request.MaxEntries is { } maxEntries)
            {
                if (maxEntries <= 0)
                {
                    throw new RemoteMutationValidationException("MaxEntries must be greater than 0.");
                }

                if (logsPage.MaxEntries != maxEntries)
                {
                    logsPage.MaxEntries = maxEntries;
                    changed++;
                }
            }

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.LogsLevelsSet,
                Changed: changed > 0,
                Message: "Updated logs settings.",
                AffectedCount: changed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetPreviewPausedAsync(
        RemoteSetPreviewPausedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.SetPreviewPaused(request.IsPaused);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.PreviewPausedSet,
                Changed: changed,
                Message: request.IsPaused
                    ? "Paused remote preview stream emission."
                    : "Resumed remote preview stream emission.",
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetPreviewSettingsAsync(
        RemoteSetPreviewSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.ApplyPreviewSettings(request);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.PreviewSettingsSet,
                Changed: changed > 0,
                Message: changed > 0
                    ? "Updated preview settings."
                    : "Preview settings unchanged.",
                AffectedCount: changed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> InjectPreviewInputAsync(
        RemotePreviewInputRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var topLevel = ResolveInspectionRoot();
            if (topLevel is null)
            {
                throw new RemoteMutationUnavailableException("Preview input target is unavailable.");
            }

            var modifiers = BuildRawModifiers(request);
            var point = MapPreviewPoint(topLevel, request);
            var timestamp = unchecked((ulong)Environment.TickCount64);
            var eventType = request.EventType?.Trim().ToLowerInvariant() ?? "pointer_move";
            switch (eventType)
            {
                case "pointer_move":
                {
                    var args = new RawPointerEventArgs(
                        MouseDevice.Primary,
                        timestamp,
                        topLevel,
                        RawPointerEventType.Move,
                        point,
                        modifiers);
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "pointer_down":
                {
                    var args = new RawPointerEventArgs(
                        MouseDevice.Primary,
                        timestamp,
                        topLevel,
                        MapPointerDownType(request.Button),
                        point,
                        ApplyPointerButtonModifier(modifiers, request.Button, isDown: true));
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "pointer_up":
                {
                    var args = new RawPointerEventArgs(
                        MouseDevice.Primary,
                        timestamp,
                        topLevel,
                        MapPointerUpType(request.Button),
                        point,
                        ApplyPointerButtonModifier(modifiers, request.Button, isDown: false));
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "wheel":
                {
                    var args = new RawMouseWheelEventArgs(
                        MouseDevice.Primary,
                        timestamp,
                        topLevel,
                        point,
                        new Vector(request.DeltaX, request.DeltaY),
                        modifiers);
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "key_down":
                case "keydown":
                {
                    var key = ParsePreviewKey(request.Key);
                    var args = new RawKeyEventArgs(
                        KeyboardDevice.Instance,
                        timestamp,
                        topLevel,
                        RawKeyEventType.KeyDown,
                        key,
                        modifiers);
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "key_up":
                case "keyup":
                {
                    var key = ParsePreviewKey(request.Key);
                    var args = new RawKeyEventArgs(
                        KeyboardDevice.Instance,
                        timestamp,
                        topLevel,
                        RawKeyEventType.KeyUp,
                        key,
                        modifiers);
                    InputManager.Instance?.ProcessInput(args);
                    break;
                }
                case "text":
                case "text_input":
                {
                    if (!string.IsNullOrEmpty(request.Text))
                    {
                        var args = new RawTextInputEventArgs(
                            KeyboardDevice.Instance,
                            timestamp,
                            topLevel,
                            request.Text);
                        InputManager.Instance?.ProcessInput(args);
                    }

                    break;
                }
                default:
                    throw new RemoteMutationValidationException("Unsupported preview input event type: " + request.EventType);
            }

            _lastPointerRoot = topLevel;
            _lastPointerPosition = topLevel.PointToScreen(point);
            _hasPointerPosition = true;
            _lastKeyModifiers = modifiers.ToKeyModifiers();

            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.PreviewInputInject,
                Changed: true,
                Message: "Injected preview input event: " + eventType + ".",
                AffectedCount: 1);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetMetricsPausedAsync(
        RemoteSetPausedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.SetMetricsPaused(request.IsPaused);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.MetricsPausedSet,
                Changed: changed,
                Message: request.IsPaused
                    ? "Paused remote metrics stream emission."
                    : "Resumed remote metrics stream emission.",
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetMetricsSettingsAsync(
        RemoteSetMetricsSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.ApplyMetricsSettings(request);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.MetricsSettingsSet,
                Changed: changed > 0,
                Message: changed > 0
                    ? "Updated metrics settings."
                    : "Metrics settings unchanged.",
                AffectedCount: changed);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetProfilerPausedAsync(
        RemoteSetPausedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.SetProfilerPaused(request.IsPaused);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.ProfilerPausedSet,
                Changed: changed,
                Message: request.IsPaused
                    ? "Paused remote profiler stream emission."
                    : "Resumed remote profiler stream emission.",
                AffectedCount: changed ? 1 : 0);
        }, cancellationToken);
    }

    public ValueTask<RemoteMutationResult> SetProfilerSettingsAsync(
        RemoteSetProfilerSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return InvokeOnUiThreadAsync(() =>
        {
            var streamPauseController = GetStreamPauseControllerOrThrow();
            var changed = streamPauseController.ApplyProfilerSettings(request);
            return new RemoteMutationResult(
                Operation: RemoteMutationMethods.ProfilerSettingsSet,
                Changed: changed > 0,
                Message: changed > 0
                    ? "Updated profiler settings."
                    : "Profiler settings unchanged.",
                AffectedCount: changed);
        }, cancellationToken);
    }

    public void Dispose()
    {
        ClearRemoteOverlayAdorner(dispatchIfRequired: true);
        _inputSubscription?.Dispose();
    }

    private void ClearRemoteOverlayAdorner(bool dispatchIfRequired)
    {
        var adorner = _remoteOverlayAdorner;
        _remoteOverlayAdorner = null;
        _remoteOverlayTargetVisual = null;
        _remoteOverlayDisplayOptions = default;
        if (adorner is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            adorner.Dispose();
            return;
        }

        if (!dispatchIfRequired)
        {
            return;
        }

        try
        {
            Dispatcher.UIThread.Post(
                () => adorner.Dispose(),
                DispatcherPriority.Send);
        }
        catch
        {
            // Best-effort cleanup during shutdown.
        }
    }

    private void OnRawInput(RawInputEventArgs rawInput)
    {
        switch (rawInput)
        {
            case RawPointerEventArgs pointerEventArgs:
                if (pointerEventArgs.Root is Visual pointerRootVisual &&
                    pointerRootVisual.DoesBelongToDevTool())
                {
                    break;
                }

                _lastKeyModifiers = pointerEventArgs.InputModifiers.ToKeyModifiers();
                if (pointerEventArgs.Root is Visual visual)
                {
                    _lastPointerPosition = visual.PointToScreen(pointerEventArgs.Position);
                    _hasPointerPosition = true;
                }

                if (pointerEventArgs.Root is PopupRoot popupRoot && popupRoot.ParentTopLevel is { } popupParent)
                {
                    _lastPointerRoot = popupParent;
                }
                else if (pointerEventArgs.Root is TopLevel topLevel)
                {
                    _lastPointerRoot = topLevel;
                }

                if (_overlayState.IsLiveHoverEnabled)
                {
                    RefreshRemoteOverlayAdorner(ResolveOverlaySelectionScope());
                }
                break;

            case RawKeyEventArgs keyEventArgs:
                if (keyEventArgs.Root is Visual keyRootVisual &&
                    keyRootVisual.DoesBelongToDevTool())
                {
                    break;
                }

                var modifiers = keyEventArgs.Modifiers.ToKeyModifiers();
                _lastKeyModifiers = keyEventArgs.Type switch
                {
                    RawKeyEventType.KeyDown => MergeModifiersOnKeyDown(keyEventArgs.Key, modifiers),
                    RawKeyEventType.KeyUp => MergeModifiersOnKeyUp(keyEventArgs.Key, modifiers),
                    _ => modifiers,
                };
                break;
        }
    }

    private static KeyModifiers MergeModifiersOnKeyDown(Key key, KeyModifiers modifiers)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => modifiers | KeyModifiers.Control,
            Key.LeftShift or Key.RightShift => modifiers | KeyModifiers.Shift,
            Key.LeftAlt or Key.RightAlt => modifiers | KeyModifiers.Alt,
            _ => modifiers,
        };
    }

    private static KeyModifiers MergeModifiersOnKeyUp(Key key, KeyModifiers modifiers)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => modifiers & ~KeyModifiers.Control,
            Key.LeftShift or Key.RightShift => modifiers & ~KeyModifiers.Shift,
            Key.LeftAlt or Key.RightAlt => modifiers & ~KeyModifiers.Alt,
            _ => modifiers,
        };
    }

    private TopLevel? ResolveInspectionRoot()
    {
        if (_lastPointerRoot is { } pointerRoot)
        {
            return pointerRoot;
        }

        if (_root is PopupRoot popupRoot && popupRoot.ParentTopLevel is { } popupParent)
        {
            return popupParent;
        }

        if (_root is TopLevel topLevel)
        {
            return topLevel;
        }

        if (_root is Visual visual)
        {
            return TopLevel.GetTopLevel(visual);
        }

        return null;
    }

    private Control? FindHoveredControl(TopLevel topLevel)
    {
        foreach (var popupRoot in GetPopupRoots(topLevel))
        {
            var popupHoveredControl = GetHoveredControl(popupRoot);
            if (popupHoveredControl is not null)
            {
                return popupHoveredControl;
            }
        }

        return GetHoveredControl(topLevel);
    }

    private Control? GetHoveredControl(TopLevel topLevel)
    {
        if (!_hasPointerPosition)
        {
            return null;
        }

        var point = topLevel.PointToClient(_lastPointerPosition);
        var hoveredVisual = topLevel.GetVisualsAt(point, static visual =>
            {
                if (visual is AdornerLayer || !visual.IsVisible)
                {
                    return false;
                }

                return visual is not IInputElement inputElement || inputElement.IsHitTestVisible;
            })
            .FirstOrDefault();

        if (hoveredVisual is null)
        {
            return null;
        }

        if (hoveredVisual is Control hoveredControl)
        {
            return hoveredControl;
        }

        return hoveredVisual.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault();
    }

    private static List<PopupRoot> GetPopupRoots(TopLevel root)
    {
        var popupRoots = new List<PopupRoot>();

        void ProcessProperty<T>(Control control, AvaloniaProperty<T> property)
        {
            if (control.GetValue(property) is IPopupHostProvider popupProvider &&
                popupProvider.PopupHost is PopupRoot popupRoot)
            {
                popupRoots.Add(popupRoot);
            }
        }

        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            if (control is Popup popup && popup.Host is PopupRoot popupRoot)
            {
                popupRoots.Add(popupRoot);
            }

            ProcessProperty(control, Control.ContextFlyoutProperty);
            ProcessProperty(control, Control.ContextMenuProperty);
            ProcessProperty(control, FlyoutBase.AttachedFlyoutProperty);
            ProcessProperty(control, ToolTipDiagnostics.ToolTipProperty);
            ProcessProperty(control, Button.FlyoutProperty);
        }

        return popupRoots;
    }

    private static AvaloniaObject? ResolveLookupTarget(TreeLookup lookup, AvaloniaObject target)
    {
        if (lookup.FindPath(target) is not null)
        {
            return target;
        }

        if (target is Visual visual)
        {
            foreach (var ancestor in visual.GetVisualAncestors().OfType<AvaloniaObject>())
            {
                if (lookup.FindPath(ancestor) is not null)
                {
                    return ancestor;
                }
            }
        }

        if (target is ILogical logical)
        {
            for (var cursor = logical.LogicalParent; cursor is AvaloniaObject parent; cursor = (parent as ILogical)?.LogicalParent)
            {
                if (lookup.FindPath(parent) is not null)
                {
                    return parent;
                }
            }
        }

        return null;
    }

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

    private TreeLookup BuildTreeLookup(string scope)
    {
        var roots = ResolveTreeProvider(scope).Create(_root);
        var nodesByPath = new Dictionary<string, AvaloniaObject>(StringComparer.Ordinal);
        var nodesById = new Dictionary<string, AvaloniaObject>(StringComparer.Ordinal);
        for (var i = 0; i < roots.Length; i++)
        {
            FillTreeLookup(roots[i], i.ToString(), nodesByPath, nodesById);
        }

        if (nodesByPath.Count == 0)
        {
            nodesByPath["0"] = _root;
            nodesById[_nodeIdentityProvider.GetNodeId(_root)] = _root;
        }

        return new TreeLookup(nodesByPath, nodesById, roots);
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

    private static AvaloniaObject ResolveRequiredTarget(
        TreeLookup lookup,
        string? nodeId,
        string? nodePath,
        string? controlName,
        out string? targetPath)
    {
        targetPath = null;
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            if (lookup.TryGetByNodeId(nodeId, out var byNodeId))
            {
                targetPath = lookup.FindPath(byNodeId);
                return byNodeId;
            }

            throw new RemoteMutationNotFoundException("NodeId '" + nodeId + "' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(nodePath))
        {
            if (lookup.TryGet(nodePath, out var byPath))
            {
                targetPath = nodePath;
                return byPath;
            }

            throw new RemoteMutationNotFoundException("NodePath '" + nodePath + "' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(controlName))
        {
            var byName = lookup.FindByControlName(controlName);
            if (byName is null)
            {
                throw new RemoteMutationNotFoundException("ControlName '" + controlName + "' was not found.");
            }

            targetPath = lookup.FindPath(byName);
            return byName;
        }

        throw new RemoteMutationValidationException("NodeId, NodePath, or ControlName is required.");
    }

    private static AvaloniaObject ResolveRequiredSelectionTarget(
        TreeLookup lookup,
        string? nodeId,
        string? nodePath,
        string? controlName,
        out string targetNodeId,
        out string targetPath)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            if (lookup.TryGetByNodeId(nodeId, out var byNodeId))
            {
                targetNodeId = lookup.FindNodeId(byNodeId) ?? nodeId;
                targetPath = lookup.FindPath(byNodeId)
                             ?? throw new RemoteMutationNotFoundException("Selected node path could not be resolved.");
                return byNodeId;
            }

            throw new RemoteMutationNotFoundException("NodeId '" + nodeId + "' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(nodePath))
        {
            if (lookup.TryGet(nodePath, out var byPath))
            {
                targetNodeId = lookup.FindNodeId(byPath)
                               ?? throw new RemoteMutationNotFoundException("Selected node id could not be resolved.");
                targetPath = nodePath;
                return byPath;
            }

            throw new RemoteMutationNotFoundException("NodePath '" + nodePath + "' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(controlName))
        {
            var byName = lookup.FindByControlName(controlName);
            if (byName is null)
            {
                throw new RemoteMutationNotFoundException("ControlName '" + controlName + "' was not found.");
            }

            targetNodeId = lookup.FindNodeId(byName)
                           ?? throw new RemoteMutationNotFoundException("Selected node id could not be resolved.");
            targetPath = lookup.FindPath(byName)
                         ?? throw new RemoteMutationNotFoundException("Selected node path could not be resolved.");
            return byName;
        }

        throw new RemoteMutationValidationException("NodeId, NodePath, or ControlName is required.");
    }

    private static RoutedEvent ResolveRoutedEventOrThrow(string eventName, string? ownerTypeHint)
    {
        var query = RoutedEventRegistry.Instance.GetAllRegistered()
            .Where(registered => string.Equals(registered.Name, eventName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(ownerTypeHint))
        {
            query = query.Where(registered => TypeNameMatches(registered.OwnerType, ownerTypeHint!));
        }

        var matches = query.Distinct().ToArray();
        if (matches.Length == 0)
        {
            throw new RemoteMutationNotFoundException(
                "Routed event '" + eventName + "' was not found" +
                (string.IsNullOrWhiteSpace(ownerTypeHint) ? "." : " for owner '" + ownerTypeHint + "'."));
        }

        if (matches.Length > 1)
        {
            throw new RemoteMutationValidationException(
                "Routed event '" + eventName + "' is ambiguous. Provide EventOwnerType.");
        }

        return matches[0];
    }

    private static ResolvedProperty ResolvePropertyOrThrow(
        AvaloniaObject target,
        string propertyText,
        string? propertyKind,
        string? propertyDeclaringType)
    {
        var parts = ParsePropertyText(propertyText);
        var ownerTypeHint = string.IsNullOrWhiteSpace(propertyDeclaringType)
            ? parts.OwnerTypeHint
            : propertyDeclaringType;
        var normalizedKind = NormalizePropertyKind(propertyKind);

        var avaloniaMatches = normalizedKind == ClrPropertyKind
            ? Array.Empty<AvaloniaProperty>()
            : AvaloniaPropertyRegistry.Instance.GetRegistered(target)
            .Union(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(target.GetType()))
            .Distinct()
            .Where(property =>
                string.Equals(property.Name, parts.Name, StringComparison.OrdinalIgnoreCase) &&
                (ownerTypeHint is null || TypeNameMatches(property.OwnerType, ownerTypeHint)))
            .ToArray();

        var clrMatches = normalizedKind == AvaloniaPropertyKind
            ? Array.Empty<PropertyInfo>()
            : target.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Where(property =>
                    string.Equals(property.Name, parts.Name, StringComparison.OrdinalIgnoreCase) &&
                    (ownerTypeHint is null || (property.DeclaringType is not null && TypeNameMatches(property.DeclaringType, ownerTypeHint))))
                .ToArray();

        var totalMatches = avaloniaMatches.Length + clrMatches.Length;
        if (totalMatches == 0)
        {
            throw new RemoteMutationNotFoundException("Property '" + propertyText + "' was not found.");
        }

        if (totalMatches > 1)
        {
            throw new RemoteMutationValidationException(
                "Property '" + propertyText + "' is ambiguous. Provide property kind or owner type.");
        }

        if (avaloniaMatches.Length == 1)
        {
            return ResolvedProperty.Create(avaloniaMatches[0]);
        }

        return ResolvedProperty.Create(clrMatches[0]);
    }

    private static (string Name, string? OwnerTypeHint) ParsePropertyText(string propertyText)
    {
        var text = propertyText.Trim();
        if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
        {
            text = text.Substring(1, text.Length - 2);
        }

        var separatorIndex = text.LastIndexOf('.');
        if (separatorIndex > 0 && separatorIndex < text.Length - 1)
        {
            return (text.Substring(separatorIndex + 1), text.Substring(0, separatorIndex));
        }

        return (text, null);
    }

    private static bool TypeNameMatches(Type type, string typeNameHint)
    {
        return string.Equals(type.AssemblyQualifiedName, typeNameHint, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type.FullName, typeNameHint, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type.Name, typeNameHint, StringComparison.OrdinalIgnoreCase);
    }

    private static object? ConvertPropertyValue(RemoteSetPropertyRequest request, ResolvedProperty property)
    {
        var propertyType = property.PropertyType;
        var nonNullableType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var allowsNull = !nonNullableType.IsValueType || Nullable.GetUnderlyingType(propertyType) is not null;

        if (request.ValueIsNull)
        {
            if (!allowsNull)
            {
                throw new RemoteMutationValidationException(
                    "Property '" + property.DisplayName + "' does not allow null values.");
            }

            return null;
        }

        if (request.ValueText is null)
        {
            throw new RemoteMutationValidationException(
                "ValueText is required when ValueIsNull and ClearValue are false.");
        }

        try
        {
            object? converted;
            if (nonNullableType == typeof(string))
            {
                converted = request.ValueText;
            }
            else
            {
                converted = PropertyValueEditorStringConversion.FromString(request.ValueText, nonNullableType);
            }

            if (property.IsAvaloniaProperty && !property.IsValidValue(converted))
            {
                throw new RemoteMutationValidationException(
                    "Value '" + request.ValueText + "' is not valid for property '" + property.DisplayName + "'.");
            }

            return converted;
        }
        catch (RemoteMutationValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RemoteMutationValidationException(
                "Failed to convert '" + request.ValueText + "' for property '" + property.DisplayName + "': " + ex.GetBaseException().Message);
        }
    }

    private static string? NormalizePropertyKind(string? propertyKind)
    {
        if (string.IsNullOrWhiteSpace(propertyKind))
        {
            return null;
        }

        var normalized = propertyKind.Trim().ToLowerInvariant();
        return normalized switch
        {
            AvaloniaPropertyKind => AvaloniaPropertyKind,
            ClrPropertyKind => ClrPropertyKind,
            _ => null,
        };
    }

    private sealed class ResolvedProperty
    {
        private readonly AvaloniaProperty? _avaloniaProperty;
        private readonly PropertyInfo? _clrProperty;

        private ResolvedProperty(AvaloniaProperty avaloniaProperty)
        {
            _avaloniaProperty = avaloniaProperty;
            DisplayName = avaloniaProperty.Name;
            PropertyType = avaloniaProperty.PropertyType;
            IsReadOnly = avaloniaProperty.IsReadOnly;
            CanClearValue = true;
        }

        private ResolvedProperty(PropertyInfo clrProperty)
        {
            _clrProperty = clrProperty;
            DisplayName = clrProperty.Name;
            PropertyType = clrProperty.PropertyType;
            IsReadOnly = !clrProperty.CanWrite;
            CanClearValue = false;
        }

        public string DisplayName { get; }

        public Type PropertyType { get; }

        public bool IsReadOnly { get; }

        public bool CanClearValue { get; }

        public bool IsAvaloniaProperty => _avaloniaProperty is not null;

        public static ResolvedProperty Create(AvaloniaProperty property) => new(property);

        public static ResolvedProperty Create(PropertyInfo property) => new(property);

        public AvaloniaProperty GetAvaloniaPropertyOrThrow()
        {
            if (_avaloniaProperty is null)
            {
                throw new RemoteMutationValidationException(
                    "Property '" + DisplayName + "' is not an Avalonia property.");
            }

            return _avaloniaProperty;
        }

        public object? GetValue(AvaloniaObject target)
        {
            if (_avaloniaProperty is not null)
            {
                return target.GetValue(_avaloniaProperty);
            }

            return _clrProperty!.GetValue(target);
        }

        public void SetValue(AvaloniaObject target, object? value)
        {
            if (_avaloniaProperty is not null)
            {
                target.SetValue(_avaloniaProperty, value);
                return;
            }

            try
            {
                _clrProperty!.SetValue(target, value);
            }
            catch (TargetInvocationException ex)
            {
                throw new RemoteMutationValidationException(
                    "Failed to set property '" + DisplayName + "': " + ex.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                throw new RemoteMutationValidationException(
                    "Failed to set property '" + DisplayName + "': " + ex.GetBaseException().Message);
            }
        }

        public void ClearValue(AvaloniaObject target)
        {
            if (_avaloniaProperty is null)
            {
                throw new RemoteMutationValidationException(
                    "Property '" + DisplayName + "' does not support ClearValue.");
            }

            target.ClearValue(_avaloniaProperty);
        }

        public bool IsValidValue(object? value)
        {
            return _avaloniaProperty?.IsValidValue(value) ?? true;
        }
    }

    private static string NormalizeStateToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            throw new RemoteMutationValidationException("PseudoClass is required.");
        }

        return trimmed;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) <= 0.0001;
    }

    private static int CountEnabledEventNodes(IEnumerable<EventTreeNodeBase> nodes)
    {
        var enabled = 0;
        foreach (var node in nodes)
        {
            enabled += CountEnabledEventNodes(node);
        }

        return enabled;
    }

    private static int CountEnabledEventNodes(EventTreeNodeBase node)
    {
        var enabled = node.IsEnabled == true ? 1 : 0;
        if (node.Children is null)
        {
            return enabled;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            enabled += CountEnabledEventNodes(node.Children[i]);
        }

        return enabled;
    }

    private static EventTreeNode ResolveEventNodeOrThrow(
        EventsPageViewModel eventsPage,
        RemoteSetEventEnabledRequest request)
    {
        var matches = new List<EventTreeNode>(2);
        foreach (var ownerNode in eventsPage.Nodes)
        {
            Collect(ownerNode);
        }

        if (matches.Count == 0)
        {
            throw new RemoteMutationNotFoundException("Requested event node was not found.");
        }

        if (matches.Count > 1)
        {
            throw new RemoteMutationValidationException(
                "Requested event is ambiguous. Provide EventId or EventOwnerType.");
        }

        return matches[0];

        void Collect(EventTreeNodeBase node)
        {
            if (node is EventTreeNode eventNode && EventMatches(eventNode, request))
            {
                matches.Add(eventNode);
            }

            if (node.Children is null)
            {
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                Collect(node.Children[i]);
            }
        }
    }

    private static bool EventMatches(EventTreeNode eventNode, RemoteSetEventEnabledRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.EventId))
        {
            var eventId = BuildEventNodeId(eventNode.Event);
            if (!string.Equals(eventId, request.EventId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.EventName))
        {
            if (!string.Equals(eventNode.Event.Name, request.EventName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EventOwnerType))
        {
            if (!TypeNameMatches(eventNode.Event.OwnerType, request.EventOwnerType))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildEventNodeId(RoutedEvent routedEvent)
    {
        return CreateStableId(
            "event-node",
            routedEvent.OwnerType.FullName ?? routedEvent.OwnerType.Name,
            routedEvent.Name);
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

    private BreakpointService GetBreakpointServiceOrThrow()
    {
        return _breakpointService ??
               throw new RemoteMutationUnavailableException("Breakpoints controls are unavailable in this host.");
    }

    private EventsPageViewModel GetEventsPageOrThrow()
    {
        return _eventsPageViewModel ??
               throw new RemoteMutationUnavailableException("Events controls are unavailable in this host.");
    }

    private LogsPageViewModel GetLogsPageOrThrow()
    {
        return _logsPageViewModel ??
               throw new RemoteMutationUnavailableException("Logs controls are unavailable in this host.");
    }

    private IRemoteStreamPauseController GetStreamPauseControllerOrThrow()
    {
        return _streamPauseController ??
               throw new RemoteMutationUnavailableException("Stream pause controls are unavailable in this host.");
    }

    private static Point MapPreviewPoint(TopLevel topLevel, RemotePreviewInputRequest request)
    {
        var x = request.X;
        var y = request.Y;
        if (request.FrameWidth is { } frameWidth && frameWidth > 0)
        {
            x = x * topLevel.Bounds.Width / frameWidth;
        }

        if (request.FrameHeight is { } frameHeight && frameHeight > 0)
        {
            y = y * topLevel.Bounds.Height / frameHeight;
        }

        if (double.IsNaN(x) || double.IsInfinity(x))
        {
            x = 0;
        }

        if (double.IsNaN(y) || double.IsInfinity(y))
        {
            y = 0;
        }

        return new Point(
            Math.Clamp(x, 0, Math.Max(0, topLevel.Bounds.Width)),
            Math.Clamp(y, 0, Math.Max(0, topLevel.Bounds.Height)));
    }

    private static RawInputModifiers BuildRawModifiers(RemotePreviewInputRequest request)
    {
        var modifiers = RawInputModifiers.None;
        if (request.Ctrl)
        {
            modifiers |= RawInputModifiers.Control;
        }

        if (request.Shift)
        {
            modifiers |= RawInputModifiers.Shift;
        }

        if (request.Alt)
        {
            modifiers |= RawInputModifiers.Alt;
        }

        if (request.Meta)
        {
            modifiers |= RawInputModifiers.Meta;
        }

        return modifiers;
    }

    private static RawInputModifiers ApplyPointerButtonModifier(
        RawInputModifiers modifiers,
        string? button,
        bool isDown)
    {
        var flag = NormalizePointerButton(button) switch
        {
            "right" => RawInputModifiers.RightMouseButton,
            "middle" => RawInputModifiers.MiddleMouseButton,
            "x1" => RawInputModifiers.XButton1MouseButton,
            "x2" => RawInputModifiers.XButton2MouseButton,
            _ => RawInputModifiers.LeftMouseButton,
        };

        return isDown
            ? modifiers | flag
            : modifiers & ~flag;
    }

    private static RawPointerEventType MapPointerDownType(string? button)
    {
        return NormalizePointerButton(button) switch
        {
            "right" => RawPointerEventType.RightButtonDown,
            "middle" => RawPointerEventType.MiddleButtonDown,
            "x1" => RawPointerEventType.XButton1Down,
            "x2" => RawPointerEventType.XButton2Down,
            _ => RawPointerEventType.LeftButtonDown,
        };
    }

    private static RawPointerEventType MapPointerUpType(string? button)
    {
        return NormalizePointerButton(button) switch
        {
            "right" => RawPointerEventType.RightButtonUp,
            "middle" => RawPointerEventType.MiddleButtonUp,
            "x1" => RawPointerEventType.XButton1Up,
            "x2" => RawPointerEventType.XButton2Up,
            _ => RawPointerEventType.LeftButtonUp,
        };
    }

    private static string NormalizePointerButton(string? button)
    {
        var normalized = button?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "2" => "right",
            "1" => "middle",
            "3" => "x1",
            "4" => "x2",
            _ => normalized ?? "left",
        };
    }

    private static Key ParsePreviewKey(string? keyText)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            return Key.None;
        }

        var key = keyText.Trim();
        return key switch
        {
            "ArrowLeft" => Key.Left,
            "ArrowRight" => Key.Right,
            "ArrowUp" => Key.Up,
            "ArrowDown" => Key.Down,
            "Escape" => Key.Escape,
            "Esc" => Key.Escape,
            "Enter" => Key.Enter,
            "Tab" => Key.Tab,
            "Backspace" => Key.Back,
            "Delete" => Key.Delete,
            "Insert" => Key.Insert,
            "Home" => Key.Home,
            "End" => Key.End,
            "PageUp" => Key.PageUp,
            "PageDown" => Key.PageDown,
            " " => Key.Space,
            "Spacebar" => Key.Space,
            "Space" => Key.Space,
            "Control" => Key.LeftCtrl,
            "Shift" => Key.LeftShift,
            "Alt" => Key.LeftAlt,
            "Meta" => Key.LWin,
            _ => ParsePreviewKeyFallback(key),
        };
    }

    private static Key ParsePreviewKeyFallback(string key)
    {
        if (key.Length == 1 && char.IsLetter(key[0]))
        {
            var letter = char.ToUpperInvariant(key[0]).ToString();
            if (Enum.TryParse<Key>(letter, ignoreCase: true, out var parsedLetter))
            {
                return parsedLetter;
            }
        }

        if (key.Length == 1 && char.IsDigit(key[0]))
        {
            var digit = "D" + key;
            if (Enum.TryParse<Key>(digit, ignoreCase: true, out var parsedDigit))
            {
                return parsedDigit;
            }
        }

        return Enum.TryParse<Key>(key, ignoreCase: true, out var parsed)
            ? parsed
            : Key.None;
    }

    private static string DescribeTarget(AvaloniaObject target)
    {
        if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
        {
            return target.GetType().Name + "#" + named.Name;
        }

        return target.GetType().Name;
    }

    private static string FormatValue(object? value)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return "(unset)";
        }

        if (value is null)
        {
            return "null";
        }

        return PropertyValueEditorStringConversion.ToString(value) ?? value.ToString() ?? string.Empty;
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

    private static void DisposeTreeNodes(IReadOnlyList<TreeNode> roots)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            roots[i].Dispose();
        }
    }

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
}
