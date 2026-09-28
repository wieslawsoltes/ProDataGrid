// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaConcurrentCompilationTests
    {
        [Fact]
        public void Shared_Evaluator_Publishes_One_Cold_Plan_For_Concurrent_Readers()
        {
            var registry = new ExcelFunctionRegistry();
            var context = Context(registry);
            var observer = new FormulaCalculationTelemetry();
            context.Workbook.Settings.CalculationObserver = observer;
            FormulaExpression expression = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            var one = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            for (var i = 0; i < 10000; i++) expression = new FormulaBinaryExpression(FormulaBinaryOperator.Add, expression, one);
            var evaluator = new FormulaEvaluator();
            var resolver = new DictionaryValueResolver();
            using var ready = new CountdownEvent(8);
            using var start = new ManualResetEventSlim(false);
            var tasks = new Task<FormulaValue>[8];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    ready.Signal();
                    start.Wait();
                    return evaluator.Evaluate(expression, context, resolver);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            var allReady = ready.Wait(TimeSpan.FromSeconds(15));
            start.Set();
            Assert.True(allReady);
            Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(30)));
            foreach (var task in tasks) Assert.Equal(10001, task.Result.AsNumber());
            Assert.Equal(1, observer.CompiledExpressions);
            Assert.Equal(tasks.Length - 1, observer.CompileCacheHits);
        }

        [Fact]
        public void Concurrent_Registry_Identity_Changes_Do_Not_Publish_Incompatible_Plans()
        {
            var first = new ExcelFunctionRegistry();
            var second = new ExcelFunctionRegistry();
            first.Register(new ConstantFunction(17));
            second.Register(new LazyConstantFunction());
            var firstContext = Context(first);
            var secondContext = Context(second);
            var evaluator = new FormulaEvaluator();
            var expression = new ExcelFormulaParser().Parse("CUSTOM()+1", new FormulaParseOptions());
            var resolver = new DictionaryValueResolver();
            Parallel.For(0, 1024, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                var result = evaluator.Evaluate(expression, i % 2 == 0 ? firstContext : secondContext, resolver);
                Assert.Equal(i % 2 == 0 ? 18 : 32, result.AsNumber());
            });
        }

        [Fact]
        public void Compilation_Observer_Can_Reenter_The_Already_Published_Plan()
        {
            var context = Context(new ExcelFunctionRegistry());
            var evaluator = new FormulaEvaluator();
            var expression = new FormulaLiteralExpression(FormulaValue.FromNumber(42));
            var resolver = new DictionaryValueResolver();
            var observer = new ReentrantObserver(() => evaluator.Evaluate(expression, context, resolver));
            context.Workbook.Settings.CalculationObserver = observer;
            Assert.Equal(42, evaluator.Evaluate(expression, context, resolver).AsNumber());
            Assert.Equal(42, observer.Nested.AsNumber());
            Assert.Equal(1, observer.Compilations);
            Assert.Equal(1, observer.CacheHits);
        }

        private static FormulaEvaluationContext Context(ExcelFunctionRegistry registry)
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Settings.ApplyNumberPrecision = false;
            return new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry);
        }

        private sealed class ConstantFunction : IFormulaFunction
        {
            private readonly double _value;
            public ConstantFunction(double value) { _value = value; }
            public string Name => "CUSTOM";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) => FormulaValue.FromNumber(_value);
        }

        private sealed class LazyConstantFunction : ILazyFormulaFunction
        {
            public string Name => "CUSTOM";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(0, 0);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
                => FormulaValue.FromError(new FormulaError(FormulaErrorType.Calc));
            public FormulaValue InvokeLazy(FormulaFunctionContext context, IReadOnlyList<FormulaExpression> args,
                FormulaEvaluator evaluator, IFormulaValueResolver resolver) => FormulaValue.FromNumber(31);
        }

        private sealed class ReentrantObserver : IFormulaCalculationObserver
        {
            private readonly Func<FormulaValue> _evaluate;
            private bool _entered;
            public ReentrantObserver(Func<FormulaValue> evaluate) { _evaluate = evaluate; }
            public FormulaValue Nested { get; private set; }
            public int Compilations { get; private set; }
            public int CacheHits { get; private set; }
            public void OnExpressionCompiled(FormulaExpression expression, int instructionCount, TimeSpan duration, bool fromCache)
            {
                if (fromCache) { CacheHits++; return; }
                Compilations++;
                if (!_entered)
                {
                    _entered = true;
                    Nested = _evaluate();
                }
            }
            public void OnRecalculationStarted(IFormulaWorkbook workbook, IReadOnlyCollection<FormulaCellAddress> dirtyCells) { }
            public void OnRecalculationCompleted(IFormulaWorkbook workbook, IReadOnlyList<FormulaCellAddress> recalculated, IReadOnlyList<FormulaCellAddress> cycle, TimeSpan duration) { }
            public void OnCellEvaluated(FormulaCellAddress address, FormulaValue value, TimeSpan duration) { }
            public void OnExpressionParsed(FormulaCellAddress address, string formulaText, TimeSpan duration) { }
        }
    }
}
