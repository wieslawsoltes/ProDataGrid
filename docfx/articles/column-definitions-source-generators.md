# Column Definitions: Source Generators

`ProDataGrid.SourceGenerators` generates column definitions, typed value accessors, compiled Avalonia binding metadata, data-operation delegates, and view-model properties at compile time. The generated path does not inspect item properties with reflection and is suitable for trimming and Native AOT.

## Install

Reference the grid normally and add the generator as a private analyzer dependency:

```xml
<ItemGroup>
  <PackageReference Include="ProDataGrid" />
  <PackageReference Include="ProDataGrid.SourceGenerators"
                    PrivateAssets="all" />
</ItemGroup>
```

The analyzer injects its configuration attributes into the consuming compilation under `ProDataGrid.SourceGeneration`. It does not add a runtime attribute assembly.

## Generate a schema from a model

Annotate the item type and override individual columns where required:

```csharp
using ProDataGrid.SourceGeneration;

[GenerateDataGridColumns(
    ProviderName = "TradeGridSchema",
    SchemaId = "trading/trade/v1",
    Strict = true)]
public sealed class Trade
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric,
        Header = "ID", ColumnKey = "trade-id", Order = 0, IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text,
        Header = "Symbol", ColumnKey = "trade-symbol", Order = 1, Width = "2*")]
    public string Symbol { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric,
        Header = "Price", ColumnKey = "trade-price", Order = 2, FormatString = "N2")]
    public decimal Price { get; init; }
}
```

`PublicProperties` discovery includes eligible public properties. Use `Discovery = DataGridColumnDiscovery.AttributedOnly` when the model must opt in property by property, and `[DataGridIgnoreColumn]` to exclude a property.

The generated provider implements `IDataGridGeneratedSchema<TItem>`, which is composed from five focused contracts:

- `IDataGridColumnDefinitionProvider<TItem>` creates a new mutable definition list for each grid.
- `IDataGridSortingCompiler<TItem>` compiles sorting descriptors to an item comparer.
- `IDataGridFilteringCompiler<TItem>` compiles filtering descriptors to an item predicate.
- `IDataGridSearchingCompiler<TItem>` compiles search descriptors to an item predicate.
- `IDataGridFastPathOptionsProvider` creates strict accessor-only options.

All operations use generated `DataGridColumnValueAccessor<TItem,TValue>` instances. Descriptor column ids and property paths are keys only; they are never reflected over.

## Canonical manifest, typed fields, and item identity

Every provider also implements `IDataGridGeneratedSchemaManifestProvider`. Its immutable manifest contains a format version, stable schema ID, deterministic shape hash, item/key types, stable field ordinals, column keys, and the compiled accessors used by the provider. Set `SchemaId` explicitly for schemas whose state is persisted or shared across assemblies; otherwise the generator uses the item metadata name plus `/v1`.

Each column is emitted as a strongly typed static field descriptor. Common operations can therefore be built without property-path strings:

```csharp
SortingDescriptor[] sorting =
{
    TradeGridSchema.Price.Descending(),
    TradeGridSchema.Symbol.Ascending()
};

FilteringDescriptor[] filtering =
{
    TradeGridSchema.Price.GreaterThanOrEqual(100m),
    TradeGridSchema.Symbol.Contains("MS", StringComparison.OrdinalIgnoreCase)
};

SearchDescriptor symbolSearch = TradeGridSchema.Symbol.Search(
    "A*",
    SearchMatchMode.Wildcard);
```

`[DataGridKey]` accepts one accessible, non-nullable field or property. The provider implements `IDataGridItemKey<TItem,TKey>`, exposes the default typed key comparer, and creates `DataGridGeneratedItemIndex<TItem,TKey>` instances. The index handles reset, insert, remove, move, and replace changes while preserving allocation-free typed key lookup on reads:

```csharp
DataGridGeneratedItemIndex<Trade, int> index =
    TradeGridSchema.CreateItemIndex(items);

if (index.TryGetIndex(tradeId, out int rowIndex))
{
    // Coordinate selection, state, drag/drop, or chart focus by stable identity.
}
```

The index rejects duplicate keys and captures the key at insertion time, so an accidental key mutation cannot corrupt dictionary lookup. Replacing the item explicitly refreshes its captured key.

The provider also creates a `DataGridGeneratedOperationController<TItem>`. It owns the three operation models, column definitions, fast-path options, compiled delegates, and model subscriptions behind one disposable lifetime. Select `ExternalPipeline` for DynamicData, async-stream, or server adapters; this disables collection-view sort/filter ownership:

```csharp
using DataGridGeneratedOperationController<Trade> controller =
    TradeGridSchema.CreateController(DataGridOperationExecution.ExternalPipeline);

controller.OperationsChanged += (_, args) =>
{
    if ((args.Change & DataGridGeneratedOperationChange.Filtering) != 0)
    {
        filterSubject.OnNext(controller.FilterPredicate);
    }
};

controller.SortingModel.SetOrUpdate(TradeGridSchema.Price.Descending());
controller.FilteringModel.SetOrUpdate(TradeGridSchema.Symbol.Contains("AVLN"));
```

This controller does not depend on ReactiveUI or DynamicData; it deliberately adapts to the existing ProDataGrid operation models. Optional generated reactive adapters consume its single change stream without duplicating descriptor compilation or model event wiring.

For a named, ViewModel-owned controller, use `GenerateDataGridController`. Multiple independently configured grids may be generated on the same partial ViewModel:

```csharp
[GenerateDataGridController(
    typeof(Trade),
    "Trades",
    SourceMember = nameof(_source),
    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
    OperationExecution = DataGridOperationExecution.ExternalPipeline,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Operations |
               DataGridGeneratedFeatures.Selection,
    Streaming = true)]
public sealed partial class TradesViewModel : ReactiveObject, IDisposable
{
    private readonly SourceCache<Trade, int> _source = new(static item => item.Id);

    public TradesViewModel()
    {
        InitializeTrades(CreateTradesController());
        Items = ConnectTradesPipeline(
            ReactiveUI.Primitives.Reactive.Concurrency.AvaloniaScheduler.Instance);
    }

    public ReadOnlyObservableCollection<Trade> Items { get; }

    public void Dispose()
    {
        DisposeTrades();
        _source.Dispose();
    }
}
```

The generated group contains `Trades`, `InitializeTrades`, `CreateTradesController`, and `DisposeTrades`. A DynamicData source additionally gets a one-owner `ConnectTradesPipeline`/`DisconnectTradesPipeline` pair plus `TradesErrors` and `TradesCompletion`. The generated pipeline applies compiled predicates/comparers upstream, uses `UseReplaceForUpdates`, and performs the optional scheduler hop at the final binding boundary.

Local controllers can apply a reusable sort/filter/search preset as one revision while retaining compile-time field types:

```csharp
DataGridGeneratedOperationPreset riskPreset = new(
    "AVLN high value",
    sorting: [TradeGridSchema.Price.Descending()],
    filtering:
    [
        TradeGridSchema.Symbol.EqualTo("AVLN"),
        TradeGridSchema.Price.GreaterThanOrEqual(100m)
    ]);

Trades.ApplyPreset(riskPreset);
```

`DataGridSample.Pages.GeneratedOperationsControllerPage` combines this named-controller pattern with a generated ReactiveUI grid and search view. Its passive compiled-binding shell supplies only application-specific commands and status presentation.

`ImplementationType` accepts an `IDataGridGeneratedControllerFactory<TItem>`. `ConfigureMethod` names a static method on the partial ViewModel with a `ref DataGridGeneratedControllerOptions<TItem>` parameter. Both shapes are validated before emission.

## Augment a view model

The view model must be partial. It may inherit any base type, including `ReactiveObject`:

```csharp
using ProDataGrid.SourceGeneration;
using ReactiveUI;

[GenerateDataGridViewModel(typeof(Trade),
    ProviderName = "TradeGridSchema",
    Streaming = true)]
public sealed partial class TradesViewModel : ReactiveObject
{
    public IReadOnlyList<Trade> Items { get; } = LoadTrades();
}
```

