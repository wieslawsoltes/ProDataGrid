using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ProDiagnostics.Viewer.ViewModels;

namespace ProDiagnostics.Viewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private DataGrid? _metricsGrid;
    private DataGrid? _activitiesGrid;
    private bool _metricsGridHandlersAttached;
    private readonly List<(ColumnVisibilityOption option, PropertyChangedEventHandler handler)> _columnVisibilityHandlers = new();

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        WireMetricsGridHandlers();
        WireColumnVisibility();
        Closed += (_, _) =>
        {
            DetachMetricsGridHandlers();
            DetachColumnVisibility();
            _viewModel.Dispose();
        };
    }

    private void WireMetricsGridHandlers()
    {
        _metricsGrid = this.FindControl<DataGrid>("MetricsGrid");
        if (_metricsGrid == null)
        {
            return;
        }

        _metricsGrid.AttachedToVisualTree += MetricsGrid_AttachedToVisualTree;
        _metricsGrid.DetachedFromVisualTree += MetricsGrid_DetachedFromVisualTree;

        if (_metricsGrid.IsAttachedToVisualTree())
        {
            AttachMetricsGridHandlers(_metricsGrid);
        }
    }

    private void MetricsGrid_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachMetricsGridHandlers(sender as DataGrid);
    }

    private void MetricsGrid_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachMetricsGridHandlers(sender as DataGrid);
    }

    private void AttachMetricsGridHandlers(DataGrid? grid)
    {
        if (grid == null || _metricsGridHandlersAttached)
        {
            return;
        }

        grid.CellPointerPressed += MetricsGrid_CellPointerPressed;
        _metricsGridHandlersAttached = true;
    }

    private void DetachMetricsGridHandlers(DataGrid? grid = null)
    {
        if (!_metricsGridHandlersAttached)
        {
            return;
        }

        var target = grid ?? _metricsGrid;
        if (target != null)
        {
            target.CellPointerPressed -= MetricsGrid_CellPointerPressed;
        }

        _metricsGridHandlersAttached = false;
    }

    private void MetricsGrid_CellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        var args = e.PointerPressedEventArgs;
        if (args.ClickCount != 2)
        {
            return;
        }

        if (!args.GetCurrentPoint(e.Cell).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Row?.DataContext is MetricSeriesViewModel metric)
        {
            _viewModel.OpenMetricTab(metric);
        }
    }

    private void WireColumnVisibility()
    {
        _metricsGrid ??= this.FindControl<DataGrid>("MetricsGrid");
        _activitiesGrid ??= this.FindControl<DataGrid>("ActivitiesGrid");

        BindColumnVisibility(_viewModel.GetMetricColumn("metric"), FindColumn(_metricsGrid, "Metric"));
        BindColumnVisibility(_viewModel.GetMetricColumn("description"), FindColumn(_metricsGrid, "Description"));
        BindColumnVisibility(_viewModel.GetMetricColumn("meter"), FindColumn(_metricsGrid, "Meter"));
        BindColumnVisibility(_viewModel.GetMetricColumn("unit"), FindColumn(_metricsGrid, "Unit"));
        BindColumnVisibility(_viewModel.GetMetricColumn("last"), FindColumn(_metricsGrid, "Last"));
        BindColumnVisibility(_viewModel.GetMetricColumn("avg"), FindColumn(_metricsGrid, "Avg"));
        BindColumnVisibility(_viewModel.GetMetricColumn("min"), FindColumn(_metricsGrid, "Min"));
        BindColumnVisibility(_viewModel.GetMetricColumn("max"), FindColumn(_metricsGrid, "Max"));
        BindColumnVisibility(_viewModel.GetMetricColumn("samples"), FindColumn(_metricsGrid, "Samples"));
        BindColumnVisibility(_viewModel.GetMetricColumn("trend"), FindColumn(_metricsGrid, "Trend"));
        BindColumnVisibility(_viewModel.GetMetricColumn("tags"), FindColumn(_metricsGrid, "Tags"));

        BindColumnVisibility(_viewModel.GetActivityColumn("started"), FindColumn(_activitiesGrid, "Started"));
        BindColumnVisibility(_viewModel.GetActivityColumn("activity"), FindColumn(_activitiesGrid, "Activity"));
        BindColumnVisibility(_viewModel.GetActivityColumn("duration"), FindColumn(_activitiesGrid, "Duration (ms)"));
        BindColumnVisibility(_viewModel.GetActivityColumn("source"), FindColumn(_activitiesGrid, "Source"));
        BindColumnVisibility(_viewModel.GetActivityColumn("tags"), FindColumn(_activitiesGrid, "Tags"));
    }

    private void BindColumnVisibility(ColumnVisibilityOption? option, DataGridColumn? column)
    {
        if (option == null || column == null)
        {
            return;
        }

        column.IsVisible = option.IsVisible;
        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (args.PropertyName == nameof(ColumnVisibilityOption.IsVisible))
            {
                column.IsVisible = option.IsVisible;
            }
        };

        option.PropertyChanged += handler;
        _columnVisibilityHandlers.Add((option, handler));
    }

    private void DetachColumnVisibility()
    {
        foreach (var (option, handler) in _columnVisibilityHandlers)
        {
            option.PropertyChanged -= handler;
        }

        _columnVisibilityHandlers.Clear();
    }

    private static DataGridColumn? FindColumn(DataGrid? grid, string header)
    {
        if (grid == null)
        {
            return null;
        }

        return grid.Columns.FirstOrDefault(column =>
            column.Header is string text &&
            string.Equals(text, header, StringComparison.Ordinal));
    }
}
