# ProDataGrid Source-Generation Expansion Specification

Status: approved implementation specification; core implementation available, advanced integrations and sample migrations in progress

Target: reflection-free source generation for complex reactive, streaming, remote, hierarchical, spreadsheet, and analytics applications

Last updated: 2026-08-08

Implementation checkpoint (2026-08-08): the canonical manifest and typed field API, direct attribute-scoped incremental schema, ViewModel, controller, indexed-column, generated cell-draw-cache, and generated-view pipelines, gated assembly/namespace/registry coordination, assembly registry with optional Microsoft DI registration and explicit reflection-free XAML view mappings, stable item index, typed operation builders, named operation controller, controller factory/options customization, DynamicData `SourceList`/`SourceCache` ownership, bounded async/channel streaming, keyed snapshot reconciliation, revisioned remote queries, hierarchy loading and wrapper-aware compiled column bindings, keyed selection/state, grouping/summaries, editing/validation/undo, typed DataGrid clipboard/fill adapters, conditional rules and runtime model factories, drag/drop, bands/chooser/layout, indexed columns, recycling cell templates, typed/recycling row details with direct nested-schema references, custom-drawing factories/options and bounded per-item caches, compiled row command/parameter/content accessors for button and toggle columns, distinct values, performance profiles, pivot/chart/formula/outline metadata, localized providers, diagnostics, Avalonia/ReactiveUI view recipes, typed view-state projections, routed-event command bridges, typed ReactiveUI interaction response adapters, platform-aware generated keyboard maps, typed input command bridges, activation-scoped XY/current-cell/scroll-state interactions, and activation/visual-tree-scoped renderer metric sinks are implemented with focused tests. Generated remote-query validation now covers offset paging, bounded cache reuse, field translation, cancellation-resistant stale responses, loading/error/content state, and retry through a passive ReactiveUI sample. The formal editing sample validates direct and DataGrid-driven paste/fill, compiled DataAnnotations and custom policies, async validation, bounded exports, and keyed undo/redo without property paths. The formal conditional-formatting sample validates typed comparison/custom predicates, cached runtime descriptors, cell/row targets, generated view binding, reactive updates, and runtime enable/disable without property reflection. The formal pivot/chart sample validates globally ordered typed pivot fields, generated model construction and customization, pivot-derived chart series, and a direct numeric selector fast path without property paths or boxed numeric access. The formal generated-view recipe sample validates four independently generated ReactiveUI layouts over one strict schema and collection view, including stable recipe constants, named slots, compiled search, editability, automation metadata, and shared source updates. ProDiagnostics and its streaming viewer serve as validation applications with eight generated schemas and no inline DataGrid columns or disabled compiled-binding scopes. Generated views also have Avalonia Headless coverage and deterministic screenshot verification. Assembly/namespace policy and registry discovery intentionally remain in the compilation-wide coordination lane only when requested; direct-only compilations bypass that model and source-type enumeration entirely. Advanced items explicitly marked partial below remain tracked work.

## 1. Purpose

This specification defines the next source-generation capabilities for ProDataGrid. It is based on a static audit of the repository's sample applications, current public DataGrid feature surface, current generator implementation, generator tests, and documentation.

The proposal focuses on three outcomes:

1. Keep row access, data operations, state identity, templates, and view wiring reflection-free and NativeAOT-friendly.
2. Remove repeated sorting, filtering, searching, hierarchy, selection, streaming, and lifecycle code from complex reactive applications.
3. Preserve full customization through small runtime contracts, validated implementation types, partial hooks, and custom generated-view base classes.

Code blocks labelled **Proposed API** describe the remaining target shape. Unlabelled APIs are implemented unless their feature row below is marked partial.

### 1.1 Implementation coverage

| Feature | Status | Implemented boundary / remaining work |
|---|---|---|
| F01 incremental foundation | Implemented | Direct type and property-triggered schemas, ViewModels, controllers, indexed-column triggers, cell-draw caches, and generated views use equatable attributed candidates with isolated composition and stable semantic/output reuse. View framework/collision facts and owner-driven schema options are part of the candidate graph. Assembly/namespace policy and registry coordination remain compilation-wide only when requested; an empty-policy gate prevents global model construction and source-type enumeration for direct-only consumers. |
| F02–F07 identity and data operations | Implemented | Typed fields/builders, key/index services, operation ownership, DynamicData list/cache pipelines, bounded streams, snapshot reconciliation, and revisioned remote queries are available. The remote-query sample validates offset paging, bounded cache reuse, field translation, cancellation, stale-response suppression, observable state, and retry. |
| F08 hierarchy | Core implemented | Typed hierarchy delegates, async loading, expansion/key operations, reset preservation, and `HierarchicalRows` wrapper-aware compiled bindings are available; broader conversion of legacy sample trees remains. |
| F09–F14 data workflows | Core implemented | Grouping, rendered total/group summary definitions, incremental summaries, selection, versioned state/migration, editing/validation/undo, clipboard/fill/export, and conditional rules share canonical accessors. Formal grouping, selection/state, editing/transfer, and conditional-formatting samples validate the main runtime paths; relative-formula fill remains. |
| F15 layout/indexed columns | Implemented | Nested band trees, chooser visibility/order/reset, layout state, method-backed indexed column families, typed formula slots that bypass runtime getters, and replaceable pin/freeze command bridges are available. The formal indexed spreadsheet sample validates runtime family replacement. |
| F16 templates/drawing | Implemented | Typed recycling cell/edit/new-row templates, resource/implementation/factory row-details sources, typed nested-grid recipes, validated custom-drawing factories/options, invalidation-source-compatible wiring, bounded generated item caches, compiled button/toggle command/parameter/content accessors, and template-root automation metadata are available. |
| F17 drag/drop | Implemented | Keyed request/result adapters and domain-owned handlers are available. |
| F18 analytics | Core implemented | Typed pivot fields, globally ordered axis/value factories, generated configurable pivot-model construction, neutral chart/outline/formula roles, direct numeric chart selectors, compile-time formula dependency validation, runtime indexed formula definitions, and an optional reflection-free chart adapter are available. The formal pivot/chart sample validates pivot-derived and direct chart projections; optional formula-parser analyzers, relative-reference fill, keyed chart synchronization, and spreadsheet range projection remain. |
| F19 generated views | Implemented | Avalonia and ReactiveUI code-only views, compiled binding indexers, custom bases, recipes, named slots, automation metadata, state bridges, typed loading/empty/error projections, hierarchical and formula-model bindings, retry-command bindings, typed routed-event command bridges, typed ReactiveUI interaction responses, activation/DataContext-scoped subscriptions, protected handler factories, and `[DataGridViewRegistration]` mappings for existing XAML views are available. |
| F20 localization/accessibility/diagnostics | Implemented | Validated direct localization providers, resource keys, stable automation IDs/names/help, and generated diagnostics manifests are available. |
| F21 collection views/dynamic shapes | Partial | Typed collection-view factories and range-aware generated services are available; unknown runtime shapes still require explicit user adapters. |
| F22 header filtering/distinct values | Implemented | Typed editor metadata, bounded local/remote distinct-value providers, and cached per-field commands for sort/filter/visibility/pin/freeze/autosize/reset are available through a replaceable interaction boundary. |
| F23 performance/input diagnostics | Implemented | Explicit performance profiles, platform-aware keyboard maps, typed input-command feedback, compile-time high-frequency/details compatibility validation, stable-key current-cell and XY navigation, scroll-state interactions, diagnostics metric-name manifests, replaceable renderer metric sinks, and ReactiveUI/Avalonia lifetime management are available. |

## 2. Existing baseline

The current generator described in [Column Definitions (Source Generators)](column-definitions-source-generators.md) already provides:

- `[GenerateDataGridColumns]` on item types and equivalent assembly/namespace discovery.
- `[DataGridColumn]` and `[DataGridIgnoreColumn]` metadata.
- All current generated column kinds.
- Stable keys, ordering, sizing, theme/resource keys, common column options, custom implementation types, factories, and configure hooks.
- Generated `DataGridColumnDefinition` collections and `DataGridFastPathOptions`.
- Typed getters/setters and compiled sort, filter, and search factories.
- DynamicData upstream-operation bypass support.
- `[GenerateDataGridViewModel]` augmentation.
- `[GenerateDataGridView]` code-only Avalonia and ReactiveUI views with custom bases and compiled binding indexers.
- Assembly/namespace-wide generation and diagnostics `PDGSG001` through `PDGSG014`.

The new work must extend these contracts additively. It must not create a second incompatible column metadata system.

## 3. Repository audit

### 3.1 Audited corpus

The audit covered all three application samples:

- `DataGridSample`: 189 page views plus application/window XAML, 169 C# files under `ViewModels`, and 41 model files.
- `ProDataGrid.ExcelSample`: workbook, spreadsheet, formula, fill, clipboard, selection, chart, ribbon, and sheet-tab scenarios.
- `ProDataGrid.MarketDashboardSample`: a live multi-grid/multi-chart ReactiveUI dashboard, async data service, snapshot reconciliation, commands, and DI composition.

Across these applications the audited surface contains:

- 199 `.axaml` files.
- 196 `.axaml.cs` files.
- 180 C# files under `ViewModel`/`ViewModels` folders.
- 47 C# files under `Model`/`Models` folders.
- 29,960 lines in `DataGridSample/ViewModels` alone.
- 15 handwritten DataGrid adapter/factory classes in `DataGridSample/Adapters`.

The audit also covered the 532 public types indexed in the current API documentation across collections, columns, sorting, filtering, searching, grouping, summaries, selection, hierarchy, clipboard, filling, editing, conditional formatting, drag/drop, state, pivoting, reporting, formula, charting, sizing, and diagnostics namespaces.

### 3.2 Quantitative findings

The main sample application contains:

- 170 page views with `ItemsSource` bindings.
- 75 page views using `ColumnDefinitionsSource`.
- 844 explicit DataGrid column elements, including 494 text, 142 template, 100 numeric, 52 hierarchical, and 15 custom-drawing columns.
- 26 hierarchical/tree pages with `x:CompileBindings="False"` even though the application defaults to compiled bindings.
- 177 page code-behind files attaching `DataContext` from `AttachedToVisualTree`.
- 42 page code-behind files with 157 custom event-handler methods.
- 68 XAML `Click="On..."` handlers, plus sorting, selection, editing, lifecycle, clipboard, scroll, and column handlers.
- 26 ViewModel/adapter files constructing 73 sorting, filtering, or search descriptors.
- 27 ViewModel/adapter files manually subscribing to sorting, filtering, search, or selection change events.
- 21 ViewModels using `DeferRefresh` manually.
- 8 DynamicData ViewModels with 15 `SourceList`/`SourceCache` instances and 28 `BehaviorSubject` instances.
- 13 files with 77 handwritten `nameof(...) => ...` property-path switch arms.
- 12 ViewModels directly importing `Avalonia.Threading`.

These counts are not quality metrics. They identify repeated integration seams that source generation can safely standardize.

### 3.3 Sample-derived feature matrix

The page-family counts overlap because several pages exercise more than one feature.

