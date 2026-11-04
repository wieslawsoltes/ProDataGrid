using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class OptimizedHierarchyFlatLayoutPerformanceTests
{
    private const int MeasuredIterations = 3;
    private readonly ITestOutputHelper _output;

    public OptimizedHierarchyFlatLayoutPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    [Trait("Category", "Performance")]
    public async Task RepresentativeOptimizedHierarchy_ComparesNestedFlatAndVirtualVisualLayouts()
    {
        var results = new List<LayoutResult>();
        results.Add(await MeasureLayoutAsync(
            "nested",
            new NestedSurfaceHierarchyPage(),
            DataGridVisualLayoutMode.Nested));
        results.Add(await MeasureLayoutAsync(
            "flat",
            new FlatSurfaceHierarchyPage(),
            DataGridVisualLayoutMode.Flat));
        results.Add(await MeasureLayoutAsync(
            "virtual",
            new VirtualSurfaceHierarchyPage(),
            DataGridVisualLayoutMode.Virtualized,
            measureVirtualSurface: true));

        var report = new LayoutComparisonReport(
            DateTimeOffset.UtcNow,
            149_792,
            MeasuredIterations,
            Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-working-tree",
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            results);
        string artifactPath = WriteReport(report);

        foreach (LayoutResult layout in results)
        {
            foreach (CellPathResult path in layout.CellPaths)
            {
                _output.WriteLine(
                    $"{layout.Layout}/{path.Key}: layout median=" +
                    $"{Median(path.Samples.Select(sample => sample.LayoutMilliseconds)):F3} ms; " +
                    $"render median={Median(path.Samples.Select(sample => sample.RenderMilliseconds)):F3} ms; " +
                    $"end-to-end median={Median(path.Samples.Select(sample => sample.TotalMilliseconds)):F3} ms; " +
                    $"visuals={Median(path.Samples.Select(sample => (double)sample.VisualCount)):F0}; " +
                    $"depth={Median(path.Samples.Select(sample => (double)sample.MaximumVisualDepth)):F0}");
            }
        }
        _output.WriteLine($"Report: {artifactPath}");

        LayoutSample nested = results.Single(result => result.Layout == "nested").CellPaths[0].Samples[0];
        LayoutSample flat = results.Single(result => result.Layout == "flat").CellPaths[0].Samples[0];
        LayoutSample virtualSurface = results.Single(result => result.Layout == "virtual").CellPaths[0].Samples[0];
        Assert.True(nested.CellsPresenterCount > 0);
        Assert.Equal(0, flat.CellsPresenterCount);
        Assert.Equal(0, virtualSurface.CellsPresenterCount);
        Assert.Equal(0, virtualSurface.RealizedRows);
        Assert.True(flat.VisualCount < nested.VisualCount);
        Assert.True(flat.MaximumVisualDepth < nested.MaximumVisualDepth);
        Assert.True(virtualSurface.VisualCount < flat.VisualCount);
    }

    private static async Task<LayoutResult> MeasureLayoutAsync(
        string layoutName,
        Control page,
        DataGridVisualLayoutMode expectedMode,
        bool measureVirtualSurface = false)
    {
        var window = new Window
        {
            Width = 1_200,
            Height = 760,
            Content = page,
        };
        window.ApplySampleTheme();

        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedHierarchyCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal(expectedMode, grid.VisualLayoutMode);
            await viewModel.LoadRepresentativeWorkloadAsync();
            PumpLayout(window);
            Assert.Equal(288, viewModel.Model.Flattened.Count);

            int modeCount = measureVirtualSurface ? 1 : viewModel.Paths.Count;
            var pathResults = new List<CellPathResult>(modeCount);
            for (int modeIndex = 0; modeIndex < modeCount; modeIndex++)
            {
                string modeKey;
                string modeName;
                if (measureVirtualSurface)
                {
                    VirtualSurfaceModeOption mode = viewModel.VirtualModes[0];
                    viewModel.SelectedVirtualMode = mode;
                    modeKey = mode.Key;
                    modeName = mode.Name;
                }
                else
                {
                    OptimizedCellPathOption mode = viewModel.Paths[modeIndex];
                    viewModel.SelectedPath = mode;
                    modeKey = mode.Key;
                    modeName = mode.Name;
                }

                PumpLayout(window);

                viewModel.Model.ExpandAll();
                PumpLayout(window);
                viewModel.Model.CollapseAll();
                PumpLayout(window);

                var samples = new List<LayoutSample>(MeasuredIterations);
                for (int iteration = 0; iteration < MeasuredIterations; iteration++)
                {
                    viewModel.Model.ExpandAll();
                    PumpLayout(window);
                    Assert.Equal(149_792, viewModel.Model.Flattened.Count);

                    long allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
                    var totalTimer = Stopwatch.StartNew();
                    var dispatchTimer = Stopwatch.StartNew();
                    viewModel.Model.CollapseAll();
                    dispatchTimer.Stop();

                    var layoutTimer = Stopwatch.StartNew();
                    PumpLayout(window);
                    layoutTimer.Stop();

                    var renderTimer = Stopwatch.StartNew();
                    using var frame = window.CaptureRenderedFrame();
                    renderTimer.Stop();
                    totalTimer.Stop();

                    Assert.Equal(viewModel.RootCount, viewModel.Model.Flattened.Count);
                    Visual[] visuals = grid.GetSelfAndVisualDescendants().ToArray();
                    int cellsPresenterCount = visuals.Count(
                        visual => visual.GetType().Name == "DataGridCellsPresenter");
                    samples.Add(new LayoutSample(
                        dispatchTimer.Elapsed.TotalMilliseconds,
                        layoutTimer.Elapsed.TotalMilliseconds,
                        renderTimer.Elapsed.TotalMilliseconds,
                        totalTimer.Elapsed.TotalMilliseconds,
                        GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore,
                        visuals.Length,
                        GetMaximumVisualDepth(grid),
                        cellsPresenterCount,
                        visuals.OfType<DataGridRow>().Count(row => row.IsVisible && row.Index >= 0)));
                }

                pathResults.Add(new CellPathResult(modeKey, modeName, samples));
            }

            return new LayoutResult(
                layoutName,
                window.ClientSize.Width,
                window.ClientSize.Height,
                window.RenderScaling,
                pathResults);
        }
        finally
        {
            window.Close();
        }
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

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        return sorted[sorted.Length / 2];
    }

    private static string WriteReport(LayoutComparisonReport report)
    {
        string? configuredDirectory = Environment.GetEnvironmentVariable("DATAGRID_PERF_ARTIFACT_DIR");
        string directory;
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            directory = Path.GetFullPath(configuredDirectory);
        }
        else
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null &&
                !File.Exists(Path.Combine(current.FullName, "Avalonia.Controls.DataGrid.slnx")))
            {
                current = current.Parent;
            }

            string root = current?.FullName ?? Directory.GetCurrentDirectory();
            directory = Path.Combine(root, "artifacts", "performance", "optimized-hierarchy-layout");
        }

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "optimized-hierarchy-layout-comparison.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return Path.GetFullPath(path);
    }

    private sealed record LayoutComparisonReport(
        DateTimeOffset TimestampUtc,
        int ExpandedNodeCount,
        int MeasuredIterations,
        string Commit,
        string Runtime,
        string OS,
        string Architecture,
        int LogicalProcessorCount,
        IReadOnlyList<LayoutResult> Layouts);

    private sealed record LayoutResult(
        string Layout,
        double ClientWidth,
        double ClientHeight,
        double RenderScaling,
        IReadOnlyList<CellPathResult> CellPaths);

    private sealed record CellPathResult(
        string Key,
        string Name,
        IReadOnlyList<LayoutSample> Samples);

    private sealed record LayoutSample(
        double DispatchMilliseconds,
        double LayoutMilliseconds,
        double RenderMilliseconds,
        double TotalMilliseconds,
        long AllocatedBytes,
        int VisualCount,
        int MaximumVisualDepth,
        int CellsPresenterCount,
        int RealizedRows);
}