The generator adds:

```csharp
public IDataGridGeneratedSchema<Trade> DataGridSchema { get; }
public DataGridColumnDefinitionList ColumnDefinitions { get; }
public DataGridFastPathOptions FastPathOptions { get; }
```

All three property names are configurable. Existing members are preserved and produce `PDGSG006` instead of being overwritten.

Bind the results with compiled XAML bindings:

```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoGenerateColumns="False"
          ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          FastPathOptions="{Binding FastPathOptions}" />
```

`FastPathOptions` is an Avalonia direct property, so no view code is needed to apply generated options.

## Assembly and namespace coverage

Generation can be applied without modifying model source:

```csharp
[assembly: GenerateDataGridColumns(
    typeof(ExternalModels.Trade),
    ProviderName = "ExternalTradeSchema",
    ProviderNamespace = "MyApp.Generated")]

[assembly: GenerateDataGridViewModel(
    typeof(TradesViewModel),
    typeof(ExternalModels.Trade),
    ProviderName = "ExternalTradeSchema")]
```

Use `[assembly: GenerateDataGridColumnsForNamespace("MyApp.Models")]` to cover every eligible model in a namespace. `IncludeNestedNamespaces` controls recursive coverage. `[assembly: GenerateDataGridViewModelsForNamespace("MyApp.ViewModels")]` augments partial view models and infers their item type from the configured `ItemsPropertyName`.

Assembly and namespace requests are merged deterministically with type and property configuration. Provider-name collisions receive stable suffixes.

Add `[assembly: GenerateDataGridRegistry]` when another assembly needs reflection-free schema discovery. The generated `ProDataGrid.Generated.GeneratedProDataGridRegistration` exposes all manifest providers and `TryGetSchema` overloads for item `Type` and stable schema ID. `RegistryNamespace` and `RegistryName` are configurable. When `Microsoft.Extensions.DependencyInjection` is referenced, the registry also emits `AddGeneratedProDataGrids(IServiceCollection)`; Microsoft DI remains optional.

Existing XAML views can participate in the same reflection-free registry:

```csharp
[assembly: DataGridViewRegistration(typeof(TradesViewModel), typeof(TradesView))]

if (GeneratedProDataGridRegistration.TryCreateView(viewModel, out Control? view))
{
    return view;
}
```

The generator validates that each registered view derives from `Control`, has an accessible parameterless constructor, and that each ViewModel has at most one mapping. The generated type switch constructs the view and assigns its `DataContext`; it does not use naming conventions, `Type.GetType`, or `Activator.CreateInstance`.

## Runtime indexed columns and formulas

Use `GenerateDataGridIndexedColumns` when the row stores a bounded runtime family in slots rather than CLR properties. The generator validates the getter, optional setter, and notification-name method once and emits direct generic factories without reflection, expression compilation, or per-cell property discovery:

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

    public static string GetCellPropertyName(int index) => GetSpreadsheetName(index);
}
```

The emitted `SpreadsheetRowCells.CreateColumn<TValue>` creates text, numeric, checkbox, date/time, progress, slider, hyperlink, image, hierarchical, custom-drawing, or formula definitions from the same indexed family. Each non-formula definition receives a cached `DataGridBindingDefinition` and typed value accessor:

```csharp
var amount = new DataGridGeneratedIndexedColumnOptions<double>
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

Formula slots bypass the row getter entirely. They produce `DataGridFormulaColumnDefinition` instances with a stable formula name, explicit value type, optional per-cell overrides, sizing, and the same final `Configure` hook:

