using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class NavigationSampleTests
{
    [AvaloniaFact]
    public void Mvvm_framework_page_runs_framework_neutral_route_recipes()
    {
        var viewModel = new MvvmRouteFrameworksViewModel();
        var view = new MvvmRouteFrameworksPage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 620, Content = view };
        window.ApplySampleTheme();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.Same(viewModel.RouteNavigationModel, grid.RouteNavigationModel);
            Assert.Same(viewModel.RouteContextFactory, grid.RouteContextFactory);
            Assert.Same(viewModel.NavigationInputModel, grid.NavigationInputModel);

            viewModel.NavigateCommand.Execute().Subscribe();
            Assert.Contains("RoutingState.Navigate", viewModel.Status, StringComparison.Ordinal);

            viewModel.SelectedRecipe = viewModel.Recipes[1];
            viewModel.ResetCommand.Execute().Subscribe();
            Assert.Contains("NavigateAsync(absoluteUri", viewModel.Status, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_view_binds_generated_cell_input_and_application_route_models()
    {
        var viewModel = new GeneratedNavigationViewModel();
        var view = new GeneratedNavigationGridView(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.ApplySampleTheme();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();

            Assert.Same(viewModel.NavigationModel, grid.NavigationModel);
            Assert.Same(viewModel.RouteNavigationModel, grid.RouteNavigationModel);
            Assert.Same(viewModel.NavigationInputModel, grid.NavigationInputModel);
            Assert.Same(viewModel.RouteContextFactory, grid.RouteContextFactory);
            DataGridNavigationInputResult jResult = viewModel.NavigationInputModel.Resolve(
                CreateKeyRequest(DataGridNavigationInputKey.J));
            Assert.Equal(DataGridNavigationInputDecision.Navigate, jResult.Decision);
            Assert.Equal(DataGridNavigationCommand.Down, jResult.Command);
            Assert.True(viewModel.NavigationModel.RequestNavigate(DataGridNavigationCommand.Down));

            DataGridCell target = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Single(cell => ReferenceEquals(cell.DataContext, viewModel.Items[2]) && cell.OwningColumn?.DisplayIndex == 1);
            RaisePrimaryClick(target, window);

            Assert.Contains("generated/303", viewModel.Status, StringComparison.Ordinal);
            Assert.Contains("key 303", viewModel.Status, StringComparison.Ordinal);
            Assert.Contains("cell 2:1", viewModel.Status, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Route_page_click_routes_the_exact_cell_context_inside_the_grid()
    {
        var viewModel = new RouteNavigationSampleViewModel();
        var view = new RouteNavigationPage { DataContext = viewModel };
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.ApplySampleTheme();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            DataGridCell target = grid.GetVisualDescendants()
                .OfType<DataGridCell>()
                .Single(cell => ReferenceEquals(cell.DataContext, viewModel.Items[2]) && cell.OwningColumn?.DisplayIndex == 2);

            RaisePrimaryClick(target, window);

            Assert.Contains("work-items/108", viewModel.Status, StringComparison.Ordinal);
            Assert.Contains("key 108", viewModel.Status, StringComparison.Ordinal);
            Assert.Contains("cell 2:2", viewModel.Status, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    private static void RaisePrimaryClick(Control source, Visual root)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var position = new Point(source.Bounds.Width / 2, source.Bounds.Height / 2);
        var pressedProperties = new PointerPointProperties(
            RawInputModifiers.LeftMouseButton,
            PointerUpdateKind.LeftButtonPressed);
        source.RaiseEvent(new PointerPressedEventArgs(
            source,
            pointer,
            root,
            position,
            0,
            pressedProperties,
            KeyModifiers.None,
            clickCount: 1));
        var releasedProperties = new PointerPointProperties(
            RawInputModifiers.None,
            PointerUpdateKind.LeftButtonReleased);
        source.RaiseEvent(new PointerReleasedEventArgs(
            source,
            pointer,
            root,
            position,
            0,
            releasedProperties,
            KeyModifiers.None,
            MouseButton.Left));
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGridNavigationInputRequest CreateKeyRequest(DataGridNavigationInputKey key) =>
        new(
            DataGridNavigationInputKind.KeyDown,
            key,
            key,
            DataGridNavigationKeyDeviceKind.Keyboard,
            DataGridNavigationInputModifiers.None,
            DataGridNavigationPointerDeviceKind.Unknown,
            DataGridNavigationPointerButton.None,
            DataGridNavigationWheelDirection.None,
            0,
            double.NaN,
            double.NaN,
            0,
            0,
            DataGridNavigationInputTargetKind.Cell,
            DataGridNavigationPosition.Unset,
            DataGridNavigationPosition.Unset,
            false);

    [AvaloniaFact]
    public void Extended_navigation_pages_bind_models_without_view_event_handlers()
    {
        var customViewModel = new CustomNavigationModelViewModel();
        var hierarchyViewModel = new HierarchicalNavigationViewModel();
        var stateViewModel = new NavigationStateViewModel();
        var tabs = new TabControl
        {
            ItemsSource = new object[]
            {
                new CustomNavigationModelPage { DataContext = customViewModel },
                new HierarchicalNavigationPage { DataContext = hierarchyViewModel },
                new NavigationStatePage { DataContext = stateViewModel }
            }
        };
        var window = new Window { Width = 1000, Height = 680, Content = tabs };
        window.ApplySampleTheme();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid customGrid = tabs.GetLogicalDescendants().OfType<DataGrid>().First();
            Assert.Same(customViewModel.NavigationModel, customGrid.NavigationModel);

            stateViewModel.UseSpreadsheetPolicyCommand.Execute().Subscribe();
            stateViewModel.CaptureCommand.Execute().Subscribe();
            stateViewModel.UseContainedPolicyCommand.Execute().Subscribe();
            stateViewModel.RestoreCommand.Execute().Subscribe();

            Assert.Equal(DataGridNavigationBoundaryMode.Wrap, stateViewModel.NavigationModel.HorizontalBoundaryMode);
            Assert.Equal(DataGridTabNavigationMode.Always, stateViewModel.NavigationModel.TabNavigationMode);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Custom_policy_redirects_protected_row_and_cancels_final_column()
    {
        var model = new CustomNavigationModelViewModel.GuardedNavigationModel();
        var down = new DataGridNavigationRequest(
            DataGridNavigationCommand.Down,
            DataGridNavigationOrigin.Command,
            new DataGridNavigationPosition(0, 1),
            new DataGridNavigationPosition(1, 1),
            Avalonia.Input.KeyModifiers.None,
            isEditing: false,
            DataGridSelectionMode.Single,
            DataGridSelectionUnit.Cell,
            Avalonia.Media.FlowDirection.LeftToRight,
            firstRowIndex: 0,
            lastRowIndex: 3,
            firstColumnDisplayIndex: 0,
            lastColumnDisplayIndex: 2);

        DataGridNavigationResult redirected = model.Resolve(down);
        DataGridNavigationResult canceled = model.Resolve(new DataGridNavigationRequest(
            DataGridNavigationCommand.Right,
            DataGridNavigationOrigin.Command,
            new DataGridNavigationPosition(0, 2),
            proposedPosition: null,
            Avalonia.Input.KeyModifiers.None,
            isEditing: false,
            DataGridSelectionMode.Single,
            DataGridSelectionUnit.Cell,
            Avalonia.Media.FlowDirection.LeftToRight,
            firstRowIndex: 0,
            lastRowIndex: 3,
            firstColumnDisplayIndex: 0,
            lastColumnDisplayIndex: 2));

        Assert.Equal(new DataGridNavigationPosition(2, 1), redirected.Target);
        Assert.Equal(DataGridNavigationDecision.Stay, canceled.Decision);
        Assert.Equal(DataGridNavigationFailureReason.Canceled, canceled.FailureReason);
    }
}
