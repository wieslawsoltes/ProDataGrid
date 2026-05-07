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
    public partial class TreePageTreeView : UserControl
    {
        private DataGridRow? _hovered;
        private readonly DataGrid _tree;
        private System.IDisposable? _adorner;

        public TreePageTreeView()
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

            if (row is null || row.OwningGrid != _tree)
            {
                _hovered = null;
                return;
            }

            _hovered = row;

            var visual = ResolveNode(row.DataContext)?.Visual as Visual;
            var shouldVisualizeMarginPadding = (DataContext as TreePageViewModel)?.MainView.ShouldVisualizeMarginPadding;
            if (visual is null || shouldVisualizeMarginPadding is null)
            {
                return;
            }

            _adorner = Controls.ControlHighlightAdorner.Add(visual, visualizeMarginPadding: shouldVisualizeMarginPadding == true);
        }

        private void RemoveAdorner(object? sender, PointerEventArgs e)
        {
            _adorner?.Dispose();
            _adorner = null;
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
                    oldViewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
                if (change.GetNewValue<object?>() is TreePageViewModel newViewModel)
                    newViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is TreePageViewModel viewModel)
            {
                viewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
            }

            _adorner?.Dispose();
            _adorner = null;

            base.OnDetachedFromVisualTree(e);
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
            var sb = new StringBuilder();
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
