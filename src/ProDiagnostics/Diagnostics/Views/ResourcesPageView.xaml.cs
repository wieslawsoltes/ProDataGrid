using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views
{
    partial class ResourcesPageView : UserControl
    {
        private DataGridRow? _hovered;
        private DataGrid _tree;
        private System.IDisposable? _adorner;
        private Visual? _adornedVisual;
        private MainViewModel? _mainView;

        public ResourcesPageView()
        {
            InitializeComponent();
            _tree = this.GetControl<DataGrid>("resourcesTree");
        }

        protected void UpdateAdorner(object? sender, PointerEventArgs e)
        {
            if (e.Source is not StyledElement source)
            {
                return;
            }

            var row = source.FindLogicalAncestorOfType<DataGridRow>();
            if (row == _hovered)
            {
                return;
            }

            _adorner?.Dispose();
            _adorner = null;

            if (row is null || row.OwningGrid != _tree)
            {
                _hovered = null;
                _adornedVisual = null;
                return;
            }

            _hovered = row;

            var node = ResolveNode(row.DataContext);
            var visual = ResolveVisual(node);
            _adornedVisual = visual;

            if (visual is null ||
                DataContext is not ResourcesPageViewModel vm ||
                !vm.MainView.HighlightElements ||
                !vm.MainView.ShouldRenderLocalHighlightAdorners)
            {
                return;
            }

            _adorner = Controls.ControlHighlightAdorner.Add(visual, vm.MainView.OverlayDisplayOptions);
        }

        private void RemoveAdorner(object? sender, PointerEventArgs e)
        {
            _adorner?.Dispose();
            _adorner = null;
            _adornedVisual = null;
        }

        private static ResourceTreeNode? ResolveNode(object? dataContext)
        {
            return dataContext switch
            {
                HierarchicalNode node => node.Item as ResourceTreeNode,
                ResourceTreeNode resourceNode => resourceNode,
                _ => null
            };
        }

        private static Visual? ResolveVisual(ResourceTreeNode? node)
        {
            if (node is null)
            {
                return null;
            }

            if (node is ResourceHostNode hostNode)
            {
                return hostNode.Host as Visual;
            }

            return node.Source as Visual;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != DataContextProperty)
            {
                return;
            }

            if (change.GetOldValue<object?>() is ResourcesPageViewModel oldViewModel)
            {
                oldViewModel.MainView.PropertyChanged -= OnMainViewPropertyChanged;
            }

            if (change.GetNewValue<object?>() is ResourcesPageViewModel newViewModel)
            {
                _mainView = newViewModel.MainView;
                _mainView.PropertyChanged += OnMainViewPropertyChanged;
                RefreshAdornerFromCurrentVisual();
            }
            else
            {
                _mainView = null;
                _adornedVisual = null;
                _adorner?.Dispose();
                _adorner = null;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_mainView is not null)
            {
                _mainView.PropertyChanged -= OnMainViewPropertyChanged;
            }

            _adorner?.Dispose();
            _adorner = null;
            _adornedVisual = null;
            _mainView = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnMainViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.HighlightElements)
                or nameof(MainViewModel.ShouldRenderLocalHighlightAdorners)
                or nameof(MainViewModel.ShouldVisualizeMarginPadding)
                or nameof(MainViewModel.ShowOverlayInfo)
                or nameof(MainViewModel.ShowOverlayRulers)
                or nameof(MainViewModel.ShowOverlayExtensionLines))
            {
                RefreshAdornerFromCurrentVisual();
            }
        }

        private void RefreshAdornerFromCurrentVisual()
        {
            _adorner?.Dispose();
            _adorner = null;

            if (_adornedVisual is null || _mainView is not { HighlightElements: true, ShouldRenderLocalHighlightAdorners: true })
            {
                return;
            }

            _adorner = Controls.ControlHighlightAdorner.Add(_adornedVisual, _mainView.OverlayDisplayOptions);
        }
    }
}
