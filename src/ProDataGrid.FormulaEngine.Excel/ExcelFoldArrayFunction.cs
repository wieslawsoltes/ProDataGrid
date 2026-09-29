// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class FoldArrayFunction : ExcelLambdaHelperFunction
    {
        private readonly bool _scan;
        public FoldArrayFunction(bool scan) : base(scan ? "SCAN" : "REDUCE", new FormulaFunctionInfo(3, 3))
        {
            _scan = scan;
        }

        public override FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            var evaluation = context.EvaluationContext;
            var omitted = evaluation.IsArgumentOmitted(args[0]);
            var accumulator = evaluator.Evaluate(args[0], evaluation, resolver);
            if (!ExcelLambdaHelperUtilities.TryArray(evaluator.Evaluate(args[1], evaluation, resolver), out var source, out var error))
                return FormulaValue.FromError(error);
            if (!ExcelLambdaHelperUtilities.TryLambda(args[2], 2, evaluation, evaluator, resolver, out var lambda, out var lambdaError))
                return lambdaError;

            FormulaArray? result = null;
            if (_scan && !ExcelArrayShapeUtilities.TryCreate(context, source.RowCount, source.ColumnCount, out result, out error))
                return FormulaValue.FromError(error);
            if (_scan && omitted) accumulator = FormulaValue.FromNumber(0);
            var first = true;
            var parameters = new FormulaValue[2];
            for (var row = 0; row < source.RowCount; row++)
            {
                for (var column = 0; column < source.ColumnCount; column++)
                {
                    var element = ExcelDynamicArrayUtilities.GetArrayValue(source, row, column);
                    // The documented REDUCE omission rule seeds from the first element.
                    // A blank-cell argument is not a syntactically omitted argument.
                    if (!_scan && omitted && first)
                        accumulator = element;
                    else
                    {
                        parameters[0] = accumulator;
                        parameters[1] = element;
                        accumulator = evaluator.InvokeLambda(lambda, parameters, evaluation, resolver);
                    }
                    first = false;
                    if (_scan)
                    {
                        if (!ExcelLambdaHelperUtilities.TryScalar(accumulator, out var scalar))
                            return ExcelLambdaHelperUtilities.NestedResult();
                        accumulator = scalar;
                        result![row, column] = scalar;
                    }
                }
            }
            // REDUCE intentionally supports an array or closure accumulator. Callers can compose
            // its result; the workbook cell boundary separately rejects uncalled lambdas.
            return _scan ? FormulaValue.FromArray(result!) : accumulator;
        }
    }
}
