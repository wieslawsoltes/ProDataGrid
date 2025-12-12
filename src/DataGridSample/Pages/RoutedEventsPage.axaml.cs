using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DataGridSample.ViewModels;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.DataGridHierarchical;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using System;
using Avalonia.Threading;

namespace DataGridSample.Pages;

public partial class RoutedEventsPage : UserControl
{
    public RoutedEventsPage()
    {
        InitializeComponent();
        AddHandler(DataGridColumnHeader.LeftClickEvent, OnHeaderLeftClick, RoutingStrategies.Bubble);
        AddHandler(DataGridColumnHeader.HeaderPointerPressedEvent, OnHeaderPointerPressed, RoutingStrategies.Bubble);
        AddHandler(DataGridColumnHeader.HeaderPointerReleasedEvent, OnHeaderPointerReleased, RoutingStrategies.Bubble);
        AddHandler(DataGridHierarchicalPresenter.ToggleRequestedEvent, OnHierarchicalToggleRequested, RoutingStrategies.Bubble);
        DataContextChanged += OnDataContextChanged;
        WireLogsSubscription();
    }

    private RoutedEventsViewModel? Vm => DataContext as RoutedEventsViewModel;

    private void Log(string message) => Vm?.AddLog(message);

    private static string DescribeItem(object? item)
    {
        if (item is RoutedEventsViewModel.SampleItem sample)
        {
            return $"{sample.Name} (Id={sample.Id}, Group={sample.Group}, Category={sample.Category}, Status={sample.Status})";
        }

        return item?.ToString() ?? "null";
    }

    private static string DescribeColumn(DataGridColumn? column)
    {
        if (column == null)
        {
            return "null";
        }

        var header = column.Header ?? "(no header)";
        return $"{header} (DisplayIndex={column.DisplayIndex})";
    }

    private static string DescribeItems(IList items)
    {
        if (items.Count == 0)
        {
            return "[]";
        }

        var parts = new List<string>(items.Count);
        foreach (var item in items)
        {
            parts.Add(DescribeItem(item));
        }

        return "[" + string.Join(", ", parts) + "]";
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        Log($"LoadingRow Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)}");
    }