| Feature family | Audited page families | Generator opportunity |
|---|---:|---|
| Generated/auto/bindable/dynamic columns | 36 | Extend schemas to bands, indexed column families, templates, localization, and registries. |
| Sort/filter/search/group | 33 | Generate typed descriptor builders, presets, models, ownership, and operation controllers. |
| Selection/navigation | 28 | Generate stable keys, fast index resolvers, shared selection, and state restoration. |
| Hierarchy/tree | 26 | Generate `HierarchicalOptions<T>`, expansion/key accessors, typed node bindings, and streaming adapters. |
| Dynamic/live/range updates | 15 | Generate DynamicData and async-stream pipelines, batching, scheduling, and disposal. |
| Pivot analytics | 14 | Generate typed axis/value/calculated-field selectors and layout profiles. |
| Formula/Power Fx | 8 | Generate formula metadata, dependency/value access, validation, and static rule registration. |
| Summaries | 5 | Generate typed summary descriptions and incremental aggregate accessors. |
| State persistence | 10 | Generate stable key maps, schema versions, serializers, and migration hooks. |
| Clipboard/fill/edit/validation | 12 | Generate typed conversion, import/export, fill, validation, and edit-policy adapters. |
| Drag/drop | 8 | Generate keyed flat/hierarchical reorder adapters and command/interaction bridges. |
| Chart/report integration | 4 | Generate typed chart series, range projections, selection synchronization, and outline fields. |
| Virtualization/scroll/performance | 14 | Generate performance profiles, row-height/key accessors, and runtime diagnostics manifests. |
| Styling/conditional formatting | 4 | Generate typed predicates and resource-key metadata while leaving visuals in resources. |
| Complex mimic/application layouts | 16 | Add reusable view recipes without attempting to generate bespoke shells. |
| Frozen/layout/banding | 5 | Generate band trees, visibility/chooser metadata, width groups, and layout-state keys. |

Representative evidence includes:

- `GeneratedColumnsDynamicDataViewModel` demonstrates the current best path but still manually owns three models, three subjects, three model-event subscriptions, the DynamicData pipeline, commands, and disposal.
- The DynamicData adapter folder repeats property-path switches and descriptor translation for flat and hierarchical sources.
- Hierarchical pages disable compiled XAML binding because the runtime node wrapper is difficult to type in XAML.
- `SheetViewModel` builds runtime indexed columns with handwritten `IPropertyInfo`, typed delegates, formula special cases, and per-slot configuration.
- The Excel sample requires handwritten attached binders for fast-path options, grid selection state, clipboard state, and row-drag policy.
- `StateFullPage` performs capture, migration-sensitive key resolution, serialization, and restore directly in the view.
- The market dashboard reconciles service snapshots into multiple collections, dispatches updates to the UI scheduler, coordinates five grids/charts, and manually owns many commands and event subscriptions.

### 3.4 What should not be generated

The samples also contain logic that must remain user code:

- Domain rules, order execution, portfolio accounting, and external API clients.
- Bespoke application shells such as the complete Excel ribbon or market terminal layout.
- Arbitrary formula or Power Fx evaluation semantics.
- Branding, resource dictionaries, control themes, and visual design.
- Custom chart rendering algorithms.
- Data storage, networking, authentication, and retry policy.

The generator should produce typed metadata, adapters, controllers, and optional reusable view composition. It should not become an application framework.

### 3.5 ProDiagnostics validation migration

ProDiagnostics is a production validation lane, not a synthetic sample. It combines flat read-only grids, editable template cells, multi-grid view models, live telemetry, column visibility, hierarchical wrappers, existing XAML views, and an inspector whose domain is arbitrary runtime objects.

| Surface | Row type | Generated contract | Validation purpose |
|---|---|---|---|
| Viewer metrics | `MetricSeriesViewModel` | streaming schema, keyed template, fast-path options, layout controller | high-frequency updates, numeric formatting, reusable trend cell, column chooser |
| Viewer activities | `ActivityEventViewModel` | streaming schema and second named ViewModel projection | multiple schemas on one ViewModel and formatted telemetry rows |
| Assets | `AssetEntryViewModel` | attributed-only schema | sortable reflection-free read-only grid |
| Control properties | `PropertyViewModel` | shared template schema and generated layout visibility | editable recycling template plus runtime column-profile switching |
| Resource details | `PropertyViewModel` | second ViewModel projection of the shared schema | schema reuse with per-view layout policy |
| Resource picker | `ResourceReferenceEntryViewModel` | text/template schema | external collection-view sort/filter ownership |
| Resources | `ResourceEntryViewModel` and `ResourceTreeNode` | two named schemas on one ViewModel | flat and hierarchical grids on the same screen |
| Visual/logical tree | `TreeNode` | hierarchical-row schema | compiled binding through `HierarchicalNode.Item` with no XAML binding fallback |

The migration introduced two APIs because the application exposed real gaps:

```csharp
[GenerateDataGridColumns(
    Discovery = DataGridColumnDiscovery.AttributedOnly,
    HierarchicalRows = true)]
internal abstract class TreeNode
{
    [DataGridColumn(
        DataGridColumnKind.Hierarchical,
        SortMemberPath = nameof(Type),
        TemplateKey = "VisualTreeNodeCellTemplate")]
    public TreeNode Item => this;
}
```

`HierarchicalRows` keeps canonical schema accessors typed to `TItem`, but emits separate compiled column bindings and value accessors typed to `HierarchicalNode`. Generated sort paths are prefixed with `Item.`. This preserves typed data operations while matching the row wrapper actually presented by a hierarchical DataGrid.

```csharp
[assembly: GenerateDataGridRegistry(
    RegistryName = "ProDiagnosticsGeneratedSchemas",
    RegistryNamespace = "Avalonia.Diagnostics.Generated")]
[assembly: DataGridViewRegistration(typeof(TreePageViewModel), typeof(TreePageView))]

// Generated registry usage; no Type.GetType or Activator.CreateInstance.
if (ProDiagnosticsGeneratedSchemas.TryCreateView(viewModel, out Control? view))
{
    return view;
}
```

Multiple `[GenerateDataGridViewModel]` attributes on one partial type are supported when each projection has distinct member names. Hint names include the column-definition member name so the generated files remain deterministic and collision-free.

The reflection-free boundary is explicit:

- ProDiagnostics' own grid schemas, compiled column bindings, fast-path options, column layout, schema registry, and view lookup are generated.
- Reflection used only to inspect unknown third-party runtime objects remains in the inspector domain. It is not a DataGrid binding or view-location fallback.
- Future inspected assemblies may opt into generated inspection metadata. Unknown types still require the inspector's dynamic provider; silently pretending they are statically knowable would make the diagnostics tool incomplete.

Migration validation includes generator-driver tests for hierarchical wrappers, multi-grid ViewModels, registered XAML views, generated-code compilation, ProDiagnostics registry/schema tests, Avalonia Headless view creation, and the complete ProDiagnostics test suite. A repository audit test or build check should continue to prevent reintroduction of inline DataGrid columns and `x:CompileBindings="False"` in these two validation projects.

## 4. Design principles

### 4.1 Reflection-free by construction

Generated paths must not call `Type.GetProperty`, `PropertyDescriptor`, expression compilation, `DynamicInvoke`, or runtime XAML loading to discover row members. A relaxed compatibility mode may use existing DataGrid behavior outside a generated controller, but generated strict mode must report an error or disable the affected feature instead of silently adding reflection.

### 4.2 One canonical schema

Column keys, property keys, typed accessors, summaries, conditional rules, state, hierarchy, export, chart, and pivot metadata must reference one generated schema manifest. The same property must not be rediscovered independently by each feature.

### 4.3 Explicit operation ownership

Sorting, filtering, and searching each have exactly one execution owner:

- `View`: `DataGridCollectionView` applies the descriptors.
- `ExternalPipeline`: DynamicData or another local reactive pipeline applies them.
- `Remote`: a query provider applies them server-side.

Generated adapters must set the existing ownership flags consistently. A generated application must never sort or filter twice.

### 4.4 Strict MVVM layering

New generated ViewModel/controller APIs should be UI-framework neutral. The preferred architecture is:

1. A generated metadata and operation controller in a small presentation runtime.
2. A generated Avalonia adapter that maps the controller to existing DataGrid models and events.
3. An optional generated view that owns only UI composition and adapter activation.

The existing `[GenerateDataGridViewModel]` members that expose Avalonia DataGrid model types remain supported for compatibility. New complex-application APIs should prefer the layered controller mode.

### 4.5 Framework strategy, not framework lock-in

The core output must work with plain INPC. ReactiveUI adds activation, `ReactiveCommand`, schedulers, observable properties, and `Interaction<TInput,TOutput>` where relevant. Additional MVVM strategies may be added later as separate generator strategies without changing schema metadata.

### 4.6 Bounded lifetime and backpressure

Every generated subscription, timer, channel, and cache has an explicit owner and a deterministic disposal path. Async and streaming adapters require an explicit buffer/coalescing policy. Unbounded queues are not a default.

### 4.7 Customization precedence

For every extensible feature, precedence is:

1. Explicit user implementation/factory type.
2. A correctly shaped named partial hook or factory method.
3. Generated default implementation.
4. Existing runtime fallback only when strict mode is disabled and the user has explicitly selected fallback behavior.

Property metadata overrides type defaults; type defaults override namespace defaults; namespace defaults override assembly defaults.

## 5. Proposed architecture

### 5.1 Packages and layers

| Layer | Responsibility | Dependencies |
|---|---|---|
| `ProDataGrid.SourceGeneration.Abstractions` | Public attributes/enums needed across assemblies. | BCL only. |
| `ProDataGrid.SourceGenerators` | Incremental discovery, validation, and emission. | Roslyn only. |
| `ProDataGrid.Generation.Runtime` | UI-neutral schema, query, controller, lifetime, and state contracts. | BCL; optional System.Reactive abstractions only if unavoidable. |
| `ProDataGrid.Generation.Avalonia` | Adapters to DataGrid models, bindings, interactions, and generated views. | Avalonia and ProDataGrid. |
| `ProDataGrid.Generation.DynamicData` | `SourceList`/`SourceCache` pipelines and change-set policies. | DynamicData. |
| `ProDataGrid.Generation.ReactiveUI` | Activation, commands, interactions, and schedulers. | ReactiveUI, not `Avalonia.ReactiveUI`. |

Package splitting is a target architecture. Initial implementation may keep assemblies consolidated while enforcing these dependency boundaries internally.

### 5.2 Incremental generator pipeline

The generator uses isolated incremental pipelines for direct requests and a separately gated compilation-wide lane for assembly/namespace expansion and registries:

- `ForAttributeWithMetadataName` for item, property, ViewModel, view, assembly, and namespace triggers.
- Small immutable, equatable semantic models before `.Collect()`.
- Separate pipelines for schema, controllers, views, registries, and diagnostics.
- Reference/capability detection isolated from source syntax changes.
- Assembly/namespace expansion collected only within the affected policy scope.
- Stable hint names based on metadata identity and feature name.
- Deterministic ordering independent of syntax tree order.
- Cancellation checks in discovery and emission loops.

Editing one row type must not invalidate generated outputs for unrelated schemas or views.

The `CompilationWideRequests` gate checks the assembly request surface before the global semantic step. When no assembly/namespace policy or registry is present, it emits no value, so unrelated compilation edits cannot invoke `Discovery.Build` or enumerate source types. Property-only schemas use their own attributed candidate lane, preserving their diagnostics and output without depending on the global scan. Incremental run-reason tests cover the direct type/property bypass, property-candidate isolation and invalidation, and an active namespace policy.

### 5.3 Generated manifest

Every schema should expose a versioned manifest that is reusable by all generated features.

```csharp
// Proposed generated shape; abbreviated.
public static class TradeGridSchema
{
    public const int ManifestVersion = 1;
    public const string SchemaId = "Trading.Trade/v1";
    public const string SchemaHash = "...";

    public static ReadOnlySpan<DataGridGeneratedField> Fields { get; }
    public static DataGridGeneratedAccessor<Trade, int> Id { get; }
    public static DataGridGeneratedAccessor<Trade, decimal> Price { get; }

    public static bool TryGetField(string key, out DataGridGeneratedField field);
    public static IComparer<Trade> CreateSortComparer(ReadOnlySpan<GridSort> sorts);
    public static Predicate<Trade> CreateFilter(ReadOnlySpan<GridFilter> filters);
    public static Predicate<Trade> CreateSearch(in GridSearch search);
}
```

