using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ReactiveUI.Avalonia;

namespace ProDataGrid.FlatLayout.Benchmarks;

public enum HierarchyCellPath
{
    Standard,
    OptimizedTheme,
    OptimizedPresenter,
    DirectHierarchy,
    BuiltInDrawn,
    CustomDrawn,
}

[MemoryDiagnoser]
[RankColumn]
[MedianColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[Orderer(SummaryOrderPolicy.Method, MethodOrderPolicy.Declared)]
public class HierarchyCollapseLayoutBenchmarks : HierarchyFlatLayoutBenchmarkBase
{
    [IterationSetup(Target = nameof(Nested))]
    public void PrepareNested()
    {
        ExpandAndLayout(NestedScenario);
        CollapseWithoutLayout(NestedScenario);
    }

    [IterationSetup(Target = nameof(Flat))]
    public void PrepareFlat()
    {
        ExpandAndLayout(FlatScenario);
        CollapseWithoutLayout(FlatScenario);
    }

    [IterationSetup(Target = nameof(Virtualized))]
    public void PrepareVirtualized()
    {
        ExpandAndLayout(VirtualizedScenario);
        CollapseWithoutLayout(VirtualizedScenario);
    }

    [Benchmark(Baseline = true)]
    public int Nested() => CompletePendingLayout(NestedScenario);

    [Benchmark]
    public int Flat() => CompletePendingLayout(FlatScenario);

    [Benchmark]
    public int Virtualized() => CompletePendingLayout(VirtualizedScenario);
}

[MemoryDiagnoser]
[RankColumn]
[MedianColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[Orderer(SummaryOrderPolicy.Method, MethodOrderPolicy.Declared)]
public class HierarchyCollapseEndToEndBenchmarks : HierarchyFlatLayoutBenchmarkBase
{
    [IterationSetup(Target = nameof(Nested))]
    public void PrepareNested() => ExpandAndLayout(NestedScenario);

    [IterationSetup(Target = nameof(Flat))]
    public void PrepareFlat() => ExpandAndLayout(FlatScenario);

    [IterationSetup(Target = nameof(Virtualized))]
    public void PrepareVirtualized() => ExpandAndLayout(VirtualizedScenario);

    [Benchmark(Baseline = true)]
    public int Nested() => CollapseAndLayout(NestedScenario);

    [Benchmark]
    public int Flat() => CollapseAndLayout(FlatScenario);

    [Benchmark]
    public int Virtualized() => CollapseAndLayout(VirtualizedScenario);
}

public abstract class HierarchyFlatLayoutBenchmarkBase
{
    private const int ExpandedNodeCount = 149_792;
    private static bool s_applicationInitialized;

    [ParamsSource(nameof(CellPaths))]
    public HierarchyCellPath CellPath { get; set; }

    public IEnumerable<HierarchyCellPath> CellPaths
    {
        get
        {
            string? configuredPath = Environment.GetEnvironmentVariable("PRODATAGRID_BENCHMARK_CELL_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath) &&
                Enum.TryParse(configuredPath, ignoreCase: true, out HierarchyCellPath selectedPath))
            {
                yield return selectedPath;
                yield break;
            }

            yield return HierarchyCellPath.Standard;
            yield return HierarchyCellPath.OptimizedTheme;
            yield return HierarchyCellPath.OptimizedPresenter;
            yield return HierarchyCellPath.DirectHierarchy;
            yield return HierarchyCellPath.BuiltInDrawn;
            yield return HierarchyCellPath.CustomDrawn;
        }
    }

    protected BenchmarkScenario NestedScenario { get; private set; } = null!;

    protected BenchmarkScenario FlatScenario { get; private set; } = null!;

    protected BenchmarkScenario VirtualizedScenario { get; private set; } = null!;

    internal BenchmarkScenario NestedScenarioForDiagnostics => NestedScenario;

    internal BenchmarkScenario FlatScenarioForDiagnostics => FlatScenario;

    internal BenchmarkScenario VirtualizedScenarioForDiagnostics => VirtualizedScenario;

    internal void ExpandNestedForDiagnostics() => ExpandAndLayout(NestedScenario);

    internal void CollapseNestedForDiagnostics() => CollapseWithoutLayout(NestedScenario);

    internal void ExpandFlatForDiagnostics() => ExpandAndLayout(FlatScenario);

    internal void CollapseFlatForDiagnostics() => CollapseWithoutLayout(FlatScenario);

    internal void ExpandVirtualizedForDiagnostics() => ExpandAndLayout(VirtualizedScenario);

    internal void CollapseVirtualizedForDiagnostics() => CollapseWithoutLayout(VirtualizedScenario);

    [GlobalSetup(Target = "Nested")]
    public void GlobalSetupNested()
    {
        EnsureApplication();
        string pathKey = GetPathKey(CellPath);
        NestedScenario = CreateScenario(new NestedSurfaceHierarchyPage(), pathKey, DataGridVisualLayoutMode.Nested);
        WarmScenario(NestedScenario);
    }

    [GlobalSetup(Target = "Flat")]
    public void GlobalSetupFlat()
    {
        EnsureApplication();
        string pathKey = GetPathKey(CellPath);
        FlatScenario = CreateScenario(new FlatSurfaceHierarchyPage(), pathKey, DataGridVisualLayoutMode.Flat);
        WarmScenario(FlatScenario);
    }

    [GlobalSetup(Target = "Virtualized")]
    public void GlobalSetupVirtualized()
    {
        EnsureApplication();
        string pathKey = GetPathKey(CellPath);
        VirtualizedScenario = CreateScenario(
            new FlatSurfaceHierarchyPage(),
            pathKey,
            DataGridVisualLayoutMode.Virtualized);
        WarmScenario(VirtualizedScenario);
    }

    [GlobalCleanup(Target = "Nested")]
    public void GlobalCleanupNested() => NestedScenario.Dispose();

    [GlobalCleanup(Target = "Flat")]
    public void GlobalCleanupFlat() => FlatScenario.Dispose();

    [GlobalCleanup(Target = "Virtualized")]
    public void GlobalCleanupVirtualized() => VirtualizedScenario.Dispose();

    protected static void ExpandAndLayout(BenchmarkScenario scenario)
    {
        scenario.ViewModel.Model.ExpandAll();
        PumpLayout(scenario.Window);
        EnsureCount(scenario, ExpandedNodeCount);
    }

    protected static void CollapseWithoutLayout(BenchmarkScenario scenario)
    {
        scenario.ViewModel.Model.CollapseAll();
        EnsureCount(scenario, scenario.ViewModel.RootCount);
    }

    protected static int CompletePendingLayout(BenchmarkScenario scenario)
    {
        PumpLayout(scenario.Window);
        return EnsureCount(scenario, scenario.ViewModel.RootCount);
    }

    protected static int CollapseAndLayout(BenchmarkScenario scenario)
    {
        scenario.ViewModel.Model.CollapseAll();
        PumpLayout(scenario.Window);
        return EnsureCount(scenario, scenario.ViewModel.RootCount);
    }

    private static void EnsureApplication()
    {
        if (s_applicationInitialized)
        {
            return;
        }

        AppContext.SetSwitch("ProDataGrid.Diagnostics.IsEnabled", false);
        AppBuilder.Configure<BenchmarkApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .UseReactiveUI(static _ => { })
            .SetupWithoutStarting();
        s_applicationInitialized = true;
    }

    private static BenchmarkScenario CreateScenario(
        Control page,
        string pathKey,
        DataGridVisualLayoutMode expectedMode)
    {
        var window = new Window
        {
            Width = 1_200,
            Height = 760,
            Content = page,
        };

        window.Show();
        PumpLayout(window);

        var viewModel = page.DataContext as OptimizedHierarchyCellPathsViewModel
            ?? throw new InvalidOperationException("The hierarchy comparison page did not create its ViewModel.");
        DataGrid grid = page.GetVisualDescendants().OfType<DataGrid>().Single();
        if (expectedMode == DataGridVisualLayoutMode.Virtualized)
        {
            grid.CellTheme = null;
            grid.VisualLayoutMode = expectedMode;
            PumpLayout(window);
        }
        if (grid.VisualLayoutMode != expectedMode)
        {
            throw new InvalidOperationException(
                $"Expected {expectedMode} visual layout, got {grid.VisualLayoutMode}.");
        }

        OptimizedCellPathOption path = viewModel.Paths.Single(option => option.Key == pathKey);
        viewModel.SelectedPath = path;
        WaitWithDispatcher(viewModel.LoadRepresentativeWorkloadAsync());
        PumpLayout(window);
        if (expectedMode == DataGridVisualLayoutMode.Virtualized &&
            pathKey != "custom-drawn" &&
            grid.GetVisualDescendants().OfType<DataGridCell>().Any())
        {
            throw new InvalidOperationException(
                $"The {pathKey} virtualized scenario created retained display-cell controls.");
        }
        viewModel.Model.CollapseAll();
        PumpLayout(window);
        EnsureCount(viewModel, viewModel.RootCount);
        return new BenchmarkScenario(window, grid, viewModel);
    }

    private static void WarmScenario(BenchmarkScenario scenario)
    {
        ExpandAndLayout(scenario);
        _ = CollapseAndLayout(scenario);
    }

    private static void WaitWithDispatcher(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
        }

        task.GetAwaiter().GetResult();
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static int EnsureCount(BenchmarkScenario scenario, int expected)
        => EnsureCount(scenario.ViewModel, expected);

    private static int EnsureCount(OptimizedHierarchyCellPathsViewModel viewModel, int expected)
    {
        int count = viewModel.Model.Flattened.Count;
        if (count != expected)
        {
            throw new InvalidOperationException($"Expected {expected:n0} visible nodes, got {count:n0}.");
        }

        return count;
    }

    private static string GetPathKey(HierarchyCellPath path)
    {
        return path switch
        {
            HierarchyCellPath.Standard => "standard",
            HierarchyCellPath.OptimizedTheme => "optimized-theme",
            HierarchyCellPath.OptimizedPresenter => "optimized-presenter",
            HierarchyCellPath.DirectHierarchy => "direct-hierarchy",
            HierarchyCellPath.BuiltInDrawn => "built-in-drawn",
            HierarchyCellPath.CustomDrawn => "custom-drawn",
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null),
        };
    }
}

public sealed class BenchmarkScenario : IDisposable
{
    public BenchmarkScenario(
        Window window,
        DataGrid grid,
        OptimizedHierarchyCellPathsViewModel viewModel)
    {
        Window = window;
        Grid = grid;
        ViewModel = viewModel;
    }

    public Window Window { get; }

    public DataGrid Grid { get; }

    public OptimizedHierarchyCellPathsViewModel ViewModel { get; }

    public void Dispose() => Window.Close();
}

public sealed class BenchmarkApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        AddDataGridStyle("Fluent.v2.xaml");
        AddDataGridStyle("Optimized.xaml");
        AddDataGridStyle("Flat.xaml");
    }

    private void AddDataGridStyle(string fileName)
    {
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/"))
        {
            Source = new Uri($"avares://Avalonia.Controls.DataGrid/Themes/{fileName}"),
        });
    }
}
