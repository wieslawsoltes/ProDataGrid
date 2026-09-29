// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal abstract class ExcelLambdaHelperFunction : ExcelFunctionBase, ILazyFormulaFunction
    {
        protected ExcelLambdaHelperFunction(string name, FormulaFunctionInfo info) : base(name, info) { }

        // Resolving a lambda body requires the caller's evaluator and resolver. Do not silently
        // substitute another workbook resolver when the eager-only interface is called directly.
        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            => ExcelLexicalUtilities.IncorrectParameters();

        public abstract FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
            FormulaEvaluator evaluator, IFormulaValueResolver resolver);
    }

    internal static class ExcelLambdaHelperUtilities
    {
        public static bool TryLambda(FormulaExpression expression, int parameterCount,
            FormulaEvaluationContext context, FormulaEvaluator evaluator, IFormulaValueResolver resolver,
            out FormulaLambda lambda, out FormulaValue error)
        {
            var value = evaluator.Evaluate(expression, context, resolver);
            lambda = null!;
            error = value.Kind == FormulaValueKind.Error ? value : ExcelLexicalUtilities.IncorrectParameters();
            if (value.Kind != FormulaValueKind.Lambda || value.AsLambda().ParameterCount != parameterCount) return false;
            lambda = value.AsLambda();
            return true;
        }

        public static bool TryArray(FormulaValue value, out FormulaArray array, out FormulaError error)
        {
            if (value.Kind == FormulaValueKind.Lambda)
            {
                array = null!;
                error = new FormulaError(FormulaErrorType.Value);
                return false;
            }
            return ExcelArrayShapeUtilities.TryGetArray(value, out array, out error);
        }

        public static bool TryScalar(FormulaValue value, out FormulaValue scalar)
        {
            // A single-cell result can be materialized; multi-cell or callable results would
            // create nested arrays, which the helper's rectangular result cannot represent.
            if (value.Kind == FormulaValueKind.Array)
            {
                var array = value.AsArray();
                if (array.RowCount == 1 && array.ColumnCount == 1)
                    value = ExcelDynamicArrayUtilities.GetArrayValue(array, 0, 0);
            }
            scalar = value.Kind == FormulaValueKind.Blank ? FormulaValue.FromNumber(0) : value;
            return value.Kind != FormulaValueKind.Array && value.Kind != FormulaValueKind.Lambda && value.Kind != FormulaValueKind.Reference;
        }

        public static FormulaValue NestedResult()
            => FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc, "The lambda must return a single value."));
    }

    public sealed partial class ExcelFunctionRegistry
    {
        partial void RegisterLambdaHelpers()
        {
            Register(new MapFunction());
            Register(new MakeArrayFunction());
            Register(new FoldArrayFunction(scan: false));
            Register(new FoldArrayFunction(scan: true));
            Register(new ByAxisFunction(byRow: true));
            Register(new ByAxisFunction(byRow: false));
        }
    }
}