The runtime manifest stores stable IDs and delegates, not reflection metadata. A diagnostic/debug view may expose names and types, but hot paths use ordinal field IDs or generated switch dispatch.

### 5.4 Reflection-free registry and DI

For each assembly the generator should optionally emit:

```csharp
public static class GeneratedProDataGridRegistration
{
    public static IServiceCollection AddGeneratedProDataGrids(
        this IServiceCollection services);

    public static bool TryGetSchema(
        Type itemType,
        out IDataGridGeneratedSchema schema);

    public static bool TryCreateView(
        Type viewModelType,
        out Control view);
}
```

The implementation is a generated type switch or frozen lookup table. It must not scan assemblies. A non-DI overload should remain available so Microsoft DI is optional.
The `IServiceCollection` overload is emitted only when Microsoft DI is referenced.

## 6. Proposed API conventions

### 6.1 Controller trigger

```csharp
[Flags]
public enum DataGridGeneratedFeatures
{
    None = 0,
    Columns = 1 << 0,
    Sorting = 1 << 1,
    Filtering = 1 << 2,
    Searching = 1 << 3,
    Selection = 1 << 4,
    State = 1 << 5,
    Hierarchy = 1 << 6,
    Grouping = 1 << 7,
    Summaries = 1 << 8,
    ConditionalFormatting = 1 << 9,
    Editing = 1 << 10,
    Clipboard = 1 << 11,
    Fill = 1 << 12,
    DragDrop = 1 << 13,
    Diagnostics = 1 << 14
}

public enum DataGridGeneratedSourceKind
{
    Enumerable,
    ObservableCollection,
    DynamicDataSourceList,
    DynamicDataSourceCache,
    AsyncEnumerable,
    ChannelReader,
    Remote
}

public enum DataGridOperationExecution
{
    View,
    ExternalPipeline,
    Remote
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GenerateDataGridControllerAttribute : Attribute
{
    public GenerateDataGridControllerAttribute(Type itemType, string name);

    public string? ProviderName { get; set; }
    public string? SourceMember { get; set; }
    public DataGridGeneratedSourceKind SourceKind { get; set; }
    public DataGridGeneratedFeatures Features { get; set; }
    public DataGridOperationExecution OperationExecution { get; set; }
    public string? KeyMember { get; set; }
    public Type? ImplementationType { get; set; }
    public string? ConfigureMethod { get; set; }
    public bool Strict { get; set; } = true;
    public bool Streaming { get; set; }
}
```

The `name` is required so one ViewModel may own several grids without member collisions. Existing `[GenerateDataGridViewModel]` remains the concise single-grid compatibility API.

### 6.2 Item metadata

The existing `[DataGridColumn]` remains the primary property attribute. The following focused attributes extend its schema:

```csharp
[DataGridKey]
[DataGridChildren]
[DataGridExpanded]
[DataGridParentKey]
[DataGridGroup(Order = 0, Direction = ListSortDirection.Ascending)]
[DataGridSummary(DataGridAggregateType.Sum, Scope = DataGridSummaryScope.Both)]
[DataGridConditionalFormat(
    DataGridCondition.GreaterThan,
    Operand = "1000000",
    CellThemeKey = "LargeTradeCell")]
[DataGridBand("Execution", Order = 1)]
[DataGridExport(Format = "N2", NullText = "-")]
[DataGridValidation(ValidatorMethod = nameof(ValidatePrice))]
[DataGridPivotAxis(DataGridPivotFieldArea.Rows, Order = 0)]
[DataGridPivotValue(PivotAggregateType.Sum, Format = "C2")]
[DataGridChartValue(Series = "Price", Role = DataGridChartRole.Value)]
```

Attributes that reference user code use `nameof(...)`. Discovery validates the complete method signature and accessibility.

### 6.3 Small customization contracts

Do not introduce one feature-factory interface with dozens of methods. Use focused contracts such as:

```csharp
public interface IDataGridItemKey<TItem, TKey>
{
    TKey GetKey(TItem item);
}

public interface IDataGridGeneratedFilterFactory<TItem>
{
    Predicate<TItem> Create(ReadOnlySpan<GridFilter> filters);
}

public interface IDataGridGeneratedSummaryFactory<TItem>
{
    IDataGridIncrementalSummary<TItem> Create(in DataGridSummaryContext context);
}

public interface IDataGridGeneratedViewAdapter<TViewModel>
{
    void Attach(Control view, TViewModel viewModel, CompositeDisposable lifetime);
}
```

An implementation type is emitted as a direct constructor or static factory call after compile-time validation. The generator never instantiates user code inside the compiler process.

### 6.4 Generated output names

For a controller named `Trades`, the default generated members are grouped under one property:

```csharp
public TradeGridController Trades { get; private set; } = null!;
public void InitializeTrades(TradeGridController controller);
```

The controller exposes `Items`, `Columns`, `FastPath`, operation state, selection, state commands, diagnostics, and lifetime. Explicit initialization is the initial lifetime model: the user constructs the source and generated controller in the constructor, passes it to the generated initialization method, and disposes it with the ViewModel. Flat forwarding properties are opt-in for compatibility with existing XAML. This prevents a ViewModel from receiving twenty generated top-level members per grid.

## 7. Feature specifications

### F01. Incremental foundation and compatibility manifest — P0

Requirements:

- Refactor discovery away from one full-compilation transform.
- Emit one canonical manifest per schema.
- Emit deterministic cross-assembly registries when requested.
- Preserve all existing generated type/member names unless the user selects the new controller API.
- Add a schema format version and stable schema ID.
- Test incremental step caching and deterministic output.

This work is a prerequisite for every other feature.

### F02. Stable identity and fast index resolution — P0

The selection, state, DynamicData `SourceCache`, hierarchy, drag/drop, and chart coordination samples all need stable identity.

Requirements:

- `[DataGridKey]` on a field/property or `KeyMember` on the controller.
- Composite keys through a validated static method.
- Generated typed key selector, equality comparer, key-to-item resolver, and optional item-to-index cache.
- Incremental cache updates for add/remove/move/replace/reset.
- Reference-identity mode only when explicitly selected.
- Diagnostics for nullable, mutable, non-unique, missing, or incompatible keys.
- Reuse the same key in `DataGridStateOptions`, selection preservation, DynamicData cache integration, drag/drop, and chart selection.

The generator should use value-type generic key paths where possible and avoid boxing keys on the steady-state selection path. Existing object-key APIs may be bridged at the Avalonia adapter boundary.

### F03. Typed operation descriptors, builders, and presets — P0

Manual string paths and repeated descriptor construction should be replaced with generated field references:

```csharp
Trades.Sort.Set(
    TradeGridSchema.Price.Descending(),
    TradeGridSchema.Timestamp.Descending());

Trades.Filter.Set(
    TradeGridSchema.Desk.Contains(DeskFilter, StringComparison.OrdinalIgnoreCase),
    TradeGridSchema.Price.GreaterThanOrEqual(MinimumPrice));

Trades.Search.Set(GridSearch.Contains(Query));
```

Requirements:

- Strongly typed builders for every supported filtering operator.
- Compile-time operator/type validation.
- Stable field IDs in normalized descriptors.
- Reusable named sort/filter/search presets declared by attributes or static methods.
- Allocation-conscious `Set`, `SetOrUpdate`, and deferred/batched updates.
- Conversion to existing `SortingDescriptor`, `FilteringDescriptor`, and `SearchDescriptor` only at the UI adapter boundary.
- Custom comparer/predicate hooks remain supported.

### F04. Generated operation controller — P1

Generate sorting, filtering, and search state plus the correct adapter ownership from one controller declaration.

Requirements:

- Independent enablement of sort, filter, and search.
- Multi-sort and sort-cycle configuration.
- Filter combination (`All`/`Any`) and per-field combination policy.
- Search scope, match mode, highlighting, navigation, and debounce configuration.
- Descriptor chips/summaries as optional projected read-only collections.
- Generated clear/apply-preset/remove-descriptor commands in the selected MVVM strategy.
- Correct `OwnsViewSorts`/`OwnsViewFilter` and search ownership behavior.
- No event subscription in the user's ViewModel for standard operation propagation.
- One controller can be headless-tested without creating a DataGrid.

ReactiveUI mode should use `ReactiveCommand` and activation-aware subscriptions. Plain mode should expose commands through small framework-neutral command interfaces or `ICommand` only in the UI adapter.

### F05. DynamicData pipelines — P1

This feature replaces the repeated `BehaviorSubject`, model-event, adapter-factory, `SortAndBind`, and disposal code in the DynamicData samples.

Requirements:

- `SourceList<T>` and `SourceCache<T,TKey>` support.
- Generated filter, search, and multi-sort pipelines using the canonical schema.
- `SortAndBind` configuration including `UseReplaceForUpdates`.
- Source-cache key reuse from `[DataGridKey]`.
- Flat and hierarchical change sets.
- External operation ownership so the DataGrid never re-applies operations.
- Optional transform stage with a user-supplied typed implementation.
- Optional grouping and incremental summaries after filtering.
- Explicit observe-on scheduler at the final UI collection boundary only.
- Test-scheduler support.
- One disposable controller owns all subjects and subscriptions.
- Error and completion observables; no swallowed pipeline failures.

The generated filter/search predicate should be rebuilt only when descriptor revisions change. Row evaluation must not allocate and must not box primitive property values when a typed operator exists.

### F06. Async streams, channels, and snapshot reconciliation — P1

The streaming pages and market dashboard require sources beyond DynamicData.

Requirements:

- `IAsyncEnumerable<T>` and `ChannelReader<T>` adapters.
- Append, upsert-by-key, remove-by-key, and replace-snapshot modes.
- Generated keyed snapshot diffing instead of unconditional collection clear/repopulate.
- Configurable batch size, time window, bounded capacity, and overflow policy.
- Cancellation on controller disposal or ReactiveUI deactivation, as configured.
- Background ingestion with exactly one UI scheduler hop per emitted batch.
- Monotonic revision IDs so stale snapshots or remote responses are ignored.
- Metrics for queued, coalesced, dropped, applied, and stale updates.

Default streaming policy should be bounded coalescing by key, not an unbounded queue.

### F07. Remote/server-side query controller — P1

Complex applications often cannot materialize all rows locally.

**Proposed API:**

```csharp
public interface IDataGridQueryProvider<TItem, TKey>
{
    ValueTask<DataGridQueryPage<TItem, TKey>> ExecuteAsync(
        DataGridQuery query,
        CancellationToken cancellationToken);
}

public sealed record DataGridQuery(
    long Revision,
    GridSortSet Sorts,
    GridFilterSet Filters,
    GridSearch Search,
    DataGridPageRequest Page,
    GridGroupSet Groups);
```

Requirements:

- Offset and cursor paging.
- Cancellation and stale-response suppression.
- Debounce/coalescing of rapid descriptor changes.
- Optional page cache and prefetch policy.
- Total count, unknown count, and streaming continuation support.
- Translation hooks from stable generated field IDs to backend field names.
- Provider errors exposed as state suitable for ReactiveUI binding/interactions.
- No network or persistence implementation in generated code.

### F08. Hierarchical schemas and typed node bindings — P1

**Proposed metadata:**

```csharp
public sealed class FolderNode
{
    [DataGridKey]
    public required Guid Id { get; init; }

    [DataGridChildren]
    public ObservableCollection<FolderNode> Children { get; } = [];

    [DataGridExpanded]
    public bool IsExpanded { get; set; }

    [DataGridColumn(DataGridColumnKind.Hierarchical, ColumnKey = "name")]
    public required string Name { get; init; }
}
```

Requirements:

