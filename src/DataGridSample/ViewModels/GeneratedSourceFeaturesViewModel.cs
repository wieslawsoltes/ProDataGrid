using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedFeatureRow), ProviderName = "GeneratedFeatureRowSchema", Streaming = true)]
public sealed partial class GeneratedSourceFeaturesViewModel : ReactiveObject, IDisposable
{
    private readonly Dictionary<int, GeneratedFeatureRow> _byId = new();
    private readonly IReadOnlyList<IDataGridGeneratedSummary<GeneratedFeatureRow>> _summaries;
    private int _nextId = 4;

    [Reactive]
    private string _status = "Ready: grouped collection view, typed summaries, validation, fill, clipboard, and undo are generated.";

    public GeneratedSourceFeaturesViewModel()
    {
        Items = new ObservableCollection<GeneratedFeatureRow>
        {
            new() { Id = 1, Symbol = "AVLN", Desk = "Warsaw", Amount = 125_000m, Timestamp = DateTimeOffset.Now.AddMinutes(-3) },
            new() { Id = 2, Symbol = "RXUI", Desk = "London", Amount = 74_500m, Timestamp = DateTimeOffset.Now.AddMinutes(-2) },
            new() { Id = 3, Symbol = "GRID", Desk = "Warsaw", Amount = 98_250m, Timestamp = DateTimeOffset.Now.AddMinutes(-1) }
        };
        foreach (GeneratedFeatureRow item in Items) _byId.Add(item.Id, item);

        ItemsView = GeneratedFeatureRowSchema.CreateCollectionView(Items);
        EditController = GeneratedFeatureRowSchema.CreateEditController(key => _byId[key]);
        ClipboardController = GeneratedFeatureRowSchema.CreateClipboardController(EditController);
        FillController = GeneratedFeatureRowSchema.CreateFillController(EditController);
        OperationController = GeneratedFeatureRowSchema.CreateController();
        ColumnLayoutController = new DataGridGeneratedColumnLayoutController(
            ColumnDefinitions,
            GeneratedFeatureRowSchema.BandFields);
        HeaderCommandController = new DataGridGeneratedHeaderCommandController<GeneratedFeatureRow>(
            GeneratedFeatureRowSchema.Instance.Manifest,
            OperationController,
            ColumnLayoutController);
        AmountHeaderCommands = HeaderCommandController.ForField("amount");
        _summaries = GeneratedFeatureRowSchema.CreateSummaries();
        ResetSummaries();

        AddCommand = ReactiveCommand.Create(Add);
        FillCommand = ReactiveCommand.Create(FillAmounts);
        UndoCommand = ReactiveCommand.Create(Undo);
        ExportCommand = ReactiveCommand.Create(Export);
        SummarizeCommand = ReactiveCommand.Create(Summarize);
    }

    public ObservableCollection<GeneratedFeatureRow> Items { get; }
    public DataGridCollectionView ItemsView { get; }
    public DataGridGeneratedEditController<GeneratedFeatureRow, int> EditController { get; }
    public DataGridGeneratedClipboardController<GeneratedFeatureRow, int> ClipboardController { get; }
    public DataGridGeneratedFillController<GeneratedFeatureRow, int> FillController { get; }
    public DataGridGeneratedOperationController<GeneratedFeatureRow> OperationController { get; }
    public DataGridGeneratedColumnLayoutController ColumnLayoutController { get; }
    public DataGridGeneratedHeaderCommandController<GeneratedFeatureRow> HeaderCommandController { get; }
    public DataGridGeneratedHeaderCommandSet AmountHeaderCommands { get; }
    public SortingModel SortingModel => OperationController.SortingModel;
    public FilteringModel FilteringModel => OperationController.FilteringModel;
    public ReactiveCommand<RxVoid, RxVoid> AddCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> FillCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> UndoCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ExportCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> SummarizeCommand { get; }

    private void Add()
    {
        var item = new GeneratedFeatureRow
        {
            Id = _nextId++,
            Symbol = "NEW",
            Desk = _nextId % 2 == 0 ? "Warsaw" : "London",
            Amount = 50_000m + _nextId * 1_250m,
            Timestamp = DateTimeOffset.Now
        };
        _byId.Add(item.Id, item);
        Items.Add(item);
        ResetSummaries();
        Status = $"Added key {item.Id}; collection-view grouping updates from the observable source.";
    }

    private void FillAmounts()
    {
        DataGridGeneratedTransferResult<int> result = FillController.Fill(
            Items, "amount", 0, index => 25_000m + index * 10_000m);
        ResetSummaries();
        Status = $"Generated typed fill applied {result.AppliedCells} cells as one undo unit.";
    }

    private void Undo()
    {
        bool changed = EditController.Undo();
        ResetSummaries();
        Status = changed ? "Undid the latest generated edit batch by stable item key." : "Nothing to undo.";
    }

    private void Export()
    {
        string csv = ClipboardController.Export(
            Items,
            ["symbol", "desk", "amount"],
            DataGridGeneratedExportFormat.Csv,
            formatProvider: CultureInfo.InvariantCulture);
        Status = $"Generated CSV ({csv.Length} chars): {csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0]}";
    }

    private void Summarize()
    {
        ResetSummaries();
        Status = $"Generated summaries: sum={_summaries[0].Value:N2}, average={_summaries[1].Value:N2}; groups={GeneratedFeatureRowSchema.GroupFields.Count}.";
    }

    private void ResetSummaries()
    {
        for (int index = 0; index < _summaries.Count; index++) _summaries[index].Reset(Items);
    }

    public void Dispose()
    {
        HeaderCommandController.Dispose();
        ColumnLayoutController.Dispose();
        OperationController.Dispose();
        EditController.Dispose();
    }
}
