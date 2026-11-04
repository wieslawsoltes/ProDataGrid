using System.ComponentModel;
using Avalonia.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ReactiveUI.Avalonia;

namespace ProDataGrid.FlatLayout.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
public class VirtualSurfaceSortingBenchmarks
{
    private const int RowCount = 250_000;
    private static readonly string[] s_owners =
        { "Atlas", "Beacon", "Cirrus", "Delta", "Ember", "Flux", "Gaia", "Helix" };

    private SortRow[] _rows = null!;
    private DataGridSortDescription _sort = null!;

    [GlobalSetup]
    public void Setup()
    {
        _rows = new SortRow[RowCount];
        for (int index = 0; index < _rows.Length; index++)
        {
            _rows[index] = new SortRow(
                index + 1,
                s_owners[(index / 6) % s_owners.Length]);
        }

        var accessor = new DataGridColumnValueAccessor<SortRow, string>(static row => row.Owner);
        _sort = DataGridSortDescription.FromAccessor(
            accessor,
            ListSortDirection.Ascending,
            propertyPath: nameof(SortRow.Owner));
    }

    [Benchmark]
    public object[] SortOwner() => _sort.OrderBy(_rows).ToArray();

    private sealed record SortRow(int Id, string Owner);
}

[MemoryDiagnoser]
[MedianColumn]
public class VirtualSurfaceSortInteractionBenchmarks
{
    private static bool s_applicationInitialized;
    private Window _window = null!;
    private OptimizedFlatCellPathsViewModel _viewModel = null!;
    private ListSortDirection _direction;

    [Params(false, true)]
    public bool ActiveSearch { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        EnsureApplication();
        var page = new VirtualSurfaceFlatDataPage();
        _window = new Window
        {
            Width = 1_200,
            Height = 760,
            Content = page,
        };
        _window.Show();
        PumpLayout();

        _viewModel = page.DataContext as OptimizedFlatCellPathsViewModel
            ?? throw new InvalidOperationException("The virtual-surface page did not create its ViewModel.");
        _viewModel.TargetRowCount = 250_000;
        WaitWithDispatcher(_viewModel.LoadRepresentativeWorkloadAsync());
        PumpLayout();

        if (ActiveSearch)
        {
            _viewModel.Operations.SearchModel.HighlightMode = SearchHighlightMode.None;
            _viewModel.Operations.SearchModel.SetOrUpdate(new SearchDescriptor(
                "Delta",
                matchMode: SearchMatchMode.Contains,
                scope: SearchScope.VisibleColumns,
                comparison: StringComparison.OrdinalIgnoreCase));
            PumpLayout();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _window.Close();

    [Benchmark]
    public int SortOwnerFromHeaderModel()
    {
        _direction = _direction == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _viewModel.Operations.SortingModel.SetOrUpdate(new SortingDescriptor(
            nameof(OptimizedCellSampleRow.Owner),
            _direction,
            nameof(OptimizedCellSampleRow.Owner)));
        PumpLayout();
        return _viewModel.Operations.SearchModel.Results.Count;
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

    private static void WaitWithDispatcher(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Yield();
        }

        task.GetAwaiter().GetResult();
    }

    private void PumpLayout()
    {
        Dispatcher.UIThread.RunJobs();
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
