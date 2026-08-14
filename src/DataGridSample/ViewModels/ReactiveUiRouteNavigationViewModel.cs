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
        RouteContextFactory = new DataGridRouteContextFactory(item => ((OrderRouteRow)item).Id);
        NavigationInputModel = new DataGridNavigationInputModel(
            DataGridNavigationInputBinding.Pointer(
                DataGridNavigationInputKind.PointerReleased,
                DataGridNavigationPointerButton.Primary,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate),
                targetKind: DataGridNavigationInputTargetKind.Cell),
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.Enter,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate)));
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = $"{e.Request.Kind}: {e.Result.Status} · key {e.Request.Context.ItemKey} · cell {e.Request.Context.Position.RowIndex}:{e.Request.Context.Position.ColumnDisplayIndex} · stack depth {Router.NavigationStack.Count}";

        OpenOrderCommand = ReactiveCommand.Create(() => RouteNavigationModel.RequestNavigate(DataGridRouteNavigationKind.Navigate));
        BackCommand = ReactiveCommand.Create(() => RouteNavigationModel.RequestNavigate(DataGridRouteNavigationKind.Back));
        ResetCommand = ReactiveCommand.Create(() => RouteNavigationModel.RequestNavigate(DataGridRouteNavigationKind.Reset));

        Router.NavigateAndReset.Execute(new OrderListRouteViewModel(this)).Subscribe();
    }

    public RoutingState Router { get; }

    public IViewLocator ViewLocator { get; }

    public ObservableCollection<OrderRouteRow> Orders { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public DataGridRouteContextFactory RouteContextFactory { get; }

    public DataGridNavigationInputModel NavigationInputModel { get; }

    public ReactiveCommand<RxVoid, bool> OpenOrderCommand { get; }

    public ReactiveCommand<RxVoid, bool> BackCommand { get; }

    public ReactiveCommand<RxVoid, bool> ResetCommand { get; }

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
