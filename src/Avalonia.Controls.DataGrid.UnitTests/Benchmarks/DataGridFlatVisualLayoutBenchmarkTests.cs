// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public sealed class DataGridFlatVisualLayoutBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public DataGridFlatVisualLayoutBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    [Trait("Category", "Benchmark")]
    public void Flat_And_Nested_Scroll_Layout_Comparison_Reports_Structural_Metrics()
    {
        IReadOnlyList<BenchmarkItem> items = Enumerable.Range(0, 100_000)
            .Select(index => new BenchmarkItem(index, $"Item {index:D6}", $"Group {index % 32:D2}"))
            .ToArray();

        LayoutSnapshot nested = RunScenario(items, useFlatLayout: false);
        LayoutSnapshot flat = RunScenario(items, useFlatLayout: true);

        _output.WriteLine(
            $"Nested: visuals={nested.VisualCount:n0}, max-depth={nested.MaximumDepth}, " +
            $"row-presenters={nested.CellsPresenterCount:n0}, scroll-layout={nested.ScrollLayoutMilliseconds:n2} ms");
        _output.WriteLine(
            $"Flat: visuals={flat.VisualCount:n0}, max-depth={flat.MaximumDepth}, " +
            $"row-presenters={flat.CellsPresenterCount:n0}, scroll-layout={flat.ScrollLayoutMilliseconds:n2} ms");

        Assert.True(nested.CellsPresenterCount > 0);
        Assert.Equal(0, flat.CellsPresenterCount);
        Assert.True(flat.VisualCount < nested.VisualCount,
            $"Expected fewer flat visuals. Flat={flat.VisualCount}, Nested={nested.VisualCount}");
        Assert.True(flat.MaximumDepth < nested.MaximumDepth,
            $"Expected a shallower flat tree. Flat={flat.MaximumDepth}, Nested={nested.MaximumDepth}");
    }

    private static LayoutSnapshot RunScenario(IReadOnlyList<BenchmarkItem> items, bool useFlatLayout)
    {
        var grid = new DataGrid
        {
            Width = 900,
            Height = 560,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = items,
            RowHeight = 32,
            UseLogicalScrollable = true,
        };
        AddColumns(grid);

        var window = new Window
        {
            Width = 940,
            Height = 600,
            Content = grid,
        };
        window.SetThemeStyles(useFlatLayout ? DataGridTheme.SimpleFlat : DataGridTheme.SimpleV2);
        if (useFlatLayout)
        {
            Assert.True(grid.TryFindResource("DataGridFlatTheme", out object? resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
        }
        else
        {
            Assert.True(grid.TryFindResource(typeof(DataGrid), out object? resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
        }

        try
        {
            window.Show();
            PumpLayout(window);

            // Warm the same forward/backward realization path used by the measured trace.
            ScrollTo(grid, items, 50_000);
            ScrollTo(grid, items, 0);

            var stopwatch = Stopwatch.StartNew();
            ScrollTo(grid, items, 25_000);
            ScrollTo(grid, items, 75_000);
            ScrollTo(grid, items, 10_000);
            ScrollTo(grid, items, 90_000);
            ScrollTo(grid, items, 0);
            stopwatch.Stop();

            int visualCount = grid.GetSelfAndVisualDescendants().Count();
            int maximumDepth = GetMaximumVisualDepth(grid);
            int cellsPresenterCount = grid.GetVisualDescendants().OfType<DataGridCellsPresenter>().Count();
            return new LayoutSnapshot(
                visualCount,
                maximumDepth,
                cellsPresenterCount,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AddColumns(DataGrid grid)
    {
        for (int columnIndex = 0; columnIndex < 8; columnIndex++)
        {
            string propertyName = columnIndex switch
            {
                0 => nameof(BenchmarkItem.Id),
                1 => nameof(BenchmarkItem.Name),
                _ => nameof(BenchmarkItem.Group),
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = $"Column {columnIndex + 1}",
                Width = new DataGridLength(columnIndex == 1 ? 180 : 110),
                Binding = new Binding(propertyName),
            });
        }
    }

    private static void ScrollTo(DataGrid grid, IReadOnlyList<BenchmarkItem> items, int index)
    {
        grid.ScrollIntoView(items[index], grid.ColumnsInternal[0]);
        PumpLayout(grid);
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static int GetMaximumVisualDepth(Visual root)
    {
        int maximum = 0;
        var stack = new Stack<(Visual Visual, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            (Visual visual, int depth) = stack.Pop();
            maximum = Math.Max(maximum, depth);
            foreach (Visual child in visual.GetVisualChildren())
            {
                stack.Push((child, depth + 1));
            }
        }

        return maximum;
    }

    private sealed record BenchmarkItem(int Id, string Name, string Group);

    private sealed record LayoutSnapshot(
        int VisualCount,
        int MaximumDepth,
        int CellsPresenterCount,
        double ScrollLayoutMilliseconds);
}
