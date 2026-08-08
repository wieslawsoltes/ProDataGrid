using System;
using System.Collections.Specialized;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Reactive;
using ProDataGrid.SourceGeneration;

namespace Avalonia.Diagnostics.ViewModels
{
    [GenerateDataGridColumns(
        ProviderName = "VisualTreeGridSchema",
        SchemaId = "prodiagnostics/visual-tree/v1",
        Discovery = DataGridColumnDiscovery.AttributedOnly,
        Strict = true,
        HierarchicalRows = true)]
    internal abstract class TreeNode : ViewModelBase, IDisposable
    {
        private readonly IDisposable? _classesSubscription;
        private string _classes;
        private bool _isExpanded;

        protected TreeNode(AvaloniaObject avaloniaObject, TreeNode? parent, string? customTypeName = null, bool showDecorations = true)
        {
            _classes = string.Empty;
            Parent = parent;
            Type = customTypeName ?? avaloniaObject.GetType().Name;
            Visual = avaloniaObject;
            FontWeight = IsRoot ? FontWeight.Bold : FontWeight.Normal;
            ShowDecorations = showDecorations;

            ElementName = (avaloniaObject as INamed)?.Name;

            if (avaloniaObject is StyledElement { Classes: { } classes })
            {
                _classesSubscription = ((IObservable<object?>)classes.GetWeakCollectionChangedObservable())
                    .StartWith(null)
                    .Subscribe(_ =>
                    {
                        if (classes.Count > 0)
                        {
                            Classes = $"({string.Join(" ", classes)})";
                        }
                        else
                        {
                            Classes = string.Empty;
                        }
                    });
            }
        }

        private bool IsRoot => Visual is TopLevel ||
                               Visual is ContextMenu ||
                               Visual is IPopupHost;

        public FontWeight FontWeight { get; }

        public abstract TreeNodeCollection Children
        {
            get;
        }

        public string Classes
        {
            get { return _classes; }
            private set { RaiseAndSetIfChanged(ref _classes, value); }
        }

        public string? ElementName
        {
            get;
        }

        public AvaloniaObject Visual
        {
            get;
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { RaiseAndSetIfChanged(ref _isExpanded, value); }
        }

        public TreeNode? Parent
        {
            get;
        }

        public string Type
        {
            get;
            private set;
        }

        public bool ShowDecorations { get; }

        [DataGridColumn(
            DataGridColumnKind.Hierarchical,
            Header = "Visual",
            ColumnKey = "visual",
            Order = 0,
            Width = "SizeToCells",
            SortMemberPath = nameof(Type),
            TemplateKey = "VisualTreeNodeCellTemplate",
            IsReadOnly = true)]
        public TreeNode Item => this;

        public void Dispose()
        {
            _classesSubscription?.Dispose();
            Children.Dispose();
        }
    }
}
