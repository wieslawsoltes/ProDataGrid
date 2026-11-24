// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridColumnHeaderCaptureLostTests
{
    [AvaloniaFact]
    public void A_Header_Drag_That_Loses_Capture_Is_Cancelled()
    {
        var (grid, root, columns) = CreateGrid();
        try
        {
            IPointer? pointer = null;
            root.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            var start = PointInHeader(grid, root, columns[2], 30);

            root.MouseMove(start);
            root.MouseDown(start, MouseButton.Left);
            root.MouseMove(start + new Vector(-20, 0), RawInputModifiers.LeftMouseButton);
            root.MouseMove(start + new Vector(-40, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(grid.ColumnHeaders.DragIndicator);

            pointer!.Capture(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(grid.ColumnHeaders.DragIndicator);
            Assert.DoesNotContain(":pressed", HeaderFor(grid, columns[2]).Classes);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Headers_Can_Be_Reordered_After_A_Drag_Lost_Capture()
    {
        var (grid, root, columns) = CreateGrid();
        try
        {
            IPointer? pointer = null;
            root.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            var start = PointInHeader(grid, root, columns[2], 30);
            root.MouseMove(start);
            root.MouseDown(start, MouseButton.Left);
            root.MouseMove(start + new Vector(-20, 0), RawInputModifiers.LeftMouseButton);
            root.MouseMove(start + new Vector(-40, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            pointer!.Capture(null);
            Dispatcher.UIThread.RunJobs();

            Drag(root, PointInHeader(grid, root, columns[2], 30), PointInHeader(grid, root, columns[0], 5));

            Assert.Equal(0, columns[2].DisplayIndex);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void A_Header_Resize_That_Loses_Capture_Stops_Resizing()
    {
        var (grid, root, columns) = CreateGrid();
        try
        {
            IPointer? pointer = null;
            root.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            var start = PointInHeader(grid, root, columns[0], 118);

            root.MouseMove(start);
            root.MouseDown(start, MouseButton.Left);
            root.MouseMove(start + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            var widthWhenLost = columns[0].ActualWidth;
            Assert.True(widthWhenLost > 120);

            pointer!.Capture(null);
            Dispatcher.UIThread.RunJobs();
            root.MouseMove(start + new Vector(60, 0), RawInputModifiers.LeftMouseButton);
            root.MouseUp(start + new Vector(60, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(widthWhenLost, columns[0].ActualWidth);
            Assert.False(HeaderFor(grid, columns[0]).IsResizing);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void A_Header_Click_That_Loses_Capture_Does_Not_Sort()
    {
        var (grid, root, columns) = CreateGrid();
        try
        {
            IPointer? pointer = null;
            root.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            var point = PointInHeader(grid, root, columns[1], 60);

            root.MouseMove(point);
            root.MouseDown(point, MouseButton.Left);
            pointer!.Capture(null);
            root.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var header = HeaderFor(grid, columns[1]);
            Assert.DoesNotContain(":sortascending", header.Classes);
            Assert.DoesNotContain(":pressed", header.Classes);

            root.MouseDown(point, MouseButton.Left);
            root.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(":sortascending", header.Classes);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void A_Completed_Header_Drag_Still_Reorders()
    {
        var (grid, root, columns) = CreateGrid();
        try
        {
            Drag(root, PointInHeader(grid, root, columns[2], 30), PointInHeader(grid, root, columns[0], 5));

            Assert.Equal(0, columns[2].DisplayIndex);
            Assert.Null(grid.ColumnHeaders.DragIndicator);
        }
        finally
        {
            root.Close();
        }
    }

    private static void Drag(Window root, Point from, Point to)
    {
        root.MouseMove(from);
        root.MouseDown(from, MouseButton.Left);
        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            root.MouseMove(from + (to - from) * i / steps, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
        }

        root.MouseUp(to, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        root.UpdateLayout();
    }

    private static Point PointInHeader(DataGrid grid, Window root, DataGridColumn column, double x)
    {
        var header = HeaderFor(grid, column);
        return header.TranslatePoint(new Point(x, header.Bounds.Height / 2), root)!.Value;
    }

    private static DataGridColumnHeader HeaderFor(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private static (DataGrid grid, Window root, DataGridColumn[] columns) CreateGrid()
    {
        var root = new Window
        {
            Width = 600,
            Height = 300
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            ItemsSource = new ObservableCollection<Row> { new("a", "b", "c"), new("d", "e", "f") }
        };

        var columns = new DataGridColumn[]
        {
            new DataGridTextColumn { Header = "First", Binding = new Binding(nameof(Row.First)), Width = new DataGridLength(120) },
            new DataGridTextColumn { Header = "Second", Binding = new Binding(nameof(Row.Second)), Width = new DataGridLength(120) },
            new DataGridTextColumn { Header = "Third", Binding = new Binding(nameof(Row.Third)), Width = new DataGridLength(120) }
        };

        foreach (var column in columns)
        {
            grid.ColumnsInternal.Add(column);
        }

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, columns);
    }

    private sealed record Row(string First, string Second, string Third);
}
