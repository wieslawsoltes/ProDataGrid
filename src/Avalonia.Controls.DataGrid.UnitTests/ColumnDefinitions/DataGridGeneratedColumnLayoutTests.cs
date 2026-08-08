// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedColumnLayoutTests
{
    [Fact]
    public void Controller_updates_and_resets_chooser_layout_by_stable_key()
    {
        var first = new DataGridTextColumnDefinition
        {
            Header = "A", ColumnKey = "a", IsVisible = true, DisplayIndex = 0, Width = new DataGridLength(100)
        };
        var second = new DataGridTextColumnDefinition
        {
            Header = "B", ColumnKey = "b", IsVisible = true, DisplayIndex = 1
        };
        using var controller = new DataGridGeneratedColumnLayoutController([first, second]);

        controller.SetVisible("a", false);
        controller.SetDisplayIndex("b", 0);
        Assert.False(controller.Choices[0].IsVisible);
        Assert.Equal(0, second.DisplayIndex);

        controller.Reset();
        Assert.True(controller.Choices[0].IsVisible);
        Assert.Equal(1, second.DisplayIndex);
        Assert.Equal(new DataGridLength(100), first.Width);
    }

    [Fact]
    public void Controller_builds_deterministic_nested_band_tree()
    {
        DataGridColumnDefinition[] columns =
        [
            new DataGridTextColumnDefinition { ColumnKey = "price" },
            new DataGridTextColumnDefinition { ColumnKey = "size" }
        ];
        DataGridGeneratedBandField[] fields =
        [
            new("price", ["Market", "Quote"], 1),
            new("size", ["Market", "Quote"], 2)
        ];

        using var controller = new DataGridGeneratedColumnLayoutController(columns, fields);

        DataGridGeneratedBandNode market = Assert.Single(controller.Bands);
        DataGridGeneratedBandNode quote = Assert.Single(market.Children);
        Assert.Equal(["price", "size"], [quote.Children[0].ColumnKey, quote.Children[1].ColumnKey]);
    }
}
