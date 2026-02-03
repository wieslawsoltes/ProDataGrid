// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridFocusRetentionOnScrollTests
{
    [AvaloniaFact]
    public void Scrolling_Recycling_Focused_Row_Keeps_Focus_Within_Grid()
    {
        var items = Enumerable.Range(0, 200).Select(i => new FocusItem($"Item {i}")).ToList();
        var window = new Window
        {
            Width = 320,
            Height = 220
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.All,
            UseLogicalScrollable = true
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(FocusItem.Name))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var firstRow = GetVisibleRows(grid).First(r => r.Index == 0);
        var firstCell = firstRow.Cells[0];
        firstCell.Focus();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(grid.IsKeyboardFocusWithin);
        Assert.True(firstCell.IsKeyboardFocusWithin);

        grid.ScrollIntoView(items[150], grid.ColumnsInternal[0]);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(grid.IsKeyboardFocusWithin);
        Assert.DoesNotContain(GetVisibleRows(grid), row => row.Index == 0);

        window.Close();
    }

    private static IReadOnlyList<DataGridRow> GetVisibleRows(DataGrid grid)
    {
        return grid.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Where(r => r.IsVisible)
            .ToList();
    }

    private sealed class FocusItem
    {
        public FocusItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