    private void OnUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        Log($"UnloadingRow Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)}");
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e) =>
        Log($"Sorting Column={DescribeColumn(e.Column)} SortMemberPath={e.Column.SortMemberPath}");

    private void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        e.Column.HeaderPointerPressed += OnColumnHeaderPointerPressedClr;
        e.Column.HeaderPointerReleased += OnColumnHeaderPointerReleasedClr;
        Log($"AutoGeneratingColumn Property={e.PropertyName} Type={e.PropertyType} ColumnHeader={e.Column.Header}");
    }

    private void OnColumnDisplayIndexChanged(object? sender, DataGridColumnEventArgs e) =>
        Log($"ColumnDisplayIndexChanged Column={DescribeColumn(e.Column)}");

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e) =>
        Log($"ColumnReordered Column={DescribeColumn(e.Column)}");

    private void OnColumnReordering(object? sender, DataGridColumnReorderingEventArgs e) =>
        Log($"ColumnReordering Column={DescribeColumn(e.Column)} Cancel={e.Cancel}");

    private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e) =>
        Log($"CellPointerPressed Row={e.Row.Index} Column={DescribeColumn(e.Column)} Item={DescribeItem(e.Row.DataContext)} Button={e.PointerPressedEventArgs.GetCurrentPoint(this).Properties.PointerUpdateKind}");

    private void OnCurrentCellChanged(object? sender, DataGridCurrentCellChangedEventArgs e) =>
        Log($"CurrentCellChanged Old={DescribeColumn(e.OldColumn)} OldItem={DescribeItem(e.OldItem)} -> New={DescribeColumn(e.NewColumn)} NewItem={DescribeItem(e.NewItem)}");

    private void OnHorizontalScroll(object? sender, DataGridScrollEventArgs e) =>
        Log($"HorizontalScroll Type={e.ScrollEventType} NewValue={e.NewValue:F2} Source={e.Source?.GetType().Name}");

    private void OnVerticalScroll(object? sender, DataGridScrollEventArgs e) =>
        Log($"VerticalScroll Type={e.ScrollEventType} NewValue={e.NewValue:F2} Source={e.Source?.GetType().Name}");

    private void OnCopyingRowClipboardContent(object? sender, DataGridRowClipboardEventArgs e) =>
        Log($"CopyingRowClipboardContent IsHeader={e.IsColumnHeadersRow} Item={DescribeItem(e.Item)} Cells={e.ClipboardRowContent.Count}");

    private void OnLoadingRowDetails(object? sender, DataGridRowDetailsEventArgs e) =>
        Log($"LoadingRowDetails Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Details={e.DetailsElement?.GetType().Name}");

    private void OnUnloadingRowDetails(object? sender, DataGridRowDetailsEventArgs e) =>
        Log($"UnloadingRowDetails Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)}");

    private void OnRowDetailsVisibilityChanged(object? sender, DataGridRowDetailsEventArgs e) =>
        Log($"RowDetailsVisibilityChanged Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} DetailsVisible={e.DetailsElement?.IsVisible}");

    private void OnLoadingRowGroup(object? sender, DataGridRowGroupHeaderEventArgs e) =>
        Log($"LoadingRowGroup Property={e.RowGroupHeader.PropertyName}");

    private void OnUnloadingRowGroup(object? sender, DataGridRowGroupHeaderEventArgs e) =>
        Log($"UnloadingRowGroup Property={e.RowGroupHeader.PropertyName}");

    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e) =>
        Log($"BeginningEdit Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Column={DescribeColumn(e.Column)} Trigger={e.EditingEventArgs?.GetType().Name}");

    private void OnCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e) =>
        Log($"CellEditEnding Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Column={DescribeColumn(e.Column)} Action={e.EditAction} Cancel={e.Cancel}");

    private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e) =>
        Log($"CellEditEnded Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Column={DescribeColumn(e.Column)} Action={e.EditAction}");

    private void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e) =>
        Log($"PreparingCellForEdit Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Column={DescribeColumn(e.Column)} EditingElement={e.EditingElement?.GetType().Name}");

    private void OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e) =>
        Log($"RowEditEnding Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Action={e.EditAction} Cancel={e.Cancel}");

    private void OnRowEditEnded(object? sender, DataGridRowEditEndedEventArgs e) =>
        Log($"RowEditEnded Row={e.Row.Index} Item={DescribeItem(e.Row.DataContext)} Action={e.EditAction}");

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        Log($"SelectionChanged Added={e.AddedItems.Count} Removed={e.RemovedItems.Count} AddedItems={DescribeItems(e.AddedItems)} RemovedItems={DescribeItems(e.RemovedItems)}");

    private void OnHeaderLeftClick(object? sender, DataGridColumnHeaderClickEventArgs e) =>
        Log($"HeaderLeftClick SourceHeader={(e.Source as DataGridColumnHeader)?.Content ?? e.Source} Modifiers={e.KeyModifiers}");

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e) =>
        Log($"HeaderPointerPressed Header={(e.Source as DataGridColumnHeader)?.Content ?? e.Source} Button={e.GetCurrentPoint(this).Properties.PointerUpdateKind}");

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        Log($"HeaderPointerReleased Header={(e.Source as DataGridColumnHeader)?.Content ?? e.Source} Button={e.InitialPressMouseButton}");

    private void OnHierarchicalToggleRequested(object? sender, RoutedEventArgs e) =>
        Log("ToggleRequested (hierarchical presenter)");

    private INotifyCollectionChanged? _logCollection;

    private void OnDataContextChanged(object? sender, EventArgs e) => WireLogsSubscription();

    private void WireLogsSubscription()
    {
        if (_logCollection != null)
        {
            _logCollection.CollectionChanged -= LogsOnCollectionChanged;
        }

        _logCollection = Vm?.Logs;
        if (_logCollection != null)
        {
            _logCollection.CollectionChanged += LogsOnCollectionChanged;
            ScheduleScrollToLastLog();
        }
    }

    private void LogsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add ||
            e.Action == NotifyCollectionChangedAction.Reset ||
            e.Action == NotifyCollectionChangedAction.Replace)
        {
            ScheduleScrollToLastLog();
        }
    }

    private void ScheduleScrollToLastLog()
    {
        Dispatcher.UIThread.Post(ScrollToLastLog, DispatcherPriority.Background);
    }

    private void ScrollToLastLog()
    {
        var list = this.FindControl<ListBox>("LogList");
        var logs = Vm?.Logs;
        if (list == null || logs == null || logs.Count == 0)
        {
            return;
        }

        list.SelectedIndex = logs.Count - 1;
        list.ScrollIntoView(logs[logs.Count - 1]);
    }

    private void OnColumnHeaderPointerPressedClr(object? sender, PointerPressedEventArgs e)
    {
        if (sender is DataGridColumn column)
        {
            Log($"Column.HeaderPointerPressed CLR Column={column.Header}");
        }
    }

    private void OnColumnHeaderPointerReleasedClr(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is DataGridColumn column)
        {
            Log($"Column.HeaderPointerReleased CLR Column={column.Header}");
        }
    }
}