- Generate `HierarchicalOptions<T>` delegates for children, leaf, expanded getter/setter, and identity.
- Optional parent-key/path and depth selectors.
- Async child-loader hook returning `ValueTask<IReadOnlyList<T>>`.
- Expand/collapse-all and expand-to-key operations.
- Preserve expansion and selection across resets and source swaps.
- Hierarchical filtering modes: ancestors-of-match, descendants-of-match, match-only, and custom.
- Sibling-only and global sort policies.
- SourceList/SourceCache hierarchy support.
- Cycle and duplicate-key diagnostics in strict/debug mode.
- Generate a public typed node projection or binding indexer so hierarchical views can keep compiled bindings enabled.

The final requirement directly addresses the 26 audited pages that currently opt out of compiled bindings.

### F09. Grouping, summaries, and incremental aggregates — P2

Requirements:

- Generate typed group selectors instead of path-based group descriptions.
- Generate default group order, comparer, key formatter, and expansion key.
- Generate total/group summary descriptions from `[DataGridSummary]`.
- Support count, distinct count, sum, average, min, max, and custom calculators.
- Reuse typed column accessors for summary value reads.
- Incremental add/remove/replace aggregation for streaming sources.
- Define reset fallback for calculators that cannot reverse a removed value.
- Preserve current `IDataGridSummaryCalculator` customization.
- Support summary scope, placement, string format, alignment, theme key, and title.

The DataGrid runtime may need a typed group-description contract and a summary value-accessor property so generated code can avoid existing path lookup.

Implemented grouping/summary API: `[DataGridGroup]` emits ordered typed selectors and direct `DataGridGeneratedGroupDescription<TItem,TValue>` adapters. `[DataGridSummary]` emits allocation-conscious `IDataGridGeneratedSummary<TItem>` instances for Add/Remove/Replace/Reset and installs `DataGridSummaryDefinition` metadata on generated column definitions. Each materialized column receives independent aggregate/custom descriptions with scope, format, and title; the DataGrid summary calculators reuse the generated column value accessor instead of property reflection. `DataGridSummaryDefinition.Factory` preserves direct custom-description ownership. Generated views expose total/group visibility and placement options. The formal sample proves two-level grouping, rendered group/total summaries, and deterministic incremental aggregate updates.

### F10. Selection, current cell, and shared selection — P1

Requirements:

- Generate a typed `SelectionModel<T>` configuration from the canonical item key.
- Generate fast index cache or `IDataGridIndexOf` integration.
- Row, cell, column, and mixed selection-unit profiles.
- Strongly typed selected-items and current-item projections.
- Selection preservation across sort/filter/page/hierarchy/source reset.
- Shared selection between multiple grids or another selecting control.
- Selection-origin stream for pointer, keyboard, binding, model, and restore changes.
- Optional ReactiveUI commands for select-all, clear, select-by-key, and range selection.
- Chart/grid selection bridge based on item and column keys.

Selection state must be stored by key, not only by row index.

Implemented selection API: keyed schemas emit an allocation-conscious `DataGridGeneratedSelectionController<TItem,TKey>`, an `IdentitySelectionModel`, selection profiles, immutable snapshots, origin/version notifications, current-cell keys, and adapters in both directions. Generated views accept explicit `SelectionMode` and `SelectionUnit` values when binding a shared model. Projection scans the model's active filtered/reordered source rather than using raw-source indexes. Controller and state-restore updates supersede stale queued identity restoration without disabling replacement preservation, and `PreserveUnloadedKeys` keeps off-page keys across source resets.

### F11. State persistence and migration — P1

Requirements:

- Generate `DataGridStateOptions` column/item key selectors and resolvers.
- Generate a stable schema ID, schema hash, and user-controlled state version.
- Capture/restore selected sections through a generated UI adapter or interaction, keeping DataGrid access out of the ViewModel.
- Generate JSON serialization metadata where the configured serializer supports source generation.
- Support partial migration hooks:

```csharp
static partial bool TryMigrateTradesState(
    int fromVersion,
    int toVersion,
    ref DataGridState state);
```

- Support renamed/removed/split/merged column-key maps.
- Restore columns, operations, conditional formatting, grouping, hierarchy, selection, and scroll independently.
- Report unstable keys and schema-breaking changes at compile time where possible.
- Never persist delegates, controls, templates, or arbitrary runtime objects.

ReactiveUI generated views should use an `Interaction` or generated view adapter to execute capture/restore against the actual DataGrid.

Implemented state API: keyed schemas emit stable descriptors, item/column key options, alias maps from `PreviousColumnKeys`, generated state controllers, migration delegates, serializer injection, and default source-generated JSON metadata. Generated views expose all-section and section-selective capture/restore methods. Typed interaction handlers receive the generated view and its owned DataGrid, keeping UI state access out of the ViewModel. Scroll-state capture filters non-item placeholders before invoking generated typed key selectors.

### F12. Editing, conversion, validation, and undo — P2

Requirements:

- Generate typed setters, null handling, culture-aware parsers, and formatters.
- Compile common `DataAnnotations` validation rules into direct code.
- Validate custom methods referenced with `nameof`.
- Generate per-column edit eligibility and coercion hooks.
- Generate editing interaction model/factory implementations for declared trigger profiles.
- Optional `INotifyDataErrorInfo` and ReactiveUI-compatible validation projections.
- Optional keyed edit transactions with before/after values for undo/redo services.
- Cross-field validation stays in user code through a small service/hook.
- Async validation must be cancellable, revisioned, and must not block the UI thread.

The generator should not create a general-purpose validation framework; it should adapt declared rules to existing DataGrid editing behavior.

Implemented editing API: keyed schemas emit direct typed edit fields, culture-aware parsers/formatters, compiled `Required`, string-length, minimum/maximum-length, and numeric `Range` validators, validated sync/async/coercion/eligibility hooks, a revisioned cancellable edit controller, structured results, explicit multi-cell batches, and keyed undo/redo. Effective `IsReadOnly` columns are excluded from the edit manifest even when their CLR setter is accessible.

### F13. Clipboard import/export and fill — P2

Requirements:

- Generate typed cell-to-text and text-to-cell converters per column.
- Support text, CSV, HTML, Markdown, XML, YAML, and JSON export metadata already exposed by DataGrid.
- Generate header/key maps independent of display order.
- Culture, null, quoting, formula, and error policies.
- Generate `IDataGridClipboardImportModel` and factory adapters for standard cases.
- Generate standard copy, numeric/date sequence, relative formula, and custom fill strategies.
- Rectangular selection and dynamic/indexed column-family support.
- Maximum cell count and payload size limits for import.
- Paste/fill validation should batch notifications and return structured errors.

This should replace most of the Excel sample's handwritten clipboard and fill plumbing while retaining its spreadsheet-specific policy as a custom implementation.

Implemented transfer API: generated schemas expose typed clipboard, fill, clipboard-import-model, and fill-model factories. The DataGrid adapters resolve stable `ColumnKey` values to generated edit fields, apply rectangular or one-value multi-cell paste, extrapolate numeric/date/time/duration sequences or perform cyclic copy, enforce hard cell and payload limits, publish structured keyed errors, and commit each operation as one undo batch. Generated views compile-time validate and bind the two model interfaces and configure selection, edit triggers, copy mode, read-only state, and conservative add/delete defaults.

### F14. Conditional formatting and style metadata — P2

Requirements:

- Compile simple comparisons, range, null, text, and row predicates from metadata.
- Reuse typed field accessors and typed constant conversion.
- Cell/row theme keys, foreground/background binding accessors, order, scope, and stop-if-true.
- Named rules and runtime enable/disable state.
- Custom static predicate methods for complex rules.
- Optional Power Fx rule provider integration without embedding Power Fx in the core generator.
- Resource keys remain strings because resources are resolved by Avalonia; optional resource-manifest validation may warn when a key is known to be absent.

Generated predicates must not allocate per evaluated cell.

Implemented conditional-formatting API: repeatable `[DataGridConditionalFormat]` metadata emits cached `DataGridGeneratedConditionalRule<TItem,TValue>` instances behind `IDataGridGeneratedConditionalRule`. Built-in comparisons use the canonical typed getter and compile-time-converted operand; custom rules call a validated static `bool (TItem, TValue)` method. Stable rule and column IDs, priority, `StopIfTrue`, theme key, and `ConditionalFormattingTarget.Cell` or `Row` are preserved.

Every schema exposes the typed `ConditionalRules` collection and `CreateConditionalFormattingModel()`. The runtime factory creates custom-predicate descriptors with `ConditionalFormattingValueSource.Item`, so evaluation calls the cached generated predicate and never resolves a property path. Generated views accept `ConditionalFormattingModelPropertyName`, validate the member against `IConditionalFormattingModel`, and bind it directly to `DataGrid.ConditionalFormattingModelProperty`; `PDGSG131` rejects missing or incompatible members. The formal sample validates five cell rules, two row rules, custom cross-field predicates, priorities, reactive updates, rule clearing/restoration, and rendered theme resources through Avalonia Headless.

### F15. Column bands, chooser, layout, and indexed column families — P2

Requirements:

- Generate band trees from repeatable `[DataGridBand]` metadata.
- Default display order, visibility, resize/reorder/hide permissions, width-sharing groups, and left/right frozen placement.
- Generate column chooser items and commands keyed by schema column ID.
- Support fixed property columns and runtime indexed/method-backed column families.

**Implemented indexed-column API:**

```csharp
[GenerateDataGridIndexedColumns(
    Name = "Cells",
    GetterMethod = nameof(GetCell),
    SetterMethod = nameof(SetCell),
    NotificationNameMethod = nameof(GetCellPropertyName))]
public sealed partial class SpreadsheetRow
{
    public object? GetCell(int index) => ...;
    public void SetCell(int index, object? value) => ...;
    public static string GetCellPropertyName(int index) => ...;
}
```

The generated family exposes:

```csharp
DataGridColumnDefinition CreateColumn<TValue>(
    int index,
    in DataGridGeneratedIndexedColumnOptions<TValue> options);
```

This removes the duplicated handwritten `ClrPropertyInfo`/`DataGridBindingDefinition` helper pattern while keeping runtime column count and per-slot customization. Standard kinds receive a cached typed accessor; `DataGridGeneratedIndexedColumnKind.Formula` deliberately creates a `DataGridFormulaColumnDefinition` without invoking or requiring the indexed getter. Formula options include `Formula`, `FormulaName`, `AllowCellFormulas`, sizing, read-only policy, and the common final `Configure` callback.

Generated views accept `FormulaModelPropertyName`. The resolved member must implement `IDataGridFormulaModel`; the emitter installs a direct compiled property binding on `DataGrid.FormulaModelProperty`, and `PDGSG130` rejects missing or incompatible members. The formal indexed spreadsheet sample validates 7–12 replaceable columns, typed slot notification names, strict fast-path operation, structured and chained formulas, a per-cell override, and generated ReactiveUI lifetime wiring.

For `DataTable`, dictionaries, or other truly runtime-defined shapes, the generator cannot infer a schema. It should generate only an adapter shell around a user-supplied typed/dynamic accessor provider and clearly mark that path as runtime-defined.

### F16. Templates, row details, and custom drawing — P2

Requirements:

- Typed/recycling `FuncDataTemplate<T>` generation from validated factory methods.
- Resource-key templates remain supported.
- Row-details template and visibility-policy metadata.
- Nested-grid schema references without reflection view lookup.
- Custom drawing operation factory and invalidation-source hooks.
- Generated cell cache keys for `IDataGridCellDrawOperationItemCache` implementations.
- Button/toggle command and parameter accessors with compiled bindings.
- Accessibility metadata for generated template roots.

The generator must not serialize arbitrary control trees into attributes. Common generated-view recipes and user-authored resource templates are the supported composition mechanisms.

Implemented row-details and nested-grid API:

```csharp
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
public sealed partial class BooksViewModel : ReactiveObject;
```

