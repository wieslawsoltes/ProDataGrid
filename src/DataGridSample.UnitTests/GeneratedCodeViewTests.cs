using System;
using System.Collections;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.DataGridClipboard;
using Avalonia.Controls.DataGridFilling;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts.Avalonia;
using ReactiveUI.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class GeneratedCodeViewTests
{
    [AvaloniaFact]
    public void Avalonia_strategy_binds_generated_members_and_uses_custom_base()
    {
        var viewModel = new GeneratedColumnsAttributesViewModel();
        var view = new GeneratedColumnsCodeView(viewModel);
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.IsAssignableFrom<GeneratedGridViewBase>(view);
            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.ColumnDefinitions, grid.ColumnDefinitionsSource);
            Assert.Same(viewModel.FastPathOptions, grid.FastPathOptions);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reactive_ui_strategy_binds_models_and_reactive_source_generated_property()
    {
        using var viewModel = new GeneratedColumnsDynamicDataViewModel();
        var view = new GeneratedReactiveDataGridView(viewModel);
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.IsAssignableFrom<ReactiveUserControl<GeneratedColumnsDynamicDataViewModel>>(view);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);

            searchBox.Text = "AVLN";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("AVLN", viewModel.Query);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_reactive_view_state_projections_bind_state_message_and_retry_command()
    {
        var viewModel = new GeneratedReactiveViewStatesViewModel();
        var view = new GeneratedReactiveViewStatesPage(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Control loading = view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "GeneratedLoadingState");
            Control empty = view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "GeneratedEmptyState");
            Control error = view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "GeneratedErrorState");
            TextBlock errorMessage = view.GetLogicalDescendants().OfType<TextBlock>().Single(control => control.Name == "GeneratedErrorMessage");
            Button retry = view.GetLogicalDescendants().OfType<Button>().Single(control => control.Name == "GeneratedRetryButton");

            Assert.False(grid.IsVisible);
            Assert.False(loading.IsVisible);
            Assert.False(empty.IsVisible);
            Assert.True(error.IsVisible);
            Assert.Equal(viewModel.ErrorMessage, errorMessage.Text);
            Assert.Same(viewModel.RetryCommand, retry.Command);

            viewModel.ErrorMessage = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Generated trades could not be loaded.", errorMessage.Text);

            viewModel.ViewState = DataGridGeneratedViewState.Loading;
            Dispatcher.UIThread.RunJobs();
            Assert.True(loading.IsVisible);
            Assert.False(error.IsVisible);

            viewModel.ViewState = DataGridGeneratedViewState.Empty;
            Dispatcher.UIThread.RunJobs();
            Assert.True(empty.IsVisible);
            Assert.False(loading.IsVisible);

            retry.Command!.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(DataGridGeneratedViewState.Content, viewModel.ViewState);
            Assert.True(grid.IsVisible);
            Assert.False(empty.IsVisible);
            Assert.Equal(3, viewModel.Items.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_routed_event_bridge_executes_typed_reactive_command_and_propagates_feedback()
    {
        var viewModel = new GeneratedReactiveEventCommandsViewModel();
        var view = new GeneratedReactiveEventCommandsPage(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            GeneratedEventCommandRow first = viewModel.Items[0];
            GeneratedEventCommandRow second = viewModel.Items[1];
            DataGridColumn firstColumn = grid.Columns[0];
            DataGridColumn secondColumn = grid.Columns[1];

            int eventCount = viewModel.EventCount;
            var selection = new DataGridSelectionChangedEventArgs(
                DataGrid.SelectionChangedEvent,
                new ArrayList(),
                new ArrayList { first },
                DataGridSelectionChangeSource.Pointer,
                new RoutedEventArgs());
            grid.RaiseEvent(selection);

            Assert.Equal(eventCount + 1, viewModel.EventCount);
            Assert.NotNull(viewModel.LastEventData);
            Assert.Equal(DataGridGeneratedViewEventKinds.SelectionChanged, viewModel.LastEventData.Kind);
            Assert.Same(first, viewModel.LastEventData.AddedItems[0]);
            Assert.Equal(DataGridSelectionChangeSource.Pointer, viewModel.LastEventData.SelectionSource);
            Assert.True(viewModel.LastEventData.IsUserInitiated);
            Assert.StartsWith("SelectionChanged #", first.LastEvent);

            var current = new DataGridCurrentCellChangedEventArgs(
                firstColumn,
                first,
                secondColumn,
                second,
                DataGrid.CurrentCellChangedEvent,
                grid);
            grid.RaiseEvent(current);

            Assert.Equal(DataGridGeneratedViewEventKinds.CurrentCellChanged, viewModel.LastEventData.Kind);
            Assert.Same(first, viewModel.LastEventData.OldItem);
            Assert.Same(second, viewModel.LastEventData.NewItem);
            Assert.Equal(firstColumn.ColumnKey?.ToString(), viewModel.LastEventData.OldColumnKey);
            Assert.Equal(secondColumn.ColumnKey?.ToString(), viewModel.LastEventData.NewColumnKey);

            viewModel.HandleSortingRequests = true;
            var sorting = new DataGridColumnEventArgs(firstColumn, DataGrid.SortingEvent, grid);
            grid.RaiseEvent(sorting);
            Assert.True(sorting.Handled);
            Assert.Equal(DataGridGeneratedViewEventKinds.Sorting, viewModel.LastEventData.Kind);
            Assert.Equal(firstColumn.ColumnKey?.ToString(), viewModel.LastEventData.ColumnKey);

            viewModel.CancelPendingEdits = true;
            var row = new DataGridRow { DataContext = first };
            var beginningEdit = new DataGridBeginningEditEventArgs(
                firstColumn,
                row,
                new RoutedEventArgs(),
                DataGrid.BeginningEditEvent,
                grid);
            grid.RaiseEvent(beginningEdit);

            Assert.True(beginningEdit.Cancel);
            Assert.Equal(DataGridGeneratedViewEventKinds.BeginningEdit, viewModel.LastEventData.Kind);
            Assert.Same(first, viewModel.LastEventData.Item);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Generated_reactive_interaction_handler_is_typed_and_scoped_to_view_activation()
    {
        var viewModel = new GeneratedReactiveEventCommandsViewModel();
        var view = new GeneratedReactiveEventCommandsPage(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();

        string response = await viewModel.InspectGeneratedGrid
            .Handle(viewModel.Items[0])
            .ToTask();

        Assert.Equal($"AVLN: generated view has {grid.Columns.Count} typed columns.", response);

        var replacementViewModel = new GeneratedReactiveEventCommandsViewModel();
        view.DataContext = replacementViewModel;
        Dispatcher.UIThread.RunJobs();

        await Assert.ThrowsAnyAsync<Exception>(() => viewModel.InspectGeneratedGrid
            .Handle(viewModel.Items[0])
            .ToTask());

        string replacementResponse = await replacementViewModel.InspectGeneratedGrid
            .Handle(replacementViewModel.Items[1])
            .ToTask();
        Assert.Equal($"RXUI: generated view has {grid.Columns.Count} typed columns.", replacementResponse);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        await Assert.ThrowsAnyAsync<Exception>(() => replacementViewModel.InspectGeneratedGrid
            .Handle(replacementViewModel.Items[0])
            .ToTask());
    }

    [AvaloniaFact]
    public void Generated_view_remains_fully_customizable_by_subclassing_hooks()
    {
        var view = new CustomizedGeneratedView();
        DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();

        Assert.False(grid.CanUserSortColumns);
        Assert.Equal("customized", grid.Tag);
    }

    [Fact]
    public void Generated_virtualization_view_model_handles_typed_input_without_a_grid_reference()
    {
        var viewModel = new GeneratedVirtualizationProfileViewModel();
        var input = new DataGridGeneratedInputEvent<GeneratedVirtualizationRow>(
            DataGridGeneratedInputAction.FillDown,
            Key.D,
            KeyModifiers.Control,
            viewModel.Items[0],
            rowIndex: 0,
            columnIndex: 2);

        ((System.Windows.Input.ICommand)viewModel.InputCommand).Execute(input);

        Assert.Same(input, viewModel.LastInput);
        Assert.Contains("FillDown", viewModel.LastAction, StringComparison.Ordinal);
        Assert.Contains("Streaming", viewModel.LastAction, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Generated_virtualization_profile_applies_input_and_activation_scoped_metrics()
    {
        var viewModel = new GeneratedVirtualizationProfileViewModel();
        var view = new TestGeneratedVirtualizationProfilePage(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();

        Assert.True(grid.UseLogicalScrollable);
        Assert.IsType<AdvancedRowHeightEstimator>(grid.RowHeightEstimator);
        Assert.Equal(Key.J, grid.KeyboardGestureOverrides.MoveDown.Key);
        Assert.Equal(Key.K, grid.KeyboardGestureOverrides.MoveUp.Key);

        KeyModifiers commandModifiers =
            view.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        var keyArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = Key.F,
            KeyModifiers = commandModifiers,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };
        grid.RaiseEvent(keyArgs);

        Assert.True(keyArgs.Handled);
        Assert.NotNull(viewModel.LastInput);
        Assert.Equal(DataGridGeneratedInputAction.Search, viewModel.LastInput!.Action);

        using (var meter = new Meter(DataGridGeneratedMetricsBridge.MeterName, "sample-tests"))
        {
            Counter<long> realized = meter.CreateCounter<long>("prodatagrid.rows.realized.count");
            realized.Add(1);
        }
        Assert.True(view.MetricsSink.MeasurementCount > 0);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.MetricsSink.IsDisposed);
    }

    [AvaloniaFact]
    public async Task Generated_navigation_interaction_moves_current_cell_and_round_trips_scroll_state()
    {
        var viewModel = new GeneratedVirtualizationProfileViewModel();
        for (int index = 4; index <= 80; index++)
        {
            viewModel.Items.Add(new GeneratedVirtualizationRow
            {
                Id = index,
                Workload = $"Generated workload {index}",
                Description = "Deterministic navigation and scroll-state coverage.",
                UpdatesPerSecond = index * 100d
            });
        }
        var view = new GeneratedVirtualizationProfilePage(viewModel);
        var window = new Window { Width = 900, Height = 560, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            GeneratedVirtualizationRow second = viewModel.Items[1];

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> selected = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.SetCurrentCell(
                    second,
                    "Workload"))
                .ToTask();

            Assert.True(selected.Succeeded);
            Assert.Same(second, selected.Item);
            Assert.Equal("Workload", selected.ColumnKey);
            Assert.Same(second, grid.CurrentCell.Item);

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> moved = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.MoveCurrentCell(
                    columnOffset: 1,
                    rowOffset: 1))
                .ToTask();

            Assert.True(moved.Succeeded);
            Assert.Same(viewModel.Items[2], moved.Item);
            Assert.Equal("Description", moved.ColumnKey);

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> far = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.SetCurrentCell(
                    viewModel.Items[60],
                    "Description"))
                .ToTask();
            Assert.True(far.Succeeded);
            Dispatcher.UIThread.RunJobs();

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> captured = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.CaptureScrollState())
                .ToTask();
            Assert.True(captured.Succeeded);
            Assert.NotNull(captured.ScrollState);

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> reset = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.SetCurrentCell(
                    viewModel.Items[0],
                    "Id"))
                .ToTask();
            Assert.True(reset.Succeeded);
            Dispatcher.UIThread.RunJobs();

            DataGridGeneratedNavigationResult<GeneratedVirtualizationRow> restored = await viewModel.Navigation
                .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.RestoreScrollState(
                    captured.ScrollState))
                .ToTask();
            Assert.True(restored.Succeeded);
            Assert.Same(captured.ScrollState, restored.ScrollState);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-navigation-interaction.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => viewModel.Navigation
            .Handle(DataGridGeneratedNavigationRequest<GeneratedVirtualizationRow>.QueryCurrentCell())
            .ToTask());
    }

    [AvaloniaFact]
    public void Generated_operations_page_binds_named_controller_models_and_generated_search_box()
    {
        using var viewModel = new GeneratedOperationsControllerViewModel();
        var view = new GeneratedOperationsControllerPage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>()
                .Single(static textBox => textBox.Name == "GeneratedSearchBox");

            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);
            Assert.Equal(viewModel.ColumnDefinitions.Count, grid.Columns.Count);

            searchBox.Text = "Warsaw";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Warsaw", viewModel.Query);
            Assert.Equal(2, viewModel.SearchModel.Descriptors.Count);
            Assert.True(viewModel.Operations.SearchPredicate(viewModel.Items[0]));
            Assert.False(viewModel.Operations.SearchPredicate(viewModel.Items[1]));

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-operations-controller.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_source_list_page_binds_and_filters_the_owned_dynamic_data_pipeline()
    {
        using var viewModel = new GeneratedDynamicDataSourceListViewModel();
        var view = new GeneratedDynamicDataSourceListPage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>()
                .Single(static textBox => textBox.Name == "GeneratedSearchBox");

            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);

            searchBox.Text = "Warsaw";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Warsaw", viewModel.Query);
            Assert.NotEmpty(viewModel.Items);
            Assert.All(viewModel.Items, static trade => Assert.Equal("Warsaw", trade.Desk));

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-dynamic-data-source-list.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_source_cache_page_binds_identity_selection_and_preserves_keyed_replacements()
    {
        using var viewModel = new GeneratedDynamicDataSourceCacheViewModel();
        var view = new GeneratedDynamicDataSourceCachePage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>()
                .Single(static textBox => textBox.Name == "GeneratedSearchBox");

            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);
            Assert.Same(viewModel.SelectionModel, grid.Selection);

            searchBox.Text = "London";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("London", viewModel.Query);
            Assert.NotEmpty(viewModel.Items);
            Assert.All(viewModel.Items, static trade => Assert.Equal("London", trade.Desk));

            searchBox.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            GeneratedTrade original = viewModel.Items.Single(static trade => trade.Id == 8);
            viewModel.RunReplacementScenarioCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();

            GeneratedTrade replacement = viewModel.Items.Single(static trade => trade.Id == 8);
            Assert.NotSame(original, replacement);
            Assert.Same(replacement, grid.SelectedItem);
            Assert.Equal(8, viewModel.SelectedKey);
            Assert.Equal(999m, viewModel.SelectedPrice);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-dynamic-data-source-cache.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Generated_hierarchical_dynamic_data_page_binds_typed_model_and_compiled_wrapper_columns()
    {
        using var viewModel = new GeneratedHierarchicalDynamicDataViewModel();
        var view = new GeneratedHierarchicalDynamicDataPage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>()
                .Single(static textBox => textBox.Name == "GeneratedSearchBox");

            Assert.True(grid.HierarchicalRowsEnabled);
            Assert.Contains("hierarchical", grid.Classes);
            Assert.Same(viewModel.HierarchicalModel, grid.HierarchicalModel);
            Assert.Same(((IHierarchicalModel)viewModel.HierarchicalModel).ObservableFlattened, grid.ItemsSource);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);
            Assert.Equal(viewModel.ColumnDefinitions.Count, grid.Columns.Count);
            Assert.Equal(20, grid.ItemsSource!.Cast<object>().Count());
            Assert.All(grid.ItemsSource.Cast<object>(), static item =>
            {
                HierarchicalNode node = Assert.IsType<HierarchicalNode>(item);
                Assert.IsType<GeneratedHierarchyNode>(node.Item);
            });

            searchBox.Text = "Warsaw";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Warsaw", viewModel.Query);
            Assert.Single(viewModel.Items);
            Assert.Equal(5, viewModel.VisibleNodeCount);

            searchBox.Text = string.Empty;
            Dispatcher.UIThread.RunJobs();
            GeneratedHierarchyNode original = viewModel.Items[0];
            viewModel.RefreshRootsCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            Assert.NotSame(original, viewModel.Items[0]);
            Assert.Equal(20, viewModel.VisibleNodeCount);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-hierarchical-dynamic-data.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Generated_remote_query_page_projects_paging_stale_and_error_state()
    {
        using var viewModel = new GeneratedRemoteQueryViewModel();
        await viewModel.Initialization;
        var view = new GeneratedRemoteQueryPage { DataContext = viewModel };
        var window = new Window { Width = 1000, Height = 640, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            TextBox searchBox = view.GetLogicalDescendants().OfType<TextBox>()
                .Single(static textBox => textBox.Name == "GeneratedSearchBox");
            Control error = view.GetLogicalDescendants().OfType<Control>()
                .Single(control => control.Name == "GeneratedErrorState");
            Button retry = view.GetLogicalDescendants().OfType<Button>()
                .Single(control => control.Name == "GeneratedRetryButton");

            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.SortingModel, grid.SortingModel);
            Assert.Same(viewModel.FilteringModel, grid.FilteringModel);
            Assert.Same(viewModel.SearchModel, grid.SearchModel);
            Assert.Equal(viewModel.ColumnDefinitions.Count, grid.Columns.Count);
            Assert.True(grid.IsVisible);
            Assert.False(error.IsVisible);

            searchBox.Text = "Contoso";
            Dispatcher.UIThread.RunJobs();
            await viewModel.LoadFirstPageCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Contoso", viewModel.Query);
            Assert.NotEmpty(viewModel.Items);
            Assert.All(viewModel.Items, static item => Assert.StartsWith("Contoso", item.Customer, StringComparison.Ordinal));

            await viewModel.SimulateErrorCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.True(error.IsVisible);
            Assert.False(grid.IsVisible);
            Assert.Same(viewModel.RetryCommand, retry.Command);

            await viewModel.RetryCommand.Execute().ToTask();
            await viewModel.RunStaleScenarioCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.True(grid.IsVisible);
            Assert.False(error.IsVisible);
            Assert.True(viewModel.StaleResponseCount >= 1);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-remote-query.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Generated_selection_state_page_round_trips_all_sections_and_legacy_aliases()
    {
        using var viewModel = new GeneratedSelectionStateViewModel();
        var view = new GeneratedSelectionStatePage { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 680, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            DataGridColumn idColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "id"));
            DataGridColumn symbolColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "symbol"));
            DataGridColumn deskColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "desk"));
            DataGridLength originalDeskWidth = deskColumn.Width;

            Assert.Same(viewModel.SelectionModel, grid.Selection);
            Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
            Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
            Assert.False(viewModel.SelectionModel.SingleSelect);
            Assert.Equal(viewModel.ColumnDefinitions.Count, grid.Columns.Count);

            await viewModel.PrepareStateCommand.Execute().ToTask();
            Assert.Equal(
                new[] { 4, 1 },
                viewModel.SelectionModel.SelectedItems.Cast<GeneratedFeatureRow>().Select(static item => item.Id));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, viewModel.SortingModel.Descriptors.Count);
            Assert.Single(viewModel.FilteringModel.Descriptors);
            Assert.Equal(2, viewModel.SearchModel.Descriptors.Count);
            Assert.False(viewModel.SelectionModel.SingleSelect);
            Assert.Contains(
                4,
                viewModel.SelectionModel.Source.Cast<GeneratedFeatureRow>().Select(static item => item.Id));
            Assert.Equal(
                new[] { 4, 1 },
                viewModel.SelectionModel.SelectedItems.Cast<GeneratedFeatureRow>().Select(static item => item.Id));
            Assert.Equal(
                new[] { 4, 1 },
                grid.SelectedItems.Cast<GeneratedFeatureRow>().Select(static item => item.Id));
            Assert.Equal(new[] { 1, 4 }, viewModel.SelectionController.SelectedItemKeys.Order());

            await viewModel.CaptureStateCommand.Execute().ToTask();
            Assert.NotNull(viewModel.StatePayload);
            Assert.True(viewModel.StatePayloadLength > 100);
            Assert.Contains("sample/generated-feature-row/v2", viewModel.StatePayload, StringComparison.Ordinal);

            await viewModel.ScrambleStateCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(viewModel.SortingModel.Descriptors);
            Assert.Empty(viewModel.FilteringModel.Descriptors);
            Assert.Empty(viewModel.SearchModel.Descriptors);
            Assert.Empty(viewModel.SelectionController.SelectedItemKeys);
            Assert.Equal(grid.Columns.Count - 1, idColumn.DisplayIndex);
            Assert.False(symbolColumn.IsVisible);
            Assert.Equal(new DataGridLength(240), deskColumn.Width);

            await viewModel.RestoreStateCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, viewModel.SortingModel.Descriptors.Count);
            Assert.Single(viewModel.FilteringModel.Descriptors);
            Assert.Equal(2, viewModel.SearchModel.Descriptors.Count);
            Assert.Equal(new[] { 1, 4 }, viewModel.SelectionController.SelectedItemKeys.Order());
            Assert.Equal(0, idColumn.DisplayIndex);
            Assert.True(symbolColumn.IsVisible);
            Assert.Equal(originalDeskWidth, deskColumn.Width);

            await viewModel.LegacyRoundTripCommand.Execute().ToTask();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, viewModel.MigrationCount);
            Assert.Contains("Migrated version 1", viewModel.Status, StringComparison.Ordinal);
            Assert.Equal(new[] { 1, 4 }, viewModel.SelectionController.SelectedItemKeys.Order());

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-selection-state.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_grouping_summary_page_materializes_typed_groups_and_rendered_summary_metadata()
    {
        var viewModel = new GeneratedGroupingSummariesViewModel();
        var view = new GeneratedGroupingSummariesPage { DataContext = viewModel };
        var window = new Window { Width = 1180, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.True(grid.ShowTotalSummary);
            Assert.True(grid.ShowGroupSummary);
            Assert.Equal(DataGridSummaryRowPosition.Bottom, grid.TotalSummaryPosition);
            Assert.Equal(DataGridGroupSummaryPosition.Footer, grid.GroupSummaryPosition);
            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Equal(2, viewModel.Items.GroupDescriptions.Count);
            Assert.All(
                viewModel.Items.GroupDescriptions,
                static description => Assert.StartsWith("DataGridGeneratedGroupDescription", description.GetType().Name, StringComparison.Ordinal));

            DataGridColumn orderColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "order-id"));
            DataGridColumn revenueColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "revenue"));
            DataGridColumn unitPriceColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "unit-price"));
            Assert.Single(orderColumn.Summaries);
            Assert.Equal("Orders: ", orderColumn.Summaries[0].Title);
            Assert.Equal(DataGridSummaryScope.Both, revenueColumn.Summaries[0].Scope);
            Assert.Equal("C2", revenueColumn.Summaries[0].StringFormat);
            Assert.Equal(DataGridSummaryScope.Total, unitPriceColumn.Summaries[0].Scope);

            grid.RecalculateSummaries();
            Dispatcher.UIThread.RunJobs();
            DataGridSummaryCell[] renderedSummaryCells = grid.GetVisualDescendants().OfType<DataGridSummaryCell>().ToArray();
            Assert.Contains(renderedSummaryCells, static cell => cell.DisplayText == "Orders: 12");
            Assert.Contains(
                renderedSummaryCells,
                static cell => cell.Column?.ColumnKey?.ToString() == "revenue" &&
                               cell.DisplayText.StartsWith("Revenue: ", StringComparison.Ordinal));

            viewModel.AddBatchCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            grid.RecalculateSummaries();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(15, grid.ItemsSource!.Cast<object>().Count());
            Assert.Equal(15, viewModel.OrderCount);
            Assert.Contains(
                grid.GetVisualDescendants().OfType<DataGridSummaryCell>(),
                static cell => cell.DisplayText == "Orders: 15");

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-grouping-summaries.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_indexed_spreadsheet_page_binds_formulas_and_replaces_runtime_columns()
    {
        using var viewModel = new GeneratedIndexedSpreadsheetViewModel();
        var view = new GeneratedIndexedSpreadsheetPage { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 780, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.ColumnDefinitions, grid.ColumnDefinitionsSource);
            Assert.Same(viewModel.FormulaModel, grid.FormulaModel);
            Assert.Same(viewModel.FastPathOptions, grid.FastPathOptions);
            Assert.True(grid.FastPathOptions.StrictMode);
            Assert.Equal(10, grid.Columns.Count);
            Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
            Assert.Equal(DataGridSelectionUnit.CellOrRowHeader, grid.SelectionUnit);

            viewModel.FormulaModel.Recalculate();
            Dispatcher.UIThread.RunJobs();
            GeneratedSpreadsheetRow first = viewModel.Items[0];
            double expectedTotal = (double)first.GetCell(1)! * (double)first.GetCell(2)! *
                (1d - (double)first.GetCell(3)!);
            object? evaluatedTotal = viewModel.EvaluateFormula(0, 4);
            Assert.True(evaluatedTotal is double, $"Expected E1 to be numeric, but it was '{evaluatedTotal ?? "<null>"}'.");
            Assert.Equal(expectedTotal, (double)evaluatedTotal, precision: 8);
            Assert.Equal(expectedTotal / 5d, Assert.IsType<double>(viewModel.EvaluateFormula(0, 6)), precision: 8);
            Assert.Equal(expectedTotal + (double)first.GetCell(8)!, Assert.IsType<double>(viewModel.EvaluateFormula(0, 9)), precision: 8);

            viewModel.ApplyCellFormulaCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            object? evaluatedOverride = viewModel.EvaluateFormula(0, 7);
            Assert.True(evaluatedOverride is double, $"Expected H1 to be numeric, but it was '{evaluatedOverride ?? "<null>"}'.");
            Assert.Equal(150d, (double)evaluatedOverride, precision: 8);

            viewModel.AddColumnCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(11, grid.Columns.Count);
            Assert.Equal("K", grid.Columns[^1].ColumnKey);
            viewModel.AddColumnCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(12, grid.Columns.Count);
            Assert.Equal("L", grid.Columns[^1].ColumnKey);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-indexed-spreadsheet.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_conditional_formatting_page_binds_and_renders_typed_cell_and_row_rules()
    {
        var viewModel = new GeneratedConditionalFormattingViewModel();
        var view = new GeneratedConditionalFormattingPage { DataContext = viewModel };
        var window = new Window { Width = 1160, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(viewModel.Items, grid.ItemsSource);
            Assert.Same(viewModel.ConditionalFormatting, grid.ConditionalFormattingModel);
            Assert.Equal(7, grid.ConditionalFormattingModel.Descriptors.Count);
            Assert.True(grid.FastPathOptions.StrictMode);

            GeneratedConditionalFormattingRow first = viewModel.Items[0];
            DataGridRow row = grid.GetVisualDescendants().OfType<DataGridRow>()
                .Single(candidate => ReferenceEquals(candidate.DataContext, first));
            DataGridColumn scoreColumn = grid.Columns.Single(column => Equals(column.ColumnKey, "score"));
            DataGridCell scoreCell = row.GetVisualDescendants().OfType<DataGridCell>()
                .Single(cell => ReferenceEquals(cell.OwningColumn, scoreColumn));
            ControlTheme alertTheme = Assert.IsType<ControlTheme>(view.Resources["GeneratedRowAlertTheme"]);
            ControlTheme lowScoreTheme = Assert.IsType<ControlTheme>(view.Resources["GeneratedScoreLowCellTheme"]);
            Assert.Same(alertTheme, row.Theme);
            Assert.Same(lowScoreTheme, scoreCell.Theme);

            first.Score = 96d;
            first.Change = 4d;
            first.Target = 80d;
            first.Status = "On Track";
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            ControlTheme highScoreTheme = Assert.IsType<ControlTheme>(view.Resources["GeneratedScoreHighCellTheme"]);
            Assert.Same(highScoreTheme, scoreCell.Theme);
            Assert.NotSame(alertTheme, row.Theme);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-conditional-formatting.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_pivot_chart_page_binds_source_and_pivot_charts_without_property_paths()
    {
        using var viewModel = new GeneratedPivotChartViewModel();
        var view = new GeneratedPivotChartPage { DataContext = viewModel };
        var window = new Window { Width = 1180, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid sourceGrid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            Assert.Equal("generated-pivot-chart-grid", AutomationProperties.GetAutomationId(sourceGrid));
            Assert.Same(viewModel.Items, sourceGrid.ItemsSource);
            Assert.True(sourceGrid.FastPathOptions.StrictMode);
            Assert.Equal(7, viewModel.ColumnDefinitions.Count);
            Assert.Equal(7, sourceGrid.Columns.Count(static column => column.ColumnKey != null));
            ProChartView directChart = view.GetVisualDescendants().OfType<ProChartView>().Single();
            Assert.Same(viewModel.DirectChartModel, directChart.ChartModel);

            TabControl tabs = view.GetLogicalDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            view.UpdateLayout();

            DataGrid pivotGrid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            Assert.Same(viewModel.Pivot.Rows, pivotGrid.ItemsSource);
            Assert.Equal(viewModel.Pivot.ColumnDefinitions.Count, pivotGrid.Columns.Count);
            ProChartView pivotChart = view.GetVisualDescendants().OfType<ProChartView>().Single();
            Assert.Same(viewModel.PivotChartModel, pivotChart.ChartModel);
            Assert.NotEmpty(viewModel.PivotChartModel.Snapshot.Series);

            viewModel.AddPeriodCommand.Execute().Subscribe();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(15, viewModel.SourceRowCount);
            Assert.NotEmpty(viewModel.Pivot.Rows);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-pivot-chart.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_reactive_view_recipes_expose_distinct_layout_slots_and_shared_bindings()
    {
        using var viewModel = new GeneratedReactiveViewRecipesViewModel();
        var view = new GeneratedReactiveViewRecipesPage { DataContext = viewModel };
        var window = new Window { Width = 1120, Height = 720, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            TabControl tabs = view.GetLogicalDescendants().OfType<TabControl>().Single();
            DataGrid gridOnly = Assert.Single(view.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal("generated-recipe-grid-only", AutomationProperties.GetAutomationId(gridOnly));
            Assert.Same(viewModel.Items, gridOnly.ItemsSource);
            Assert.True(gridOnly.FastPathOptions.StrictMode);
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<ContentControl>(),
                static control => control.Name is "GeneratedToolbarSlot" or "GeneratedExplorerSlot");

            tabs.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            view.UpdateLayout();
            DataGrid explorerGrid = Assert.Single(view.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal("generated-recipe-explorer", AutomationProperties.GetAutomationId(explorerGrid));
            Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), static control => control.Name == "GeneratedSearchBox");
            Assert.Contains(view.GetVisualDescendants().OfType<ContentControl>(), static control => control.Name == "GeneratedToolbarSlot");
            Assert.Contains(view.GetVisualDescendants().OfType<ContentControl>(), static control => control.Name == "GeneratedExplorerSlot");

            tabs.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            view.UpdateLayout();
            DataGrid spreadsheetGrid = Assert.Single(view.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal("generated-recipe-spreadsheet", AutomationProperties.GetAutomationId(spreadsheetGrid));
            Assert.False(spreadsheetGrid.IsReadOnly);
            Assert.Contains(view.GetVisualDescendants().OfType<ContentControl>(), static control => control.Name == "GeneratedFormulaBarSlot");

            tabs.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            view.UpdateLayout();
            DataGrid analyticsGrid = Assert.Single(view.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal("generated-recipe-analytics", AutomationProperties.GetAutomationId(analyticsGrid));
            Assert.Contains(view.GetVisualDescendants().OfType<TextBox>(), static control => control.Name == "GeneratedSearchBox");
            Assert.Contains(view.GetVisualDescendants().OfType<ContentControl>(), static control => control.Name == "GeneratedAnalyticsSlot");

            viewModel.Query = "Runtime";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, viewModel.VisibleRowCount);
            Assert.Single(analyticsGrid.ItemsSource!.Cast<object>());

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-reactive-view-recipes.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Generated_editing_clipboard_fill_page_binds_and_executes_typed_datagrid_adapters()
    {
        using var viewModel = new GeneratedEditingClipboardFillViewModel();
        var view = new GeneratedEditingClipboardFillPage { DataContext = viewModel };
        var window = new Window { Width = 1180, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            Assert.Same(viewModel.ItemsView, grid.ItemsSource);
            Assert.Same(viewModel.ClipboardImportModel, grid.ClipboardImportModel);
            Assert.Same(viewModel.FillModel, grid.FillModel);
            Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
            Assert.Equal(DataGridSelectionUnit.CellOrRowHeader, grid.SelectionUnit);
            Assert.Equal(
                DataGridEditTriggers.CellDoubleClick | DataGridEditTriggers.TextInput | DataGridEditTriggers.F2,
                grid.EditTriggers);
            Assert.Equal(DataGridClipboardCopyMode.IncludeHeader, grid.ClipboardCopyMode);
            Assert.False(grid.IsReadOnly);
            Assert.False(grid.CanUserAddRows);
            Assert.False(grid.CanUserDeleteRows);
            Assert.Equal(viewModel.ColumnDefinitions.Count, grid.Columns.Count);

            DataGridColumn productColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "product"));
            int productColumnIndex = grid.Columns.IndexOf(productColumn);
            var paste = new DataGridClipboardImportContext(
                grid,
                "grid adapter",
                [new DataGridCellInfo(viewModel.Items[0], productColumn, 0, productColumnIndex)]);
            Assert.True(viewModel.ClipboardImportModel.Paste(paste));
            Assert.Equal("GRID ADAPTER", viewModel.Items[0].Product);
            Assert.Equal(1, viewModel.LastAppliedCells);

            DataGridColumn quantityColumn = grid.Columns.Single(static column => Equals(column.ColumnKey, "quantity"));
            int quantityColumnIndex = grid.Columns.IndexOf(quantityColumn);
            viewModel.FillModel.ApplyFill(new DataGridFillContext(
                grid,
                new DataGridCellRange(0, 1, quantityColumnIndex, quantityColumnIndex),
                new DataGridCellRange(0, 3, quantityColumnIndex, quantityColumnIndex)));
            Assert.Equal(new[] { 10, 20, 30, 40 }, viewModel.Items.Take(4).Select(static item => item.Quantity));
            Assert.Equal(2, viewModel.LastAppliedCells);

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-editing-clipboard-fill.png"));
                using FileStream stream = File.Create(path);
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Assert.True(new FileInfo(path).Length > 0);
            }

            Assert.True(viewModel.EditController.Undo());
            Assert.Equal(new[] { 10, 20, 5, 8 }, viewModel.Items.Take(4).Select(static item => item.Quantity));
            Assert.True(viewModel.EditController.Undo());
            Assert.Equal("ALPHA", viewModel.Items[0].Product);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Explorer_recipe_exposes_automation_and_named_slots_and_can_capture_populated_view()
    {
        var viewModel = new GeneratedColumnsAttributesViewModel();
        var view = new GeneratedColumnsCodeView(viewModel);
        var window = new Window
        {
            Width = 1000,
            Height = 640,
            Content = view
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            DataGrid grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            ContentControl toolbar = view.GetLogicalDescendants().OfType<ContentControl>()
                .Single(control => control.Name == "GeneratedToolbarSlot");
            ContentControl explorer = view.GetLogicalDescendants().OfType<ContentControl>()
                .Single(control => control.Name == "GeneratedExplorerSlot");

            Assert.Equal("generated-columns-code-grid", AutomationProperties.GetAutomationId(grid));
            Assert.Equal("generated-columns-code-grid-toolbar", AutomationProperties.GetAutomationId(toolbar));
            Assert.Equal("generated-columns-code-grid-recipe", AutomationProperties.GetAutomationId(explorer));
            Assert.Equal(3, grid.ItemsSource!.Cast<object>().Count());

            string? screenshotDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(screenshotDirectory))
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Directory.CreateDirectory(screenshotDirectory);
                string path = Path.GetFullPath(Path.Combine(screenshotDirectory, "generated-explorer-recipe.png"));
                using (FileStream stream = File.Create(path))
                {
                    frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                }
                Assert.True(new FileInfo(path).Length > 0);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class CustomizedGeneratedView : GeneratedColumnsCodeView
    {
        protected override void ConfigureGeneratedDataGrid(DataGrid dataGrid)
        {
            dataGrid.CanUserSortColumns = false;
            dataGrid.Tag = "customized";
        }
    }

    private sealed class TestGeneratedVirtualizationProfilePage : GeneratedVirtualizationProfilePage
    {
        public TestGeneratedVirtualizationProfilePage(GeneratedVirtualizationProfileViewModel viewModel)
            : base(viewModel)
        {
        }

        public GeneratedVirtualizationMetricsSink MetricsSink { get; } = new();

        protected override IDataGridGeneratedMetricsSink CreateGeneratedMetricsSink() => MetricsSink;
    }
}