```csharp
var total = new DataGridGeneratedIndexedColumnOptions<double>
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

A generated view can bind an application-owned formula model directly. `FormulaModelPropertyName` is compile-time validated as `IDataGridFormulaModel`; an incompatible member reports `PDGSG130` instead of falling back to a runtime binding:

```csharp
[GenerateDataGridView(
    typeof(SpreadsheetRow),
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    FormulaModelPropertyName = nameof(FormulaModel))]
public sealed partial class SpreadsheetViewModel : ReactiveObject
{
    public IDataGridFormulaModel FormulaModel { get; } = new DataGridFormulaModel();
}
```

`DataGridSample.Pages.GeneratedIndexedSpreadsheetPage` demonstrates a replaceable 7–12 column family, typed slot notifications, strict fast-path options, structured/chained formulas, a row-local cell formula, and generated ReactiveUI view composition.

## Generated pivot and chart projections

Assign analytics roles to the same attributed properties that define the grid schema. Pivot axes and values, chart categories and values, outline fields, and formula dependencies then share the canonical column key and generated getter:

```csharp
[GenerateDataGridColumns(
    ProviderName = "SalesSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    Strict = true)]
public sealed class Sale
{
    [DataGridColumn(Header = "Period", ColumnKey = "period")]
    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotColumn, Order = 0)]
    [DataGridChartField(DataGridGeneratedAnalyticsRole.ChartCategory, Order = 0)]
    public string Period { get; init; } = string.Empty;

    [DataGridColumn(Header = "Region", ColumnKey = "region")]
    [DataGridPivotAxis(DataGridGeneratedAnalyticsRole.PivotRow, Order = 0)]
    public string Region { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Revenue", ColumnKey = "revenue")]
    [DataGridPivotValue(PivotAggregateType.Sum, Order = 0, Format = "C0")]
    [DataGridChartField(
        DataGridGeneratedAnalyticsRole.ChartValue,
        Order = 0,
        Series = "Revenue",
        Format = "C0",
        Aggregate = DataGridAggregateType.Sum)]
    public double Revenue { get; init; }
}
```

The provider exposes `AnalyticsFields`, `CreatePivotAxisFields`, `CreatePivotValueFields`, and `CreatePivotTableModel`. Pivot fields are globally ordered by `Order` and then stable column key, independently of CLR property order. The model factory installs row, column, filter, and value fields while auto-refresh is suspended, assigns the supplied source, runs an optional application callback, and enables refresh only after configuration succeeds:

```csharp
PivotTableModel pivot = SalesSchema.CreatePivotTableModel(
    items,
    static model =>
    {
        model.Layout.RowLayout = PivotRowLayout.Tabular;
        model.Layout.ValuesPosition = PivotValuesPosition.Columns;
        model.Layout.ShowRowSubtotals = false;
    });
```

No pivot field receives a property path. Each selector calls the generated field accessor directly. The callback remains the customization boundary for layout, sorting, subtotal policy, filters, calculated fields, and application-specific pivot behavior.

Projects that reference `ProDataGrid.Charting` can create a direct chart projection from the same manifest:

```csharp
DataGridChartModel chart = DataGridGeneratedChartAdapter.CreateModel(
    items,
    SalesSchema.AnalyticsFields);
```

The adapter orders chart values deterministically and clears every runtime category/value/X/size property path. For numeric CLR properties, generated metadata implements `IDataGridGeneratedNumericAnalyticsField` and supplies a cached `Func<object, double?>`; the chart hot path therefore avoids the base interface's boxed value getter and runtime numeric conversion. Non-numeric or user-supplied fields retain the compatible conversion fallback. `IDataGridGeneratedAnalyticsField` and the original `DataGridGeneratedAnalyticsField<TItem, TValue>` constructor remain unchanged, preserving existing source and binary consumers.

`DataGridSample.Pages.GeneratedPivotChartPage` demonstrates an attributed-only strict schema, ordered row/column/filter/value fields, a configurable generated `PivotTableModel`, a chart driven by the pivot result, a direct chart driven by generated numeric selectors, current ReactiveUI source generation, passive compiled XAML, reactive source changes, metric and series switching, and Avalonia Headless screenshot coverage.

## Column coverage and customization

`DataGridColumnKind` covers every current definition builder: text, checkbox, hyperlink, image, numeric, progress bar, slider, date picker, time picker, masked text, autocomplete, toggle button, toggle switch, hierarchical, custom drawing, all three combo-box modes, template, button, and formula.

Common options include stable keys, ordering, widths, visibility, editability, sorting, hiding, resizing, reordering, themes, and filter resources. Kind-specific options include numeric limits, increments, masks, templates, formulas, combo-box members, content, and search behavior.

For code-level customization, specify a public static method on the item type:

```csharp
[DataGridColumn(DataGridColumnKind.Text,
    ConfigureMethod = nameof(ConfigureSymbol))]
public string Symbol { get; set; } = string.Empty;

public static void ConfigureSymbol(DataGridTextColumnDefinition column)
{
    column.Watermark = "Ticker";
}
```

`FactoryMethod` replaces construction of one definition. `GenerateDataGridColumns.ConfigureMethod` customizes the completed list.

For full ownership, set `ImplementationType` to a public parameterless implementation of `IDataGridGeneratedSchema<TItem>`. The generated provider becomes a stable facade that forwards every operation to user code.

### Typed editing, clipboard, fill, and undo

For keyed schemas, writable columns become `IDataGridGeneratedEditField<TItem>` instances. A writable property marked `IsReadOnly = true` is intentionally excluded. Parsers and formatters are culture-aware span/delegate calls; `Required`, string-length, minimum/maximum-length, and numeric `Range` annotations compile into direct validation. `ValidatorMethod`, `AsyncValidatorMethod`, `CoerceMethod`, and `CanEditMethod` add validated static hooks without reflection. Asynchronous validation is cancellable and revisioned per item key and column key.

The generated schema owns the standard factories:

```csharp
DataGridGeneratedEditController<Order, int> edits =
    OrderSchema.CreateEditController(ResolveOrder);

DataGridGeneratedClipboardImportModel<Order, int> clipboard =
    OrderSchema.CreateClipboardImportModel(
        edits,
        ReportTransfer,
        CultureInfo.InvariantCulture,
        new DataGridGeneratedTransferLimits(10_000, 1_000_000));

DataGridGeneratedFillModel<Order, int> fill =
    OrderSchema.CreateFillModel(edits, ReportTransfer, maximumCells: 10_000);
```

The clipboard adapter maps DataGrid columns by stable `ColumnKey`, never by property path. It supports rectangular tabular paste and one-value multi-cell paste, returns structured keyed parse/validation errors, enforces cell/payload limits, and records each operation as one undo batch. The fill adapter supports bounded cyclic copy and automatic numeric, date/time, and duration sequences through the same edit fields. The lower-level generated controller exports text, CSV, JSON, Markdown, HTML, XML, and YAML with typed formatters.

Bind the adapters directly from a generated Avalonia or ReactiveUI view:

```csharp
[GenerateDataGridView(
    typeof(Order),
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    ClipboardImportModelPropertyName = nameof(ClipboardImportModel),
    FillModelPropertyName = nameof(FillModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.CellOrRowHeader,
    EditTriggers = DataGridEditTriggers.CellDoubleClick |
                   DataGridEditTriggers.TextInput |
                   DataGridEditTriggers.F2,
    ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader)]
public sealed partial class OrdersViewModel : ReactiveObject
{
}
```

Both named members are compile-time validated against `IDataGridClipboardImportModel` and `IDataGridFillModel`. Generated views disable add/delete rows by default so strict generated grids do not silently enter the DataGrid's reflection-based new-item path. Applications that need spreadsheet-specific formulas or domain transactions can retain a custom adapter while reusing the generated fields and controller.

`DataGridSample.Pages.GeneratedEditingClipboardFillPage` demonstrates direct and DataGrid-driven edits, DataAnnotations plus custom synchronous/asynchronous validation, coercion, row eligibility, paste errors, numeric series, undo/redo, bounded multi-format export, compiled bindings, and a passive ReactiveUI shell.

Canonical field metadata can also declare export/null formatting, backend names, filter-editor profiles, header/description resource keys, accessibility text, and sensitive-data policy. For reflection-free strongly typed localization, use validated static provider methods:

```csharp
[DataGridColumn(
    Header = "Amount",
    Description = "Order amount",
    HeaderProviderMethod = nameof(GetAmountHeader),
    DescriptionProviderMethod = nameof(GetAmountDescription),
    BackendFieldName = "total_amount",
    FilterEditor = DataGridGeneratedFilterEditorKind.Range,
    AutomationId = "amount-cell")]
public decimal Amount { get; set; }

public static string GetAmountHeader(IFormatProvider provider) =>
    TradingResources.Amount;

public static string GetAmountDescription() =>
    TradingResources.AmountDescription;
```

The generated metadata exposes `ResolveHeader` and `ResolveDescription`; generated column creation calls the provider directly with `CurrentUICulture`.

Typed template columns may name static `(TItem, Control?) -> Control` factories for display, edit, and new-row cells. The generator validates every signature and creates recycling templates without runtime XAML loading or reflection.

Custom-drawing columns can create a validated factory by accessible parameterless type or static method and configure the hot-path options directly:

```csharp
[GenerateDataGridCellDrawCache(InitialCapacity = 4, MaximumCapacity = 16)]
public sealed partial class Quote
{
    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        DrawOperationFactoryMethod = nameof(CreatePriceFactory),
        DrawingMode = DataGridCustomDrawingMode.DrawOperation,
        RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
        TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
        SharedTextLayoutCacheCapacity = 1024,
        DrawOperationLayoutFastPath = true)]
    public decimal Price { get; set; }

    public static IDataGridCellDrawOperationFactory CreatePriceFactory() =>
        new PriceDrawOperationFactory
        {
            UseItemCacheContract = true,
            ItemCacheSlot = PriceCellDrawCacheSlot
        };
}
```

`DrawOperationFactoryType` is useful for a stateless factory with a public parameterless constructor. `DrawOperationFactoryMethod` supports configured instances and must be static, parameterless, accessible, and return `IDataGridCellDrawOperationFactory`. Assigning the factory through the generated definition preserves automatic `IDataGridCellDrawOperationInvalidationSource` subscription.

`GenerateDataGridCellDrawCache` is an independent incremental pipeline for partial row classes. It emits `IDataGridCellDrawOperationItemCache`, deterministic `{Property}CellDrawCacheSlot` constants for attributed custom-drawing columns, array-backed O(1) storage, and whole-cache/per-slot clear methods. `InitialCapacity` avoids first-use growth; `MaximumCapacity` bounds retained entries and rejects out-of-range slots without allocation. Set `GenerateSlotConstants = false` when slot ownership is entirely external.

### Typed grouping and rendered summaries

`DataGridGroup` emits ordered `DataGridGeneratedGroupField<TItem,TValue>` values. `CreateCollectionView` installs their direct typed group descriptions, avoiding `DataGridPathGroupDescription` and property-path lookup:

```csharp
[DataGridColumn(Header = "Region", ColumnKey = "region")]
[DataGridGroup(Order = 0)]
public string Region { get; init; } = string.Empty;

[DataGridColumn(DataGridColumnKind.Numeric, Header = "Revenue", ColumnKey = "revenue")]
[DataGridSummary(
    DataGridAggregateType.Sum,
    Scope = DataGridSummaryScope.Both,
    Format = "C2",
    Title = "Revenue: ")]
public decimal Revenue { get; init; }
```

Every `DataGridSummary` now produces both an incremental `IDataGridGeneratedSummary<TItem>` and a `DataGridSummaryDefinition` on the generated column definition. The latter creates an independent `DataGridSummaryDescription` for each materialized column, so a shared definition collection remains safe across grids. The DataGrid's summary service reads the already-generated column value accessor; built-in sum, average, count, distinct-count, minimum, maximum, first, and last calculations therefore avoid reflection.

Generated aggregates support `Add`, `Remove`, `Replace`, and `Reset`. Use the first three for streaming change sets and reserve `Reset` for a source reset or a custom calculation that cannot reverse removals. `Scope`, `Format`, `Title`, the column's `SummaryCellThemeKey`, and the generated view's summary placement are preserved:

```csharp
[GenerateDataGridView(
    typeof(Order),
    Framework = DataGridViewFramework.ReactiveUI,
    ShowTotalSummary = true,
    ShowGroupSummary = true,
    TotalSummaryPosition = DataGridSummaryRowPosition.Bottom,
    GroupSummaryPosition = DataGridGroupSummaryPosition.Footer)]
