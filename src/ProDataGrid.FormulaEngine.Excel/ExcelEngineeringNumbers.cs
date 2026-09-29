// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal static class ExcelEngineeringNumbers
    {
        public static FormulaValue NumError()
            => FormulaValue.FromError(new FormulaError(FormulaErrorType.Num));

        public static bool TryFinite(FormulaFunctionContext context, FormulaValue value,
            out double number, out FormulaError error)
        {
            if (!FormulaCoercion.TryCoerceToNumber(value, context.EvaluationContext.Workbook.Settings,
                    out number, out error))
                return false;
            if (!double.IsFinite(number))
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            return true;
        }

        // Check floating-point bounds before any narrowing conversion. In particular,
        // invalid shifts must never reach C#'s masked machine-shift implementation.
        public static bool TryInteger(FormulaFunctionContext context, FormulaValue value,
            double minimum, double maximum, bool truncate, out long integer, out FormulaError error)
        {
            integer = 0;
            if (!TryFinite(context, value, out var number, out error)) return false;
            var integral = Math.Truncate(number);
            if ((!truncate && number != integral) || integral < minimum || integral > maximum)
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            integer = (long)integral;
            return true;
        }
    }
}
