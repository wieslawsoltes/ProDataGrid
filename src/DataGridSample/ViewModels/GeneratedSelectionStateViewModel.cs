// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Selection;
using DataGridSample.Models;
using DataGridSample.Pages;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedFeatureRow), ProviderName = "GeneratedFeatureRowSchema")]
[GenerateDataGridController(
    typeof(GeneratedFeatureRow),
    "StatefulRows",
    ProviderName = "GeneratedFeatureRowSchema",
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching,
    OperationExecution = DataGridOperationExecution.View)]
[GenerateDataGridView(
    typeof(GeneratedFeatureRow),
    ViewName = "GeneratedSelectionStateGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated keyed selection and state",
    AutomationId = "generated-selection-state-grid",
    ControllerName = "StatefulRows",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query),
    SelectionModelPropertyName = nameof(SelectionModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.FullRow,
    StateControllerPropertyName = nameof(StateController),
    InteractionPropertyNames = [nameof(ManageGridState)],
    InteractionHandlerTypes = [typeof(GeneratedSelectionStateInteractionHandler)])]
public sealed partial class GeneratedSelectionStateViewModel : ReactiveObject, IDisposable
{
    private const int PageSize = 8;
    private readonly IReadOnlyList<GeneratedFeatureRow> _allRows;
    private readonly Dictionary<int, GeneratedFeatureRow> _rowsByKey;
    private readonly ObservableCollection<GeneratedFeatureRow> _items = [];
    private readonly CompositeDisposable _subscriptions = new();
    private bool _synchronizingSelection;
    private bool _disposed;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Generated item/column keys drive selection, paging, and versioned state.";

    [Reactive]
    private string _selectedKeys = "None";

    [Reactive]
    private int _loadedSelectedCount;

    [Reactive]
    private int _pageNumber = 1;

    [Reactive]
    private int _statePayloadLength;

    [Reactive]
    private int _migrationCount;

