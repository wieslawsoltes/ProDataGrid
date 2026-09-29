// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    // Scalar argument views avoid a parameter array and delegate closure for every output cell.
    internal readonly struct ExcelScalarArguments
    {
        private readonly IReadOnlyList<FormulaValue> _values;
        private readonly uint _omitted;
        private readonly int _row;
        private readonly int _column;

        public ExcelScalarArguments(IReadOnlyList<FormulaValue> values, uint omitted, int row = 0, int column = 0)
        {
            _values = values; _omitted = omitted; _row = row; _column = column;
        }

        public int Count => _values.Count;
        public bool IsOmitted(int index) => index >= Count || (_omitted & (1u << index)) != 0;

        public FormulaValue this[int index]
        {
            get
            {
                var value = _values[index];
                if (value.Kind != FormulaValueKind.Array) return value;
                var array = value.AsArray();
                return ExcelDynamicArrayUtilities.GetArrayValue(array,
                    array.RowCount == 1 ? 0 : _row, array.ColumnCount == 1 ? 0 : _column);
            }
        }
    }

    internal abstract class ExcelElementwiseFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        protected ExcelElementwiseFunction(string name, int min, int max) : base(name, new FormulaFunctionInfo(min, max)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => Apply(context, args, 0);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < Info.MinArgs || args.Count > Info.MaxArgs) return ExcelTextUtilities.ValueError();
            var values = new FormulaValue[args.Count];
            uint omitted = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (context.EvaluationContext.IsArgumentOmitted(args[i])) omitted |= 1u << i;
                values[i] = evaluator.Evaluate(args[i], context.EvaluationContext, resolver);
            }
            return Apply(context, values, omitted);
        }

        private FormulaValue Apply(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args, uint omitted)
        {
            if (args.Count < Info.MinArgs || args.Count > Info.MaxArgs) return ExcelTextUtilities.ValueError();
            var rows = 1;
            var columns = 1;
            var hasArray = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i].Kind != FormulaValueKind.Array) continue;
                hasArray = true;
                var array = args[i].AsArray();
                rows = Math.Max(rows, array.RowCount);
                columns = Math.Max(columns, array.ColumnCount);
            }
            if (!hasArray) return InvokeScalar(context, new ExcelScalarArguments(args, omitted));
            for (var i = 0; i < args.Count; i++)
            {
                if (args[i].Kind != FormulaValueKind.Array) continue;
                var array = args[i].AsArray();
                if ((array.RowCount != 1 && array.RowCount != rows) || (array.ColumnCount != 1 && array.ColumnCount != columns))
                    return ExcelTextUtilities.ValueError();
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out var error))
                return FormulaValue.FromError(error);
            for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                    result[row, column] = InvokeScalar(context, new ExcelScalarArguments(args, omitted, row, column));
            return FormulaValue.FromArray(result);
        }

        protected abstract FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args);
    }

    internal static class ExcelTextUtilities
    {
        public const int MaximumLength = 32767;
        public static FormulaValue ValueError() => FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
        public static FormulaValue Text(string text) => text.Length <= MaximumLength ? FormulaValue.FromText(text) : ValueError();
        public static bool TryText(FormulaValue value, out string text, out FormulaError error)
            => FormulaCoercion.TryCoerceToText(value, out text, out error);

        public static bool TryInteger(FormulaFunctionContext context, FormulaValue value, out int integer, out FormulaError error)
        {
            integer = 0;
            if (!FormulaCoercion.TryCoerceToNumber(value, context.EvaluationContext.Workbook.Settings, out var number, out error)) return false;
            number = Math.Truncate(number);
            if (!double.IsFinite(number) || number < int.MinValue || number > int.MaxValue)
            {
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
            integer = (int)number;
            return true;
        }
    }
}
