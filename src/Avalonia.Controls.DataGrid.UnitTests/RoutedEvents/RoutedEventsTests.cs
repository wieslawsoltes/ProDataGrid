using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.DataGridTests;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace Avalonia.Controls.DataGridTests.RoutedEvents;

public class RoutedEventsTests
{
    [AvaloniaFact]
    public void LoadingRow_RoutedEvent_Bubbles_And_Calls_CLR_Handler()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var clrHit = false;
        var parentHit = false;

        dataGrid.LoadingRow += (_, _) => clrHit = true;
        parent.AddHandler(DataGrid.LoadingRowEvent, (_, _) => parentHit = true);

        dataGrid.RaiseEvent(new DataGridRowEventArgs(new DataGridRow(), DataGrid.LoadingRowEvent, dataGrid));

        Assert.True(clrHit);
        Assert.True(parentHit);
    }

    [AvaloniaFact]
    public void Sorting_RoutedEvent_Reaches_Parent()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var parentHit = false;
        parent.AddHandler(DataGrid.SortingEvent, (_, e) =>
        {
            parentHit = true;
            Assert.Same(dataGrid, e.Source);
        });

        var column = new DataGridTextColumn();
        dataGrid.RaiseEvent(new DataGridColumnEventArgs(column, DataGrid.SortingEvent, dataGrid));

        Assert.True(parentHit);
    }

    [AvaloniaFact]
    public void HorizontalScroll_RoutedEvent_Reaches_CLR_Handler()
    {
        var dataGrid = new DataGrid();
        var clrHit = false;

        dataGrid.HorizontalScroll += (_, e) =>
        {
            clrHit = true;
            Assert.Equal(ScrollEventType.SmallIncrement, e.ScrollEventType);
            Assert.Equal(1, e.NewValue);
        };

        dataGrid.RaiseEvent(new DataGridScrollEventArgs(ScrollEventType.SmallIncrement, 1, DataGrid.HorizontalScrollEvent, dataGrid));

        Assert.True(clrHit);
    }

    [AvaloniaFact]
    public void Hierarchical_Toggle_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var presenter = new DataGridHierarchicalPresenter();
        parent.Children.Add(presenter);

        var hit = false;
        parent.AddHandler(DataGridHierarchicalPresenter.ToggleRequestedEvent, (_, e) =>
        {
            hit = true;
            Assert.Same(presenter, e.Source);
        });

        presenter.RaiseEvent(new RoutedEventArgs(DataGridHierarchicalPresenter.ToggleRequestedEvent, presenter));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void AutoGeneratingColumn_RoutedEvent_Bubbles_And_Cancel()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var parentHit = false;
        parent.AddHandler(DataGrid.AutoGeneratingColumnEvent, (_, e) =>
        {
            parentHit = true;
            e.Cancel = true;
        });

        var args = new DataGridAutoGeneratingColumnEventArgs(
            "Name",
            typeof(string),
            new DataGridTextColumn(),
            DataGrid.AutoGeneratingColumnEvent,
            dataGrid);

        dataGrid.RaiseEvent(args);

        Assert.True(parentHit);
        Assert.True(args.Cancel);
    }

    [AvaloniaFact]
    public void ColumnReordering_RoutedEvent_Bubbles_And_Cancel()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var bubbleHit = false;
        parent.AddHandler(DataGrid.ColumnReorderingEvent, (_, e) =>
        {
            bubbleHit = true;
            e.Cancel = true;
        });

        var args = new DataGridColumnReorderingEventArgs(new DataGridTextColumn(), DataGrid.ColumnReorderingEvent, dataGrid);
        dataGrid.RaiseEvent(args);

        Assert.True(bubbleHit);
        Assert.True(args.Cancel);
    }

    [AvaloniaFact]
    public void Editing_RoutedEvents_Bubble()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var beginningEditHit = false;
        var rowEditEndingHit = false;

        parent.AddHandler(DataGrid.BeginningEditEvent, (_, e) =>
        {
            beginningEditHit = true;
            e.Cancel = true;
        });

        parent.AddHandler(DataGrid.RowEditEndingEvent, (_, e) =>
        {
            rowEditEndingHit = true;
            e.Cancel = true;
        });

        dataGrid.RaiseEvent(new DataGridBeginningEditEventArgs(
            new DataGridTextColumn(),
            new DataGridRow(),
            new RoutedEventArgs(),
            DataGrid.BeginningEditEvent,
            dataGrid));

        dataGrid.RaiseEvent(new DataGridRowEditEndingEventArgs(
            new DataGridRow(),
            new DataGridEditAction(),
            DataGrid.RowEditEndingEvent,
            dataGrid));

        Assert.True(beginningEditHit);
        Assert.True(rowEditEndingHit);
    }

    [AvaloniaFact]
    public void RowDetails_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var hit = false;
        parent.AddHandler(DataGrid.LoadingRowDetailsEvent, (_, e) => hit = true);

        dataGrid.RaiseEvent(new DataGridRowDetailsEventArgs(
            new DataGridRow(),
            new Border(),
            DataGrid.LoadingRowDetailsEvent,
            dataGrid));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void RowGroup_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var hit = false;
        parent.AddHandler(DataGrid.LoadingRowGroupEvent, (_, e) => hit = true);

        dataGrid.RaiseEvent(new DataGridRowGroupHeaderEventArgs(
            new DataGridRowGroupHeader(),
            DataGrid.LoadingRowGroupEvent,
            dataGrid));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void CurrentCellChanged_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var hit = false;
        parent.AddHandler(DataGrid.CurrentCellChangedEvent, (_, e) => hit = true);

        var oldColumn = new DataGridTextColumn { Header = "Old" };
        var newColumn = new DataGridTextColumn { Header = "New" };

        dataGrid.RaiseEvent(new DataGridCurrentCellChangedEventArgs(
            oldColumn,
            new object(),
            newColumn,
            new object(),
            DataGrid.CurrentCellChangedEvent,
            dataGrid));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void Clipboard_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var hit = false;
        parent.AddHandler(DataGrid.CopyingRowClipboardContentEvent, (_, e) =>
        {
            hit = true;
            Assert.False(e.IsColumnHeadersRow);
        });

        dataGrid.RaiseEvent(new DataGridRowClipboardEventArgs(
            item: new object(),
            isColumnHeadersRow: false,
            routedEvent: DataGrid.CopyingRowClipboardContentEvent,
            source: dataGrid));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void Header_LeftClick_RoutedEvent_Bubbles()
    {
        var parent = new Grid();
        var header = new DataGridColumnHeader();
        parent.Children.Add(header);

        var hit = false;
        parent.AddHandler(DataGridColumnHeader.LeftClickEvent, (_, e) =>
        {
            hit = true;
            Assert.Equal(KeyModifiers.Control, e.KeyModifiers);
        });

        header.RaiseEvent(new DataGridColumnHeaderClickEventArgs(
            KeyModifiers.Control,
            DataGridColumnHeader.LeftClickEvent,
            header));

        Assert.True(hit);
    }

    [AvaloniaFact]
    public void VerticalScroll_RoutedEvent_Reaches_AddHandler()
    {
        var parent = new Grid();
        var dataGrid = new DataGrid();
        parent.Children.Add(dataGrid);

        var hit = false;
        parent.AddHandler(DataGrid.VerticalScrollEvent, (_, e) =>
        {
            hit = true;
            Assert.Equal(ScrollEventType.LargeDecrement, e.ScrollEventType);
        });

        dataGrid.RaiseEvent(new DataGridScrollEventArgs(
            ScrollEventType.LargeDecrement,
            5,
            DataGrid.VerticalScrollEvent,
            dataGrid));

        Assert.True(hit);
    }
}
