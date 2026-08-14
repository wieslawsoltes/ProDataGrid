// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ReactiveUI;

namespace DataGridSample.Adapters;

/// <summary>
/// A reflection-free ReactiveUI view locator for the route navigation sample.
/// </summary>
public sealed class ReactiveUiRouteViewLocator : IViewLocator
{
    public IViewFor<TViewModel>? ResolveView<TViewModel>() where TViewModel : class =>
        ResolveView<TViewModel>(null);

    public IViewFor<TViewModel>? ResolveView<TViewModel>(string? contract) where TViewModel : class
    {
        IViewFor? view = typeof(TViewModel) == typeof(OrderListRouteViewModel)
            ? new OrderListRouteView()
            : typeof(TViewModel) == typeof(OrderDetailRouteViewModel)
                ? new OrderDetailRouteView()
                : null;
        return view as IViewFor<TViewModel>;
    }

    public IViewFor? ResolveView(object? instance) => ResolveView(instance, null);

    public IViewFor? ResolveView(object? instance, string? contract) => instance switch
    {
        OrderListRouteViewModel => new OrderListRouteView(),
        OrderDetailRouteViewModel => new OrderDetailRouteView(),
        _ => null
    };
}