public sealed partial class OrdersViewModel : ReactiveObject
{
}
```

For a custom calculator, a column `ConfigureMethod` can assign `DataGridSummaryDefinition.Factory`. The factory is called directly once per materialized column and must return a fresh `DataGridSummaryDescription`; no activation or method reflection is used.

`DataGridSample.Pages.GeneratedGroupingSummariesPage` demonstrates two typed group levels, rendered group footers and total summaries, five incremental aggregates, and deterministic Add/Replace/Remove/Reset updates in a passive compiled-binding ReactiveUI page.

### Typed conditional formatting

`DataGridConditionalFormat` compiles comparisons and custom predicates against the same typed accessor used by generated columns, sorting, summaries, export, and editing. Each rule has a stable ID and column key plus priority, stop behavior, a resource theme key, and a cell or row target:

```csharp
[DataGridColumn(
    DataGridColumnKind.Numeric,
    Header = "Score",
    ColumnKey = "score")]
[DataGridConditionalFormat(
    DataGridCondition.GreaterThanOrEqual,
    RuleId = "score-high",
    Operand = "90",
    CellThemeKey = "ScoreHighCellTheme",
    Priority = 0)]
[DataGridConditionalFormat(
    DataGridCondition.Custom,
    RuleId = "score-below-target",
    PredicateMethod = nameof(IsBelowTarget),
    CellThemeKey = "ScoreBelowTargetCellTheme",
    Priority = 1)]
public double Score { get; set; }

[DataGridColumn(Header = "Status", ColumnKey = "status")]
[DataGridConditionalFormat(
    DataGridCondition.Equals,
    RuleId = "row-overdue",
    Operand = "Overdue",
    CellThemeKey = "RowAlertTheme",
    Target = ConditionalFormattingTarget.Row)]
public string Status { get; set; } = string.Empty;

public static bool IsBelowTarget(Order item, double score) =>
    score < item.Target;
```

Built-in rules convert `Operand` at compile time and emit a direct typed predicate. A custom predicate must be an accessible static `bool (TItem, TValue)` method. Generated predicates do not allocate while evaluating cells, and `ConditionalFormattingValueSource.Item` prevents the runtime adapter from resolving a property path.

The schema exposes `IReadOnlyList<IDataGridGeneratedConditionalRule> ConditionalRules` for inspection or custom composition and `CreateConditionalFormattingModel()` for the normal runtime model. The factory creates `ConditionalFormattingDescriptor` instances whose cached predicates call the generated getter directly; rule ordering, `StopIfTrue`, theme keys, stable column keys, and cell/row targets are preserved.

A generated view can bind an application-owned model without a runtime binding path:

```csharp
[GenerateDataGridView(
    typeof(Order),
    Framework = DataGridViewFramework.ReactiveUI,
    ConditionalFormattingModelPropertyName = nameof(ConditionalFormatting))]
