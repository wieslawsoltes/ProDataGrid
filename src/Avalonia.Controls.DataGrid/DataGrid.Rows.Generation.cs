// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Styling;
using System.Diagnostics;

namespace Avalonia.Controls
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {

        /// <summary>
        /// Returns a row for the provided index. The row gets first loaded through the LoadingRow event.
        /// </summary>
        private DataGridRow GenerateRow(int rowIndex, int slot)
        {
            return GenerateRow(rowIndex, slot, DataConnection.GetDataItem(rowIndex));
        }



        /// <summary>
        /// Returns a row for the provided index. The row gets first loaded through the LoadingRow event.
        /// </summary>
        private DataGridRow GenerateRow(int rowIndex, int slot, object dataContext)
        {
            Debug.Assert(rowIndex > -1);
            using var activity = DataGridDiagnostics.GenerateRow();
            using var _ = DataGridDiagnostics.BeginRowGenerate();
            activity?.SetTag(DataGridDiagnostics.Tags.RowIndex, rowIndex);
            activity?.SetTag(DataGridDiagnostics.Tags.Slot, slot);
            string source = null;
            DataGridRow dataGridRow = GetGeneratedRow(dataContext);
            bool isOwnContainer = false;
            if (dataGridRow != null)
            {
                source = DataGridDiagnostics.Sources.Existing;
            }

            if (dataGridRow == null && IsItemItsOwnContainerOverride(dataContext))
            {
                dataGridRow = dataContext as DataGridRow;
                isOwnContainer = dataGridRow != null;
                if (isOwnContainer)
                {
                    source = DataGridDiagnostics.Sources.OwnContainer;
                }
            }

            if (dataGridRow == null)
            {
                var recycledRow = DisplayData.GetRecycledRow(dataContext, rowIndex, slot);
                source = recycledRow != null
                    ? DataGridDiagnostics.Sources.Recycled
                    : DataGridDiagnostics.Sources.New;
                dataGridRow = recycledRow ?? CreateRowContainer(dataContext, rowIndex, slot);
                var previousDataContext = (dataGridRow.RecycledDataContext ?? dataGridRow.DataContext);
                var hasPlaceholderTransition =
                    recycledRow != null &&
                    !ReferenceEquals(previousDataContext, dataContext) &&
                    (ReferenceEquals(dataContext, DataGridCollectionView.NewItemPlaceholder) ||
                     ReferenceEquals(previousDataContext, DataGridCollectionView.NewItemPlaceholder));

                // Compiled bindings in recycled cells can throw when a row transitions to or from
                // the new-item placeholder. Drop stale visuals before changing DataContext.
                if (hasPlaceholderTransition)
                {
                    foreach (DataGridCell cell in dataGridRow.Cells)
                    {
                        cell.Content = null;
                    }
                }

                dataGridRow.Index = rowIndex;
                dataGridRow.Slot = slot;
                dataGridRow.OwningGrid = this;
                dataGridRow.DataContext = dataContext;
                dataGridRow.IsPlaceholder = ReferenceEquals(dataContext, DataGridCollectionView.NewItemPlaceholder);
                UpdateRowHeader(dataGridRow);
                if (RowTheme is {} rowTheme)
                {
                    dataGridRow.SetValue(ThemeProperty, rowTheme, BindingPriority.Template);
                }
                CompleteCellsCollection(dataGridRow);
                PrepareRowForItem(dataGridRow, dataContext);

                // Placeholder transitions clear every cell's Content before DataContext change,
                // so regenerate all columns (including templates). On a normal recycle, only the
                // exact built-in columns whose retained content is data-context safe are reused.
                // Derived/custom columns regenerate because their element generation may depend on
                // the row item. Direct-provider cells also regenerate when the new item is not
                // compatible with the provider, allowing the binding fallback to take over.
                // Reused descendants can retain valid measure state after their DataContext changes;
                // height-based scrolling may consume the row's DesiredSize before the next layout pass.
                if (hasPlaceholderTransition)
                {
                    foreach (DataGridCell cell in dataGridRow.Cells)
                    {
                        cell.Content = cell.OwningColumn.GenerateElementInternal(cell, dataContext);
                        cell.InvalidateMeasureForContentChange();
                    }
                }
                else if (recycledRow != null)
                {
                    foreach (DataGridCell cell in dataGridRow.Cells)
                    {
                        if (ShouldRegenerateRecycledCell(cell, dataContext))
                        {
                            cell.Content = cell.OwningColumn.GenerateElementInternal(cell, dataContext);
                            cell.InvalidateMeasureForContentChange();
                        }
                        else if (double.IsNaN(RowHeight))
                        {
                            // Reused descendants can retain the previous item's desired size.
                            cell.InvalidateMeasureForContentChange();
                        }
                    }
                }
                ApplyConditionalFormattingForRow(dataGridRow);
                dataGridRow.ClearRecyclingState();
                NotifyPreparedRowCells(dataGridRow);
                OnLoadingRow(new DataGridRowEventArgs(dataGridRow));
            }
            else if (isOwnContainer)
            {
                dataGridRow.Index = rowIndex;
                dataGridRow.Slot = slot;
                dataGridRow.OwningGrid = this;
                dataGridRow.DataContext = dataContext;
                dataGridRow.IsPlaceholder = ReferenceEquals(dataGridRow.DataContext, DataGridCollectionView.NewItemPlaceholder);
                UpdateRowHeader(dataGridRow);
                if (RowTheme is {} rowTheme)
                {
                    dataGridRow.SetValue(ThemeProperty, rowTheme, BindingPriority.Template);
                }
                CompleteCellsCollection(dataGridRow);
                PrepareRowForItem(dataGridRow, dataGridRow.DataContext ?? dataContext);
                ApplyConditionalFormattingForRow(dataGridRow);
                dataGridRow.ClearRecyclingState();
                NotifyPreparedRowCells(dataGridRow);
                OnLoadingRow(new DataGridRowEventArgs(dataGridRow));
            }

            if (source != null)
            {
                activity?.SetTag(DataGridDiagnostics.Tags.Source, source);
                DataGridDiagnostics.RecordRowRealized(source);
            }
            return dataGridRow;
        }



        /// <summary>
        /// Creates all the editing elements for the current editing row, so the bindings
        /// all exist during validation.
        /// </summary>
        private void GenerateEditingElements()
        {
            if (EditingRow != null && EditingRow.Cells != null)
            {
                Debug.Assert(EditingRow.Cells.Count == ColumnsItemsInternal.Count);
                foreach (DataGridColumn column in ColumnsInternal.GetDisplayedColumns(c => c.IsVisible && !c.IsReadOnly))
                {
                    column.GenerateEditingElementInternal(EditingRow.Cells[column.Index], EditingRow.DataContext);
                }
            }
        }

        private static bool ShouldRegenerateRecycledCell(DataGridCell cell, object dataContext)
        {
            DataGridColumn column = cell.OwningColumn;
            if (column == null)
            {
                return false;
            }

            // Derived/custom columns can make element generation depend on the row item,
            // even when they reuse one of the built-in coalesced cell container types.
            if (!column.CanReuseCellContentOnDataContextChange)
            {
                return true;
            }

            // Direct retained text cells re-evaluate and switch their accessor/binding mode
            // synchronously when the row DataContext changes. Only drawn-provider cells need
            // a regeneration-time compatibility check here.
            if (cell is DataGridDirectTextCell or DataGridDirectHierarchicalCell)
            {
                return false;
            }

            if (cell is not DataGridCustomDrawingCell { UsesValueProvider: true })
            {
                return false;
            }

            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            return dataContext == null ||
                   accessor == null ||
                   !accessor.ItemType.IsInstanceOfType(dataContext);
        }



        /// <summary>
        /// Checks if the row for the provided dataContext has been generated and is present
        /// in either the loaded rows, pre-fetched rows, or editing row.
        /// The displayed rows are *not* searched. Returns null if the row does not belong to those 3 categories.
        /// </summary>
        private DataGridRow GetGeneratedRow(object dataContext)
        {
            // Check the list of rows being loaded via the LoadingRow event.
            DataGridRow dataGridRow = GetLoadedRow(dataContext);
            if (dataGridRow != null)
            {
                return dataGridRow;
            }

            // Check the potential editing row.
            if (EditingRow != null && dataContext == EditingRow.DataContext)
            {
                return EditingRow;
            }

            // Check the potential focused row.
            if (_focusedRow != null && dataContext == _focusedRow.DataContext)
            {
                return _focusedRow;
            }

            return null;
        }



        private DataGridRow GetLoadedRow(object dataContext)
        {
            foreach (DataGridRow dataGridRow in _loadedRows)
            {
                if (dataGridRow.DataContext == dataContext)
                {
                    return dataGridRow;
                }
            }
            return null;
        }



        private Control InsertDisplayedElement(int slot, bool updateSlotInformation)
        {
            Control slotElement;
            if (IsGroupHeaderSlot(slot))
            {
                slotElement = GenerateRowGroupHeader(slot, rowGroupInfo: RowGroupHeadersTable.GetValueAt(slot));
            }
            else if (IsGroupFooterSlot(slot))
            {
                slotElement = GenerateRowGroupFooter(slot, rowGroupInfo: RowGroupFootersTable.GetValueAt(slot));
            }
            else if (UsesLayoutItemPresentation)
            {
                slotElement = GenerateLayoutItemContainer(slot);
            }
            else
            {
                // If we're grouping, the GroupLevel needs to be fixed later by methods calling this
                // which end up inserting rows. We don't do it here because elements could be inserted
                // from top to bottom or bottom to up so it's better to do in one pass
                slotElement = GenerateRow(RowIndexFromSlot(slot), slot);
            }
            InsertDisplayedElement(slot, slotElement, wasNewlyAdded: false, updateSlotInformation: updateSlotInformation);
            return slotElement;
        }



        private void InsertDisplayedElement(int slot, Control element, bool wasNewlyAdded, bool updateSlotInformation)
        {
            using var insertTimer = DataGridDiagnostics.BeginRowsDisplayElementInsert();

            // We can only support creating new rows that are adjacent to the currently visible rows
            // since they need to be added to the visual tree for us to Measure them.
            Debug.Assert(DisplayData.FirstScrollingSlot == -1 || slot >= GetPreviousVisibleSlot(DisplayData.FirstScrollingSlot) && slot <= GetNextVisibleSlot(DisplayData.LastScrollingSlot));
            Debug.Assert(element != null);

            DataGridRow row = null;
            DataGridItemContainer itemContainer = null;
            DataGridRowGroupHeader groupHeader = null;
            DataGridRowGroupFooter groupFooter = null;
            double elementHeight = 0;
            bool measureDeferred = false;

            if (_rowsPresenter != null)
            {
                row = element as DataGridRow;
                using (DataGridDiagnostics.BeginRowsDisplayElementAttach())
                {
                    if (row != null)
                    {
                        LoadRowVisualsForDisplay(row);

                        if (!ReferenceEquals(row.Parent, _rowsPresenter))
                        {
                            _rowsPresenter.Children.Add(row);
                        }

                        if (!IsRowRecyclable(row))
                        {
                            element.Clip = null;
                            Debug.Assert(row.Index == RowIndexFromSlot(slot));
                        }
                    }
                    else if ((itemContainer = element as DataGridItemContainer) != null)
                    {
                        if (!ReferenceEquals(itemContainer.Parent, _rowsPresenter))
                        {
                            _rowsPresenter.Children.Add(itemContainer);
                        }
                    }
                    else
                    {
                        groupHeader = element as DataGridRowGroupHeader;
                        if (groupHeader == null)
                        {
                            groupFooter = element as DataGridRowGroupFooter;
                        }
                        Debug.Assert(groupHeader != null || groupFooter != null);  // Rows, RowGroupHeaders, or RowGroupFooters
                        if (groupHeader != null)
                        {
                            groupHeader.TotalIndent = (groupHeader.Level == 0) ? 0 : RowGroupSublevelIndents[groupHeader.Level - 1];
                            if (!ReferenceEquals(groupHeader.Parent, _rowsPresenter))
                            {
                                _rowsPresenter.Children.Add(element);
                            }
                            groupHeader.IsRecycled = false;
                            groupHeader.LoadVisualsForDisplay();
                        }
                        else if (groupFooter != null)
                        {
                            if (!ReferenceEquals(groupFooter.Parent, _rowsPresenter))
                            {
                                _rowsPresenter.Children.Add(element);
                            }
                            groupFooter.IsRecycled = false;
                            groupFooter.ApplySummaryRowTheme();
                            groupFooter.UpdateSummaryRowOffset();
                            groupFooter.UpdateSummaryRowState();
                        }
                    }

                    if (row != null)
                    {
                        _rowsPresenter.RegisterAnchorCandidate(row);
                    }
                    else if (itemContainer != null)
                    {
                        _rowsPresenter.RegisterAnchorCandidate(itemContainer);
                    }
                    else if (groupHeader != null)
                    {
                        _rowsPresenter.RegisterAnchorCandidate(groupHeader);
                    }
                    else if (groupFooter != null)
                    {
                        _rowsPresenter.RegisterAnchorCandidate(groupFooter);
                    }
                }

                measureDeferred = row != null && TryGetDeferredRowHeight(slot, row, out elementHeight);
                if (measureDeferred)
                {
                    row.SetDeferredHeight(elementHeight);
                }

                using (DataGridDiagnostics.BeginRowsDisplayElementMeasure())
                {
                    row?.InvalidateMeasure();
                    if (measureDeferred)
                    {
                        AvailableSlotElementRoom -= elementHeight;
                    }
                    else
                    {
                        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        elementHeight = element.DesiredSize.Height;
                        AvailableSlotElementRoom -= elementHeight;
                    }
                }

                using (DataGridDiagnostics.BeginRowsDisplayElementHeightRecord())
                {
                    var estimator = RowHeightEstimator;

                    if (groupHeader != null)
                    {
                        elementHeight = groupHeader.DesiredSize.Height;
                        _rowGroupHeightsByLevel[groupHeader.Level] = elementHeight;
                        // Record the measured group header height with the estimator
                        if (estimator != null)
                        {
                            RecordMeasuredGroupHeaderHeight(estimator, slot, groupHeader.Level, elementHeight);
                        }
                    }
                    else if (groupFooter != null)
                    {
                        elementHeight = groupFooter.DesiredSize.Height;
                        _rowGroupHeightsByLevel[groupFooter.Level] = elementHeight;
                        if (estimator != null)
                        {
                            RecordMeasuredGroupHeaderHeight(estimator, slot, groupFooter.Level, elementHeight);
                        }
                    }

                    if (row != null)
                    {
                        // Record the measured row height with the estimator
                        bool hasDetails = GetRowDetailsVisibility(slot);
                        // Details height is already included in element.DesiredSize.Height
                        if (estimator != null)
                        {
                            RecordMeasuredRowHeight(estimator, slot, elementHeight, hasDetails);
                        }

                        // Update the legacy estimate for backward compatibility
                        if (RowHeightEstimate == DataGrid.DATAGRID_defaultRowHeight && double.IsNaN(row.Height))
                        {
                            RowHeightEstimate = element.DesiredSize.Height;
                        }
                    }

                    UpdateScrollHeightEstimate(slot, elementHeight);
                }
            }

            using (DataGridDiagnostics.BeginRowsDisplayElementLoad())
            {
                if (wasNewlyAdded)
                {
                    DisplayData.CorrectSlotsAfterInsertion(slot, element, isCollapsed: false);
                }
                else
                {
                    DisplayData.LoadScrollingSlot(slot, element, updateSlotInformation);
                }
            }
        }

        private bool TryGetDeferredRowHeight(int slot, DataGridRow row, out double height)
        {
            height = 0;
            if (!_scrollingByHeight || row.IsMeasureValid || GetRowDetailsVisibility(slot))
            {
                return false;
            }

            var rowHeight = row.Height;
            if (double.IsNaN(rowHeight) || double.IsInfinity(rowHeight))
            {
                return false;
            }

            height = Math.Max(row.MinHeight, Math.Min(row.MaxHeight, rowHeight)) +
                row.Margin.Top + row.Margin.Bottom;
            return !double.IsNaN(height) && !double.IsInfinity(height) && height >= 0;
        }


    }
}
