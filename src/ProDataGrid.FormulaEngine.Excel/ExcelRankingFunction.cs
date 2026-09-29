// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelRankingOperation { EqualRank, AverageRank, InclusivePercent, ExclusivePercent }

    internal class ExcelRankingFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        private readonly ExcelRankingOperation _operation;
        public ExcelRankingFunction(string name, ExcelRankingOperation operation)
            : base(name, new FormulaFunctionInfo(2, 3)) { _operation = operation; }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => Evaluate(context, args, optionOmitted: false);

        public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            if (args.Count < 2 || args.Count > 3) return Error(FormulaErrorType.Value);
            var values = new FormulaValue[args.Count];
            for (var i = 0; i < args.Count; i++) values[i] = evaluator.Evaluate(args[i], context.EvaluationContext, resolver);
            return Evaluate(context, values, args.Count > 2 && context.EvaluationContext.IsArgumentOmitted(args[2]));
        }

        private FormulaValue Evaluate(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args, bool optionOmitted)
        {
            if (args.Count < 2 || args.Count > 3) return Error(FormulaErrorType.Value);
            var percentile = _operation is ExcelRankingOperation.InclusivePercent or ExcelRankingOperation.ExclusivePercent;
            var targetValue = args[percentile ? 1 : 0];
            var targets = new ExcelArrayOperand(targetValue);
            var source = new ExcelArrayOperand(args[percentile ? 0 : 1]);
            var optionValue = args.Count < 3 || optionOmitted ? FormulaValue.FromNumber(percentile ? 3 : 0) : args[2];
            var options = new ExcelArrayOperand(optionValue);
            var settings = context.EvaluationContext.Workbook.Settings;
            if (source.IsUnresolvedReference || targets.IsUnresolvedReference || options.IsUnresolvedReference) return Error(FormulaErrorType.Value);
            if (source.Length > settings.MaximumArrayCellCount || source.Length > Array.MaxLength) return Error(FormulaErrorType.Num);
            var rows = Math.Max(targets.Rows, options.Rows);
            var columns = Math.Max(targets.Columns, options.Columns);
            if ((targets.Rows != 1 && targets.Rows != rows) || (targets.Columns != 1 && targets.Columns != columns) ||
                (options.Rows != 1 && options.Rows != rows) || (options.Columns != 1 && options.Columns != columns)) return Error(FormulaErrorType.Value);
            if (rows > 1048576 || columns > 16384 || (long)rows * columns > settings.MaximumArrayCellCount) return Error(FormulaErrorType.Num);

            var arrayResult = targetValue.Kind == FormulaValueKind.Array || optionValue.Kind == FormulaValueKind.Array;
            if ((long)rows * columns == 1)
            {
                if (!TryControls(targets.Read(0, 0), options.Read(0, 0), settings, percentile, out var target, out var option, out var error))
                    return FormulaValue.FromError(error);
                if (!ExcelRankSearch.TryScan(source, target, settings, out var position, out error)) return FormulaValue.FromError(error);
                var value = Finish(position, target, option, settings);
                if (!arrayResult) return value;
                var singleton = new FormulaArray(1, 1);
                singleton[0, 0] = value;
                return FormulaValue.FromArray(singleton);
            }

            double[]? rented = null;
            try
            {
                // One private snapshot for all queries. Sorting never touches a host array or mask.
                var capacity = (int)source.Length;
                Span<double> values = capacity <= 128 ? stackalloc double[128] : (rented = ArrayPool<double>.Shared.Rent(capacity));
                if (!ExcelRankSearch.TryCopy(source, values, settings, out var count, out var error)) return FormulaValue.FromError(error);
                var sorted = values.Slice(0, count);
                sorted.Sort();
                if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out error)) return FormulaValue.FromError(error);
                for (var row = 0; row < rows; row++)
                    for (var column = 0; column < columns; column++)
                    {
                        var target = targets.Read(targets.Rows == 1 ? 0 : row, targets.Columns == 1 ? 0 : column);
                        var option = options.Read(options.Rows == 1 ? 0 : row, options.Columns == 1 ? 0 : column);
                        result[row, column] = TryControls(target, option, settings, percentile, out var x, out var order, out error)
                            ? Finish(ExcelRankSearch.Find(sorted, x), x, order, settings) : FormulaValue.FromError(error);
                    }
                return FormulaValue.FromArray(result);
            }
            finally
            {
                if (rented != null) ArrayPool<double>.Shared.Return(rented, clearArray: true);
            }
        }

        private FormulaValue Finish(in ExcelRankPosition position, double target, double option, FormulaCalculationSettings settings)
        {
            var value = _operation is ExcelRankingOperation.EqualRank or ExcelRankingOperation.AverageRank
                ? ExcelRankSearch.Rank(position, option != 0, _operation == ExcelRankingOperation.AverageRank)
                : ExcelRankSearch.Percent(position, target, option, _operation == ExcelRankingOperation.ExclusivePercent);
            return value.Kind == FormulaValueKind.Number ? ExcelFunctionUtilities.CreateNumber(settings, value.AsNumber()) : value;
        }

        private static bool TryControls(FormulaValue target, FormulaValue option, FormulaCalculationSettings settings, bool percentile,
            out double x, out double order, out FormulaError error)
        {
            order = 0;
            if (!FormulaCoercion.TryCoerceToNumber(target, settings, out x, out error) ||
                !FormulaCoercion.TryCoerceToNumber(option, settings, out order, out error)) return false;
            if (!double.IsFinite(x) || !double.IsFinite(order) || percentile && Math.Truncate(order) < 1)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            return true;
        }

        private static FormulaValue Error(FormulaErrorType type) => FormulaValue.FromError(new FormulaError(type));
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterRankingExtensions()
        {
            Register(new ExcelRankingFunction("RANK", ExcelRankingOperation.EqualRank));
            Register(new ExcelRankingFunction("PERCENTRANK", ExcelRankingOperation.InclusivePercent));
            Register(new ExcelRankingFunction("PERCENTRANK.INC", ExcelRankingOperation.InclusivePercent));
            Register(new ExcelRankingFunction("PERCENTRANK.EXC", ExcelRankingOperation.ExclusivePercent));
        }
    }
}
