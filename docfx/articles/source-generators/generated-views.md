# Generated views

`[GenerateDataGridView]` emits a code-only Avalonia control tree with compiled binding indexers. Source generators cannot add XAML to Avalonia's XAML compilation pipeline, so generated views use direct C# construction while preserving the same MVVM boundary: the ViewModel owns state and commands; the view owns controls and activation.

## Generate an Avalonia view

```csharp
[GenerateDataGridViewModel(typeof(Trade), ProviderName = "TradeSchema")]
[GenerateDataGridView(
    typeof(Trade),
    ViewName = "TradesView",
    ViewNamespace = "MyApp.Views",
    Title = "Trades",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query))]
public sealed partial class TradesViewModel
{
    // Items and model properties remain ordinary ViewModel state.
}
```

The emitted view binds item source, generated definitions, fast-path options, and configured models with generated `ClrPropertyInfo` delegates. It does not use string-path reflection bindings.

The parameterless constructor supports XAML hosts, DI, and view locators. A second constructor accepts the typed ViewModel.

## ReactiveUI strategy

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    SearchTextPropertyName = nameof(Query))]
public sealed partial class TradesViewModel : ReactiveObject { }
```

The generated type derives from `ReactiveUserControl<TViewModel>` and uses `WhenActivated` for subscriptions and handlers that require activation.

The generator recognizes properties produced from ReactiveUI.SourceGenerators `[Reactive]` fields by inspecting the declared field. This is necessary because Roslyn generators cannot consume another generator's emitted output in the same run.

Reference `ReactiveUI.Avalonia` and initialize current ReactiveUI with `UseReactiveUI` during Avalonia startup.

## View recipes

`Recipe` selects a stable layout contract:

| Recipe | Layout contract |
| --- | --- |
| `GridOnly` | Title and grid. |
| `SearchableGrid` | Compact title/search/grid layout. |
| `OperationsToolbar` | Adds `GeneratedToolbarSlot`. |
| `Explorer` | Adds toolbar and `GeneratedExplorerSlot`. |
| `Spreadsheet` | Adds toolbar and `GeneratedFormulaBarSlot`. |
| `Analytics` | Adds toolbar and `GeneratedAnalyticsSlot`. |
| `MasterDetail` | Adds toolbar and `GeneratedDetailsSlot`. |

Search is emitted only when `SearchTextPropertyName` is supplied and is independent of recipe choice. The view exposes `GeneratedRecipe` and stable automation IDs for its slots.

Override `CreateGeneratedToolbar` or `CreateGeneratedRecipeContent` to fill recipe slots.

## Multiple views for one ViewModel

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
public sealed partial class WorkItemsViewModel : ReactiveObject { }
```

Each view has independent framework, recipe, editability, title, automation, event, and interaction configuration while sharing one generated schema/ViewModel projection.

## Model bindings

Generated views can bind these typed ViewModel members:

- items, column definitions, and fast-path options;
- sorting, filtering, searching, and two-way search text;
- identity selection;
- clipboard import, fill, formula, and conditional formatting models;
- hierarchical model and hierarchy-aware filter policy;
- generated state controller;
- loading/empty/error state and retry command;
- diagnostics status.

Every named member is compile-time validated. Missing or incompatible members produce focused diagnostics; generated code does not add a runtime binding fallback.

## Loading, empty, and error states

```csharp
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

    public ReactiveCommand<Unit, Unit> RetryCommand { get; }
}
```

The state property must be `DataGridGeneratedViewState`; the error is an optional string and retry implements `ICommand`. `PDGSG125` reports an invalid state projection.

The view keeps the DataGrid alive while hiding it, preserving loaded rows, columns, selection, and scroll position. Override:

- `CreateGeneratedViewStateHost`;
- `CreateGeneratedLoadingContent`;
- `CreateGeneratedEmptyContent`;
- `CreateGeneratedErrorContent`.

## Typed grid event bridges

