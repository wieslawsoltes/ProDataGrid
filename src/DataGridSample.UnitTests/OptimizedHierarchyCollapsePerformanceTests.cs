using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
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

public sealed class OptimizedHierarchyCollapsePerformanceTests
{
    private const int MeasuredIterations = 3;
    private readonly ITestOutputHelper _output;

    public OptimizedHierarchyCollapsePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public async Task RepresentativeWorkload_ReportsCollapseLatencyAcrossAllCellPaths()
    {
        var page = new OptimizedHierarchyCellPathsPage();
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
            var grid = Assert.IsType<DataGrid>(page.FindControl<DataGrid>("OptimizedHierarchyCellPathsGrid"));
            await viewModel.LoadRepresentativeWorkloadAsync();
            PumpLayout(window);
            Assert.Equal(288, viewModel.Model.Flattened.Count);

            var flattenedChangeCount = 0;
            viewModel.Model.FlattenedChanged += (_, _) => flattenedChangeCount++;
            var results = new List<CellPathResult>(viewModel.Paths.Count);

            foreach (OptimizedCellPathOption path in viewModel.Paths)
            {
                viewModel.SelectedPath = path;
                PumpLayout(window);

                // Warm the path-specific columns, bulk model code, layout, and renderer.
                viewModel.Model.ExpandAll();
                PumpLayout(window);
                viewModel.Model.CollapseAll();
                PumpLayout(window);

                var samples = new List<CollapseSample>(MeasuredIterations);
                for (int iteration = 0; iteration < MeasuredIterations; iteration++)
                {
                    viewModel.Model.ExpandAll();
                    PumpLayout(window);
                    Assert.Equal(149_792, viewModel.Model.Flattened.Count);

                    var changesBefore = flattenedChangeCount;
                    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
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

                    Assert.Equal(changesBefore + 1, flattenedChangeCount);
                    Assert.Equal(viewModel.RootCount, viewModel.Model.Flattened.Count);
                    Assert.All(viewModel.Model.Flattened, node => Assert.False(node.IsExpanded));

                    int realizedRows = grid.GetVisualDescendants()
                        .OfType<DataGridRow>()
                        .Count(row => row.IsVisible && row.Index >= 0);
                    Assert.True(realizedRows > 0);

                    samples.Add(new CollapseSample(
                        dispatchTimer.Elapsed.TotalMilliseconds,
                        layoutTimer.Elapsed.TotalMilliseconds,
                        renderTimer.Elapsed.TotalMilliseconds,
                        totalTimer.Elapsed.TotalMilliseconds,
                        GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore,
                        realizedRows));
                }

                results.Add(new CellPathResult(path.Key, path.Name, samples));
            }

            var report = new CollapsePerformanceReport(
                DateTimeOffset.UtcNow,
                149_792,
                viewModel.RootCount,
                MeasuredIterations,
                Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-working-tree",
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessorCount,
                window.ClientSize.Width,
                window.ClientSize.Height,
                window.RenderScaling,
                results);
            string artifactPath = WriteReport(report);

            foreach (CellPathResult result in results)
            {
                _output.WriteLine(
                    $"{result.Key}: dispatch median={Median(result.Samples.Select(sample => sample.DispatchMilliseconds)):F3} ms; " +
                    $"end-to-end median={Median(result.Samples.Select(sample => sample.TotalMilliseconds)):F3} ms");
            }
            _output.WriteLine($"Report: {artifactPath}");
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

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        return sorted[sorted.Length / 2];
    }

    private static string WriteReport(CollapsePerformanceReport report)
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
            directory = Path.Combine(
                root,
                "artifacts",
                "performance",
                "optimized-hierarchy-collapse");
        }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "optimized-hierarchy-collapse.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return Path.GetFullPath(path);
    }

    private sealed record CollapsePerformanceReport(
        DateTimeOffset TimestampUtc,
        int ExpandedNodeCount,
        int CollapsedNodeCount,
        int MeasuredIterations,
        string Commit,
        string Runtime,
        string OS,
        string Architecture,
        int LogicalProcessorCount,
        double ClientWidth,
        double ClientHeight,
        double RenderScaling,
        IReadOnlyList<CellPathResult> CellPaths);

    private sealed record CellPathResult(
        string Key,
        string Name,
        IReadOnlyList<CollapseSample> Samples);

    private sealed record CollapseSample(
        double DispatchMilliseconds,
        double LayoutMilliseconds,
        double RenderMilliseconds,
        double TotalMilliseconds,
        long AllocatedBytes,
        int RealizedRows);
}
