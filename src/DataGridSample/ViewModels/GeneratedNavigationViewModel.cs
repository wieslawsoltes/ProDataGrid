// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridNavigation;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(
    typeof(GeneratedNavigationRow),
    ProviderName = "GeneratedNavigationRowSchema",
    GenerateNavigationModel = true,
    GenerateNavigationInputModel = true,
    GenerateRouteContextFactory = true)]
[GenerateDataGridView(
    typeof(GeneratedNavigationRow),
    ViewName = "GeneratedNavigationGridView",
    ViewNamespace = "DataGridSample.Pages",
    Recipe = DataGridViewRecipe.GridOnly,
    Title = "Generated navigation grid",
    AutomationId = "generated-navigation-grid",
    NavigationModelPropertyName = nameof(NavigationModel),
    RouteNavigationModelPropertyName = nameof(RouteNavigationModel),
    NavigationInputModelPropertyName = nameof(NavigationInputModel),
    RouteContextFactoryPropertyName = nameof(RouteContextFactory))]
public sealed partial class GeneratedNavigationViewModel : ReactiveObject
{
    private GeneratedNavigationRow? _selectedItem;
    private string _status = "Both navigation models and their compiled bindings were source-generated.";

    public GeneratedNavigationViewModel()
    {
        Items = new ObservableCollection<GeneratedNavigationRow>
        {
            new(301, "Accounts", "Finance", "Ready"),
            new(302, "Incidents", "Operations", "Attention"),
            new(303, "Identity", "Security", "Active"),
            new(304, "Forecast", "Planning", "Draft")
        };
        SelectedItem = Items[0];

        var resolver = new DelegateDataGridRouteResolver(context =>
            context.Item is GeneratedNavigationRow row
                ? new DataGridRoute($"generated/{row.Id}", row, "details")
                : null);
        RouteNavigationModel = GeneratedNavigationRowSchema.CreateRouteNavigationModel(
            resolver,
            new RouteNavigationSampleViewModel.InMemoryRouteNavigator());

        NavigationInputModel.SetBindings(
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.J,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Down)),
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.K,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Up)),
            DataGridNavigationInputBinding.PhysicalKeyDown(
                DataGridNavigationInputKey.H,
                DataGridNavigationInputResult.Navigate(DataGridNavigationCommand.Left)),
            DataGridNavigationInputBinding.Pointer(
                DataGridNavigationInputKind.PointerPressed,
                DataGridNavigationPointerButton.Primary,
                DataGridNavigationInputResult.NavigateToTarget(),
                targetKind: DataGridNavigationInputTargetKind.Cell),
            DataGridNavigationInputBinding.Pointer(
                DataGridNavigationInputKind.PointerReleased,
                DataGridNavigationPointerButton.Primary,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate),
                targetKind: DataGridNavigationInputTargetKind.Cell),
            DataGridNavigationInputBinding.KeyDown(
                DataGridNavigationInputKey.Enter,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Navigate)),
            DataGridNavigationInputBinding.Wheel(
                DataGridNavigationWheelDirection.Up,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Back),
                DataGridNavigationInputModifiers.Control),
            DataGridNavigationInputBinding.Wheel(
                DataGridNavigationWheelDirection.Down,
                DataGridNavigationInputResult.NavigateRoute(DataGridRouteNavigationKind.Forward),
                DataGridNavigationInputModifiers.Control));
        NavigationInputModel.InputResolving += (_, e) =>
        {
            if (e.Request.Kind == DataGridNavigationInputKind.KeyDown &&
                e.Request.Key == DataGridNavigationInputKey.G)
            {
                e.Result = DataGridNavigationInputResult.Navigate(
                    (e.Request.Modifiers & DataGridNavigationInputModifiers.Shift) != 0
                        ? DataGridNavigationCommand.GridEnd
                        : DataGridNavigationCommand.GridStart);
            }
        };

        NavigationModel.NavigationChanged += (_, e) =>
            Status = e.Completed.Moved
                ? $"Generated cell model: {e.Completed.Request.Command} → {e.Completed.NewPosition.RowIndex}:{e.Completed.NewPosition.ColumnDisplayIndex}"
                : $"Generated cell model: {e.Completed.FailureReason}";
        RouteNavigationModel.NavigationChanged += (_, e) =>
            Status = $"Generated route model: {e.Result.Status} · {e.Result.CurrentRoute.Path} · key {e.Request.Context.ItemKey} · cell {e.Request.Context.Position.RowIndex}:{e.Request.Context.Position.ColumnDisplayIndex}";

        NextCellCommand = ReactiveCommand.Create(() =>
            NavigationModel.RequestNavigate(DataGridNavigationCommand.Next));
        PreviousCellCommand = ReactiveCommand.Create(() =>
            NavigationModel.RequestNavigate(DataGridNavigationCommand.Previous));
        OpenRouteCommand = ReactiveCommand.Create(() =>
            RouteNavigationModel.RequestNavigate(DataGridRouteNavigationKind.Navigate));
        BackCommand = ReactiveCommand.Create(() =>
            RouteNavigationModel.RequestNavigate(DataGridRouteNavigationKind.Back));
    }

    public ObservableCollection<GeneratedNavigationRow> Items { get; }

    public DataGridRouteNavigationModel RouteNavigationModel { get; }

    public ReactiveCommand<RxVoid, bool> NextCellCommand { get; }

    public ReactiveCommand<RxVoid, bool> PreviousCellCommand { get; }

    public ReactiveCommand<RxVoid, bool> OpenRouteCommand { get; }

    public ReactiveCommand<RxVoid, bool> BackCommand { get; }

    public GeneratedNavigationRow? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

}

public sealed class GeneratedNavigationRow
{
    public GeneratedNavigationRow(int id, string name, string area, string state)
    {
        Id = id;
        Name = name;
        Area = area;
        State = state;
    }

    [DataGridKey]
    [DataGridColumn(Order = 0, ColumnKey = "id", IsReadOnly = true)]
    public int Id { get; set; }

    [DataGridColumn(Order = 1, ColumnKey = "name")]
    public string Name { get; set; }

    [DataGridColumn(Order = 2, ColumnKey = "area")]
    public string Area { get; set; }

    [DataGridColumn(Order = 3, ColumnKey = "state")]
    public string State { get; set; }
}
