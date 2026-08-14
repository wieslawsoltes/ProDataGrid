# Selection, navigation, and state

Generated selection and state APIs use stable item and column keys. They preserve identity across sorting, filtering, grouping, paging, collection replacement, and persisted-state migrations without retaining unloaded row objects.

## Prerequisite: stable identity

Mark a key or configure a key selector:

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

    [DataGridColumn(
        ColumnKey = "symbol",
        PreviousColumnKeys = ["ticker"])]
    public string Symbol { get; set; } = string.Empty;
}
```

The same selector drives the item index, DynamicData cache, snapshot reconciliation, identity selection, state, drag/drop, and chart synchronization.

## Selection controller

A keyed provider emits:

- `CreateSelectionController`;
- `CreateIdentitySelectionModel`;
- `CreateStateDescriptor`;
- `CreateStateOptions`;
- `CreateStateController`.

`DataGridGeneratedSelectionController<TItem,TKey>` stores row, column, cell, anchor, and current-cell selection by key. `PreserveUnloadedKeys` retains identity across paging and remote windows.

Its `IdentitySelectionModel` projection resolves indexes against the current filtered/sorted view rather than a stale raw-source position. Controller-driven selection and state restoration supersede older identity restores queued by resets.

## Bind selection in a generated view

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    SelectionModelPropertyName = nameof(SelectionModel),
    SelectionMode = DataGridSelectionMode.Extended,
    SelectionUnit = DataGridSelectionUnit.FullRow)]
public sealed partial class TradesViewModel : ReactiveObject
{
    public IdentitySelectionModel SelectionModel { get; }
}
```

The generated binding is compiled and validated. The ViewModel owns only the UI-neutral selection state/model.

## Current item and paging

`CreateCollectionViewController` maintains a sticky current-item key across:

- `Refresh`;
- `SetPageSize` and page changes;
- `ReplaceView` with new item instances;
- capture and restore.

`PreserveCurrentItemByKey` and `PreserveSelectionByKey` are independent schema options.

## State envelopes

`DataGridGeneratedStateController` captures configured `DataGridStateSections` into an envelope containing:

- stable schema ID;
- deterministic schema hash;
- state version;
- column layout, sorting, filtering, searching, grouping, selection, hierarchy, and scroll data selected by the options.

Restore validates the envelope before mutating the grid. It applies `PreviousColumnKeys`, then invokes an optional migration delegate. The default serializer uses generated JSON metadata.

```csharp
DataGridGeneratedStateController state =
    TradeSchema.CreateStateController(
        DataGridStateSections.Columns |
        DataGridStateSections.Sorting |
        DataGridStateSections.Filtering |
        DataGridStateSections.Selection |
        DataGridStateSections.Scroll);
```

Increment `StateVersion` when a persisted contract needs migration. Change `SchemaId` only when the state belongs to a different logical schema.

## Generated view state interaction

Bind an application-owned state controller and a typed ReactiveUI interaction:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    StateControllerPropertyName = nameof(StateController),
    InteractionPropertyNames = [nameof(ManageGridState)],
    InteractionHandlerTypes = [typeof(TradeStateInteractionHandler)])]
public sealed partial class TradesViewModel : ReactiveObject
{
    public DataGridGeneratedStateController StateController { get; }
    public Interaction<GridStateRequest, GridStateResult> ManageGridState { get; } = new();
}
```

Generated ReactiveUI views expose capture/restore helpers to their handler while active. The ViewModel never receives the `DataGrid` instance.

## Typed current-cell navigation

A ReactiveUI generated view can own a dedicated navigation interaction:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    NavigationInteractionPropertyName = nameof(GridNavigation))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public Interaction<
        DataGridGeneratedNavigationRequest<Trade>,
        DataGridGeneratedNavigationResult<Trade>> GridNavigation { get; } = new();
}
```

Requests support:

- query or set current cell;
- bring a stable-key item into view;
- move by visible row/column offsets;
- capture or restore scroll state.

```csharp
DataGridGeneratedNavigationResult<Trade> result = await GridNavigation
    .Handle(DataGridGeneratedNavigationRequest<Trade>.SetCurrentCell(
        selectedTrade,
        "trade-price"))
    .ToTask();
```

Results return a status enum, typed item, row index, display-column index, stable column key, and optional scroll state. The built-in handler is active only while the view is activated and reconnects when `DataContext` changes.

Override `CreateGeneratedNavigationInteractionHandler` for application navigation policy or DI-backed construction.

## Navigation model generation

