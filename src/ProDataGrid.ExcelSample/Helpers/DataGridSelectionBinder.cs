using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using ProDataGrid.ExcelSample.Models;

namespace ProDataGrid.ExcelSample.Helpers;

public sealed class DataGridSelectionBinder
{
    public static readonly AttachedProperty<SpreadsheetSelectionState?> SelectionStateProperty =
        AvaloniaProperty.RegisterAttached<DataGridSelectionBinder, DataGrid, SpreadsheetSelectionState?>(
            "SelectionState");

    private static readonly ConditionalWeakTable<DataGrid, SelectionSubscription> Subscriptions = new();

    static DataGridSelectionBinder()
    {
        SelectionStateProperty.Changed.AddClassHandler<DataGrid>(OnSelectionStateChanged);
    }

    public static void SetSelectionState(AvaloniaObject element, SpreadsheetSelectionState? value)
    {
        element.SetValue(SelectionStateProperty, value);
    }

    public static SpreadsheetSelectionState? GetSelectionState(AvaloniaObject element)
    {
        return element.GetValue(SelectionStateProperty);
    }

    private static void OnSelectionStateChanged(DataGrid grid, AvaloniaPropertyChangedEventArgs args)
    {
        var subscription = Subscriptions.GetOrCreateValue(grid);
        subscription.Detach();
        subscription.Attach(grid, args.NewValue as SpreadsheetSelectionState);
    }

    private sealed class SelectionSubscription
    {
        private DataGrid? _grid;
        private SpreadsheetSelectionState? _state;
        private bool _isUpdating;
        private DataGridCellInfo? _lastSelectedCell;

        public void Attach(DataGrid grid, SpreadsheetSelectionState? state)
        {
            _grid = grid;
            _state = state;
            if (_grid == null || _state == null)
            {
                return;
            }

            _grid.CurrentCellChanged += GridOnCurrentCellChanged;
            _grid.SelectedCellsChanged += GridOnSelectedCellsChanged;
            _state.PropertyChanged += StateOnPropertyChanged;

            if (_state.CurrentCell.HasValue || _state.SelectedRange.HasValue)
            {
                UpdateGridSelection();
            }
            else
            {
                UpdateState();
            }
        }

        public void Detach()
        {
            if (_grid != null)
            {
                _grid.CurrentCellChanged -= GridOnCurrentCellChanged;
                _grid.SelectedCellsChanged -= GridOnSelectedCellsChanged;
            }

            if (_state != null)
            {
                _state.PropertyChanged -= StateOnPropertyChanged;
            }

            _grid = null;
            _state = null;
            _isUpdating = false;
        }

        private void GridOnCurrentCellChanged(object? sender, DataGridCurrentCellChangedEventArgs e)
        {
            UpdateState();
        }

        private void GridOnSelectedCellsChanged(object? sender, DataGridSelectedCellsChangedEventArgs e)
        {
            if (e.AddedCells.Count > 0)
            {
                for (var i = e.AddedCells.Count - 1; i >= 0; i--)
                {
                    var cell = e.AddedCells[i];
                    if (cell.IsValid)
                    {
                        _lastSelectedCell = cell;
                        break;
                    }
                }
            }

            UpdateState();
        }

