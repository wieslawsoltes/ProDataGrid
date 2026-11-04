// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class ByAxisFunction : ExcelLambdaHelperFunction
    {
        private readonly bool _byRow;
        public ByAxisFunction(bool byRow) : base(byRow ? "BYROW" : "BYCOL", new FormulaFunctionInfo(2, 2))
        {
            _byRow = byRow;
        }

        public override FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            var evaluation = context.EvaluationContext;
            if (!ExcelLambdaHelperUtilities.TryArray(evaluator.Evaluate(args[0], evaluation, resolver), out var source, out var error))
                return FormulaValue.FromError(error);
            if (!ExcelLambdaHelperUtilities.TryLambda(args[1], 1, evaluation, evaluator, resolver, out var lambda, out var lambdaError))
                return lambdaError;
            if (!ExcelArrayShapeUtilities.TryCreate(context, _byRow ? source.RowCount : 1, _byRow ? 1 : source.ColumnCount, out var result, out error))
                return FormulaValue.FromError(error);

            var count = _byRow ? source.RowCount : source.ColumnCount;
            var width = _byRow ? source.ColumnCount : source.RowCount;
            var parameters = new FormulaValue[1];
            for (var index = 0; index < count; index++)
            {
                // A fresh slice is necessary: a UDF can retain its arguments or a closure can
                // capture the slice. Reusing and mutating it would corrupt earlier invocations.
                if (!ExcelArrayShapeUtilities.TryCreate(context, _byRow ? 1 : width, _byRow ? width : 1, out var slice, out error))
                    return FormulaValue.FromError(error);
                for (var offset = 0; offset < width; offset++)
                    slice[_byRow ? 0 : offset, _byRow ? offset : 0] = ExcelDynamicArrayUtilities.GetArrayValue(
                        source, _byRow ? index : offset, _byRow ? offset : index);
                parameters[0] = FormulaValue.FromArray(slice);
                var value = evaluator.InvokeLambda(lambda, parameters, evaluation, resolver);
                if (!ExcelLambdaHelperUtilities.TryScalar(value, out var scalar))
                    return ExcelLambdaHelperUtilities.NestedResult();
                result[_byRow ? index : 0, _byRow ? 0 : index] = scalar;
            }
            return FormulaValue.FromArray(result);
        }
    }
}
