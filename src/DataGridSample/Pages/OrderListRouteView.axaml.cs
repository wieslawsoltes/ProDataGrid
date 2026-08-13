// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using DataGridSample.ViewModels;
using ReactiveUI.Avalonia;

namespace DataGridSample.Pages;

public partial class OrderListRouteView : ReactiveUserControl<OrderListRouteViewModel>
{
    public OrderListRouteView()
    {
        InitializeComponent();
    }
}