        private void StateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SpreadsheetSelectionState.CurrentCell) &&
                e.PropertyName != nameof(SpreadsheetSelectionState.SelectedRange))
            {
                return;
            }

            UpdateGridSelection();
        }

        private void UpdateState()
        {
            if (_grid == null || _state == null || _isUpdating)
            {
                return;
            }

            _isUpdating = true;
            var syncGrid = false;
            SpreadsheetCellReference? currentReference = null;
            try
            {
                var current = _grid.CurrentCell;
                currentReference = current.IsValid && TryResolveCellReference(current, out var resolvedCurrent)
                    ? resolvedCurrent
                    : (SpreadsheetCellReference?)null;

                var selected = _grid.SelectedCells;
                if (selected == null || selected.Count == 0)
                {
                    if (!currentReference.HasValue && _lastSelectedCell.HasValue &&
                        TryResolveCellReference(_lastSelectedCell.Value, out var fallbackReference))
                    {
                        currentReference = fallbackReference;
                    }

                    _state.CurrentCell = currentReference;
                    _state.SelectedRange = currentReference.HasValue
                        ? new SpreadsheetCellRange(currentReference.Value, currentReference.Value)
                        : null;
                    return;
                }

                var minRow = int.MaxValue;
                var maxRow = int.MinValue;
                var minColumn = int.MaxValue;
                var maxColumn = int.MinValue;
                var selectedCount = 0;
                SpreadsheetCellReference? singleSelection = null;

                for (var i = 0; i < selected.Count; i++)
                {
                    var cell = selected[i];
                    if (!cell.IsValid || !TryResolveCellReference(cell, out var reference))
                    {
                        continue;
                    }

                    selectedCount++;
                    if (selectedCount == 1)
                    {
                        singleSelection = reference;
                    }

                    minRow = Math.Min(minRow, reference.RowIndex);
                    maxRow = Math.Max(maxRow, reference.RowIndex);
                    minColumn = Math.Min(minColumn, reference.ColumnIndex);
                    maxColumn = Math.Max(maxColumn, reference.ColumnIndex);
                }

                if (minRow == int.MaxValue)
                {
                    _state.CurrentCell = currentReference;
                    _state.SelectedRange = null;
                    return;
                }

                var start = new SpreadsheetCellReference(minRow, minColumn);
                var end = new SpreadsheetCellReference(maxRow, maxColumn);
                var range = new SpreadsheetCellRange(start, end);

                if (_lastSelectedCell.HasValue &&
                    TryResolveCellReference(_lastSelectedCell.Value, out var lastReference) &&
                    IsWithinRange(range, lastReference) &&
                    (!currentReference.HasValue || !currentReference.Value.Equals(lastReference)))
                {
                    currentReference = lastReference;
                    syncGrid = true;
                }
                else if (selectedCount == 1 && singleSelection.HasValue &&
                         (!currentReference.HasValue || !currentReference.Value.Equals(singleSelection.Value)))
                {
                    currentReference = singleSelection;
                    syncGrid = true;
                }

                if (!currentReference.HasValue || !IsWithinRange(range, currentReference.Value))
                {
                    currentReference = range.Start;
                    syncGrid = true;
                }

                _state.CurrentCell = currentReference;
                _state.SelectedRange = range;
            }
            finally
            {
                _isUpdating = false;
            }

            if (syncGrid && currentReference.HasValue)
            {
                UpdateGridCurrentCell(currentReference.Value);
            }
        }

        private void UpdateGridSelection()
        {
            if (_grid == null || _state == null || _isUpdating)
            {
                return;
            }

            var range = _state.SelectedRange ?? (_state.CurrentCell.HasValue
                ? new SpreadsheetCellRange(_state.CurrentCell.Value, _state.CurrentCell.Value)
                : (SpreadsheetCellRange?)null);

            if (!range.HasValue)
            {
                _isUpdating = true;
                try
                {
                    _grid.SelectedCells.Clear();
                    _grid.CurrentCell = DataGridCellInfo.Unset;
                }
                finally
                {
                    _isUpdating = false;
                }

                return;
            }

            if (_grid.ItemsSource is not IList items)
            {
                return;
            }

            var columnCount = _grid.Columns.Count;
            var rowCount = items.Count;
            if (columnCount == 0 || rowCount == 0)
            {
                return;
            }

            var startRow = range.Value.Start.RowIndex;
            var endRow = range.Value.End.RowIndex;
            var startColumn = range.Value.Start.ColumnIndex;
            var endColumn = range.Value.End.ColumnIndex;

            var columnMap = BuildColumnIndexMap(_grid);
            var rowMap = BuildRowIndexMap(items);

            _isUpdating = true;
            try
            {
                var selectedCells = _grid.SelectedCells;
                selectedCells.Clear();

                for (var rowIndex = startRow; rowIndex <= endRow; rowIndex++)
                {
                    if (!TryResolveRowItem(items, rowMap, rowIndex, out var item, out var viewRowIndex))
                    {
                        continue;
                    }

                    for (var columnIndex = startColumn; columnIndex <= endColumn; columnIndex++)
                    {
                        if (!TryResolveColumn(_grid, columnMap, columnIndex, out var column, out var viewColumnIndex))
                        {
                            continue;
                        }

                        selectedCells.Add(new DataGridCellInfo(item, column, viewRowIndex, viewColumnIndex, isValid: true));
                    }
                }

                var currentReference = _state.CurrentCell ?? range.Value.Start;
                if (TryResolveRowItem(items, rowMap, currentReference.RowIndex, out var currentItem, out var currentViewRow) &&
                    TryResolveColumn(_grid, columnMap, currentReference.ColumnIndex, out var currentColumn, out var currentViewColumn))
                {
                    _grid.CurrentCell = new DataGridCellInfo(currentItem, currentColumn, currentViewRow, currentViewColumn, isValid: true);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateGridCurrentCell(SpreadsheetCellReference current)
        {
            if (_grid == null || _isUpdating)
            {
                return;
            }

            if (_grid.ItemsSource is not IList items)
            {
                return;
            }

            var columnMap = BuildColumnIndexMap(_grid);
            var rowMap = BuildRowIndexMap(items);
            if (!TryResolveRowItem(items, rowMap, current.RowIndex, out var currentItem, out var currentViewRow) ||
                !TryResolveColumn(_grid, columnMap, current.ColumnIndex, out var currentColumn, out var currentViewColumn))
            {
                return;
            }

            _isUpdating = true;
            try
            {
                _grid.CurrentCell = new DataGridCellInfo(currentItem, currentColumn, currentViewRow, currentViewColumn, isValid: true);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static bool TryResolveCellReference(DataGridCellInfo cell, out SpreadsheetCellReference reference)
        {
            var rowIndex = cell.RowIndex;
            if (cell.Item is SpreadsheetRow row)
            {
                rowIndex = Math.Max(0, row.RowIndex - 1);
            }

            var columnIndex = cell.ColumnIndex;
            if (cell.Column?.Header is string header && ExcelColumnName.TryParseIndex(header, out var parsed))
            {
                columnIndex = parsed;
            }

            if (rowIndex < 0 || columnIndex < 0)
            {
                reference = default;
                return false;
            }

            reference = new SpreadsheetCellReference(rowIndex, columnIndex);
            return true;
        }

        private static bool IsWithinRange(SpreadsheetCellRange range, SpreadsheetCellReference cell)
        {
            return cell.RowIndex >= range.Start.RowIndex &&
                   cell.RowIndex <= range.End.RowIndex &&
                   cell.ColumnIndex >= range.Start.ColumnIndex &&
                   cell.ColumnIndex <= range.End.ColumnIndex;
        }

        private static Dictionary<int, (DataGridColumn Column, int ViewIndex)> BuildColumnIndexMap(DataGrid grid)
        {
            var map = new Dictionary<int, (DataGridColumn Column, int ViewIndex)>();
            for (var i = 0; i < grid.Columns.Count; i++)
            {
                var column = grid.Columns[i];
                if (column?.Header is string header && ExcelColumnName.TryParseIndex(header, out var columnIndex))
                {
                    map[columnIndex] = (column, i);
                }
            }

            return map;
        }

        private static Dictionary<int, (object Item, int ViewIndex)> BuildRowIndexMap(IList items)
        {
            var map = new Dictionary<int, (object Item, int ViewIndex)>();
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is SpreadsheetRow row)
                {
                    var rowIndex = row.RowIndex - 1;
                    if (rowIndex >= 0)
                    {
                        map[rowIndex] = (row, i);
                    }
                }
            }

            return map;
        }

        private static bool TryResolveRowItem(IList items, Dictionary<int, (object Item, int ViewIndex)> rowMap, int rowIndex, out object item, out int viewRowIndex)
        {
            if (rowMap.TryGetValue(rowIndex, out var entry))
            {
                item = entry.Item;
                viewRowIndex = entry.ViewIndex;
                return true;
            }

            if ((uint)rowIndex < (uint)items.Count)
            {
                var entryItem = items[rowIndex];
                if (entryItem == null)
                {
                    item = null!;
                    viewRowIndex = -1;
                    return false;
                }

                item = entryItem;
                viewRowIndex = rowIndex;
                return true;
            }

            item = null!;
            viewRowIndex = -1;
            return false;
        }

        private static bool TryResolveColumn(DataGrid grid, Dictionary<int, (DataGridColumn Column, int ViewIndex)> columnMap, int columnIndex, out DataGridColumn column, out int viewColumnIndex)
        {
            if (columnMap.TryGetValue(columnIndex, out var entry))
            {
                column = entry.Column;
                viewColumnIndex = entry.ViewIndex;
                return true;
            }

            if ((uint)columnIndex < (uint)grid.Columns.Count)
            {
                column = grid.Columns[columnIndex];
                viewColumnIndex = columnIndex;
                return true;
            }

            column = null!;
            viewColumnIndex = -1;
            return false;
        }
    }
}
