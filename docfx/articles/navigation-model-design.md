# Navigation Model Design

Status: implementation design for the model-driven DataGrid navigation subsystem.

## Purpose

ProDataGrid already has mature keyboard movement, current-cell, selection, editing,
grouping, hierarchy, and virtualization behavior. The navigation model adds two
composable boundaries around that engine:

1. cell navigation resolves fast, synchronous movement inside the grid; and
2. route navigation resolves a stable row/column context into an asynchronous
   application route.

Applications can observe, cancel, redirect, or programmatically issue either kind of
navigation without duplicating DataGrid internals or handling view events in
code-behind.

The design preserves the current default behavior. Assigning no custom model must
produce the same movement, selection, editing, scrolling, and focus results as today.

## Research baseline

The public contract combines the strongest conventions from established grids:

- WPF DataGrid supplies the baseline arrow, Home/End, Page Up/Page Down, Enter,
  editing, and Shift-selection behavior.
- The WAI-ARIA grid pattern separates cell-to-cell navigation from interaction inside
  an editor or embedded widget, with Enter/F2 entering interaction mode and Escape
  restoring grid navigation.
- AG Grid exposes current-cell APIs and interception points that can redirect or stop
  navigation while leaving row-model and virtualization details in the grid.
- Spreadsheet-oriented grids add Ctrl/Cmd edge jumps, row-major Tab traversal,
  explicit wrapping policies, and predictable behavior for hidden/frozen columns.
- ReactiveUI models view-model-first navigation as route segments in a stack owned by
  `IScreen`/`RoutingState` and hosted by `RoutedViewHost`.
- Prism models navigation as URI requests with parameters, target regions or page
  hosts, history operations, and explicit success/cancellation/failure results.
- CommunityToolkit.Mvvm is intentionally UI-framework and router agnostic. Its
  applications inject an application-defined navigation service and expose async
  commands, so no Toolkit-specific router type can be assumed.

The resulting ProDataGrid contract is input-independent, model-driven, observable,
and compatible with Avalonia routed input and focus management.

## Architectural boundary

```text
keyboard / command / application API
                 |
                 v
       DataGridNavigationRequest
                 |
                 v
        IDataGridNavigationModel
          |       |        |
       default  redirect  cancel/stay
          |       |        |
          +-------+--------+
                  |
                  v
       existing DataGrid movement engine
                  |
          selection/edit/focus/scroll
                  |
                  v
       DataGridNavigationCompleted
```

The cell model decides policy; the grid owns mechanics. In particular, the model does not
realize rows, mutate selection collections, commit editors, or calculate scroll
offsets. This keeps virtualization and layout out of application ViewModels.

Application routing is a separate, opt-in pipeline:

```text
row/cell activation or ViewModel command
                 |
                 v
        DataGridRouteContext
      (item/key/column/origin)
                 |
                 v
       IDataGridRouteResolver
                 |
                 v
   DataGridRouteNavigationRequest
    (navigate/replace/reset/back/forward)
                 |
                 v
       IDataGridRouteNavigator
          |       |        |
     ReactiveUI  Prism  app-defined service
                 |
                 v
   DataGridRouteNavigationResult
```

The core route contract contains no ReactiveUI, Prism, CommunityToolkit, service
locator, or view type. An adapter is responsible for translating the request into its
native router and translating native completion into the common result. Route
activation is asynchronous and cancellation-aware; arrow-key navigation stays
synchronous and allocation-free.

## Public API shape

The new `Avalonia.Controls.DataGridNavigation` namespace contains:

- `DataGridNavigationCommand`: semantic operations such as Up, Down, Left, Right,
  PageUp, PageDown, RowStart, RowEnd, GridStart, GridEnd, Next, Previous, Enter,
  BeginEdit, CancelEdit, Expand, Collapse, and ExpandAll.
- `DataGridNavigationOrigin`: Keyboard, Programmatic, Command, Automation, and
  RestoredState.
- `DataGridNavigationPosition`: immutable row/column coordinates for a data cell.
- `DataGridNavigationRequest`: command, origin, current position, modifier state,
  editing state, selection mode/unit, flow direction, and proposed default target
  when one is available.
- `DataGridNavigationDecision`: use the built-in route, move to an explicit target,
  stay and consume, or leave the grid.
