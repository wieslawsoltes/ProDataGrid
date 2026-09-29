// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class ExcelSignedRadixToDecimalFunction : ExcelScalarFunctionBase
    {
        private readonly int _radix;
        public ExcelSignedRadixToDecimalFunction(string name, int radix) : base(name, 1, 1) { _radix = radix; }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
            => ExcelRadixUtilities.TrySigned(args[0], _radix, out var number, out var error)
                ? ExcelFunctionUtilities.CreateNumber(context, number) : FormulaValue.FromError(error);
    }

    internal sealed class ExcelFixedRadixFunction : ExcelElementwiseFunction
    {
        private readonly int _source;
        private readonly int _target;

        public ExcelFixedRadixFunction(string name, int source, int target) : base(name, 1, 2)
        {
            _source = source;
            _target = target;
        }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            var bits = ExcelRadixUtilities.SignedBits(_target);
            var minimum = -(1L << (bits - 1));
            var maximum = (1L << (bits - 1)) - 1;
            long number;
            FormulaError error;
            if (_source == 10)
            {
                if (!ExcelEngineeringNumbers.TryInteger(context, args[0], minimum, maximum,
                        truncate: true, out number, out error)) return FormulaValue.FromError(error);
            }
            else if (!ExcelRadixUtilities.TrySigned(args[0], _source, out number, out error))
            {
                return FormulaValue.FromError(error);
            }
            if (number < minimum || number > maximum) return ExcelEngineeringNumbers.NumError();

            var places = 0;
            if (!args.IsOmitted(1))
            {
                if (!ExcelEngineeringNumbers.TryInteger(context, args[1], 1, 10,
                        truncate: true, out var width, out error)) return FormulaValue.FromError(error);
                places = (int)width;
            }
            if (number < 0)
            {
                var encoded = unchecked((ulong)number) & ((1UL << bits) - 1);
                return FormulaValue.FromText(ExcelRadixUtilities.Format(encoded, _target, 10));
            }
            var count = ExcelRadixUtilities.DigitCount((ulong)number, _target);
            if (places != 0 && count > places) return ExcelEngineeringNumbers.NumError();
            return FormulaValue.FromText(ExcelRadixUtilities.Format((ulong)number, _target, Math.Max(count, places)));
        }
    }

    internal sealed class ExcelBaseFunction : ExcelScalarFunctionBase
    {
        public ExcelBaseFunction() : base("BASE", 2, 3) { }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelEngineeringNumbers.TryInteger(context, args[0], 0, 9007199254740991d,
                    truncate: true, out var number, out var error) ||
                !ExcelEngineeringNumbers.TryInteger(context, args[1], 2, 36,
                    truncate: true, out var radix, out error)) return FormulaValue.FromError(error);
            long minimumLength = 0;
            if (args.Count > 2 && !ExcelEngineeringNumbers.TryInteger(context, args[2], 0, 255,
                    truncate: true, out minimumLength, out error)) return FormulaValue.FromError(error);
            var width = Math.Max((int)minimumLength, ExcelRadixUtilities.DigitCount((ulong)number, (int)radix));
            return FormulaValue.FromText(ExcelRadixUtilities.Format((ulong)number, (int)radix, width));
        }
    }

    internal sealed class ExcelDecimalFunction : ExcelScalarFunctionBase
    {
        public ExcelDecimalFunction() : base("DECIMAL", 2, 2) { }

        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelEngineeringNumbers.TryInteger(context, args[1], 2, 36,
                    truncate: true, out var radix, out var error)) return FormulaValue.FromError(error);
            return ExcelRadixUtilities.TryUnsignedDouble(args[0], (int)radix, out var number, out error)
                ? ExcelFunctionUtilities.CreateNumber(context, number) : FormulaValue.FromError(error);
        }
    }

    public sealed partial class ExcelFunctionRegistry
    {
        private void RegisterRadixDefaults()
        {
            Register(new ExcelSignedRadixToDecimalFunction("BIN2DEC", 2));
            Register(new ExcelSignedRadixToDecimalFunction("OCT2DEC", 8));
            Register(new ExcelSignedRadixToDecimalFunction("HEX2DEC", 16));
            Register(new ExcelFixedRadixFunction("DEC2BIN", 10, 2));
            Register(new ExcelFixedRadixFunction("DEC2OCT", 10, 8));
            Register(new ExcelFixedRadixFunction("DEC2HEX", 10, 16));
            Register(new ExcelFixedRadixFunction("BIN2OCT", 2, 8));
            Register(new ExcelFixedRadixFunction("BIN2HEX", 2, 16));
            Register(new ExcelFixedRadixFunction("OCT2BIN", 8, 2));
            Register(new ExcelFixedRadixFunction("OCT2HEX", 8, 16));
            Register(new ExcelFixedRadixFunction("HEX2BIN", 16, 2));
            Register(new ExcelFixedRadixFunction("HEX2OCT", 16, 8));
            Register(new ExcelBaseFunction());
            Register(new ExcelDecimalFunction());
        }
    }
}
