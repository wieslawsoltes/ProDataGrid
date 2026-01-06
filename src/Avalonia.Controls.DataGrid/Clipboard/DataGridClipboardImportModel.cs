// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;

namespace Avalonia.Controls.DataGridClipboard
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridClipboardImportContext
    {
        private readonly DataGrid _grid;
        private readonly IReadOnlyList<DataGridCellInfo> _selectedCells;

        public DataGridClipboardImportContext(DataGrid grid, string text, IReadOnlyList<DataGridCellInfo> selectedCells)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Text = text ?? string.Empty;
            _selectedCells = selectedCells ?? Array.Empty<DataGridCellInfo>();
        }

        public DataGrid Grid => _grid;

        public string Text { get; }

        public IReadOnlyList<DataGridCellInfo> SelectedCells => _selectedCells;

        public int RowCount => _grid.DataConnection?.Count ?? 0;

        public int ColumnCount => _grid.ColumnsItemsInternal?.Count ?? 0;

        public int CurrentRowIndex => _grid.RowIndexFromSlot(_grid.CurrentSlot);

        public int CurrentColumnIndex => _grid.CurrentColumnIndex;

        public DataGridSelectionUnit SelectionUnit => _grid.SelectionUnit;

        public bool IsReadOnly => _grid.IsReadOnly;

        public bool AllowConverterFallback => _grid.AllowPasteConverterFallback;

        public bool TryGetColumn(int columnIndex, out DataGridColumn column)
        {
            column = null;

            var columns = _grid.ColumnsItemsInternal;
            if (columns == null || columnIndex < 0 || columnIndex >= columns.Count)
            {
                return false;
            }

            column = columns[columnIndex];
            return column != null;
        }

        public bool TryGetRowItem(int rowIndex, out object item)
        {
            item = null;

            var connection = _grid.DataConnection;
            if (connection == null || rowIndex < 0 || rowIndex >= connection.Count)
            {
                return false;
            }

            item = connection.GetDataItem(rowIndex);
            if (item == null || ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
            {
                return false;
            }

            return true;
        }

        public IDisposable BeginRowEdit(int rowIndex, out object item)
        {
            item = null;

            if (!TryGetRowItem(rowIndex, out item))
            {
                return NoopDisposable.Instance;
            }

            _grid.DataConnection.BeginEdit(item);
            return new RowEditScope(_grid, item);
        }

        public bool TrySetCellText(int rowIndex, int columnIndex, string text)
        {
            if (!TryGetRowItem(rowIndex, out var item))
            {
                return false;
            }

            return TrySetCellText(item, columnIndex, text);
        }

        public bool TrySetCellText(object item, int columnIndex, string text)
        {
            return _grid.TrySetCellValue(item, columnIndex, text);
        }

        private sealed class RowEditScope : IDisposable
        {
            private DataGrid _grid;
            private object _item;

            public RowEditScope(DataGrid grid, object item)
            {
                _grid = grid;
                _item = item;
            }

            public void Dispose()
            {
                var grid = _grid;
                var item = _item;
                if (grid == null || item == null)
                {
                    return;
                }

                _grid = null;
                _item = null;
                grid.DataConnection.EndEdit(item);
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();

            public void Dispose()
            {
            }
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridClipboardImportModel
    {
        bool Paste(DataGridClipboardImportContext context);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridClipboardImportModelFactory
    {
        IDataGridClipboardImportModel Create();
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridClipboardImportModel : IDataGridClipboardImportModel
    {
        public virtual bool Paste(DataGridClipboardImportContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.IsReadOnly || context.RowCount == 0 || context.ColumnCount == 0)
            {
                return false;
            }

            if (!context.Grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
            {
                return false;
            }

            var rows = ParseClipboardText(context.Text);
            if (rows.Count == 0)
            {
                return false;
            }

            if (rows.Count == 1 && rows[0].Count == 1 &&
                context.SelectionUnit != DataGridSelectionUnit.FullRow &&
                context.SelectedCells.Count > 1)
            {
                return TryPasteSingleValueToSelection(context, rows);
            }

            if (!TryGetPasteAnchor(context, out var startRow, out var startColumn))
            {
                return false;
            }

            if (TryPasteSingleValueToSelection(context, rows))
            {
                return true;
            }

            return TryPasteTable(context, rows, startRow, startColumn);
        }

        protected virtual bool TryPasteSingleValueToSelection(
            DataGridClipboardImportContext context,
            IReadOnlyList<List<string>> rows)
        {
            if (rows.Count != 1 || rows[0].Count != 1)
            {
                return false;
            }

            var selectedCells = context.SelectedCells;
            if (selectedCells.Count <= 1)
            {
                return false;
            }

            var value = rows[0][0];
            var applied = false;
            foreach (var rowGroup in selectedCells
                .Where(c => c.IsValid)
                .GroupBy(c => c.RowIndex)
                .OrderBy(g => g.Key))
            {
                using var editScope = context.BeginRowEdit(rowGroup.Key, out var item);
                if (item == null)
                {
                    continue;
                }

                foreach (var cell in rowGroup)
                {
                    if (context.TrySetCellText(item, cell.ColumnIndex, value))
                    {
                        applied = true;
                    }
                }
            }

            return applied;
        }

        protected virtual bool TryPasteTable(
            DataGridClipboardImportContext context,
            IReadOnlyList<List<string>> rows,
            int startRow,
            int startColumn)
        {
            for (var rowOffset = 0; rowOffset < rows.Count; rowOffset++)
            {
                var rowIndex = startRow + rowOffset;
                if (rowIndex < 0 || rowIndex >= context.RowCount)
                {
                    break;
                }

                using var editScope = context.BeginRowEdit(rowIndex, out var item);
                if (item == null)
                {
                    continue;
                }

                var rowValues = rows[rowOffset];
                for (var colOffset = 0; colOffset < rowValues.Count; colOffset++)
                {
                    var columnIndex = startColumn + colOffset;
                    if (columnIndex < 0 || columnIndex >= context.ColumnCount)
                    {
                        break;
                    }

                    context.TrySetCellText(item, columnIndex, rowValues[colOffset]);
                }
            }

            return true;
        }

        protected virtual bool TryGetPasteAnchor(DataGridClipboardImportContext context, out int rowIndex, out int columnIndex)
        {
            rowIndex = -1;
            columnIndex = -1;

            var selectedCells = context.SelectedCells;
            if (context.SelectionUnit != DataGridSelectionUnit.FullRow && selectedCells.Count > 0)
            {
                rowIndex = selectedCells.Min(c => c.RowIndex);
                columnIndex = selectedCells.Min(c => c.ColumnIndex);
                return rowIndex >= 0 && columnIndex >= 0;
            }

            rowIndex = context.CurrentRowIndex;
            columnIndex = context.CurrentColumnIndex;
            return rowIndex >= 0 && columnIndex >= 0;
        }

        protected virtual List<List<string>> ParseClipboardText(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && lines[i].Length == 0)
                {
                    continue;
                }

                rows.Add(lines[i].Split('\t').ToList());
            }

            return rows;
        }
    }
}
