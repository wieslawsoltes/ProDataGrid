# Layout, templates, and rendering

Generated layout and rendering metadata covers column bands, chooser state, frozen placement, runtime indexed families, recycling templates, row details, direct/drawn cells, and custom drawing caches. Visual resources and application-specific control composition remain user-owned.

## Layout item templates

Generated built-in or custom layouts can use `DataGridItemContainer` presentation instead of row/cell visuals. Set `LayoutPresentation = DataGridGeneratedLayoutPresentation.Items`, provide positive item estimates, and select exactly one item-template source:

```csharp
[GenerateDataGridView(
    typeof(Product),
    ViewName = "ProductCardsView",
    Layout = DataGridGeneratedLayout.Wrap,
    LayoutPresentation = DataGridGeneratedLayoutPresentation.Items,
    LayoutItemWidthEstimate = 240,
    LayoutItemHeightEstimate = 92,
    LayoutHorizontalSpacing = 8,
    LayoutVerticalSpacing = 8,
    LayoutItemTemplateFactoryMethod = nameof(Product.CreateCard))]
```

`LayoutItemTemplateKey`, `LayoutItemTemplateImplementationType`, and `LayoutItemTemplateFactoryMethod` follow the same resource/implementation/typed-factory choices as other generated templates. The factory contract is `(TItem, Control?) -> Control`; generated code emits a recycling template and passes the existing root back on reuse. The generator reports `PDGSG139` for a missing or conflicting template source, a non-positive/non-finite estimate, or item presentation without a layout model.

Columns are still generated as semantic metadata but their headers/cells are not realized in item mode. See [Generated layouts](layouts.md) and [Item-template layout presentation](../item-layout-presentation.md).

## Column layout metadata

Use stable keys for all persisted and interactive layout operations:

```csharp
[DataGridColumn(
    Header = "Symbol",
    ColumnKey = "symbol",
    DisplayIndex = 0,
    FrozenPlacement = DataGridFrozenPlacement.Left,
    Width = "2*",
    MinWidth = 100,
    CanUserHide = false,
    CanUserResize = true,
    CanUserReorder = true,
    WidthSharingGroup = "identity")]
public string Symbol { get; set; } = string.Empty;
```

`DataGridGeneratedColumnLayoutController` coordinates visibility, ordering, widths, frozen placement, reset, chooser entries, and header commands. It observes live definition changes and never uses property paths for identity.

## Column bands

Attach one or more band paths to a field:

```csharp
[DataGridColumn(Header = "Bid", ColumnKey = "bid")]
[DataGridBand("Market/Prices", Order = 0)]
public decimal Bid { get; set; }

[DataGridColumn(Header = "Ask", ColumnKey = "ask")]
[DataGridBand("Market/Prices", Order = 1)]
public decimal Ask { get; set; }
```

Generated band paths form a deterministic nested tree. Duplicate or conflicting placements fail at generation time. The tree can drive column-band presentation, chooser grouping, and layout reset while column definitions retain their stable keys.

## Runtime indexed column families

Use `[GenerateDataGridIndexedColumns]` when a bounded family is stored by slot instead of CLR property:

```csharp
[GenerateDataGridIndexedColumns(
    Name = "Cells",
    GetterMethod = nameof(GetCell),
    SetterMethod = nameof(SetCell),
    NotificationNameMethod = nameof(GetCellPropertyName))]
public sealed class SpreadsheetRow : ReactiveObject
{
    private readonly object?[] _cells = new object?[32];

    public object? GetCell(int index) => _cells[index];

    public void SetCell(int index, object? value)
    {
        _cells[index] = value;
        this.RaisePropertyChanged(GetCellPropertyName(index));
    }

    public static string GetCellPropertyName(int index) =>
        SpreadsheetNames.FromIndex(index);
}
```

The generated `SpreadsheetRowCells.CreateColumn<TValue>` accepts typed options:

```csharp
DataGridGeneratedIndexedColumnOptions<double> amount = new()
{
    Header = "B",
    ColumnKey = "B",
    PropertyName = "B",
    Kind = DataGridGeneratedIndexedColumnKind.Numeric,
    FormatString = "N2",
    Width = new DataGridLength(105)
};

columns.Add(SpreadsheetRowCells.CreateColumn<double>(1, in amount));
```

The factory supports text, numeric, checkbox, date/time, progress, slider, hyperlink, image, hierarchical, custom-drawing, and formula definitions. Non-formula definitions receive cached compiled binding metadata and typed accessors.

Formula slots bypass the row getter:

```csharp
DataGridGeneratedIndexedColumnOptions<double> total = new()
{
    Header = "E",
    ColumnKey = "E",
    PropertyName = "E",
    Kind = DataGridGeneratedIndexedColumnKind.Formula,
    Formula = "=([@B]*[@C])*(1-[@D])",
    FormulaName = "E",
    IsReadOnly = true
};

columns.Add(SpreadsheetRowCells.CreateColumn<double>(4, in total));
```

The getter, optional setter, notification method, and generic value types are validated at compile time.

## Recycling cell templates

Template columns may name static factories for display, edit, and new-row cells:

```csharp
[DataGridColumn(
    DataGridColumnKind.Template,
    TemplateFactoryMethod = nameof(CreateDisplayCell),
    EditingTemplateFactoryMethod = nameof(CreateEditingCell),
    NewRowTemplateFactoryMethod = nameof(CreateNewRowCell),
    ReuseCellContent = true)]
public string Status { get; set; } = string.Empty;

public static Control CreateDisplayCell(StatusRow item, Control? existing)
{
    TextBlock text = existing as TextBlock ?? new TextBlock();
    text.Text = item.Status;
    return text;
}
```