Forward selected events to one compile-time validated command:

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    RoutedEvents = DataGridGeneratedViewEventKinds.SelectionChanging |
                   DataGridGeneratedViewEventKinds.SelectionChanged |
                   DataGridGeneratedViewEventKinds.Sorting |
                   DataGridGeneratedViewEventKinds.Editing |
                   DataGridGeneratedViewEventKinds.CellLifecycle,
    RoutedEventCommandPropertyName = nameof(GridEventCommand))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public ReactiveCommand<DataGridGeneratedViewEvent<Trade>, Unit>
        GridEventCommand { get; }
}
```

Supported flags:

- `SelectionChanging` and `SelectionChanged`;
- `CurrentCellChanged`;
- `Sorting`;
- `BeginningEdit`, `CellEditEnding`, `CellEditEnded`, `RowEditEnding`, `RowEditEnded`;
- `CellPrepared`, `CellClearing`, `CellValueChanged`;
- combinations `Editing`, `CellLifecycle`, and `All`.

`DataGridGeneratedViewEvent<TItem>` exposes:

- typed current, row, and proposed items;
- stable current/proposed column keys and row indexes;
- edit action and trigger event;
- selection source/guarantee and zero-copy selected/proposed row, cell, and column projections;
- proposed current cell and anchor;
- hierarchy node/path;
- realized cell, row, and row data context;
- old/new value and cell-value change origin.

For `SelectionChanging` and edit-ending events, set `Cancel` synchronously. Set `Handled` for supported routed triggers. The generated handler copies feedback back to the original event before ProDataGrid continues.

`CellValueChanged` reports successful DataGrid editor commits; it is not a general property-change observer. `CellPrepared` and `CellClearing` are suitable for bounded container-scoped services.

ReactiveUI subscriptions attach during activation and detach on deactivation. Plain Avalonia views own subscriptions for the view lifetime. Invalid event flags or command types report `PDGSG126`.

## Typed ReactiveUI interactions

```csharp
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

Property and handler arrays are parallel. Every property must implement the exact `IInteraction<TInput,TOutput>` contract, and its handler must implement `IDataGridGeneratedViewInteractionHandler<TInput,TOutput>` with matching type arguments.

The generated view observes `DataContext` only while active. Replacement unregisters/disposes old adapters, cancels their context token, and registers the new set. Disposable handlers are disposed. `PDGSG127` reports mismatches.

Each interaction has a protected `CreateGeneratedInteractionHandlerN` factory for DI-backed construction.

## Navigation interaction

`NavigationInteractionPropertyName` binds the exact interaction:

```csharp
Interaction<
    DataGridGeneratedNavigationRequest<Trade>,
    DataGridGeneratedNavigationResult<Trade>>
```

The built-in handler supports current-cell query/set, bring-into-view by stable item, XY movement, and scroll capture/restore. See [selection, navigation, and state](selection-navigation-state.md#typed-current-cell-navigation).

This legacy generated-view bridge is intentionally distinct from
`NavigationModelPropertyName` and `RouteNavigationModelPropertyName`:

- the interaction asynchronously reaches an activated view to perform precise
  current-cell and scroll operations by typed item/key;
- the cell navigation model synchronously decides policy for keyboard, command, and
  programmatic operations and exposes a ViewModel-to-grid controller;
- the route navigation model asynchronously leaves the grid workflow and invokes an
  application router.

They may be configured together. The generated names, subscriptions, and lifetimes
do not overlap.

## Input maps and command feedback

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    InputMapType = typeof(TradeGridInputMap),
    InputCommandPropertyName = nameof(GridInputCommand))]
