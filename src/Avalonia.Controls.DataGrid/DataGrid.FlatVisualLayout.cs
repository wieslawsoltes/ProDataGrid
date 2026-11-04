// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.ComponentModel;

using Avalonia.Collections;
using Avalonia.Utilities;

namespace Avalonia.Controls;

#if !DATAGRID_INTERNAL
public
#else
internal
#endif
partial class DataGrid
{
    private DataGridRow? _virtualCompatibilityRow;
    private bool _virtualAutomationRequiresRows;
    private bool _virtualEditingRequiresRows;
    private bool _virtualItemsRequireRows;

    internal bool UsesFlatVisualLayout => VisualLayoutMode != DataGridVisualLayoutMode.Nested;

    internal bool UsesVirtualCellSurface =>
        VisualLayoutMode == DataGridVisualLayoutMode.Virtualized && CanDrawAllVisibleColumnsOnVirtualSurface();

    internal bool UsesVirtualCellSurfaceFallback =>
        VisualLayoutMode == DataGridVisualLayoutMode.Virtualized && !UsesVirtualCellSurface;

    internal bool UsesDefaultVirtualRowPipeline =>
        UsesVirtualCellSurface &&
        UsesDefaultRealizationFactory &&
        GetType() == typeof(DataGrid);

    internal bool UsesLightweightVirtualRows =>
        UsesDefaultVirtualRowPipeline &&
        !_virtualAutomationRequiresRows &&
        !_virtualEditingRequiresRows &&
        !_virtualItemsRequireRows &&
        !AreRowHeadersVisible &&
        !ShowRowNumbers &&
        RowDetailsTemplate is null &&
        !HasLoadingRowHandlers() &&
        !HasUnloadingRowHandlers() &&
        RowGroupHeadersTable.RangeCount == 0 &&
        RowGroupFootersTable.RangeCount == 0 &&
        _collapsedSlotsTable.IsEmpty;

    internal bool TryGetLightweightVirtualRowHeight(out double rowHeight)
    {
        rowHeight = double.NaN;
        if (!UsesLightweightVirtualRows || !double.IsFinite(RowHeight))
        {
            return false;
        }

        rowHeight = DataGridRow.GetFlatDesiredHeight(this, RowHeight);
        return double.IsFinite(rowHeight) && MathUtilities.GreaterThan(rowHeight, 0);
    }

    private void RequireRetainedVirtualRowsForEditing()
    {
        if (!UsesLightweightVirtualRows || !DisplayData.HasVirtualScrollingElements)
        {
            return;
        }

        _virtualEditingRequiresRows = true;
        MaterializeRetainedVirtualRows();
    }

    private void ReleaseRetainedVirtualRowsForEditing()
    {
        if (!_virtualEditingRequiresRows)
        {
            return;
        }

        int firstSlot = DisplayData.FirstScrollingSlot;
        _virtualEditingRequiresRows = false;
        if (firstSlot >= 0 && TryGetLightweightVirtualRowHeight(out _))
        {
            ResetDisplayedRows();
            RemoveRecycledChildrenFromVisualTree();
            UpdateDisplayedRows(firstSlot, CellsEstimatedHeight);
        }

        _rowsPresenter?.InvalidateMeasure();
    }

    internal void RequireRetainedVirtualRowsForAutomation()
    {
        if (_virtualAutomationRequiresRows)
        {
            return;
        }

        _virtualAutomationRequiresRows = true;
        if (DisplayData.HasVirtualScrollingElements)
        {
            MaterializeRetainedVirtualRows();
        }
    }

    internal void RequireRetainedVirtualRowsForItems()
    {
        _virtualItemsRequireRows = true;
    }

    private void MaterializeRetainedVirtualRows()
    {
        int firstSlot = DisplayData.FirstScrollingSlot;
        if (firstSlot < 0)
        {
            return;
        }

        ResetDisplayedRows();
        UpdateDisplayedRows(firstSlot, CellsEstimatedHeight);
        _rowsPresenter?.InvalidateMeasure();
    }

    private bool CanRetargetDefaultVirtualRowsNow()
    {
        if (!UsesDefaultVirtualRowPipeline ||
            AreRowHeadersVisible ||
            ShowRowNumbers ||
            RowDetailsTemplate is not null)
        {
            return false;
        }

        if (HasLoadingRowHandlers())
        {
            return false;
        }

        using var unloadingRoute = BuildEventRoute(UnloadingRowEvent);
        return !unloadingRoute.HasHandlers;
    }

    private bool HasLoadingRowHandlers()
    {
        using var route = BuildEventRoute(LoadingRowEvent);
        return route.HasHandlers;
    }

