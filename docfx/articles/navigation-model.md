# Navigation model

The navigation model separates **policy** from DataGrid mechanics. A ViewModel can
choose whether a semantic operation uses the built-in route, moves to an explicit
cell, redirects to another command, stays in the grid, or lets focus leave. The
DataGrid still owns editing, selection, focus, virtualization, hidden and frozen
columns, grouping, and scrolling.

No model assignment is required. Every DataGrid creates a
`DataGridNavigationModel` whose defaults preserve the existing keyboard behavior.

## Bind a model

Create one model per grid or per generated ViewModel instance:

```csharp
public sealed class OrdersViewModel : ReactiveObject
{
    public DataGridNavigationModel NavigationModel { get; } = new()
    {
        HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap,
        VerticalBoundaryMode = DataGridNavigationBoundaryMode.Contained,
        TabNavigationMode = DataGridTabNavigationMode.Always,
        TabBoundaryMode = DataGridNavigationBoundaryMode.Exit
    };
}
```

```xml
<DataGrid ItemsSource="{CompiledBinding Orders}"
          NavigationModel="{CompiledBinding NavigationModel}"
          SelectionUnit="Cell" />
```

Do not share one controller model between simultaneously active grids unless every
request is intentionally broadcast to all of them.

## Semantic commands

`DataGridNavigationCommand` is independent of gestures:

| Area | Commands |
| --- | --- |
| Cells | `Up`, `Down`, `Left`, `Right`, `PageUp`, `PageDown` |
| Edges | `RowStart`, `RowEnd`, `ColumnStart`, `ColumnEnd`, `GridStart`, `GridEnd` |
| Traversal | `Next`, `Previous`, `Enter` |
| Editing | `BeginEdit`, `CancelEdit` |
| Hierarchy | `Expand`, `Collapse`, `ExpandAll` |

Keyboard gestures are translated to these operations before policy runs. Existing
`KeyboardGestureOverrides`, `EnterKeyNavigationMode`, and
`ContinueEditingOnEnter` settings remain in effect.

## ViewModel commands

`DataGridNavigationModel` implements `IDataGridNavigationController`. Its request
channel is attached while it is bound to a DataGrid and detached when replaced.
This lets any MVVM command request movement without receiving the control:

```csharp
NextCommand = ReactiveCommand.Create(() =>
    NavigationModel.RequestNavigate(DataGridNavigationCommand.Next));

ExtendDownCommand = ReactiveCommand.Create(() =>
    NavigationModel.RequestNavigate(
        DataGridNavigationCommand.Down,
        KeyModifiers.Shift));
```

The same call works inside Prism `DelegateCommand`, CommunityToolkit
`RelayCommand`, or a plain `ICommand`. `RequestNavigate` returns `false` when no
grid is currently bound or the grid cannot handle the request.

For control-side integration, use `DataGrid.Navigate` and `DataGrid.CanNavigate`.
`CanNavigate` calls `IDataGridNavigationQueryModel.Query`, which is deliberately
side-effect free and does not raise preview events.

## Decisions and events

The model receives an immutable `DataGridNavigationRequest` containing the current
and proposed positions, modifiers, edit and selection state, flow direction, and
visible boundaries. It returns one of these decisions:

- `UseDefault`: run the mature built-in movement engine;
- `MoveTo`: apply an explicit row/display-column target;
- `RedirectTo`: execute another semantic command through the built-in engine;
- `Stay`: consume the operation without moving;
- `Cancel`: stay with a canceled failure reason;
- `LeaveGrid`: leave the operation unhandled so normal focus traversal can continue.

`NavigationChanging` is a cancelable preview. `NavigationChanged` reports old and
new positions, handled/moved flags, and a typed failure reason such as no rows, no
columns, invalid target, boundary, or cancellation.

## Custom policy

Override only `ResolveCore`; never calculate layout offsets or realize rows in a
model:

```csharp
public sealed class GuardedNavigationModel : DataGridNavigationModel
{
    protected override DataGridNavigationResult ResolveCore(
        DataGridNavigationRequest request)
    {
        if (request.Command == DataGridNavigationCommand.Down &&
            request.CurrentPosition.RowIndex == 0)
        {
            return DataGridNavigationResult.MoveTo(
                new DataGridNavigationPosition(
                    rowIndex: 2,
                    request.CurrentPosition.ColumnDisplayIndex));
        }

        return base.ResolveCore(request);
    }
}
```

Explicit targets use row indexes in the active view and **display** column indexes.
The grid rejects hidden, filler, group, or out-of-range targets and reports
`InvalidTarget` without partially changing selection.

