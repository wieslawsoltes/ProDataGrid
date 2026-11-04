// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class QuotientFunction : ExcelScalarFunctionBase
    {
        public QuotientFunction() : base("QUOTIENT", 2, 2) { }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var numerator, out var error) ||
                !ExcelNumericUtilities.TryFinite(context, args[1], out var denominator, out error)) return FormulaValue.FromError(error);
            return denominator == 0 ? ExcelNumericUtilities.DivisionError()
                : ExcelNumericUtilities.Number(context, Math.Truncate(numerator / denominator));
        }
    }

    internal sealed class ParityRoundFunction : ExcelScalarFunctionBase
    {
        private readonly bool _odd;
        public ParityRoundFunction(bool odd) : base(odd ? "ODD" : "EVEN", 1, 1) { _odd = odd; }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var number, out var error)) return FormulaValue.FromError(error);
            var magnitude = Math.Abs(number);
            // Beyond binary64's consecutive integer range, adding one is not representable.
            if (magnitude >= 9007199254740992d) return ExcelNumericUtilities.Number(context, number);
            var rounded = _odd ? Math.Ceiling(magnitude) : 2 * Math.Ceiling(magnitude / 2);
            if (_odd && rounded % 2 == 0) rounded++;
            return ExcelNumericUtilities.Number(context, number < 0 ? -rounded : rounded);
        }
    }
}
