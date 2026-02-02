// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFormulas;
using Avalonia.Data.Core;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Formulas
{
    public sealed class DataGridFormulaModelTests
    {
        [AvaloniaFact]
        public void FormulaModel_Evaluates_Structured_References()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d),
                new("B", 20d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var nameProperty = CreateProperty(nameof(RowItem.Name), row => row.Name, (row, value) => row.Name = value);
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var doubleDefinition = builder.Formula(
                header: "Double",
                formula: "=[@Amount]*2",
                formulaName: "Double",
                configure: column => column.ColumnKey = "Double");

            var totalDefinition = builder.Formula(
                header: "Total",
                formula: "=SUM(SalesTable[Amount])",
                formulaName: "Total",
                configure: column => column.ColumnKey = "Total");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    builder.Text(
                        header: "Name",
                        property: nameProperty,
                        getter: row => row.Name,
                        setter: (row, value) => row.Name = value,
                        configure: column => column.ColumnKey = "Name"),
                    amountDefinition,
                    doubleDefinition,
                    totalDefinition
                },
                AutoGenerateColumns = false
            };

            var model = (DataGridFormulaModel)grid.FormulaModel;
            model.Recalculate();

            Assert.Equal(20d, model.Evaluate(items[0], doubleDefinition));
            Assert.Equal(40d, model.Evaluate(items[1], doubleDefinition));
            Assert.Equal(30d, model.Evaluate(items[0], totalDefinition));
        }

        [AvaloniaFact]
        public void FormulaModel_Resolves_Totals_Scope()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d),
                new("B", 20d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var totalsDefinition = builder.Formula(
                header: "Totals",
                formula: "=SalesTable[[#Totals],[Amount]]",
                formulaName: "Totals",
                configure: column => column.ColumnKey = "Totals");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    amountDefinition,
                    totalsDefinition
                },
                AutoGenerateColumns = false,
                ShowTotalSummary = true
            };

            var amountColumn = grid.ColumnsInternal.First(column => Equals(column.Header, "Amount"));
            amountColumn.Summaries.Add(new DataGridAggregateSummaryDescription
            {
                Aggregate = DataGridAggregateType.Sum
            });

            var model = (DataGridFormulaModel)grid.FormulaModel;
            model.Recalculate();

            Assert.Equal(30d, model.Evaluate(items[0], totalsDefinition));
        }

        [AvaloniaFact]
        public void FormulaModel_TrySetCellFormula_Returns_Error_On_Parse_Failure()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var formulaDefinition = builder.Formula(
                header: "Calc",
                formula: "=[@Amount]*2",
                formulaName: "Calc",
                configure: column => column.ColumnKey = "Calc");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    amountDefinition,
                    formulaDefinition
                },
                AutoGenerateColumns = false
            };

            var model = (DataGridFormulaModel)grid.FormulaModel;
            model.Recalculate();

            var success = model.TrySetCellFormula(items[0], formulaDefinition, "=[@Amount]+", out var error);

            Assert.False(success);
            Assert.False(string.IsNullOrWhiteSpace(error));
            Assert.Equal("#VALUE!", model.Evaluate(items[0], formulaDefinition));
        }

        [AvaloniaFact]
        public void FormulaModel_Resolves_Named_Ranges_In_Formulas()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d),
                new("B", 20d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var formulaDefinition = builder.Formula(
                header: "Calc",
                formula: "=[@Amount]*TaxRate",
                formulaName: "Calc",
                configure: column => column.ColumnKey = "Calc");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    amountDefinition,
                    formulaDefinition
                },
                AutoGenerateColumns = false
            };

            var model = (DataGridFormulaModel)grid.FormulaModel;

            var success = model.TrySetNamedRange("TaxRate", "2", out var error);

            Assert.True(success);
            Assert.True(string.IsNullOrWhiteSpace(error));

            model.Recalculate();

            Assert.Equal(20d, model.Evaluate(items[0], formulaDefinition));
            Assert.Equal(40d, model.Evaluate(items[1], formulaDefinition));
        }

        [AvaloniaFact]
        public void FormulaModel_TrySetNamedRange_Rejects_ColumnName()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    amountDefinition
                },
                AutoGenerateColumns = false
            };

            var model = (DataGridFormulaModel)grid.FormulaModel;

            var success = model.TrySetNamedRange("Amount", "2", out var error);

            Assert.False(success);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [AvaloniaFact]
        public void FormulaModel_TryGetNamedRange_Returns_Stored_Formula()
        {
            var items = new ObservableCollection<RowItem>
            {
                new("A", 10d)
            };

            var builder = DataGridColumnDefinitionBuilder.For<RowItem>();
            var amountProperty = CreateProperty(nameof(RowItem.Amount), row => row.Amount, (row, value) => row.Amount = value);

            var amountDefinition = builder.Numeric(
                header: "Amount",
                property: amountProperty,
                getter: row => row.Amount,
                setter: (row, value) => row.Amount = value,
                configure: column => column.ColumnKey = "Amount");

            var grid = new DataGrid
            {
                Name = "SalesTable",
                ItemsSource = items,
                ColumnDefinitionsSource = new ObservableCollection<DataGridColumnDefinition>
                {
                    amountDefinition
                },
                AutoGenerateColumns = false
            };

            var model = (DataGridFormulaModel)grid.FormulaModel;

            model.TrySetNamedRange("TaxRate", "2", out _);

            Assert.True(model.TryGetNamedRange("TaxRate", out var formula));
            Assert.Equal("=2", formula);
        }

        private static IPropertyInfo CreateProperty<TValue>(
            string name,
            System.Func<RowItem, TValue> getter,
            System.Action<RowItem, TValue>? setter = null)
        {
            return new ClrPropertyInfo(
                name,
                target => getter((RowItem)target),
                setter == null
                    ? null
                    : (target, value) => setter((RowItem)target, value is null ? default! : (TValue)value),
                typeof(TValue));
        }

        private sealed class RowItem
        {
            public RowItem(string name, double amount)
            {
                Name = name;
                Amount = amount;
            }

            public string Name { get; set; }

            public double Amount { get; set; }
        }
    }
}
