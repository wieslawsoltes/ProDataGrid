// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    public sealed class WorkbookValueResolver : IFormulaValueResolver, IFormulaRangeValueResolver, IFormulaStructuredReferenceResolver
    {
        private readonly IFormulaParser? _parser;
        private readonly FormulaEvaluator _evaluator;
        private readonly HashSet<FormulaEvaluationKey> _evaluationStack = new();
        private readonly HashSet<string> _nameEvaluationStack = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _allowCircularReferences;

        public WorkbookValueResolver(IFormulaParser? parser = null, bool allowCircularReferences = false)
        {
            _parser = parser;
            _allowCircularReferences = allowCircularReferences;
            _evaluator = new FormulaEvaluator();
        }

        public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value)
        {
            if (!TryGetNameExpression(context, name, out var expression, out var scopeKey))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
                return false;
            }

            if (!_nameEvaluationStack.Add(scopeKey))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Circ));
                return true;
            }

            try
            {
                value = _evaluator.Evaluate(expression, context.WithoutLocals(), this);
                return true;
            }
            finally
            {
                _nameEvaluationStack.Remove(scopeKey);
            }
        }

        public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value)
        {
            var origin = new FormulaCellAddress(context.Worksheet.Name, context.Address.Row, context.Address.Column);
            var sheetRef = reference.Start.Sheet;
            if (sheetRef.HasValue && (sheetRef.Value.IsRange || sheetRef.Value.IsExternal))
            {
                return TryResolveMultiSheetReference(context, reference, sheetRef.Value, out value);
            }

            if (reference.Kind == FormulaReferenceKind.Cell)
            {
                if (!FormulaReferenceResolver.TryResolveCell(reference.Start, origin, out var address))
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    return true;
                }

                value = EvaluateCell(context.Workbook, context, address);
                return true;
            }

            if (!FormulaReferenceResolver.TryResolveRange(reference, origin, out var range))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            var rows = range.End.Row - range.Start.Row + 1;
            var columns = range.End.Column - range.Start.Column + 1;
            var array = new FormulaArray(rows, columns, range.Start);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var address = new FormulaCellAddress(range.Start.SheetName, range.Start.Row + row, range.Start.Column + column);
                    array[row, column] = EvaluateCell(context.Workbook, context, address);
                }
            }

            value = FormulaValue.FromArray(array);
            return true;
        }

        public IEnumerable<FormulaValue> EnumerateReferenceValues(
            FormulaEvaluationContext context,
            FormulaReference reference)
        {
            var origin = new FormulaCellAddress(context.Worksheet.Name, context.Address.Row, context.Address.Column);
            var sheetRef = reference.Start.Sheet;
            if (sheetRef.HasValue && (sheetRef.Value.IsRange || sheetRef.Value.IsExternal))
            {
                foreach (var item in EnumerateMultiSheetReferenceValues(context, reference, sheetRef.Value))
                {
                    yield return item;
                }
                yield break;
            }

            if (reference.Kind == FormulaReferenceKind.Cell)
            {
                if (!FormulaReferenceResolver.TryResolveCell(reference.Start, origin, out var address))
                {
                    yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    yield break;
                }

                yield return EvaluateCell(context.Workbook, context, address);
                yield break;
            }

            if (!FormulaReferenceResolver.TryResolveRange(reference, origin, out var range))
            {
                yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                yield break;
            }

            for (var row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (var column = range.Start.Column; column <= range.End.Column; column++)
                {
                    var address = new FormulaCellAddress(range.Start.SheetName, row, column);
                    yield return EvaluateCell(context.Workbook, context, address);
                }
            }
        }

        public bool TryResolveStructuredReference(
            FormulaEvaluationContext context,
            FormulaStructuredReference reference,
            out FormulaValue value)
        {
            if (context.Workbook is IFormulaStructuredReferenceResolver workbookResolver &&
                workbookResolver.TryResolveStructuredReference(context, reference, out value))
            {
                return true;
            }

            if (context.Worksheet is IFormulaStructuredReferenceResolver worksheetResolver &&
                worksheetResolver.TryResolveStructuredReference(context, reference, out value))
            {
                return true;
            }

            value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
            return false;
        }

        private bool TryResolveMultiSheetReference(
            FormulaEvaluationContext context,
            FormulaReference reference,
            FormulaSheetReference sheetRef,
            out FormulaValue value)
        {
            value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            if (!TryGetWorkbookForSheet(context.Workbook, sheetRef, out var workbook))
            {
                return true;
            }

            if (!TryGetSheetRange(workbook, sheetRef, out var sheets))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            var origin = new FormulaCellAddress(context.Worksheet.Name, context.Address.Row, context.Address.Column);
            if (!TryResolveReferenceOnSheet(reference, origin, sheets[0].Name, out var startRange))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            var rowsPerSheet = startRange.End.Row - startRange.Start.Row + 1;
            var columns = startRange.End.Column - startRange.Start.Column + 1;

            if (sheets.Count == 1 && reference.Kind == FormulaReferenceKind.Cell)
            {
                var cellValue = EvaluateCell(workbook, context, startRange.Start);
                value = cellValue;
                return true;
            }

            var totalRows = rowsPerSheet * sheets.Count;
            var array = new FormulaArray(totalRows, columns, startRange.Start);

            for (var sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                var sheet = sheets[sheetIndex];
                if (!TryResolveReferenceOnSheet(reference, origin, sheet.Name, out var range))
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    return true;
                }

                for (var row = 0; row < rowsPerSheet; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var address = new FormulaCellAddress(range.Start.SheetName, range.Start.Row + row, range.Start.Column + column);
                        var cellValue = EvaluateCell(workbook, context, address);
                        array[sheetIndex * rowsPerSheet + row, column] = cellValue;
                    }
                }
            }

            value = FormulaValue.FromArray(array);
            return true;
        }

        private IEnumerable<FormulaValue> EnumerateMultiSheetReferenceValues(
            FormulaEvaluationContext context,
            FormulaReference reference,
            FormulaSheetReference sheetRef)
        {
            if (!TryGetWorkbookForSheet(context.Workbook, sheetRef, out var workbook))
            {
                yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                yield break;
            }

            if (!TryGetSheetRange(workbook, sheetRef, out var sheets))
            {
                yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                yield break;
            }

            var origin = new FormulaCellAddress(context.Worksheet.Name, context.Address.Row, context.Address.Column);
            if (!TryResolveReferenceOnSheet(reference, origin, sheets[0].Name, out var startRange))
            {
                yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                yield break;
            }

            var rowsPerSheet = startRange.End.Row - startRange.Start.Row + 1;
            var columns = startRange.End.Column - startRange.Start.Column + 1;

            if (sheets.Count == 1 && reference.Kind == FormulaReferenceKind.Cell)
            {
                yield return EvaluateCell(workbook, context, startRange.Start);
                yield break;
            }

            for (var sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                var sheet = sheets[sheetIndex];
                if (!TryResolveReferenceOnSheet(reference, origin, sheet.Name, out var range))
                {
                    yield return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    yield break;
                }

                for (var row = 0; row < rowsPerSheet; row++)
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var address = new FormulaCellAddress(range.Start.SheetName, range.Start.Row + row, range.Start.Column + column);
                        yield return EvaluateCell(workbook, context, address);
                    }
                }
            }
        }

        private static bool TryGetWorkbookForSheet(
            IFormulaWorkbook current,
            FormulaSheetReference sheetRef,
            out IFormulaWorkbook workbook)
        {
            workbook = current;
            if (!sheetRef.IsExternal)
            {
                return true;
            }

            if (current is IFormulaWorkbookResolver resolver &&
                !string.IsNullOrWhiteSpace(sheetRef.WorkbookName) &&
                resolver.TryGetWorkbook(sheetRef.WorkbookName, out workbook))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetSheetRange(
            IFormulaWorkbook workbook,
            FormulaSheetReference sheetRef,
            out List<IFormulaWorksheet> sheets)
        {
            sheets = new List<IFormulaWorksheet>();
            var startName = sheetRef.StartSheetName;
            if (string.IsNullOrWhiteSpace(startName))
            {
                return false;
            }

            var endName = sheetRef.EndSheetName ?? startName;
            var worksheetList = workbook.Worksheets;
            var startIndex = IndexOfSheet(worksheetList, startName);
            var endIndex = IndexOfSheet(worksheetList, endName);
            if (startIndex < 0 || endIndex < 0)
            {
                return false;
            }

            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            for (var i = startIndex; i <= endIndex; i++)
            {
                sheets.Add(worksheetList[i]);
            }

            return true;
        }

        private static int IndexOfSheet(IReadOnlyList<IFormulaWorksheet> sheets, string name)
        {
            for (var i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryResolveReferenceOnSheet(
            FormulaReference reference,
            FormulaCellAddress origin,
            string sheetName,
            out FormulaRangeAddress range)
        {
            range = default;

            if (!TryResolveCellOnSheet(reference.Start, origin, sheetName, out var start))
            {
                return false;
            }

            if (!TryResolveCellOnSheet(reference.End, origin, sheetName, out var end))
            {
                return false;
            }

            range = new FormulaRangeAddress(start, end);
            return true;
        }

        private static bool TryResolveCellOnSheet(
            FormulaReferenceAddress address,
            FormulaCellAddress origin,
            string sheetName,
            out FormulaCellAddress resolved)
        {
            resolved = default;

            var row = address.Mode == FormulaReferenceMode.A1
                ? address.Row
                : address.RowIsAbsolute ? address.Row : origin.Row + address.Row;

            var column = address.Mode == FormulaReferenceMode.A1
                ? address.Column
                : address.ColumnIsAbsolute ? address.Column : origin.Column + address.Column;

            if (row <= 0 || column <= 0)
            {
                return false;
            }

            resolved = new FormulaCellAddress(sheetName, row, column);
            return true;
        }

        private FormulaValue EvaluateCell(IFormulaWorkbook workbook, FormulaEvaluationContext context, FormulaCellAddress address)
        {
            var worksheet = workbook.GetWorksheet(address.SheetName ?? context.Worksheet.Name);
            var cell = worksheet.GetCell(address.Row, address.Column);

            if (cell.Expression == null && cell.Formula != null && _parser != null)
            {
                try
                {
                    var options = workbook.Settings.CreateParseOptions();
                    cell.Expression = _parser.Parse(cell.Formula, options);
                }
                catch (FormulaParseException)
                {
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
                }
            }

            if (cell.Expression == null)
            {
                return cell.Value;
            }

            var resolvedAddress = address.SheetName == null
                ? new FormulaCellAddress(worksheet.Name, address.Row, address.Column)
                : address;
            var evaluationKey = new FormulaEvaluationKey(workbook.Name, resolvedAddress);

            if (_evaluationStack.Contains(evaluationKey))
            {
                return _allowCircularReferences
                    ? cell.Value
                    : FormulaValue.FromError(new FormulaError(FormulaErrorType.Circ));
            }

            _evaluationStack.Add(evaluationKey);
            try
            {
                var cellContext = new FormulaEvaluationContext(
                    workbook,
                    worksheet,
                    address,
                    context.FunctionRegistry);
                var value = _evaluator.Evaluate(cell.Expression, cellContext, this).ToCellResult();
                cell.Value = value;
                return value;
            }
            finally
            {
                _evaluationStack.Remove(evaluationKey);
            }
        }

        private readonly struct FormulaEvaluationKey : IEquatable<FormulaEvaluationKey>
        {
            private readonly string _workbookName;
            private readonly FormulaCellAddress _address;

            public FormulaEvaluationKey(string? workbookName, FormulaCellAddress address)
            {
                _workbookName = workbookName ?? string.Empty;
                _address = address;
            }

            public bool Equals(FormulaEvaluationKey other)
            {
                return string.Equals(_workbookName, other._workbookName, StringComparison.OrdinalIgnoreCase) &&
                       _address.Equals(other._address);
            }

            public override bool Equals(object? obj)
            {
                return obj is FormulaEvaluationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = 17;
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(_workbookName);
                    hash = (hash * 31) + _address.GetHashCode();
                    return hash;
                }
            }
        }

        private static bool TryGetNameExpression(
            FormulaEvaluationContext context,
            string name,
            out FormulaExpression expression,
            out string scopeKey)
        {
            if (context.Worksheet is IFormulaNameProvider sheetProvider &&
                sheetProvider.TryGetName(name, out expression))
            {
                scopeKey = $"{context.Worksheet.Name}!{name}";
                return true;
            }

            if (context.Workbook is IFormulaNameProvider workbookProvider &&
                workbookProvider.TryGetName(name, out expression))
            {
                scopeKey = name;
                return true;
            }

            expression = null!;
            scopeKey = string.Empty;
            return false;
        }
    }
}
