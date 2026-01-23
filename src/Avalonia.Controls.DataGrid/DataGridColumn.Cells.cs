// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Avalonia.Controls
{
    abstract partial class DataGridColumn
    {
        private static readonly AttachedProperty<bool> CellBackgroundBindingAppliedProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, DataGridCell, bool>("CellBackgroundBindingApplied");

        private static readonly AttachedProperty<bool> CellForegroundBindingAppliedProperty =
            AvaloniaProperty.RegisterAttached<DataGridColumn, DataGridCell, bool>("CellForegroundBindingApplied");

        /// <summary>
        /// Gets the value of a cell according to the specified binding.
        /// </summary>
        /// <param name="item">The item associated with a cell.</param>
        /// <param name="binding">The binding to get the value of.</param>
        /// <returns>The resultant cell value.</returns>
        internal object GetCellValue(object item, IBinding binding)
        {
            Debug.Assert(OwningGrid != null);

            object content = null;
            if (binding != null)
            {
                OwningGrid.ClipboardContentControl.DataContext = item;
                var sub = OwningGrid.ClipboardContentControl.Bind(ContentControl.ContentProperty, binding);
                content = OwningGrid.ClipboardContentControl.GetValue(ContentControl.ContentProperty);
                sub.Dispose();
            }
            return content;
        }

        public Control GetCellContent(DataGridRow dataGridRow)
        {
            dataGridRow = dataGridRow ?? throw new ArgumentNullException(nameof(dataGridRow));
            if (OwningGrid == null)
            {
                throw DataGridError.DataGrid.NoOwningGrid(GetType());
            }
            if (dataGridRow.OwningGrid == OwningGrid)
            {
                DataGridCell dataGridCell = dataGridRow.Cells[Index];
                if (dataGridCell != null)
                {
                    return dataGridCell.Content as Control;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the column which contains the given element
        /// </summary>
        /// <param name="element">element contained in a column</param>
        /// <returns>Column that contains the element, or null if not found
        /// </returns>
        public static DataGridColumn GetColumnContainingElement(Control element)
        {
            // Walk up the tree to find the DataGridCell or DataGridColumnHeader that contains the element
            Visual parent = element;
            while (parent != null)
            {
                if (parent is DataGridCell cell)
                {
                    return cell.OwningColumn;
                }
                if (parent is DataGridColumnHeader columnHeader)
                {
                    return columnHeader.OwningColumn;
                }
                parent = parent.GetVisualParent();
            }
            return null;
        }

        /// <summary>
        /// Clears the current sort direction
        /// </summary>
        public void ClearSort()
        {
            //InvokeProcessSort is already validating if sorting is possible
            _headerCell?.InvokeProcessSort(KeyboardHelper.GetPlatformCtrlOrCmdKeyModifier(OwningGrid));
        }

        /// <summary>
        /// When overridden in a derived class, causes the column cell being edited to revert to the unedited value.
        /// </summary>
        /// <param name="editingElement">
        /// The element that the column displays for a cell in editing mode.
        /// </param>
        /// <param name="uneditedValue">
        /// The previous, unedited value in the cell being edited.
        /// </param>
        protected virtual void CancelCellEdit(Control editingElement, object uneditedValue)
        { }

        internal void CancelCellEditInternal(Control editingElement, object uneditedValue)
        {
            CancelCellEdit(editingElement, uneditedValue);
        }

        /// <summary>
        /// When overridden in a derived class, called when a cell in the column exits editing mode.
        /// </summary>
        protected virtual void EndCellEdit()
        { }

        internal void EndCellEditInternal()
        {
            EndCellEdit();
        }

        internal virtual DataGridColumnHeader CreateHeader()
        {
            var result = new DataGridColumnHeader
            {
                OwningColumn = this
            };
            _headerContentBinding?.Dispose();
            _headerTemplateBinding?.Dispose();
            _headerContentBinding = result.Bind(ContentControl.ContentProperty, this.GetObservable(HeaderProperty));
            _headerTemplateBinding = result.Bind(ContentControl.ContentTemplateProperty, this.GetObservable(HeaderTemplateProperty));
            result.Classes.Replace(HeaderStyleClasses);
            ApplyHeaderTheme(result);

            var filterTheme = FilterTheme ?? OwningGrid?.ColumnHeaderFilterTheme;
            if (filterTheme != null)
            {
                result.FilterTheme = filterTheme;
            }

            result.FilterFlyout = FilterFlyout;
            result.ShowFilterButton = ShowFilterButton || FilterFlyout != null;

            result.PointerPressed += (s, e) =>
            {
                if (e.Handled)
                {
                    return;
                }

                e.RoutedEvent = DataGridColumnHeader.HeaderPointerPressedEvent;
                e.Source ??= result;
                result.RaiseEvent(e);
                HeaderPointerPressed?.Invoke(this, e);
            };
            result.PointerReleased += (s, e) =>
            {
                if (e.Handled)
                {
                    return;
                }

                e.RoutedEvent = DataGridColumnHeader.HeaderPointerReleasedEvent;
                e.Source ??= result;
                result.RaiseEvent(e);
                HeaderPointerReleased?.Invoke(this, e);
            };
            return result;
        }

        internal void ApplyHeaderTheme(DataGridColumnHeader header)
        {
            var theme = HeaderTheme ?? OwningGrid?.ColumnHeaderTheme;
            if (theme != null)
            {
                header.SetValue(StyledElement.ThemeProperty, theme, BindingPriority.Template);
            }
            else
            {
                header.ClearValue(StyledElement.ThemeProperty);
            }
        }

        internal Control GenerateElementInternal(DataGridCell cell, object dataItem)
        {
            return GenerateElement(cell, dataItem);
        }

        internal void ApplyCellBindings(DataGridCell cell)
        {
            ApplyCellBinding(cell, TemplatedControl.BackgroundProperty, CellBackgroundBinding, CellBackgroundBindingAppliedProperty);
            ApplyCellBinding(cell, TemplatedControl.ForegroundProperty, CellForegroundBinding, CellForegroundBindingAppliedProperty);
        }

        internal void RefreshCellBindings(DataGridCell cell, string propertyName)
        {
            if (propertyName == nameof(CellBackgroundBinding) || propertyName == nameof(CellForegroundBinding))
            {
                ApplyCellBindings(cell);
            }
        }

        private static void ApplyCellBinding(
            DataGridCell cell,
            AvaloniaProperty property,
            IBinding binding,
            AttachedProperty<bool> appliedProperty)
        {
            if (binding != null)
            {
                cell.ClearValue(property);
                cell.Bind(property, binding);
                cell.SetValue(appliedProperty, true);
                return;
            }

            if (cell.GetValue(appliedProperty))
            {
                cell.ClearValue(property);
                cell.ClearValue(appliedProperty);
            }
        }

        protected virtual void RefreshEditingElement(Control editingElement)
        {
        }

        internal object PrepareCellForEditInternal(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            RefreshEditingElement(editingElement);
            var result = PrepareCellForEdit(editingElement, editingEventArgs);
            editingElement.Focus();

            return result;
        }

        //TODO Binding
        internal Control GenerateEditingElementInternal(DataGridCell cell, object dataItem)
        {
            if (_editingElement == null)
            {
                _editingElement = GenerateEditingElement(cell, dataItem, out _editBinding);
            }

            return _editingElement;
        }

        /// <summary>
        /// Clears the cached editing element.
        /// </summary>
        //TODO Binding
        internal void RemoveEditingElement()
        {
            _editingElement = null;
        }

        /// <summary>
        /// Clears cached cell/editor elements held by the column.
        /// </summary>
        internal virtual void ClearElementCache()
        {
            RemoveEditingElement();
            _editBinding = null;
            if (_headerContentBinding != null)
            {
                _headerContentBinding.Dispose();
                _headerContentBinding = null;
            }
            if (_headerTemplateBinding != null)
            {
                _headerTemplateBinding.Dispose();
                _headerTemplateBinding = null;
            }
            if (_headerCell != null)
            {
                var headerCell = _headerCell;
                _headerCell = null;
                headerCell.ClearValue(ContentControl.ContentProperty);
                headerCell.ClearValue(ContentControl.ContentTemplateProperty);
                headerCell.ClearValue(StyledElement.ThemeProperty);
                headerCell.ClearValue(DataGridColumnHeader.FilterThemeProperty);
                headerCell.ClearValue(DataGridColumnHeader.FilterFlyoutProperty);
                headerCell.ClearValue(DataGridColumnHeader.ShowFilterButtonProperty);
                headerCell.OwningColumn = null;
            }
        }

        /// <summary>
        /// We get the sort description from the data source.  We don't worry whether we can modify sort -- perhaps the sort description
        /// describes an unchangeable sort that exists on the data.
        /// </summary>
        internal DataGridSortDescription GetSortDescription()
        {
            if (OwningGrid != null)
            {
                var descriptor = OwningGrid.GetSortingDescriptorForColumn(this);
                if (descriptor != null)
                {
                    if (descriptor.HasComparer)
                    {
                        if (descriptor.Comparer is IDataGridColumnValueAccessorComparer)
                        {
                            var propertyPath = descriptor.PropertyPath;
                            if (string.IsNullOrEmpty(propertyPath))
                            {
                                propertyPath = GetSortPropertyName();
                            }

                            return !string.IsNullOrEmpty(propertyPath)
                                ? DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction, propertyPath)
                                : DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction);
                        }

                        return DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction);
                    }

                    if (descriptor.HasPropertyPath)
                    {
                        var accessor = DataGridColumnMetadata.GetValueAccessor(this);
                        if (accessor != null)
                        {
                            var culture = descriptor.Culture ?? CultureInfo.InvariantCulture;
                            var comparer = DataGridColumnValueAccessorComparer.Create(accessor, culture);
                            return DataGridSortDescription.FromComparer(comparer, descriptor.Direction, descriptor.PropertyPath);
                        }

                        return DataGridSortDescription.FromPath(descriptor.PropertyPath, descriptor.Direction, descriptor.Culture);
                    }
                }

                if (OwningGrid.DataConnection != null
                    && OwningGrid.DataConnection.SortDescriptions != null)
                {
                    if (CustomSortComparer != null)
                    {
                        return OwningGrid.DataConnection.SortDescriptions
                            .OfType<DataGridComparerSortDescription>()
                            .FirstOrDefault(s => s.SourceComparer == CustomSortComparer);
                    }

                    var accessor = DataGridColumnMetadata.GetValueAccessor(this);
                    if (accessor != null)
                    {
                        var match = OwningGrid.DataConnection.SortDescriptions
                            .OfType<DataGridComparerSortDescription>()
                            .FirstOrDefault(s => s.SourceComparer is IDataGridColumnValueAccessorComparer accessorComparer
                                && ReferenceEquals(accessorComparer.Accessor, accessor));
                        if (match != null)
                        {
                            return match;
                        }
                    }

                    string propertyName = GetSortPropertyName();

                    return OwningGrid.DataConnection.SortDescriptions.FirstOrDefault(s => s.HasPropertyPath && s.PropertyPath == propertyName);
                }
            }

            return null;
        }

        internal string GetSortPropertyName()
        {
            string result = SortMemberPath;

            if (String.IsNullOrEmpty(result))
            {
                if (this is DataGridBoundColumn boundColumn)
                {
                    if (boundColumn.Binding is Binding binding)
                    {
                        result = binding.Path;
                    }
                    else if (boundColumn.Binding is CompiledBindingExtension compiledBinding)
                    {
                        result = compiledBinding.Path.ToString();
                    }
                }
            }

            return result;
        }

    }
}
