using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFilling;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.Themes.Fluent;
using ProDataGrid.ExcelSample.Models;
using Xunit;

namespace ProDataGrid.ExcelSample.Tests;

public sealed class SpreadsheetFillModelTests
{
    [AvaloniaFact]
    public void SpreadsheetFillModel_Fills_TextSeries_WithNumbers()
    {
        var items = new ObservableCollection<TextRow>
        {
            new TextRow { A = "Item 1" },
            new TextRow { A = "Item 2" },
            new TextRow(),
            new TextRow()
        };

        var (window, grid) = CreateGrid(items);
        try
        {
            grid.FillModel = new SpreadsheetFillModel();

            var source = new DataGridCellRange(0, 1, 0, 0);
            var target = new DataGridCellRange(0, 3, 0, 0);

            grid.FillModel.ApplyFill(new DataGridFillContext(grid, source, target));

            Assert.Equal("Item 3", items[2].A);
            Assert.Equal("Item 4", items[3].A);
        }
        finally
        {
            grid.FillModel = null;
            window.Content = null;
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static (Window Window, DataGrid Grid) CreateGrid(ObservableCollection<TextRow> items)
    {
        var window = CreateWindow();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "A",
            Binding = new Binding(nameof(TextRow.A))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private static Window CreateWindow()
    {
        var window = new Window
        {
            Width = 640,
            Height = 480
        };

        window.Styles.Add(new FluentTheme());
        window.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml")
        });

        return window;
    }

    private sealed class TextRow
    {
        public string? A { get; set; }
    }
}