The generator validates the nested collection as `IEnumerable<TNested>`, validates an optional string summary property, and emits a recycling detail presenter that creates the referenced nested schema/fast-path options once. Presenter reuse updates only typed member accessors. Resource-key, accessible parameterless `IDataTemplate` implementation, and typed static factory-method sources are supported as mutually exclusive alternatives and are available on class- and assembly-level view attributes. `PDGSG123` reports invalid combinations or signatures. The built-in detail root, summary, and nested grid have stable automation IDs/names/help text. The migrated `RowDetailsSelectionPage` is a generated `ReactiveUserControl<TViewModel>` with a real selection/materialization headless test and no page XAML or attach-time code-behind.

Implemented row-action API:

```csharp
[DataGridColumn(
    DataGridColumnKind.Button,
    ContentMember = nameof(ActionLabel),
    CommandMember = nameof(ActionCommand),
    CommandParameterMember = nameof(Id))]
public string Action => Id;

[DataGridColumn(
    DataGridColumnKind.ToggleButton,
    CheckedContentMember = nameof(CheckedLabel),
    UncheckedContentMember = nameof(UncheckedLabel),
    CommandMember = nameof(ToggleCommand))]
public bool Enabled { get; set; }
```

The same direct-accessor path supports `ContentMember` for buttons/toggle buttons and `OnContentMember`/`OffContentMember` for toggle switches. Each configured role gets one cached typed `DataGridBindingDefinition` per schema. `CommandMember` must be an accessible readable `ICommand` property. A missing `CommandParameterMember` deliberately passes the row item, preserving the existing button-column fallback. `PDGSG124` rejects incompatible column kinds, static/member conflicts, missing or inaccessible members, and non-command command members. The migrated `ButtonColumnDefinitionBindingsPage` is now a generated ReactiveUI C# view backed by attributed reactive rows and has schema, command-state, and Avalonia Headless tests; its former reflection binding factory, page XAML, and attach-time code-behind were removed.

Implemented custom-drawing API:

```csharp
[GenerateDataGridCellDrawCache(InitialCapacity = 4, MaximumCapacity = 4)]
public sealed partial class QuoteRow : ReactiveObject
{
    [DataGridColumn(
        DataGridColumnKind.CustomDrawing,
        Order = 0,
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

`DrawOperationFactoryType` supports accessible parameterless factories; `DrawOperationFactoryMethod` supports static parameterless factory methods and is the customization path for configured factories. The generator rejects conflicting or incompatible factories with `PDGSG122`. Generated cache storage implements `IDataGridCellDrawOperationItemCache`, assigns deterministic slots in column order, emits whole-cache and per-slot invalidation methods, uses array-backed O(1) lookup, and refuses slots beyond `MaximumCapacity`. The existing runtime subscription to `IDataGridCellDrawOperationInvalidationSource` remains active because generated definitions assign the factory through the normal `DrawOperationFactory` property.

### F17. Drag/drop and reorder adapters — P2

Requirements:

- Flat move/copy by stable item key.
- SourceList index move and SourceCache order-key strategies.
- Hierarchical reparent/reorder with parent-key and cycle validation.
- Typed target validation and operation selection hooks.
- Command/interaction output for domain-owned mutation.
- Generated session status suitable for badges and diagnostics.
- Header-only, row, cell, or custom drag-handle policies.
- Selection-drag coordination.

The default generated adapter should request a move from a ViewModel service. It must not guess how domain collections should be mutated.

### F18. Pivot, outline, formula, and chart metadata — P3

These integrations should be capability-gated so projects that reference only the core grid do not see or compile analytics output.

Pivot requirements:

- Typed row/column/filter/value field selectors.
- Date/numeric grouping, sort, value filters, slicers, missing-item policy, and layout defaults.
- Typed calculated-measure dependencies and custom aggregate factories.
- Display modes including percent, running total, difference, parent percent, and index.

Outline requirements:

- Typed group/detail fields, subtotal fields, and expansion keys.
- Generated bindings for outline row projections.

Formula requirements:

- Stable formula names and column dependency metadata.
- Static formula syntax validation when the optional formula analyzer package is present.
- Generated typed value resolver/setter tables.
- A1/structured reference metadata and relative-reference support for fill.
- User code remains responsible for custom functions and dynamic formula text.

Chart requirements:

- Typed category/value/series selectors.
- Range-to-series projection for spreadsheet selection.
- Keyed incremental chart updates from the same source pipeline.
- Grid/chart selection and current-item synchronization.
- User-defined chart source/renderer implementations remain first-class.

Implemented pivot/chart API: `[DataGridPivotAxis]`, `[DataGridPivotValue]`, `[DataGridChartField]`, `[DataGridOutlineField]`, and `[DataGridFormulaField]` contribute roles to the canonical `AnalyticsFields` manifest. Generated `CreatePivotAxisFields` and `CreatePivotValueFields` methods globally order matches by declared order and stable column key. `CreatePivotTableModel(IEnumerable, Action<PivotTableModel>?)` installs typed row, column, filter, and value selectors with refresh suspended, invokes the application customization callback, and only then activates the model. Configuration failures dispose the partial model before propagating the exception.

Numeric chart roles additionally expose a cached selector through the additive `IDataGridGeneratedNumericAnalyticsField` contract. The optional `ProDataGrid.Charting` adapter uses this delegate directly for value, X, and size dimensions, avoiding the boxed compatibility getter and conversion path for generated numeric properties. The base analytics interface remains unchanged for custom user implementations. The formal `GeneratedPivotChartPage` combines a strict generated grid, pivot table, pivot-derived chart, and direct chart with ReactiveUI commands and deterministic headless coverage. Long-form series grouping, keyed incremental selection synchronization, spreadsheet range projection, and optional formula syntax analysis remain.

### F19. Generated view recipes and event bridges — P1/P3

Generated Avalonia and ReactiveUI views support the following recipe set:

- `GridOnly`
- `SearchableGrid`
- `OperationsToolbar`
- `Explorer`
- `Spreadsheet`
- `Analytics`
- `MasterDetail`

**Implemented API:**

```csharp
[GenerateDataGridView(
    typeof(Trade),
    ViewName = "TradeBlotterView",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.OperationsToolbar,
    ControllerName = "Trades",
    BaseType = typeof(WorkspaceViewBase<>),
    AutomationId = "trade-blotter")]
public sealed partial class TradeBlotterViewModel;
```

Requirements:

- Bind against the grouped controller property.
- Use Avalonia binding indexers/compiled paths only.
- Generate ReactiveUI activation and adapter lifetime wiring.
- Bind commands instead of emitting code-behind event handlers.
- Bridge routed grid events to generated ViewModel commands/interactions when requested.
- Support custom base types, themes, resource keys, toolbar slots, empty/loading/error states, diagnostics status, and row details.
- Emit reflection-free view registration.
- Allow multiple views per ViewModel.

The formal `GeneratedReactiveViewRecipesPage` applies four `GenerateDataGridView` declarations to one ReactiveUI ViewModel. `GridOnly`, `Explorer`, `Spreadsheet`, and `Analytics` receive independent generated classes, stable `GeneratedRecipe` constants, automation IDs, read-only/editable configuration, and named toolbar/recipe slots while sharing one strict attributed-only schema and one collection view. Its Explorer and Analytics search boxes use the same compiled two-way query binding and generated canonical-field predicates. ViewModel, generator, Avalonia Headless, and deterministic screenshot tests cover the contract without view-owned event handlers.

Implemented state-projection API:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    ViewStatePropertyName = nameof(ViewState),
    ErrorMessagePropertyName = nameof(ErrorMessage),
    RetryCommandPropertyName = nameof(RetryCommand),
    LoadingText = "Loading trades…",
    EmptyText = "No trades found.",
    ErrorText = "Trades are unavailable.",
    RetryText = "Retry")]
public sealed partial class TradeBlotterViewModel : ReactiveObject
{
    [Reactive] private DataGridGeneratedViewState _viewState;
    [Reactive] private string? _errorMessage;
    public ReactiveCommand<RxVoid, RxVoid> RetryCommand { get; }
}
```

The generated state host preserves the DataGrid instance and switches four projections with compiled bindings. State, optional error text, and retry command members are resolved and type-checked at compile time. `PDGSG125` rejects incomplete or incompatible contracts. Loading, empty, error, and retry controls have stable automation IDs and replaceable protected factory hooks. Type-, assembly-, and namespace-level view policies share the same options.

Implemented routed-event bridge API:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanged |
                   DataGridGeneratedViewEventKinds.Sorting |
                   DataGridGeneratedViewEventKinds.Editing,
    RoutedEventCommandPropertyName = nameof(GridEventCommand))]
public sealed partial class TradeBlotterViewModel : ReactiveObject
{
    public ReactiveCommand<DataGridGeneratedViewEvent<Trade>, RxVoid> GridEventCommand { get; }
}
```

The generated view emits direct subscriptions only for requested events and executes the validated `ICommand` property with `DataGridGeneratedViewEvent<TItem>`. The snapshot carries typed row/current items, stable column keys, row index, edit action, selection origin, and zero-copy added/removed item projections. Mutable `Cancel` and `Handled` feedback is copied back to the routed event. The supported event set covers selection, current cell, sorting, and the cell/row editing lifecycle. `PDGSG126` rejects zero/unknown flags, missing commands, and command members that do not implement `ICommand`. Type, assembly, and namespace policies share the contract, and derived views can extend the wiring through `ConfigureGeneratedRoutedEventCommands(DataGrid)`.

Implemented ReactiveUI interaction response API:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    InteractionPropertyNames = [nameof(ConfirmTrade)],
    InteractionHandlerTypes = [typeof(ConfirmTradeHandler)])]
public sealed partial class TradeBlotterViewModel : ReactiveObject
{
    public Interaction<Trade, bool> ConfirmTrade { get; } = new();
}

public sealed class ConfirmTradeHandler :
    IDataGridGeneratedViewInteractionHandler<Trade, bool>
{
    public ValueTask<bool> HandleAsync(
        DataGridGeneratedViewInteractionContext<Trade> context) =>
        ValueTask.FromResult(context.Input.Quantity <= 1_000);
}
```

The generator resolves the exact `IInteraction<TInput, TOutput>` type arguments and validates a matching accessible, closed, parameterless handler implementation. `PDGSG127` rejects mismatched arrays, duplicate or missing properties, non-ReactiveUI declarations, and incompatible implementations. Generated ReactiveUI views attach routed events and interactions through `WhenActivated`. Interaction registration also follows reflection-free `DataContext` changes while active; replacing or deactivating a ViewModel unregisters the previous handlers, cancels their context token, and disposes handlers implementing `IDisposable`. Protected typed handler factories let derived generated views supply DI-backed implementations without replacing lifetime wiring.

Custom frameworks cannot be loaded as arbitrary compiler plugins from user code. A new framework strategy should be delivered as a compatible generator package/strategy. User customization within an existing strategy uses base types, runtime adapters, named partial hooks, and implementation types.

### F20. Localization, accessibility, diagnostics, and test metadata — P3

Requirements:

- Header/description resource keys and direct strongly typed resource access when configured.
- Culture-aware generated format defaults.
- Stable automation IDs for grid, columns, filter editors, search, and generated toolbar commands.
- Automation names/help text from explicit metadata or localized headers.
- A generated debug manifest showing accessor coverage, operation ownership, active fallbacks, schema version, and stream metrics.
- Optional generated test-data builder metadata, but not generated business test cases.
- Headless tests must be able to locate generated controls without visual-tree reflection.

### F21. Collection views, paging, currency, and auto-column replacement — P2

Requirements:

