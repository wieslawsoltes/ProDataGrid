using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using DynamicData;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedTrade), ProviderName = "GeneratedTradeSchema", Streaming = true)]
[GenerateDataGridController(
    typeof(GeneratedTrade),
    "Trades",
    ProviderName = "GeneratedTradeSchema",
    SourceMember = nameof(_source),
    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Selection |
               DataGridGeneratedFeatures.State |
               DataGridGeneratedFeatures.Diagnostics,
    OperationExecution = DataGridOperationExecution.ExternalPipeline,
    Streaming = true)]
[GenerateDataGridView(
    typeof(GeneratedTrade),
    ViewName = "GeneratedReactiveDataGridView",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Title = "Generated ReactiveUI + DynamicData view",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query))]
public sealed partial class GeneratedColumnsDynamicDataViewModel : ReactiveObject, IDisposable
{
    private static readonly string[] s_symbols = ["AVLN", "RXUI", "DDYN", "GRID", "AOT"];
    private static readonly string[] s_desks = ["Warsaw", "London", "New York"];
    private readonly SourceCache<GeneratedTrade, int> _source = new(static trade => trade.Id);
    private readonly CompositeDisposable _subscriptions = new();
    private readonly Random _random = new(2408);
    private readonly ReadOnlyObservableCollection<GeneratedTrade> _items;
    private int _nextId;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _deskFilter = string.Empty;

    [Reactive]
    private decimal _minimumPrice;

    public GeneratedColumnsDynamicDataViewModel()
    {
        InitializeTrades(CreateTradesController());
        SortingModel = Trades.SortingModel;
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        FilteringModel = Trades.FilteringModel;
        SearchModel = Trades.SearchModel;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;
        SearchModel.HighlightCurrent = true;
        SearchModel.WrapNavigation = true;

        AddStreamingBatchCommand = ReactiveCommand.Create(AddStreamingBatch);
        SortPriceDescendingCommand = ReactiveCommand.Create(SortPriceDescending);
        ClearSortsCommand = ReactiveCommand.Create(ClearSorts);
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);

        // Commands and source edits in this sample already run on the Avalonia UI thread.
        // Keeping the generated pipeline synchronous makes the initial snapshot and command
        // results immediately observable while callers with background sources can still pass
        // an explicit scheduler to ConnectTradesPipeline.
        _items = ConnectTradesPipeline();

        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));
        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName is nameof(DeskFilter) or nameof(MinimumPrice))
            .Subscribe(_ => ApplyFilters()));

        AddTrades(500);
    }

    public ReadOnlyObservableCollection<GeneratedTrade> Items => _items;

    public SortingModel SortingModel { get; }

    public FilteringModel FilteringModel { get; }

    public SearchModel SearchModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddStreamingBatchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SortPriceDescendingCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearSortsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearFiltersCommand { get; }

    private void AddStreamingBatch() => AddTrades(50);

    private void SortPriceDescending()
    {
        SortingModel.SetOrUpdate(GeneratedTradeSchema.Price.Descending());
    }

    private void ClearSorts() => SortingModel.Clear();

    private void ClearFilters()
    {
        DeskFilter = string.Empty;
        MinimumPrice = 0;
    }

    public void Dispose()
    {
        DisposeTrades();
        _subscriptions.Dispose();
        _source.Dispose();
    }

    private void AddTrades(int count)
    {
        _source.Edit(updater =>
        {
            for (int index = 0; index < count; index++)
            {
                int id = ++_nextId;
                updater.AddOrUpdate(new GeneratedTrade
                {
                    Id = id,
                    Symbol = s_symbols[id % s_symbols.Length],
                    Desk = s_desks[id % s_desks.Length],
                    Price = 20m + (decimal)(_random.NextDouble() * 180d),
                    Quantity = _random.Next(1, 5000),
                    Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(index)
                });
            }
        });
    }

    private void ApplySearch(string query)
    {
        SearchModel.Clear();
        if (!string.IsNullOrWhiteSpace(query))
        {
            SearchModel.SetOrUpdate(new SearchDescriptor(
                query,
                comparison: StringComparison.OrdinalIgnoreCase));
        }
    }

    private void ApplyFilters()
    {
        using IDisposable update = FilteringModel.DeferRefresh();
        FilteringModel.Clear();
        if (!string.IsNullOrWhiteSpace(DeskFilter))
        {
            FilteringModel.SetOrUpdate(
                GeneratedTradeSchema.Desk.Contains(DeskFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (MinimumPrice > 0)
        {
            FilteringModel.SetOrUpdate(GeneratedTradeSchema.Price.GreaterThanOrEqual(MinimumPrice));
        }
    }

}
