// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using ProDataGrid.FormulaEngine.Excel;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    public sealed class FormulaLexicalBoundaryTests
    {
        [Fact]
        public void Omitted_Arguments_Preserve_The_Existing_Literal_Api()
        {
            var call = (FormulaFunctionCallExpression)Parse("IF(TRUE,,A1)");
            var omitted = Assert.IsType<FormulaLiteralExpression>(call.Arguments[1]);
            Assert.True(omitted.IsOmitted);
            Assert.Equal(FormulaValue.Blank, omitted.Value);
            Assert.False(new FormulaLiteralExpression(FormulaValue.Blank).IsOmitted);
            Assert.Throws<ArgumentException>(() => new FormulaLiteralExpression(FormulaValue.FromNumber(1), true));
        }

        [Fact]
        public void Invocation_Arguments_Are_Snapshotted_And_Validated()
        {
            var first = new FormulaLiteralExpression(FormulaValue.FromNumber(1));
            var args = new FormulaExpression[] { first };
            var invocation = new FormulaInvocationExpression(Parse("LAMBDA(x,x)"), args);
            args[0] = Parse("5");
            Assert.Same(first, invocation.Arguments[0]);
            Assert.Throws<ArgumentNullException>(() => new FormulaInvocationExpression(null!, args));
            Assert.Throws<ArgumentNullException>(() => new FormulaInvocationExpression(first, null!));
            Assert.Throws<ArgumentException>(() => new FormulaInvocationExpression(first, new FormulaExpression[] { null! }));
        }

        [Theory]
        [InlineData(253, false)]
        [InlineData(254, true)]
        public void Lambda_Parameter_Limits_Are_Enforced(int count, bool error)
        {
            var text = new StringBuilder("LAMBDA(");
            for (var i = 0; i < count; i++) text.Append("_p").Append(i).Append(',');
            text.Append("_p0)(");
            for (var i = 0; i < count; i++) { if (i > 0) text.Append(','); text.Append(i); }
            text.Append(')');
            var workbook = new TestWorkbook("Book1");
            var context = Context(workbook, new ExcelFunctionRegistry());
            var result = new FormulaEvaluator().Evaluate(Parse(text.ToString()), context, new WorkbookValueResolver());
            if (error) Assert.Equal(FormulaErrorType.Value, result.AsError().Type);
            else Assert.Equal(0, result.AsNumber());
        }

        [Fact]
        public void Registered_Functions_Take_Precedence_Over_Defined_Names_In_Dependency_Analysis()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Names.SetExpression("SUM", Parse("C1"));
            var registry = new ExcelFunctionRegistry();
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            var sheet = workbook.GetWorksheet("Sheet1");
            engine.SetCellFormula(sheet, 1, 3, "SUM(1,2)");
            var address = new FormulaCellAddress("Sheet1", 1, 3);
            Assert.Empty(engine.DependencyGraph.GetDependencies(address));
            Assert.False(engine.Recalculate(workbook, new[] { address }).HasCycle);
            Assert.Equal(3, sheet.GetCell(1, 3).Value.AsNumber());
            Assert.Throws<ArgumentNullException>(() => new FormulaDependencyGraph(null!));
        }

        [Fact]
        public void Local_Callable_Shadowing_Does_Not_Apply_Builtin_Declaration_Rules()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Names.SetExpression("value", Parse("A1"));
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(9);
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), new ExcelFunctionRegistry());
            engine.SetCellFormula(sheet, 1, 3, "LET(LAMBDA,LAMBDA(a,b,a+b),LAMBDA(value,2))");
            var address = new FormulaCellAddress("Sheet1", 1, 3);
            Assert.Contains(new FormulaCellAddress("Sheet1", 1, 1), engine.DependencyGraph.GetDependencies(address));
            engine.Recalculate(workbook, new[] { address });
            Assert.Equal(11, sheet.GetCell(1, 3).Value.AsNumber());
        }

        [Fact]
        public void Replaced_Lexical_Functions_Use_Their_Actual_Metadata()
        {
            var workbook = new TestWorkbook("Book1");
            workbook.Names.SetExpression("value", Parse("A1"));
            var registry = new ExcelFunctionRegistry();
            registry.Register(new OrdinaryLambda());
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            var sheet = workbook.GetWorksheet("Sheet1");
            sheet.GetCell(1, 1).Value = FormulaValue.FromNumber(9);
            engine.SetCellFormula(sheet, 1, 3, "LAMBDA(value,2)");
            var address = new FormulaCellAddress("Sheet1", 1, 3);
            Assert.Contains(new FormulaCellAddress("Sheet1", 1, 1), engine.DependencyGraph.GetDependencies(address));
            engine.Recalculate(workbook, new[] { address });
            Assert.Equal(9, sheet.GetCell(1, 3).Value.AsNumber());
        }

        [Fact]
        public void Callable_Values_Cannot_Leak_Into_A_Worksheet_Spill()
        {
            var workbook = new TestWorkbook("Book1");
            var registry = new ExcelFunctionRegistry();
            var engine = new FormulaCalculationEngine(new ExcelFormulaParser(), registry);
            var sheet = workbook.GetWorksheet("Sheet1");
            engine.SetCellFormula(sheet, 1, 1, "HSTACK(1,LAMBDA(x,x))");
            engine.Recalculate(workbook, new[] { new FormulaCellAddress("Sheet1", 1, 1) });
            Assert.Equal(FormulaErrorType.Calc, sheet.GetCell(1, 1).Value.AsError().Type);
            Assert.Equal(FormulaValueKind.Blank, sheet.GetCell(1, 2).Value.Kind);
        }

        [Fact]
        public void Public_Lambda_Api_Validates_Parameters_And_Omission_Lengths()
        {
            var context = Context(new TestWorkbook("Book1"), new ExcelFunctionRegistry());
            var body = Parse("x");
            Assert.Throws<ArgumentNullException>(() => context.CreateLambda(null!, body));
            Assert.Throws<ArgumentNullException>(() => context.CreateLambda(new[] { "x" }, null!));
            Assert.Throws<ArgumentException>(() => context.CreateLambda(new[] { "x", "X" }, body));
            Assert.Throws<ArgumentException>(() => context.WithLocalValue("", FormulaValue.Blank));
            Assert.Throws<ArgumentNullException>(() => context.IsArgumentOmitted(null!));
            var evaluator = new FormulaEvaluator();
            var lambda = context.CreateLambda(new[] { "x" }, body);
            var result = evaluator.InvokeLambda(lambda, new[] { FormulaValue.Blank }, context,
                new WorkbookValueResolver(), new bool[0]);
            Assert.Equal(FormulaErrorType.Value, result.AsError().Type);
        }

        private static FormulaExpression Parse(string formula) => new ExcelFormulaParser().Parse(formula, new FormulaParseOptions());
        private static FormulaEvaluationContext Context(TestWorkbook workbook, ExcelFunctionRegistry registry)
            => new FormulaEvaluationContext(workbook, workbook.GetWorksheet("Sheet1"), new FormulaCellAddress("Sheet1", 1, 1), registry);

        private sealed class OrdinaryLambda : IFormulaFunction
        {
            public string Name => "LAMBDA";
            public FormulaFunctionInfo Info { get; } = new FormulaFunctionInfo(2, 2);
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args) => args[0];
        }
    }
}
