// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.Adapters;

/// <summary>
/// Adapts the framework-neutral DataGrid route contract to a ReactiveUI routing stack.
/// </summary>
public sealed class ReactiveUiDataGridRouteNavigator : IDataGridRouteNavigator
{
    private readonly IScreen _screen;
    private readonly Func<DataGridRoute, IRoutableViewModel?> _viewModelFactory;

    public ReactiveUiDataGridRouteNavigator(
        IScreen screen,
        Func<DataGridRoute, IRoutableViewModel?> viewModelFactory)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _viewModelFactory = viewModelFactory ?? throw new ArgumentNullException(nameof(viewModelFactory));
    }

    public DataGridRouteNavigationCapabilities Capabilities =>
        DataGridRouteNavigationCapabilities.Navigate |
        DataGridRouteNavigationCapabilities.Reset |
        DataGridRouteNavigationCapabilities.Back;

    public async ValueTask<DataGridRouteNavigationResult> NavigateAsync(
        DataGridRouteNavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        switch (request.Kind)
        {
            case DataGridRouteNavigationKind.Navigate:
            case DataGridRouteNavigationKind.Reset:
                IRoutableViewModel? viewModel = _viewModelFactory(request.Route);
                if (viewModel == null)
                {
                    return DataGridRouteNavigationResult.FromStatus(
                        DataGridRouteNavigationStatus.RouteNotFound);
                }

                if (request.Kind == DataGridRouteNavigationKind.Reset)
                {
                    await _screen.Router.NavigateAndReset.Execute(viewModel).ToTask(cancellationToken);
                }
                else
                {
                    await _screen.Router.Navigate.Execute(viewModel).ToTask(cancellationToken);
                }

                return DataGridRouteNavigationResult.Success(request.Route);

            case DataGridRouteNavigationKind.Back:
                if (_screen.Router.NavigationStack.Count <= 1)
                {
                    return DataGridRouteNavigationResult.FromStatus(
                        DataGridRouteNavigationStatus.NotSupported);
                }

                IRoutableViewModel current = await _screen.Router.NavigateBack
                    .Execute(RxVoid.Default)
                    .ToTask(cancellationToken);
                return DataGridRouteNavigationResult.Success(
                    new DataGridRoute(current.UrlPathSegment));

            default:
                return DataGridRouteNavigationResult.FromStatus(
                    DataGridRouteNavigationStatus.NotSupported);
        }
    }
}
