// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedTemplateTests
{
    [Fact]
    public void Typed_template_matches_builds_and_recycles()
    {
        var template = new DataGridGeneratedFuncDataTemplate<Row>(static (item, existing) =>
            existing ?? new TextBlock { Text = item.Name });
        var existing = new TextBlock { Text = "existing" };

        Assert.True(template.Match(new Row("A")));
        Assert.False(template.Match("wrong"));
        Assert.Equal("A", Assert.IsType<TextBlock>(template.Build(new Row("A"))).Text);
        Assert.Same(existing, template.Build(new Row("B"), existing));
    }

    [Fact]
    public void Typed_template_ignores_null_measurement_probe()
    {
        bool invoked = false;
        var template = new DataGridGeneratedFuncDataTemplate<Row>((_, existing) =>
        {
            invoked = true;
            return existing ?? new TextBlock();
        });

        Assert.Null(template.Build(null!));
        Assert.False(invoked);
    }

    [Fact]
    public void Template_definition_prefers_direct_generated_template()
    {
        var template = new DataGridGeneratedFuncDataTemplate<Row>(static (_, existing) => existing ?? new TextBlock());
        var definition = new DataGridTemplateColumnDefinition
        {
            CellTemplateKey = "MissingResource",
            CellTemplate = template
        };

        Assert.Same(template, definition.CellTemplate);
    }

    private sealed record Row(string Name);
}
