// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class MapFunction : ExcelLambdaHelperFunction
    {
        public MapFunction() : base("MAP", new FormulaFunctionInfo(2, 254)) { }

        public override FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            var evaluation = context.EvaluationContext;
            var count = args.Count - 1;
            var sources = new FormulaArray[count];
            for (var i = 0; i < count; i++)
            {
                var value = evaluator.Evaluate(args[i], evaluation, resolver);
                if (!ExcelLambdaHelperUtilities.TryArray(value, out sources[i], out var error))
                    return FormulaValue.FromError(error);
                if (sources[i].RowCount != sources[0].RowCount || sources[i].ColumnCount != sources[0].ColumnCount)
                    return FormulaValue.FromError(new FormulaError(FormulaErrorType.Value, "MAP input dimensions must match."));
            }
            if (!ExcelLambdaHelperUtilities.TryLambda(args[count], count, evaluation, evaluator, resolver, out var lambda, out var lambdaError))
                return lambdaError;
            if (!ExcelArrayShapeUtilities.TryCreate(context, sources[0].RowCount, sources[0].ColumnCount, out var result, out var allocationError))
                return FormulaValue.FromError(allocationError);

            // The immutable invocation scope snapshots this reusable buffer. No reparsing,
            // AST substitution, or per-cell input-array materialization is needed.
            var parameters = new FormulaValue[count];
            for (var row = 0; row < result.RowCount; row++)
            {
                for (var column = 0; column < result.ColumnCount; column++)
                {
                    for (var i = 0; i < count; i++)
                        parameters[i] = ExcelDynamicArrayUtilities.GetArrayValue(sources[i], row, column);
                    var value = evaluator.InvokeLambda(lambda, parameters, evaluation, resolver);
                    if (!ExcelLambdaHelperUtilities.TryScalar(value, out var scalar))
                        return ExcelLambdaHelperUtilities.NestedResult();
                    result[row, column] = scalar;
                }
            }
            return FormulaValue.FromArray(result);
        }
    }
}