- Generate a typed `DataGridCollectionView` factory for local enumerable/observable sources.
- Apply generated grouping, paging, and currency/current-item defaults.
- Generate add/delete/new-row policies only when a user service owns creation and mutation.
- Preserve current item and selection by stable key when pages or sources change.
- Convert runtime auto-generation into a build-time schema when the item type is statically known.
- Generate the `AutoGeneratingColumn` customization as schema metadata rather than a view event handler.
- Support interfaces and explicit-interface properties when the target member can be resolved statically.
- Detect `DataTable`, `ICustomTypeDescriptor`, dictionaries, and runtime property bags as dynamic shapes and require an explicit runtime accessor provider.
- Generate range-aware adapters for add/remove/replace/move/reset so bulk updates are not expanded into avoidable per-item work.

The generator must not pretend that an unknown runtime schema is compile-time typed. Dynamic shapes use a clearly labeled runtime provider and are excluded from strict typed accessor guarantees.

### F22. Header menus, filter editors, and distinct-value providers — P2

Requirements:

- Generate per-field filter-editor metadata from the field type and explicit overrides.
- Text, numeric, date/time, boolean, enum, range, and distinct-value editor profiles.
- Generate local distinct-value enumeration using typed accessors.
- Support async/server distinct-value providers keyed by generated field ID and current query context.
- Cancellation, debounce, result limits, and stale-response suppression for remote values.
- Generate header-menu commands for sort, clear sort, filter, clear filter, visibility, pin/freeze, autosize, and reset layout.
- Integrate generated column chooser and band metadata.
- Allow a user-provided editor factory, flyout factory, or resource key per field.
- Keep all visual styling in Avalonia resources/templates.

Distinct-value generation must be bounded and should not scan an unbounded live source on the UI thread.

Implemented command API:

```csharp
using DataGridGeneratedOperationController<Trade> operations =
    TradeGridSchema.CreateController();
using DataGridGeneratedColumnLayoutController layout =
    TradeGridSchema.CreateColumnLayoutController();
using DataGridGeneratedHeaderCommandController<Trade> headers =
    TradeGridSchema.CreateHeaderCommandController(
        operations,
        layout,
        interaction: new TradeGridHeaderInteraction());

DataGridGeneratedHeaderCommandSet price = headers.ForField("price");
price.SortDescending.Execute(null);
price.ClearFilter.Execute(null);
price.HideColumn.Execute(null);
price.PinLeft.Execute(null);
price.FreezeThrough.Execute(null);
price.AutoSize.Execute(null);
price.ResetLayout.Execute(null);
```

`IDataGridGeneratedHeaderInteraction` is the UI boundary for pin, freeze, autosize,
and any grid-instance behavior. Applications may replace that small interface or the
complete `IDataGridGeneratedHeaderCommandHandler`. Sort, filter, visibility, and
layout-reset operations use generated field IDs and existing typed controllers. No
reflection, expression compilation, or DataGrid reference is introduced in the
ViewModel.

### F23. Virtualization, scrolling, input, and diagnostics profiles — P2

Source generation cannot optimize the DataGrid renderer by itself, but it can make performance-sensitive configuration explicit and consistent.

Requirements:

- Named profiles for uniform rows, estimated variable height, measured variable height, spreadsheet, tree, and high-frequency streaming.
- Generate row-height estimator, cache-key, template-reuse, logical-scrolling, frozen-column, and virtualization configuration where the runtime API supports it.
- Validate incompatible settings such as an unbounded details template in a high-frequency profile.
- Generate keyboard gesture maps and command bridges for common navigation/edit/search/fill operations.
- Generate XY-focus and current-cell bindings for code-only view recipes.
- Generate scroll/state interactions without exposing the DataGrid instance to the ViewModel.
- Expose row realization/recycling, update queue, search index, hierarchy flatten, and generated pipeline metrics through the diagnostics manifest.
- Allow user-defined row-height estimator, cache, input-map, and diagnostics sink implementation types.

Generated profiles are presets, not hidden heuristics. Explicit user properties always win, and the active settings appear in diagnostics.

Implemented API:

- `GenerateDataGridViewAttribute.PerformanceProfile` selects the generated view preset; type-, assembly-, and namespace-level requests use the same option.
- `InputMapType` accepts a validated `IDataGridGeneratedInputMap`; `InputCommandPropertyName` resolves a typed `ICommand` target and emits `DataGridGeneratedInputEvent<TItem>` with handled feedback.
- The default map uses Avalonia's platform command modifier for search and adds fill/undo/redo commands for the spreadsheet profile. Custom maps can replace both DataGrid gesture overrides and command matching.
- `DiagnosticsSinkType` accepts a validated `IDataGridGeneratedMetricsSink`. `DataGridGeneratedMetricsBridge` forwards the process-wide ProDataGrid meter's long/double counter, up/down-counter, and histogram samples without a tag collection allocation.
- `NavigationInteractionPropertyName` validates an exact ReactiveUI `IInteraction<DataGridGeneratedNavigationRequest<TItem>, DataGridGeneratedNavigationResult<TItem>>`. The generated activation-scoped handler supports current-cell query/set, visible-column and row-offset XY movement, stable-key bring-into-view, and scroll-state capture/restore without exposing the DataGrid to the ViewModel.
- Generated diagnostics manifests list stable renderer and generated-pipeline metric names appropriate to the schema capabilities.
- ReactiveUI subscriptions are activation scoped. Plain Avalonia metric subscriptions follow visual-tree attachment. Generated subscriptions own and deterministically dispose custom sinks.
- Protected `CreateGeneratedPerformanceOptions`, `CreateGeneratedInputMap`, and `CreateGeneratedMetricsSink` factories provide DI and custom-estimator/cache escape hatches; `ConfigureGeneratedDataGrid` runs after the preset so application settings win.
- `PDGSG128` rejects unsupported profiles, invalid input maps or sinks, missing/incompatible input commands, and the provably unbounded combination of `HighFrequencyStreaming` with always-visible row details. `PDGSG127` rejects invalid navigation interaction contracts. ReactiveUI custom bases that need activation remain validated through `PDGSG013`.

The built-in meter does not currently tag samples with a DataGrid instance identity. A generated subscription therefore attaches schema/profile context for its owning view but intentionally documents that process-wide boundary rather than claiming per-grid attribution.

## 8. End-to-end proposed usages

### 8.1 ReactiveUI + DynamicData `SourceCache`

```csharp
[GenerateDataGridColumns(
    ProviderName = "TradeGridSchema",
    Strict = true,
    Streaming = true)]
public sealed class Trade
{
    [DataGridKey]
    [DataGridColumn(DataGridColumnKind.Numeric,
        Header = "ID",
        ColumnKey = "trade-id",
        IsReadOnly = true)]
    public int Id { get; init; }

    [DataGridColumn(DataGridColumnKind.Text,
        Header = "Symbol",
        ColumnKey = "trade-symbol")]
    public required string Symbol { get; init; }

    [DataGridColumn(DataGridColumnKind.Text,
        Header = "Desk",
        ColumnKey = "trade-desk")]
    public required string Desk { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric,
        Header = "Price",
        ColumnKey = "trade-price",
        FormatString = "N2")]
    [DataGridSummary(DataGridAggregateType.Average, Format = "N2")]
    public decimal Price { get; init; }
}

[GenerateDataGridController(
    typeof(Trade),
    "Trades",
    ProviderName = "TradeGridSchema",
    SourceMember = nameof(_source),
    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
    OperationExecution = DataGridOperationExecution.ExternalPipeline,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Selection |
               DataGridGeneratedFeatures.Summaries |
               DataGridGeneratedFeatures.State,
    Streaming = true)]
[GenerateDataGridView(
    typeof(Trade),
    ViewName = "TradeBlotterView",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.OperationsToolbar,
    ControllerName = "Trades")]
public sealed partial class TradeBlotterViewModel : ReactiveObject, IDisposable
{
    private readonly SourceCache<Trade, int> _source =
        new(static trade => trade.Id);

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _desk = string.Empty;

    public TradeBlotterViewModel()
    {
        InitializeTrades(TradeBlotterViewModelGenerated.CreateTrades(
            _source,
            RxSchedulers.MainThreadScheduler));

        this.WhenAnyValue(x => x.Query)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .Subscribe(Trades.Search.SetText)
            .DisposeWith(Trades.Lifetime);

        this.WhenAnyValue(x => x.Desk)
            .Subscribe(value => Trades.Filter.Set(
                TradeGridSchema.Desk.Contains(value)))
            .DisposeWith(Trades.Lifetime);
    }

    public void Dispose()
    {
        Trades.Dispose();
        _source.Dispose();
    }
}
```

Generated output owns descriptor-to-predicate/comparer translation, DynamicData subjects, operation subscriptions, `SortAndBind`, read-only output collection, adapter factories, metrics, and error propagation. The user owns source mutation and ViewModel lifetime.

### 8.2 Custom implementation and configure hook

```csharp
[GenerateDataGridController(
    typeof(AuditEntry),
    "Audit",
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.State,
    ImplementationType = typeof(AuditGridControllerFactory),
    ConfigureMethod = nameof(ConfigureAuditGrid))]
public sealed partial class AuditViewModel : ReactiveObject
{
    private static void ConfigureAuditGrid(
        ref DataGridGeneratedControllerOptions<AuditEntry> options)
    {
        options.Search.Debounce = TimeSpan.FromMilliseconds(75);
        options.State.Version = 3;
    }
}

public sealed class AuditGridControllerFactory :
    IDataGridGeneratedControllerFactory<AuditEntry>
{
    public IDataGridGeneratedController<AuditEntry> Create(
        in DataGridGeneratedControllerContext<AuditEntry> context)
    {
        return new AuditGridController(context);
    }
}
```

The generator validates the interface, constructor/factory shape, accessibility, generic item type, and nullability before emitting the call.

### 8.3 Hierarchical streaming explorer

```csharp
[GenerateDataGridColumns(ProviderName = "FileNodeSchema", Streaming = true)]
public sealed class FileNode
{
    [DataGridKey]
    public required string Path { get; init; }

    [DataGridChildren]
    public ObservableCollection<FileNode> Children { get; } = [];

    [DataGridExpanded]
    public bool IsExpanded { get; set; }

    [DataGridColumn(DataGridColumnKind.Hierarchical,
        Header = "Name",
        ColumnKey = "name")]
    public required string Name { get; init; }

    [DataGridColumn(DataGridColumnKind.Numeric,
        Header = "Size",
        ColumnKey = "size")]
    [DataGridSummary(DataGridAggregateType.Sum)]
    public long Size { get; init; }
}

[GenerateDataGridController(
    typeof(FileNode),
    "Files",
    SourceMember = nameof(Roots),
    SourceKind = DataGridGeneratedSourceKind.ObservableCollection,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Hierarchy |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Selection |
               DataGridGeneratedFeatures.State,
    Streaming = true)]
public sealed partial class FileExplorerViewModel : ReactiveObject
{
    public ObservableCollection<FileNode> Roots { get; } = [];
}
```

The generated Avalonia view binds to a typed generated node projection, so it does not require `x:CompileBindings="False"`.

### 8.4 Remote data

```csharp
[GenerateDataGridController(
    typeof(Customer),
    "Customers",
    SourceMember = nameof(_provider),
    SourceKind = DataGridGeneratedSourceKind.Remote,
    OperationExecution = DataGridOperationExecution.Remote,
    KeyMember = nameof(Customer.Id),
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Selection |
               DataGridGeneratedFeatures.State)]
public sealed partial class CustomerSearchViewModel : ReactiveObject
{
    private readonly IDataGridQueryProvider<Customer, Guid> _provider;

    public CustomerSearchViewModel(
        IDataGridQueryProvider<Customer, Guid> provider)
    {
        _provider = provider;
        InitializeCustomers(
            CustomerSearchViewModelGenerated.CreateCustomers(provider));
    }
}
```

