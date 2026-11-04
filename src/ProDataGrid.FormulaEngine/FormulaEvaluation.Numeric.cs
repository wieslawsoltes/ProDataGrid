// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProDataGrid.FormulaEngine
{
    public sealed partial class FormulaEvaluator
    {
        private static FormulaValue EvaluateNumberBinary(
            FormulaBinaryOperator op, double left, double right, FormulaCalculationSettings settings)
        {
            // Match general coercion exactly: normalize both operands, then the numeric result.
            // Text, booleans, errors, implicit intersection and concatenation use the general path.
            if (settings.ApplyNumberPrecision)
            {
                left = FormulaNumberUtilities.ApplyPrecision(left, settings.NumberPrecisionDigits);
                right = FormulaNumberUtilities.ApplyPrecision(right, settings.NumberPrecisionDigits);
            }

            double result;
            switch (op)
            {
                case FormulaBinaryOperator.Add:
                    result = left + right;
                    break;
                case FormulaBinaryOperator.Subtract:
                    result = left - right;
                    break;
                case FormulaBinaryOperator.Multiply:
                    result = left * right;
                    break;
                case FormulaBinaryOperator.Divide:
                    // Preserve the established division semantics, including subnormal values.
                    if (Math.Abs(right) <= double.Epsilon)
                    {
                        return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
                    }
                    result = left / right;
                    break;
                case FormulaBinaryOperator.Power:
                    result = Math.Pow(left, right);
                    break;
                default:
                    // CompareTo, rather than direct operators, preserves the existing NaN ordering.
                    var comparison = left.CompareTo(right);
                    return op switch
                    {
                        FormulaBinaryOperator.Equal => FormulaValue.FromBoolean(comparison == 0),
                        FormulaBinaryOperator.NotEqual => FormulaValue.FromBoolean(comparison != 0),
                        FormulaBinaryOperator.Less => FormulaValue.FromBoolean(comparison < 0),
                        FormulaBinaryOperator.LessOrEqual => FormulaValue.FromBoolean(comparison <= 0),
                        FormulaBinaryOperator.Greater => FormulaValue.FromBoolean(comparison > 0),
                        FormulaBinaryOperator.GreaterOrEqual => FormulaValue.FromBoolean(comparison >= 0),
                        _ => FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc))
                    };
            }

            return FormulaValue.FromNumber(settings.ApplyNumberPrecision
                ? FormulaNumberUtilities.ApplyPrecision(result, settings.NumberPrecisionDigits)
                : result);
        }
    }
}
