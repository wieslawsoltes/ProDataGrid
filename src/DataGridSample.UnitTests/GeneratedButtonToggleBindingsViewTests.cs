using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ReactiveUI.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class GeneratedButtonToggleBindingsViewTests
{
    [Fact]
    public void Generated_schema_exposes_compiled_row_action_and_toggle_bindings()
    {
        var viewModel = new ButtonColumnDefinitionBindingsViewModel();

        Assert.Equal(7, viewModel.ColumnDefinitions.Count);
        var action = Assert.IsType<DataGridButtonColumnDefinition>(viewModel.ColumnDefinitions[2]);
        var fallback = Assert.IsType<DataGridButtonColumnDefinition>(viewModel.ColumnDefinitions[3]);
        var favorite = Assert.IsType<DataGridToggleButtonColumnDefinition>(viewModel.ColumnDefinitions[4]);
        var presence = Assert.IsType<DataGridToggleSwitchColumnDefinition>(viewModel.ColumnDefinitions[5]);

        Assert.NotNull(action.ContentBinding);
        Assert.NotNull(action.CommandBinding);
        Assert.NotNull(action.CommandParameterBinding);
        Assert.NotNull(fallback.CommandBinding);
        Assert.Null(fallback.CommandParameterBinding);
        Assert.NotNull(favorite.CheckedContentBinding);
        Assert.NotNull(favorite.UncheckedContentBinding);
        Assert.NotNull(favorite.CommandBinding);
        Assert.NotNull(presence.OnContentBinding);
        Assert.NotNull(presence.OffContentBinding);
        Assert.NotNull(presence.CommandBinding);
    }

    [Fact]
    public void Reactive_row_commands_update_observable_state()
    {
        var viewModel = new ButtonColumnDefinitionBindingsViewModel();
        ButtonColumnDefinitionBindingsItem item = viewModel.Items[0];

        ((ICommand)item.RunActionCommand).Execute(item.Name);
        Assert.Equal(1, item.ClickCount);
        Assert.Equal("Pause", item.ActionLabel);

        ((ICommand)item.ClearClicksCommand).Execute(item);
        Assert.Equal(0, item.ClickCount);
        Assert.Equal("Run", item.ActionLabel);
        Assert.Contains("default row parameter", item.LastEvent);
    }

    [AvaloniaFact]
    public void Generated_reactive_view_binds_items_columns_and_fast_path()
    {
        var viewModel = new ButtonColumnDefinitionBindingsViewModel();
        var view = new ButtonColumnDefinitionBindingsPage(viewModel);
        var window = new Window { Width = 1100, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.IsAssignableFrom<ReactiveUserControl<ButtonColumnDefinitionBindingsViewModel>>(view);
            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.ColumnDefinitions, grid.ColumnDefinitionsSource);
            Assert.Same(viewModel.FastPathOptions, grid.FastPathOptions);
        }
        finally
        {
            window.Close();
        }
    }
}
