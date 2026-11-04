using BenchmarkDotNet.Running;
using System.Diagnostics.Tracing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace ProDataGrid.FlatLayout.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--profile")
        {
            RunProfileLoop(
                args[1],
                args.Length >= 3 ? int.Parse(args[2]) : 100,
                args.Length >= 4
                    ? Enum.Parse<HierarchyCellPath>(args[3], ignoreCase: true)
                    : HierarchyCellPath.OptimizedTheme,
                args.Length >= 5 && args[4].Equals("inspect", StringComparison.OrdinalIgnoreCase));
            return;
        }

        if (args.Length >= 2 && args[0] == "--profile-end-to-end")
        {
            RunEndToEndProfileLoop(
                args[1],
                args.Length >= 3 ? int.Parse(args[2]) : 100,
                args.Length >= 4
                    ? Enum.Parse<HierarchyCellPath>(args[3], ignoreCase: true)
                    : HierarchyCellPath.OptimizedTheme);
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    private static void RunProfileLoop(
        string layout,
        int iterations,
        HierarchyCellPath cellPath,
        bool inspect)
    {
        var benchmark = new HierarchyCollapseLayoutBenchmarks
        {
            CellPath = cellPath,
        };

        if (layout.Equals("nested", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupNested();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (inspect && iteration == 0)
                {
                    benchmark.ExpandNestedForDiagnostics();
                    PrintVisualStatistics(benchmark.NestedScenarioForDiagnostics, "expanded");
                    benchmark.CollapseNestedForDiagnostics();
                    PrintVisualStatistics(benchmark.NestedScenarioForDiagnostics, "before-layout");
                }
                else
                {
                    benchmark.PrepareNested();
                }
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Nested();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            if (inspect)
            {
                PrintVisualStatistics(benchmark.NestedScenarioForDiagnostics, "after-layout");
            }
            benchmark.GlobalCleanupNested();
            return;
        }

        if (layout.Equals("flat", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupFlat();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (inspect && iteration == 0)
                {
                    benchmark.ExpandFlatForDiagnostics();
                    PrintVisualStatistics(benchmark.FlatScenarioForDiagnostics, "expanded");
                    benchmark.CollapseFlatForDiagnostics();
                    PrintVisualStatistics(benchmark.FlatScenarioForDiagnostics, "before-layout");
                }
                else
                {
                    benchmark.PrepareFlat();
                }
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Flat();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            if (inspect)
            {
                PrintVisualStatistics(benchmark.FlatScenarioForDiagnostics, "after-layout");
            }
            benchmark.GlobalCleanupFlat();
            return;
        }

        if (layout.Equals("virtual", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupVirtualized();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (inspect && iteration == 0)
                {
                    benchmark.ExpandVirtualizedForDiagnostics();
                    PrintVisualStatistics(benchmark.VirtualizedScenarioForDiagnostics, "expanded");
                    benchmark.CollapseVirtualizedForDiagnostics();
                    PrintVisualStatistics(benchmark.VirtualizedScenarioForDiagnostics, "before-layout");
                }
                else
                {
                    benchmark.PrepareVirtualized();
                }
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Virtualized();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            if (inspect)
            {
                PrintVisualStatistics(benchmark.VirtualizedScenarioForDiagnostics, "after-layout");
            }
            benchmark.GlobalCleanupVirtualized();
            return;
        }

        throw new ArgumentException("Profile layout must be 'nested', 'flat', or 'virtual'.", nameof(layout));
    }

    private static void RunEndToEndProfileLoop(
        string layout,
        int iterations,
        HierarchyCellPath cellPath)
    {
        var benchmark = new HierarchyCollapseEndToEndBenchmarks
        {
            CellPath = cellPath,
        };

        if (layout.Equals("nested", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupNested();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                benchmark.PrepareNested();
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Nested();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            benchmark.GlobalCleanupNested();
            return;
        }

        if (layout.Equals("flat", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupFlat();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                benchmark.PrepareFlat();
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Flat();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            benchmark.GlobalCleanupFlat();
            return;
        }

        if (layout.Equals("virtual", StringComparison.OrdinalIgnoreCase))
        {
            benchmark.GlobalSetupVirtualized();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                benchmark.PrepareVirtualized();
                ProfileEventSource.Log.LayoutStart();
                try
                {
                    benchmark.Virtualized();
                }
                finally
                {
                    ProfileEventSource.Log.LayoutStop();
                }
            }
            benchmark.GlobalCleanupVirtualized();
            return;
        }

        throw new ArgumentException("Profile layout must be 'nested', 'flat', or 'virtual'.", nameof(layout));
    }

    private static void PrintVisualStatistics(BenchmarkScenario scenario, string phase)
    {
        Visual[] visuals = scenario.Grid.GetVisualDescendants().ToArray();
        DataGridRow[] rows = visuals.OfType<DataGridRow>().ToArray();
        DataGridCell[] cells = visuals.OfType<DataGridCell>().ToArray();
        DataGridCellsPresenter[] cellsPresenters = visuals.OfType<DataGridCellsPresenter>().ToArray();
        TextBlock[] textBlocks = visuals.OfType<TextBlock>().ToArray();
        Console.WriteLine(
            $"phase={phase}; layout={scenario.Grid.VisualLayoutMode}; " +
            $"visuals={visuals.Length}; rows={rows.Length}/{rows.Count(static row => row.IsVisible)} visible; " +
            $"cells={cells.Length}/{cells.Count(static cell => cell.IsVisible)} visible; " +
            $"cell-presenters={cellsPresenters.Length}; text={textBlocks.Length}/{textBlocks.Count(static text => text.IsVisible)} visible; " +
            $"invalid-measure={visuals.Count(static visual => visual is Layoutable layoutable && !layoutable.IsMeasureValid)}; " +
            $"invalid-cell-measure={cells.Count(static cell => !cell.IsMeasureValid)}; " +
            $"invalid-drawn-measure={cells.Count(static cell => cell is DataGridCustomDrawingCell && !cell.IsMeasureValid)}; " +
            $"invalid-arrange={visuals.Count(static visual => visual is Layoutable layoutable && !layoutable.IsArrangeValid)}");
        Console.WriteLine(
            "row-layout=" + string.Join(
                ", ",
                rows.OrderBy(static row => row.Index)
                    .Select(static row => $"{row.Index}:{row.DesiredSize.Height:F2}/{row.Bounds.Height:F2}")));
    }

    [EventSource(Name = "ProDataGrid-FlatLayout-Profile")]
    private sealed class ProfileEventSource : EventSource
    {
        public static readonly ProfileEventSource Log = new();

        [Event(1, Level = EventLevel.Informational)]
        public void LayoutStart() => WriteEvent(1);

        [Event(2, Level = EventLevel.Informational)]
        public void LayoutStop() => WriteEvent(2);
    }
}
