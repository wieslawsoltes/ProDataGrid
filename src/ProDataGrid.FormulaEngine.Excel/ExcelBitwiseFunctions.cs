// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal enum ExcelBitwiseOperation
    {
        And,
        Or,
        Xor,
        LeftShift,
        RightShift
    }

    internal sealed class ExcelBitwiseFunction : ExcelScalarFunctionBase
    {
        private const ulong Maximum = (1UL << 48) - 1;
        private readonly ExcelBitwiseOperation _operation;

        public ExcelBitwiseFunction(string name, ExcelBitwiseOperation operation) : base(name, 2, 2)
        {
            _operation = operation;
        }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelEngineeringNumbers.TryInteger(context, args[0], 0, Maximum,
                    truncate: false, out var first, out var error))
                return FormulaValue.FromError(error);

            ulong result;
            var number = (ulong)first;
            if (_operation == ExcelBitwiseOperation.LeftShift || _operation == ExcelBitwiseOperation.RightShift)
            {
                if (!ExcelEngineeringNumbers.TryInteger(context, args[1], -53, 53,
                        truncate: false, out var shift, out error))
                    return FormulaValue.FromError(error);
                var left = _operation == ExcelBitwiseOperation.LeftShift;
                if (shift < 0) { shift = -shift; left = !left; }
                var count = (int)shift;
                if (left)
                {
                    // Test the result's range before shifting, including shift counts
                    // larger than the 48-bit value domain and the zero special case.
                    if (number != 0 && (count >= 48 || number > (Maximum >> count)))
                        return ExcelEngineeringNumbers.NumError();
                    result = number == 0 ? 0 : number << count;
                }
                else
                {
                    result = count >= 48 ? 0 : number >> count;
                }
            }
            else
            {
                if (!ExcelEngineeringNumbers.TryInteger(context, args[1], 0, Maximum,
                        truncate: false, out var second, out error))
                    return FormulaValue.FromError(error);
                result = _operation switch
                {
                    ExcelBitwiseOperation.And => number & (ulong)second,
                    ExcelBitwiseOperation.Or => number | (ulong)second,
                    _ => number ^ (ulong)second
                };
            }
            return ExcelFunctionUtilities.CreateNumber(context, result);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterBitwiseDefaults()
        {
            Register(new ExcelBitwiseFunction("BITAND", ExcelBitwiseOperation.And));
            Register(new ExcelBitwiseFunction("BITOR", ExcelBitwiseOperation.Or));
            Register(new ExcelBitwiseFunction("BITXOR", ExcelBitwiseOperation.Xor));
            Register(new ExcelBitwiseFunction("BITLSHIFT", ExcelBitwiseOperation.LeftShift));
            Register(new ExcelBitwiseFunction("BITRSHIFT", ExcelBitwiseOperation.RightShift));
        }
    }
}
