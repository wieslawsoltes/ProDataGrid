// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    [Collection(FormulaAllocationCollection.Name)]
    public sealed class ExcelTrimMeanTests
    {
        [Theory]
        [InlineData("TRIMMEAN({1;2;3;4;100},0.4)", 3d)]
        [InlineData("TRIMMEAN({1;2;3;4;100},0)", 22d)]
        [InlineData("TRIMMEAN({1;2;3;4;100},0.39)", 22d)]
        [InlineData("TRIMMEAN({1;2;3;4;100},0.8)", 3d)]
        [InlineData("TRIMMEAN({1;2;3;4;100},1)", 3d)]
        [InlineData("TRIMMEAN({1;2;3;100},0.5)", 2.5d)]
        [InlineData("TRIMMEAN({1;2;3;100},0.99)", 2.5d)]
        [InlineData("TRIMMEAN({1;2;3;100},A1)", 26.5d)]
        [InlineData("TRIMMEAN({1;2;3;100},\"0.5\")", 2.5d)]
        [InlineData("TRIMMEAN({1;2;3;100},FALSE)", 26.5d)]
        [InlineData("TRIMMEAN({1;2;3},TRUE)", 2d)]
        [InlineData("TRIMMEAN({1,2;3,100},0.5)", 2.5d)]
        [InlineData("TRIMMEAN({1;\"99\";TRUE;3},0)", 2d)]
        [InlineData("TRIMMEAN(42,0.5)", 42d)]
        [InlineData("TRIMMEAN({-1E308;1;1E308},0)", 0.3333333333333333d)]
        [InlineData("TRIMMEAN({1E308;1E308},0)", 1E308)]
        [InlineData("SUM(TRIMMEAN({1;2;3;4;100},{0;0.4;0.8}))", 28d)]
        public void Examples_Agree_Across_Evaluation_And_Formatting(string formula, double expected)
        {
            var context = Context();
            var parser = new ExcelFormulaParser();
            var expression = parser.Parse(formula, new FormulaParseOptions());
            var formatted = new ExcelFormulaFormatter().Format(expression, new FormulaFormatOptions());
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var evaluator = new FormulaEvaluator();
                AssertBits(expected, evaluator.Evaluate(expression, context, new WorkbookValueResolver()).AsNumber());
                AssertBits(expected, evaluator.Evaluate(parser.Parse(formatted, new FormulaParseOptions()), context, new WorkbookValueResolver()).AsNumber());
            }
        }

        [Theory]
        [InlineData("TRIMMEAN({1;2},1)", FormulaErrorType.Num)]
        [InlineData("TRIMMEAN({1;2},-0.1)", FormulaErrorType.Num)]
        [InlineData("TRIMMEAN({1;2},1.1)", FormulaErrorType.Num)]
        [InlineData("TRIMMEAN(A1:A3,0)", FormulaErrorType.Num)]
        [InlineData("TRIMMEAN({\"a\";TRUE},0)", FormulaErrorType.Num)]
        [InlineData("TRIMMEAN({1;2},\"bad\")", FormulaErrorType.Value)]
        [InlineData("TRIMMEAN({1;2},NA())", FormulaErrorType.NA)]
        [InlineData("TRIMMEAN(HSTACK(1,NA()),0)", FormulaErrorType.NA)]
        [InlineData("TRIMMEAN(1)", FormulaErrorType.Value)]
        [InlineData("TRIMMEAN(1,0,2)", FormulaErrorType.Value)]
        public void Invalid_Arguments_Are_Formula_Errors(string formula, FormulaErrorType expected)
        {
            var context = Context();
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new WorkbookValueResolver());
                Assert.Equal(expected, result.AsError().Type);
            }
        }

        [Fact]
        public void Documented_Eleven_Value_Example_And_Precision_Settings()
        {
            var context = Context();
            var data = Values(4,5,6,7,2,3,4,5,1,2,3);
            AssertBits(34d / 9, Invoke(context, data, FormulaValue.FromNumber(0.2)).AsNumber());
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 4;
            Assert.Equal(3.778, Invoke(context, data, FormulaValue.FromNumber(0.2)).AsNumber());
        }

        [Fact]
        public void Exact_Mean_Matches_Independent_Integer_Rational_Oracle()
        {
            var random = new Random(337771);
            var context = Context();
            var edges = new[] { 0d, -0d, double.Epsilon, -double.Epsilon, 2*double.Epsilon, -3*double.Epsilon,
                double.MaxValue, double.MinValue, 1E308, -1E308, 1d, -1d, 1E-308, -1E-308 };
            for (var attempt = 0; attempt < 800; attempt++)
            {
                var count = random.Next(1, 97);
                var data = new double[count];
                for (var i = 0; i < data.Length; i++)
                {
                    var bits = unchecked((ulong)random.NextInt64());
                    if (((bits >> 52) & 2047) == 2047) bits ^= 1UL << 52;
                    if ((i & 1) != 0) bits |= 1UL << 63;
                    data[i] = attempt % 3 == 0 ? edges[random.Next(edges.Length)] : BitConverter.Int64BitsToDouble(unchecked((long)bits));
                }
                var input = Values(data);
                var percentages = new FormulaArray(5, 1);
                var numerators = new[] { 0, 4, 8, 12, random.Next(16) };
                for (var i = 0; i < numerators.Length; i++) percentages[i, 0] = FormulaValue.FromNumber(numerators[i] / 16d);
                var batch = Invoke(context, input, FormulaValue.FromArray(percentages)).AsArray();
                Array.Sort(data);
                for (var i = 0; i < numerators.Length; i++)
                {
                    // Percentages are exact binary fractions; determine the retained subset
                    // using integer arithmetic, independently from production floating-point code.
                    var tail = count * numerators[i] / 32;
                    var expected = RationalMean(data, tail, count - 2 * tail);
                    AssertBits(expected, batch[i, 0].AsNumber());
                    AssertBits(expected, Invoke(context, input, percentages[i, 0]).AsNumber());
                }
            }
        }

        [Fact]
        public void Cancellation_Subnormal_Ties_And_Extreme_Values_Are_Rounded_Once()
        {
            var context = Context();
            var samples = new[] {
                new[] { double.MaxValue, double.MaxValue }, new[] { double.MinValue, double.MinValue },
                new[] { 1E308, 1d, -1E308 }, new[] { 1E308, 1E-308, -1E308 },
                new[] { 0d, double.Epsilon }, new[] { double.Epsilon, 2*double.Epsilon },
                new[] { 0d, -double.Epsilon }, new[] { -double.Epsilon, -2*double.Epsilon },
                new[] { 1d, Math.BitIncrement(1d) }, new[] { Math.BitIncrement(1d), Math.BitIncrement(Math.BitIncrement(1d)) },
                new[] { double.MaxValue, double.Epsilon, double.MinValue }, new[] { -0d, -0d }
            };
            foreach (var sample in samples)
            {
                var expected = RationalMean(sample, 0, sample.Length);
                AssertBits(expected, Invoke(context, Values(sample), FormulaValue.FromNumber(0)).AsNumber());
                Array.Reverse(sample);
                AssertBits(expected, Invoke(context, Values(sample), FormulaValue.FromNumber(0)).AsNumber());
            }
        }

        [Fact]
        public void Batch_Percentages_Retain_Order_Duplicates_Shapes_And_Element_Errors()
        {
            var context = Context();
            var percentages = new FormulaArray(2, 3);
            percentages[0, 0] = FormulaValue.FromNumber(0.8);
            percentages[0, 1] = FormulaValue.FromNumber(0);
            percentages[0, 2] = FormulaValue.FromNumber(0.4);
            percentages[1, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA, "target"));
            percentages[1, 1] = FormulaValue.FromNumber(0.4);
            percentages[1, 2] = FormulaValue.FromNumber(2);
            var result = Invoke(context, Values(1,2,3,4,100), FormulaValue.FromArray(percentages)).AsArray();
            Assert.Equal(2, result.RowCount); Assert.Equal(3, result.ColumnCount); Assert.Null(result.Origin);
            Assert.Equal(3, result[0,0].AsNumber()); Assert.Equal(22, result[0,1].AsNumber()); Assert.Equal(3, result[0,2].AsNumber());
            Assert.Equal(percentages[1,0], result[1,0]); Assert.Equal(3, result[1,1].AsNumber()); Assert.Equal(FormulaErrorType.Num, result[1,2].AsError().Type);
            Assert.Equal(0.4, percentages[0,2].AsNumber());
            Assert.Equal(FormulaValueKind.Array, Invoke(context, Values(1,2,3), Values(0)).Kind);
        }

        [Fact]
        public void Masks_And_Source_Errors_Remain_Owned_By_The_Host()
        {
            var context = Context();
            var array = new FormulaArray(5, 1, sparse: true);
            var failure = new FormulaError(FormulaErrorType.Ref, "host error");
            array.SetValue(0, 0, FormulaValue.FromError(failure), present: false);
            array[1, 0] = FormulaValue.FromNumber(100); array[2, 0] = FormulaValue.FromNumber(1);
            array[3, 0] = FormulaValue.FromText("999"); array[4, 0] = FormulaValue.FromNumber(2);
            Assert.Equal(2, Invoke(context, FormulaValue.FromArray(array), FormulaValue.FromNumber(0.75)).AsNumber());
            Assert.Equal(100, array[1, 0].AsNumber()); Assert.False(array.IsPresent(0,0));
            array.SetValue(0, 0, FormulaValue.FromError(failure), present: true);
            Assert.Equal(failure, Invoke(context, FormulaValue.FromArray(array), FormulaValue.FromNumber(0)).AsError());
        }

        [Fact]
        public void Physical_Input_Output_And_Nonfinite_Arguments_Are_Bounded()
        {
            var context = Context();
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                Assert.Equal(FormulaErrorType.Num, Invoke(context, Values(value), FormulaValue.FromNumber(0)).AsError().Type);
                Assert.Equal(FormulaErrorType.Num, Invoke(context, Values(1), FormulaValue.FromNumber(value)).AsError().Type);
            }
            context.Workbook.Settings.MaximumArrayCellCount = 3;
            Assert.Equal(FormulaErrorType.Num, Invoke(context, Values(1,2,3,4), FormulaValue.FromNumber(0)).AsError().Type);
            Assert.Equal(FormulaErrorType.Num, Invoke(context, Values(1,2,3), Values(0,0,0,0)).AsError().Type);
        }

        [Fact]
        public void Concurrent_Pools_Error_Recovery_And_Source_Evaluation_Counts()
        {
            var registry = new ExcelFunctionRegistry();
            var counted = new CountedSource(); registry.Register(counted);
            var context = Context(registry);
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                counted.Calls = 0;
                new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse("TRIMMEAN(SOURCE(),{0;0.5;0.8})", new FormulaParseOptions()), context, new WorkbookValueResolver());
                Assert.Equal(1, counted.Calls);
            }
            var numbers = new double[257];
            for (var i = 0; i < numbers.Length; i++) numbers[i] = i;
            var source = Values(numbers);
            Parallel.For(0, 64, i =>
            {
                Assert.Equal(FormulaErrorType.Value, Invoke(context, source, FormulaValue.FromText("invalid")).AsError().Type);
                Assert.Equal(128, Invoke(context, source, FormulaValue.FromNumber((i % 16) / 16d)).AsNumber());
            });
        }

        [Fact]
        public void Changes_To_Data_And_Percentages_Recalculate_The_Actual_Spill()
        {
            var context = Context(); var sheet = context.Worksheet;
            var values = new[] { 1d,2,3,4,100 };
            for (var i=0;i<values.Length;i++) sheet.GetCell(i+1,1).Value = FormulaValue.FromNumber(values[i]);
            sheet.GetCell(1,2).Value = FormulaValue.FromNumber(0);
            sheet.GetCell(2,2).Value = FormulaValue.FromNumber(0.4);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), context.FunctionRegistry);
            engine.SetCellFormula(sheet,1,4,"TRIMMEAN(A1:A5,B1:B2)");
            engine.Recalculate(context.Workbook,new[]{new FormulaCellAddress("Sheet1",1,4)});
            Assert.Equal(22,sheet.GetCell(1,4).Value.AsArray()[0,0].AsNumber()); Assert.Equal(3,sheet.GetCell(2,4).Value.AsNumber());
            sheet.GetCell(5,1).Value = FormulaValue.FromNumber(5);
            engine.Recalculate(context.Workbook,new[]{new FormulaCellAddress("Sheet1",5,1)});
            Assert.Equal(3,sheet.GetCell(1,4).Value.AsArray()[0,0].AsNumber()); Assert.Equal(3,sheet.GetCell(2,4).Value.AsNumber());
        }

        [Fact]
        public void Warm_Scalar_Uses_No_Managed_Workspace_And_Batch_Only_Owns_Result()
        {
            var context = Context();
            var numbers = new double[1000]; for (var i=0;i<numbers.Length;i++) numbers[i]=i;
            var source = Values(numbers);
            Assert.True(context.FunctionRegistry.TryGetFunction("TRIMMEAN",out var function));
            var call = new FormulaFunctionContext(context);
            var scalarArgs = new[]{source,FormulaValue.FromNumber(0.2)};
            var batchArgs = new[]{source,Values(0,0.25,0.5,0.75)};
            var scalar = new Func<FormulaValue>(()=>function.Invoke(call,scalarArgs));
            var batch = new Func<FormulaValue>(()=>function.Invoke(call,batchArgs));
            var output = new Func<FormulaValue>(()=>FormulaValue.FromArray(new FormulaArray(4,1)));
            for(var i=0;i<100;i++){scalar();batch();output();}
            Assert.Equal(0,Allocation(scalar));
            var expected=Allocation(output); var actual=Allocation(batch); Assert.Equal(expected,actual);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Allocation(Func<FormulaValue> operation)
        {
            var start=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++)
            {
                var value=operation();
                if(value.Kind==FormulaValueKind.Error) throw new InvalidOperationException("Unexpected allocation-test error");
            }
            return GC.GetAllocatedBytesForCurrentThread()-start;
        }

        // Independent oracle: exact BigInteger sums, then a binary search over ordered
        // representable doubles. No production accumulator or bit-rounding helper is used.
        private static double RationalMean(double[] values,int first,int count)
        {
            BigInteger sum=0;
            for(var i=first;i<first+count;i++) sum+=Units(values[i]);
            if(sum.IsZero)return 0;
            var negative=sum.Sign<0;sum=BigInteger.Abs(sum);
            ulong low=0,high=0x7fefffffffffffffUL;
            while(low<high)
            {
                var mid=low+(high-low+1)/2;
                if(Units(BitConverter.Int64BitsToDouble((long)mid))*count<=sum)low=mid;else high=mid-1;
            }
            var selected=low;
            if(low<0x7fefffffffffffffUL)
            {
                var below=sum-Units(BitConverter.Int64BitsToDouble((long)low))*count;
                var above=Units(BitConverter.Int64BitsToDouble((long)(low+1)))*count-sum;
                if(above<below||above==below&&(low&1)!=0)selected++;
            }
            if(negative)selected|=1UL<<63;
            return BitConverter.Int64BitsToDouble(unchecked((long)selected));
        }
        private static BigInteger Units(double value)
        {
            var bits=unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            var exponent=(int)((bits>>52)&2047);var significand=bits&0x000fffffffffffffUL;
            if(exponent!=0)significand|=1UL<<52;
            BigInteger number=new BigInteger(significand)<<Math.Max(0,exponent-1);
            return(bits>>63)==0?number:-number;
        }
        private static FormulaValue Values(params double[] values)
        {
            var array=new FormulaArray(values.Length,1);for(var i=0;i<values.Length;i++)array[i,0]=FormulaValue.FromNumber(values[i]);
            return FormulaValue.FromArray(array);
        }
        private static void AssertBits(double expected,double actual)=>Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(actual));
        private static FormulaEvaluationContext Context(ExcelFunctionRegistry? registry=null)
        {
            var workbook=new TestWorkbook("Book1");workbook.Settings.ApplyNumberPrecision=false;
            return new FormulaEvaluationContext(workbook,workbook.GetWorksheet("Sheet1"),new FormulaCellAddress("Sheet1",1,1),registry??new ExcelFunctionRegistry());
        }
        private static FormulaValue Invoke(FormulaEvaluationContext context,params FormulaValue[] args)
        {
            Assert.True(context.FunctionRegistry.TryGetFunction("TRIMMEAN",out var function));return function.Invoke(new FormulaFunctionContext(context),args);
        }
        private sealed class CountedSource:IFormulaFunction
        {
            public int Calls;public string Name=>"SOURCE";public FormulaFunctionInfo Info{get;}=new FormulaFunctionInfo(0,0);
            public FormulaValue Invoke(FormulaFunctionContext context,IReadOnlyList<FormulaValue> args){Calls++;return Values(1,2,3,4,100);}
        }
    }
}
