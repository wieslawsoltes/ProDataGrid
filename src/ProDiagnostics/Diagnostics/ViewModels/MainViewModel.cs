using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Diagnostics.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class MainViewModel : ViewModelBase, IDisposable
    {
        private const int LastVisibleTabIndex = 15;
        private const int LastTabIndex = 16;
        private const int LastRightPanelTabIndex = LastTabIndex - 3;

        private readonly AvaloniaObject _root;
        private readonly TreePageViewModel _logicalTree;
        private readonly TreePageViewModel _visualTree;
        private readonly TreePageViewModel _combinedTree;
        private readonly ResourcesPageViewModel _resources;
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
        private readonly IDisposable _pointerOverSubscription;

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

        public MainViewModel(AvaloniaObject root)
        {
            _root = root;
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
            _logicalTree = new TreePageViewModel(this, logicalProvider.Create(root), treeModelFactory, _pinnedProperties);
            _visualTree = new TreePageViewModel(this, visualProvider.Create(root), treeModelFactory, _pinnedProperties);
            _combinedTree = new TreePageViewModel(this, combinedProvider.Create(root), treeModelFactory, _pinnedProperties);
            _resources = new ResourcesPageViewModel(this, resourceProvider.Create(root), resourceModelFactory, resourceFormatter);
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
                                newTree.SelectControl(control);
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

        public void ShowHotKeys() => SelectedTab = 16;

        public void ShowBreakpoints() => SelectedTab = 8;

        public void ShowTransportSettings() => SelectedTab = 14;

        public void ShowMetrics() => SelectedTab = 10;

        public void ShowViewModelsBindings()
        {
            _viewModelsBindings.InspectSelection();
            SelectedTab = 11;
        }

        public void ShowStyles()
        {
            _stylesDiagnostics.InspectSelection();
            SelectedTab = 13;
        }

        public void ShowLogs() => SelectedTab = 9;

        public void ShowElements3D()
        {
            _elements3D.InspectSelection();
            SelectedTab = 4;
        }

        public void ShowProfiler() => SelectedTab = 12;

        public void ShowSettings() => SelectedTab = 15;

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
                case ResourcesPageViewModel resources:
                    resources.UpdateDetailsView();
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
            if (!ReferenceEquals(_combinedTree.SelectedNode?.Visual, control))
            {
                _combinedTree.SelectControl(control);
            }

            switch (Content)
            {
                case TreePageViewModel tree:
                    if (!ReferenceEquals(tree, _combinedTree))
                    {
                        tree.SelectControl(control);
                    }

                    break;
                case ResourcesPageViewModel resources:
                    resources.SelectResourceHost(control);
                    break;
            }

            NotifyTreeSelectionChanged(control);
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

            SelectedTab = GetTabIndex(options.LaunchView);
        }

        internal void NotifyTreeSelectionChanged(AvaloniaObject? selectedObject)
        {
            if (!_isSynchronizingCombinedTreeSelection &&
                selectedObject is Control control &&
                !ReferenceEquals(_combinedTree.SelectedNode?.Visual, control))
            {
                _isSynchronizingCombinedTreeSelection = true;
                try
                {
                    _combinedTree.SelectControl(control);
                }
                finally
                {
                    _isSynchronizingCombinedTreeSelection = false;
                }
            }

            var resolvedSelection = _combinedTree.SelectedNode?.Visual as AvaloniaObject
                ?? selectedObject
                ?? _selectedDiagnosticsObject
                ?? _logicalTree.SelectedNode?.Visual
                ?? _visualTree.SelectedNode?.Visual
                ?? TryGetFocusedDiagnosticsObject()
                ?? TryGetPointerOverDiagnosticsObject()
                ?? TryGetFirstNonDevToolsTopLevel()
                ?? _root;
            _selectedDiagnosticsObject = resolvedSelection;
            _viewModelsBindings.InspectControl(resolvedSelection);
            _stylesDiagnostics.InspectControl(resolvedSelection);
            _elements3D.InspectControl(resolvedSelection);
            RefreshPropertiesTabContent();
            UpdateInspectionHighlight();
        }

        internal BreakpointService BreakpointService => _breakpointService;

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
                DevToolsViewKind.Elements3D => 4,
                DevToolsViewKind.Resources => 5,
                DevToolsViewKind.Assets => 6,
                DevToolsViewKind.Events => 7,
                DevToolsViewKind.Breakpoints => 8,
                DevToolsViewKind.Logs => 9,
                DevToolsViewKind.Metrics => 10,
                DevToolsViewKind.ViewModelsBindings => 11,
                DevToolsViewKind.Profiler => 12,
                DevToolsViewKind.Styles => 13,
                DevToolsViewKind.TransportSettings => 14,
                DevToolsViewKind.Settings => 15,
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
                    if (inspectSelection)
                    {
                        _elements3D.InspectSelection();
                    }

                    return _elements3D;
                case 5:
                    return _resources;
                case 6:
                    return _assets;
                case 7:
                    return _events;
                case 8:
                    return _breakpoints;
                case 9:
                    return _logs;
                case 10:
                    return _metrics;
                case 11:
                    if (inspectSelection)
                    {
                        _viewModelsBindings.InspectSelection();
                    }

                    return _viewModelsBindings;
                case 12:
                    return _profiler;
                case 13:
                    if (inspectSelection)
                    {
                        _stylesDiagnostics.InspectSelection();
                    }

                    return _stylesDiagnostics;
                case 14:
                    return _transportSettings;
                case 15:
                    return _settings;
                case 16:
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
    }
}