The controller owns debounce, cancellation, page state, stale-response suppression, and UI-neutral loading/error state. The provider owns query translation and transport.

### 8.5 Assembly and namespace policy

```csharp
[assembly: GenerateDataGridColumnsForNamespace(
    "Contoso.Trading.Models",
    IncludeChildNamespaces = true,
    Strict = true,
    Streaming = true)]

[assembly: GenerateDataGridControllersForNamespace(
    "Contoso.Trading.ViewModels",
    ItemNamespace = "Contoso.Trading.Models",
    Framework = DataGridControllerFramework.ReactiveUI,
    DefaultFeatures = DataGridGeneratedFeatures.Columns |
                      DataGridGeneratedFeatures.Sorting |
                      DataGridGeneratedFeatures.Filtering |
                      DataGridGeneratedFeatures.Searching |
                      DataGridGeneratedFeatures.State)]
```

Namespace policy supplies defaults only. An explicit type/controller attribute may opt out or override any setting. Ambiguous ViewModel-to-item matching is an error; the generator does not guess from similar names.

### 8.6 Runtime indexed spreadsheet columns

```csharp
[GenerateDataGridIndexedColumns(
    Name = "Cells",
    GetterMethod = nameof(GetCell),
    SetterMethod = nameof(SetCell),
    NotificationNameMethod = nameof(GetCellPropertyName))]
public sealed partial class SpreadsheetRow : ReactiveObject
{
    private readonly object?[] _cells;

    public object? GetCell(int index) => _cells[index];

    public void SetCell(int index, object? value)
    {
        if (Equals(_cells[index], value))
        {
            return;
        }

        _cells[index] = value;
        this.RaisePropertyChanged(GetCellPropertyName(index));
    }

    public static string GetCellPropertyName(int index) =>
        ExcelColumnName.FromIndex(index);
}

DataGridColumnDefinition price = SpreadsheetRowCells.CreateColumn<decimal>(
    index: 2,
    new DataGridIndexedColumnOptions<decimal>
    {
        Header = "C",
        ColumnKey = "C",
        Kind = DataGridColumnKind.Numeric,
        FormatString = "N2"
    });
```

The generated accessor and binding path are cached per slot and use direct method calls.

## 9. Diagnostics

Proposed diagnostic range for expansion work:

| ID | Default | Condition |
|---|---|---|
| `PDGSG100` | Error | Duplicate or empty stable field/column key. |
| `PDGSG101` | Error | Invalid/missing key member or incompatible composite-key method. |
| `PDGSG102` | Error | Generated controller/member name collision. |
| `PDGSG103` | Error | Source member type does not match configured source kind. |
| `PDGSG104` | Error | Conflicting operation owners or double-application configuration. |
| `PDGSG105` | Error | Custom hook/factory signature is invalid. |
| `PDGSG106` | Error | Required optional integration assembly is not referenced. |
| `PDGSG107` | Warning | Generated pipeline has no recognized disposal/activation owner. |
| `PDGSG108` | Warning | Streaming output has no explicit scheduler or UI boundary. |
| `PDGSG109` | Error | Invalid hierarchy children/expanded/parent-key configuration. |
| `PDGSG110` | Warning | Summary cannot update incrementally and will reset/recompute. |
| `PDGSG111` | Error | Persisted state requested without stable item and column keys. |
| `PDGSG112` | Warning | Known template/theme/resource key is missing from an optional resource manifest. |
| `PDGSG113` | Error | Strict generated path would require reflection or dynamic code. |
| `PDGSG114` | Error | Remote query provider has an incompatible item/key type. |
| `PDGSG115` | Error | Generated view binding target is missing or has an incompatible type. |
| `PDGSG116` | Error | Custom implementation is inaccessible, abstract, open generic, or incompatible. |
| `PDGSG117` | Error | Duplicate controller feature declaration for the same name. |
| `PDGSG118` | Warning | Async stream uses an unbounded buffer without explicit opt-in. |
| `PDGSG119` | Error | Namespace convention produces an ambiguous ViewModel/item/view match. |
| `PDGSG120` | Warning | Hierarchical compiled-binding projection is unavailable and runtime binding would be required. |
| `PDGSG121` | Error | Generated formula metadata has invalid dependencies, names, or value-resolver configuration. |
| `PDGSG122` | Error | Custom drawing factory configuration is conflicting or incompatible. |
| `PDGSG123` | Error | Generated row-details source, nested collection, or template factory is invalid. |
| `PDGSG124` | Error | Generated button/toggle content, command, or parameter binding is invalid. |
| `PDGSG125` | Error | Generated view-state projection is incomplete or has an incompatible state, message, or retry-command member. |
| `PDGSG126` | Error | Generated routed-event bridge uses zero/unknown flags, omits its command, or targets a member that does not implement `ICommand`. |
| `PDGSG127` | Error | Generated ReactiveUI interaction metadata is incomplete, duplicated, targets an incompatible property, or names an invalid handler implementation. |
| `PDGSG128` | Error | Generated performance profile, input map, command, metrics sink, or details combination is invalid. |
| `PDGSG129` | Error | Generated clipboard-import or fill-model binding is missing or incompatible. |
| `PDGSG130` | Error | Generated formula-model binding is missing or does not implement `IDataGridFormulaModel`. |
| `PDGSG131` | Error | Generated conditional-formatting-model binding is missing or does not implement `IConditionalFormattingModel`. |

Strict mode promotes applicable fallback warnings to errors. Diagnostics should point to the smallest relevant attribute argument or member declaration and include the expected signature/type.

## 10. Performance specification

### 10.1 Generator performance

Required properties:

- No full source-text scans outside attribute candidates.
- No semantic model requests for files without relevant candidates.
- No global compilation dependency for type-scoped generation.
- No nondeterministic ordering.
- No generated timestamp or machine-specific path.
- Cached immutable semantic models with structural equality.
- Separate output nodes so view edits do not invalidate unrelated schemas.

Benchmark scenarios:

1. 1,000 annotated row types, cold generation.
2. One property edit in one row type.
3. One generated-view recipe edit.
4. One assembly/namespace policy edit.
5. 100 controllers using the same row schema.
6. Design-time compilation with incomplete code.

Performance gates should be based on tracked repository baselines and allocations, not fragile universal millisecond targets.

### 10.2 Runtime hot paths

Required properties:

- Zero reflection and zero runtime expression compilation in strict generated paths.
- No per-row allocation during sort comparisons, filter evaluation, search evaluation, key lookup, or simple summary updates after warm-up.
- No primitive boxing when a typed operator/accessor path is available.
- Descriptor compilation once per descriptor revision, not per row.
- Incremental collection changes remain incremental through the final bound collection.
- One UI scheduler hop per emitted batch.
- Bounded stream buffers and bounded key/index caches.
- Deterministic disposal releases sources, subjects, timers, adapters, and event handlers.

Benchmark suites:

- Generated accessor get/set versus current compiled binding and reflection fallback.
- Sort comparer construction and 1M comparisons.
- Filter/search predicate construction and 1M evaluations.
- SourceList and SourceCache update throughput at 10k/100k rows.
- Key/index selection resolution after add/remove/move/replace/reset.
- Hierarchical flattening and expand/collapse under range changes.
- Incremental summaries and grouping.
- Snapshot reconciliation for the market-dashboard shape.
- Indexed spreadsheet cell access/fill/paste.
- Generated view creation/activation/deactivation.

Any performance claim in documentation must include the benchmark project, runtime, configuration, data shape, median, distribution, and allocation result.

## 11. Testing specification

### 11.1 Generator unit tests

Use xUnit and Roslyn generator-driver tests for:

- Every trigger scope: property, type, ViewModel, view, namespace, and assembly.
- Every feature attribute and precedence rule.
- Exact generated source or focused structural assertions.
- Every diagnostic and recovery path.
- Deterministic hint names/output.
- Incremental step caching after unrelated and related edits.
- Incomplete syntax and design-time errors.
- Custom implementation/hook signature validation.
- Multiple controllers and views per ViewModel.
- Cross-assembly public manifest consumption.

### 11.2 Runtime unit tests

- Typed operator parity with existing sorting/filtering/search semantics.
- Null, nullable, enum, numeric, date/time, culture, and custom comparer cases.
- Operation ownership and no double application.
- Key/index cache correctness under every collection-change action.
- Stream batching, overflow, cancellation, error, and disposal.
- Remote query revision and stale-response behavior.
- Hierarchical key, path, expansion, selection, and cycle behavior.
- Incremental summary parity with full recomputation.
- State versioning and migrations.
- Clipboard/fill/edit conversion and validation.

### 11.3 Avalonia Headless tests

- Generated views for plain Avalonia and ReactiveUI.
- Compiled bindings enabled, including hierarchical projections.
- Keyboard/pointer selection, editing, clipboard, fill, drag/drop, and routed event bridges.
- Activation/deactivation and no duplicate subscriptions.
- State capture/restore through the generated view adapter.
- Automation IDs and accessible names.
- Generated row-details selection, nested-grid materialization, typed schema wiring, and presenter recycling.
- Screenshot coverage only where visual composition is part of the contract.

### 11.4 Integration and deployment tests

- DynamicData SourceList and SourceCache end-to-end tests.
- Optional integration absent/present reference tests.
- `dotnet publish -p:PublishAot=true` smoke applications.
- Trimming warnings treated as errors for generated sample projects.
- No runtime binding warnings in strict generated samples.
- Memory-leak tests for generated views/controllers after detach/dispose.

## 12. Implementation plan

### Phase 0 — generator foundation (implemented)

1. Split discovery into attribute-driven incremental pipelines.
2. Introduce equatable schema/field/controller/view semantic models.
3. Emit versioned schema manifests and preserve current outputs.
4. Add generator performance tests and incremental-caching tests.
5. Add the abstractions/runtime boundary needed for cross-assembly manifests.

Exit criteria: existing generator tests pass unchanged; one-type edits do not regenerate unrelated type outputs; deterministic-output and performance baselines exist.

### Phase 1 — identity and operation core

1. Add `[DataGridKey]` and generated key/index services.
2. Add typed operation descriptors/builders and canonical field IDs.
3. Add typed local collection-view construction and range-aware adapters.
4. Add the UI-neutral generated operation controller.
5. Add Avalonia adapters to existing sorting/filtering/search models.
6. Add generated presets, ownership diagnostics, and strict no-reflection enforcement.

Exit criteria: the sorting/filtering/searching model samples can be rewritten without manual property-path switches or model-event subscriptions.

### Phase 2 — reactive and live data

1. Add DynamicData SourceList support.
2. Add SourceCache support using generated keys.
3. Add hierarchical DynamicData support.
4. Add async-enumerable/channel ingestion and keyed snapshot reconciliation.
5. Add scheduler, backpressure, metrics, errors, cancellation, and disposal.
6. Add ReactiveUI activation and command strategy.

Exit criteria: all eight DynamicData sample ViewModels use generated pipelines; the generated trade sample no longer owns manual subjects/event handlers; streaming and disposal tests pass.

### Phase 3 — hierarchy, selection, and state

1. Generate hierarchy options and typed node projections.
2. Convert hierarchical sample views back to compiled bindings.
3. Generate selection and fast index resolution.
4. Generate state key maps, interactions/adapters, versions, and migrations.
5. Add shared grid/chart/control selection bridges.

Exit criteria: representative hierarchy, grouped selection, paging selection, selection fast-index, and full-state pages run without reflection binding or view-owned state logic.

Current status: core implementation is available. Formal hierarchy and paging/full-state samples are complete; broader grouped/shared-control selection sample conversion remains.

### Phase 4 — editing workflows

1. Generate edit/conversion/validation policies.
2. Generate clipboard import/export adapters.
3. Generate fill models and formula-relative fill hooks.
4. Generate optional undo transaction output.
5. Add indexed/method-backed column families.

