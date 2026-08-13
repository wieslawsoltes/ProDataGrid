# Route navigation and MVVM frameworks

Application routing is distinct from cell movement. `IDataGridRouteNavigationModel`
maps the current row/cell context to a stable route and asynchronously delegates it
to the application's router. The core package contains no ReactiveUI, Prism,
CommunityToolkit, service-locator, or view dependency.

## Core pipeline

```text
DataGridRouteContext -> IDataGridRouteResolver -> DataGridRoute
    -> DataGridRouteNavigationRequest -> IDataGridRouteNavigator
    -> DataGridRouteNavigationResult
```

`DataGridRouteContext` carries the item, optional stable item key, column key,
current position, and activation origin. `DataGridRoute` carries a stable path,
optional immutable parameter/state, and optional target region/outlet/screen.

The supported operations are `Navigate`, `Replace`, `Reset`, `Back`, and `Forward`.
Navigators advertise their supported operations through
`DataGridRouteNavigationCapabilities`; unsupported history operations return a
typed `NotSupported` result.

## Create and bind a route model

```csharp
var resolver = new DelegateDataGridRouteResolver(context =>
    context.Item is Order order
        ? new DataGridRoute(
            path: $"orders/{order.Id}",
            parameter: order.Id,
            target: "details")
        : null);

RouteNavigationModel = new DataGridRouteNavigationModel(
    resolver,
    applicationNavigator);
```

```xml
<DataGrid ItemsSource="{CompiledBinding Orders}"
          RouteNavigationModel="{CompiledBinding RouteNavigationModel}" />
```

An application command can construct context from stable ViewModel state and call
the model directly. A control behavior can call `DataGrid.NavigateRouteAsync`, which
uses `GetCurrentRouteContext` for the current cell.

```csharp
DataGridRouteNavigationResult result = await RouteNavigationModel.NavigateAsync(
    DataGridRouteNavigationKind.Navigate,
    new DataGridRouteContext(
        selectedOrder,
        selectedOrder?.Id,
        "name",
        DataGridNavigationPosition.Unset,
        DataGridRouteNavigationOrigin.Command,
        hasItem: selectedOrder is not null),
    cancellationToken);
```

Ordinary outcomes do not throw. Inspect `Succeeded`, `Canceled`, `RouteNotFound`,
`NotSupported`, `Busy`, `InvalidRequest`, or `Failed`, plus the optional exception
and message. The model converts adapter exceptions into `Failed`, resets
`IsNavigating` in `finally`, and publishes completion telemetry.

## ReactiveUI

Use one `IScreen` and one `RoutingState` as the navigation root. Map a stable route
to an `IRoutableViewModel`, then translate operations as follows:

| ProDataGrid | ReactiveUI |
| --- | --- |
| `Navigate` | `RoutingState.Navigate.Execute(viewModel)` |
| `Reset` | `RoutingState.NavigateAndReset.Execute(viewModel)` |
| `Back` | `RoutingState.NavigateBack.Execute()` |
| `Replace`, `Forward` | return `NotSupported` unless the application adds explicit history support |

Host the router with `RoutedViewHost`. Keep route-to-ViewModel creation in an
AOT-safe factory/type switch, not `Activator.CreateInstance` or reflection.

The sample's `ReactiveUiDataGridRouteNavigator` is a working adapter and
`ReactiveUiRouteViewLocator` is a reflection-free view locator. The
`ReactiveUiRouteNavigationPage` demonstrates `IScreen`, `RoutingState`,
`IRoutableViewModel`, and `RoutedViewHost` together.

