// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaCompilationSafetyTests
    {
        [Theory]
        [InlineData(50000, false, 1d)]
        [InlineData(20000, true, 20001d)]
        public void Deep_Generated_Expressions_Compile_Without_Clr_Recursion(int depth, bool binary, double expected)
        {
            FormulaExpression expression = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            var one = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            for (var i = 0; i < depth; i++)
            {
                expression = binary
                    ? new FormulaBinaryExpression(FormulaBinaryOperator.Add, expression, one)
                    : new FormulaUnaryExpression(FormulaUnaryOperator.Negate, expression);
            }
            var context = CreateContext(new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            Assert.Equal(expected, evaluator.Evaluate(expression, context, new DictionaryValueResolver()).AsNumber());
        }

        [Fact]
        public void Warm_Compiled_Literal_Does_Not_Allocate_Per_Evaluation()
        {
            var expression = new FormulaLiteralExpression(FormulaValue.FromNumber(42));
            var context = CreateContext(new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            for (var i = 0; i < 100; i++)
            {
                evaluator.Evaluate(expression, context, resolver);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            double total = 0;
            for (var i = 0; i < 1000; i++)
            {
                total += evaluator.Evaluate(expression, context, resolver).AsNumber();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(42000, total);
            // Allow one-time runtime bookkeeping, but not an AST/operand stack per call.
            Assert.True(allocated < 4096, $"Warm 1000-evaluation allocation: {allocated} bytes");
        }

        [Fact]
        public void Registering_Lazy_Replacement_Invalidates_Cached_Eager_Plan()
        {
            var registry = new ExcelFunctionRegistry();
            registry.Register(new ConstantFunction("SWITCHABLE", 11));
            var context = CreateContext(registry);
            var expression = new ExcelFormulaParser().Parse("SWITCHABLE(5)", new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            var telemetry = new FormulaCalculationTelemetry();
            context.Workbook.Settings.CalculationObserver = telemetry;

            Assert.Equal(11, evaluator.Evaluate(expression, context, resolver).AsNumber());
            registry.Register(new LazyConstantFunction());
            Assert.Equal(42, evaluator.Evaluate(expression, context, resolver).AsNumber());
            Assert.Equal(42, evaluator.Evaluate(expression, context, resolver).AsNumber());
            Assert.Equal(2, telemetry.CompiledExpressions);
            Assert.Equal(1, telemetry.CompileCacheHits);
        }

        [Fact]
        public void Registering_Eager_Replacement_Invalidates_Cached_Lazy_Plan()
        {
            var registry = new ExcelFunctionRegistry();
            registry.Register(new LazyConstantFunction());
            var context = CreateContext(registry);
            var expression = new ExcelFormulaParser().Parse("SWITCHABLE(5)", new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            Assert.Equal(42, evaluator.Evaluate(expression, context, resolver).AsNumber());
            registry.Register(new ConstantFunction("SWITCHABLE", 11));
            Assert.Equal(11, evaluator.Evaluate(expression, context, resolver).AsNumber());
        }

        [Fact]
        public void Registry_Revision_Changes_On_Add_And_Replace_But_Not_Failed_Registration()
        {
            var registry = new ExcelFunctionRegistry();
            var revision = ((IFormulaFunctionRegistryVersion)registry).Version;
            registry.Register(new ConstantFunction("CUSTOM", 1));
            Assert.Equal(revision + 1, registry.Version);
            registry.Register(new ConstantFunction("custom", 2));
            Assert.Equal(revision + 2, registry.Version);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
            Assert.Equal(revision + 2, registry.Version);
        }

        [Theory]
        [InlineData("100-(20/4)", 95d)]
        [InlineData("IF(TRUE,3,1/0)", 3d)]
        [InlineData("SUM(1,2,3)*4", 24d)]
        [InlineData("INDEX({10,20;30,40},2,1)", 30d)]
        [InlineData("IFERROR(1/0,19)", 19d)]
        public void Iterative_Compiler_Preserves_Interpreted_Semantics(string formula, double expected)
        {
            var context = CreateContext(new ExcelFunctionRegistry());
            var expression = new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            var compiled = evaluator.Evaluate(expression, context, resolver);
            context.Workbook.Settings.EnableCompiledExpressions = false;
            var interpreted = evaluator.Evaluate(expression, context, resolver);
            Assert.Equal(expected, compiled.AsNumber());
            Assert.Equal(compiled, interpreted);
        }

        [Fact]
        public void Reentrant_Function_Uses_Independent_Pooled_Operand_Stack()
        {
            var registry = new ExcelFunctionRegistry();
            var context = CreateContext(registry);
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            registry.Register(new ReentrantFunction(evaluator, resolver));
            var expression = new ExcelFormulaParser().Parse("3+REENTER()*2", new FormulaParseOptions());
            for (var i = 0; i < 100; i++)
            {
                Assert.Equal(1161, evaluator.Evaluate(expression, context, resolver).AsNumber());
            }
        }

        [Fact]
        public void Shared_Expression_Does_Not_Reuse_Plan_From_A_Different_Registry()
        {
            var first = new ExcelFunctionRegistry();
            var second = new ExcelFunctionRegistry();
            first.Register(new ConstantFunction("SWITCHABLE", 11));
            second.Register(new LazyConstantFunction());
            var expression = new ExcelFormulaParser().Parse("SWITCHABLE(5)", new FormulaParseOptions());
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            Assert.Equal(11, evaluator.Evaluate(expression, CreateContext(first), resolver).AsNumber());
            Assert.Equal(42, evaluator.Evaluate(expression, CreateContext(second), resolver).AsNumber());
            Assert.Equal(11, evaluator.Evaluate(expression, CreateContext(first), resolver).AsNumber());
        }

        private static FormulaEvaluationContext CreateContext(ExcelFunctionRegistry registry)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.EnableCompiledExpressions = true;
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"),
                new FormulaCellAddress("Sheet1", 1, 1), registry);
        }

        private sealed class ConstantFunction : IFormulaFunction
        {
            private readonly double _value;
            public ConstantFunction(string name, double value) { Name = name; _value = value; }
            public string Name { get; }
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(1, 1);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
                => FormulaValue.FromNumber(_value);
        }

        private sealed class LazyConstantFunction : ILazyFormulaFunction
        {
            public string Name => "SWITCHABLE";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(1, 1);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
                => FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> arguments,
                FormulaEvaluator evaluator, IFormulaValueResolver resolver) => FormulaValue.FromNumber(42);
        }

        private sealed class ReentrantFunction : IFormulaFunction
        {
            private readonly FormulaEvaluator _evaluator;
            private readonly IFormulaValueResolver _resolver;
            private readonly FormulaExpression _nested = new ExcelFormulaParser().Parse("123+456", new FormulaParseOptions());
            public ReentrantFunction(FormulaEvaluator evaluator, IFormulaValueResolver resolver)
            {
                _evaluator = evaluator;
                _resolver = resolver;
            }
            public string Name => "REENTER";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
                => _evaluator.Evaluate(_nested, context.EvaluationContext, _resolver);
        }
    }
}
