// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridClipboard;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class DataGridClipboardImportModelTests
{
    [AvaloniaFact]
    public void Paste_SingleValue_Fills_Selected_Cells()
    {
        var items = new ObservableCollection<Row>
        {
            new(),
            new()
        };

        var (window, grid) = CreateGrid(items);
        try
        {
            var selected = new List<DataGridCellInfo>
            {
                new(items[0], grid.ColumnsInternal[0], 0, 0, true),
                new(items[0], grid.ColumnsInternal[1], 0, 1, true),
                new(items[1], grid.ColumnsInternal[0], 1, 0, true),
                new(items[1], grid.ColumnsInternal[1], 1, 1, true)
            };

            var context = new DataGridClipboardImportContext(grid, "hello", selected);
            var model = new DataGridClipboardImportModel();

            Assert.True(model.Paste(context));

            Assert.Equal("hello", items[0].A);
            Assert.Equal("hello", items[0].B);
            Assert.Equal("hello", items[1].A);
            Assert.Equal("hello", items[1].B);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_Table_Starts_From_Selection_Anchor()
    {
        var items = new ObservableCollection<Row>
        {
            new(),
            new(),
            new()
        };

        var (window, grid) = CreateGrid(items);
        try
        {
            var selected = new List<DataGridCellInfo>
            {
                new(items[1], grid.ColumnsInternal[1], 1, 1, true)
            };

            var context = new DataGridClipboardImportContext(grid, "A\tB\nC\tD", selected);
            var model = new DataGridClipboardImportModel();

            Assert.True(model.Paste(context));

            Assert.Equal("A", items[1].B);
            Assert.Equal("B", items[1].C);
            Assert.Equal("C", items[2].B);
            Assert.Equal("D", items[2].C);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paste_SingleValue_Returns_False_When_No_Cells_Accept_Edit()
    {
        var items = new ObservableCollection<Row>
        {
            new(),
            new()
        };

        var (window, grid) = CreateGrid(items, readOnlyColumns: true);
        try
        {
            var selected = new List<DataGridCellInfo>
            {
                new(items[0], grid.ColumnsInternal[0], 0, 0, true),
                new(items[0], grid.ColumnsInternal[1], 0, 1, true),
                new(items[1], grid.ColumnsInternal[0], 1, 0, true),
                new(items[1], grid.ColumnsInternal[1], 1, 1, true)
            };

            var context = new DataGridClipboardImportContext(grid, "X", selected);
            var model = new DataGridClipboardImportModel();

            Assert.False(model.Paste(context));
            Assert.Equal(string.Empty, items[0].A);
            Assert.Equal(string.Empty, items[0].B);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid) CreateGrid(ObservableCollection<Row> items)
    {
        return CreateGrid(items, readOnlyColumns: false);
    }

    private static (Window Window, DataGrid Grid) CreateGrid(ObservableCollection<Row> items, bool readOnlyColumns)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionUnit = DataGridSelectionUnit.Cell,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "A",
            Binding = new Binding(nameof(Row.A)),
            IsReadOnly = readOnlyColumns
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "B",
            Binding = new Binding(nameof(Row.B)),
            IsReadOnly = readOnlyColumns
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "C",
            Binding = new Binding(nameof(Row.C)),
            IsReadOnly = readOnlyColumns
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "D",
            Binding = new Binding(nameof(Row.D)),
            IsReadOnly = readOnlyColumns
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private sealed class Row
    {
        public string A { get; set; } = string.Empty;

        public string B { get; set; } = string.Empty;

        public string C { get; set; } = string.Empty;

        public string D { get; set; } = string.Empty;
    }
}
