// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Selection;
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
    "KeyedRows",
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
    ViewName = "GeneratedDynamicDataSourceCacheGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated DynamicData SourceCache pipeline",
    AutomationId = "generated-dynamic-data-source-cache-grid",
    ControllerName = "KeyedRows",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query),
    SelectionModelPropertyName = nameof(SelectionModel))]
public sealed partial class GeneratedDynamicDataSourceCacheViewModel : ReactiveObject, IDisposable
{
    private const int TrackedTradeKey = 8;
    private static readonly string[] s_symbols = ["AVLN", "RXUI", "GRID", "AOT"];
    private static readonly string[] s_desks = ["Warsaw", "London", "New York"];

    private readonly SourceCache<GeneratedTrade, int> _source = new(static trade => trade.Id);
    private readonly CompositeDisposable _subscriptions = new();
    private readonly ReadOnlyObservableCollection<GeneratedTrade> _items;
    private int _nextId;
    private bool _disposed;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Generated keys keep selection attached while cache replacements reorder rows.";

    [Reactive]
    private int _cacheItemCount;

    [Reactive]
    private int _batchCount;

    [Reactive]
    private int _replacementCount;

    [Reactive]
    private int _errorCount;

    [Reactive]
    private int? _selectedKey;

    [Reactive]
    private decimal? _selectedPrice;

