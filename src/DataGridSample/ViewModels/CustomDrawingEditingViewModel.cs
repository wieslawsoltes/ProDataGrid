using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DataGridSample.CustomDrawing;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(CustomDrawingEditingRow), ProviderName = "CustomDrawingEditingSchema")]
public sealed partial class CustomDrawingEditingViewModel : ReactiveObject
{
    public CustomDrawingEditingViewModel()
    {
        Rows = new ObservableCollection<CustomDrawingEditingRow>();
        AddRowCommand = ReactiveCommand.Create(AddRow);
        ResetRowsCommand = ReactiveCommand.Create(ResetRows);
        ResetRows();
    }

    public ObservableCollection<CustomDrawingEditingRow> Rows { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddRowCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetRowsCommand { get; }

    private void AddRow()
    {
        int nextId = Rows.Count + 1;
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = nextId,
            Title = $"Task {nextId}",
            Notes = "New editable row created from command.",
            Category = "Draft"
        });
    }

    private void ResetRows()
    {
        Rows.Clear();
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 1,
            Title = "Release validation",
            Notes = "Verify custom-drawing text editing and commit behavior.",
            Category = "QA"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 2,
            Title = "Performance notes",
            Notes = "Track scroll smoothness while editing frequently updated cells.",
            Category = "Perf"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 3,
            Title = "Docs update",
            Notes = "Capture usage guidance for editable custom drawing columns.",
            Category = "Docs"
        });
        Rows.Add(new CustomDrawingEditingRow
        {
            Id = 4,
            Title = "Regression sweep",
            Notes = "Switch tabs and re-select cells to validate consistent foreground updates.",
            Category = "Stability"
        });
    }
}

[GenerateDataGridCellDrawCache(InitialCapacity = 4, MaximumCapacity = 4)]
public sealed partial class CustomDrawingEditingRow : ReactiveObject
{
    private int _id;
    private string _title = string.Empty;
    private string _notes = string.Empty;
    private string _category = string.Empty;

    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        Header = "ID",
        Order = 0,
        Width = "90",
        IsReadOnly = true,
        DrawOperationFactoryMethod = nameof(CreateIdFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true)]
    public int Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        Header = "Title",
        Order = 1,
        Width = "220",
        IsReadOnly = false,
        DrawOperationFactoryMethod = nameof(CreateTitleFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true)]
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        Header = "Notes",
        Order = 2,
        Width = "*",
        IsReadOnly = false,
        DrawOperationFactoryMethod = nameof(CreateNotesFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true)]
    public string Notes
    {
        get => _notes;
        set => this.RaiseAndSetIfChanged(ref _notes, value);
    }

    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        Header = "Category",
        Order = 3,
        Width = "140",
        IsReadOnly = false,
        DrawOperationFactoryMethod = nameof(CreateCategoryFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true)]
    public string Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    private static IDataGridCellDrawOperationFactory CreateFactory(int cacheSlot) =>
        new SkiaTextCellDrawOperationFactory
        {
            Padding = new Thickness(4, 2, 4, 2),
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            UseItemCacheContract = true,
            ItemCacheSlot = cacheSlot
        };

    public static IDataGridCellDrawOperationFactory CreateIdFactory() => CreateFactory(IdCellDrawCacheSlot);

    public static IDataGridCellDrawOperationFactory CreateTitleFactory() => CreateFactory(TitleCellDrawCacheSlot);

    public static IDataGridCellDrawOperationFactory CreateNotesFactory() => CreateFactory(NotesCellDrawCacheSlot);

    public static IDataGridCellDrawOperationFactory CreateCategoryFactory() => CreateFactory(CategoryCellDrawCacheSlot);
}