public sealed partial class OrdersViewModel : ReactiveObject
{
    public IConditionalFormattingModel ConditionalFormatting { get; } =
        OrderGridSchema.CreateConditionalFormattingModel();
}
```

`ConditionalFormattingModelPropertyName` is supported by type-, assembly-, and namespace-level generated-view requests. The member is compile-time validated as `IConditionalFormattingModel`; a missing or incompatible member reports `PDGSG131` instead of silently introducing reflection binding. Applications can enable, disable, replace, or merge rules by mutating the bound model while retaining the immutable generated metadata.

`DataGridSample.Pages.GeneratedConditionalFormattingPage` demonstrates five cell rules and two row rules, typed custom predicates, priorities, runtime rule toggling, reactive value changes, passive compiled XAML, and Avalonia Headless verification of the applied cell and row themes.

### Row commands and dynamic button/toggle content

Button, toggle-button, and toggle-switch definitions can resolve commands, parameters, and state labels directly from each row. Name accessible row properties with the member options; the generator emits cached `ClrPropertyInfo` and `DataGridBindingDefinition` instances with typed static delegates:

```csharp
[GenerateDataGridColumns(Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class ServerRow : ReactiveObject
{
    [DataGridColumn(
        DataGridColumnKind.Button,
        ContentMember = nameof(RestartLabel),
        CommandMember = nameof(RestartCommand),
        CommandParameterMember = nameof(Id))]
    public string RestartAction => Id;

    [DataGridColumn(
        DataGridColumnKind.ToggleButton,
        CheckedContentMember = nameof(PinnedLabel),
        UncheckedContentMember = nameof(UnpinnedLabel),
        CommandMember = nameof(PinChangedCommand),
        CommandParameterMember = nameof(Id))]
    public bool IsPinned { get; set; }

    [DataGridColumn(
        DataGridColumnKind.ToggleSwitch,
        OnContentMember = nameof(OnlineLabel),
        OffContentMember = nameof(OfflineLabel),
        CommandMember = nameof(PresenceChangedCommand))]
    public bool IsOnline { get; set; }

    public string Id { get; init; } = string.Empty;
    public string RestartLabel => "Restart";
    public string PinnedLabel => "Pinned";
    public string UnpinnedLabel => "Not pinned";
    public string OnlineLabel => "Online";
    public string OfflineLabel => "Offline";
    public ICommand RestartCommand { get; init; } = default!;
    public ICommand PinChangedCommand { get; init; } = default!;
    public ICommand PresenceChangedCommand { get; init; } = default!;
}
```

Supported member options are `ContentMember`, `CheckedContentMember`, `UncheckedContentMember`, `OnContentMember`, `OffContentMember`, `CommandMember`, and `CommandParameterMember`. `CommandMember` must implement `ICommand`. A member-based label cannot be combined with its static counterpart. `Content` remains the legacy static on-label fallback for toggle switches.

`CommandParameterMember` is optional. When neither a compiled parameter binding nor a static parameter is configured, the generated cell passes the row item. A row-level `CommandMember` takes precedence over a definition-wide static command. `FactoryMethod` and `ConfigureMethod` remain available when an application needs a custom command bridge or control implementation.

The runtime definition surfaces are additive: `ContentBinding`, state-specific content bindings, `CommandBinding`, and `CommandParameterBinding` accept cached `DataGridBindingDefinition` values. Existing static content/command properties and XAML bindings continue to work.

## DynamicData, async streams, and snapshot reconciliation

The low-level schema compilers remain available for custom pipelines, but named controllers generate the standard `SourceList<T>` and `SourceCache<T,TKey>` pipeline, descriptor subjects, operation propagation, final scheduler boundary, errors, completion, and disposal automatically. `SourceCache` generation requires its key type to match `[DataGridKey]` or `KeyMember`, and generated external pipelines reject view-owned operation execution at compile time.

`DataGridSample.Pages.GeneratedDynamicDataSourceListPage` demonstrates the `SourceList<T>` shape end to end: batched edits enter one generated pipeline, sort/filter/search predicates are replaced upstream through the named controller, errors are observable, and the generated ReactiveUI view receives the bound read-only collection. The sample exposes deterministic published-item, batch, and error counters and verifies the complete lifetime in ViewModel and Avalonia Headless tests.

`DataGridSample.Pages.GeneratedDynamicDataSourceCachePage` demonstrates the keyed `SourceCache<T,TKey>` shape. The generated pipeline enables replace-aware sorting, reuses the `[DataGridKey]` accessor for an identity selection model, and binds that model through a generated ReactiveUI view. Replacing the selected trade with a new instance carrying the same key can move it under the active sort without moving selection to an adjacent row. The sample also exercises upstream filtering/search, deterministic cache batches, observable errors, and idempotent disposal. Runtime and Avalonia Headless regressions verify both the replacement instance and its preserved key.

For `IAsyncEnumerable<T>` or `ChannelReader<T>`, the named controller emits `Run{Name}StreamAsync`, `Stop{Name}Stream`, `{Name}StreamPump`, and `{Name}StreamMetrics`. Ingestion uses a bounded buffer with explicit `Wait`, `DropNewest`, `DropOldest`, or keyed `CoalesceByKey` overflow policy and one callback per drained batch. Disposal cancels active enumeration.

Keyed schemas also expose reusable streaming primitives directly:

```csharp
DataGridGeneratedSnapshotReconciler<Trade, int> snapshots =
    TradeGridSchema.CreateSnapshotReconciler();

DataGridGeneratedSnapshotMetrics result = snapshots.Reconcile(
    visibleTrades,
    serviceSnapshot,
    revision);
```

Snapshot reconciliation applies keyed add/remove/move/replace changes without clearing the target collection, rejects duplicate keys before mutation, and ignores stale revisions. `CreateStreamBuffer` and `CreateAsyncStreamPump` provide the lower-level allocation-conscious APIs for custom adapters.

### Generated remote queries

Use `DataGridGeneratedSourceKind.Remote` with `DataGridOperationExecution.Remote` when a named controller owns typed operation state but a service executes the query. The generated ViewModel members construct immutable, monotonically revisioned `DataGridRemoteQuery<TItem>` values from the controller's current sorting, filtering, and searching descriptors. The application supplies an `IDataGridQueryProvider<TItem,TKey>`; networking, persistence, authentication, retries, and backend-specific serialization stay behind that interface.

```csharp
[GenerateDataGridController(
    typeof(Order),
    "Orders",
    ProviderName = "OrderSchema",
    SourceMember = nameof(_provider),
    SourceKind = DataGridGeneratedSourceKind.Remote,
    OperationExecution = DataGridOperationExecution.Remote,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching)]
public sealed partial class OrdersViewModel : ReactiveObject, IDisposable
{
    private readonly IDataGridQueryProvider<Order, int> _provider;

    private void InitializeRemoteQueries()
    {
        InitializeOrders(CreateOrdersController());
        InitializeOrdersRemoteQuery(CreateOrdersRemoteQueryController(
            debounce: TimeSpan.FromMilliseconds(50),
            pageCacheCapacity: 4,
            fieldNameTranslator: TranslateBackendField));
    }

    private ValueTask<DataGridQueryPage<Order, int>?> LoadPageAsync(
        int pageIndex,
        CancellationToken cancellationToken) =>
        QueryOrdersAsync(
            DataGridPageRequest.FromOffset(pageIndex * 50, 50),
            cacheKey: $"v{Orders.Version}:page:{pageIndex}",
            cancellationToken: cancellationToken);
}
```

`Create{Name}RemoteQueryController`, `Initialize{Name}RemoteQuery`, `Query{Name}Async`, `{Name}RemoteQuery`, and `Dispose{Name}RemoteQuery` are generated for a keyed remote controller. A newer request cancels the previous request and advances the revision. A provider response is accepted only when both its revision and the controller's current revision match; canceled or stale results return `null` and cannot overwrite current rows. `StateChanged` reports loading, errors, and stale suppression. Offset and opaque-cursor paging are both supported through `DataGridPageRequest`.

The optional page cache is bounded by page count and uses caller-defined stable cache keys, so keys should include every operation or data revision that affects the page. `TranslateField` maps stable generated field IDs to backend names without changing schema identity. Providers may call the generated schema's `CreateSortComparer`, `CreateFilterPredicate`, and `CreateSearchPredicate` for an in-process reflection-free implementation, or serialize the descriptors for a remote backend.

`DataGridSample.Pages.GeneratedRemoteQueryPage` demonstrates the complete ReactiveUI path: generated descriptor builders, offset paging, cached back-navigation, field translation, cancellation, a cancellation-resistant stale response, loading/error/content projection, retry, deterministic service data, and passive generated-view hosting. ViewModel and Avalonia Headless tests cover the provider contract, cache, filters, sorting, search, stale suppression, disposal, and error recovery.

## Generated hierarchy, selection, and state helpers

Annotate one children property with `[DataGridChildren]`, an optional writable Boolean property with `[DataGridExpanded]`, and an optional parent-key field/property with `[DataGridParentKey]`. The provider emits typed `CreateHierarchicalOptions()` and `GetParentKey()` methods. Invalid or ambiguous hierarchy members produce `PDGSG109`.

Set `HierarchicalRows = true` on `[GenerateDataGridColumns]` when the schema's columns are displayed by a DataGrid using `HierarchicalRowsEnabled`. Canonical manifest accessors remain typed to the item, while column bindings are generated against `HierarchicalNode` and unwrap `node.Item` with compiled delegates. Sort member paths are emitted under `Item.`. This replaces the common `x:CompileBindings="False"` hierarchical-column workaround.

A keyed schema also emits `CreateIdentitySelectionModel()` and `CreateStateOptions(...)`. Both reuse the same typed identity selector as the item index, streams, snapshot reconciliation, and `SourceCache`; persisted state and selection therefore cannot drift onto a different property-key convention.

Set a stable schema ID and increment `StateVersion` whenever persisted state needs a migration. `PreviousColumnKeys` declares compile-time-validated aliases for renamed columns:

```csharp
[GenerateDataGridColumns(
    ProviderName = "TradeSchema",
    SchemaId = "sample/trade/v2",
    StateVersion = 2)]
public sealed class Trade
{
    [DataGridKey]
    [DataGridColumn(ColumnKey = "id")]
    public int Id { get; init; }

    [DataGridColumn(ColumnKey = "symbol", PreviousColumnKeys = ["ticker"])]
    public string Symbol { get; set; } = string.Empty;
}
```

The keyed provider emits `CreateSelectionController`, `CreateIdentitySelectionModel`, `CreateStateDescriptor`, `CreateStateOptions`, and `CreateStateController`. Configure a generated view with the same selection model and state controller:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    SelectionModelPropertyName = nameof(SelectionModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.FullRow,
    StateControllerPropertyName = nameof(StateController),
    InteractionPropertyNames = [nameof(ManageGridState)],
    InteractionHandlerTypes = [typeof(TradeStateInteractionHandler)])]
public sealed partial class TradesViewModel : ReactiveObject
{
}
```

`DataGridGeneratedSelectionController<TItem,TKey>` stores row, column, cell, and current-cell selection by stable keys. `PreserveUnloadedKeys` retains selections across paging. Its identity-model projection scans the model's current filtered and sorted source, so selected indexes never come from a stale raw-source position. Controller-driven selection and generated state restoration explicitly supersede any older identity restore queued by a collection-view reset, while ordinary source replacement still preserves the prior identity snapshot.

`DataGridGeneratedStateController` captures the configured DataGrid sections into an envelope containing the schema ID, deterministic shape hash, and state version. It validates the envelope, applies column aliases, calls the optional migration delegate, and serializes through `IDataGridStateSerializer`; the default serializer uses generated JSON metadata. Generated ReactiveUI views expose `CaptureGeneratedState` and `RestoreGeneratedState`, allowing a typed `Interaction` handler to access the owned DataGrid while the ViewModel remains UI-framework agnostic. Scroll-state sampling excludes the new-item placeholder before invoking the typed item-key selector.

`DataGridSample.Pages.GeneratedSelectionStatePage` demonstrates extended selection across paging and item replacement, filtered/reordered identity projection, every persisted state section, JSON capture, deliberate state scrambling, restoration, a `ticker` to `symbol` alias, and a version-one-to-version-two migration. ViewModel, runtime, generator, and Avalonia Headless tests cover the complete flow.

## Diagnostics

| Code | Meaning |
| --- | --- |
| `PDGSG001` | Target type is unsupported. |
| `PDGSG002` | No eligible columns were found. |
| `PDGSG003` | A property type cannot use the requested column kind. |
| `PDGSG004` | A configuration or factory method is invalid. |
| `PDGSG005` | A generated view model or containing type is not partial. |
| `PDGSG006` | A requested generated member already exists. |
| `PDGSG007` | A custom schema implementation does not satisfy the contract. |
| `PDGSG008` | A namespace request matched no eligible types. |
| `PDGSG009` | Required column configuration is missing. |
| `PDGSG010` | A requested item property is inaccessible. |
| `PDGSG011` | An item property is ambiguous. |
| `PDGSG012` | A generated-view binding member is missing. |
| `PDGSG013` | A generated-view custom base is invalid. |
| `PDGSG014` | A requested generated-view framework is not referenced. |
| `PDGSG100` | A stable column key is empty or duplicated. |
| `PDGSG101` | A `[DataGridKey]` member is invalid, nullable, or ambiguous. |
| `PDGSG103` | A generated controller source member is missing or incompatible. |
| `PDGSG104` | The source kind and operation owner would execute operations twice or in the wrong layer. |
| `PDGSG109` | Generated hierarchy metadata is invalid or ambiguous. |
| `PDGSG117` | A named controller collides with another generated controller. |
| `PDGSG118` | Persisted schema/state metadata is invalid. |
| `PDGSG121` | Formula names or stable-key dependencies are invalid. |
| `PDGSG122` | A custom-drawing factory type/method is conflicting or incompatible. |
| `PDGSG123` | Generated row-details sources or typed nested-grid members are conflicting or incompatible. |
| `PDGSG124` | A button/toggle member binding is unsupported, conflicting, inaccessible, missing, or has an invalid command type. |
| `PDGSG125` | A generated view-state projection is incomplete or uses an incompatible state, message, or command member. |
| `PDGSG126` | A generated routed-event bridge uses unsupported flags or an incompatible command member. |
| `PDGSG127` | A generated ReactiveUI interaction or typed navigation-interaction declaration has mismatched metadata, an incompatible property, or an invalid handler implementation. |
| `PDGSG128` | A generated-view performance profile, input map, input command, diagnostics sink, or provably incompatible high-frequency setting is invalid. |
| `PDGSG129` | A generated clipboard-import or fill-model binding is missing or incompatible. |
| `PDGSG130` | A generated formula-model binding is missing or does not implement `IDataGridFormulaModel`. |
| `PDGSG131` | A generated conditional-formatting-model binding is missing or does not implement `IConditionalFormattingModel`. |

The generator is incremental and emits stable hint names and deterministic column ordering, making generated-source diffs and build caching predictable. Direct type and property column triggers, ViewModel, controller, generated-view, indexed-column, and cell-draw-cache requests use isolated attributed pipelines. The compilation-wide semantic model is activated only when an assembly/namespace policy or registry actually requires cross-type coordination, so ordinary direct-attribute consumers do not enumerate unrelated source types after a compilation edit.

## Generate code-only Avalonia views

Source generators cannot add XAML to Avalonia's XAML compilation pipeline, so `GenerateDataGridView` emits an equivalent C# control tree. It uses Avalonia binding indexers and `CompiledBindingExtension` paths backed by generated `ClrPropertyInfo` delegates; it does not create string-path reflection bindings.

Add view generation alongside view-model generation:

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeGridSchema")]
[GenerateDataGridView(
    typeof(Trade),
    ViewName = "TradesView",
    ViewNamespace = "MyApp.Views",
    Title = "Trades",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query))]
public sealed partial class TradesViewModel : ReactiveObject
{
    // Items and model properties remain ordinary view-model state.
}
```

The generated view contains a title, an optional two-way search box, and a configured `DataGrid`. It binds items, definitions, fast-path options, and any named sorting, filtering, search, selection, and state models. The parameterless constructor supports XAML, DI, and view locators; an overload accepts the typed view model directly.

`Recipe` selects a stable layout contract without taking ownership of application-specific controls:

| Recipe | Generated layout contract |
| --- | --- |
| `GridOnly` | Title and grid only. |
| `SearchableGrid` | Compact title/search/grid layout. The search box is emitted when `SearchTextPropertyName` is supplied. |
| `OperationsToolbar` | Adds the `GeneratedToolbarSlot` customization point. |
| `Explorer` | Adds toolbar and `GeneratedExplorerSlot` customization points. |
| `Spreadsheet` | Adds toolbar and `GeneratedFormulaBarSlot` customization points. |
| `Analytics` | Adds toolbar and `GeneratedAnalyticsSlot` customization points. |
| `MasterDetail` | Adds toolbar and `GeneratedDetailsSlot` customization points. |

The generator exposes the selected value through the view's `GeneratedRecipe` constant. Each named slot has a deterministic automation ID and can be replaced by overriding `CreateGeneratedToolbar` or `CreateGeneratedRecipeContent` in a custom base or derived view. `ConfigureGeneratedDataGrid` remains the focused escape hatch for grid configuration. Search is an explicit compiled two-way binding and is independent of recipe selection.

Multiple `GenerateDataGridView` declarations can target the same ViewModel. Each emitted view receives its own framework, recipe, editability, title, automation metadata, and optional bindings while sharing the same generated schema and ViewModel collection:

```csharp
[GenerateDataGridViewModel(typeof(WorkItem), ProviderName = "WorkItemSchema")]
[GenerateDataGridView(
    typeof(WorkItem),
    ViewName = "WorkItemExplorerView",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Explorer,
    SearchTextPropertyName = nameof(Query))]
[GenerateDataGridView(
    typeof(WorkItem),
    ViewName = "WorkItemSpreadsheetView",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Spreadsheet,
    IsReadOnly = false)]
public sealed partial class WorkItemsViewModel : ReactiveObject
{
    // One generated Items/ColumnDefinitions/FastPathOptions contract is shared.
}
```

`GeneratedReactiveViewRecipesPage` in the sample application exercises `GridOnly`, `Explorer`, `Spreadsheet`, and `Analytics` as four independently emitted ReactiveUI controls over one strict attributed-only schema and one `DataGridCollectionView`. Its ViewModel commands mutate the shared source, Explorer and Analytics use the same compiled canonical-field search, and Avalonia Headless coverage verifies each recipe's slots, automation metadata, editability, strict fast path, and shared binding.

Every generated control uses stable automation IDs derived from `AutomationId`. The grid also receives an accessible name/help text, and the title is exposed as a level-one automation heading. These identifiers are covered by Avalonia Headless tests and do not require visual-tree reflection for test lookup.

### Hierarchical generated views

Set `HierarchicalModelPropertyName` when the ViewModel exposes a typed `HierarchicalModel<TItem>`. The generator emits a compiled property-info binding for `DataGrid.HierarchicalModel`, enables hierarchical rows, and adds the `hierarchical` style class:

```csharp
[GenerateDataGridColumns(
    ProviderName = "NodeSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    HierarchicalRows = true)]
public sealed class Node
{
    [DataGridKey]
    public int Id { get; init; }

    [DataGridChildren]
    public ObservableCollection<Node> Children { get; } = new();

    [DataGridExpanded]
    public bool IsExpanded { get; set; }

    [DataGridColumn(DataGridColumnKind.Hierarchical)]
    public Node Item => this;
}

[GenerateDataGridViewModel(typeof(Node), ProviderName = "NodeSchema")]
[GenerateDataGridView(
    typeof(Node),
    Framework = DataGridViewFramework.ReactiveUI,
    HierarchicalModelPropertyName = nameof(Hierarchy))]
public sealed partial class ExplorerViewModel : ReactiveObject
{
    public HierarchicalModel<Node> Hierarchy { get; } =
        new(NodeSchema.CreateHierarchicalOptions());
}
```

The hierarchical model exclusively owns the grid's flattened wrapper `ItemsSource`; the generated view deliberately omits the ordinary root-items binding in this mode. This prevents binding order from replacing `IReadOnlyList<HierarchicalNode>` with the root collection. Generated column bindings remain wrapper-aware (`HierarchicalNode.Item`) while operation descriptors and DynamicData pipelines remain strongly typed to `TItem`.

### Loading, empty, and error projections

Generated views can project one typed state property into mutually exclusive content, loading, empty, and error surfaces. The view model owns the state transition and retry command; generated C# owns only the visual projection and compiled bindings.

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeGridSchema")]
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    ViewStatePropertyName = nameof(ViewState),
    ErrorMessagePropertyName = nameof(ErrorMessage),
    RetryCommandPropertyName = nameof(RetryCommand),
    LoadingText = "Loading trades…",
    EmptyText = "No trades match the current query.",
    ErrorText = "Trades could not be loaded.",
    RetryText = "Try again")]
public sealed partial class TradesViewModel : ReactiveObject
{
    [Reactive]
    private DataGridGeneratedViewState _viewState;

    [Reactive]
    private string? _errorMessage;

    public ReactiveCommand<RxVoid, RxVoid> RetryCommand { get; }
}
```

`ViewStatePropertyName` must resolve to `DataGridGeneratedViewState`. `ErrorMessagePropertyName`, when present, must be `string`; `RetryCommandPropertyName` must implement `ICommand`. ReactiveUI.SourceGenerators `[Reactive]` fields are resolved directly from their declared field types. Missing or incompatible members report `PDGSG012` or `PDGSG125`; the generator never falls back to a reflection binding.

The generated state host keeps the DataGrid alive while hiding it, so loaded rows, column layout, selection, and scroll state survive temporary loading or error projections. Each surface receives stable IDs ending in `-loading`, `-empty`, `-error`, or `-retry`. Applications can replace `CreateGeneratedViewStateHost`, `CreateGeneratedLoadingContent`, `CreateGeneratedEmptyContent`, or `CreateGeneratedErrorContent` in a derived generated view. A non-null error-message property overrides `ErrorText`; a null value restores that static fallback. The same options are available on type-, assembly-, and namespace-level view attributes.

### Routed-event command bridges

Generated views can forward selected DataGrid routed events to one compile-time validated ViewModel command. The generated view subscribes directly to only the requested CLR events and creates a typed snapshot; it does not use reflection, runtime binding paths, or user code-behind.

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeGridSchema")]
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged |
                   DataGridGeneratedViewEventKinds.Sorting |
                   DataGridGeneratedViewEventKinds.Editing,
    RoutedEventCommandPropertyName = nameof(GridEventCommand))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public ReactiveCommand<DataGridGeneratedViewEvent<Trade>, RxVoid> GridEventCommand { get; }
}
```

The supported flags are `SelectionChanged`, `CurrentCellChanged`, `Sorting`, `BeginningEdit`, `CellEditEnding`, `CellEditEnded`, `RowEditEnding`, and `RowEditEnded`; `Editing` and `All` are convenience combinations. `DataGridGeneratedViewEvent<TItem>` exposes the typed row/current items, stable column keys, row index, edit action, selection origin, and zero-copy typed views of added and removed selection items. A command can synchronously set `Cancel` for cancellable edit events or `Handled` for any routed event; generated code copies those values back before routing continues.

`RoutedEventCommandPropertyName` must identify an accessible property implementing `ICommand`; ReactiveUI.SourceGenerators `[Reactive]` command fields are also resolved from their declared type. Invalid flag combinations or incompatible command members report `PDGSG126`. Assembly and namespace view policies support the same options. A derived generated view can override `ConfigureGeneratedRoutedEventCommands(DataGrid)` to add application-specific wiring while retaining the generated bridge.

For ReactiveUI views, routed-event subscriptions are attached by `WhenActivated` and removed on deactivation. Reactivation therefore cannot accumulate duplicate handlers. Plain Avalonia generated views retain view-owned subscriptions for the lifetime of the view.

### Typed ReactiveUI interaction responses

A generated ReactiveUI view can register one or more typed `Interaction<TInput, TOutput>` response adapters without reflection or handwritten code-behind. Property names and implementation types are parallel arrays so the same metadata works on type-, assembly-, and namespace-level view declarations:

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeGridSchema")]
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    InteractionPropertyNames = [nameof(ConfirmTrade)],
    InteractionHandlerTypes = [typeof(ConfirmTradeHandler)])]
public sealed partial class TradesViewModel : ReactiveObject
{
    public Interaction<Trade, bool> ConfirmTrade { get; } = new();
}

public sealed class ConfirmTradeHandler :
    IDataGridGeneratedViewInteractionHandler<Trade, bool>
{
    public ValueTask<bool> HandleAsync(
        DataGridGeneratedViewInteractionContext<Trade> context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(context.Input.Quantity <= 1_000);
    }
}
```

Every configured property must implement the exact `IInteraction<TInput, TOutput>` contract. Its paired implementation must be accessible, non-abstract, closed, parameterless, and implement `IDataGridGeneratedViewInteractionHandler<TInput, TOutput>` with the same type arguments. `PDGSG127` rejects incomplete arrays, duplicate properties, non-ReactiveUI views, incompatible interaction types, and invalid implementation types.

The generated view observes `DataContext` directly through the Avalonia property observable while active. Replacing the ViewModel unregisters and disposes the old response adapters, cancels their context token, and registers adapters for the replacement. Deactivation performs the same cleanup. Handlers that implement `IDisposable` are disposed, and an in-flight handler receives cancellation through `DataGridGeneratedViewInteractionContext<TInput>.CancellationToken`.

Each generated interaction also exposes a protected `CreateGeneratedInteractionHandlerN()` factory. A derived generated view can override that factory to construct a DI-backed or otherwise application-specific implementation while preserving the compile-time interaction signature and generated lifetime management.

### Performance profiles, keyboard maps, and renderer metrics

Generated views can apply an explicit performance profile, replace the grid keyboard map, forward command-oriented gestures to a typed ViewModel command, and consume ProDataGrid renderer metrics through a typed sink:

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeGridSchema")]
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    PerformanceProfile = DataGridGeneratedPerformanceProfile.VariableHeightEstimated,
    InputMapType = typeof(TradeGridInputMap),
    InputCommandPropertyName = nameof(GridInputCommand),
    DiagnosticsSinkType = typeof(TradeGridMetricsSink))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public ReactiveCommand<DataGridGeneratedInputEvent<Trade>, RxVoid> GridInputCommand { get; }
}
```

`PerformanceProfile` selects a named `DataGridGeneratedPerformanceOptions` preset. The generated view applies it before calling `ConfigureGeneratedDataGrid`, so an explicit application override always wins. A derived view can instead override `CreateGeneratedPerformanceOptions()` to provide a custom row-height estimator, scrolling choice, or related runtime setting without changing the ViewModel.

`InputMapType` must be an accessible, non-abstract, closed, parameterless implementation of `IDataGridGeneratedInputMap`. `CreateKeyboardGestureOverrides` replaces the grid's built-in gesture set, while the allocation-free `TryMatch` path maps command-oriented keys. The default map exposes platform-command+F for search; the spreadsheet profile additionally exposes fill-down, fill-right, undo, and redo. The generated view obtains the platform command modifier from Avalonia, falling back to Control. `DataGridGeneratedInputEvent<TItem>` contains the typed selected row, row and display-column indexes, physical key and modifiers, action, and mutable handled feedback without exposing the DataGrid to the ViewModel.

`DiagnosticsSinkType` must implement `IDataGridGeneratedMetricsSink`. `DataGridGeneratedMetricsBridge` listens only to `ProDataGrid.Diagnostic.Meter` and forwards long and double counter, up/down-counter, and histogram samples with the generated schema ID and active performance profile. Metric tags remain a `ReadOnlySpan<KeyValuePair<string, object>>`; the bridge does not allocate a tag collection. The subscription owns and deterministically disposes the sink. ReactiveUI views scope input handlers and metric subscriptions to `WhenActivated`; plain Avalonia views own input handlers with the view and metric subscriptions with visual-tree attachment.

Built-in renderer instruments are opt-in. Set the `ProDataGrid.Diagnostics.IsEnabled` AppContext switch before DataGrid initialization as described in [Metrics and Activities](metrics-and-activities.md). The current meter is process-wide, so a generated subscription supplies its view's schema/profile context but cannot infer which individual DataGrid instance produced a built-in sample.

The generated `CreateGeneratedInputMap()` and `CreateGeneratedMetricsSink()` factories are protected and virtual. Applications can therefore use a configured parameterless implementation directly or override the factories in a derived generated view to resolve DI-backed implementations. Invalid profiles, command properties, input maps, sinks, or activation-incompatible ReactiveUI custom bases report `PDGSG128` or `PDGSG013` at compile time. Type-, assembly-, and namespace-level view attributes expose the same options.

### Typed current-cell and scroll interactions

ReactiveUI generated views can expose a dedicated UI-to-ViewModel-neutral navigation boundary:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    NavigationInteractionPropertyName = nameof(GridNavigation))]
public sealed partial class TradeViewModel : ReactiveObject
{
    public Interaction<
        DataGridGeneratedNavigationRequest<Trade>,
        DataGridGeneratedNavigationResult<Trade>> GridNavigation { get; } = new();
}

DataGridGeneratedNavigationResult<Trade> selected = await GridNavigation
    .Handle(DataGridGeneratedNavigationRequest<Trade>.SetCurrentCell(
        selectedTrade,
        "trade-price"))
    .ToTask();

DataGridGeneratedNavigationResult<Trade> moved = await GridNavigation
    .Handle(DataGridGeneratedNavigationRequest<Trade>.MoveCurrentCell(
        columnOffset: 1,
        rowOffset: 0))
    .ToTask();

DataGridGeneratedNavigationResult<Trade> captured = await GridNavigation
    .Handle(DataGridGeneratedNavigationRequest<Trade>.CaptureScrollState())
    .ToTask();
```

