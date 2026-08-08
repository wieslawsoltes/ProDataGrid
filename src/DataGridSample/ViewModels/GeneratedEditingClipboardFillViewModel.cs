// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedEditableOrder), ProviderName = "GeneratedEditableOrderSchema")]
[GenerateDataGridView(
    typeof(GeneratedEditableOrder),
    ViewName = "GeneratedEditingClipboardFillGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    Title = "Generated editing, clipboard, fill, and undo",
    AutomationId = "generated-editing-clipboard-fill-grid",
    ItemsPropertyName = nameof(ItemsView),
    ClipboardImportModelPropertyName = nameof(ClipboardImportModel),
    FillModelPropertyName = nameof(FillModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
    EditTriggers = DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.TextInput | DataGridEditTriggers.F2,
    ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader)]
public sealed partial class GeneratedEditingClipboardFillViewModel : ReactiveObject, IDisposable
{
    private static readonly string[] s_transferColumns = ["product", "quantity", "unit-price", "discount"];
    private static readonly DataGridGeneratedExportFormat[] s_exportFormats =
    [
        DataGridGeneratedExportFormat.Csv,
        DataGridGeneratedExportFormat.Json,
        DataGridGeneratedExportFormat.Markdown,
        DataGridGeneratedExportFormat.Html,
        DataGridGeneratedExportFormat.Xml,
        DataGridGeneratedExportFormat.Yaml
    ];

    private readonly Dictionary<int, GeneratedEditableOrder> _byId = new();
    private int _exportFormatIndex;
    private bool _disposed;

    [Reactive]
    private string _status = "Generated typed edit fields are ready; DataGrid paste and fill use reflection-free adapters.";

    [Reactive]
    private string _pasteText = "omega\t12\t44.125\t0.10\nx\t0\tinvalid\t0.75";

    [Reactive]
    private string _exportPreview = "Export preview will appear here.";

    [Reactive]
    private bool _canUndo;

    [Reactive]
    private bool _canRedo;

    [Reactive]
    private int _lastAppliedCells;

    [Reactive]
    private int _lastErrorCount;

    public GeneratedEditingClipboardFillViewModel()
    {
        Items = new ObservableCollection<GeneratedEditableOrder>(CreateInitialOrders());
        for (int index = 0; index < Items.Count; index++)
        {
            _byId.Add(Items[index].OrderId, Items[index]);
        }

        ItemsView = GeneratedEditableOrderSchema.CreateCollectionView(Items);
        EditController = GeneratedEditableOrderSchema.CreateEditController(key => _byId[key]);
        ClipboardController = GeneratedEditableOrderSchema.CreateClipboardController(EditController);
        FillController = GeneratedEditableOrderSchema.CreateFillController(EditController);
        ClipboardImportModel = GeneratedEditableOrderSchema.CreateClipboardImportModel(
            EditController,
            ReportGridTransfer,
            CultureInfo.InvariantCulture,
            new DataGridGeneratedTransferLimits(maximumCells: 256, maximumCharacters: 16 * 1024));
        FillModel = GeneratedEditableOrderSchema.CreateFillModel(
            EditController,
            ReportGridTransfer,
            maximumCells: 256,
            useSeries: true);
        EditController.Changed += OnEditControllerChanged;

        ApplyValidEditCommand = ReactiveCommand.Create(ApplyValidEdit);
        ApplyInvalidEditCommand = ReactiveCommand.Create(ApplyInvalidEdit);
        ValidateAsyncCommand = ReactiveCommand.CreateFromTask(ValidateAsync);
        PasteCommand = ReactiveCommand.Create(Paste);
        FillSeriesCommand = ReactiveCommand.Create(FillSeries);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        ExportCommand = ReactiveCommand.Create(Export);
    }

    public ObservableCollection<GeneratedEditableOrder> Items { get; }

    public DataGridCollectionView ItemsView { get; }

    public DataGridGeneratedEditController<GeneratedEditableOrder, int> EditController { get; }

    public DataGridGeneratedClipboardController<GeneratedEditableOrder, int> ClipboardController { get; }

    public DataGridGeneratedFillController<GeneratedEditableOrder, int> FillController { get; }

    public DataGridGeneratedClipboardImportModel<GeneratedEditableOrder, int> ClipboardImportModel { get; }

    public DataGridGeneratedFillModel<GeneratedEditableOrder, int> FillModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyValidEditCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyInvalidEditCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ValidateAsyncCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> PasteCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> FillSeriesCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> UndoCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RedoCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ExportCommand { get; }

    public bool IsDisposed => _disposed;

    private void ApplyValidEdit()
    {
        DataGridGeneratedEditResult product = EditController.TrySetText(
            Items[0], "product", "  catalyst  ".AsSpan(), CultureInfo.InvariantCulture);
        DataGridGeneratedEditResult price = EditController.TrySetText(
            Items[0], "unit-price", "123.456".AsSpan(), CultureInfo.InvariantCulture);
        RefreshRows();
        Status = $"Typed parser/coercion: product={product.Status}, price={price.Status}, stored={Items[0].UnitPrice:0.00}.";
    }

    private void ApplyInvalidEdit()
    {
        DataGridGeneratedEditResult range = EditController.TrySetText(
            Items[1], "quantity", "0".AsSpan(), CultureInfo.InvariantCulture);
        DataGridGeneratedEditResult locked = EditController.TrySetText(
            Items[^1], "unit-price", "99".AsSpan(), CultureInfo.InvariantCulture);
        LastErrorCount = (range.IsApplied ? 0 : 1) + (locked.IsApplied ? 0 : 1);
        Status = $"Validation={range.Status}: {range.Error} Locked-row policy={locked.Status}.";
    }

    private async Task ValidateAsync()
    {
        DataGridGeneratedEditResult rejected = await EditController.TrySetValueAsync(
            Items[0], "unit-price", 6_000m).ConfigureAwait(true);
        DataGridGeneratedEditResult accepted = await EditController.TrySetValueAsync(
            Items[0], "unit-price", 148.678m).ConfigureAwait(true);
        RefreshRows();
        LastErrorCount = rejected.IsApplied ? 0 : 1;
        Status = $"Async approval={rejected.Status}: {rejected.Error} Latest valid edit={accepted.Status}, stored={Items[0].UnitPrice:0.00}.";
    }

    private void Paste()
    {
        DataGridGeneratedTransferResult<int> result = ClipboardController.PasteDelimited(
            Items,
            s_transferColumns,
            PasteText.AsSpan(),
            '\t',
            CultureInfo.InvariantCulture,
            new DataGridGeneratedTransferLimits(maximumCells: 256, maximumCharacters: 16 * 1024));
        PublishTransfer("Typed paste", result);
    }

    private void FillSeries()
    {
        DataGridGeneratedTransferResult<int> result = FillController.Fill(
            Items,
            "quantity",
            startIndex: 0,
            static index => 10 + index * 10,
            maximumCells: 256);
        PublishTransfer("Typed numeric series", result);
    }

    private void Undo()
    {
        bool changed = EditController.Undo();
        RefreshRows();
        Status = changed ? "Undo restored the latest keyed edit batch." : "Nothing to undo.";
    }

    private void Redo()
    {
        bool changed = EditController.Redo();
        RefreshRows();
        Status = changed ? "Redo replayed the latest keyed edit batch." : "Nothing to redo.";
    }

    private void Export()
    {
        DataGridGeneratedExportFormat format = s_exportFormats[_exportFormatIndex++ % s_exportFormats.Length];
        string exported = ClipboardController.Export(
            Items,
            s_transferColumns,
            format,
            includeHeaders: true,
            formatProvider: CultureInfo.InvariantCulture,
            limits: new DataGridGeneratedTransferLimits(maximumCells: 256, maximumCharacters: 32 * 1024));
        ExportPreview = exported.Length <= 240 ? exported : exported[..240] + "…";
        Status = $"Generated {format} export contains {exported.Length} characters.";
    }

    private void ReportGridTransfer(DataGridGeneratedTransferResult<int> result) =>
        PublishTransfer("DataGrid adapter", result);

    private void PublishTransfer(string operation, DataGridGeneratedTransferResult<int> result)
    {
        LastAppliedCells = result.AppliedCells;
        LastErrorCount = result.Errors.Count;
        RefreshRows();
        Status = $"{operation}: applied={result.AppliedCells}, errors={result.Errors.Count}, truncated={result.Truncated}.";
    }

    private void OnEditControllerChanged(object? sender, DataGridGeneratedEditChangedEventArgs args)
    {
        CanUndo = EditController.CanUndo;
        CanRedo = EditController.CanRedo;
    }

    private void RefreshRows() => ItemsView.Refresh();

    private static IReadOnlyList<GeneratedEditableOrder> CreateInitialOrders() =>
    [
        new() { OrderId = 3101, Product = "ALPHA", Quantity = 10, UnitPrice = 25m, Discount = 0.05m, Due = new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero) },
        new() { OrderId = 3102, Product = "BETA", Quantity = 20, UnitPrice = 30m, Discount = 0.10m, Due = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero) },
        new() { OrderId = 3103, Product = "GAMMA", Quantity = 5, UnitPrice = 75m, Discount = 0m, Due = new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero) },
        new() { OrderId = 3104, Product = "DELTA", Quantity = 8, UnitPrice = 42.5m, Discount = 0.15m, Due = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero) },
        new() { OrderId = 3105, Product = "EPSILON", Quantity = 12, UnitPrice = 18m, Discount = 0.05m, Due = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero) },
        new() { OrderId = 3106, Product = "LOCKED", Quantity = 4, UnitPrice = 250m, Discount = 0m, Due = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero), Locked = true }
    ];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        EditController.Changed -= OnEditControllerChanged;
        EditController.Dispose();
        _disposed = true;
    }
}
