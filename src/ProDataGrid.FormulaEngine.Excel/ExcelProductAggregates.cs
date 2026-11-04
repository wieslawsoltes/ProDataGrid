// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Buffers;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class SumProductFunction : ExcelFunctionBase
    {
        public SumProductFunction() : base("SUMPRODUCT", new FormulaFunctionInfo(1, 255)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count < 1 || args.Count > 255) return ExcelTextUtilities.ValueError();
            var first = new ExcelArrayOperand(args[0]);
            if (first.IsUnresolvedReference) return ExcelTextUtilities.ValueError();
            for (var i = 1; i < args.Count; i++)
            {
                var operand = new ExcelArrayOperand(args[i]);
                if (operand.IsUnresolvedReference || operand.Rows != first.Rows || operand.Columns != first.Columns)
                    return ExcelTextUtilities.ValueError();
            }

            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = new ExcelCompensatedSum();
            // One/two-argument calls need neither operand buffers nor an intermediate product array.
            if (args.Count <= 2)
            {
                var second = args.Count == 2 ? new ExcelArrayOperand(args[1]) : default;
                for (var row = 0; row < first.Rows; row++)
                {
                    for (var column = 0; column < first.Columns; column++)
                    {
                        if (!ExcelArrayOperand.TryNumeric(first.Read(row, column), settings, out var product, out _, out var error))
                            return FormulaValue.FromError(error);
                        if (args.Count == 2)
                        {
                            if (!ExcelArrayOperand.TryNumeric(second.Read(row, column), settings, out var factor, out _, out error))
                                return FormulaValue.FromError(error);
                            product *= factor;
                        }
                        if (!sum.Add(product)) return ExcelNumericUtilities.NumError();
                    }
                }
                return ExcelNumericUtilities.Number(context, sum.Total);
            }

            var operands = ArrayPool<ExcelArrayOperand>.Shared.Rent(args.Count);
            try
            {
                for (var i = 0; i < args.Count; i++) operands[i] = new ExcelArrayOperand(args[i]);
                for (var row = 0; row < first.Rows; row++)
                {
                    for (var column = 0; column < first.Columns; column++)
                    {
                        var product = 1d;
                        for (var i = 0; i < args.Count; i++)
                        {
                            // Do not stop at zero: a later operand may contain a formula error.
                            if (!ExcelArrayOperand.TryNumeric(operands[i].Read(row, column), settings, out var number, out _, out var error))
                                return FormulaValue.FromError(error);
                            product *= number;
                        }
                        if (!sum.Add(product)) return ExcelNumericUtilities.NumError();
                    }
                }
                return ExcelNumericUtilities.Number(context, sum.Total);
            }
            finally
            {
                // Operand views retain workbook arrays; return a cleared buffer on every exit.
                ArrayPool<ExcelArrayOperand>.Shared.Return(operands, clearArray: true);
            }
        }
    }

    internal enum ExcelPairedSquareOperation { DifferenceOfSquares, SumOfSquares, SquaredDifference }

    internal sealed class PairedSquareSumFunction : ExcelFunctionBase
    {
        private readonly ExcelPairedSquareOperation _operation;
        public PairedSquareSumFunction(string name, ExcelPairedSquareOperation operation)
            : base(name, new FormulaFunctionInfo(2, 2)) { _operation = operation; }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            if (args.Count != 2) return ExcelTextUtilities.ValueError();
            var left = new ExcelArrayOperand(args[0]);
            var right = new ExcelArrayOperand(args[1]);
            if (left.IsUnresolvedReference || right.IsUnresolvedReference) return ExcelTextUtilities.ValueError();
            if (left.Length != right.Length) return FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            var settings = context.EvaluationContext.Workbook.Settings;
            var sum = new ExcelCompensatedSum();
            var rightRow = 0;
            var rightColumn = 0;
            for (var row = 0; row < left.Rows; row++)
            {
                for (var column = 0; column < left.Columns; column++)
                {
                    if (!ExcelArrayOperand.TryNumeric(left.Read(row, column), settings, out var x, out var xNumeric, out var error) ||
                        !ExcelArrayOperand.TryNumeric(right.Read(rightRow, rightColumn), settings, out var y, out var yNumeric, out error))
                        return FormulaValue.FromError(error);
                    if (++rightColumn == right.Columns) { rightColumn = 0; rightRow++; }
                    // Ignore the pair in place; never compress independently and misalign observations.
                    if (!xNumeric || !yNumeric) continue;
                    var term = _operation switch
                    {
                        ExcelPairedSquareOperation.DifferenceOfSquares => x * x - y * y,
                        ExcelPairedSquareOperation.SumOfSquares => x * x + y * y,
                        _ => (x - y) * (x - y)
                    };
                    if (!sum.Add(term)) return ExcelNumericUtilities.NumError();
                }
            }
            return ExcelNumericUtilities.Number(context, sum.Total);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterProductAggregates()
        {
            Register(new SumProductFunction());
            Register(new PairedSquareSumFunction("SUMX2MY2", ExcelPairedSquareOperation.DifferenceOfSquares));
            Register(new PairedSquareSumFunction("SUMX2PY2", ExcelPairedSquareOperation.SumOfSquares));
            Register(new PairedSquareSumFunction("SUMXMY2", ExcelPairedSquareOperation.SquaredDifference));
        }
    }
}
