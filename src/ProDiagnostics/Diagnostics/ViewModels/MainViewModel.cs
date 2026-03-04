using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Rendering;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class MainViewModel : ViewModelBase, IDisposable
    {
        private const int LastVisibleTabIndex = 16;
        private const int LastTabIndex = 17;
        private const int LastRightPanelTabIndex = LastTabIndex - 3;

        private readonly AvaloniaObject _root;
        private readonly TreePageViewModel _logicalTree;
        private readonly TreePageViewModel _visualTree;
        private readonly TreePageViewModel _combinedTree;
        private readonly ResourcesPageViewModel _resources;
        private readonly CodePageViewModel _code;
        private readonly AssetsPageViewModel _assets;
        private readonly EventsPageViewModel _events;
        private readonly BreakpointService _breakpointService;
        private readonly BreakpointsPageViewModel _breakpoints;
        private readonly LogsPageViewModel _logs;
        private readonly MetricsPageViewModel _metrics;
        private readonly ViewModelsBindingsPageViewModel _viewModelsBindings;
        private readonly StylesDiagnosticsPageViewModel _stylesDiagnostics;
        private readonly TransportSettingsPageViewModel _transportSettings;
        private readonly Elements3DPageViewModel _elements3D;
        private readonly ProfilerPageViewModel _profiler;
        private readonly SettingsPageViewModel _settings;
        private readonly HotKeyPageViewModel _hotKeys;
        private readonly ISourceLocationService _sourceLocationService;
        private readonly IDisposable _pointerOverSubscription;
        private readonly InProcessRemoteSelectionState _selectionStore = new();

        private readonly HashSet<string> _pinnedProperties = new();

        private ViewModelBase? _content;
        private ViewModelBase? _treeContent;
        private ViewModelBase? _rightContent;
        private int _selectedTab = -1;
        private int _selectedTreeTab;
        private int _selectedRightTab;
        private string? _focusedControl;
        private IInputElement? _pointerOverElement;
        private bool _shouldVisualizeMarginPadding = true;
        private bool _showOverlayInfo;
        private bool _showOverlayRulers;
        private bool _showOverlayExtensionLines;
        private bool _highlightElements = true;
        private bool _liveHoverOverlay = true;
        private bool _trackFocusedControl = true;
        private bool _freezePopups;
        private string? _pointerOverElementName;
        private IInputRoot? _pointerOverRoot;
        private IScreenshotHandler? _screenshotHandler;
        private bool _showPropertyType;
        private bool _showImplementedInterfaces;
        private IBrush? _focusHighlighter;
        private IDisposable? _currentFocusHighlightAdorner;
        private IDisposable? _currentInspectionHighlightAdorner;
        private AvaloniaObject? _selectedDiagnosticsObject;
        private bool _isSynchronizingCombinedTreeSelection;
        private bool _isSynchronizingTabSelection;
        private readonly Dictionary<AvaloniaObject, SourceLocationInfo> _sourceLocationCache = new();
        private bool _isSyncingFromCodeSelection;
        private ControlDetailsViewModel? _logicalDetailsSubscription;
        private ControlDetailsViewModel? _visualDetailsSubscription;
        private ControlDetailsViewModel? _combinedDetailsSubscription;
        private DevToolsRemoteLoopbackSession? _remoteLoopbackSession;
        private IRemoteReadOnlyDiagnosticsDomainService? _remoteReadOnly;
        private IRemoteMutationDiagnosticsDomainService? _remoteMutation;
        private IDisposable? _remoteSelectionStreamSubscription;
        private bool _remoteLoopbackInitializationRequested;
        private bool _isApplyingSelectionSnapshot;
        private bool _suppressRemoteSelectionPush;

        public MainViewModel(AvaloniaObject root)
        {
            _root = root;
            _sourceLocationService = new PortablePdbSourceLocationService();
            var templateProvider = new TemplateVisualTreeProvider();
            var treeModelFactory = new TreeHierarchyModelFactory();
            var logicalProvider = new LogicalTreeNodeProvider();
            var visualProvider = new VisualTreeNodeProvider();
            var combinedProvider = new CombinedTreeNodeProvider(templateProvider);
            var resourceFormatter = new ResourceNodeFormatter();
            var resourceNodeFactory = new ResourceTreeNodeFactory(resourceFormatter);
            var resourceProvider = new ResourceTreeNodeProvider(resourceNodeFactory);
            var resourceModelFactory = new ResourceHierarchyModelFactory();

            _breakpointService = new BreakpointService();
            _logicalTree = new TreePageViewModel(this, logicalProvider.Create(root), treeModelFactory, _pinnedProperties, remoteScope: "logical");
            _visualTree = new TreePageViewModel(this, visualProvider.Create(root), treeModelFactory, _pinnedProperties, remoteScope: "visual");
            _combinedTree = new TreePageViewModel(this, combinedProvider.Create(root), treeModelFactory, _pinnedProperties, remoteScope: "combined");
            _resources = new ResourcesPageViewModel(this, resourceProvider.Create(root), resourceModelFactory, resourceFormatter);
            _code = new CodePageViewModel(GetSelectedDiagnosticsObject, OnCodeCaretLocationChanged, _sourceLocationService);
            _assets = new AssetsPageViewModel(this);
            _events = new EventsPageViewModel(this, _breakpointService);
            _breakpoints = new BreakpointsPageViewModel(_breakpointService);
            _logs = new LogsPageViewModel();
            _metrics = new MetricsPageViewModel();
            _viewModelsBindings = new ViewModelsBindingsPageViewModel(this, GetSelectedDiagnosticsObject);
            _stylesDiagnostics = new StylesDiagnosticsPageViewModel(this, GetSelectedDiagnosticsObject);
            _transportSettings = new TransportSettingsPageViewModel();
            _elements3D = new Elements3DPageViewModel(root, GetSelectedDiagnosticsObject);
            _profiler = new ProfilerPageViewModel();
            _settings = new SettingsPageViewModel(this);
            _hotKeys = new HotKeyPageViewModel();
            _treeContent = ResolveTreeTabContent(0);
            _rightContent = ResolveRightTabContent(3, inspectSelection: false);

            _logicalTree.PropertyChanged += TreePagePropertyChanged;
            _visualTree.PropertyChanged += TreePagePropertyChanged;
            _combinedTree.PropertyChanged += TreePagePropertyChanged;
            AttachDetailsSubscription(_logicalTree);
            AttachDetailsSubscription(_visualTree);
            AttachDetailsSubscription(_combinedTree);

            _stylesDiagnostics.PropertyChanged += StylesDiagnosticsPropertyChanged;
            _resources.PropertyChanged += ResourcesPagePropertyChanged;
            _assets.PropertyChanged += AssetsPagePropertyChanged;
            _elements3D.PropertyChanged += Elements3DPropertyChanged;
            _selectionStore.SelectionChanged += SelectionStoreSelectionChanged;

            UpdateFocusedControl();

            if (KeyboardDevice.Instance is not null)
            {
                KeyboardDevice.Instance.PropertyChanged += KeyboardPropertyChanged;
            }

            SelectedTab = 0;
            if (root is TopLevel topLevel)
            {
                _pointerOverRoot = topLevel;
                _pointerOverSubscription = topLevel.GetObservable(TopLevel.PointerOverElementProperty)
                    .Subscribe(x => PointerOverElement = x);
            }
            else
            {
                _pointerOverSubscription = InputManager.Instance!.PreProcess.Subscribe(e =>
                {
                    if (e is Input.Raw.RawPointerEventArgs pointerEventArgs)
                    {
                        PointerOverRoot = pointerEventArgs.Root;
                        PointerOverElement = pointerEventArgs.Root.InputHitTest(pointerEventArgs.Position);
                    }
                });
            }

            PublishSelectionToStore(GetSelectedDiagnosticsObject(), combinedNodePathHint: _combinedTree.SelectedNodePath, pushRemote: false);
        }

        public bool FreezePopups
        {
            get => _freezePopups;
            set => RaiseAndSetIfChanged(ref _freezePopups, value);
        }

        public bool ShouldVisualizeMarginPadding
        {
            get => _shouldVisualizeMarginPadding;
            set
            {
                if (RaiseAndSetIfChanged(ref _shouldVisualizeMarginPadding, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool ShowOverlayInfo
        {
            get => _showOverlayInfo;
            set
            {
                if (RaiseAndSetIfChanged(ref _showOverlayInfo, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool ShowOverlayRulers
        {
            get => _showOverlayRulers;
            set
            {
                if (RaiseAndSetIfChanged(ref _showOverlayRulers, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool ShowOverlayExtensionLines
        {
            get => _showOverlayExtensionLines;
            set
            {
                if (RaiseAndSetIfChanged(ref _showOverlayExtensionLines, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool HighlightElements
        {
            get => _highlightElements;
            set
            {
                if (RaiseAndSetIfChanged(ref _highlightElements, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool LiveHoverOverlay
        {
            get => _liveHoverOverlay;
            set
            {
                if (RaiseAndSetIfChanged(ref _liveHoverOverlay, value))
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public bool TrackFocusedControl
        {
            get => _trackFocusedControl;
            set
            {
                if (RaiseAndSetIfChanged(ref _trackFocusedControl, value))
                {
                    UpdateFocusedControl();
                }
            }
        }

        internal OverlayDisplayOptions OverlayDisplayOptions => new(
            VisualizeMarginPadding: ShouldVisualizeMarginPadding,
            ShowInfo: ShowOverlayInfo,
            ShowRulers: ShowOverlayRulers,
            ShowExtensionLines: ShowOverlayExtensionLines);

        public void ToggleVisualizeMarginPadding() => ShouldVisualizeMarginPadding = !ShouldVisualizeMarginPadding;

        public void ToggleOverlayInfo() => ShowOverlayInfo = !ShowOverlayInfo;

        public void ToggleOverlayRulers() => ShowOverlayRulers = !ShowOverlayRulers;

        public void ToggleOverlayExtensionLines() => ShowOverlayExtensionLines = !ShowOverlayExtensionLines;

        public void ToggleHighlightElements() => HighlightElements = !HighlightElements;

        public void ToggleLiveHoverOverlay() => LiveHoverOverlay = !LiveHoverOverlay;

        public void ToggleFocusTracking() => TrackFocusedControl = !TrackFocusedControl;

        public void ApplyOverlayDefaults()
        {
            ShouldVisualizeMarginPadding = true;
            ShowOverlayInfo = false;
            ShowOverlayRulers = false;
            ShowOverlayExtensionLines = false;
            HighlightElements = true;
            LiveHoverOverlay = true;
        }

        private IRenderer? TryGetRenderer()
            => _root switch
            {
                TopLevel topLevel => topLevel.Renderer,
                Avalonia.Diagnostics.Controls.Application app => app.RendererRoot,
                _ => null
            };

        private bool GetDebugOverlay(RendererDebugOverlays overlay)
            => ((TryGetRenderer()?.Diagnostics.DebugOverlays ?? RendererDebugOverlays.None) & overlay) != 0;

        private void SetDebugOverlay(RendererDebugOverlays overlay, bool enable, [CallerMemberName] string? propertyName = null)
        {
            if (TryGetRenderer() is not { } renderer)
            {
                return;
            }

            var oldValue = renderer.Diagnostics.DebugOverlays;
            var newValue = enable ? oldValue | overlay : oldValue & ~overlay;
            if (oldValue == newValue)
            {
                return;
            }

            renderer.Diagnostics.DebugOverlays = newValue;
            RaisePropertyChanged(propertyName);
        }

        public bool ShowDirtyRectsOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.DirtyRects);
            set => SetDebugOverlay(RendererDebugOverlays.DirtyRects, value);
        }

        public void ToggleDirtyRectsOverlay() => ShowDirtyRectsOverlay = !ShowDirtyRectsOverlay;

        public bool ShowFpsOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.Fps);
            set => SetDebugOverlay(RendererDebugOverlays.Fps, value);
        }

        public void ToggleFpsOverlay() => ShowFpsOverlay = !ShowFpsOverlay;

        public bool ShowLayoutTimeGraphOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.LayoutTimeGraph);
            set => SetDebugOverlay(RendererDebugOverlays.LayoutTimeGraph, value);
        }

        public void ToggleLayoutTimeGraphOverlay() => ShowLayoutTimeGraphOverlay = !ShowLayoutTimeGraphOverlay;

        public bool ShowRenderTimeGraphOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.RenderTimeGraph);
            set => SetDebugOverlay(RendererDebugOverlays.RenderTimeGraph, value);
        }

        public void ToggleRenderTimeGraphOverlay() => ShowRenderTimeGraphOverlay = !ShowRenderTimeGraphOverlay;

        public ViewModelBase? Content
        {
            get => _content;
            private set
            {
                if (_content is TreePageViewModel oldTree &&
                    value is TreePageViewModel newTree &&
                    oldTree.SelectedNode?.Visual is Control control)
                {
                    DispatcherTimer.RunOnce(
                        () =>
                        {
                            try
                            {
                                newTree.SelectControl(control, notifyMainSelection: false);
                            }
                            catch
                            {
                            }
                        },
                        TimeSpan.FromMilliseconds(0));
                }

                RaiseAndSetIfChanged(ref _content, value);
            }
        }

        public ViewModelBase? TreeContent
        {
            get => _treeContent;
            private set => RaiseAndSetIfChanged(ref _treeContent, value);
        }

        public ViewModelBase? RightContent
        {
            get => _rightContent;
            private set => RaiseAndSetIfChanged(ref _rightContent, value);
        }

        public int SelectedTreeTab
        {
            get => _selectedTreeTab;
            set
            {
                var normalized = NormalizeTreeTabIndex(value);
                if (!RaiseAndSetIfChanged(ref _selectedTreeTab, normalized))
                {
                    return;
                }

                TreeContent = ResolveTreeTabContent(normalized);
                if (TreeContent is TreePageViewModel activeTree)
                {
                    activeTree.EnsureRemoteTreeLoaded();
                }

                RefreshPropertiesTabContent();
                if (!_isSynchronizingTabSelection)
                {
                    SelectedTab = MapTreeTabToGlobalTab(normalized);
                }
            }
        }

        public int SelectedRightTab
        {
            get => _selectedRightTab;
            set
            {
                var normalized = NormalizeRightTabIndex(value);
                if (!RaiseAndSetIfChanged(ref _selectedRightTab, normalized))
                {
                    return;
                }

                RightContent = ResolveRightTabContent(MapRightTabToGlobalTab(normalized), inspectSelection: true);
                if (!_isSynchronizingTabSelection)
                {
                    SelectedTab = MapRightTabToGlobalTab(normalized);
                }
            }
        }

        public int SelectedTab
        {
            get => _selectedTab;
            set
            {
                var normalized = NormalizeGlobalTabIndex(value);
                _selectedTab = normalized;

                _isSynchronizingTabSelection = true;
                try
                {
                    if (IsTreeTab(normalized))
                    {
                        SelectedTreeTab = MapGlobalTabToTreeTab(normalized);
                    }
                    else
                    {
                        SelectedRightTab = MapGlobalTabToRightTab(normalized);
                    }
                }
                finally
                {
                    _isSynchronizingTabSelection = false;
                }

                Content = IsTreeTab(normalized) ? TreeContent : RightContent;
                RaisePropertyChanged();
            }
        }

        public string? FocusedControl
        {
            get => _focusedControl;
            private set => RaiseAndSetIfChanged(ref _focusedControl, value);
        }

        public IInputRoot? PointerOverRoot
        {
            get => _pointerOverRoot;
            private set => RaiseAndSetIfChanged(ref _pointerOverRoot, value);
        }

        public IInputElement? PointerOverElement
        {
            get => _pointerOverElement;
            private set
            {
                var changed = RaiseAndSetIfChanged(ref _pointerOverElement, value);
                PointerOverElementName = value?.GetType()?.Name;
                if (changed)
                {
                    UpdateInspectionHighlight();
                }
            }
        }

        public string? PointerOverElementName
        {
            get => _pointerOverElementName;
            private set => RaiseAndSetIfChanged(ref _pointerOverElementName, value);
        }

        public void ShowHotKeys() => SelectedTab = 17;

        public void ShowCode() => SelectedTab = 4;

        public void ShowBreakpoints() => SelectedTab = 9;

        public void ShowTransportSettings() => SelectedTab = 15;

        public void ShowMetrics() => SelectedTab = 11;

        public void ShowViewModelsBindings()
        {
            _viewModelsBindings.InspectSelection();
            SelectedTab = 12;
        }

        public void ShowStyles()
        {
            _stylesDiagnostics.InspectSelection();
            SelectedTab = 14;
        }

        public void ShowLogs() => SelectedTab = 10;

        public void ShowElements3D()
        {
            _elements3D.InspectSelection();
            SelectedTab = 5;
        }

        public void ShowProfiler() => SelectedTab = 13;

        public void ShowSettings() => SelectedTab = 16;

        public void SelectNextToolTab()
        {
            var next = SelectedTab + 1;
            if (next > LastVisibleTabIndex)
            {
                next = 0;
            }

            SelectedTab = next;
        }

        public void SelectPreviousToolTab()
        {
            var previous = SelectedTab - 1;
            if (previous < 0)
            {
                previous = LastVisibleTabIndex;
            }

            SelectedTab = previous;
        }

        public void RefreshCurrentTool()
        {
            switch (Content)
            {
                case TreePageViewModel tree:
                    tree.UpdatePropertiesView();
                    break;
                case ControlDetailsViewModel details:
                    details.UpdatePropertiesView(ShowImplementedInterfaces);
                    break;
                case CodePageViewModel codePage:
                    codePage.Refresh();
                    break;
                case ResourcesPageViewModel resources:
                    resources.Refresh();
                    break;
                case AssetsPageViewModel assets:
                    assets.Refresh();
                    break;
                case EventsPageViewModel eventsPage:
                    eventsPage.RefreshRecordedEvents();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.Refresh();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.Refresh();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.Refresh();
                    break;
                case ViewModelsBindingsPageViewModel viewModelsPage:
                    viewModelsPage.Refresh();
                    break;
                case Elements3DPageViewModel elements3DPage:
                    elements3DPage.Refresh();
                    break;
                case StylesDiagnosticsPageViewModel stylesPage:
                    stylesPage.Refresh();
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.Refresh();
                    break;
            }
        }

        public void ClearCurrentTool()
        {
            switch (Content)
            {
                case EventsPageViewModel eventsPage:
                    eventsPage.Clear();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.ClearAll();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.Clear();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.Clear();
                    break;
                case ViewModelsBindingsPageViewModel viewModelsPage:
                    viewModelsPage.Clear();
                    break;
                case StylesDiagnosticsPageViewModel stylesPage:
                    stylesPage.Clear();
                    break;
                case CodePageViewModel codePage:
                    codePage.InspectControl(GetSelectedDiagnosticsObject());
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.Clear();
                    break;
            }
        }

        public void SelectNextSearchMatch()
        {
            switch (Content)
            {
                case EventsPageViewModel eventsPage:
                    eventsPage.SelectNextMatch();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.SelectNextMatch();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.SelectNextMatch();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.SelectNextMatch();
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.SelectNextMatch();
                    break;
            }
        }

        public void SelectPreviousSearchMatch()
        {
            switch (Content)
            {
                case EventsPageViewModel eventsPage:
                    eventsPage.SelectPreviousMatch();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.SelectPreviousMatch();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.SelectPreviousMatch();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.SelectPreviousMatch();
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.SelectPreviousMatch();
                    break;
            }
        }

        public void RemoveSelectedRecord()
        {
            switch (Content)
            {
                case EventsPageViewModel eventsPage:
                    eventsPage.RemoveSelectedRecord();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.RemoveSelectedRecord();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.RemoveSelectedRecord();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.RemoveSelectedRecord();
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.RemoveSelectedRecord();
                    break;
            }
        }

        public void ClearSelectionOrFilter()
        {
            switch (Content)
            {
                case EventsPageViewModel eventsPage:
                    eventsPage.ClearSelectionOrFilter();
                    break;
                case BreakpointsPageViewModel breakpointsPage:
                    breakpointsPage.ClearSelectionOrFilter();
                    break;
                case LogsPageViewModel logsPage:
                    logsPage.ClearSelectionOrFilter();
                    break;
                case MetricsPageViewModel metricsPage:
                    metricsPage.ClearSelectionOrFilter();
                    break;
                case ProfilerPageViewModel profilerPage:
                    profilerPage.ClearSelectionOrFilter();
                    break;
            }
        }

        public void SetBreakpointFromContext()
        {
            if (Content is TreePageViewModel treePage && treePage.Details?.SelectedProperty is AvaloniaPropertyViewModel propertyViewModel)
            {
                treePage.Details.SetPropertyBreakpoint(propertyViewModel);
                return;
            }

            if (Content is EventsPageViewModel eventsPage && eventsPage.SelectedEvent is { } selectedEvent)
            {
                eventsPage.AddSourceEventBreakpoint(selectedEvent);
            }
        }

        public void SelectControl(Control control)
        {
            if (Content is ResourcesPageViewModel resources)
            {
                resources.SelectResourceHost(control);
            }

            PublishSelectionToStore(control, combinedNodePathHint: ResolveCombinedNodePath(nodePath: null, scope: "combined", selectedObject: control));
        }

        public void EnableSnapshotStyles(bool enable)
        {
            if (Content is TreePageViewModel treeVm && treeVm.Details != null)
            {
                treeVm.Details.SnapshotFrames = enable;
            }
            else if (Content is StylesDiagnosticsPageViewModel stylesVm)
            {
                stylesVm.SnapshotFrames = enable;
            }
        }

        public void Dispose()
        {
            if (KeyboardDevice.Instance is not null)
            {
                KeyboardDevice.Instance.PropertyChanged -= KeyboardPropertyChanged;
            }

            _logicalTree.PropertyChanged -= TreePagePropertyChanged;
            _visualTree.PropertyChanged -= TreePagePropertyChanged;
            _combinedTree.PropertyChanged -= TreePagePropertyChanged;
            DetachDetailsSubscription(_logicalTree, ref _logicalDetailsSubscription);
            DetachDetailsSubscription(_visualTree, ref _visualDetailsSubscription);
            DetachDetailsSubscription(_combinedTree, ref _combinedDetailsSubscription);
            _stylesDiagnostics.PropertyChanged -= StylesDiagnosticsPropertyChanged;
            _resources.PropertyChanged -= ResourcesPagePropertyChanged;
            _assets.PropertyChanged -= AssetsPagePropertyChanged;
            _elements3D.PropertyChanged -= Elements3DPropertyChanged;
            _selectionStore.SelectionChanged -= SelectionStoreSelectionChanged;
            _remoteSelectionStreamSubscription?.Dispose();
            _remoteSelectionStreamSubscription = null;

            _pointerOverSubscription.Dispose();
            _breakpointService.Clear();
            _logicalTree.Dispose();
            _visualTree.Dispose();
            _combinedTree.Dispose();
            _resources.Dispose();
            _events.Dispose();
            _breakpoints.Dispose();
            _logs.Dispose();
            _metrics.Dispose();
            _code.InspectControl(null);
            _stylesDiagnostics.Dispose();
            _transportSettings.Dispose();
            _profiler.Dispose();
            _settings.Dispose();
            _currentFocusHighlightAdorner?.Dispose();
            _currentInspectionHighlightAdorner?.Dispose();
            if (TryGetRenderer() is { } renderer)
            {
                renderer.Diagnostics.DebugOverlays = RendererDebugOverlays.None;
            }

            if (_remoteLoopbackSession is not null)
            {
                try
                {
                    _remoteLoopbackSession.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    // Best-effort cleanup for diagnostics loopback transport.
                }

                _remoteLoopbackSession = null;
            }
        }

        private void UpdateFocusedControl()
        {
            var element = KeyboardDevice.Instance?.FocusedElement;
            FocusedControl = element?.GetType().Name;

            _currentFocusHighlightAdorner?.Dispose();
            _currentFocusHighlightAdorner = null;

            if (!TrackFocusedControl)
            {
                return;
            }

            if (FocusHighlighter is IBrush brush && element is InputElement input && !input.DoesBelongToDevTool())
            {
                _currentFocusHighlightAdorner = ControlHighlightAdorner.Add(input, brush);
            }
        }

        private void UpdateInspectionHighlight()
        {
            _currentInspectionHighlightAdorner?.Dispose();
            _currentInspectionHighlightAdorner = null;

            if (!HighlightElements)
            {
                return;
            }

            var visual = ResolveInspectionHighlightVisual();
            if (visual is null)
            {
                return;
            }

            _currentInspectionHighlightAdorner = ControlHighlightAdorner.Add(visual, OverlayDisplayOptions);
        }

        private Visual? ResolveInspectionHighlightVisual()
        {
            if (LiveHoverOverlay &&
                PointerOverElement is Visual pointerVisual &&
                !pointerVisual.DoesBelongToDevTool())
            {
                return pointerVisual;
            }

            if (_selectedDiagnosticsObject is Visual selectedVisual && !selectedVisual.DoesBelongToDevTool())
            {
                return selectedVisual;
            }

            return null;
        }

        private void KeyboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(KeyboardDevice.Instance.FocusedElement))
            {
                UpdateFocusedControl();
            }
        }

        public void RequestTreeNavigateTo(Control control, bool isVisualTree)
        {
            var tree = isVisualTree ? _visualTree : _logicalTree;
            var node = tree.FindNode(control);
            if (node != null)
            {
                SelectedTab = isVisualTree ? 2 : 1;
                tree.SelectControl(control);
            }
        }

        public int? StartupScreenIndex { get; private set; }

        [DependsOn(nameof(TreePageViewModel.SelectedNode))]
        [DependsOn(nameof(Content))]
        public bool CanShot(object? parameter)
        {
            var tree = GetActiveTreeViewModel();
            return tree.SelectedNode != null
                && tree.SelectedNode.Visual is Visual visual
                && visual.VisualRoot != null;
        }

        public async void Shot(object? parameter)
        {
            if (GetActiveTreeViewModel().SelectedNode?.Visual is Control control && _screenshotHandler is { })
            {
                try
                {
                    await _screenshotHandler.Take(control);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        public void SetOptions(DevToolsOptions options)
        {
            _screenshotHandler = options.ScreenshotHandler;
            StartupScreenIndex = options.StartupScreenIndex;
            ShowImplementedInterfaces = options.ShowImplementedInterfaces;
            FocusHighlighter = options.FocusHighlighterBrush;

            ShouldVisualizeMarginPadding = options.VisualizeMarginPaddingOverlay;
            ShowOverlayInfo = options.ShowOverlayInfo;
            ShowOverlayRulers = options.ShowOverlayRulers;
            ShowOverlayExtensionLines = options.ShowOverlayExtensionLines;
            HighlightElements = options.HighlightElements;
            LiveHoverOverlay = options.LiveHoverOverlay;
            TrackFocusedControl = options.TrackFocusedControl;

            _events.SetOptions(options);
            _logs.SetOptions(options);
            _metrics.SetOptions(options);
            _profiler.SetOptions(options);
            _transportSettings.SetOptions(options);
            _hotKeys.SetOptions(options);

            if (options.UseRemoteRuntime)
            {
                _ = EnsureRemoteReadOnlyLoopbackAsync(options);
            }

            SelectedTab = GetTabIndex(options.LaunchView);
        }

        internal void NotifyTreeSelectionChanged(
            AvaloniaObject? selectedObject,
            string? scope = null,
            string? nodePath = null)
        {
            PublishSelectionToStore(selectedObject, combinedNodePathHint: ResolveCombinedNodePath(nodePath, scope, selectedObject));
        }

        private void SelectionStoreSelectionChanged(RemoteSelectionSnapshot snapshot)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                _ = Dispatcher.UIThread.InvokeAsync(() => SelectionStoreSelectionChanged(snapshot));
                return;
            }

            ApplySelectionSnapshot(snapshot);
        }

        private void PublishSelectionToStore(AvaloniaObject? selectedObject, string? combinedNodePathHint, bool pushRemote = true)
        {
            var resolvedSelection = selectedObject
                ?? _selectedDiagnosticsObject
                ?? _combinedTree.SelectedNode?.Visual
                ?? _logicalTree.SelectedNode?.Visual
                ?? _visualTree.SelectedNode?.Visual
                ?? TryGetFocusedDiagnosticsObject()
                ?? TryGetPointerOverDiagnosticsObject()
                ?? TryGetFirstNonDevToolsTopLevel()
                ?? _root;
            var combinedPath = ResolveCombinedNodePath(combinedNodePathHint, scope: null, resolvedSelection);
            var target = DescribeSelectionTarget(resolvedSelection);
            var targetType = resolvedSelection.GetType().FullName ?? resolvedSelection.GetType().Name;

            var previousSuppress = _suppressRemoteSelectionPush;
            if (!pushRemote)
            {
                _suppressRemoteSelectionPush = true;
            }

            try
            {
                _selectionStore.SetSelection(
                    scope: "combined",
                    nodeId: null,
                    nodePath: combinedPath,
                    target: target,
                    targetType: targetType);
            }
            finally
            {
                _suppressRemoteSelectionPush = previousSuppress;
            }
        }

        private void PublishSelectionByNodePath(string nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return;
            }

            AvaloniaObject? selectedObject = null;
            if (_combinedTree.TryGetNodeByPath(nodePath, out var treeNode))
            {
                selectedObject = treeNode?.Visual;
            }

            PublishSelectionToStore(selectedObject, combinedNodePathHint: nodePath);
        }

        private string? ResolveCombinedNodePath(string? nodePath, string? scope, AvaloniaObject? selectedObject)
        {
            if (!string.IsNullOrWhiteSpace(nodePath) &&
                string.Equals(NormalizeScope(scope), "combined", StringComparison.Ordinal))
            {
                return nodePath;
            }

            if (selectedObject is not null &&
                _combinedTree.TryGetNodePathForObject(selectedObject, out var pathFromObject))
            {
                return pathFromObject;
            }

            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return _combinedTree.SelectedNodePath;
            }

            if (_combinedTree.TryGetNodeByPath(nodePath, out _))
            {
                return nodePath;
            }

            return _combinedTree.SelectedNodePath;
        }

        private void ApplySelectionSnapshot(RemoteSelectionSnapshot snapshot)
        {
            if (_isApplyingSelectionSnapshot)
            {
                return;
            }

            _isApplyingSelectionSnapshot = true;
            try
            {
                AvaloniaObject? resolvedSelection = null;
                if (!string.IsNullOrWhiteSpace(snapshot.NodePath) &&
                    _combinedTree.TrySelectNodeByPath(snapshot.NodePath, notifyMainSelection: false))
                {
                    resolvedSelection = _combinedTree.SelectedNode?.Visual;
                }

                resolvedSelection ??= _combinedTree.SelectedNode?.Visual
                    ?? _selectedDiagnosticsObject
                    ?? TryGetFocusedDiagnosticsObject()
                    ?? TryGetPointerOverDiagnosticsObject()
                    ?? TryGetFirstNonDevToolsTopLevel()
                    ?? _root;

                SynchronizeTreeSelections(resolvedSelection);
                _selectedDiagnosticsObject = resolvedSelection;
                if (resolvedSelection is Control selectedControl &&
                    RightContent is ResourcesPageViewModel resourcesPage)
                {
                    resourcesPage.SelectResourceHost(selectedControl);
                }

                if (RightContent is ViewModelsBindingsPageViewModel)
                {
                    _viewModelsBindings.InspectControl(resolvedSelection);
                }

                if (RightContent is StylesDiagnosticsPageViewModel)
                {
                    _stylesDiagnostics.InspectControl(resolvedSelection);
                }

                if (RightContent is Elements3DPageViewModel)
                {
                    _elements3D.InspectControl(resolvedSelection);
                }

                RefreshPropertiesTabContent();
                if (RightContent is CodePageViewModel)
                {
                    SyncCodeFromCurrentContext();
                }
                UpdateInspectionHighlight();

                if (!_suppressRemoteSelectionPush)
                {
                    _ = PushRemoteSelectionAsync(snapshot);
                }
            }
            finally
            {
                _isApplyingSelectionSnapshot = false;
            }
        }

        private void SynchronizeTreeSelections(AvaloniaObject selection)
        {
            if (!_isSynchronizingCombinedTreeSelection &&
                selection is Control control &&
                !ReferenceEquals(_combinedTree.SelectedNode?.Visual, control))
            {
                _isSynchronizingCombinedTreeSelection = true;
                try
                {
                    _combinedTree.SelectControl(control, notifyMainSelection: false);
                }
                finally
                {
                    _isSynchronizingCombinedTreeSelection = false;
                }
            }

            _combinedTree.SelectObject(selection, notifyMainSelection: false);
            _logicalTree.SelectObject(selection, notifyMainSelection: false);
            _visualTree.SelectObject(selection, notifyMainSelection: false);
        }

        private static string DescribeSelectionTarget(AvaloniaObject selection)
        {
            if (selection is StyledElement { Name: { Length: > 0 } name })
            {
                return selection.GetType().Name + "#" + name;
            }

            return selection.GetType().Name;
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

        internal BreakpointService BreakpointService => _breakpointService;
        internal bool HasRemoteMutation => _remoteMutation is not null;

        public bool ShowImplementedInterfaces
        {
            get => _showImplementedInterfaces;
            private set => RaiseAndSetIfChanged(ref _showImplementedInterfaces, value);
        }

        public void ToggleShowImplementedInterfaces(object parameter)
        {
            SetShowImplementedInterfaces(!ShowImplementedInterfaces);
        }

        internal void SetShowImplementedInterfaces(bool value)
        {
            if (!RaiseAndSetIfChanged(ref _showImplementedInterfaces, value, nameof(ShowImplementedInterfaces)))
            {
                return;
            }

            if (Content is TreePageViewModel viewModel)
            {
                viewModel.UpdatePropertiesView();
            }
            else if (Content is ControlDetailsViewModel detailsViewModel)
            {
                detailsViewModel.UpdatePropertiesView(value);
            }
            else if (Content is ResourcesPageViewModel resourcesViewModel)
            {
                resourcesViewModel.UpdateDetailsView();
            }
        }

        public bool ShowDetailsPropertyType
        {
            get => _showPropertyType;
            private set => RaiseAndSetIfChanged(ref _showPropertyType, value);
        }

        public void ToggleShowDetailsPropertyType(object parameter)
        {
            ShowDetailsPropertyType = !ShowDetailsPropertyType;
        }

        public IBrush? FocusHighlighter
        {
            get => _focusHighlighter;
            private set
            {
                if (RaiseAndSetIfChanged(ref _focusHighlighter, value))
                {
                    UpdateFocusedControl();
                }
            }
        }

        public void SelectFocusHighlighter(object parameter)
        {
            FocusHighlighter = parameter as IBrush;
        }

        private static int GetTabIndex(DevToolsViewKind viewKind)
        {
            return viewKind switch
            {
                DevToolsViewKind.CombinedTree => 0,
                DevToolsViewKind.LogicalTree => 1,
                DevToolsViewKind.VisualTree => 2,
                DevToolsViewKind.Code => 4,
                DevToolsViewKind.Elements3D => 5,
                DevToolsViewKind.Resources => 6,
                DevToolsViewKind.Assets => 7,
                DevToolsViewKind.Events => 8,
                DevToolsViewKind.Breakpoints => 9,
                DevToolsViewKind.Logs => 10,
                DevToolsViewKind.Metrics => 11,
                DevToolsViewKind.ViewModelsBindings => 12,
                DevToolsViewKind.Profiler => 13,
                DevToolsViewKind.Styles => 14,
                DevToolsViewKind.TransportSettings => 15,
                DevToolsViewKind.Settings => 16,
                _ => 0
            };
        }

        private static bool IsTreeTab(int tabIndex) => tabIndex is 0 or 1 or 2;

        private static int NormalizeTreeTabIndex(int tabIndex)
        {
            return tabIndex switch
            {
                < 0 => 0,
                > 2 => 2,
                _ => tabIndex
            };
        }

        private static int NormalizeRightTabIndex(int tabIndex)
        {
            return Math.Clamp(tabIndex, 0, LastRightPanelTabIndex);
        }

        private static int NormalizeGlobalTabIndex(int tabIndex)
        {
            return tabIndex switch
            {
                < 0 => 0,
                > LastTabIndex => LastTabIndex,
                _ => tabIndex
            };
        }

        // Left panel order is Combined, Visual, Logical for faster inspection flow.
        private static int MapTreeTabToGlobalTab(int treeTabIndex)
        {
            return treeTabIndex switch
            {
                1 => 2,
                2 => 1,
                _ => 0
            };
        }

        private static int MapGlobalTabToTreeTab(int globalTabIndex)
        {
            return globalTabIndex switch
            {
                2 => 1,
                1 => 2,
                _ => 0
            };
        }

        private static int MapRightTabToGlobalTab(int rightTabIndex)
        {
            return NormalizeRightTabIndex(rightTabIndex) + 3;
        }

        private static int MapGlobalTabToRightTab(int globalTabIndex)
        {
            return NormalizeRightTabIndex(globalTabIndex - 3);
        }

        private ViewModelBase ResolveTreeTabContent(int treeTabIndex)
        {
            return treeTabIndex switch
            {
                1 => _visualTree,
                2 => _logicalTree,
                _ => _combinedTree
            };
        }

        private ViewModelBase? ResolveRightTabContent(int globalTabIndex, bool inspectSelection)
        {
            switch (globalTabIndex)
            {
                case 3:
                    return ResolvePropertiesContent();
                case 4:
                    SyncCodeFromCurrentContext();
                    return _code;
                case 5:
                    if (inspectSelection)
                    {
                        _elements3D.InspectSelection();
                    }

                    return _elements3D;
                case 6:
                    if (inspectSelection)
                    {
                        _resources.Refresh();
                    }

                    return _resources;
                case 7:
                    if (inspectSelection)
                    {
                        _assets.Refresh();
                    }

                    return _assets;
                case 8:
                    return _events;
                case 9:
                    return _breakpoints;
                case 10:
                    return _logs;
                case 11:
                    return _metrics;
                case 12:
                    if (inspectSelection)
                    {
                        _viewModelsBindings.InspectSelection();
                    }

                    return _viewModelsBindings;
                case 13:
                    return _profiler;
                case 14:
                    if (inspectSelection)
                    {
                        _stylesDiagnostics.InspectSelection();
                    }

                    return _stylesDiagnostics;
                case 15:
                    return _transportSettings;
                case 16:
                    return _settings;
                case 17:
                    return _hotKeys;
                default:
                    return _resources;
            }
        }

        private TreePageViewModel GetActiveTreeViewModel()
        {
            return SelectedTreeTab switch
            {
                1 => _visualTree,
                2 => _logicalTree,
                _ => _combinedTree
            };
        }

        private ControlDetailsViewModel? ResolvePropertiesContent()
        {
            return GetActiveTreeViewModel().Details;
        }

        private void RefreshPropertiesTabContent()
        {
            if (SelectedRightTab != 0)
            {
                return;
            }

            var propertiesContent = ResolvePropertiesContent();
            if (!ReferenceEquals(RightContent, propertiesContent))
            {
                RightContent = propertiesContent;
            }

            if (SelectedTab == 3)
            {
                Content = RightContent;
            }
        }

        private void TreePagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not TreePageViewModel treeViewModel)
            {
                return;
            }

            if (e.PropertyName == nameof(TreePageViewModel.Details))
            {
                AttachDetailsSubscription(treeViewModel);
            }
        }

        private void AttachDetailsSubscription(TreePageViewModel treeViewModel)
        {
            ref var currentDetails = ref GetDetailsSubscriptionSlot(treeViewModel);
            if (ReferenceEquals(currentDetails, treeViewModel.Details))
            {
                return;
            }

            if (currentDetails is not null)
            {
                currentDetails.PropertyChanged -= ControlDetailsPropertyChanged;
            }

            currentDetails = treeViewModel.Details;
            if (currentDetails is not null)
            {
                currentDetails.PropertyChanged += ControlDetailsPropertyChanged;
            }
        }

        private void DetachDetailsSubscription(TreePageViewModel treeViewModel, ref ControlDetailsViewModel? currentDetails)
        {
            if (currentDetails is not null)
            {
                currentDetails.PropertyChanged -= ControlDetailsPropertyChanged;
                currentDetails = null;
            }

            treeViewModel.PropertyChanged -= TreePagePropertyChanged;
        }

        private ref ControlDetailsViewModel? GetDetailsSubscriptionSlot(TreePageViewModel treeViewModel)
        {
            if (ReferenceEquals(treeViewModel, _logicalTree))
            {
                return ref _logicalDetailsSubscription;
            }

            if (ReferenceEquals(treeViewModel, _visualTree))
            {
                return ref _visualDetailsSubscription;
            }

            return ref _combinedDetailsSubscription;
        }

        private void ControlDetailsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ControlDetailsViewModel.SelectedEntity)
                or nameof(ControlDetailsViewModel.SelectedProperty)
                or nameof(ControlDetailsViewModel.XamlSourceText)
                or nameof(ControlDetailsViewModel.CodeSourceText))
            {
                SyncCodeFromCurrentContext();
            }
        }

        private void StylesDiagnosticsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StylesDiagnosticsPageViewModel.SelectedTreeEntry))
            {
                if (!_isApplyingSelectionSnapshot)
                {
                    var selectedTreeEntry = _stylesDiagnostics.SelectedTreeEntry;
                    if (selectedTreeEntry?.SourceObject is AvaloniaObject sourceObject)
                    {
                        PublishSelectionToStore(sourceObject, combinedNodePathHint: ResolveCombinedNodePath(selectedTreeEntry.NodePath, "combined", sourceObject));
                    }
                    else if (!string.IsNullOrWhiteSpace(selectedTreeEntry?.NodePath))
                    {
                        PublishSelectionByNodePath(selectedTreeEntry.NodePath);
                    }
                }

                SyncCodeFromCurrentContext();
                return;
            }

            if (e.PropertyName is nameof(StylesDiagnosticsPageViewModel.SelectedFrame)
                or nameof(StylesDiagnosticsPageViewModel.SelectedSetter)
                or nameof(StylesDiagnosticsPageViewModel.SelectedResolutionEntry))
            {
                SyncCodeFromCurrentContext();
            }
        }

        private void Elements3DPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Elements3DPageViewModel.SelectedNode) || _isApplyingSelectionSnapshot)
            {
                return;
            }

            var selectedNode = _elements3D.SelectedNode;
            if (selectedNode?.Visual is AvaloniaObject selectedObject)
            {
                PublishSelectionToStore(selectedObject, combinedNodePathHint: ResolveCombinedNodePath(selectedNode.NodePath, "combined", selectedObject));
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedNode?.NodePath))
            {
                PublishSelectionByNodePath(selectedNode.NodePath);
            }
        }

        private void ResourcesPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ResourcesPageViewModel.SelectedResource))
            {
                SyncCodeFromCurrentContext();
            }
        }

        private void AssetsPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AssetsPageViewModel.SelectedAsset))
            {
                SyncCodeFromCurrentContext();
            }
        }

        private void SyncCodeFromCurrentContext()
        {
            if (_isSyncingFromCodeSelection)
            {
                return;
            }

            if (_remoteReadOnly is not null &&
                SelectedRightTab != MapGlobalTabToRightTab(4))
            {
                return;
            }

            var selectedObject = GetSelectedDiagnosticsObject();
            var preferredSourceText = GetPreferredSourceLocationText();
            _code.InspectControl(selectedObject, preferredSourceText);
        }

        private string? GetPreferredSourceLocationText()
        {
            switch (RightContent)
            {
                case StylesDiagnosticsPageViewModel styles:
                    return styles.GetPreferredSourceLocationText();
                case ResourcesPageViewModel resources:
                    return resources.SelectedResource?.SourceLocation;
                case AssetsPageViewModel assets:
                    return assets.SelectedAsset?.SourceLocation;
                case ControlDetailsViewModel details:
                    return !string.IsNullOrWhiteSpace(details.XamlSourceText)
                        ? details.XamlSourceText
                        : details.CodeSourceText;
                case CodePageViewModel:
                    return _code.SelectedDocumentTab == 0
                        ? _code.XamlLocationText
                        : _code.CodeLocationText;
                default:
                    return null;
            }
        }

        private void OnCodeCaretLocationChanged(SourceDocumentLocation location)
        {
            if (_isSyncingFromCodeSelection)
            {
                return;
            }

            _isSyncingFromCodeSelection = true;
            try
            {
                if (TrySelectControlBySourceLocation(location, out var matchedControl))
                {
                    SelectControl(matchedControl);
                }

                _stylesDiagnostics.TrySelectBySourceLocation(location);
                _resources.TrySelectResourceBySourceLocation(location);
                _assets.TrySelectAssetBySourceLocation(location);
            }
            finally
            {
                _isSyncingFromCodeSelection = false;
            }
        }

        private bool TrySelectControlBySourceLocation(SourceDocumentLocation target, out Control matchedControl)
        {
            matchedControl = null!;
            var bestScore = int.MaxValue;

            foreach (var node in EnumerateTreeNodes(_combinedTree.Nodes))
            {
                if (node.Visual is not Control control)
                {
                    continue;
                }

                var sourceInfo = GetCachedSourceLocation(control);
                var hasXamlScore = TryGetLocationScore(sourceInfo.XamlLocation, target, out var xamlScore);
                var hasCodeScore = TryGetLocationScore(sourceInfo.CodeLocation, target, out var codeScore);
                if (!hasXamlScore && !hasCodeScore)
                {
                    continue;
                }

                var score = hasXamlScore && hasCodeScore
                    ? Math.Min(xamlScore, codeScore)
                    : hasXamlScore
                        ? xamlScore
                        : codeScore;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                matchedControl = control;
                if (score == 0)
                {
                    return true;
                }
            }

            return bestScore < int.MaxValue;
        }

        private SourceLocationInfo GetCachedSourceLocation(AvaloniaObject source)
        {
            if (_sourceLocationCache.TryGetValue(source, out var cached))
            {
                return cached;
            }

            var resolved = _sourceLocationService.ResolveObject(source);
            _sourceLocationCache[source] = resolved;
            return resolved;
        }

        private static bool TryGetLocationScore(SourceDocumentLocation? candidate, SourceDocumentLocation target, out int score)
        {
            score = int.MaxValue;
            if (candidate is null)
            {
                return false;
            }

            if (!SourceLocationTextParser.IsSameDocument(candidate.FilePath, target.FilePath))
            {
                return false;
            }

            score = Math.Abs(candidate.Line - target.Line) * 1000 + Math.Abs(candidate.Column - target.Column);
            return true;
        }

        private static IEnumerable<TreeNode> EnumerateTreeNodes(IEnumerable<TreeNode> roots)
        {
            var stack = new Stack<TreeNode>();
            foreach (var root in roots)
            {
                stack.Push(root);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                for (var i = current.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push(current.Children[i]);
                }
            }
        }

        private AvaloniaObject? GetSelectedDiagnosticsObject()
        {
            return _selectedDiagnosticsObject
                ?? (Content as TreePageViewModel)?.SelectedNode?.Visual
                ?? _combinedTree.SelectedNode?.Visual
                ?? _logicalTree.SelectedNode?.Visual
                ?? _visualTree.SelectedNode?.Visual
                ?? TryGetFocusedDiagnosticsObject()
                ?? TryGetPointerOverDiagnosticsObject()
                ?? TryGetFirstNonDevToolsTopLevel()
                ?? _root;
        }

        private AvaloniaObject? TryGetFocusedDiagnosticsObject()
        {
            if (KeyboardDevice.Instance?.FocusedElement is not AvaloniaObject focusedObject)
            {
                return null;
            }

            if (focusedObject is Visual focusedVisual && focusedVisual.DoesBelongToDevTool())
            {
                return null;
            }

            return focusedObject;
        }

        private AvaloniaObject? TryGetPointerOverDiagnosticsObject()
        {
            if (PointerOverElement is not AvaloniaObject pointerObject)
            {
                return null;
            }

            if (pointerObject is Visual pointerVisual && pointerVisual.DoesBelongToDevTool())
            {
                return null;
            }

            return pointerObject;
        }

        private AvaloniaObject? TryGetFirstNonDevToolsTopLevel()
        {
            if (_root is not TopLevelGroup topLevelGroup)
            {
                return _root as TopLevel;
            }

            foreach (var topLevel in topLevelGroup.Items)
            {
                if (topLevel is not Visual visual || !visual.DoesBelongToDevTool())
                {
                    return topLevel;
                }
            }

            return null;
        }

        private async Task EnsureRemoteReadOnlyLoopbackAsync(DevToolsOptions options)
        {
            if (_remoteLoopbackSession is not null)
            {
                BindRemoteReadOnlyTabs(_remoteLoopbackSession);
                return;
            }

            if (_remoteLoopbackInitializationRequested)
            {
                return;
            }

            _remoteLoopbackInitializationRequested = true;
            try
            {
                var session = await DevToolsRemoteLoopbackSession.StartAsync(
                    _root,
                    options.RemoteLoopbackOptions).ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _remoteLoopbackSession = session;
                    BindRemoteReadOnlyTabs(session);
                });
            }
            catch
            {
                _remoteLoopbackInitializationRequested = false;
            }
        }

        private void BindRemoteReadOnlyTabs(DevToolsRemoteLoopbackSession session)
        {
            _remoteReadOnly = session.ReadOnly;
            _remoteMutation = session.Mutation;
            _remoteSelectionStreamSubscription?.Dispose();
            _remoteSelectionStreamSubscription = session.Stream.Subscribe(
                RemoteStreamTopics.Selection,
                RemoteJsonSerializerContext.Default.RemoteSelectionSnapshot,
                OnRemoteSelectionStreamPayload);
            var activeTree = GetActiveTreeViewModel();
            _logicalTree.SetRemoteReadOnlySource(session.ReadOnly, refreshTreeNow: ReferenceEquals(activeTree, _logicalTree));
            _visualTree.SetRemoteReadOnlySource(session.ReadOnly, refreshTreeNow: ReferenceEquals(activeTree, _visualTree));
            _combinedTree.SetRemoteReadOnlySource(session.ReadOnly, refreshTreeNow: ReferenceEquals(activeTree, _combinedTree));
            _logicalTree.SetRemoteMutationSource(session.Mutation);
            _visualTree.SetRemoteMutationSource(session.Mutation);
            _combinedTree.SetRemoteMutationSource(session.Mutation);
            _events.SetRemoteMutationSource(session.Mutation, GetRemoteTargetContext);
            _breakpoints.SetRemoteMutationSource(session.Mutation);
            _logs.SetRemoteMutationSource(session.Mutation);
            _metrics.SetRemoteMutationSource(session.Mutation);
            _profiler.SetRemoteMutationSource(session.Mutation);
            var activeRightGlobalTab = MapRightTabToGlobalTab(SelectedRightTab);
            _code.SetRemoteReadOnlySource(
                session.ReadOnly,
                GetRemoteSelectionContext,
                refreshNow: activeRightGlobalTab == 4);
            _viewModelsBindings.SetRemoteReadOnlySource(
                session.ReadOnly,
                GetRemoteSelectionContext,
                refreshNow: activeRightGlobalTab == 12);
            _assets.SetRemoteReadOnlySource(
                session.ReadOnly,
                refreshNow: activeRightGlobalTab == 7);
            _resources.SetRemoteReadOnlySource(
                session.ReadOnly,
                refreshNow: activeRightGlobalTab == 6);
            _stylesDiagnostics.SetRemoteReadOnlySource(
                session.ReadOnly,
                GetRemoteSelectionContext,
                refreshNow: activeRightGlobalTab == 14);
            _elements3D.SetRemoteReadOnlySource(
                session.ReadOnly,
                GetRemoteSelectionContext,
                refreshNow: activeRightGlobalTab == 5);
            _ = RefreshRemoteSelectionSnapshotAsync();
        }

        private void OnRemoteSelectionStreamPayload(RemoteTypedStreamPayload<RemoteSelectionSnapshot> payload)
        {
            if (!payload.IsParsed || payload.Payload is null)
            {
                return;
            }

            var snapshot = payload.Payload;
            if (!Dispatcher.UIThread.CheckAccess())
            {
                _ = Dispatcher.UIThread.InvokeAsync(() => ApplyRemoteSelectionSnapshot(snapshot));
                return;
            }

            ApplyRemoteSelectionSnapshot(snapshot);
        }

        private async Task RefreshRemoteSelectionSnapshotAsync()
        {
            var readOnly = _remoteReadOnly;
            if (readOnly is null)
            {
                return;
            }

            try
            {
                var snapshot = await readOnly.GetSelectionSnapshotAsync(
                    new RemoteSelectionSnapshotRequest
                    {
                        Scope = "combined",
                    }).ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() => ApplyRemoteSelectionSnapshot(snapshot));
            }
            catch
            {
                // Keep local selection state when remote snapshot bootstrap fails.
            }
        }

        private void ApplyRemoteSelectionSnapshot(RemoteSelectionSnapshot snapshot)
        {
            var combinedSnapshot = snapshot;
            if (!string.Equals(NormalizeScope(snapshot.Scope), "combined", StringComparison.Ordinal))
            {
                var selectionFromScope = ResolveSelectionObjectFromScope(snapshot.Scope, snapshot.NodePath);
                var combinedPath = ResolveCombinedNodePath(nodePath: null, scope: null, selectedObject: selectionFromScope);
                combinedSnapshot = snapshot with
                {
                    Scope = "combined",
                    NodePath = combinedPath,
                    Target = selectionFromScope is null ? snapshot.Target : DescribeSelectionTarget(selectionFromScope),
                    TargetType = selectionFromScope?.GetType().FullName ?? selectionFromScope?.GetType().Name ?? snapshot.TargetType,
                };
            }

            var previousSuppress = _suppressRemoteSelectionPush;
            _suppressRemoteSelectionPush = true;
            try
            {
                _selectionStore.SetSelection(
                    scope: "combined",
                    nodeId: combinedSnapshot.NodeId,
                    nodePath: combinedSnapshot.NodePath,
                    target: combinedSnapshot.Target,
                    targetType: combinedSnapshot.TargetType);
            }
            finally
            {
                _suppressRemoteSelectionPush = previousSuppress;
            }
        }

        private AvaloniaObject? ResolveSelectionObjectFromScope(string? scope, string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                return null;
            }

            var tree = NormalizeScope(scope) switch
            {
                "logical" => _logicalTree,
                "visual" => _visualTree,
                _ => _combinedTree,
            };
            if (tree.TryGetNodeByPath(nodePath, out var treeNode))
            {
                return treeNode?.Visual;
            }

            return null;
        }

        private (string Scope, string? NodePath, string? ControlName) GetRemoteSelectionContext()
        {
            var selection = _selectionStore.GetSnapshot("combined");
            var nodePath = selection.NodePath ?? _combinedTree.SelectedNodePath;
            var controlName = (_selectedDiagnosticsObject as INamed)?.Name;
            return ("combined", nodePath, controlName);
        }

        internal (string Scope, string? NodePath, string? ControlName) GetRemoteTargetContext(AvaloniaObject? target)
        {
            if (target is not null &&
                _combinedTree.TryGetNodePathForObject(target, out var nodePath) &&
                !string.IsNullOrWhiteSpace(nodePath))
            {
                var controlName = (target as INamed)?.Name;
                return ("combined", nodePath, controlName);
            }

            if (target is INamed named && !string.IsNullOrWhiteSpace(named.Name))
            {
                return ("combined", null, named.Name);
            }

            return GetRemoteSelectionContext();
        }

        private async Task PushRemoteSelectionAsync(RemoteSelectionSnapshot snapshot)
        {
            var mutation = _remoteMutation;
            if (mutation is null)
            {
                return;
            }

            var controlName = (_selectedDiagnosticsObject as INamed)?.Name;
            if (string.IsNullOrWhiteSpace(snapshot.NodePath) && string.IsNullOrWhiteSpace(controlName))
            {
                return;
            }

            try
            {
                await mutation.SetSelectionAsync(
                    new RemoteSetSelectionRequest
                    {
                        Scope = "combined",
                        NodePath = snapshot.NodePath,
                        ControlName = controlName,
                    }).ConfigureAwait(false);
            }
            catch
            {
                // Keep local selection flow resilient when remote command path fails.
            }
        }

        internal async Task<bool> InspectHoveredViaRemoteAsync()
        {
            var mutation = _remoteMutation;
            var readOnly = _remoteReadOnly;
            if (mutation is null || readOnly is null)
            {
                return false;
            }

            try
            {
                var result = await mutation.InspectHoveredAsync(
                    new RemoteInspectHoveredRequest
                    {
                        Scope = "combined",
                        RequireInspectGesture = false,
                        IncludeDevTools = false,
                    }).ConfigureAwait(false);
                if (!result.Changed)
                {
                    return true;
                }

                var selection = await readOnly.GetSelectionSnapshotAsync(
                    new RemoteSelectionSnapshotRequest
                    {
                        Scope = "combined",
                    }).ConfigureAwait(false);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ApplyRemoteSelectionSnapshot(selection);
                });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
