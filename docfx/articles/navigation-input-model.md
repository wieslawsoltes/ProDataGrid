# Navigation input model

The navigation input model lets a ViewModel translate key, pointer, and wheel input
into semantic cell or application-route navigation. It removes the need for
code-behind handlers, behaviors, custom templates, or UI-framework event objects in
the ViewModel.

The responsibilities stay deliberately separate:

```text
Avalonia routed input
        |
        v
DataGrid normalization
        |
        v
IDataGridNavigationInputModel       ViewModel input policy
        |
        +---- cell command --------> IDataGridNavigationModel
        |
        +---- route operation -----> IDataGridRouteNavigationModel
        |
        v
DataGrid edit / focus / selection / layout / scroll mechanics
```

The input model chooses **intent**. The cell navigation model chooses cell policy.
The route model chooses application navigation. The grid remains the only layer
that handles routed events, hit testing, editing, focus, selection, virtualization,
and spatial layout geometry.

## Create and bind a model

Configure the model in the ViewModel and bind it like every other DataGrid model:

```csharp
public sealed class OrdersViewModel : ReactiveObject
{
    public OrdersViewModel()
    {
        NavigationInputModel.SetBindings(
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.J,
                DataGridNavigationInputResult.Navigate(
                    DataGridNavigationCommand.Down)),
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.K,
                DataGridNavigationInputResult.Navigate(
                    DataGridNavigationCommand.Up)));
    }

    public DataGridNavigationInputModel NavigationInputModel { get; } = new();
}
```

```xml
<DataGrid ItemsSource="{CompiledBinding Orders}"
          NavigationInputModel="{CompiledBinding NavigationInputModel}" />
```

Bindings are evaluated in declaration order and the first match wins. Replacing the
table with `SetBindings` is explicit and does not scan commands, methods, attributes,
or assemblies at runtime.

## Normalized input contract

`DataGridNavigationInputRequest` is immutable and contains no Avalonia event args,
control, visual, or routed-event reference. It describes:

- event kind: key down/up, pointer pressed/released, or pointer wheel;
- logical key and layout-independent physical key;
- keyboard, gamepad, remote, or unknown key device;
- Shift, Control, Alt, and Meta modifiers;
- mouse, pen, touch, button, click count, and wheel direction/deltas;
- pointer coordinates relative to the grid;
- semantic hit target: grid, cell, row, row header, column header, group header, or
  empty area;
- target and current cell positions; and
- whether a cell editor is active.

This contract is suitable for a plain ViewModel unit test. Avalonia-specific
normalization is confined to `DataGrid`.

## Logical and physical keys

Use a logical binding when the shortcut follows the user's active keyboard layout:

```csharp
DataGridNavigationInputBinding.KeyDown(
    DataGridNavigationInputKey.G,
    DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.GridStart))
```

Use a physical binding when the shortcut represents a stable key position, such as
a game control or Vim-style cluster that must not move when the keyboard layout
changes:

```csharp
DataGridNavigationInputBinding.PhysicalKeyDown(
    DataGridNavigationInputKey.H,
    DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Left))
```

Bindings can require modifiers. By default, extra modifiers are allowed; set
`exactModifiers: true` when the shortcut must match exactly:

```csharp
DataGridNavigationInputBinding.KeyDown(
    DataGridNavigationInputKey.Enter,
    DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.GridEnd),
    DataGridNavigationInputModifiers.Control |
        DataGridNavigationInputModifiers.Shift,
    exactModifiers: true)
```

## Pointer and wheel navigation

A pointer binding matches normalized button, click count, modifiers, and semantic
target. `NavigateToTarget` activates the row or cell already resolved by the grid;
the ViewModel never searches the visual tree:

```csharp
DataGridNavigationInputBinding.Pointer(
    DataGridNavigationInputKind.PointerPressed,
    DataGridNavigationPointerButton.Primary,
    DataGridNavigationInputResult.NavigateToTarget(),
    clickCount: 1,
    targetKind: DataGridNavigationInputTargetKind.Cell)
```

