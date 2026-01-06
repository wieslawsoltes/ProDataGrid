using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DataGridSample.Models;
using DataGridSample.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DataGridSample.Pages
{
    public partial class PowerFxSpreadsheetPage : UserControl
    {
        public PowerFxSpreadsheetPage()
        {
            InitializeComponent();
        }

        private void OnCurrentCellChanged(object? sender, DataGridCurrentCellChangedEventArgs e)
        {
            if (!TrySelectCell(e.NewItem, e.NewColumn))
            {
                UpdateCurrentCellSelection();
            }

            UpdateSelectionSummary();
        }

        private void OnSelectedCellsChanged(object? sender, DataGridSelectedCellsChangedEventArgs e)
        {
            UpdateSelectionSummary();

            if (PowerFxGrid == null)
            {
                return;
            }

            var currentCell = PowerFxGrid.CurrentCell;
            if (currentCell.IsValid)
            {
                TrySelectCell(currentCell);
                return;
            }

            var addedCell = e.AddedCells.FirstOrDefault(cell => cell.IsValid);
            if (!TrySelectCell(addedCell))
            {
                UpdateCurrentCellSelection();
            }
        }

        private void OnNameBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            ApplyNameBoxSelection();
            e.Handled = true;
        }

        private void OnNameBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            ApplyNameBoxSelection();
        }

        private void ApplyNameBoxSelection()
        {
            if (PowerFxGrid == null || DataContext is not PowerFxSpreadsheetViewModel viewModel)
            {
                return;
            }

            var rawText = viewModel.NameBoxText?.Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                viewModel.NameBoxText = viewModel.SelectionRange == "No selection"
                    ? string.Empty
                    : viewModel.SelectionRange;
                return;
            }

            if (viewModel.TryResolveNamedRange(rawText, out var resolvedRange))
            {
                rawText = resolvedRange;
            }

            if (!TryParseNameBoxRange(rawText, out var range))
            {
                viewModel.NameBoxText = viewModel.SelectionRange == "No selection"
                    ? string.Empty
                    : viewModel.SelectionRange;
                return;
            }

            if (viewModel.Rows.Count == 0 || PowerFxGrid.Columns.Count == 0)
            {
                return;
            }

            int startRowIndex;
            int endRowIndex;
            int startColIndex;
            int endColIndex;

            switch (range.Kind)
            {
                case NameBoxRangeKind.Column:
                    if (!TryResolveColumnIndex(range.StartColumnKey, out var startColumnIndex)
                        || !TryResolveColumnIndex(range.EndColumnKey, out var endColumnIndex))
                    {
                        viewModel.NameBoxText = viewModel.SelectionRange == "No selection"
                            ? string.Empty
                            : viewModel.SelectionRange;
                        return;
                    }

                    startRowIndex = 0;
                    endRowIndex = viewModel.Rows.Count - 1;
                    startColIndex = Math.Min(startColumnIndex, endColumnIndex);
                    endColIndex = Math.Max(startColumnIndex, endColumnIndex);
                    break;
                case NameBoxRangeKind.Row:
                    startRowIndex = Math.Clamp(Math.Min(range.StartRow, range.EndRow), 0, viewModel.Rows.Count - 1);
                    endRowIndex = Math.Clamp(Math.Max(range.StartRow, range.EndRow), 0, viewModel.Rows.Count - 1);
                    startColIndex = 0;
                    endColIndex = PowerFxGrid.Columns.Count - 1;
                    break;
                default:
                    if (!TryResolveColumnIndex(range.StartColumnKey, out var cellStartColumnIndex)
                        || !TryResolveColumnIndex(range.EndColumnKey, out var cellEndColumnIndex))
                    {
                        viewModel.NameBoxText = viewModel.SelectionRange == "No selection"
                            ? string.Empty
                            : viewModel.SelectionRange;
                        return;
                    }

                    startRowIndex = Math.Clamp(Math.Min(range.StartRow, range.EndRow), 0, viewModel.Rows.Count - 1);
                    endRowIndex = Math.Clamp(Math.Max(range.StartRow, range.EndRow), 0, viewModel.Rows.Count - 1);
                    startColIndex = Math.Min(cellStartColumnIndex, cellEndColumnIndex);
                    endColIndex = Math.Max(cellStartColumnIndex, cellEndColumnIndex);
                    break;
            }

            var selectedCells = new List<DataGridCellInfo>();
            for (var rowIndex = startRowIndex; rowIndex <= endRowIndex; rowIndex++)
            {
                var item = viewModel.Rows[rowIndex];
                for (var colIndex = startColIndex; colIndex <= endColIndex; colIndex++)
                {
                    if (colIndex < 0 || colIndex >= PowerFxGrid.Columns.Count)
                    {
                        continue;
                    }

                    var column = PowerFxGrid.Columns[colIndex];
                    selectedCells.Add(new DataGridCellInfo(item, column, rowIndex, colIndex, isValid: true));
                }
            }

            if (selectedCells.Count == 0)
            {
                return;
            }

            PowerFxGrid.SelectedCells = selectedCells;
            var currentItem = viewModel.Rows[startRowIndex];
            var currentColumn = PowerFxGrid.Columns[startColIndex];
            PowerFxGrid.CurrentCell = new DataGridCellInfo(currentItem, currentColumn, startRowIndex, startColIndex, isValid: true);
            PowerFxGrid.ScrollIntoView(currentItem, currentColumn);
            UpdateCurrentCellSelection();
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (PowerFxGrid == null || DataContext is not PowerFxSpreadsheetViewModel viewModel)
            {
                return;
            }

            var selectedCells = PowerFxGrid.SelectedCells?.Where(cell => cell.IsValid).ToList() ?? new List<DataGridCellInfo>();
            if (selectedCells.Count == 0)
            {
                viewModel.UpdateSelectionSummary("No selection", 0, 0, null, null);
                return;
            }

            var minRow = int.MaxValue;
            var maxRow = int.MinValue;
            var minColumn = int.MaxValue;
            var maxColumn = int.MinValue;
            var sum = 0d;
            var numericCount = 0;

            foreach (var cellInfo in selectedCells)
            {
                if (cellInfo.Item is not PowerFxSheetRow row)
                {
                    continue;
                }

                if (!TryResolveColumnKey(cellInfo.Column, cellInfo.ColumnIndex, out var columnKey))
                {
                    continue;
                }

                var columnIndex = ColumnKeyToIndex(columnKey);
                if (columnIndex <= 0)
                {
                    continue;
                }

                var cell = row.GetCell(columnKey);
                if (cell?.NumericValue is double number)
                {
                    sum += number;
                    numericCount++;
                }

                var rowIndex = cellInfo.RowIndex >= 0 ? cellInfo.RowIndex + 1 : 0;

                if (rowIndex > 0)
                {
                    minRow = Math.Min(minRow, rowIndex);
                    maxRow = Math.Max(maxRow, rowIndex);
                }

                if (columnIndex > 0)
                {
                    minColumn = Math.Min(minColumn, columnIndex);
                    maxColumn = Math.Max(maxColumn, columnIndex);
                }
            }

            var range = BuildRange(minColumn, minRow, maxColumn, maxRow, selectedCells.Count);
            var average = numericCount > 0 ? sum / numericCount : (double?)null;
            viewModel.UpdateSelectionSummary(range, selectedCells.Count, numericCount, numericCount > 0 ? sum : null, average);
        }

        private void UpdateCurrentCellSelection()
        {
            if (PowerFxGrid == null || DataContext is not PowerFxSpreadsheetViewModel viewModel)
            {
                return;
            }

            var currentCell = PowerFxGrid.CurrentCell;
            if (TrySelectCell(currentCell))
            {
                return;
            }

            var firstSelected = PowerFxGrid.SelectedCells.FirstOrDefault(cell => cell.IsValid);
            if (TrySelectCell(firstSelected))
            {
                return;
            }

            viewModel.ClearSelection();
        }

        private static int ColumnKeyToIndex(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return -1;
            }

            var upper = char.ToUpperInvariant(key[0]);
            return upper is >= 'A' and <= 'Z' ? upper - 'A' + 1 : -1;
        }

        private static string ColumnIndexToKey(int index)
        {
            if (index <= 0 || index > 26)
            {
                return "?";
            }

            return ((char)('A' + index - 1)).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildRange(int minColumn, int minRow, int maxColumn, int maxRow, int count)
        {
            if (minColumn <= 0 || minRow <= 0 || maxColumn <= 0 || maxRow <= 0)
            {
                return count > 0 ? "Selection" : "No selection";
            }

            var start = $"{ColumnIndexToKey(minColumn)}{minRow}";
            var end = $"{ColumnIndexToKey(maxColumn)}{maxRow}";

            return count == 1 || start == end ? start : $"{start}:{end}";
        }

        private bool TryResolveColumnIndex(string columnKey, out int columnIndex)
        {
            columnIndex = -1;

            if (PowerFxGrid == null || string.IsNullOrWhiteSpace(columnKey))
            {
                return false;
            }

            for (var i = 0; i < PowerFxGrid.Columns.Count; i++)
            {
                if (PowerFxGrid.Columns[i].Tag is string tag &&
                    string.Equals(tag, columnKey, StringComparison.OrdinalIgnoreCase))
                {
                    columnIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseNameBoxRange(string text, out NameBoxRange range)
        {
            range = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Length > 2)
            {
                return false;
            }

            var first = parts[0].Trim();
            var second = parts.Length == 2 ? parts[1].Trim() : string.Empty;

            if (parts.Length == 1)
            {
                if (TryParseCellAddress(first, out var rowIndex, out var columnKey))
                {
                    range = NameBoxRange.Cell(rowIndex, rowIndex, columnKey, columnKey);
                    return true;
                }

                if (IsLettersOnly(first))
                {
                    var key = first.ToUpperInvariant();
                    range = NameBoxRange.Column(key, key);
                    return true;
                }

                if (TryParseRowIndex(first, out var rowOnlyIndex))
                {
                    range = NameBoxRange.Row(rowOnlyIndex, rowOnlyIndex);
                    return true;
                }

                return false;
            }

            if (IsLettersOnly(first) && IsLettersOnly(second))
            {
                range = NameBoxRange.Column(first.ToUpperInvariant(), second.ToUpperInvariant());
                return true;
            }

            if (TryParseRowIndex(first, out var startRowIndex) && TryParseRowIndex(second, out var endRowIndex))
            {
                range = NameBoxRange.Row(startRowIndex, endRowIndex);
                return true;
            }

            if (TryParseCellAddress(first, out var startRow, out var startColumnKey)
                && TryParseCellAddress(second, out var endRow, out var endColumnKey))
            {
                range = NameBoxRange.Cell(startRow, endRow, startColumnKey, endColumnKey);
                return true;
            }

            return false;
        }

        private static bool TryParseCellAddress(string text, out int rowIndex, out string columnKey)
        {
            rowIndex = -1;
            columnKey = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            var letterCount = 0;
            while (letterCount < trimmed.Length && char.IsLetter(trimmed[letterCount]))
            {
                letterCount++;
            }

            if (letterCount == 0 || letterCount == trimmed.Length)
            {
                return false;
            }

            columnKey = trimmed.Substring(0, letterCount).ToUpperInvariant();
            var numberPart = trimmed.Substring(letterCount);

            return TryParseRowIndex(numberPart, out rowIndex);
        }

        private static bool TryParseRowIndex(string text, out int rowIndex)
        {
            rowIndex = -1;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowNumber))
            {
                return false;
            }

            if (rowNumber <= 0)
            {
                return false;
            }

            rowIndex = rowNumber - 1;
            return true;
        }

        private static bool IsLettersOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (var ch in text)
            {
                if (!char.IsLetter(ch))
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct NameBoxRange
        {
            private NameBoxRange(NameBoxRangeKind kind, int startRow, int endRow, string startColumnKey, string endColumnKey)
            {
                Kind = kind;
                StartRow = startRow;
                EndRow = endRow;
                StartColumnKey = startColumnKey;
                EndColumnKey = endColumnKey;
            }

            public NameBoxRangeKind Kind { get; }

            public int StartRow { get; }

            public int EndRow { get; }

            public string StartColumnKey { get; }

            public string EndColumnKey { get; }

            public static NameBoxRange Cell(int startRow, int endRow, string startColumnKey, string endColumnKey)
            {
                return new NameBoxRange(NameBoxRangeKind.Cell, startRow, endRow, startColumnKey, endColumnKey);
            }

            public static NameBoxRange Column(string startColumnKey, string endColumnKey)
            {
                return new NameBoxRange(NameBoxRangeKind.Column, -1, -1, startColumnKey, endColumnKey);
            }

            public static NameBoxRange Row(int startRow, int endRow)
            {
                return new NameBoxRange(NameBoxRangeKind.Row, startRow, endRow, string.Empty, string.Empty);
            }
        }

        private enum NameBoxRangeKind
        {
            Cell,
            Column,
            Row
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private bool TrySelectCell(DataGridCellInfo cellInfo)
        {
            if (!cellInfo.IsValid)
            {
                return false;
            }

            return TrySelectCell(cellInfo.Item, cellInfo.Column, cellInfo.ColumnIndex);
        }

        private bool TrySelectCell(object? item, DataGridColumn? column)
        {
            if (PowerFxGrid == null)
            {
                return false;
            }

            var columnIndex = column != null ? PowerFxGrid.Columns.IndexOf(column) : -1;
            return TrySelectCell(item, column, columnIndex);
        }

        private bool TrySelectCell(object? item, DataGridColumn? column, int columnIndex)
        {
            if (PowerFxGrid == null || DataContext is not PowerFxSpreadsheetViewModel viewModel)
            {
                return false;
            }

            if (item is not PowerFxSheetRow row)
            {
                return false;
            }

            if (!TryResolveColumnKey(column, columnIndex, out var columnKey))
            {
                return false;
            }

            viewModel.SelectCell(row, columnKey);
            return true;
        }

        private bool TryResolveColumnKey(DataGridColumn? column, int columnIndex, out string columnKey)
        {
            columnKey = string.Empty;

            if (column?.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                columnKey = tag;
                return true;
            }

            if (columnIndex >= 0)
            {
                var fallbackKey = ColumnIndexToKey(columnIndex + 1);
                if (fallbackKey != "?")
                {
                    columnKey = fallbackKey;
                    return true;
                }
            }

            return false;
        }
    }
}
