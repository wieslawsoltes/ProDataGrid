// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

/// <summary>
/// Demonstrates how framework-native route operations remain behind the
/// framework-neutral <see cref="IDataGridRouteNavigator"/> boundary.
/// </summary>
public sealed class MvvmRouteFrameworksViewModel : ReactiveObject
{
    private FrameworkRouteRecipe? _selectedRecipe;
    private string _status = "Select a framework recipe and execute a route operation.";

    public MvvmRouteFrameworksViewModel()
    {
        Recipes = new ObservableCollection<FrameworkRouteRecipe>
        {
            new(
                "ReactiveUI",
                "IScreen + RoutingState",
                "Navigate / NavigateAndReset / NavigateBack",
                "Working adapter and RoutedViewHost sample are on the adjacent ReactiveUI page."),
            new(
                "Prism",
                "IRegionManager or INavigationService",
                "RequestNavigate / NavigateAsync / GoBackAsync",
                "Wrap the platform-specific journal and NavigationParameters in IDataGridRouteNavigator."),
            new(
                "CommunityToolkit.Mvvm",
                "Application-owned navigation service",
                "AsyncRelayCommand -> NavigateAsync",
                "The Toolkit supplies commands and observable state; the application supplies the router."),
            new(
                "Plain MVVM + DI",
                "Scoped IDataGridRouteNavigationModel",
                "ICommand -> NavigateAsync",
                "Register the resolver and native adapter with Microsoft.Extensions.DependencyInjection.")
        };
        SelectedRecipe = Recipes[0];

        var resolver = new DelegateDataGridRouteResolver(context =>
            context.Item is FrameworkRouteRecipe recipe
                ? new DataGridRoute(
                    $"frameworks/{recipe.Name.ToLowerInvariant().Replace(' ', '-')}",
                    recipe,
                    "details")
                : null);
        var navigator = new FrameworkRecipeNavigator(() => SelectedRecipe);
        RouteNavigationModel = new DataGridRouteNavigationModel(resolver, navigator);
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = e.Result.Succeeded
                ? $"{e.Result.CurrentRoute.Path}: {navigator.LastNativeOperation}"
                : $"{e.Request.Kind}: {e.Result.Status}";

        NavigateCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Navigate));
        ResetCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Reset));
        BackCommand = ReactiveCommand.CreateFromTask(() => NavigateAsync(DataGridRouteNavigationKind.Back));
    }

    public ObservableCollection<FrameworkRouteRecipe> Recipes { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public ReactiveCommand<RxVoid, RxVoid> NavigateCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ResetCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> BackCommand { get; }

    public FrameworkRouteRecipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set => this.RaiseAndSetIfChanged(ref _selectedRecipe, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    private async Task NavigateAsync(DataGridRouteNavigationKind kind)
    {
        FrameworkRouteRecipe? recipe = SelectedRecipe;
        DataGridRouteContext context = kind == DataGridRouteNavigationKind.Back
            ? DataGridRouteContext.Empty
            : new DataGridRouteContext(
                recipe,
                recipe?.Name,
                "framework",
                DataGridNavigationPosition.Unset,
                DataGridRouteNavigationOrigin.Command,
                recipe != null);
        await RouteNavigationModel.NavigateAsync(kind, context);
    }

    public sealed record FrameworkRouteRecipe(
        string Name,
        string NativeHost,
        string NativeOperations,
        string IntegrationNotes);

    private sealed class FrameworkRecipeNavigator : IDataGridRouteNavigator
    {
        private readonly Func<FrameworkRouteRecipe?> _selectedRecipe;

        public FrameworkRecipeNavigator(Func<FrameworkRouteRecipe?> selectedRecipe)
        {
            _selectedRecipe = selectedRecipe;
        }

        public DataGridRouteNavigationCapabilities Capabilities =>
            DataGridRouteNavigationCapabilities.Navigate |
            DataGridRouteNavigationCapabilities.Reset |
            DataGridRouteNavigationCapabilities.Back;

        public string LastNativeOperation { get; private set; } = "No native operation";

        public ValueTask<DataGridRouteNavigationResult> NavigateAsync(
            DataGridRouteNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromResult(DataGridRouteNavigationResult.FromStatus(
                    DataGridRouteNavigationStatus.Canceled));
            }

            FrameworkRouteRecipe? recipe = request.Route.Parameter as FrameworkRouteRecipe ?? _selectedRecipe();
            if (recipe == null)
            {
                return ValueTask.FromResult(DataGridRouteNavigationResult.FromStatus(
                    DataGridRouteNavigationStatus.RouteNotFound));
            }

            LastNativeOperation = MapOperation(recipe.Name, request.Kind);
            DataGridRoute currentRoute = !request.Route.IsValid
                ? new DataGridRoute($"frameworks/{recipe.Name.ToLowerInvariant().Replace(' ', '-')}")
                : request.Route;
            return ValueTask.FromResult(DataGridRouteNavigationResult.Success(currentRoute));
        }

        private static string MapOperation(string framework, DataGridRouteNavigationKind kind) =>
            (framework, kind) switch
            {
                ("ReactiveUI", DataGridRouteNavigationKind.Navigate) => "RoutingState.Navigate.Execute(viewModel)",
                ("ReactiveUI", DataGridRouteNavigationKind.Reset) => "RoutingState.NavigateAndReset.Execute(viewModel)",
                ("ReactiveUI", DataGridRouteNavigationKind.Back) => "RoutingState.NavigateBack.Execute()",
                ("Prism", DataGridRouteNavigationKind.Navigate) => "INavigationService.NavigateAsync(uri, parameters)",
                ("Prism", DataGridRouteNavigationKind.Reset) => "NavigateAsync(absoluteUri, parameters)",
                ("Prism", DataGridRouteNavigationKind.Back) => "INavigationService.GoBackAsync(parameters)",
                ("CommunityToolkit.Mvvm", DataGridRouteNavigationKind.Navigate) => "AsyncRelayCommand -> applicationNavigation.NavigateAsync(route)",
                ("CommunityToolkit.Mvvm", DataGridRouteNavigationKind.Reset) => "AsyncRelayCommand -> applicationNavigation.ResetAsync(route)",
                ("CommunityToolkit.Mvvm", DataGridRouteNavigationKind.Back) => "AsyncRelayCommand -> applicationNavigation.BackAsync()",
                (_, DataGridRouteNavigationKind.Navigate) => "ICommand -> IDataGridRouteNavigationModel.NavigateAsync(route)",
                (_, DataGridRouteNavigationKind.Reset) => "ICommand -> IDataGridRouteNavigationModel.NavigateAsync(reset)",
                _ => "ICommand -> IDataGridRouteNavigationModel.NavigateAsync(back)"
            };
    }
}
