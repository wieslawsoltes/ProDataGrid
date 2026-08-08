// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedFeatureRow), ProviderName = "GeneratedFeatureRowSchema")]
[GenerateDataGridController(
    typeof(GeneratedFeatureRow),
    "Operations",
    ProviderName = "GeneratedFeatureRowSchema",
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching,
    OperationExecution = DataGridOperationExecution.View)]
[GenerateDataGridView(
    typeof(GeneratedFeatureRow),
    ViewName = "GeneratedOperationsControllerGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated typed operation grid",
    AutomationId = "generated-operations-controller-grid",
    ControllerName = "Operations",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query))]
public sealed partial class GeneratedOperationsControllerViewModel : ReactiveObject, IDisposable
{
    private static readonly DataGridGeneratedOperationPreset s_riskPreset = new(
        "Warsaw high value",
        sorting: [GeneratedFeatureRowSchema.Amount.Descending()],
        filtering:
        [
            GeneratedFeatureRowSchema.Desk.EqualTo("Warsaw"),
            GeneratedFeatureRowSchema.Amount.GreaterThanOrEqual(100_000m)
        ]);

    private readonly ObservableCollection<GeneratedFeatureRow> _items;
    private readonly IDisposable _querySubscription;
    private int _nextId = 7;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Use the generated search box, sort headers, or apply the typed preset.";

    public GeneratedOperationsControllerViewModel()
    {
        _items =
        [
            CreateRow(1, "AVLN", "Warsaw", 125_000m, 1),
            CreateRow(2, "RXUI", "London", 74_500m, 2),
            CreateRow(3, "GRID", "Warsaw", 98_250m, 3),
            CreateRow(4, "AOT", "New York", 205_000m, 4),
            CreateRow(5, "FAST", "Warsaw", 160_750m, 5),
            CreateRow(6, "LIVE", "London", 112_400m, 6)
        ];

        InitializeOperations(CreateOperationsController());
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;

        ApplyRiskPresetCommand = ReactiveCommand.Create(ApplyRiskPreset);
        ClearOperationsCommand = ReactiveCommand.Create(ClearOperations);
        AddRowCommand = ReactiveCommand.Create(AddRow);

        _querySubscription = Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch);
    }

    public ObservableCollection<GeneratedFeatureRow> Items => _items;

    public SortingModel SortingModel => Operations.SortingModel;

    public FilteringModel FilteringModel => Operations.FilteringModel;

    public SearchModel SearchModel => Operations.SearchModel;

    public ReactiveCommand<RxVoid, RxVoid> ApplyRiskPresetCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearOperationsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddRowCommand { get; }

    public void Dispose()
    {
        _querySubscription.Dispose();
        DisposeOperations();
    }

    private void ApplyRiskPreset()
    {
        Query = string.Empty;
        Operations.ApplyPreset(s_riskPreset);
        Status = $"Applied '{s_riskPreset.Name}' as controller revision {Operations.Version}.";
    }

    private void ClearOperations()
    {
        Query = string.Empty;
        Operations.ClearOperations();
        Status = $"Cleared generated operations at revision {Operations.Version}.";
    }

    private void AddRow()
    {
        int id = _nextId++;
        _items.Add(CreateRow(
            id,
            id % 2 == 0 ? "GEN" : "PIPE",
            id % 3 == 0 ? "London" : "Warsaw",
            80_000m + id * 7_500m,
            id));
        Status = $"Added row {id}; active generated predicates remain owned by the controller.";
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Operations.SetSearching(Array.Empty<SearchDescriptor>());
            Status = "Search cleared; all searchable generated fields are available.";
            return;
        }

        Operations.SetSearching(
        [
            GeneratedFeatureRowSchema.Symbol.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedFeatureRowSchema.Desk.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
        Status = $"Compiled search '{query}' at controller revision {Operations.Version}.";
    }

    private static GeneratedFeatureRow CreateRow(
        int id,
        string symbol,
        string desk,
        decimal amount,
        int minute) =>
        new()
        {
            Id = id,
            Symbol = symbol,
            Desk = desk,
            Amount = amount,
            Timestamp = new DateTimeOffset(2026, 8, 8, 12, minute, 0, TimeSpan.Zero)
        };
}
