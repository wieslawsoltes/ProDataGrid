// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProDataGrid.FormulaEngine;

namespace ProDataGrid.FormulaEngine.Excel
{
    internal sealed class FactorialFunction : ExcelScalarFunctionBase
    {
        private readonly bool _doubleFactorial;
        public FactorialFunction(bool doubleFactorial) : base(doubleFactorial ? "FACTDOUBLE" : "FACT", 1, 1)
        {
            _doubleFactorial = doubleFactorial;
        }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var number, out var error)) return FormulaValue.FromError(error);
            if (number < 0) return ExcelNumericUtilities.NumError();
            var n = Math.Truncate(number);
            // Every larger nonnegative integer necessarily overflows binary64. Bound before converting/looping.
            if (n > (_doubleFactorial ? 300 : 170)) return ExcelNumericUtilities.NumError();
            var product = 1d;
            for (var i = (int)n; i > 1; i -= _doubleFactorial ? 2 : 1) product *= i;
            return ExcelNumericUtilities.Number(context, product);
        }
    }

    internal enum ExcelCombinatorialOperation { Combination, Permutation, RepeatedPermutation }

    internal sealed class CombinatorialFunction : ExcelScalarFunctionBase
    {
        private readonly ExcelCombinatorialOperation _operation;
        public CombinatorialFunction(string name, ExcelCombinatorialOperation operation) : base(name, 2, 2)
        {
            _operation = operation;
        }
        protected override FormulaValue InvokeScalar(FormulaFunctionContext context, in ExcelScalarArguments args)
        {
            if (!ExcelNumericUtilities.TryFinite(context, args[0], out var n, out var error) ||
                !ExcelNumericUtilities.TryFinite(context, args[1], out var k, out error)) return FormulaValue.FromError(error);
            if (n < 0 || k < 0) return ExcelNumericUtilities.NumError();
            n = Math.Truncate(n);
            k = Math.Truncate(k);
            if (_operation == ExcelCombinatorialOperation.RepeatedPermutation)
            {
                if (n == 0 && k > 0) return ExcelNumericUtilities.NumError();
                return ExcelNumericUtilities.Number(context, k == 0 ? 1 : Math.Pow(n, k));
            }
            if (k > n || (_operation == ExcelCombinatorialOperation.Permutation && n == 0)) return ExcelNumericUtilities.NumError();
            var result = 1d;
            if (_operation == ExcelCombinatorialOperation.Permutation)
            {
                if (k > 170) return ExcelNumericUtilities.NumError(); // nPk >= k!
                for (var i = 0; i < (int)k; i++)
                {
                    result *= n - i;
                    if (!double.IsFinite(result)) return ExcelNumericUtilities.NumError();
                }
            }
            else
            {
                k = Math.Min(k, n - k);
                if (k >= 1024) return ExcelNumericUtilities.NumError(); // nCk >= 2^k when k <= n/2
                for (var i = 1; i <= (int)k; i++)
                {
                    // Divide the factor first: factorials and intermediate products can overflow
                    // even when the combination itself is representable.
                    result *= (n - k + i) / i;
                    if (!double.IsFinite(result)) return ExcelNumericUtilities.NumError();
                }
                if (result <= 9007199254740992d) result = Math.Round(result);
            }
            return ExcelNumericUtilities.Number(context, result);
        }
    }
}