Wheel input can issue cell commands or application history operations:

```csharp
NavigationInputModel.SetBindings(
    DataGridNavigationInputBinding.Wheel(
        DataGridNavigationWheelDirection.Up,
        DataGridNavigationInputResult.NavigateRoute(
            DataGridRouteNavigationKind.Back),
        DataGridNavigationInputModifiers.Control),
    DataGridNavigationInputBinding.Wheel(
        DataGridNavigationWheelDirection.Down,
        DataGridNavigationInputResult.NavigateRoute(
            DataGridRouteNavigationKind.Forward),
        DataGridNavigationInputModifiers.Control));
```

Ordinary unmodified wheel input is ignored by this table and continues through the
normal scrolling path.

For route activation, pointer input is target-aware. A matching pointer press or
release routes the hit-tested `TargetPosition`, not a possibly stale current cell.
Keyboard input routes the current cell. This keeps item, stable item key, column key,
position, and input origin together across the asynchronous route boundary. Use
pointer release for ordinary single-click activation when selection should run on
press first.

## Dynamic ViewModel policy

Use `InputResolving` when a decision depends on normalized request state. The table
is resolved first; the ViewModel may then replace the result:

```csharp
NavigationInputModel.InputResolving += (_, e) =>
{
    if (e.Request.Kind == DataGridNavigationInputKind.KeyDown &&
        e.Request.Key == DataGridNavigationInputKey.G)
    {
        bool extend =
            (e.Request.Modifiers & DataGridNavigationInputModifiers.Shift) != 0;
        e.Result = DataGridNavigationInputResult.Navigate(
            extend
                ? DataGridNavigationCommand.GridEnd
                : DataGridNavigationCommand.GridStart);
    }
};
```

Keep resolution synchronous and allocation-light. Long-running application
navigation belongs behind `IDataGridRouteNavigator`, whose route operation is
asynchronous and cancellation-aware.

## Result decisions and fallback

The model can return:

| Decision | Effect |
| --- | --- |
| `Ignore` | Preserve descendant and legacy DataGrid processing. |
| `Handle` | Consume the input without navigation. |
| `Navigate` | Execute one semantic `DataGridNavigationCommand`. |
| `NavigateToTarget` | Activate the cell or row under pointer input. |
| `NavigateToPosition` | Activate an explicit row/display-column position. |
| `NavigateRoute` | Execute Navigate, Replace, Reset, Back, or Forward with target/current grid context. |

Navigation results default to `consumeWhenNavigationFails: false`. If a target is
unavailable or a boundary policy exits, the input remains available to normal
control/focus processing. Set the flag to `true` only when the shortcut must always
be owned by the grid.

The input model is resolved once for an event. A semantic command then enters the
same edit, navigation-policy, focus, selection, spatial-layout, and scrolling
pipeline used by built-in keyboard input and ViewModel commands. Boundary policy is
not evaluated twice.

## Editors and routed input

The grid observes normalized input on the routed tunnel so application policy can
see an event before a cell template consumes it. Returning `Ignore` preserves
editor-first behavior and all legacy handling. This is important for text entry,
multiline editors, combo boxes, and embedded controls.

Recommended rules:

- ignore ordinary text-entry keys while `IsEditing` is true;
- map explicit edit commands such as Escape or F2 to semantic commands;
- leave unowned Tab and arrow operations available when focus must exit;
- avoid taking Space or Enter from an embedded button unless that is intentional;
- prefer semantic target kinds over assumptions about template visuals.

## Spatial layouts

Input and layout are independent extensions. Input first resolves to a semantic
command; an active layout may then own its geometry through
`IDataGridLayoutNavigation`. Stack layouts keep their stacking axis, while spatial
UniformGrid and Wrap layouts can resolve two-dimensional neighbors, page movement,
line edges, and first/last positions, including unrealized targets.