Exit criteria: the Excel sample removes its generic binding helper and most selection/clipboard/fill bridge boilerplate without losing spreadsheet-specific customization.

Current status: the formal editing/clipboard/fill sample is complete with typed DataAnnotations and custom validation, async approval, coercion, eligibility, DataGrid paste/fill adapters, bounded multi-format export, and undo/redo. The formal indexed spreadsheet sample is also complete with replaceable runtime column families, formula slots, generated formula-model binding, and per-cell overrides. The broader Excel sample migration and relative-formula fill integration remain.

### Phase 5 — grouping, summaries, formatting, and layout

1. Add typed group descriptions.
2. Add generated and incremental summaries.
3. Add conditional formatting predicates.
4. Add bands, chooser, frozen placement, width groups, and layout profiles.
5. Add header menus, filter-editor metadata, and bounded distinct-value providers.
6. Add template, row-details/nested-grid, and custom-drawing factory metadata.
7. Add explicit virtualization, row-height, input, and diagnostics profiles.

Exit criteria: grouping, summary, conditional-formatting, banding, chooser, and custom-drawing samples each have generated equivalents and parity tests.

Current status: the formal grouping/summaries and conditional-formatting samples are complete. They cover rendered generated summary descriptions, incremental Add/Remove/Replace/Reset aggregates, typed comparison/custom formatting predicates, cached reflection-free runtime descriptors, cell/row targets, generated model binding, reactive updates, and runtime rule toggling. The header-filter formal sample remains, while banding, chooser/layout, templates, row details, and custom drawing already have generated APIs and focused tests.

### Phase 6 — views and application integration

1. Expand generated view recipes and event-to-command bridges.
2. Add reflection-free view/schema registries and DI registration.
3. Add localization, automation metadata, loading/error/empty states, and diagnostics panel bindings.
4. Migrate representative generated samples to current ReactiveUI source generators.

Exit criteria: generated views require no handwritten event handlers; ReactiveUI views activate/dispose correctly; another framework can be added without modifying schema discovery.

Current status: the Avalonia and ReactiveUI strategies, all seven layout recipes, multiple views per ViewModel, custom bases and protected customization hooks, compiled binding indexers, activation-scoped adapters, routed-event command/interaction bridges, view-state projections, reflection-free registrations, DI helpers, localization, accessibility, and diagnostics metadata are implemented. The formal four-recipe ReactiveUI page validates distinct layouts over one generated ViewModel and schema. Dedicated custom-implementation and assembly/namespace-policy sample pages remain, while future MVVM frameworks can be added through the existing strategy boundary without changing schema discovery.

### Phase 7 — analytics integrations

1. Add pivot and outline metadata generation.
2. Add optional formula analyzer/value resolver generation.
3. Add chart series/range/selection projections.
4. Add drag/drop generated adapters.
5. Add capability-gated integration tests and benchmarks.

Exit criteria: pivot, outline, chart, formula, and drag/drop sample families each demonstrate typed generated metadata with custom implementation escape hatches.

Current status: pivot, chart, outline, and formula roles plus drag/drop adapters are implemented. The formal pivot/chart sample covers ordered row/column/filter/value fields, generated pivot-model construction, application layout customization, pivot-derived series, direct generated numeric chart selectors, reactive source changes, and deterministic headless rendering. The indexed spreadsheet sample covers generated formula definitions and model binding. Dedicated outline and drag/drop generated sample families, long-form/keyed chart coordination, range projection, optional formula-parser integration, and capability-gated analytics benchmarks remain.

### Continuous validation lane — ProDiagnostics

1. Keep every ProDiagnostics DataGrid on generated column definitions and fast-path options.
2. Exercise flat, template, hierarchical, shared-schema, multi-schema, streaming, and layout-controller paths.
3. Keep existing XAML view activation on generated registrations rather than naming reflection.
4. Run ProDiagnostics unit and Avalonia Headless tests for every generator change.
5. Record intentional inspector reflection separately from generated application wiring and never use it as a silent grid fallback.

Exit criteria: both ProDiagnostics assemblies build for every target framework, all ProDiagnostics tests pass, registry manifests match the expected schema set, registered views instantiate without reflection, and source audits contain no inline DataGrid columns or disabled compiled-binding scopes.

## 13. New sample plan

Add focused pages rather than one overloaded showcase:

1. `GeneratedOperationsControllerPage` — implemented with a named generated controller, typed local sort/filter/search, reusable typed presets, a generated ReactiveUI grid/search view hosted by a passive compiled-binding shell, and ViewModel plus Avalonia Headless coverage.
2. `GeneratedDynamicDataSourceListPage` — implemented with a generated one-owner DynamicData pipeline, deterministic batched edits, typed upstream sorting/filtering/searching, error observation, live counters, disposal, a generated ReactiveUI grid, and ViewModel plus Avalonia Headless coverage.
3. `GeneratedDynamicDataSourceCachePage` — implemented with a generated keyed `SourceCache` pipeline, replace-aware upstream sorting, filtering and search, a generated identity selection model and ReactiveUI view binding, deterministic cache batches, error counters, idempotent disposal, and runtime plus Avalonia Headless proof that a selected replacement instance keeps the same stable key after moving rows.
4. `GeneratedHierarchicalDynamicDataPage` — implemented with a generated keyed `SourceCache` root pipeline, typed children/parent/key/expansion metadata, generated hierarchy validation and expansion restoration, upstream root sorting/filtering/searching, a generated ReactiveUI view whose `HierarchicalModel` exclusively owns the flattened wrapper source, passive compiled XAML, and ViewModel plus Avalonia Headless screenshot coverage proving `HierarchicalNode.Item` column bindings.
5. `GeneratedRemoteQueryPage` — implemented with immutable generated sort/filter/search descriptors, offset paging, bounded page-cache reuse, stable-to-backend field translation, cancellation and stale-response suppression, generated loading/error/content projection, retry, deterministic provider failures, passive compiled XAML, and ViewModel plus Avalonia Headless screenshot coverage.
6. `GeneratedSelectionStatePage` — implemented with one generated key accessor shared by the fast index, extended identity selection, filtered/reordered projection, paging and replacement preservation, generated view selection-mode/unit wiring, every-section state capture/restore, source-generated JSON, stable schema metadata, a `ticker` to `symbol` alias, version-one migration, a typed ReactiveUI interaction handler, passive compiled XAML, and runtime, ViewModel, generator, and Avalonia Headless coverage.
7. `GeneratedGroupingSummariesPage` — implemented with two reflection-free typed group levels, generated group-footer and total-summary descriptions, five shared-accessor aggregates, direct Add/Remove/Replace updates, reset fallback, a generated ReactiveUI view with summary placement, passive compiled XAML, and runtime, generator, ViewModel, rendered-value, and Avalonia Headless screenshot coverage.
8. `GeneratedEditingClipboardFillPage` — implemented with generated typed edit fields, `Required`/length/range plus custom synchronous/asynchronous validation, culture-aware span parsing, coercion and row eligibility, direct and DataGrid-context bounded paste/fill, stable-key structured errors, one-batch undo/redo, CSV/JSON/Markdown/HTML/XML/YAML export, a generated ReactiveUI spreadsheet view, passive compiled XAML, and generator, runtime, ViewModel, Avalonia Headless, and deterministic screenshot coverage.
9. `GeneratedIndexedSpreadsheetPage` — implemented with one generated method-backed family materializing a replaceable 7–12 column range, typed text/numeric slots, spreadsheet notification names, strict fast-path options, formula definitions that require no runtime property accessor, structured and chained formulas, an editable row-local formula, generated `IDataGridFormulaModel` binding, passive compiled XAML, ReactiveUI source-generated state/commands, ViewModel tests, and Avalonia Headless screenshot coverage.
10. `GeneratedConditionalFormattingPage` — implemented with five generated cell rules and two generated row rules, compile-time-converted comparisons, a typed cross-field custom predicate, stable priorities and stop behavior, direct runtime-model creation without property reflection, generated `IConditionalFormattingModel` view binding, runtime clear/restore, reactive updates, passive compiled XAML, and generator, runtime, ViewModel, rendered-theme, and Avalonia Headless screenshot coverage.
11. `GeneratedPivotChartPage` — implemented with a strict attributed-only schema, globally ordered row/column/filter/value fields, a generated and callback-configurable `PivotTableModel`, a pivot-derived chart, a direct grouped chart using cached numeric selectors, ReactiveUI source-generated state and commands, reactive period updates, metric and series-source switching, passive compiled XAML, ViewModel/runtime/generator tests, and deterministic Avalonia Headless screenshot coverage.
12. `GeneratedReactiveViewRecipesPage` — implemented with four independently emitted ReactiveUI views over one strict attributed-only schema and shared collection view: grid-only, explorer, spreadsheet, and analytics recipes; compiled shared search; stable recipe constants, automation metadata, named customization slots, editability differences, ViewModel/generator tests, and deterministic Avalonia Headless screenshot coverage.
13. `GeneratedCustomImplementationsPage` — custom factory, base view, hook, comparer, validator, and summary calculator.
14. `GeneratedAssemblyNamespacePolicyPage` — assembly/namespace discovery and explicit overrides.
15. `GeneratedHeaderFiltersPage` — typed editors, local/remote distinct values, and header commands.
16. `GeneratedVirtualizationProfilePage` — implemented with a variable-height estimate profile, custom J/K/Enter gesture overrides, typed search/fill/undo/redo command forwarding, stable-key current-cell and XY navigation, scroll-state capture/restore, an activation-scoped custom metrics sink, and Avalonia Headless lifetime plus deterministic screenshot coverage.
17. `GeneratedReactiveViewStatesPage` — ReactiveUI-owned loading, empty, error, retry, and content transitions through a generated code-only view.
18. `GeneratedReactiveEventCommandsPage` — typed selection, current-cell, sorting, and editing event snapshots with command-driven handled/cancel feedback plus activation/DataContext-scoped typed ReactiveUI interaction responses.

Each sample needs a ViewModel unit test. Interaction samples also need Avalonia Headless tests. Streaming samples need deterministic virtual-time tests and exposed metrics.

## 14. Acceptance criteria

The expansion is complete when:

- Existing generator APIs and generated source remain source-compatible.
- All new generation is incremental, deterministic, and cancellation-aware.
- Strict generated paths use no reflection or runtime expression compilation.
- DynamicData and async pipelines have explicit ownership, scheduler, backpressure, errors, and disposal.
- Stable keys are shared by selection, state, hierarchy, drag/drop, and chart coordination.
- Hierarchical generated samples use compiled bindings.
- ProDiagnostics remains a green production validation lane with generated schemas for every grid and generated view registration.
- Generated views are passive, command/interaction driven, and support custom base classes.
- ReactiveUI is the first full strategy; core schema/controller output remains framework neutral.
- Custom user implementations are compile-time validated and called directly.
- Every production path has xUnit coverage; UI interactions use Avalonia Headless.
- NativeAOT sample publication succeeds without generated-code trimming warnings.
- Benchmarks demonstrate that generated paths do not regress the existing fast path and materially reduce integration allocations/boilerplate in the audited scenarios.

## 15. Decisions to make before Phase 1

1. Whether public cross-assembly attributes ship in a new abstractions package or remain injected for same-compilation use with a separate manifest contract.
2. The exact UI-neutral descriptor/controller runtime shape and whether it uses System.Reactive abstractions.
3. Whether generated controllers are constructed explicitly, lazily, or through generated DI factories. Explicit construction is the safest initial lifetime model.
4. Whether typed group/summary accessors require additions to core DataGrid public APIs or can be adapted entirely through existing column value accessors.
5. The serialization strategy used for generated state metadata and migrations.
6. Which generated view recipes are stable enough for the first release; `GridOnly`, `SearchableGrid`, and `OperationsToolbar` should come first.
