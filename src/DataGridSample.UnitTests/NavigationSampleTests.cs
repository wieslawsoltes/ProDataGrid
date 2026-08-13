using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class NavigationSampleTests
{
    [AvaloniaFact]
    public void Generated_view_binds_generated_cell_and_application_route_models()
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
            Assert.True(viewModel.NavigationModel.RequestNavigate(DataGridNavigationCommand.Down));
        }
        finally
        {
            window.Close();
        }
    }

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