The property must implement the exact `IInteraction<DataGridGeneratedNavigationRequest<TItem>, DataGridGeneratedNavigationResult<TItem>>` contract. The generated view registers its built-in `DataGridGeneratedNavigationHandler<TItem>` only while activated and reconnects it when `DataContext` changes. Requests support current-cell query/set, stable-key bring-into-view, XY movement over visible columns and active-view rows, and scroll-state capture/restore. Results return a non-allocating status enum plus typed item, row, display-column, stable column key, and optional scroll state. A derived view can override `CreateGeneratedNavigationInteractionHandler()` for DI or application-specific navigation policy.

`PDGSG127` rejects non-ReactiveUI use and item-type or input/output mismatches. `PDGSG128` rejects `HighFrequencyStreaming` combined with `RowDetailsVisibilityMode.Visible`, because that setting realizes details for every row and contradicts the bounded high-frequency profile.

### Typed row details and nested grids

`GenerateDataGridView` can assign a row-details template without reflection-based view lookup. A built-in nested-grid recipe reads the detail collection through a validated typed property, reuses a generated presenter, and references the nested item schema directly:

```csharp
[GenerateDataGridColumns(
    ProviderName = "AuthorSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class Author
{
    [DataGridColumn(DataGridColumnKind.Text, Order = 0, Width = "2*")]
    public string Name { get; set; } = string.Empty;
}

[GenerateDataGridViewModel(typeof(Book), ProviderName = "BookSchema")]
[GenerateDataGridView(
    typeof(Book),
    Framework = DataGridViewFramework.ReactiveUI,
    ItemsPropertyName = nameof(Books),
    RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected,
    RowDetailsNestedItemType = typeof(Author),
    RowDetailsNestedItemsMember = nameof(Book.Authors),
    RowDetailsNestedProviderName = "AuthorSchema",
    RowDetailsSummaryMember = nameof(Book.Summary),
    RowDetailsAutomationId = "book-authors-grid")]
public sealed partial class BooksViewModel : ReactiveObject
{
    public ObservableCollection<Book> Books { get; } = new();
}
```