## Boundary, Tab, and RTL policy

Horizontal, vertical, and Tab boundaries independently support `Contained`, `Wrap`,
or `Exit`. `TabNavigationMode.EditingOnly` preserves the traditional editor-only Tab
behavior; `Always` provides spreadsheet traversal outside edit mode.

`HorizontalNavigationMode.Physical` treats Left and Right as physical directions.
`Logical` reverses them for an RTL grid. Reordering, hidden columns, frozen-left and
frozen-right columns, and filler exclusion continue through the existing column
navigation engine.

With a spatial layout, a direction advertised by `IDataGridLayoutNavigation` wraps in
layout space at a boundary: Left to layout line end, Right to layout line start, Up to
the last layout item, and Down to the first. Directions not advertised by the layout
retain classic DataGrid cell-axis wrapping. Logical RTL redirection re-resolves the
layout target without applying the boundary policy twice.

## Editing, selection, and hierarchy

The model never commits an editor itself. `Enter`, `Next`, `Previous`, `BeginEdit`,
and `CancelEdit` delegate mechanics to the existing edit pipeline, including failed
validation and editor-first key ownership. Shift-modified commands use the existing
selection anchor and transactional selection behavior.

`Expand` and `Collapse` affect the current hierarchy node or row group only.
`ExpandAll` expands its current subtree. The same commands work from keyboard,
automation, or ViewModel commands.

## State persistence

`IDataGridNavigationStateModel` captures the model policy as a detached
`DataGridNavigationPolicyState`:

```csharp
DataGridNavigationPolicyState policy = NavigationModel.CaptureState();
NavigationModel.RestoreState(policy);
```

Current-cell identity is intentionally not duplicated in that object. It already
lives in `DataGridSelectionState`, which supports stable item and column keys:

```csharp
var options = new DataGridStateOptions
{
    ItemKeySelector = item => ((Order)item).Id,
    ItemKeyResolver = key => repository.Find((int)key),
    ColumnKeySelector = column => column.ColumnKey
};

DataGridSelectionState current = grid.CaptureSelectionState(options);
grid.RestoreSelectionState(current, options);
```

Generated applications should prefer the generated state controller and typed
interaction described in
[selection, navigation, and state](source-generators/selection-navigation-state.md).

## Focus, accessibility, and performance

- Keep one grid tab stop; do not make every realized cell an independent tab stop.
- Return `LeaveGrid` at an exit boundary to avoid a keyboard trap.
- Let editors own text-entry and embedded-widget keys until Enter/F2/Escape returns
  control to the grid.
- Keep `ResolveCore` synchronous and allocation-light. Application routes belong in
  the separate asynchronous [route navigation](route-navigation.md) pipeline.
- Use stable automation IDs on the grid and sample commands, and test keyboard-only
  entry, exit, edit, and hierarchy flows with Avalonia Headless.

These practices follow the WAI-ARIA grid distinction between grid navigation and
interaction inside a cell while preserving Avalonia focus and routed-input rules.

## Samples

The gallery includes focused pages for basics, boundary and RTL policy, editing and
selection, frozen/reordered/hidden columns, custom policy, hierarchy, state,
framework-neutral routes, ReactiveUI routing, an executable MVVM framework wiring
matrix, and source-generated navigation.

## Migrating existing grids

The navigation model is additive. A grid with no explicit `NavigationModel` keeps
its existing gesture, edit, selection, focus, and boundary behavior. Adopt it in
small steps:

1. bind a default `DataGridNavigationModel` and verify behavior is unchanged;
2. replace view event handlers with `RequestNavigate` calls from ViewModel commands;
3. move boundary and Tab choices into model properties;
4. override `ResolveCore` only for application-specific policy;
5. add a separate `RouteNavigationModel` when row activation must change screens.

`NavigationInteractionPropertyName` remains the source generator's legacy
selection/current-cell callback. It can coexist with the new cell policy property
and application route property during migration.

See also:

- [Navigation model design](navigation-model-design.md)
- [Route navigation and MVVM frameworks](route-navigation.md)
- [Source-generated navigation](source-generators/selection-navigation-state.md#navigation-model-generation)
- [WAI-ARIA grid pattern](https://www.w3.org/WAI/ARIA/apg/patterns/grid/)
- [WPF DataGrid keyboard behavior](https://learn.microsoft.com/dotnet/desktop/wpf/controls/default-keyboard-and-mouse-behavior-in-the-datagrid-control)
