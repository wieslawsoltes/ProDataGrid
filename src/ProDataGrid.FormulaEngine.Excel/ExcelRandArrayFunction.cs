// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class RandArrayFunction : ExcelFunctionBase
    {
        public RandArrayFunction() : base("RANDARRAY", new FormulaFunctionInfo(0, 5, isVolatile: true)) { }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var rows = 1;
            var columns = 1;
            var error = default(FormulaError);
            if ((!ExcelArrayShapeUtilities.IsOmitted(args, 0) && !ExcelArrayShapeUtilities.TryInteger(context, args[0], out rows, out error)) ||
                (!ExcelArrayShapeUtilities.IsOmitted(args, 1) && !ExcelArrayShapeUtilities.TryInteger(context, args[1], out columns, out error)))
            {
                return FormulaValue.FromError(error);
            }
            var minimum = 0d;
            var maximum = 1d;
            if ((!ExcelArrayShapeUtilities.IsOmitted(args, 2) && !TryNumber(context, args[2], out minimum, out error)) ||
                (!ExcelArrayShapeUtilities.IsOmitted(args, 3) && !TryNumber(context, args[3], out maximum, out error)))
            {
                return FormulaValue.FromError(error);
            }
            var whole = false;
            if (!ExcelArrayShapeUtilities.IsOmitted(args, 4) &&
                !FormulaCoercion.TryCoerceToBoolean(FormulaCoercion.ApplyImplicitIntersection(args[4], context.EvaluationContext.Address), out whole, out error))
            {
                return FormulaValue.FromError(error);
            }
            if (rows < 1 || columns < 1 || minimum >= maximum)
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Value);
            }
            var lower = Math.Ceiling(minimum);
            var upper = Math.Floor(maximum);
            // Integers outside the exact-double domain cannot all be represented without bias.
            if (whole && (lower > upper || lower < -9007199254740991d || upper > 9007199254740991d))
            {
                return ExcelArrayShapeUtilities.Error(FormulaErrorType.Num);
            }
            if (!ExcelArrayShapeUtilities.TryCreate(context, rows, columns, out var result, out error))
            {
                return FormulaValue.FromError(error);
            }
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    double value;
                    if (whole)
                    {
                        value = Random.Shared.NextInt64((long)lower, (long)upper + 1);
                    }
                    else
                    {
                        var sample = Random.Shared.NextDouble();
                        value = minimum < 0 && maximum > 0
                            ? minimum * (1 - sample) + maximum * sample
                            : minimum + (maximum - minimum) * sample;
                    }
                    result[row, column] = ExcelFunctionUtilities.CreateNumber(context, value);
                }
            }
            return FormulaValue.FromArray(result);
        }

        private static bool TryNumber(FormulaFunctionContext context, FormulaValue value, out double number, out FormulaError error)
        {
            if (!FormulaCoercion.TryCoerceToNumber(FormulaCoercion.ApplyImplicitIntersection(value, context.EvaluationContext.Address),
                    context.EvaluationContext.Workbook.Settings, out number, out error))
            {
                return false;
            }
            if (!double.IsFinite(number))
            {
                error = new FormulaError(FormulaErrorType.Num);
                return false;
            }
            return true;
        }
    }
}