`RowDetailsNestedItemsMember` must be an accessible `IEnumerable<TNested>` property. The optional summary member must be a readable `string`. Generated code creates nested column definitions and fast-path options once per recycled presenter, then updates only the typed summary and items source when the presenter is reused. The detail host, summary, and nested grid receive stable automation metadata.

Three full-customization alternatives are available and are mutually exclusive with the nested recipe:

- `RowDetailsTemplateKey` applies an Avalonia dynamic resource reference, so theme/runtime resource updates remain active.
- `RowDetailsTemplateImplementationType` constructs a validated, accessible, parameterless `IDataTemplate` implementation.
- `RowDetailsTemplateFactoryMethod` uses a validated static method on the row type with signature `Control Factory(TItem item, Control? existing)` and wraps it in `DataGridGeneratedFuncDataTemplate<TItem>`.

The same options work on assembly-level `GenerateDataGridView` attributes. `AreRowDetailsFrozen` and `RowDetailsVisibilityMode` configure the owning grid. Custom resource, implementation, and factory templates own their internal accessibility metadata; the built-in nested recipe emits it automatically.

Row-details metadata participates in the existing `DirectViewCandidates` → `DirectViewComposition` → `DirectViewSources` incremental pipeline. Referenced row and nested-item type fingerprints invalidate the affected generated view when their relevant shape changes, while unrelated generated views remain cached.

