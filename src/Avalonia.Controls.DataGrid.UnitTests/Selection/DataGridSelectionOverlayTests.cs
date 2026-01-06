// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridTests;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionOverlayTests
{
    [AvaloniaFact]
    public void FillHandle_Hidden_When_Selection_Extends_Beyond_Viewport()
    {
        var (window, grid, items) = CreateGrid(itemCount: 20, height: 120);
        try
        {
            SelectRange(grid, items, 0, items.Count - 1);
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var fillHandle = GetFillHandle(grid);
            Assert.False(fillHandle.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FillHandle_Visible_When_Selection_Fully_Visible()
    {
        var (window, grid, items) = CreateGrid(itemCount: 3, height: 240);
        try
        {
            SelectRange(grid, items, 0, 1);
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var fillHandle = GetFillHandle(grid);
            Assert.True(fillHandle.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid, ObservableCollection<RowItem> Items) CreateGrid(int itemCount, double height)
    {
        var items = new ObservableCollection<RowItem>();
        for (var i = 0; i < itemCount; i++)
        {
            items.Add(new RowItem($"Item {i}"));
        }

        var window = new Window
        {
            Width = 320,
            Height = height
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionUnit = DataGridSelectionUnit.Cell,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return (window, grid, items);
    }

    private static void SelectRange(DataGrid grid, ObservableCollection<RowItem> items, int startRow, int endRow)
    {
        var selected = new ObservableCollection<DataGridCellInfo>();
        grid.SelectedCells = selected;

        var column = grid.ColumnsInternal[0];
        for (var rowIndex = startRow; rowIndex <= endRow; rowIndex++)
        {
            selected.Add(new DataGridCellInfo(items[rowIndex], column, rowIndex, column.Index, isValid: true));
        }

        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static Border GetFillHandle(DataGrid grid)
    {
        return grid.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Name == "PART_FillHandle");
    }

    private sealed class RowItem
    {
        public RowItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