    private bool HasUnloadingRowHandlers()
    {
        using var route = BuildEventRoute(UnloadingRowEvent);
        return route.HasHandlers;
    }

    internal bool CanRetargetDefaultVirtualRow(DataGridRow row, double rowHeight)
    {
        if (row.GetType() != typeof(DataGridRow) ||
            !IsRowRecyclable(row) ||
            row.IsKeyboardFocusWithin ||
            !MathUtilities.AreClose(row.DesiredSize.Height, rowHeight))
        {
            return false;
        }

        return true;
    }

    private bool CanRetargetDefaultFlatRowsNow(out double rowHeight)
    {
        rowHeight = double.NaN;
        if (VisualLayoutMode != DataGridVisualLayoutMode.Flat ||
            !UsesDefaultRealizationFactory ||
            GetType() != typeof(DataGrid) ||
            !double.IsFinite(RowHeight) ||
            AreRowHeadersVisible ||
            ShowRowNumbers ||
            RowDetailsTemplate is not null ||
            RowGroupHeadersTable.RangeCount != 0 ||
            RowGroupFootersTable.RangeCount != 0 ||
            !_collapsedSlotsTable.IsEmpty ||
            (_conditionalFormattingModel?.Descriptors.Count ?? 0) > 0 ||
            HasActiveSearchPresentation() ||
            HasLoadingRowHandlers() ||
            HasUnloadingRowHandlers() ||
            HasCellPreparedHandlers ||
            HasCellClearingHandlers)
        {
            return false;
        }

        for (int index = 0; index < ColumnsItemsInternal.Count; index++)
        {
            DataGridColumn column = ColumnsItemsInternal[index];
            if (!column.CanReuseCellContentOnDataContextChange ||
                column.UsesDrawnDisplay ||
                column.Width.IsAuto ||
                column.Width.IsSizeToCells)
            {
                return false;
            }
        }

        rowHeight = DataGridRow.GetFlatDesiredHeight(this, RowHeight);
        return double.IsFinite(rowHeight) && MathUtilities.GreaterThan(rowHeight, 0);
    }

    internal bool CanRetargetDefaultFlatRow(DataGridRow row, double rowHeight)
    {
        if (row.GetType() != typeof(DataGridRow) ||
            !IsRowRecyclable(row) ||
            row.IsKeyboardFocusWithin ||
            row.Slot == CurrentSlot ||
            row.Cells.Count != ColumnsItemsInternal.Count ||
            !MathUtilities.AreClose(row.DesiredSize.Height, rowHeight))
        {
            return false;
        }

        for (int index = 0; index < row.Cells.Count; index++)
        {
            if (!ReferenceEquals(row.Cells[index].OwningColumn, ColumnsItemsInternal[index]))
            {
                return false;
            }
        }

        return true;
    }

