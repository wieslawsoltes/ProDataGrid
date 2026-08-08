// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedIndexedColumnTests
{
    [Fact]
    public void Factory_creates_typed_method_backed_definition()
    {
        var row = new Row(["old"]);
        var options = new DataGridGeneratedIndexedColumnOptions<string>
        {
            Header = "A",
            ColumnKey = "cell-a",
            PropertyName = "A",
            Kind = DataGridGeneratedIndexedColumnKind.Text,
            FormatString = "value:{0}"
        };

        DataGridColumnDefinition definition = DataGridGeneratedIndexedColumnFactory.Create<Row, string>(
            0,
            item => (string)item.Get(0)!,
            (item, value) => item.Set(0, value),
            in options);

        DataGridTextColumnDefinition text = Assert.IsType<DataGridTextColumnDefinition>(definition);
        Assert.Equal("cell-a", text.ColumnKey);
        Assert.Equal("A", text.SortMemberPath);
        Assert.Equal("value:{0}", text.Binding.StringFormat);
        Assert.Equal("old", text.Binding.ValueAccessor.GetValue(row));
        text.Binding.ValueAccessor.SetValue(row, "new");
        Assert.Equal("new", row.Get(0));
    }

    [Fact]
    public void Factory_creates_formula_definition_without_a_runtime_property_accessor()
    {
        var options = new DataGridGeneratedIndexedColumnOptions<decimal>
        {
            Header = "E",
            ColumnKey = "total",
            PropertyName = "E",
            Kind = DataGridGeneratedIndexedColumnKind.Formula,
            Formula = "=([@B]*[@C])*(1-[@D])",
            FormulaName = "Total",
            AllowCellFormulas = true,
            Width = new DataGridLength(120)
        };

        DataGridColumnDefinition definition = DataGridGeneratedIndexedColumnFactory.Create<Row, decimal>(
            4,
            null,
            null,
            in options);

        DataGridFormulaColumnDefinition formula = Assert.IsType<DataGridFormulaColumnDefinition>(definition);
        Assert.Equal("total", formula.ColumnKey);
        Assert.Equal("=([@B]*[@C])*(1-[@D])", formula.Formula);
        Assert.Equal("Total", formula.FormulaName);
        Assert.Equal(typeof(decimal), formula.FormulaValueType);
        Assert.True(formula.AllowCellFormulas);
        Assert.False(formula.IsReadOnly);
        Assert.Equal(new DataGridLength(120), formula.Width);
    }

    [Fact]
    public void Factory_uses_the_synthetic_property_name_for_formula_identity_defaults()
    {
        var options = new DataGridGeneratedIndexedColumnOptions<double>
        {
            Header = "G",
            PropertyName = "G",
            Kind = DataGridGeneratedIndexedColumnKind.Formula,
            Formula = "=[@E]/5"
        };

        DataGridFormulaColumnDefinition formula = Assert.IsType<DataGridFormulaColumnDefinition>(
            DataGridGeneratedIndexedColumnFactory.Create<Row, double>(
                6,
                static _ => 0d,
                null,
                in options));

        Assert.Equal("G", formula.ColumnKey);
        Assert.Equal("G", formula.FormulaName);
        Assert.True(formula.IsReadOnly);
    }

    private sealed class Row
    {
        private readonly object?[] _values;
        public Row(object?[] values) => _values = values;
        public object? Get(int index) => _values[index];
        public void Set(int index, object? value) => _values[index] = value;
    }
}
