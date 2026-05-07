using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.Reactive;
using Avalonia.Rendering;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Diagnostics.Services;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly AvaloniaObject _root;
        private readonly TreePageViewModel _logicalTree;
        private readonly TreePageViewModel _visualTree;
        private readonly TreePageViewModel _combinedTree;
        private readonly ResourcesPageViewModel _resources;
        private readonly AssetsPageViewModel _assets;
        private readonly EventsPageViewModel _events;
        private readonly HotKeyPageViewModel _hotKeys;
        private readonly IDisposable _pointerOverSubscription;
        private ViewModelBase? _content;
        private int _selectedTab;
        private string? _focusedControl;
        private IInputElement? _pointerOverElement;
        private bool _shouldVisualizeMarginPadding = true;
        private bool _freezePopups;
        private string? _pointerOverElementName;
        private TopLevel? _pointerOverRoot;
        private IScreenshotHandler? _screenshotHandler;
        private bool _showPropertyType;
        private bool _showImplementedInterfaces;
        private bool _showMenu = true;
        private bool _showResourcesTab = true;
        private bool _showAssetsTab = true;
        private bool _showEventsTab = true;
        private bool _scopeEventsToRoot = true;
        private readonly HashSet<string> _pinnedProperties = new();
        private IBrush? _FocusHighlighter;
        private IDisposable? _currentFocusHighlightAdorner = default;

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
            _logicalTree = new TreePageViewModel(this, logicalProvider.Create(root), treeModelFactory, _pinnedProperties);
            _visualTree = new TreePageViewModel(this, visualProvider.Create(root), treeModelFactory, _pinnedProperties);
            _combinedTree = new TreePageViewModel(this, combinedProvider.Create(root), treeModelFactory, _pinnedProperties);
            _resources = new ResourcesPageViewModel(this, resourceProvider.Create(root), resourceModelFactory, resourceFormatter);
            _assets = new AssetsPageViewModel(this);
            _events = new EventsPageViewModel(this);
            _hotKeys = new HotKeyPageViewModel();

            UpdateFocusedControl();

            if (KeyboardDevice.Instance is not null)
                KeyboardDevice.Instance.PropertyChanged += KeyboardPropertyChanged;
            SelectedTab = 0;
            if (root is TopLevel topLevel)
            {
                _pointerOverRoot = topLevel;
            }

            _pointerOverSubscription = InputManager.Instance!.PreProcess
                .Subscribe(e =>
                {
                    if (e is Input.Raw.RawPointerEventArgs pointerEventArgs &&
                        pointerEventArgs.Root.GetInputTopLevel() is { } currentTopLevel &&
                        pointerEventArgs.Root.GetScreenPoint(pointerEventArgs.Position) is { } screenPoint)
                    {
                        PointerOverRoot = currentTopLevel;
                        PointerOverElement = currentTopLevel.InputHitTest(currentTopLevel.PointToClient(screenPoint));
                    }
                });
        }

        public bool FreezePopups
        {
            get => _freezePopups;
            set => RaiseAndSetIfChanged(ref _freezePopups, value);
        }

        public bool ShouldVisualizeMarginPadding
        {
            get => _shouldVisualizeMarginPadding;
            set => RaiseAndSetIfChanged(ref _shouldVisualizeMarginPadding, value);
        }

        public void ToggleVisualizeMarginPadding()
            => ShouldVisualizeMarginPadding = !ShouldVisualizeMarginPadding;

        private IRenderer? TryGetRenderer()
            => _root switch
            {
                TopLevel topLevel => topLevel.Renderer,
                Controls.Application app => app.RendererRoot,
                _ => null
            };

        private bool GetDebugOverlay(RendererDebugOverlays overlay)
            => ((TryGetRenderer()?.Diagnostics.DebugOverlays ?? RendererDebugOverlays.None) & overlay) != 0;

        private void SetDebugOverlay(RendererDebugOverlays overlay, bool enable,
            [CallerMemberName] string? propertyName = null)
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

        public void ToggleDirtyRectsOverlay()
            => ShowDirtyRectsOverlay = !ShowDirtyRectsOverlay;

        public bool ShowFpsOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.Fps);
            set => SetDebugOverlay(RendererDebugOverlays.Fps, value);
        }

        public void ToggleFpsOverlay()
            => ShowFpsOverlay = !ShowFpsOverlay;

        public bool ShowLayoutTimeGraphOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.LayoutTimeGraph);
            set => SetDebugOverlay(RendererDebugOverlays.LayoutTimeGraph, value);
        }

        public void ToggleLayoutTimeGraphOverlay()
            => ShowLayoutTimeGraphOverlay = !ShowLayoutTimeGraphOverlay;

        public bool ShowRenderTimeGraphOverlay
        {
            get => GetDebugOverlay(RendererDebugOverlays.RenderTimeGraph);
            set => SetDebugOverlay(RendererDebugOverlays.RenderTimeGraph, value);
        }

        public void ToggleRenderTimeGraphOverlay()
            => ShowRenderTimeGraphOverlay = !ShowRenderTimeGraphOverlay;

        public ViewModelBase? Content
        {
            get { return _content; }
            private set
            {
                if (_content is TreePageViewModel oldTree &&
                    value is TreePageViewModel newTree &&
                    oldTree?.SelectedNode?.Visual is Control control)
                {
                    // HACK: We want to select the currently selected control in the new tree, but
                    // to select nested nodes in the tree grid, the control has to be able to
                    // expand the parent nodes. Because at this point the tree grid isn't visible,
                    // this will fail unless we schedule the selection to run after layout.
                    DispatcherTimer.RunOnce(
                        () =>
                        {
                            try
                            {
                                newTree.SelectControl(control);
                            }
                            catch { }
                        },
                        TimeSpan.FromMilliseconds(0));
                }

                RaiseAndSetIfChanged(ref _content, value);
            }
        }

        public ViewModelBase GetContent(DevToolsViewKind viewKind)
        {
            return viewKind switch
            {
                DevToolsViewKind.LogicalTree => _logicalTree,
                DevToolsViewKind.VisualTree => _visualTree,
                DevToolsViewKind.Events => _events,
                DevToolsViewKind.Resources => _resources,
                DevToolsViewKind.Assets => _assets,
                _ => _combinedTree
            };
        }

        public void SelectContent(DevToolsViewKind viewKind)
        {
            _selectedTab = GetTabIndex(viewKind);
            Content = GetContent(viewKind);
            RaisePropertyChanged(nameof(SelectedTab));
        }

        public int SelectedTab
        {
            get { return _selectedTab; }
            // [MemberNotNull(nameof(_content))]
            set
            {
                _selectedTab = value;

                switch (value)
                {
                    case 1:
                        Content = _logicalTree;
                        break;
                    case 2:
                        Content = _visualTree;
                        break;
                    case 3 when ShowResourcesTab:
                        Content = _resources;
                        break;
                    case 4 when ShowAssetsTab:
                        Content = _assets;
                        break;
                    case 5 when ShowEventsTab:
                        Content = _events;
                        break;
                    case 6:
                        Content = _hotKeys;
                        break;
                    default:
                        _selectedTab = 0;
                        Content = _combinedTree;
                        break;
                }

                RaisePropertyChanged();
            }
        }

        public string? FocusedControl
        {
            get { return _focusedControl; }
            private set { RaiseAndSetIfChanged(ref _focusedControl, value); }
        }

        public TopLevel? PointerOverRoot
        {
            get => _pointerOverRoot;
            private set => RaiseAndSetIfChanged(ref _pointerOverRoot, value);
        }

        public IInputElement? PointerOverElement
        {
            get { return _pointerOverElement; }
            private set
            {
                RaiseAndSetIfChanged(ref _pointerOverElement, value);
                PointerOverElementName = value?.GetType()?.Name;
            }
        }

        public string? PointerOverElementName
        {
            get => _pointerOverElementName;
            private set => RaiseAndSetIfChanged(ref _pointerOverElementName, value);
        }

        public void ShowHotKeys()
        {
            SelectedTab = 6;
        }

        public void SelectControl(Control control)
        {
            switch (Content)
            {
                case TreePageViewModel tree:
                    tree.SelectControl(control);
                    break;
                case ResourcesPageViewModel resources:
                    resources.SelectResourceHost(control);
                    break;
            }
        }

        public void EnableSnapshotStyles(bool enable)
        {
            if (Content is TreePageViewModel treeVm && treeVm.Details != null)
            {
                treeVm.Details.SnapshotFrames = enable;
            }
        }

        public void Dispose()
        {
            if (KeyboardDevice.Instance is not null)
                KeyboardDevice.Instance.PropertyChanged -= KeyboardPropertyChanged;
            _pointerOverSubscription.Dispose();
            _logicalTree.Dispose();
            _visualTree.Dispose();
            _combinedTree.Dispose();
            _resources.Dispose();
            _events.Dispose();
            _currentFocusHighlightAdorner?.Dispose();
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
            if (FocusHighlighter is IBrush brush
                && element is InputElement input
                && !input.DoesBelongToDevTool()
                )
            {
                _currentFocusHighlightAdorner = Controls.ControlHighlightAdorner.Add(input, brush);
            }
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

        public int? StartupScreenIndex { get; private set; } = default;

        [DependsOn(nameof(TreePageViewModel.SelectedNode))]
        [DependsOn(nameof(Content))]
        public bool CanShot(object? parameter)
        {
            return Content is TreePageViewModel tree
                && tree.SelectedNode != null
                && tree.SelectedNode.Visual is Visual visual
                && visual.VisualRoot != null;
        }

        public async void Shot(object? parameter)
        {
            if ((Content as TreePageViewModel)?.SelectedNode?.Visual is Control control
                && _screenshotHandler is { }
                )
            {
                try
                {
                    await _screenshotHandler.Take(control);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    //TODO: Notify error
                }
            }
        }

        public void SetOptions(DevToolsOptions options)
        {
            _screenshotHandler = options.ScreenshotHandler;
            StartupScreenIndex = options.StartupScreenIndex;
            ShowMenu = options.ShowMenu;
            ShowResourcesTab = options.ShowResourcesTab;
            ShowAssetsTab = options.ShowAssetsTab;
            ShowEventsTab = options.ShowEventsTab;
            ScopeEventsToRoot = options.ScopeEventsToRoot;
            ShowImplementedInterfaces = options.ShowImplementedInterfaces;
            FocusHighlighter = options.FocusHighlighterBrush;
            SelectedTab = GetTabIndex(options.LaunchView);

            _hotKeys.SetOptions(options);
        }

        public bool ShowMenu
        {
            get => _showMenu;
            private set => RaiseAndSetIfChanged(ref _showMenu, value);
        }

        public bool ShowResourcesTab
        {
            get => _showResourcesTab;
            private set => RaiseAndSetIfChanged(ref _showResourcesTab, value);
        }

        public bool ShowAssetsTab
        {
            get => _showAssetsTab;
            private set => RaiseAndSetIfChanged(ref _showAssetsTab, value);
        }

        public bool ShowEventsTab
        {
            get => _showEventsTab;
            private set => RaiseAndSetIfChanged(ref _showEventsTab, value);
        }

        public bool ScopeEventsToRoot
        {
            get => _scopeEventsToRoot;
            private set => RaiseAndSetIfChanged(ref _scopeEventsToRoot, value);
        }

        public bool ShouldRecordEvent(object? sender, RoutedEventArgs e)
        {
            if (!ScopeEventsToRoot || _root is not Visual)
            {
                return true;
            }

            return IsInEventScope(e.Source) && IsInEventScope(sender);
        }

        public bool ShouldRecordRouteFinished(RoutedEventArgs e)
        {
            if (!ScopeEventsToRoot || _root is not Visual)
            {
                return true;
            }

            return IsInEventScope(e.Source);
        }

        private bool IsInEventScope(object? value)
        {
            if (!ScopeEventsToRoot || _root is not Visual rootVisual)
            {
                return true;
            }

            return value switch
            {
                null => true,
                Visual visual => visual == rootVisual || visual.GetVisualAncestors().Contains(rootVisual),
                _ => ReferenceEquals(value, _root)
            };
        }

        public bool ShowImplementedInterfaces
        {
            get => _showImplementedInterfaces;
            private set => RaiseAndSetIfChanged(ref _showImplementedInterfaces, value);
        }

        public void ToggleShowImplementedInterfaces(object parameter)
        {
            ShowImplementedInterfaces = !ShowImplementedInterfaces;
            if (Content is TreePageViewModel viewModel)
            {
                viewModel.UpdatePropertiesView();
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
            get => _FocusHighlighter;
            private set => RaiseAndSetIfChanged(ref _FocusHighlighter, value);
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
                DevToolsViewKind.Resources => 3,
                DevToolsViewKind.Assets => 4,
                DevToolsViewKind.Events => 5,
                _ => 0
            };
        }
    }
}
