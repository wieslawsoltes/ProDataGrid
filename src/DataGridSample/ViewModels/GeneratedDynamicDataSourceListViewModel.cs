// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
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
    "LiveRows",
    ProviderName = "GeneratedTradeSchema",
    SourceMember = nameof(_source),
    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceList,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Diagnostics,
    OperationExecution = DataGridOperationExecution.ExternalPipeline,
    Streaming = true)]
[GenerateDataGridView(
    typeof(GeneratedTrade),
    ViewName = "GeneratedDynamicDataSourceListGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated DynamicData SourceList pipeline",
    AutomationId = "generated-dynamic-data-source-list-grid",
    ControllerName = "LiveRows",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query))]
public sealed partial class GeneratedDynamicDataSourceListViewModel : ReactiveObject, IDisposable
{
    private static readonly string[] s_symbols = ["AVLN", "RXUI", "GRID", "AOT"];
    private static readonly string[] s_desks = ["Warsaw", "London", "New York"];

    private readonly SourceList<GeneratedTrade> _source = new();
    private readonly CompositeDisposable _subscriptions = new();
    private readonly ReadOnlyObservableCollection<GeneratedTrade> _items;
    private int _nextId;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "The generated SourceList pipeline owns sort, filter, search, errors, and disposal.";

    [Reactive]
    private int _publishedItemCount;

    [Reactive]
    private int _batchCount;

    [Reactive]
    private int _errorCount;

    public GeneratedDynamicDataSourceListViewModel()
    {
        InitializeLiveRows(CreateLiveRowsController());
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;

        AddBatchCommand = ReactiveCommand.Create(() => AddBatch(12));
        SortPriceDescendingCommand = ReactiveCommand.Create(SortPriceDescending);
        ApplyWarsawFilterCommand = ReactiveCommand.Create(ApplyWarsawFilter);
        ClearOperationsCommand = ReactiveCommand.Create(ClearOperations);

        _items = ConnectLiveRowsPipeline();
        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));
        _subscriptions.Add(LiveRowsErrors.Subscribe(HandlePipelineError));

        AddBatch(24);
    }

    public ReadOnlyObservableCollection<GeneratedTrade> Items => _items;

    public SortingModel SortingModel => LiveRows.SortingModel;

    public FilteringModel FilteringModel => LiveRows.FilteringModel;

    public SearchModel SearchModel => LiveRows.SearchModel;

    public ReactiveCommand<RxVoid, RxVoid> AddBatchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SortPriceDescendingCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyWarsawFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearOperationsCommand { get; }

    public void Dispose()
    {
        _subscriptions.Dispose();
        DisposeLiveRows();
        _source.Dispose();
    }

    private void AddBatch(int count)
    {
        _source.Edit(rows =>
        {
            for (int index = 0; index < count; index++)
            {
                int id = ++_nextId;
                rows.Add(CreateTrade(id));
            }
        });

        PublishedItemCount += count;
        BatchCount++;
        Status = $"Published batch {BatchCount}: {count} rows; source total {PublishedItemCount}.";
    }

    private void SortPriceDescending()
    {
        LiveRows.SetSorting(
        [
            GeneratedTradeSchema.Price.Descending(),
            GeneratedTradeSchema.Id.Ascending()
        ]);
        Status = $"Compiled price ordering upstream at revision {LiveRows.Version}.";
    }

    private void ApplyWarsawFilter()
    {
        LiveRows.SetFiltering(
        [
            GeneratedTradeSchema.Desk.EqualTo("Warsaw"),
            GeneratedTradeSchema.Price.GreaterThanOrEqual(100m)
        ]);
        Status = $"Compiled Warsaw/price filtering upstream at revision {LiveRows.Version}.";
    }

    private void ClearOperations()
    {
        Query = string.Empty;
        LiveRows.ClearOperations();
        Status = $"Cleared live operations at revision {LiveRows.Version}.";
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            LiveRows.SetSearching(Array.Empty<SearchDescriptor>());
            return;
        }

        LiveRows.SetSearching(
        [
            GeneratedTradeSchema.Symbol.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedTradeSchema.Desk.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
        Status = $"Compiled live search '{query}' upstream at revision {LiveRows.Version}.";
    }

    private void HandlePipelineError(Exception error)
    {
        ErrorCount++;
        Status = $"Generated pipeline error {ErrorCount}: {error.Message}";
    }

    private static GeneratedTrade CreateTrade(int id) =>
        new()
        {
            Id = id,
            Symbol = s_symbols[id % s_symbols.Length],
            Desk = s_desks[id % s_desks.Length],
            Price = 50m + id * 7.25m,
            Quantity = 100 + id * 25,
            Timestamp = new DateTimeOffset(2026, 8, 8, 13, 0, 0, TimeSpan.Zero).AddSeconds(id)
        };
}