- `DataGridNavigationResult`: immutable decision plus optional target and editing/
  selection continuation hints.
- `DataGridNavigationChangingEventArgs`: cancelable preview with the request and
  resolved result.
- `DataGridNavigationChangedEventArgs`: completion telemetry with old/new positions,
  handled/moved status, and failure reason when a requested target is invalid.
- `IDataGridNavigationModel`: resolves requests and publishes preview/completion.
- `IDataGridNavigationQueryModel`: optional side-effect-free policy queries for
  command CanExecute evaluation.
- `IDataGridNavigationController`: optional ViewModel-to-grid request channel used by
  ReactiveCommand, RelayCommand, DelegateCommand, and plain `ICommand` without a
  control reference.
- `IDataGridNavigationModelFactory`: creates the per-grid default model.
- `DataGridNavigationModel`: extensible default implementation and the home for
  reusable boundary, wrapping, RTL, editing, and traversal policies.

The same namespace contains a distinct route family:

- `DataGridRouteContext`: item, stable item key, stable column key, grid position,
  and activation origin without a `DataGrid` or control reference.
- `DataGridRoute`: stable path, optional immutable parameter/state, and optional
  target region/outlet/screen identifier.
- `DataGridRouteNavigationKind`: Navigate, Replace, Reset, Back, and Forward.
- `DataGridRouteNavigationRequest` and `DataGridRouteNavigationResult`: immutable
  framework-neutral input and typed completion status.
- `IDataGridRouteResolver`: maps row/cell context to a route without reflection.
- `IDataGridRouteNavigator`: advertises host capabilities and executes requests with
  `ValueTask` and `CancellationToken`.
- `IDataGridRouteNavigationModel`: exposes command enablement, busy/current state,
  cancelable preview, execution, and completion telemetry.
- `DataGridRouteNavigationModel`: the default orchestration implementation.
- `DelegateDataGridRouteResolver` and `DelegateDataGridRouteNavigator`: AOT-safe
  composition seams for adapters and application-defined navigation services.

`DataGrid` adds:

- `NavigationModel` for binding or direct assignment.
- `NavigationModelFactory` and `CreateNavigationModel()` for composition roots and
  control subclasses.
- `Navigate(DataGridNavigationCommand, KeyModifiers)` as the programmatic entry point.
- `CanNavigate(...)` for command enablement without causing side effects.

The existing `KeyboardGestureOverrides`, `EnterKeyNavigationMode`, and
`ContinueEditingOnEnter` APIs remain supported. Gesture mapping determines the
semantic command; the navigation model determines policy; the existing movement
engine applies the result.

## Framework adapter contract

Adapters are deliberately thin and live outside the grid engine:

| Host | Native mapping |
| --- | --- |
| ReactiveUI | Navigate maps a route to an `IRoutableViewModel` and executes `RoutingState.Navigate`; Reset uses `NavigateAndReset`; Back uses `NavigateBack`; the adapter owns route-to-ViewModel creation and `IScreen`. |
| Prism | Path and Target map to URI/region navigation; Parameter maps to navigation parameters; Back maps to page history or a region journal; native success, cancellation, and exception values map to the typed result. |
| CommunityToolkit.Mvvm | The Toolkit ViewModel injects an application-defined `IDataGridRouteNavigator` (or wraps its existing navigation service) and invokes the model from `AsyncRelayCommand`; the Toolkit itself adds no router dependency. |
| ReactiveUI + Avalonia | The application continues hosting views in `RoutedViewHost`; the adapter is registered in the composition root and contains no view code-behind. |
| Plain MVVM / Microsoft DI | Register the application router as `IDataGridRouteNavigator`, register an AOT-safe resolver, and inject one `DataGridRouteNavigationModel` per navigation scope. |

The route `Path` is an opaque stable identifier. It may be a URI, ReactiveUI segment,
Prism registration key, or application route name. `Target` is optional because some
hosts have one stack while region/outlet routers require a destination. Parameters
remain application-owned so adapters can preserve typed ViewModel state.

Inbound deep links and browser/history changes update application ViewModel state;
ordinary bindings then restore `SelectedItem` and current cell by stable item and
column keys. This prevents the router from reaching into realized row containers and
keeps route restoration valid across sorting, filtering, paging, and virtualization.

## Required behavior matrix

The implementation and samples cover:

