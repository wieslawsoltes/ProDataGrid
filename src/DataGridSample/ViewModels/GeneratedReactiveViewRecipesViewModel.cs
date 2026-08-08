// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedRecipeRow), ProviderName = "GeneratedRecipeRowSchema")]
[GenerateDataGridView(
    typeof(GeneratedRecipeRow),
    ViewName = "GeneratedRecipeGridOnlyView",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Grid-only recipe",
    AutomationId = "generated-recipe-grid-only",
    IsReadOnly = true)]
[GenerateDataGridView(
    typeof(GeneratedRecipeRow),
    ViewName = "GeneratedRecipeExplorerView",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Explorer,
    Title = "Explorer recipe",
    AutomationId = "generated-recipe-explorer",
    SearchTextPropertyName = nameof(Query),
    IsReadOnly = true)]
[GenerateDataGridView(
    typeof(GeneratedRecipeRow),
    ViewName = "GeneratedRecipeSpreadsheetView",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    Title = "Spreadsheet recipe",
    AutomationId = "generated-recipe-spreadsheet")]
[GenerateDataGridView(
    typeof(GeneratedRecipeRow),
    ViewName = "GeneratedRecipeAnalyticsView",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Analytics,
    Title = "Analytics recipe",
    AutomationId = "generated-recipe-analytics",
    SearchTextPropertyName = nameof(Query),
    IsReadOnly = true)]
public sealed partial class GeneratedReactiveViewRecipesViewModel : ReactiveObject, IDisposable
{
    private readonly ObservableCollection<GeneratedRecipeRow> _source = [];
    private readonly IDisposable _querySubscription;
    private int _nextId = 7;
    private bool _disposed;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Four generated ReactiveUI layouts share one strict schema and collection view.";

    [Reactive]
    private int _sourceRowCount;

    [Reactive]
    private int _visibleRowCount;

    public GeneratedReactiveViewRecipesViewModel()
    {
        AddBaselineRows();
        Items = GeneratedRecipeRowSchema.CreateCollectionView(_source, sourceIsInGroupOrder: false);

        AddRowCommand = ReactiveCommand.Create(AddRow);
        AdvanceCommand = ReactiveCommand.Create(Advance);
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);
        RestoreCommand = ReactiveCommand.Create(Restore);

        _querySubscription = Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch);

        Publish("GridOnly, Explorer, Spreadsheet, and Analytics views were generated independently.");
    }

    public DataGridCollectionView Items { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddRowCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AdvanceCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearSearchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RestoreCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _querySubscription.Dispose();
    }

    private void AddRow()
    {
        int id = _nextId++;
        _source.Add(CreateRow(
            id,
            $"Generated {id}",
            id % 2 == 0 ? "Runtime" : "Tooling",
            0.35d + (id % 5) * 0.12d,
            id % 3 != 0));
        Publish($"Added row {id}; every generated recipe observed the same collection delta.");
    }

    private void Advance()
    {
        if (_source.Count == 0)
        {
            return;
        }

        GeneratedRecipeRow current = _source[0];
        _source[0] = CreateRow(
            current.Id,
            current.Name,
            current.Area,
            Math.Min(1d, current.Progress + 0.1d),
            !current.IsEnabled);
        Publish($"Replaced stable row {current.Id}; progress and enabled state changed together.");
    }

    private void ClearSearch()
    {
        if (Query.Length == 0)
        {
            ApplySearch(string.Empty);
            return;
        }

        Query = string.Empty;
    }

    private void Restore()
    {
        Query = string.Empty;
        _source.Clear();
        _nextId = 7;
        AddBaselineRows();
        Publish("Restored six deterministic recipe rows.");
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Items.Filter = null;
            Publish("Generated Explorer and Analytics search boxes now show every row.");
            return;
        }

        Func<GeneratedRecipeRow, bool> predicate = GeneratedRecipeRowSchema.Instance.CreateSearchPredicate(
        [
            GeneratedRecipeRowSchema.Name.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedRecipeRowSchema.Area.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
        Items.Filter = item => item is GeneratedRecipeRow row && predicate(row);
        Publish($"Applied compiled search '{query}' through canonical Name and Area accessors.");
    }

    private void AddBaselineRows()
    {
        _source.Add(CreateRow(1, "Schema discovery", "Compiler", 1d, true));
        _source.Add(CreateRow(2, "Compiled bindings", "UI", 0.92d, true));
        _source.Add(CreateRow(3, "Reactive activation", "Runtime", 0.84d, true));
        _source.Add(CreateRow(4, "Formula bar slot", "Spreadsheet", 0.72d, false));
        _source.Add(CreateRow(5, "Analytics slot", "Charts", 0.66d, true));
        _source.Add(CreateRow(6, "Explorer slot", "Navigation", 0.58d, true));
    }

    private static GeneratedRecipeRow CreateRow(
        int id,
        string name,
        string area,
        double progress,
        bool isEnabled) =>
        new()
        {
            Id = id,
            Name = name,
            Area = area,
            Progress = progress,
            Updated = new DateTimeOffset(2026, 8, 8, 9, id, 0, TimeSpan.Zero),
            IsEnabled = isEnabled
        };

    private void Publish(string message)
    {
        SourceRowCount = _source.Count;
        VisibleRowCount = Items.Count;
        Status = message;
    }
}