This ordering means custom keys, physical-key mappings, pointer target activation,
and route operations require no layout-specific input binding. Runtime layout
switches preserve the same ViewModel input policy.

## MVVM framework use

The core type has no dependency on an MVVM framework:

| Framework | Recommended ownership |
| --- | --- |
| ReactiveUI | Create/configure the model in a `ReactiveObject`; expose separate `ReactiveCommand`s for toolbar or automation actions. Route operations delegate to the `RoutingState` adapter. |
| CommunityToolkit.Mvvm | Expose the model from an `ObservableObject`; commands can call the same semantic navigation controller or injected route service. |
| Prism | Own the model in the ViewModel; let `NavigateRoute` reach a Prism page/region adapter through `IDataGridRouteNavigator`. |
| Caliburn.Micro | Expose the model as a normal bindable property; action methods and the model can share the same navigation service. |
| Plain MVVM / Microsoft DI | Register factories or scoped route adapters in the composition root and create one mutable input model per grid ViewModel. |

Do not register a mutable input model as a singleton unless every grid intentionally
shares one binding table and resolving event.

Toolbar and framework commands should call
`IDataGridRouteNavigationController.RequestNavigate`. The bound DataGrid then creates
the same context used by key and pointer activation; commands do not need to copy
`SelectedItem` or guess the current column.

## Source generation

The generator can create the model property and direct generated-view binding:

```csharp
[GenerateDataGridViewModel(
    typeof(Order),
    ProviderName = "OrderSchema",
    GenerateNavigationModel = true,
    GenerateNavigationInputModel = true,
    NavigationInputModelPropertyName = nameof(NavigationInputModel))]
[GenerateDataGridView(
    typeof(Order),
    NavigationModelPropertyName = nameof(NavigationModel),
    RouteNavigationModelPropertyName = nameof(RouteNavigationModel),
    NavigationInputModelPropertyName = nameof(NavigationInputModel))]
public sealed partial class OrdersViewModel : ReactiveObject
{
    public IDataGridRouteNavigationModel RouteNavigationModel { get; }

    public OrdersViewModel()
    {
        NavigationInputModel.SetBindings(/* application policy */);
    }
}
```

The schema provider emits `CreateNavigationInputModel()`. Namespace-wide attributes
support the same options. Generated views validate manual properties against
`IDataGridNavigationInputModel` and report `PDGSG141` for invalid integration.

## Testing

Test static and dynamic policy without a UI:

```csharp
DataGridNavigationInputResult result = model.Resolve(
    new DataGridNavigationInputRequest(
        DataGridNavigationInputKind.KeyDown,
        DataGridNavigationInputKey.J,
        DataGridNavigationInputKey.J,
        DataGridNavigationKeyDeviceKind.Keyboard,
        DataGridNavigationInputModifiers.None,
        DataGridNavigationPointerDeviceKind.Unknown,
        DataGridNavigationPointerButton.None,
        DataGridNavigationWheelDirection.None,
        clickCount: 0,
        x: double.NaN,
        y: double.NaN,
        wheelDeltaX: 0,
        wheelDeltaY: 0,
        DataGridNavigationInputTargetKind.Cell,
        DataGridNavigationPosition.Unset,
        DataGridNavigationPosition.Unset,
        isEditing: false));

Assert.Equal(DataGridNavigationCommand.Down, result.Command);
```

Use Avalonia Headless tests for normalization, routed-event ordering, hit targets,
editor ownership, focus exit, and the final edit/selection/layout pipeline.

## Sample

The gallery's **Navigation Source Generator** page uses a generated input model and
generated view binding. It demonstrates J/K movement, a physical-key shortcut,
dynamic G/Shift+G resolution, pointer target navigation, Ctrl+wheel route history,
and cell/route completion telemetry without view event handlers.

See also:

- [Navigation model](navigation-model.md)
- [Route navigation and MVVM frameworks](route-navigation.md)
- [Navigation model design](navigation-model-design.md)
- [Source-generated navigation](source-generators/selection-navigation-state.md#navigation-model-generation)