    public GeneratedSelectionStateViewModel()
    {
        _allRows = CreateRows();
        _rowsByKey = new Dictionary<int, GeneratedFeatureRow>(_allRows.Count);
        for (int index = 0; index < _allRows.Count; index++)
        {
            _rowsByKey.Add(_allRows[index].Id, _allRows[index]);
        }

        InitializeStatefulRows(CreateStatefulRowsController());
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;

        SelectionController = GeneratedFeatureRowSchema.CreateSelectionController(
            new DataGridGeneratedSelectionProfile
            {
                Mode = DataGridSelectionMode.Extended,
                Unit = DataGridSelectionUnit.FullRow,
                PreserveUnloadedKeys = true
            });
        SelectionModel = SelectionController.CreateIdentitySelectionModel(_items);
        SetPage(0, publishStatus: false);
        SelectionModel.SelectionChanged += SelectionModelOnSelectionChanged;
        SelectionController.SelectionChanged += SelectionControllerOnSelectionChanged;
        StateController = GeneratedFeatureRowSchema.CreateStateController(
            ResolveItem,
            migration: MigrateState);

        SelectStableKeysCommand = ReactiveCommand.Create(SelectStableKeys);
        FirstPageCommand = ReactiveCommand.Create(() => SetPage(0));
        NextPageCommand = ReactiveCommand.Create(() => SetPage(1));
        ReplaceAndReorderCommand = ReactiveCommand.Create(ReplaceAndReorder);
        PrepareStateCommand = ReactiveCommand.Create(PrepareStateScenario);
        CaptureStateCommand = ReactiveCommand.CreateFromTask(() => CaptureStateAsync(legacyV1: false));
        ScrambleStateCommand = ReactiveCommand.CreateFromTask(ScrambleStateAsync);
        RestoreStateCommand = ReactiveCommand.CreateFromTask(RestoreStateAsync);
        RoundTripStateCommand = ReactiveCommand.CreateFromTask(() => RoundTripStateAsync(legacyV1: false));
        LegacyRoundTripCommand = ReactiveCommand.CreateFromTask(() => RoundTripStateAsync(legacyV1: true));

        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));
        RefreshSelectionProjection();
    }

    public ObservableCollection<GeneratedFeatureRow> Items => _items;

    public SortingModel SortingModel => StatefulRows.SortingModel;

    public FilteringModel FilteringModel => StatefulRows.FilteringModel;

    public SearchModel SearchModel => StatefulRows.SearchModel;

    public DataGridGeneratedSelectionController<GeneratedFeatureRow, int> SelectionController { get; }

    public IdentitySelectionModel SelectionModel { get; }

    public DataGridGeneratedStateController StateController { get; }

    public Interaction<GeneratedSelectionStateRequest, GeneratedSelectionStateResult> ManageGridState { get; } = new();

    public string? StatePayload { get; private set; }

    public bool IsDisposed => _disposed;

    public ReactiveCommand<RxVoid, RxVoid> SelectStableKeysCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> FirstPageCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> NextPageCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ReplaceAndReorderCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> PrepareStateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CaptureStateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ScrambleStateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RestoreStateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RoundTripStateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> LegacyRoundTripCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        SelectionModel.SelectionChanged -= SelectionModelOnSelectionChanged;
        SelectionController.SelectionChanged -= SelectionControllerOnSelectionChanged;
        SelectionModel.Source = Array.Empty<GeneratedFeatureRow>();
        _subscriptions.Dispose();
        DisposeStatefulRows();
    }

    internal void ClearGeneratedStateScenario()
    {
        StatefulRows.ClearOperations();
        _synchronizingSelection = true;
        try
        {
            SelectionController.Clear(DataGridGeneratedSelectionOrigin.Programmatic);
            SelectionController.ApplyTo(SelectionModel);
        }
        finally
        {
            _synchronizingSelection = false;
        }
        RefreshSelectionProjection();
    }

    internal void SynchronizeGeneratedSelection(DataGridGeneratedSelectionOrigin origin)
    {
        if (_synchronizingSelection)
        {
            return;
        }
        _synchronizingSelection = true;
        try
        {
            SelectionController.CaptureFrom(SelectionModel, origin);
        }
        finally
        {
            _synchronizingSelection = false;
        }
        RefreshSelectionProjection();
    }

    private void SelectStableKeys()
    {
        _synchronizingSelection = true;
        try
        {
            SelectionController.Clear(DataGridGeneratedSelectionOrigin.Programmatic);
            SelectionController.SelectKey(1, DataGridGeneratedSelectionOrigin.Programmatic);
            SelectionController.SelectKey(4, DataGridGeneratedSelectionOrigin.Programmatic);
            SelectionController.ApplyTo(SelectionModel);
        }
        finally
        {
            _synchronizingSelection = false;
        }
        RefreshSelectionProjection();
        Status = "Selected stable keys 1 and 4; unloaded keys remain in the generated controller.";
    }

    private void SetPage(int pageIndex, bool publishStatus = true)
    {
        int offset = pageIndex * PageSize;
        _synchronizingSelection = true;
        try
        {
            _items.Clear();
            int end = Math.Min(_allRows.Count, offset + PageSize);
            for (int index = offset; index < end; index++)
            {
                _items.Add(_allRows[index]);
            }
            SelectionController.ResetSource(_items, DataGridGeneratedSelectionOrigin.Model);
            SelectionController.ApplyTo(SelectionModel);
        }
        finally
        {
            _synchronizingSelection = false;
        }
        PageNumber = pageIndex + 1;
        RefreshSelectionProjection();
        if (publishStatus)
        {
            Status = $"Loaded page {PageNumber}; {LoadedSelectedCount} selected keys are currently materialized.";
        }
    }

    private void ReplaceAndReorder()
    {
        if (PageNumber != 1)
        {
            SetPage(0, publishStatus: false);
        }
        _synchronizingSelection = true;
        try
        {
            GeneratedFeatureRow[] replacements = _items
                .Select(static row => new GeneratedFeatureRow
                {
                    Id = row.Id,
                    Symbol = row.Symbol + "*",
                    Desk = row.Desk,
                    Amount = row.Amount + 250m,
                    Timestamp = row.Timestamp.AddMinutes(1)
                })
                .Reverse()
                .ToArray();
            _items.Clear();
            for (int index = 0; index < replacements.Length; index++)
            {
                _items.Add(replacements[index]);
            }
            SelectionController.ResetSource(_items, DataGridGeneratedSelectionOrigin.Model);
            SelectionController.ApplyTo(SelectionModel);
        }
        finally
        {
            _synchronizingSelection = false;
        }
        RefreshSelectionProjection();
        Status = "Replaced and reversed page 1; selection followed stable keys, not row instances or indexes.";
    }

    private void PrepareStateScenario()
    {
        StatefulRows.SetSorting(
        [
            GeneratedFeatureRowSchema.Amount.Descending(),
            GeneratedFeatureRowSchema.Id.Ascending()
        ]);
        StatefulRows.SetFiltering([GeneratedFeatureRowSchema.Desk.EqualTo("Warsaw")]);
        Query = "Warsaw";
        SelectStableKeys();
        Status = "Prepared typed sorting, filtering, search, and keyed selection for full-state capture.";
    }

    private async Task CaptureStateAsync(bool legacyV1)
    {
        GeneratedSelectionStateOperation operation = legacyV1
            ? GeneratedSelectionStateOperation.CaptureLegacyV1
            : GeneratedSelectionStateOperation.Capture;
        GeneratedSelectionStateResult result = await ManageGridState
            .Handle(new GeneratedSelectionStateRequest(operation))
            .ToTask();
        StatePayload = result.Payload;
        StatePayloadLength = StatePayload?.Length ?? 0;
        Status = $"{result.Message} JSON: {StatePayloadLength} bytes.";
    }

    private async Task ScrambleStateAsync()
    {
        GeneratedSelectionStateResult result = await ManageGridState
            .Handle(new GeneratedSelectionStateRequest(GeneratedSelectionStateOperation.Scramble))
            .ToTask();
        Status = result.Message;
    }

    private async Task RestoreStateAsync()
    {
        GeneratedSelectionStateResult result = await ManageGridState
            .Handle(new GeneratedSelectionStateRequest(GeneratedSelectionStateOperation.Restore, StatePayload))
            .ToTask();
        RefreshSelectionProjection();
        Status = $"{result.Message} Selected keys: {SelectedKeys}.";
    }

    private async Task RoundTripStateAsync(bool legacyV1)
    {
        PrepareStateScenario();
        await CaptureStateAsync(legacyV1);
        await ScrambleStateAsync();
        await RestoreStateAsync();
        Status = legacyV1
            ? $"Migrated version 1 and restored version 2 state; aliases applied, migrations: {MigrationCount}."
            : $"Full generated state round-trip restored {SelectedKeys}; JSON: {StatePayloadLength} bytes.";
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            StatefulRows.SetSearching(Array.Empty<SearchDescriptor>());
            return;
        }
        StatefulRows.SetSearching(
        [
            GeneratedFeatureRowSchema.Symbol.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedFeatureRowSchema.Desk.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
    }

    private GeneratedFeatureRow ResolveItem(int key)
    {
        for (int index = 0; index < _items.Count; index++)
        {
            if (_items[index].Id == key)
            {
                return _items[index];
            }
        }
        return _rowsByKey.TryGetValue(key, out GeneratedFeatureRow? row) ? row : null!;
    }

    private bool MigrateState(int fromVersion, int toVersion, ref DataGridPersistedState state)
    {
        if (fromVersion != 1 || toVersion != GeneratedFeatureRowSchema.StateVersion)
        {
            return false;
        }
        MigrationCount++;
        return true;
    }

    private void SelectionModelOnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e) =>
        SynchronizeGeneratedSelection(DataGridGeneratedSelectionOrigin.Model);

    private void SelectionControllerOnSelectionChanged(object? sender, DataGridGeneratedSelectionChangedEventArgs e)
    {
        if (!_synchronizingSelection && e.Origin != DataGridGeneratedSelectionOrigin.Model)
        {
            _synchronizingSelection = true;
            try
            {
                SelectionController.ApplyTo(SelectionModel);
            }
            finally
            {
                _synchronizingSelection = false;
            }
        }
        RefreshSelectionProjection();
    }

    private void RefreshSelectionProjection()
    {
        SelectedKeys = SelectionController.SelectedItemKeys.Count == 0
            ? "None"
            : string.Join(", ", SelectionController.SelectedItemKeys);
        LoadedSelectedCount = SelectionController.GetSelectedItems().Count;
    }

    private static IReadOnlyList<GeneratedFeatureRow> CreateRows()
    {
        string[] symbols = ["AOT", "GRID", "RXUI", "FAST", "STATE", "KEYED"];
        string[] desks = ["Warsaw", "London", "New York"];
        var rows = new List<GeneratedFeatureRow>(16);
        DateTimeOffset origin = new(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
        for (int id = 1; id <= 16; id++)
        {
            rows.Add(new GeneratedFeatureRow
            {
                Id = id,
                Symbol = $"{symbols[(id - 1) % symbols.Length]}-{id:00}",
                Desk = desks[(id - 1) % desks.Length],
                Amount = 50_000m + id * 12_750m,
                Timestamp = origin.AddMinutes(id * 9)
            });
        }
        return rows;
    }
}
