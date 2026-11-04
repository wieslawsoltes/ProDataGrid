// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class MakeArrayFunction : ExcelLambdaHelperFunction
    {
        public MakeArrayFunction() : base("MAKEARRAY", new FormulaFunctionInfo(3, 3)) { }

        public override FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver)
        {
            var evaluation = context.EvaluationContext;
            if (!ExcelArrayShapeUtilities.TryInteger(context, evaluator.Evaluate(args[0], evaluation, resolver), out var rows, out var error) ||
                !ExcelArrayShapeUtilities.TryInteger(context, evaluator.Evaluate(args[1], evaluation, resolver), out var columns, out error))
                return FormulaValue.FromError(error);
            if (rows < 1 || columns < 1) return ExcelLexicalUtilities.IncorrectParameters();
            if (!ExcelLambdaHelperUtilities.TryLambda(args[2], 2, evaluation, evaluator, resolver, out var lambda, out var lambdaError))
                return lambdaError;
            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out error))
                return FormulaValue.FromError(error);

            var parameters = new FormulaValue[2];
            for (var row = 0; row < rows; row++)
            {
                parameters[0] = FormulaValue.FromNumber(row + 1);
                for (var column = 0; column < columns; column++)
                {
                    parameters[1] = FormulaValue.FromNumber(column + 1);
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
