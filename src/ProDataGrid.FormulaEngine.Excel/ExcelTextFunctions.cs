// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class TextJoinFunction : ExcelFunctionBase
    {
        public TextJoinFunction()
            : base("TEXTJOIN", new FormulaFunctionInfo(3, -1))
        {
        }

        public override FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
        {
            var address = context.EvaluationContext.Address;
            var delimiterValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[0], address);
            if (!ExcelFunctionUtilities.TryCoerceToText(delimiterValue, out var delimiter, out var error))
            {
                return FormulaValue.FromError(error);
            }

            var ignoreEmptyValue = ExcelFunctionUtilities.ApplyImplicitIntersection(args[1], address);
            if (!ExcelFunctionUtilities.TryCoerceToBoolean(ignoreEmptyValue, address, out var ignoreEmpty, out error))
            {
                return FormulaValue.FromError(error);
            }

            var builder = new StringBuilder();
            var first = true;
            for (var i = 2; i < args.Count; i++)
            {
                foreach (var value in ExcelFunctionUtilities.FlattenValues(args[i]))
                {
                    if (value.Kind == FormulaValueKind.Error)
                    {
                        return value;
                    }

                    if (!ExcelFunctionUtilities.TryCoerceToText(value, out var text, out error))
                    {
                        return FormulaValue.FromError(error);
                    }

                    if (ignoreEmpty && text.Length == 0)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        builder.Append(delimiter);
                    }

                    builder.Append(text);
                    first = false;
                }
            }

            return FormulaValue.FromText(builder.ToString());
        }
    }
}
