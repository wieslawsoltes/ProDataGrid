// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using System;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public class DataGridColumnDefinitionTests
{
    [Fact]
    public void Bound_Definition_Populates_ValueAccessor_And_Type_From_Binding()
    {
        var definition = new DataGridTextColumnDefinition
        {
            Binding = DataGridBindingDefinition.Create<Person, int>(p => p.Age)
        };

        Assert.NotNull(definition.ValueAccessor);
        Assert.Equal(typeof(int), definition.ValueType);
    }

    [Fact]
    public void Summary_definitions_materialize_independent_descriptions_with_display_metadata()
    {
        int customFactoryCalls = 0;
        var definition = new DataGridTextColumnDefinition
        {
            SummaryDefinitions = new DataGridSummaryDefinition[]
            {
                new(DataGridAggregateType.Sum, DataGridSummaryScope.Both, "N2", "Total: "),
                new(DataGridAggregateType.Custom, DataGridSummaryScope.Total, title: "Custom: ")
                {
                    Factory = () =>
                    {
                        customFactoryCalls++;
                        return new DataGridCustomSummaryDescription();
                    }
                }
            }
        };

        DataGridColumn first = definition.CreateColumn(new DataGridColumnDefinitionContext(new DataGrid()));
        DataGridColumn second = definition.CreateColumn(new DataGridColumnDefinitionContext(new DataGrid()));

        Assert.Equal(2, first.Summaries.Count);
        var aggregate = Assert.IsType<DataGridAggregateSummaryDescription>(first.Summaries[0]);
        Assert.Equal(DataGridAggregateType.Sum, aggregate.Aggregate);
        Assert.Equal(DataGridSummaryScope.Both, aggregate.Scope);
        Assert.Equal("N2", aggregate.StringFormat);
        Assert.Equal("Total: ", aggregate.Title);
        Assert.IsType<DataGridCustomSummaryDescription>(first.Summaries[1]);
        Assert.NotSame(first.Summaries[0], second.Summaries[0]);
        Assert.Equal(2, customFactoryCalls);
    }

    private sealed class Person
    {
        public int Age { get; set; }
    }
}
