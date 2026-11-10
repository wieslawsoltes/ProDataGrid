// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Linq;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridFocusedRowVisibilityTests
{
    [AvaloniaTheory]
    [InlineData(DataGridTheme.SimpleV2)]
    [InlineData(DataGridTheme.FluentV2)]
    public void FocusedCellRow_RemainsVisible_WhenScrolledOutAndBack(DataGridTheme theme)
    {
        string[] items = Enumerable.Range(0, 100).Select(index => $"Row {index}").ToArray();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            UseLogicalScrollable = true,
            RowHeight = 32,
            FrozenColumnCount = 1,
            FrozenColumnCountRight = 2
        };
        for (int column = 0; column < 7; column++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = $"Column {column}",
                Width = new DataGridLength(100),
                Binding = new CompiledBindingExtension(new CompiledBindingPathBuilder().Build())
            });
        }
        var window = new Window { Width = 440, Height = 450, Content = grid };
        window.SetThemeStyles(theme);
        Assert.True(grid.TryFindResource(typeof(DataGrid), out object? resource));
        grid.Theme = Assert.IsType<Avalonia.Styling.ControlTheme>(resource);
        try
        {
            window.Show();
            Pump(grid);
            grid.ScrollIntoView(items[90], grid.Columns[0]);
            Pump(grid);
            var focusedRow = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>()
                .Single(row => ReferenceEquals(row.DataContext, items[90]));
            Assert.True(focusedRow.Cells[0].Focus());
            Assert.True(focusedRow.IsKeyboardFocusWithin);
            for (int iteration = 0; iteration < 4; iteration++)
            {
                grid.ScrollIntoView(items[0], grid.Columns[0]);
                Pump(grid);
                grid.ScrollIntoView(items[90], grid.Columns[0]);
                Pump(grid);
                Assert.True(grid.IsKeyboardFocusWithin);
                focusedRow = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>()
                    .Single(row => ReferenceEquals(row.DataContext, items[90]));
                Assert.True(focusedRow.IsVisible);
                DataGridRow[] displayed = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
                Assert.Equal(displayed.Length, displayed.Distinct().Count());
                foreach (DataGridRow row in displayed)
                {
                    Assert.True(row.IsVisible);
                    Assert.Equal(grid.RowIndexFromSlot(row.Slot), row.Index);
                    Assert.Same(items[row.Index], row.DataContext);
                    Assert.True(row.Bounds.Y > -1000, "Displayed rows must not remain parked offscreen.");
                }
                Assert.True(focusedRow.Cells[0].Focus());
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static void Pump(DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