    internal void RetargetDefaultFlatRow(
        DataGridRow row,
        int slot,
        int rowIndex,
        object item)
    {
        row.ClearDragDropState();
        row.Index = rowIndex;
        row.Slot = slot;
        row.DataContext = item;
        row.IsPlaceholder = ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder);
        if (!row.IsValid)
        {
            row.IsValid = true;
        }
        if (row.ValidationSeverity != DataGridValidationSeverity.None)
        {
            row.ValidationSeverity = DataGridValidationSeverity.None;
        }
        if (item is INotifyDataErrorInfo)
        {
            RestoreRowValidationState(row, item);
        }
        row.UpdateFlatCellDataContexts();
        if (CurrentColumnIndex >= 0 &&
            CurrentColumnIndex < row.Cells.Count &&
            CurrentSlot == slot)
        {
            row.Cells[CurrentColumnIndex].UpdatePseudoClasses();
        }
        row.ApplyState();
    }

    internal void RetargetDefaultVirtualRow(
        DataGridRow row,
        int slot,
        int rowIndex,
        object item)
    {
        int previousSlot = row.Slot;
        bool wasCurrent = previousSlot == CurrentSlot;
        DataGridValidationSeverity previousValidationSeverity = row.ValidationSeverity;

        row.ClearDragDropState();
        row.Index = rowIndex;
        row.Slot = slot;
        row.DataContext = item;
        bool isPlaceholder = ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder);
        if (row.IsPlaceholder != isPlaceholder)
        {
            row.IsPlaceholder = isPlaceholder;
        }
        PrepareDefaultVirtualSurfaceRow(row, item, recordDiagnostics: false);
        bool isSelected = GetRowSelection(slot);
        bool isFullySelected = IsRowFullySelected(slot, isSelected);
        row.ApplyRetargetedVirtualSurfaceState(
            wasCurrent,
            previousValidationSeverity,
            isSelected,
            isFullySelected);
    }

    internal void InvalidateDefaultVirtualRowsChildIndexes()
    {
        _rowsPresenter?.InvalidateChildIndexes();
    }

    internal void MarkDefaultVirtualRowsRetargeted(
        int rowCount,
        double rowHeight,
        bool rowsRemainMeasureValid,
        bool rowsRemainArrangeValid)
    {
        if (_rowsPresenter is { } rowsPresenter)
        {
            rowsPresenter.MarkDefaultVirtualRowsRetargeted(
                rowCount,
                rowHeight,
                rowsRemainMeasureValid,
                rowsRemainArrangeValid);
        }
    }

    private bool CanDrawAllVisibleColumnsOnVirtualSurface()
    {
        if (!double.IsFinite(RowHeight) ||
            CellTheme is not null ||
            (_conditionalFormattingModel?.Descriptors.Count ?? 0) > 0 ||
            HasActiveSearchPresentation() ||
            HasCellPreparedHandlers ||
            HasCellClearingHandlers)
        {
            return false;
        }

        bool foundVisibleColumn = false;
        for (int index = 0; index < ColumnsItemsInternal.Count; index++)
        {
            DataGridColumn column = ColumnsItemsInternal[index];
            if (!column.IsVisible)
            {
                continue;
            }

            foundVisibleColumn = true;
            if (column.CellTheme is not null ||
                column.Width.IsAuto ||
                column.Width.IsSizeToCells ||
                !column.SupportsVirtualCellSurface)
            {
                return false;
            }
        }

        return foundVisibleColumn;
    }

    private bool HasActiveSearchPresentation() =>
        (_searchModel?.Descriptors.Count ?? 0) > 0 &&
        _searchModel!.HighlightMode != DataGridSearching.SearchHighlightMode.None;

    private void RefreshVirtualCellBackendAfterColumnsChanged()
    {
        if (VisualLayoutMode != DataGridVisualLayoutMode.Virtualized)
        {
            return;
        }

        foreach (DataGridRow row in GetAllRows())
        {
            CompleteCellsCollection(row);
        }

        _rowsPresenter?.ResetFlatVisualLayout();
        _rowsPresenter?.InvalidateMeasure();
        InvalidateMeasure();
    }

    internal void RefreshVirtualCellBackendIfEligibilityChanged()
    {
        if (VisualLayoutMode != DataGridVisualLayoutMode.Virtualized)
        {
            return;
        }

        bool surfaceAttached = _rowsPresenter?.VirtualSurfaceCount > 0;
        if (surfaceAttached != UsesVirtualCellSurface)
        {
            RefreshVirtualCellBackendAfterColumnsChanged();
        }
        else
        {
            _rowsPresenter?.InvalidateVirtualCellSurface();
        }
    }

    internal bool IsVirtualCompatibilityRow(DataGridRow row) =>
        UsesVirtualCellSurface && ReferenceEquals(_virtualCompatibilityRow, row);

    internal bool IsVirtualCompatibilityCell(DataGridRow row, DataGridColumn column) =>
        ReferenceEquals(EditingRow, row) && column.Index == CurrentColumnIndex;

    internal void EnsureVirtualCompatibilityRow(DataGridRow row)
    {
        if (!UsesVirtualCellSurface)
        {
            return;
        }

        if (!ReferenceEquals(_virtualCompatibilityRow, row))
        {
            ReleaseVirtualCompatibilityRow();
            _virtualCompatibilityRow = row;
        }

        CompleteCellsCollection(row);
        _rowsPresenter?.InvalidateMeasure();
    }

    private void ReleaseVirtualCompatibilityRow()
    {
        DataGridRow? row = _virtualCompatibilityRow;
        _virtualCompatibilityRow = null;
        if (row is null)
        {
            return;
        }

        CompleteCellsCollection(row);
        _rowsPresenter?.InvalidateMeasure();
    }

    private void OnVisualLayoutModeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is DataGridVisualLayoutMode oldMode &&
            e.NewValue is DataGridVisualLayoutMode newMode &&
            oldMode == newMode)
        {
            return;
        }

        if (!_measured)
        {
            _rowsPresenter?.ResetFlatVisualLayout();
            InvalidateMeasure();
            return;
        }

        _rowsPresenter?.ResetFlatVisualLayout();
        _virtualCompatibilityRow = null;
        UnloadElements(recycle: false);
        RefreshRowsAndColumns(clearRows: false);
        RefreshSelectionFromModel();
        InvalidateColumnHeadersMeasure();
        InvalidateMeasure();
    }
}
