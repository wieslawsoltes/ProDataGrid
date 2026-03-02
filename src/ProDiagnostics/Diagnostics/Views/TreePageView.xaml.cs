using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views
{
    partial class TreePageView : UserControl
    {
        private DataGridRow? _hovered;
        private DataGrid _tree;
        private System.IDisposable? _adorner;
        private Visual? _adornedVisual;
        private MainViewModel? _mainView;

        public TreePageView()
        {
            InitializeComponent();
            _tree = this.GetControl<DataGrid>("tree");
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

            var visual = ResolveNode(row.DataContext)?.Visual as Visual;
            _adornedVisual = visual;
            if (DataContext is not TreePageViewModel vm ||
                visual is null ||
                !vm.MainView.HighlightElements)
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

        private static TreeNode? ResolveNode(object? dataContext)
        {
            return dataContext switch
            {
                HierarchicalNode node => node.Item as TreeNode,
                TreeNode treeNode => treeNode,
                _ => null
            };
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DataContextProperty)
            {
                if (change.GetOldValue<object?>() is TreePageViewModel oldViewModel)
                {
                    oldViewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
                    oldViewModel.MainView.PropertyChanged -= OnMainViewPropertyChanged;
                }

                if (change.GetNewValue<object?>() is TreePageViewModel newViewModel)
                {
                    newViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
                    newViewModel.MainView.PropertyChanged += OnMainViewPropertyChanged;
                    _mainView = newViewModel.MainView;
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
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is TreePageViewModel vm)
            {
                vm.MainView.PropertyChanged -= OnMainViewPropertyChanged;
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

            if (_adornedVisual is null || _mainView is not { HighlightElements: true })
            {
                return;
            }

            _adorner = Controls.ControlHighlightAdorner.Add(_adornedVisual, _mainView.OverlayDisplayOptions);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnClipboardCopyRequested(object? sender, string selector)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                var dataTransferItem = new DataTransferItem();
                dataTransferItem.SetText(ToText(selector));
                dataTransferItem.Set(DevToolsDataFormats.Selector, selector);

                var dataTransfer = new DataTransfer();
                dataTransfer.Add(dataTransferItem);
                clipboard.SetDataAsync(dataTransfer);
            }
        }

        private static string ToText(string text)
        {
            var sb = new System.Text.StringBuilder();
            var bufferStartIndex = -1;
            for (var ic = 0; ic < text.Length; ic++)
            {
                var c = text[ic];
                switch (c)
                {
                    case '{':
                        bufferStartIndex = sb.Length;
                        break;
                    case '}' when bufferStartIndex > -1:
                        sb.Remove(bufferStartIndex, sb.Length - bufferStartIndex);
                        bufferStartIndex = sb.Length;
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