    public GeneratedDynamicDataSourceCacheViewModel()
    {
        InitializeKeyedRows(CreateKeyedRowsController());
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;

        SelectionModel = GeneratedTradeSchema.CreateIdentitySelectionModel();
        SelectionModel.SingleSelect = true;
        SelectionModel.SelectionChanged += SelectionModelOnSelectionChanged;

        AddBatchCommand = ReactiveCommand.Create(() => AddBatch(6));
        RunReplacementScenarioCommand = ReactiveCommand.Create(RunReplacementScenario);
        SelectTrackedTradeCommand = ReactiveCommand.Create(SelectTrackedTrade);
        ReplaceSelectedTradeCommand = ReactiveCommand.Create(ReplaceSelectedTrade);
        SortPriceDescendingCommand = ReactiveCommand.Create(SortPriceDescending);
        ApplyLondonFilterCommand = ReactiveCommand.Create(ApplyLondonFilter);
        ClearOperationsCommand = ReactiveCommand.Create(ClearOperations);

        _items = ConnectKeyedRowsPipeline();
        SelectionModel.Source = _items;
        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));
        _subscriptions.Add(KeyedRowsErrors.Subscribe(HandlePipelineError));

        KeyedRows.SetSorting(
        [
            GeneratedTradeSchema.Price.Ascending(),
            GeneratedTradeSchema.Id.Ascending()
        ]);
        AddBatch(18);
    }

    public ReadOnlyObservableCollection<GeneratedTrade> Items => _items;

    public SortingModel SortingModel => KeyedRows.SortingModel;

    public FilteringModel FilteringModel => KeyedRows.FilteringModel;

    public SearchModel SearchModel => KeyedRows.SearchModel;

    public IdentitySelectionModel SelectionModel { get; }

    public int TrackedKey => TrackedTradeKey;

    public bool IsDisposed => _disposed;

    public ReactiveCommand<RxVoid, RxVoid> AddBatchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RunReplacementScenarioCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SelectTrackedTradeCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceSelectedTradeCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SortPriceDescendingCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyLondonFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearOperationsCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SelectionModel.SelectionChanged -= SelectionModelOnSelectionChanged;
        SelectionModel.Source = Array.Empty<GeneratedTrade>();
        _subscriptions.Dispose();
        DisposeKeyedRows();
        _source.Dispose();
    }

    private void AddBatch(int count)
    {
        _source.Edit(cache =>
        {
            for (int index = 0; index < count; index++)
            {
                int id = ++_nextId;
                cache.AddOrUpdate(CreateTrade(id));
            }
        });

        CacheItemCount += count;
        BatchCount++;
        Status = $"Published cache batch {BatchCount}: {count} additions; cache total {CacheItemCount}.";
    }

    private void RunReplacementScenario()
    {
        KeyedRows.SetSorting(
        [
            GeneratedTradeSchema.Price.Descending(),
            GeneratedTradeSchema.Id.Ascending()
        ]);
        SelectTrackedTrade();
        ReplaceSelectedTrade();
    }

    private void SelectTrackedTrade()
    {
        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].Id != TrackedTradeKey)
            {
                continue;
            }

            SelectionModel.SelectedIndex = index;
            Status = $"Tracking stable key {TrackedTradeKey} before a replacement moves its row.";
            return;
        }

        Status = $"Stable key {TrackedTradeKey} is not visible under the current operations.";
    }

    private void ReplaceSelectedTrade()
    {
        if (SelectionModel.SelectedItem is not GeneratedTrade selected)
        {
            Status = "Select a visible trade before replacing it.";
            return;
        }

        var replacement = new GeneratedTrade
        {
            Id = selected.Id,
            Symbol = selected.Symbol,
            Desk = selected.Desk,
            Price = 999m + ReplacementCount,
            Quantity = selected.Quantity + 1_000,
            Timestamp = selected.Timestamp.AddMinutes(1)
        };

        _source.AddOrUpdate(replacement);
        ReplacementCount++;
        Status = $"Replaced key {replacement.Id}; generated identity selection is restoring it after the sorted move.";
    }

    private void SortPriceDescending()
    {
        KeyedRows.SetSorting(
        [
            GeneratedTradeSchema.Price.Descending(),
            GeneratedTradeSchema.Id.Ascending()
        ]);
        Status = $"Compiled price ordering upstream at revision {KeyedRows.Version}.";
    }

    private void ApplyLondonFilter()
    {
        KeyedRows.SetFiltering(
        [
            GeneratedTradeSchema.Desk.EqualTo("London"),
            GeneratedTradeSchema.Price.GreaterThanOrEqual(70m)
        ]);
        Status = $"Compiled London/price filtering upstream at revision {KeyedRows.Version}.";
    }

    private void ClearOperations()
    {
        Query = string.Empty;
        KeyedRows.ClearOperations();
        Status = $"Cleared keyed operations at revision {KeyedRows.Version}.";
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            KeyedRows.SetSearching(Array.Empty<SearchDescriptor>());
            return;
        }

        KeyedRows.SetSearching(
        [
            GeneratedTradeSchema.Symbol.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedTradeSchema.Desk.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
        Status = $"Compiled cache search '{query}' upstream at revision {KeyedRows.Version}.";
    }

    private void HandlePipelineError(Exception error)
    {
        ErrorCount++;
        Status = $"Generated cache pipeline error {ErrorCount}: {error.Message}";
    }

    private void SelectionModelOnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e)
    {
        if (SelectionModel.SelectedItem is not GeneratedTrade selected)
        {
            SelectedKey = null;
            SelectedPrice = null;
            return;
        }

        SelectedKey = selected.Id;
        SelectedPrice = selected.Price;
        Status = ReplacementCount > 0 && selected.Id == TrackedTradeKey
            ? $"Selection preserved stable key {selected.Id} on replacement price {selected.Price:N2}."
            : $"Selected stable key {selected.Id} at price {selected.Price:N2}.";
    }

    private static GeneratedTrade CreateTrade(int id) =>
        new()
        {
            Id = id,
            Symbol = s_symbols[id % s_symbols.Length],
            Desk = s_desks[id % s_desks.Length],
            Price = 30m + id * 8.5m,
            Quantity = 100 + id * 40,
            Timestamp = new DateTimeOffset(2026, 8, 8, 14, 0, 0, TimeSpan.Zero).AddSeconds(id)
        };
}