Factories use the exact `(TItem, Control?) -> Control` contract. The existing control is supplied during recycling. No runtime XAML loading, view-location convention, or reflection is involved.

Resource-backed `TemplateKey` and `EditingTemplateKey` remain available when the visual tree belongs in an Avalonia resource dictionary.

## Row details and nested grids

Generated views support four mutually exclusive detail sources:

1. A dynamic resource key.
2. A validated `IDataTemplate` implementation type.
3. A static `(TItem, Control?) -> Control` factory.
4. A typed nested-grid recipe.

Nested-grid example:

```csharp
[GenerateDataGridView(
    typeof(Book),
    Framework = DataGridViewFramework.ReactiveUI,
    RowDetailsVisibilityMode =
        DataGridRowDetailsVisibilityMode.VisibleWhenSelected,
    RowDetailsNestedItemType = typeof(Author),
    RowDetailsNestedItemsMember = nameof(Book.Authors),
    RowDetailsNestedProviderName = "AuthorSchema",
    RowDetailsSummaryMember = nameof(Book.Summary),
    RowDetailsAutomationId = "book-authors-grid")]
public sealed partial class BooksViewModel : ReactiveObject { }
```

The nested items member must be `IEnumerable<TNested>`. The presenter creates nested definitions/fast-path options once and updates only the summary and items source when recycled. It references the nested generated provider directly.

`AreRowDetailsFrozen` and `RowDetailsVisibilityMode` configure the owning grid. `PDGSG123` reports conflicting or incompatible detail sources.

## Built-in retained and drawn display modes

`DisplayMode` selects the runtime retained/drawn lane:

```csharp
[DataGridColumn(
    DataGridColumnKind.Numeric,
    DisplayMode = DataGridColumnDisplayMode.Drawn)]
public decimal Amount { get; set; }
```

The option is common metadata. Runtime columns that cannot use the requested drawn path retain their supported fallback.

Text columns can opt into direct cells and direct text content:

```csharp
[DataGridColumn(
    DataGridColumnKind.Text,
    UseDirectTextCell = true,
    UseDirectTextContent = true,
    TrackDirectTextValueChanges = false)]
public string ImmutableSymbol { get; init; } = string.Empty;
```

Hierarchical columns support `UseDirectCell`, `UseDirectTextContent`, `UseOptimizedPresenter`, and `TrackDirectTextValueChanges`. Custom-drawing columns support `UseDirectValueAccessor` and `TrackDirectValueChanges`.

Disable tracking only for immutable values or sources that replace/recycle rows when the value changes. Kind-incompatible options report `PDGSG009`.

## Custom drawing factories

```csharp
[GenerateDataGridCellDrawCache(
    InitialCapacity = 4,
    MaximumCapacity = 16)]
public sealed partial class Quote
{
    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        DrawOperationFactoryMethod = nameof(CreatePriceFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend =
            DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode =
            DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true,
        UseDirectValueAccessor = true,
        TrackDirectValueChanges = false)]
    public decimal Price { get; set; }

    public static IDataGridCellDrawOperationFactory CreatePriceFactory() =>
        new PriceDrawOperationFactory
        {
            UseItemCacheContract = true,
            ItemCacheSlot = PriceCellDrawCacheSlot
        };
}
```

Use `DrawOperationFactoryType` for a stateless accessible parameterless factory. Use `DrawOperationFactoryMethod` for configured instances. Both must produce `IDataGridCellDrawOperationFactory`; invalid combinations report `PDGSG122`.

Assigning the generated factory preserves automatic subscription to `IDataGridCellDrawOperationInvalidationSource`.

## Generated item draw cache

`[GenerateDataGridCellDrawCache]` is an independent incremental pipeline for partial row classes. It emits:

- `IDataGridCellDrawOperationItemCache` implementation;
- deterministic `{Property}CellDrawCacheSlot` constants;
- array-backed O(1) cache storage;
- whole-cache and per-slot clear methods.

`InitialCapacity` avoids first-use growth. `MaximumCapacity` bounds retained entries and rejects invalid slots without allocation. Set `GenerateSlotConstants = false` when an external component owns slot assignment.

## Dynamic command/content bindings

Button, toggle-button, and toggle-switch definitions can bind command, parameter, and state-specific text through generated binding definitions:

```csharp
[DataGridColumn(
    DataGridColumnKind.ToggleButton,
    CheckedContentMember = nameof(PinnedLabel),
    UncheckedContentMember = nameof(UnpinnedLabel),
    CommandMember = nameof(PinChangedCommand),
    CommandParameterMember = nameof(Id))]
public bool IsPinned { get; set; }
```

This path avoids per-cell binding discovery and preserves the existing static content/command APIs for application-wide commands.

## Performance guidance

- Prefer typed accessors and generated fast-path options before selecting a specialized realization path.
- Use drawn/direct paths for measured high-density scenarios, not as a universal replacement for templated controls.
- Bound shared text-layout and per-item caches.
- Use recycling templates for interactive content.
- Avoid `RowDetailsVisibilityMode.Visible` with `HighFrequencyStreaming`; the generator reports `PDGSG128` because details would be realized for every row.

## Related articles

- [Optimized retained and drawn cells](../optimized-cell-paths.md)
- [Custom drawing columns](../custom-drawing-columns.md)
- [Column banding](../column-banding.md)
- [Column chooser](../column-chooser.md)
- [Fast-path overview](../column-definitions-fast-path-overview.md)
