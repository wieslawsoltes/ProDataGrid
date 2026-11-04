// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Numerics;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class ExcelCombinatorialTests
    {
        [Fact]
        public void Combinations_And_Permutations_Agree_With_Exact_Integer_Oracles()
        {
            var context = ExcelScientificFunctionTests.Context();
            for (var n = 1; n <= 80; n++)
            {
                BigInteger choose = 1;
                BigInteger permutation = 1;
                for (var k = 0; k <= n; k++)
                {
                    if (k > 0)
                    {
                        choose = choose * (n - k + 1) / k;
                        permutation *= n - k + 1;
                    }
                    var args = new[] { FormulaValue.FromNumber(n), FormulaValue.FromNumber(k) };
                    ExcelScientificFunctionTests.Close((double)choose, ExcelScientificFunctionTests.Invoke(context, "COMBIN", args).AsNumber());
                    ExcelScientificFunctionTests.Close((double)permutation, ExcelScientificFunctionTests.Invoke(context, "PERMUT", args).AsNumber());
                    ExcelScientificFunctionTests.Close((double)BigInteger.Pow(n, k), ExcelScientificFunctionTests.Invoke(context, "PERMUTATIONA", args).AsNumber());
                }
            }
        }

        [Fact]
        public void Factorial_Boundaries_Agree_With_Exact_Integer_Products()
        {
            var context = ExcelScientificFunctionTests.Context();
            BigInteger factorial = 1;
            for (var n = 0; n <= 300; n++)
            {
                if (n > 0) factorial *= n;
                var result = ExcelScientificFunctionTests.Invoke(context, "FACT", FormulaValue.FromNumber(n));
                if (n <= 170) ExcelScientificFunctionTests.Close((double)factorial, result.AsNumber());
                else Assert.Equal(FormulaErrorType.Num, result.AsError().Type);
                BigInteger doubleFactorial = 1;
                for (var i = n; i > 1; i -= 2) doubleFactorial *= i;
                ExcelScientificFunctionTests.Close((double)doubleFactorial,
                    ExcelScientificFunctionTests.Invoke(context, "FACTDOUBLE", FormulaValue.FromNumber(n)).AsNumber());
            }
        }

        [Fact]
        public void Large_Combinations_Avoid_Factorial_Overflow_And_Iteration_Counts_Are_Bounded()
        {
            var context = ExcelScientificFunctionTests.Context();
            BigInteger exact = 1;
            for (var k = 1; k <= 500; k++) exact = exact * (1000 - k + 1) / k;
            ExcelScientificFunctionTests.Close((double)exact, ExcelScientificFunctionTests.Invoke(context, "COMBIN",
                FormulaValue.FromNumber(1000), FormulaValue.FromNumber(500)).AsNumber());
            Assert.Equal(1, ExcelScientificFunctionTests.Invoke(context, "COMBIN",
                FormulaValue.FromNumber(1e100), FormulaValue.FromNumber(1e100)).AsNumber());
            Assert.Equal(FormulaErrorType.Num, ExcelScientificFunctionTests.Invoke(context, "COMBIN",
                FormulaValue.FromNumber(1e100), FormulaValue.FromNumber(1e90)).AsError().Type);
        }
    }
}
