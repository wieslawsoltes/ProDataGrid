using System;
using ProDataGrid.SourceGeneration;

namespace Avalonia.Diagnostics.ViewModels
{
    [GenerateDataGridColumns(
        ProviderName = "ResourceTreeGridSchema",
        SchemaId = "prodiagnostics/resource-tree/v1",
        Discovery = DataGridColumnDiscovery.AttributedOnly,
        Strict = true,
        HierarchicalRows = true)]
    internal abstract class ResourceTreeNode : ViewModelBase, IDisposable
    {
        private bool _isExpanded;

        protected ResourceTreeNode(
            ResourceTreeNode? parent,
            string name,
            string? secondaryText = null,
            string? valuePreview = null,
            string? valueType = null,
            object? source = null)
        {
            Parent = parent;
            Name = name;
            SecondaryText = secondaryText;
            ValuePreview = valuePreview;
            ValueType = valueType;
            Source = source;
        }

        public ResourceTreeNode? Parent { get; }
        public string Name { get; }
        public string? SecondaryText { get; }
        public string? ValuePreview { get; }
        public string? ValueType { get; }
        public object? Source { get; }

        [DataGridColumn(
            DataGridColumnKind.Hierarchical,
            Header = "Resource",
            ColumnKey = "resource",
            Order = 0,
            Width = "*",
            SortMemberPath = nameof(Name),
            TemplateKey = "ResourceTreeNodeCellTemplate",
            IsReadOnly = true)]
        public ResourceTreeNode Item => this;

        public abstract ResourceTreeNodeCollection Children { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => RaiseAndSetIfChanged(ref _isExpanded, value);
        }

        public virtual void Dispose()
        {
            Children.Dispose();
        }
    }
}
