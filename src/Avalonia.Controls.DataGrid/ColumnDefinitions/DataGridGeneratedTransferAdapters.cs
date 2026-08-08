// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls.DataGridClipboard;
using Avalonia.Controls.DataGridFilling;

namespace Avalonia.Controls
{
    /// <summary>
    /// Adapts a generated typed clipboard controller to the DataGrid paste pipeline without property paths or reflection.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable row key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedClipboardImportModel<TItem, TKey> : IDataGridClipboardImportModel
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly DataGridGeneratedEditController<TItem, TKey> _edits;
        private readonly DataGridGeneratedClipboardController<TItem, TKey> _clipboard;
        private readonly Action<DataGridGeneratedTransferResult<TKey>> _resultHandler;
        private readonly IFormatProvider _formatProvider;
        private readonly DataGridGeneratedTransferLimits _limits;

        /// <summary>Initializes a generated clipboard import adapter.</summary>
        public DataGridGeneratedClipboardImportModel(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            DataGridGeneratedEditController<TItem, TKey> edits,
            Action<DataGridGeneratedTransferResult<TKey>> resultHandler = null,
            IFormatProvider formatProvider = null,
            DataGridGeneratedTransferLimits? limits = null)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _edits = edits ?? throw new ArgumentNullException(nameof(edits));
            _clipboard = new DataGridGeneratedClipboardController<TItem, TKey>(_keyAccessor, _edits);
            _resultHandler = resultHandler;
            _formatProvider = formatProvider ?? CultureInfo.CurrentCulture;
            _limits = limits ?? DataGridGeneratedTransferLimits.Default;
        }

        /// <inheritdoc />
        public bool Paste(DataGridClipboardImportContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.IsReadOnly || context.RowCount == 0 || context.ColumnCount == 0 ||
                context.Text.Length > _limits.MaximumCharacters ||
                !context.Grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true))
            {
                return false;
            }

            if (IsSingleValue(context.Text) &&
                context.SelectionUnit != DataGridSelectionUnit.FullRow &&
                context.SelectedCells.Count > 1)
            {
                return PasteSingleValue(context);
            }

            if (!TryGetPasteAnchor(context, out int startRow, out int startColumn))
            {
                return false;
            }

            var rows = new List<TItem>(Math.Max(0, context.RowCount - startRow));
            for (int rowIndex = startRow; rowIndex < context.RowCount; rowIndex++)
            {
                if (!context.TryGetRowItem(rowIndex, out object item) || item is not TItem typed)
                {
                    break;
                }
                rows.Add(typed);
            }

            var columnKeys = new List<string>(Math.Max(0, context.ColumnCount - startColumn));
            for (int columnIndex = startColumn; columnIndex < context.ColumnCount; columnIndex++)
            {
                if (!context.TryGetColumn(columnIndex, out DataGridColumn column) ||
                    column?.ColumnKey is not string columnKey ||
                    !_edits.Fields.ContainsKey(columnKey))
                {
                    break;
                }
                columnKeys.Add(columnKey);
            }

            if (rows.Count == 0 || columnKeys.Count == 0)
            {
                return false;
            }

            DataGridGeneratedTransferResult<TKey> result = _clipboard.PasteDelimited(
                rows,
                columnKeys,
                context.Text.AsSpan(),
                '\t',
                _formatProvider,
                _limits);
            _resultHandler?.Invoke(result);
            return result.AppliedCells != 0 || result.Errors.Count != 0 || result.Truncated;
        }

        private bool PasteSingleValue(DataGridClipboardImportContext context)
        {
            var errors = new List<DataGridGeneratedTransferError<TKey>>();
            int applied = 0;
            int visited = 0;
            bool truncated = false;
            _edits.BeginBatch();
            try
            {
                for (int index = 0; index < context.SelectedCells.Count; index++)
                {
                    if (visited >= _limits.MaximumCells)
                    {
                        truncated = true;
                        break;
                    }
                    visited++;
                    DataGridCellInfo cell = context.SelectedCells[index];
                    if (!cell.IsValid || !context.TryGetRowItem(cell.RowIndex, out object item) || item is not TItem typed ||
                        !context.TryGetColumn(cell.ColumnIndex, out DataGridColumn column) || column?.ColumnKey is not string columnKey ||
                        !_edits.Fields.ContainsKey(columnKey))
                    {
                        continue;
                    }

                    DataGridGeneratedEditResult edit = _edits.TrySetText(typed, columnKey, context.Text.AsSpan(), _formatProvider);
                    if (edit.IsApplied)
                    {
                        applied++;
                    }
                    else
                    {
                        errors.Add(new DataGridGeneratedTransferError<TKey>(
                            _keyAccessor.GetKey(typed), columnKey, cell.RowIndex, cell.ColumnIndex, edit));
                    }
                }
                _edits.CommitBatch();
            }
            catch
            {
                _edits.RollbackBatch();
                throw;
            }

            var result = new DataGridGeneratedTransferResult<TKey>(
                applied,
                truncated,
                errors);
            _resultHandler?.Invoke(result);
            return applied != 0 || errors.Count != 0 || result.Truncated;
        }

        private static bool TryGetPasteAnchor(DataGridClipboardImportContext context, out int rowIndex, out int columnIndex)
        {
            rowIndex = int.MaxValue;
            columnIndex = int.MaxValue;
            if (context.SelectionUnit != DataGridSelectionUnit.FullRow && context.SelectedCells.Count != 0)
            {
                for (int index = 0; index < context.SelectedCells.Count; index++)
                {
                    DataGridCellInfo cell = context.SelectedCells[index];
                    if (!cell.IsValid)
                    {
                        continue;
                    }
                    rowIndex = Math.Min(rowIndex, cell.RowIndex);
                    columnIndex = Math.Min(columnIndex, cell.ColumnIndex);
                }
                if (rowIndex != int.MaxValue && columnIndex != int.MaxValue)
                {
                    return true;
                }
            }

            rowIndex = context.CurrentRowIndex;
            columnIndex = context.CurrentColumnIndex;
            return rowIndex >= 0 && columnIndex >= 0;
        }

        private static bool IsSingleValue(string text) =>
            text.IndexOf('\t') < 0 && text.IndexOf('\r') < 0 && text.IndexOf('\n') < 0;
    }

    /// <summary>
    /// Adapts generated typed edit fields to the DataGrid fill handle with copy and numeric/date sequence support.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable row key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedFillModel<TItem, TKey> : IDataGridFillModel
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly DataGridGeneratedEditController<TItem, TKey> _edits;
        private readonly Action<DataGridGeneratedTransferResult<TKey>> _resultHandler;

        /// <summary>Initializes a generated fill-handle adapter.</summary>
        public DataGridGeneratedFillModel(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            DataGridGeneratedEditController<TItem, TKey> edits,
            Action<DataGridGeneratedTransferResult<TKey>> resultHandler = null,
            int maximumCells = 100000,
            bool useSeries = true)
        {
            if (maximumCells <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCells));
            }
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _edits = edits ?? throw new ArgumentNullException(nameof(edits));
            _resultHandler = resultHandler;
            MaximumCells = maximumCells;
            UseSeries = useSeries;
        }

        /// <summary>Gets the maximum destination cells handled by one fill operation.</summary>
        public int MaximumCells { get; }

        /// <summary>Gets whether two numeric/date source values are extrapolated as a sequence.</summary>
        public bool UseSeries { get; }

        /// <inheritdoc />
        public void ApplyFill(DataGridFillContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.Grid.IsReadOnly || context.SourceRange == context.TargetRange ||
                context.Direction == DataGridFillDirection.None)
            {
                return;
            }

            DataGridCellRange source = context.SourceRange;
            DataGridCellRange target = context.TargetRange;
            var errors = new List<DataGridGeneratedTransferError<TKey>>();
            int applied = 0;
            int visited = 0;
            bool truncated = false;

            _edits.BeginBatch();
            try
            {
                for (int rowIndex = target.StartRow; rowIndex <= target.EndRow && !truncated; rowIndex++)
                {
                    if (!TryGetRow(context, rowIndex, out TItem targetItem))
                    {
                        continue;
                    }
                    for (int columnIndex = target.StartColumn; columnIndex <= target.EndColumn; columnIndex++)
                    {
                        if (Contains(source, rowIndex, columnIndex))
                        {
                            continue;
                        }
                        if (visited >= MaximumCells)
                        {
                            truncated = true;
                            break;
                        }
                        if (!TryGetField(context.Grid, columnIndex, out string targetKey, out _))
                        {
                            continue;
                        }

                        visited++;
                        object value;
                        if (!TryGetSeriesValue(context, source, rowIndex, columnIndex, out value) &&
                            !TryGetCopiedValue(context, source, rowIndex, columnIndex, out value))
                        {
                            continue;
                        }

                        DataGridGeneratedEditResult edit = _edits.TrySetValue(targetItem, targetKey, value);
                        if (edit.IsApplied)
                        {
                            applied++;
                        }
                        else
                        {
                            errors.Add(new DataGridGeneratedTransferError<TKey>(
                                _keyAccessor.GetKey(targetItem), targetKey, rowIndex, columnIndex, edit));
                        }
                    }
                }
                _edits.CommitBatch();
            }
            catch
            {
                _edits.RollbackBatch();
                throw;
            }

            _resultHandler?.Invoke(new DataGridGeneratedTransferResult<TKey>(applied, truncated, errors));
        }

        private bool TryGetSeriesValue(
            DataGridFillContext context,
            DataGridCellRange source,
            int rowIndex,
            int columnIndex,
            out object value)
        {
            value = null;
            if (!UseSeries)
            {
                return false;
            }

            if (context.IsVerticalFill && source.RowCount >= 2 &&
                TryGetField(context.Grid, columnIndex, out _, out IDataGridGeneratedEditField<TItem> field) &&
                TryGetRow(context, source.StartRow, out TItem firstRow) &&
                TryGetRow(context, source.StartRow + 1, out TItem secondRow))
            {
                return TryExtrapolate(field.GetValue(firstRow), field.GetValue(secondRow), rowIndex - source.StartRow, out value);
            }

            if (context.IsHorizontalFill && source.ColumnCount >= 2 &&
                TryGetRow(context, rowIndex, out TItem row) &&
                TryGetField(context.Grid, source.StartColumn, out _, out IDataGridGeneratedEditField<TItem> firstField) &&
                TryGetField(context.Grid, source.StartColumn + 1, out _, out IDataGridGeneratedEditField<TItem> secondField))
            {
                return TryExtrapolate(firstField.GetValue(row), secondField.GetValue(row), columnIndex - source.StartColumn, out value);
            }

            return false;
        }

        private bool TryGetCopiedValue(
            DataGridFillContext context,
            DataGridCellRange source,
            int rowIndex,
            int columnIndex,
            out object value)
        {
            int sourceRow = source.StartRow + PositiveModulo(rowIndex - source.StartRow, source.RowCount);
            int sourceColumn = source.StartColumn + PositiveModulo(columnIndex - source.StartColumn, source.ColumnCount);
            if (!TryGetRow(context, sourceRow, out TItem sourceItem) ||
                !TryGetField(context.Grid, sourceColumn, out _, out IDataGridGeneratedEditField<TItem> sourceField))
            {
                value = null;
                return false;
            }
            value = sourceField.GetValue(sourceItem);
            return true;
        }

        private bool TryGetField(
            DataGrid grid,
            int columnIndex,
            out string columnKey,
            out IDataGridGeneratedEditField<TItem> field)
        {
            columnKey = null;
            field = null;
            if (columnIndex < 0 || columnIndex >= grid.Columns.Count ||
                grid.Columns[columnIndex]?.ColumnKey is not string key ||
                !_edits.Fields.TryGetValue(key, out field))
            {
                return false;
            }
            columnKey = key;
            return true;
        }

        private static bool TryGetRow(DataGridFillContext context, int rowIndex, out TItem item)
        {
            if (context.TryGetRowItem(rowIndex, out object value) && value is TItem typed)
            {
                item = typed;
                return true;
            }
            item = default;
            return false;
        }

        private static bool Contains(DataGridCellRange range, int rowIndex, int columnIndex) =>
            rowIndex >= range.StartRow && rowIndex <= range.EndRow &&
            columnIndex >= range.StartColumn && columnIndex <= range.EndColumn;

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static bool TryExtrapolate(object first, object second, int index, out object value)
        {
            try
            {
                switch (first)
                {
                    case byte a when second is byte b: value = checked((byte)(a + (b - a) * index)); return true;
                    case sbyte a when second is sbyte b: value = checked((sbyte)(a + (b - a) * index)); return true;
                    case short a when second is short b: value = checked((short)(a + (b - a) * index)); return true;
                    case ushort a when second is ushort b: value = checked((ushort)(a + (b - a) * index)); return true;
                    case int a when second is int b: value = checked(a + (b - a) * index); return true;
                    case uint a when second is uint b: value = checked((uint)((long)a + ((long)b - a) * index)); return true;
                    case long a when second is long b: value = checked(a + (b - a) * index); return true;
                    case ulong a when second is ulong b:
                        value = checked((ulong)((decimal)a + ((decimal)b - a) * index)); return true;
                    case float a when second is float b: value = a + (b - a) * index; return true;
                    case double a when second is double b: value = a + (b - a) * index; return true;
                    case decimal a when second is decimal b: value = a + (b - a) * index; return true;
                    case DateTime a when second is DateTime b: value = a + TimeSpan.FromTicks(checked((b - a).Ticks * index)); return true;
                    case DateTimeOffset a when second is DateTimeOffset b: value = a + TimeSpan.FromTicks(checked((b - a).Ticks * index)); return true;
                    case TimeSpan a when second is TimeSpan b: value = a + TimeSpan.FromTicks(checked((b - a).Ticks * index)); return true;
                    default: value = null; return false;
                }
            }
            catch (OverflowException)
            {
                value = null;
                return false;
            }
        }
    }
}
