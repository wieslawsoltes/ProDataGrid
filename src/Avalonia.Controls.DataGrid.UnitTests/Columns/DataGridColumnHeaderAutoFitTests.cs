// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridTests;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridColumnHeaderAutoFitTests
{
    [AvaloniaFact]
    public void Double_Click_On_Resize_Handle_Auto_Fits_Column()
    {
        var (grid, root, column) = CreateGrid(canAutoFit: true);
        try
        {
            var header = GetHeaderForColumn(grid, column);
            var initialWidth = column.ActualWidth;

            RaiseDoubleClickOnResizeHandle(header);
            grid.UpdateLayout();

            Assert.True(column.ActualWidth > initialWidth + 0.5);
            Assert.True(column.ActualWidth >= 180);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Click_On_Resize_Handle_Is_Ignored_When_Disabled()
    {
        var (grid, root, column) = CreateGrid(canAutoFit: false);
        try
        {
            var header = GetHeaderForColumn(grid, column);
            var initialWidth = column.ActualWidth;

            RaiseDoubleClickOnResizeHandle(header);
            grid.UpdateLayout();

            Assert.InRange(Math.Abs(column.ActualWidth - initialWidth), 0, 0.5);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridColumn column) CreateGrid(bool canAutoFit)
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta")
        };

        var root = new Window
        {
            Width = 400,
            Height = 200
        };

        root.SetThemeStyles();

        var column = new DataGridTemplateColumn
        {
            Header = "Name",
            Width = new DataGridLength(40),
            CellTemplate = new FuncDataTemplate<Item>((_, _) => new Border { Width = 180, Height = 20 })
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserResizeColumns = true,
            CanUserResizeColumnsOnDoubleClick = canAutoFit,
            ItemsSource = items
        };

        grid.ColumnsInternal.Add(column);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, column);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private static void RaiseDoubleClickOnResizeHandle(DataGridColumnHeader header)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var x = Math.Max(1, header.Bounds.Width - 2);
        var point = new Point(x, header.Bounds.Height / 2);
        var args = new PointerPressedEventArgs(header, pointer, header, point, 0, properties, KeyModifiers.None, 2);
        header.RaiseEvent(args);
    }

    private sealed class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
