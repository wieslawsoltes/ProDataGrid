// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    internal enum FormulaReferenceUpdateKind
    {
        InsertRows,
        DeleteRows,
        InsertColumns,
        DeleteColumns,
        RenameSheet,
        RenameTable,
        RenameTableColumn
    }

    internal readonly struct FormulaReferenceUpdate
    {
        private FormulaReferenceUpdate(
            FormulaReferenceUpdateKind kind,
            string? sheetName,
            int index,
            int count,
            string? oldName,
            string? newName,
            string? tableName)
        {
            Kind = kind;
            SheetName = sheetName;
            Index = index;
            Count = count;
            OldName = oldName;
            NewName = newName;
            TableName = tableName;
        }

        public FormulaReferenceUpdateKind Kind { get; }

        public string? SheetName { get; }

        public int Index { get; }

        public int Count { get; }

        public string? OldName { get; }

        public string? NewName { get; }

        public string? TableName { get; }

        public static FormulaReferenceUpdate InsertRows(string sheetName, int rowIndex, int count)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.InsertRows, sheetName, rowIndex, count, null, null, null);
        }

        public static FormulaReferenceUpdate DeleteRows(string sheetName, int rowIndex, int count)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.DeleteRows, sheetName, rowIndex, count, null, null, null);
        }

        public static FormulaReferenceUpdate InsertColumns(string sheetName, int columnIndex, int count)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.InsertColumns, sheetName, columnIndex, count, null, null, null);
        }

        public static FormulaReferenceUpdate DeleteColumns(string sheetName, int columnIndex, int count)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.DeleteColumns, sheetName, columnIndex, count, null, null, null);
        }

        public static FormulaReferenceUpdate RenameSheet(string oldName, string newName)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.RenameSheet, null, 0, 0, oldName, newName, null);
        }

        public static FormulaReferenceUpdate RenameTable(string oldName, string newName)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.RenameTable, null, 0, 0, oldName, newName, null);
        }

        public static FormulaReferenceUpdate RenameTableColumn(string tableName, string oldName, string newName)
        {
            return new FormulaReferenceUpdate(FormulaReferenceUpdateKind.RenameTableColumn, null, 0, 0, oldName, newName, tableName);
        }
    }

    internal static class FormulaReferenceUpdater
    {
        public static FormulaExpression Update(
            FormulaExpression expression,
            FormulaCellAddress oldOrigin,
            FormulaCellAddress newOrigin,
            FormulaReferenceUpdate update,
            out bool changed)
        {
            var rewriter = new ReferenceRewriter(oldOrigin, newOrigin, update);
            var updated = rewriter.Rewrite(expression);
            changed = rewriter.Changed;
            return updated;
        }

        private sealed class ReferenceRewriter
        {
            private readonly FormulaCellAddress _oldOrigin;
            private readonly FormulaCellAddress _newOrigin;
            private readonly FormulaReferenceUpdate _update;
            private bool _changed;

            public ReferenceRewriter(
                FormulaCellAddress oldOrigin,
                FormulaCellAddress newOrigin,
                FormulaReferenceUpdate update)
            {
                _oldOrigin = oldOrigin;
                _newOrigin = newOrigin;
                _update = update;
            }

            public bool Changed => _changed;

            public FormulaExpression Rewrite(FormulaExpression expression)
            {
                switch (expression.Kind)
                {
                    case FormulaExpressionKind.Invocation:
                        var invocation = (FormulaInvocationExpression)expression;
                        var target = Rewrite(invocation.Target);
                        var args = new FormulaExpression[invocation.Arguments.Count];
                        var changed = !ReferenceEquals(target, invocation.Target);
                        for (var i = 0; i < args.Length; i++)
                        {
                            args[i] = Rewrite(invocation.Arguments[i]);
                            changed |= !ReferenceEquals(args[i], invocation.Arguments[i]);
                        }
                        if (!changed) return expression;
                        _changed = true;
                        return new FormulaInvocationExpression(target, args);
                    case FormulaExpressionKind.Reference:
                        return RewriteReference((FormulaReferenceExpression)expression);
                    case FormulaExpressionKind.StructuredReference:
                        return RewriteStructuredReference((FormulaStructuredReferenceExpression)expression);
                    case FormulaExpressionKind.Unary:
                        return RewriteUnary((FormulaUnaryExpression)expression);
                    case FormulaExpressionKind.Binary:
                        return RewriteBinary((FormulaBinaryExpression)expression);
                    case FormulaExpressionKind.FunctionCall:
                        return RewriteFunctionCall((FormulaFunctionCallExpression)expression);
                    case FormulaExpressionKind.ArrayLiteral:
                        return RewriteArrayLiteral((FormulaArrayExpression)expression);
                    case FormulaExpressionKind.Name:
                    case FormulaExpressionKind.Literal:
                    default:
                        return expression;
                }
            }

            private FormulaExpression RewriteUnary(FormulaUnaryExpression expression)
            {
                var operand = Rewrite(expression.Operand);
                if (ReferenceEquals(operand, expression.Operand))
                {
                    return expression;
                }

                _changed = true;
                return new FormulaUnaryExpression(expression.Operator, operand);
            }

            private FormulaExpression RewriteBinary(FormulaBinaryExpression expression)
            {
                var left = Rewrite(expression.Left);
                var right = Rewrite(expression.Right);
                if (ReferenceEquals(left, expression.Left) && ReferenceEquals(right, expression.Right))
                {
                    return expression;
                }

                _changed = true;
                return new FormulaBinaryExpression(expression.Operator, left, right);
            }

            private FormulaExpression RewriteFunctionCall(FormulaFunctionCallExpression expression)
            {
                FormulaExpression[]? updated = null;
                for (var i = 0; i < expression.Arguments.Count; i++)
                {
                    var argument = expression.Arguments[i];
                    var next = Rewrite(argument);
                    if (updated == null && !ReferenceEquals(next, argument))
                    {
                        updated = new FormulaExpression[expression.Arguments.Count];
                        for (var j = 0; j < i; j++) updated[j] = expression.Arguments[j];
                    }
                    if (updated != null) updated[i] = next;
                }
                if (updated == null) return expression;
                _changed = true;
                return new FormulaFunctionCallExpression(expression.Name, updated);
            }

            private FormulaExpression RewriteArrayLiteral(FormulaArrayExpression expression)
            {
                FormulaExpression[,]? updated = null;
                for (var row = 0; row < expression.RowCount; row++)
                {
                    for (var column = 0; column < expression.ColumnCount; column++)
                    {
                        var item = expression[row, column];
                        var updatedItem = Rewrite(item);
                        if (ReferenceEquals(updatedItem, item))
                        {
                            continue;
                        }

                        updated ??= CloneArray(expression);
                        updated[row, column] = updatedItem;
                    }
                }

                if (updated == null)
                {
                    return expression;
                }

                _changed = true;
                return new FormulaArrayExpression(updated);
            }

            private static FormulaExpression[,] CloneArray(FormulaArrayExpression expression)
            {
                var items = new FormulaExpression[expression.RowCount, expression.ColumnCount];
                for (var row = 0; row < expression.RowCount; row++)
                {
                    for (var column = 0; column < expression.ColumnCount; column++)
                    {
                        items[row, column] = expression[row, column];
                    }
                }

                return items;
            }

            private FormulaExpression RewriteReference(FormulaReferenceExpression expression)
            {
                var reference = expression.Reference;
                var updated = UpdateReference(reference, out var invalid, out var changed);
                if (!changed)
                {
                    return expression;
                }

                _changed = true;
                if (invalid)
                {
                    return new FormulaLiteralExpression(FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref)));
                }

                return new FormulaReferenceExpression(updated);
            }

            private FormulaExpression RewriteStructuredReference(FormulaStructuredReferenceExpression expression)
            {
                var reference = expression.Reference;
                var updated = UpdateStructuredReference(reference, out var changed);
                if (!changed)
                {
                    return expression;
                }

                _changed = true;
                return new FormulaStructuredReferenceExpression(updated);
            }

            private FormulaReference UpdateReference(
                FormulaReference reference,
                out bool invalid,
                out bool changed)
            {
                invalid = false;
                changed = false;

                var start = reference.Start;
                var end = reference.End;

                if (_update.Kind == FormulaReferenceUpdateKind.RenameSheet)
                {
                    var startSheet = UpdateSheetReference(start.Sheet, _update.OldName, _update.NewName, out var sheetChanged);
                    var endSheet = UpdateSheetReference(end.Sheet, _update.OldName, _update.NewName, out var endSheetChanged);
                    if (sheetChanged || endSheetChanged)
                    {
                        start = start.WithSheet(startSheet);
                        end = end.WithSheet(endSheet);
                        changed = true;
                    }

                    if (!changed)
                    {
                        return reference;
                    }

                    return reference.Kind == FormulaReferenceKind.Cell
                        ? new FormulaReference(start)
                        : new FormulaReference(start, end);
                }

                if (!ShouldShiftReference(reference))
                {
                    return reference;
                }

                if (reference.Kind == FormulaReferenceKind.Cell)
                {
                    if (!TryShiftAddress(start, out var shifted, out invalid))
                    {
                        changed = true;
                        return reference;
                    }

                    if (!shifted.Equals(start))
                    {
                        changed = true;
                    }

                    return new FormulaReference(shifted);
                }

                if (!TryShiftRange(reference, out var shiftedRange, out invalid))
                {
                    changed = true;
                    return reference;
                }

                if (!shiftedRange.Start.Equals(start) || !shiftedRange.End.Equals(end))
                {
                    changed = true;
                }

                return new FormulaReference(shiftedRange.Start, shiftedRange.End);
            }

            private bool TryShiftRange(
                FormulaReference reference,
                out FormulaReference shifted,
                out bool invalid)
            {
                shifted = reference;
                invalid = false;

                if (!TryGetAbsolute(reference.Start, out var startRow, out var startColumn))
                {
                    invalid = true;
                    return false;
                }

                if (!TryGetAbsolute(reference.End, out var endRow, out var endColumn))
                {
                    invalid = true;
                    return false;
                }

                var rowResult = TryShiftRangeIndex(
                    startRow,
                    endRow,
                    out var shiftedStartRow,
                    out var shiftedEndRow,
                    out var rowInvalid,
                    applyRows: true);
                var columnResult = TryShiftRangeIndex(
                    startColumn,
                    endColumn,
                    out var shiftedStartColumn,
                    out var shiftedEndColumn,
                    out var columnInvalid,
                    applyRows: false);

                if (!rowResult || !columnResult || rowInvalid || columnInvalid)
                {
                    invalid = true;
                    return false;
                }

                var start = CreateShiftedAddress(reference.Start, shiftedStartRow, shiftedStartColumn);
                var end = CreateShiftedAddress(reference.End, shiftedEndRow, shiftedEndColumn);
                shifted = new FormulaReference(start, end);
                return true;
            }

            private bool TryShiftAddress(
                FormulaReferenceAddress address,
                out FormulaReferenceAddress shifted,
                out bool invalid)
            {
                shifted = address;
                invalid = false;

                if (!TryGetAbsolute(address, out var row, out var column))
                {
                    invalid = true;
                    return false;
                }

                if (!TryShiftIndex(row, out var shiftedRow, out var rowInvalid, applyRows: true) ||
                    !TryShiftIndex(column, out var shiftedColumn, out var columnInvalid, applyRows: false))
                {
                    invalid = true;
                    return false;
                }

                if (rowInvalid || columnInvalid)
                {
                    invalid = true;
                    return false;
                }

                shifted = CreateShiftedAddress(address, shiftedRow, shiftedColumn);
                return true;
            }

            private bool ShouldShiftReference(FormulaReference reference)
            {
                var targetSheet = _update.SheetName;
                if (string.IsNullOrWhiteSpace(targetSheet))
                {
                    return false;
                }

                var sheetRef = reference.Start.Sheet;
                if (sheetRef.HasValue)
                {
                    var sheet = sheetRef.Value;
                    if (sheet.IsExternal || sheet.IsRange)
                    {
                        return false;
                    }

                    return string.Equals(sheet.StartSheetName, targetSheet, StringComparison.OrdinalIgnoreCase);
                }

                return string.Equals(_oldOrigin.SheetName, targetSheet, StringComparison.OrdinalIgnoreCase);
            }

            private bool TryShiftIndex(
                int value,
                out int shifted,
                out bool invalid,
                bool applyRows)
            {
                shifted = value;
                invalid = false;

                var isRowOp = _update.Kind == FormulaReferenceUpdateKind.InsertRows ||
                              _update.Kind == FormulaReferenceUpdateKind.DeleteRows;
                if (applyRows != isRowOp)
                {
                    return true;
                }

                if (_update.Kind == FormulaReferenceUpdateKind.InsertRows ||
                    _update.Kind == FormulaReferenceUpdateKind.InsertColumns)
                {
                    if (value >= _update.Index)
                    {
                        shifted = value + _update.Count;
                    }
                    return true;
                }

                var deleteStart = _update.Index;
                var deleteEnd = _update.Index + _update.Count - 1;
                if (value >= deleteStart && value <= deleteEnd)
                {
                    invalid = true;
                    return true;
                }

                if (value > deleteEnd)
                {
                    shifted = value - _update.Count;
                }

                return true;
            }

            private bool TryShiftRangeIndex(
                int start,
                int end,
                out int shiftedStart,
                out int shiftedEnd,
                out bool invalid,
                bool applyRows)
            {
                shiftedStart = start;
                shiftedEnd = end;
                invalid = false;

                var isRowOp = _update.Kind == FormulaReferenceUpdateKind.InsertRows ||
                              _update.Kind == FormulaReferenceUpdateKind.DeleteRows;
                if (applyRows != isRowOp)
                {
                    return true;
                }

                if (_update.Kind == FormulaReferenceUpdateKind.InsertRows ||
                    _update.Kind == FormulaReferenceUpdateKind.InsertColumns)
                {
                    if (start >= _update.Index)
                    {
                        shiftedStart = start + _update.Count;
                    }

                    if (end >= _update.Index)
                    {
                        shiftedEnd = end + _update.Count;
                    }

                    return true;
                }

                var deleteStart = _update.Index;
                var deleteEnd = _update.Index + _update.Count - 1;

                if (end < deleteStart)
                {
                    return true;
                }

                if (start > deleteEnd)
                {
                    shiftedStart = start - _update.Count;
                    shiftedEnd = end - _update.Count;
                    return true;
                }

                if (start >= deleteStart && end <= deleteEnd)
                {
                    invalid = true;
                    return false;
                }

                shiftedStart = start < deleteStart ? start : deleteStart;
                shiftedEnd = end > deleteEnd ? end - _update.Count : deleteStart - 1;
                if (shiftedEnd < shiftedStart || shiftedStart <= 0 || shiftedEnd <= 0)
                {
                    invalid = true;
                    return false;
                }

                return true;
            }

            private bool TryGetAbsolute(
                FormulaReferenceAddress address,
                out int row,
                out int column)
            {
                if (address.Mode == FormulaReferenceMode.A1)
                {
                    row = address.Row;
                    column = address.Column;
                }
                else
                {
                    row = address.RowIsAbsolute ? address.Row : _oldOrigin.Row + address.Row;
                    column = address.ColumnIsAbsolute ? address.Column : _oldOrigin.Column + address.Column;
                }

                return row > 0 && column > 0;
            }

            private FormulaReferenceAddress CreateShiftedAddress(
                FormulaReferenceAddress address,
                int row,
                int column)
            {
                if (address.Mode == FormulaReferenceMode.A1)
                {
                    return new FormulaReferenceAddress(
                        FormulaReferenceMode.A1,
                        row,
                        column,
                        address.RowIsAbsolute,
                        address.ColumnIsAbsolute,
                        address.Sheet);
                }

                var newRow = address.RowIsAbsolute ? row : row - _newOrigin.Row;
                var newColumn = address.ColumnIsAbsolute ? column : column - _newOrigin.Column;
                return new FormulaReferenceAddress(
                    FormulaReferenceMode.R1C1,
                    newRow,
                    newColumn,
                    address.RowIsAbsolute,
                    address.ColumnIsAbsolute,
                    address.Sheet);
            }

            private FormulaStructuredReference UpdateStructuredReference(
                FormulaStructuredReference reference,
                out bool changed)
            {
                changed = false;
                var updatedSheet = reference.Sheet;
                if (_update.Kind == FormulaReferenceUpdateKind.RenameSheet)
                {
                    updatedSheet = UpdateSheetReference(reference.Sheet, _update.OldName, _update.NewName, out var sheetChanged);
                    if (sheetChanged)
                    {
                        changed = true;
                    }
                }

                if (_update.Kind == FormulaReferenceUpdateKind.RenameTable &&
                    !string.IsNullOrWhiteSpace(reference.TableName) &&
                    string.Equals(reference.TableName, _update.OldName, StringComparison.OrdinalIgnoreCase))
                {
                    changed = true;
                    return new FormulaStructuredReference(
                        updatedSheet,
                        _update.NewName,
                        reference.Scope,
                        reference.ColumnStart,
                        reference.ColumnEnd);
                }

                if (_update.Kind == FormulaReferenceUpdateKind.RenameTableColumn &&
                    !string.IsNullOrWhiteSpace(reference.TableName) &&
                    string.Equals(reference.TableName, _update.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    var columnStart = reference.ColumnStart;
                    var columnEnd = reference.ColumnEnd;
                    if (!string.IsNullOrWhiteSpace(columnStart) &&
                        string.Equals(columnStart, _update.OldName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnStart = _update.NewName;
                        changed = true;
                    }

                    if (!string.IsNullOrWhiteSpace(columnEnd) &&
                        string.Equals(columnEnd, _update.OldName, StringComparison.OrdinalIgnoreCase))
                    {
                        columnEnd = _update.NewName;
                        changed = true;
                    }

                    if (changed)
                    {
                        return new FormulaStructuredReference(
                            updatedSheet,
                            reference.TableName,
                            reference.Scope,
                            columnStart,
                            columnEnd);
                    }
                }

                if (changed && updatedSheet != reference.Sheet)
                {
                    return new FormulaStructuredReference(
                        updatedSheet,
                        reference.TableName,
                        reference.Scope,
                        reference.ColumnStart,
                        reference.ColumnEnd);
                }

                return reference;
            }

            private static FormulaSheetReference? UpdateSheetReference(
                FormulaSheetReference? sheet,
                string? oldName,
                string? newName,
                out bool changed)
            {
                changed = false;
                if (sheet == null || string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                {
                    return sheet;
                }

                var sheetRef = sheet.Value;
                var start = sheetRef.StartSheetName;
                var end = sheetRef.EndSheetName;

                if (!string.IsNullOrWhiteSpace(start) &&
                    string.Equals(start, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    start = newName;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(end) &&
                    string.Equals(end, oldName, StringComparison.OrdinalIgnoreCase))
                {
                    end = newName;
                    changed = true;
                }

                if (!changed)
                {
                    return sheet;
                }

                return new FormulaSheetReference(sheetRef.WorkbookName, start, end);
            }
        }
    }
}
