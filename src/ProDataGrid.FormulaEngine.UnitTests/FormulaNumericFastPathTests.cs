// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Globalization;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaNumericFastPathTests
    {
        private static readonly FormulaBinaryOperator[] Operators = {
            FormulaBinaryOperator.Add, FormulaBinaryOperator.Subtract, FormulaBinaryOperator.Multiply,
            FormulaBinaryOperator.Divide, FormulaBinaryOperator.Power, FormulaBinaryOperator.Equal,
            FormulaBinaryOperator.NotEqual, FormulaBinaryOperator.Less, FormulaBinaryOperator.LessOrEqual,
            FormulaBinaryOperator.Greater, FormulaBinaryOperator.GreaterOrEqual };

        [Theory]
        [InlineData(false, 15)]
        [InlineData(true, 8)]
        [InlineData(true, 15)]
        public void Numeric_Operators_Preserve_General_Coercion_Results(bool precision, int digits)
        {
            var context = Context();
            var settings = context.Workbook.Settings;
            settings.ApplyNumberPrecision = precision;
            settings.NumberPrecisionDigits = digits;
            var resolver = new PairResolver();
            var evaluator = new FormulaEvaluator();
            var expressions = new FormulaExpression[Operators.Length];
            for (var i = 0; i < expressions.Length; i++)
                expressions[i] = new FormulaBinaryExpression(Operators[i], new FormulaNameExpression("lhs"), new FormulaNameExpression("rhs"));
            var random = new Random(197403);
            var edge = new[] { 0d, -0d, 1d, -1d, double.Epsilon, -double.Epsilon, 2 * double.Epsilon,
                double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue,
                double.MinValue, 1e-10, 1.2345678901234567, -123456.78901234567 };
            foreach (var compiled in new[] { true, false })
            {
                settings.EnableCompiledExpressions = compiled;
                for (var pair = 0; pair < 800; pair++)
                {
                    var a = pair < edge.Length * edge.Length ? edge[pair / edge.Length] : (random.NextDouble() - 0.5) * 1e6;
                    var b = pair < edge.Length * edge.Length ? edge[pair % edge.Length] : (random.NextDouble() - 0.5) * 1e3;
                    resolver.Left = FormulaValue.FromNumber(a);
                    resolver.Right = FormulaValue.FromNumber(b);
                    for (var i = 0; i < Operators.Length; i++)
                    {
                        var expected = Reference(Operators[i], resolver.Left, resolver.Right, settings);
                        var actual = evaluator.Evaluate(expressions[i], context, resolver);
                        AssertSameValue(expected, actual);
                    }
                }
            }
        }

        [Theory]
        [InlineData("\"1,25\"+2", 3.25)]
        [InlineData("TRUE+2", 3)]
        [InlineData("FALSE*99", 0)]
        [InlineData("IFERROR(1/0,12)", 12)]
        [InlineData("IF(TRUE,7,1/0)", 7)]
        public void Nonnumeric_Coercion_And_Lazy_Evaluation_Retain_Fallbacks(string formula, double expected)
        {
            var context = Context();
            context.Workbook.Settings.Culture = CultureInfo.GetCultureInfo("pl-PL");
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var result = new FormulaEvaluator().Evaluate(new ExcelFormulaParser().Parse(formula, new FormulaParseOptions()), context, new PairResolver());
                Assert.Equal(expected, result.AsNumber());
            }
        }

        [Fact]
        public void Concatenation_Does_Not_Apply_Numeric_Precision_To_Named_Values()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = true;
            context.Workbook.Settings.NumberPrecisionDigits = 3;
            var resolver = new PairResolver { Left = FormulaValue.FromNumber(1.23456), Right = FormulaValue.FromNumber(7.89012) };
            var expression = new FormulaBinaryExpression(FormulaBinaryOperator.Concat, new FormulaNameExpression("lhs"), new FormulaNameExpression("rhs"));
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                Assert.Equal("1.234567.89012", new FormulaEvaluator().Evaluate(expression, context, resolver).AsText());
            }
        }

        [Fact]
        public void Numeric_Array_Operations_Preserve_Masks_Errors_And_Source_Values()
        {
            var context = Context();
            context.Workbook.Settings.ApplyNumberPrecision = false;
            var origin = new FormulaCellAddress("Sheet1", 2, 3);
            var source = new FormulaArray(4, 1, origin, sparse: true);
            source[0, 0] = FormulaValue.FromNumber(4);
            source[2, 0] = FormulaValue.FromError(new FormulaError(FormulaErrorType.NA));
            source[3, 0] = FormulaValue.FromText("5");
            var expression = new FormulaBinaryExpression(FormulaBinaryOperator.Add,
                new FormulaLiteralExpression(FormulaValue.FromArray(source)), new FormulaLiteralExpression(FormulaValue.FromNumber(2)));
            foreach (var compiled in new[] { true, false })
            {
                context.Workbook.Settings.EnableCompiledExpressions = compiled;
                var result = new FormulaEvaluator().Evaluate(expression, context, new PairResolver()).AsArray();
                Assert.Equal(origin, result.Origin);
                Assert.Equal(6, result[0, 0].AsNumber());
                Assert.False(result.IsPresent(1, 0));
                Assert.Equal(FormulaErrorType.NA, result[2, 0].AsError().Type);
                Assert.Equal(7, result[3, 0].AsNumber());
                Assert.Equal(4, source[0, 0].AsNumber());
                Assert.False(source.IsPresent(1, 0));
            }
        }

        private static FormulaValue Reference(FormulaBinaryOperator op, FormulaValue left, FormulaValue right, FormulaCalculationSettings settings)
        {
            Assert.True(FormulaCoercion.TryCoerceToNumber(left, settings, out var a, out _));
            Assert.True(FormulaCoercion.TryCoerceToNumber(right, settings, out var b, out _));
            var comparison = a.CompareTo(b);
            switch (op)
            {
                case FormulaBinaryOperator.Equal: return FormulaValue.FromBoolean(comparison == 0);
                case FormulaBinaryOperator.NotEqual: return FormulaValue.FromBoolean(comparison != 0);
                case FormulaBinaryOperator.Less: return FormulaValue.FromBoolean(comparison < 0);
                case FormulaBinaryOperator.LessOrEqual: return FormulaValue.FromBoolean(comparison <= 0);
                case FormulaBinaryOperator.Greater: return FormulaValue.FromBoolean(comparison > 0);
                case FormulaBinaryOperator.GreaterOrEqual: return FormulaValue.FromBoolean(comparison >= 0);
            }
            if (op == FormulaBinaryOperator.Divide && Math.Abs(b) <= double.Epsilon)
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Div0));
            var result = op switch {
                FormulaBinaryOperator.Add => a + b, FormulaBinaryOperator.Subtract => a - b,
                FormulaBinaryOperator.Multiply => a * b, FormulaBinaryOperator.Divide => a / b,
                FormulaBinaryOperator.Power => Math.Pow(a, b), _ => throw new InvalidOperationException()
            };
            return FormulaValue.FromNumber(settings.ApplyNumberPrecision ? FormulaNumberUtilities.ApplyPrecision(result, settings.NumberPrecisionDigits) : result);
        }

        private static void AssertSameValue(FormulaValue expected, FormulaValue actual)
        {
            Assert.Equal(expected, actual);
            if (expected.Kind == FormulaValueKind.Number && !double.IsNaN(expected.AsNumber()))
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected.AsNumber()), BitConverter.DoubleToInt64Bits(actual.AsNumber()));
        }

        private static FormulaEvaluationContext Context()
        {
            var workbook = new TestWorkbook("Book1");
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), new ExcelFunctionRegistry());
        }

        private sealed class PairResolver : IFormulaValueResolver
        {
            public FormulaValue Left { get; set; }
            public FormulaValue Right { get; set; }
            public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value)
            {
                value = name == "lhs" ? Left : Right;
                return name == "lhs" || name == "rhs";
            }
            public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value)
            {
                value = FormulaValue.Blank;
                return false;
            }
        }
    }
}