| Area | Required behavior |
| --- | --- |
| Basic cells | Four-way arrows, Home/End, Ctrl/Cmd edges, page movement |
| Tab | Forward/backward traversal, writable-cell skipping, boundary exit, new-row append |
| Enter | Down or next-cell modes, commit, continue editing, multiline editor ownership |
| Selection | Row/cell units, Shift extension, Ctrl/Cmd jumps, stable anchors |
| Editing | Editor-first key ownership, failed validation, commit/cancel, F2/text entry |
| Columns | Hidden, reordered, frozen-left, frozen-right, read-only, filler exclusion |
| Rows | Empty grids, collection mutation, grouping headers, collapsed groups, new-row placeholder |
| Hierarchy | Expand/collapse, parent/child movement, subtree expand, lazy rows |
| Virtualization | Unrealized targets, variable heights, viewport page size, scroll-to-current |
| Direction | LTR/RTL physical and logical horizontal movement |
| Focus | One tab stop, entry/exit, descendant editors, nested grids |
| Accessibility | Current-cell focus, automation updates, no keyboard trap |
| Programmatic | MVVM command navigation, cancellation, redirection, completion telemetry |
| State | Optional current-cell/model policy capture with stable item/column keys |
| Routes | Row/cell activation, stable paths, parameters, target regions, guards, cancellation |
| History | Navigate, replace, reset, back, forward, deep-link restoration |
| MVVM hosts | ReactiveUI, Prism page/region navigation, CommunityToolkit-style service, plain DI |

## Compatibility and rollout

1. Introduce the model types and a default pass-through implementation.
2. Route every semantic keyboard command through the model while retaining the
   current movement methods as the default executor.
3. Centralize explicit-target application and programmatic navigation.
4. Move reusable wrapping, boundary, RTL, and traversal policies into the default
   model without changing existing defaults.
5. Add state persistence only for opt-in navigation policy/current-cell sections;
   existing state payloads remain readable.
6. Add route adapters and recipes without adding framework dependencies to the core
   package or coupling cell navigation to an application router.

## Performance requirements

- No allocation is permitted on an unobserved, default arrow-key path after model
  initialization.
- Requests, positions, and results are readonly structs where practical.
- Navigation uses visible-column indexes and existing slot maps; it does not scan the
  item source or realize off-screen containers.
- Events are raised only when subscribed, and diagnostics remain opt-in.
- Programmatic navigation is synchronous and UI-thread-affine, matching DataGrid.
- Application route execution is asynchronous, cancellation-aware, and outside the
  cell-movement hot path.

## Test strategy

- Pure unit tests validate default and custom model decisions.
- Avalonia Headless tests drive real key input and verify focus, editing, selection,
  grouping, hierarchy, RTL, hidden/frozen columns, and virtualization.
- Regression tests preserve the existing navigation matrix.
- Sample ViewModel tests verify ReactiveCommand wiring and event-free views.
- Contract tests run the same route suite against delegate, ReactiveUI, Prism-shaped,
  and Toolkit-style adapters.
- State tests verify round-trip, missing keys, reordered columns, filtered rows, and
  backward-compatible payloads.

## Sample plan

The gallery receives focused pages rather than one overloaded demo:

1. Navigation Model Basics: binding, programmatic commands, and completion status.
2. Navigation Policies: contained, row-wrap, grid-wrap, and boundary exit.
3. Editing and Spreadsheet Navigation: Tab/Enter/F2/editor ownership.
4. Selection and Navigation: modifiers, anchors, row/cell units.
5. Advanced Layout Navigation: hidden/reordered/frozen columns, RTL, grouping.
6. Hierarchical Navigation: expand/collapse and lazy child traversal.
7. Custom Navigation Model: cancel/redirection rules implemented in a ViewModel-owned
   model with no code-behind handlers.
8. Navigation State: capture/restore with stable row and column keys.
9. Route Navigation: row/cell activation, parameters, cancellation, failures, and
   deep-link restoration using the framework-neutral contract.
10. ReactiveUI Routing: `IScreen`, `RoutingState`, `IRoutableViewModel`, and
    `RoutedViewHost` wiring.
11. Prism and Toolkit Recipes: URI/region and injected-service adapters with the same
    route resolver and ViewModel tests.

All pages use compiled bindings with explicit `x:DataType`; code-behind contains only
`InitializeComponent()`.
