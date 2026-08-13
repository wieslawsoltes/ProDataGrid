// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;
using DataGridSample.Adapters;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

public sealed class ReactiveUiRouteNavigationViewModel : ReactiveObject, IScreen
{
    private OrderRouteRow? _selectedOrder;
    private string _status = "ReactiveUI RoutingState is ready.";

    public ReactiveUiRouteNavigationViewModel()
    {
        Orders = new ObservableCollection<OrderRouteRow>
        {
            new(2401, "Alpine Ski House", 12840m, "Ready"),
            new(2402, "Blue Yonder", 7640m, "Review"),
            new(2403, "Contoso Retail", 19320m, "Attention"),
            new(2404, "Fabrikam", 9450m, "Ready")
        };
        SelectedOrder = Orders[0];

        Router = new RoutingState();
        ViewLocator = new ReactiveUiRouteViewLocator();
        var navigator = new ReactiveUiDataGridRouteNavigator(this, CreateRouteViewModel);
        var resolver = new DelegateDataGridRouteResolver(context =>
            context.Item is OrderRouteRow order
                ? new DataGridRoute($"orders/{order.Id}", order, "order-details")
                : null);
        RouteNavigationModel = new DataGridRouteNavigationModel(resolver, navigator);
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = $"{e.Request.Kind}: {e.Result.Status} · stack depth {Router.NavigationStack.Count}";

        OpenOrderCommand = ReactiveCommand.CreateFromTask(OpenOrderAsync);
        BackCommand = ReactiveCommand.CreateFromTask(BackAsync);
        ResetCommand = ReactiveCommand.CreateFromTask(ResetAsync);

        Router.NavigateAndReset.Execute(new OrderListRouteViewModel(this)).Subscribe();
    }

    public RoutingState Router { get; }

    public IViewLocator ViewLocator { get; }

    public ObservableCollection<OrderRouteRow> Orders { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> OpenOrderCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> BackCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    public OrderRouteRow? SelectedOrder
    {
        get => _selectedOrder;
        set => this.RaiseAndSetIfChanged(ref _selectedOrder, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private async Task OpenOrderAsync()
    {
        OrderRouteRow? order = SelectedOrder;
        var context = new DataGridRouteContext(
            order,
            order?.Id,
            "customer",
            DataGridNavigationPosition.Unset,
            DataGridRouteNavigationOrigin.Command,
            order != null);
        await RouteNavigationModel.NavigateAsync(DataGridRouteNavigationKind.Navigate, context);
    }

    private async Task BackAsync() =>
        await RouteNavigationModel.NavigateAsync(
            DataGridRouteNavigationKind.Back,
            DataGridRouteContext.Empty);

    private async Task ResetAsync()
    {
        OrderRouteRow? order = SelectedOrder;
        var context = new DataGridRouteContext(
            order,
            order?.Id,
            "customer",
            DataGridNavigationPosition.Unset,
            DataGridRouteNavigationOrigin.Command,
            order != null);
        await RouteNavigationModel.NavigateAsync(DataGridRouteNavigationKind.Reset, context);
    }

    private IRoutableViewModel? CreateRouteViewModel(DataGridRoute route) =>
        route.Parameter is OrderRouteRow order
            ? new OrderDetailRouteViewModel(this, order)
            : null;

    public sealed record OrderRouteRow(int Id, string Customer, decimal Total, string State);
}

public sealed class OrderListRouteViewModel : ReactiveObject, IRoutableViewModel
{
    public OrderListRouteViewModel(IScreen hostScreen)
    {
        HostScreen = hostScreen;
    }

    public string UrlPathSegment => "orders";

    public IScreen HostScreen { get; }
}

public sealed class OrderDetailRouteViewModel : ReactiveObject, IRoutableViewModel
{
    public OrderDetailRouteViewModel(
        IScreen hostScreen,
        ReactiveUiRouteNavigationViewModel.OrderRouteRow order)
    {
        HostScreen = hostScreen;
        Order = order;
    }

    public string UrlPathSegment => $"orders/{Order.Id}";

    public IScreen HostScreen { get; }

    public ReactiveUiRouteNavigationViewModel.OrderRouteRow Order { get; }
}