public sealed partial class TradesViewModel : ReactiveObject
{
    public ReactiveCommand<DataGridGeneratedInputEvent<Trade>, Unit>
        GridInputCommand { get; }
}
```

The map implements `IDataGridGeneratedInputMap`. It can replace keyboard gesture overrides and map command-oriented input through allocation-free `TryMatch`. The generated event contains typed selected item, row/display column, physical key/modifiers, action, and mutable handled feedback.

The platform command modifier comes from Avalonia with a Control fallback. The default map exposes command+F; the spreadsheet profile adds fill-down, fill-right, undo, and redo.

Override `CreateGeneratedInputMap` for DI construction. Invalid map/command/profile combinations report `PDGSG128`.

## Themes, classes, and diagnostics status

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Recipe = DataGridViewRecipe.Analytics,
    DiagnosticsStatusPropertyName = nameof(Status),
    ViewThemeKey = "TradeAnalyticsViewTheme",
    DataGridThemeKey = "TradeAnalyticsGridTheme",
    ToolbarThemeKey = "TradeAnalyticsToolbarTheme",
    RecipeContentThemeKey = "TradeAnalyticsContentTheme",
    ViewClasses = ["workspace-view", "analytics"],
    DataGridClasses = ["trade-grid", "dense"],
    ToolbarClasses = ["trade-toolbar"],
    RecipeContentClasses = ["analytics-content"])]
public sealed partial class TradeAnalyticsViewModel
{
    public string Status => "Streaming with generated accessors";
}
```

Theme keys are Avalonia dynamic resources, preserving theme variants and runtime resource changes. Classes are direct `Classes` additions. The status member must be a readable string and uses a compiled binding.

Empty keys, invalid/duplicate class tokens, or incompatible status members report `PDGSG139`. Generated views expose selected resource keys as constants for diagnostics/tests.

## Automation metadata

`AutomationId` establishes deterministic IDs for the view, grid, title, search, recipe slots, state surfaces, and built-in nested details. The grid receives accessible name/help text; the title is a level-one automation heading.

Column-level automation ID/name/help metadata comes from `DataGridColumnAttribute` and is reused by generated definitions.

## Hierarchical views

`HierarchicalModelPropertyName` binds a typed hierarchy and omits the ordinary root item binding. When a filtering model is also named, `HierarchyFilterPolicy` installs a generated hierarchy filter factory before filtering activates.

See [hierarchical data](hierarchy.md) for model/adapter factories and asynchronous expansion.

## Row details

Generated views support resource, `IDataTemplate` implementation, static recycling factory, or typed nested-grid details. See [layout, templates, and rendering](layout-templates-rendering.md#row-details-and-nested-grids).

## Custom base classes

```csharp
[GenerateDataGridView(
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI,
    BaseType = typeof(MyGridViewBase))]
public sealed partial class TradesViewModel : ReactiveObject { }
```

The base must be accessible, non-sealed, derive from the required Avalonia/ReactiveUI view type, and have an accessible parameterless constructor. A ReactiveUI custom base used by activation-scoped features must implement `IActivatableView`. Invalid bases report `PDGSG013`.

Generated views are inheritable and expose protected virtual hooks including:

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

Additional feature-specific factories cover editing, hierarchy filtering, interactions, navigation, input maps, performance options, and metric sinks.

Subclass the generated view to replace layout or grid presentation while retaining generated compiled bindings. Keep business logic in the ViewModel/services.

## Assembly and namespace view generation

```csharp
[assembly: GenerateDataGridView(
    typeof(TradesViewModel),
    typeof(Trade),
    Framework = DataGridViewFramework.ReactiveUI)]

[assembly: GenerateDataGridViewsForNamespace(
    "MyApp.ViewModels",
    Framework = DataGridViewFramework.ReactiveUI)]
```

Namespace generation infers item types from `ItemsPropertyName`. Explicit type requests can override framework, recipe, binding members, and presentation settings.

## Framework extension point

Avalonia and ReactiveUI are separate internal emission strategies over the same view model. A future MVVM framework can add a strategy without changing schema/column discovery or controller APIs.

## Related articles

- [Registries and customization](registries-and-customization.md)
- [Accessibility, diagnostics, and validation](diagnostics-performance-testing.md)
- [ReactiveUI setup and MVVM patterns](../getting-started.md)
