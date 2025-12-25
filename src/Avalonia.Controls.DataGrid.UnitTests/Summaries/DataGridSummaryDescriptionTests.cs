// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Summaries;

/// <summary>
/// Tests for the DataGridSummaryDescription classes.
/// </summary>
public class DataGridSummaryDescriptionTests
{
    #region Aggregate Summary Description Tests

    [AvaloniaFact]
    public void AggregateSummaryDescription_Calculates_Sum()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        };

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);

        Assert.Equal(60m, result);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_Calculates_Average()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Average
        };

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);

        Assert.Equal(20m, result);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_Calculates_Count()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Count
        };

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);

        Assert.Equal(3, result);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_Applies_StringFormat()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum,
            StringFormat = "N2"
        };

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);
        var formatted = description.FormatValue(result, CultureInfo.InvariantCulture);

        Assert.Equal("60.00", formatted);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_Default_Properties()
    {
        var description = new DataGridAggregateSummaryDescription();

        Assert.Equal(DataGridAggregateType.None, description.Aggregate);
        Assert.Equal(DataGridSummaryScope.Total, description.Scope);
        Assert.Null(description.StringFormat);
        Assert.Null(description.Title);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_FormatValue_Null_Returns_Empty_String()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            StringFormat = "N2"
        };

        var formatted = description.FormatValue(null, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, formatted);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_FormatValue_Prefixes_Title_With_Placeholder_Format()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Title = "Total",
            StringFormat = "{0:N2}"
        };

        var formatted = description.FormatValue(12.3m, CultureInfo.InvariantCulture);

        Assert.Equal("Total 12.30", formatted);
    }

    [AvaloniaFact]
    public void AggregateSummaryDescription_FormatValue_Prefixes_Title_With_FormatString()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Title = "Sum:",
            StringFormat = "N2"
        };

        var formatted = description.FormatValue(12.3m, CultureInfo.InvariantCulture);

        Assert.Equal("Sum: 12.30", formatted);
    }

    #endregion

    #region Custom Summary Description Tests

    [AvaloniaFact]
    public void CustomSummaryDescription_Uses_Custom_Calculator()
    {
        var calculator = new TestCustomCalculator();
        var description = new DataGridCustomSummaryDescription
        {
            Calculator = calculator
        };

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);

        Assert.Equal("Custom Result", result);
    }

    [AvaloniaFact]
    public void CustomSummaryDescription_Returns_Null_Without_Calculator()
    {
        var description = new DataGridCustomSummaryDescription();

        var items = new List<TestItem>
        {
            new() { Value = 10 },
            new() { Value = 20 }
        };

        var column = CreateColumnWithBinding(nameof(TestItem.Value));
        var result = description.Calculate(items, column);

        Assert.Null(result);
    }

    [AvaloniaFact]
    public void CustomSummaryDescription_Default_Properties()
    {
        var description = new DataGridCustomSummaryDescription();

        Assert.Equal(DataGridSummaryScope.Total, description.Scope);
        Assert.Null(description.Calculator);
    }

    #endregion

    #region Scope Tests

    [AvaloniaFact]
    public void SummaryDescription_Default_Scope_Is_Total()
    {
        var description = new DataGridAggregateSummaryDescription();

        Assert.Equal(DataGridSummaryScope.Total, description.Scope);
    }

    [AvaloniaFact]
    public void SummaryDescription_Can_Set_Group_Scope()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Scope = DataGridSummaryScope.Group
        };

        Assert.Equal(DataGridSummaryScope.Group, description.Scope);
    }

    [AvaloniaFact]
    public void SummaryDescription_Can_Set_Both_Scope()
    {
        var description = new DataGridAggregateSummaryDescription
        {
            Scope = DataGridSummaryScope.Both
        };

        Assert.Equal(DataGridSummaryScope.Both, description.Scope);
    }

    #endregion

    #region Test Helpers

    private static DataGridTextColumn CreateColumnWithBinding(string propertyName)
    {
        return new DataGridTextColumn
        {
            Binding = new Avalonia.Data.Binding(propertyName)
        };
    }

    private class TestItem
    {
        public int Value { get; set; }
    }

    private class TestCustomCalculator : IDataGridSummaryCalculator
    {
        public string Name => "Custom";

        public bool SupportsIncremental => false;

        public object? Calculate(IEnumerable items, DataGridColumn column, string? propertyName)
        {
            return "Custom Result";
        }

        public IDataGridSummaryState? CreateState() => null;
    }

    #endregion
}