Opt into per-ViewModel cell and input navigation models and ask the generated view
to bind cell, input, and application-route models:

```csharp
[GenerateDataGridViewModel(
    typeof(Trade),
    ProviderName = "TradeSchema",
    GenerateNavigationModel = true,
    NavigationModelPropertyName = nameof(NavigationModel),
    GenerateNavigationInputModel = true,
    NavigationInputModelPropertyName = nameof(NavigationInputModel),
    GenerateRouteContextFactory = true,
    RouteContextFactoryPropertyName = nameof(RouteContextFactory))]
[GenerateDataGridView(
    typeof(Trade),
    NavigationModelPropertyName = nameof(NavigationModel),
    NavigationInputModelPropertyName = nameof(NavigationInputModel),
    RouteContextFactoryPropertyName = nameof(RouteContextFactory),
    RouteNavigationModelPropertyName = nameof(RouteNavigationModel))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public IDataGridRouteNavigationModel RouteNavigationModel { get; }

    public TradesViewModel()
    {
        NavigationInputModel.SetBindings(
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.J,
                DataGridNavigationInputResult.Navigate(
                    DataGridNavigationCommand.Down)));
    }
}
```

The generator emits:

- `TradeSchema.CreateNavigationModel()`;
- `TradeSchema.CreateNavigationInputModel()`;
- `TradeSchema.CreateRouteContextFactory()` using the typed `[DataGridKey]` accessor;
- `TradeSchema.CreateRouteNavigationModel(resolver, navigator)`;
- the `NavigationModel` property when `GenerateNavigationModel = true`;
- the `NavigationInputModel` property when
  `GenerateNavigationInputModel = true`;
- the `RouteContextFactory` property when
  `GenerateRouteContextFactory = true`;
- reflection-free direct bindings to `DataGrid.NavigationModel`,
  `DataGrid.NavigationInputModel`, `DataGrid.RouteContextFactory`, and
  `DataGrid.RouteNavigationModel`;
- `PDGSG141` when a configured member does not implement the required interface.

`GenerateDataGridViewModelsForNamespace` and
`GenerateDataGridViewsForNamespace` expose the same input-model and route-context
options for namespace policy. Manual ViewModel properties are accepted when they
implement `IDataGridNavigationInputModel` or `IDataGridRouteContextFactory`.

The route property stays application-owned because its resolver, native navigator,
scope, and history lifetime belong in the composition root. The schema factory
constructs the framework-neutral orchestration model after those dependencies are
selected.

Generation is opt-in so existing ViewModels and snapshots do not gain an unexpected
controller or change behavior. Use unique property names when one ViewModel declares
multiple generated grids.

`NavigationInteractionPropertyName` remains the activated-view bridge documented
above. It can coexist with the new model properties and should be retained when the
ViewModel needs item-key current-cell lookup, bring-into-view, or scroll snapshots.

The `GeneratedNavigationPage` sample exercises generated cell/input models, the
route/context factories, and all four generated bindings as a real application consumer. Its
ViewModel configures logical and physical keys, dynamic resolution, pointer target
navigation, click/Enter route activation, stable-key context, and wheel-driven route
history.

## Transactional selection events

Generated view event bridges can forward the pre-commit `SelectionChanging` event:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanging,
    RoutedEventCommandPropertyName = nameof(GridEventCommand))]
public sealed partial class TradesViewModel
{
    public ICommand GridEventCommand { get; }
}
```

`DataGridGeneratedViewEvent<TItem>` carries proposed rows/cells/columns, proposed current item/cell/anchor, origin, and selection guarantee. Set `Cancel = true` synchronously to reject an `AtomicPreflight` proposal before selection, currency, focus, or scrolling changes.

For all routed event kinds, see [generated views](generated-views.md#typed-grid-event-bridges).

## Scroll-state safety

Generated scroll sampling excludes the new-item placeholder before invoking the typed item key selector. High-frequency navigation returns status values rather than throwing for ordinary unavailable/unrealized targets.

## Samples

- `GeneratedSelectionStatePage`: paging, replacement, aliases, version migration, and full state restoration.
- `GeneratedGroupedSharedSelectionPage`: one identity model shared by a grouped DataGrid and ListBox.
- `PagingSelectionPage`: page/currency defaults and off-page key preservation.
- `GeneratedVirtualizationInputMetricsPage`: current-cell, XY navigation, and scroll interactions.
- `GeneratedNavigationPage`: generated cell model, route factory, compiled view bindings, and ViewModel commands.