### ReactiveUI strategy

Select the ReactiveUI strategy to generate a typed `ReactiveUserControl<TViewModel>`:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    SearchTextPropertyName = nameof(Query))]
```

The generator recognizes properties produced from ReactiveUI.SourceGenerators `[Reactive]` fields even though Roslyn generators cannot consume one another's output. This allows a field such as `[Reactive] private string _query;` to be the source of the generated two-way `Query` binding. The consumer must reference `ReactiveUI.Avalonia` and initialize current ReactiveUI through `UseReactiveUI` during Avalonia startup.

View emission is selected through an internal strategy registry. Avalonia and ReactiveUI are the first two strategies; another UI MVVM integration can be added without changing column, schema, or view-model discovery.

### Custom base classes and view customization

Set `BaseType = typeof(MyGridViewBase)` to use an accessible, non-sealed `UserControl` base with a parameterless constructor. This supports shared styling, activation, services, and application-specific view infrastructure. A ReactiveUI custom base used with routed-event or interaction activation must also implement `IActivatableView`; an incompatible base reports `PDGSG013` at compile time.

Generated views are inheritable and expose these hooks:

```csharp
protected virtual Control CreateGeneratedContent();
protected virtual DataGrid CreateGeneratedDataGrid();
protected virtual void ConfigureGeneratedDataGrid(DataGrid dataGrid);
protected virtual void ConfigureGeneratedRoutedEventCommands(DataGrid dataGrid);
protected virtual Control CreateGeneratedViewStateHost(DataGrid dataGrid);
protected virtual Control CreateGeneratedLoadingContent();
protected virtual Control CreateGeneratedEmptyContent();
protected virtual Control CreateGeneratedErrorContent();
protected virtual Control? CreateGeneratedToolbar();
protected virtual Control? CreateGeneratedRecipeContent();
```

Subclass the generated view to replace the layout or fully customize the grid while retaining generated compiled bindings. The view model remains UI-framework agnostic.

Views can also be requested externally with assembly attributes:

```csharp
[assembly: GenerateDataGridView(
    typeof(TradesViewModel),
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI)]

[assembly: GenerateDataGridViewsForNamespace("MyApp.ViewModels")]
```

Namespace view generation infers the item type from `ItemsPropertyName`, matching namespace-level view-model augmentation.

A partial ViewModel can receive multiple named schema projections. Give every projection distinct member names:

```csharp
[GenerateDataGridViewModel(
    typeof(MetricRow),
    ColumnDefinitionsPropertyName = "MetricColumns",
    SchemaPropertyName = "MetricSchema",
    FastPathOptionsPropertyName = "MetricFastPath")]
[GenerateDataGridViewModel(
    typeof(ActivityRow),
    ColumnDefinitionsPropertyName = "ActivityColumns",
    SchemaPropertyName = "ActivitySchema",
    FastPathOptionsPropertyName = "ActivityFastPath")]
public sealed partial class DiagnosticsViewModel;
```
