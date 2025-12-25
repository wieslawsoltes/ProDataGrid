// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Collections;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Avalonia.Controls
{
    /// <summary>
    /// Column that renders hierarchical rows with an expander and indentation.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    class DataGridHierarchicalColumn : DataGridBoundColumn
    {
        private static readonly IValueConverter _isExpandableConverter =
            new FuncValueConverter<bool, bool>(value => !value);

        private readonly Lazy<IDataTemplate?> _cellTemplate;

        public DataGridHierarchicalColumn()
        {
            BindingTarget = ContentControl.ContentProperty;
            IsReadOnly = true;

            _cellTemplate = new Lazy<IDataTemplate?>(() =>
                OwningGrid != null && OwningGrid.TryFindResource("DataGridHierarchicalCellTemplate", out var template)
                    ? (IDataTemplate)template
                    : null);
        }

        /// <summary>
        /// Identifies the <see cref="Indent"/> property.
        /// </summary>
        public static readonly DirectProperty<DataGridHierarchicalColumn, double> IndentProperty =
            AvaloniaProperty.RegisterDirect<DataGridHierarchicalColumn, double>(
                nameof(Indent),
                o => o.Indent,
                (o, v) => o.Indent = v,
                16d);

        private double _indent = 16d;

        /// <summary>
        /// Gets or sets the per-level indent applied to the presenter.
        /// </summary>
        public double Indent
        {
            get => _indent;
            set
            {
                if (Math.Abs(_indent - value) > double.Epsilon)
                {
                    _indent = value;
                    NotifyPropertyChanged(nameof(Indent));
                }
            }
        }

        /// <summary>
        /// Gets or sets the template used to display the cell content.
        /// </summary>
        public IDataTemplate? CellTemplate { get; set; }

        /// <inheritdoc />
        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            var presenter = CreatePresenter();
            BindContent(presenter, dataItem, isEditing: false);
            return presenter;
        }

        /// <inheritdoc />
        protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
        {
            var presenter = CreatePresenter();
            BindContent(presenter, dataItem, isEditing: true);
            return presenter;
        }

        /// <inheritdoc />
        protected internal override void RefreshCellContent(Control element, string propertyName)
        {
            base.RefreshCellContent(element, propertyName);

            if (propertyName == nameof(Indent) && element is DataGridHierarchicalPresenter presenter)
            {
                presenter.Indent = Indent;
            }
        }

        /// <inheritdoc />
        protected override object? PrepareCellForEdit(Control editingElement, Avalonia.Interactivity.RoutedEventArgs editingEventArgs)
        {
            return (editingElement as ContentControl)?.Content;
        }

        private DataGridHierarchicalPresenter CreatePresenter()
        {
            var presenter = new DataGridHierarchicalPresenter
            {
                Indent = Indent
            };

            presenter.ToggleRequested += PresenterOnToggleRequested;
            presenter.Bind(DataGridHierarchicalPresenter.LevelProperty, new Binding(nameof(HierarchicalNode.Level)));
            presenter.Bind(
                DataGridHierarchicalPresenter.IsExpandedProperty,
                new Binding(nameof(HierarchicalNode.IsExpanded)) { Mode = BindingMode.OneWay });
            presenter.Bind(
                DataGridHierarchicalPresenter.IsExpandableProperty,
                new Binding(nameof(HierarchicalNode.IsLeaf))
                {
                    Mode = BindingMode.OneWay,
                    Converter = _isExpandableConverter
                });

            return presenter;
        }

        private void PresenterOnToggleRequested(object? sender, EventArgs e)
        {
            if (OwningGrid?.HierarchicalModel == null)
            {
                return;
            }

            if (sender is DataGridHierarchicalPresenter presenter &&
                presenter.DataContext is HierarchicalNode node)
            {
                var row = presenter.FindAncestorOfType<DataGridRow>();
                if (row != null)
                {
                    OwningGrid.PrepareHierarchicalAnchor(row.Slot);
                }

                OwningGrid.HierarchicalModel.Toggle(node);
            }
        }

        private void BindContent(DataGridHierarchicalPresenter presenter, object dataItem, bool isEditing)
        {
            if (Binding != null && dataItem != DataGridCollectionView.NewItemPlaceholder)
            {
                presenter.Bind(ContentControl.ContentProperty, Binding);
            }
            else
            {
                presenter.Content = dataItem;
            }

            presenter.ContentTemplate = CellTemplate ?? _cellTemplate.Value;
        }
    }
}