See the official [ReactiveUI routing guide](https://www.reactiveui.net/docs/handbook/routing.html).

## Prism page or region navigation

Prism owns URI registration, navigation parameters, regions/page hosts, and its
journal. A Prism adapter should:

1. map `DataGridRoute.Path` to the registered URI or view key;
2. map `Target` to a region name or page host;
3. copy the typed `Parameter` into `NavigationParameters`;
4. map `Navigate`, `Back`, and supported journal operations to the native service;
5. translate native success/cancellation/errors into
   `DataGridRouteNavigationResult`.

Keep the native service behind `IDataGridRouteNavigator`; the DataGrid and route
resolver must not reference Prism:

```csharp
public sealed class PrismDataGridRouteNavigator : IDataGridRouteNavigator
{
    private readonly IApplicationNavigation _navigation;

    public PrismDataGridRouteNavigator(IApplicationNavigation navigation) =>
        _navigation = navigation;

    public DataGridRouteNavigationCapabilities Capabilities =>
        DataGridRouteNavigationCapabilities.Navigate |
        DataGridRouteNavigationCapabilities.Back;

    public async ValueTask<DataGridRouteNavigationResult> NavigateAsync(
        DataGridRouteNavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        ApplicationNavigationResult native = await _navigation.NavigateAsync(
            request.Kind,
            request.Route.Path,
            request.Route.Target,
            request.Route.Parameter,
            cancellationToken);
        return native.ToDataGridResult();
    }
}
```

`IApplicationNavigation` in this example is an application boundary wrapping Prism's
page or region service. This keeps Prism version and platform differences out of
ViewModels and the grid package. See Prism's official
[navigation documentation](https://docs.prismlibrary.com/docs/navigation/index.html).

## CommunityToolkit.Mvvm

CommunityToolkit.Mvvm deliberately provides MVVM primitives rather than a router.
Inject the application's navigation service or `IDataGridRouteNavigationModel` and
invoke it from `AsyncRelayCommand`:

```csharp
public sealed partial class OrdersViewModel : ObservableObject
{
    private readonly IDataGridRouteNavigationModel _routes;

    public OrdersViewModel(IDataGridRouteNavigationModel routes) =>
        _routes = routes;

    [RelayCommand]
    private async Task OpenOrderAsync(Order order, CancellationToken token)
    {
        var context = new DataGridRouteContext(
            order, order.Id, "name", DataGridNavigationPosition.Unset,
            DataGridRouteNavigationOrigin.Command);
        await _routes.NavigateAsync(
            DataGridRouteNavigationKind.Navigate,
            context,
            token);
    }
}
```

See the official [MVVM Toolkit introduction](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/).

## Plain MVVM and Microsoft DI

Register the native navigator and resolver at their real lifetimes, then create one
route model per navigation scope:

```csharp
services.AddSingleton<IDataGridRouteNavigator, ApplicationRouteNavigator>();
services.AddSingleton<IDataGridRouteResolver, OrderRouteResolver>();
services.AddScoped<IDataGridRouteNavigationModel>(provider =>
    new DataGridRouteNavigationModel(
        provider.GetRequiredService<IDataGridRouteResolver>(),
        provider.GetRequiredService<IDataGridRouteNavigator>()));
```

The same arrangement works with plain `ICommand`, Caliburn.Micro actions, or another
MVVM framework: only the command type and native adapter change.

The gallery's **Navigation MVVM Frameworks** page runs the same resolver and route
model through a framework mapping matrix. It complements the end-to-end ReactiveUI
page and the production adapter recipes above without adding Prism or Toolkit
dependencies to the core package.

Key, pointer, remote, gamepad, or wheel gestures can also request these route
operations through a bound `IDataGridNavigationInputModel`. The input layer returns
`NavigateRoute(kind)`; route resolution and the native framework adapter remain in
this asynchronous pipeline. See [navigation input model](navigation-input-model.md).

## Guards, cancellation, and concurrency

- Use `NavigationChanging` for synchronous cancellation or route replacement.
- Pass command/lifetime cancellation tokens through every adapter call.
- A model rejects overlapping requests with a typed status; do not run two native
  journal mutations concurrently.
- Advertise only capabilities the native router actually implements.
- Treat route paths as stable identifiers, not localized display text.

## Deep links and restoration

Inbound URIs or native history changes should update application ViewModel state.
Restore the selected item/current cell by stable item and column keys through normal
selection/state APIs, then call the route model with origin `RestoredState` when a
route journal mutation is required. Routers should never search realized row
containers; sorting, filtering, paging, and virtualization make them transient.

## Source generation

Generated schema providers expose `CreateRouteNavigationModel(resolver, navigator)`.
Generated views validate and compile-bind an application-owned route property with
`RouteNavigationModelPropertyName`. See
[source-generated navigation](source-generators/selection-navigation-state.md#navigation-model-generation).
