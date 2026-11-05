// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowGroupSpacerEmptyViewTests
{
    [AvaloniaFact]
    public void Grouped_Grid_Keeps_Its_Group_Indent_When_The_View_Becomes_Empty()
    {
        var items = new ObservableCollection<Row> { new("a", "x"), new("b", "y") };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Row.Group)));
        var column = new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) };
        var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = view, HeadersVisibility = DataGridHeadersVisibility.Column };
        grid.ColumnsInternal.Add(column);
        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        grid.ApplyTemplate();

        try
        {
            grid.UpdateLayout();
            var grouped = HeaderLeft(grid, column);
            Assert.True(grid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented);

            items.Clear();
            grid.UpdateLayout();

            Assert.True(grid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented);
            Assert.Equal(grouped, HeaderLeft(grid, column));

            items.Add(new Row("c", "z"));
            grid.UpdateLayout();

            Assert.Equal(grouped, HeaderLeft(grid, column));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ungrouping_An_Empty_View_Removes_The_Group_Indent()
    {
        var items = new ObservableCollection<Row>();
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Row.Group)));
        var column = new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) };
        var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = view, HeadersVisibility = DataGridHeadersVisibility.Column };
        grid.ColumnsInternal.Add(column);
        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        grid.ApplyTemplate();

        try
        {
            grid.UpdateLayout();
            view.GroupDescriptions.Clear();
            grid.UpdateLayout();

            Assert.False(grid.ColumnsInternal.RowGroupSpacerColumn.IsRepresented);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Row_Group_Sublevel_Indent_Sets_The_Indent_Of_A_Grouped_View_Without_Rows()
    {
        var items = new ObservableCollection<Row>();
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Row.Group)));
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Row.Name)));
        var column = new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowGroupSublevelIndent = 30,
        };
        grid.ColumnsInternal.Add(column);
        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        grid.ApplyTemplate();

        try
        {
            grid.UpdateLayout();
            Assert.Equal(60, grid.ColumnsInternal.RowGroupSpacerColumn.Width.Value);
            var grouped = HeaderLeft(grid, column);

            view.GroupDescriptions.Clear();
            grid.UpdateLayout();

            Assert.Equal(60, grouped - HeaderLeft(grid, column));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_Row_Group_Sublevel_Indent_Updates_The_Group_Indent()
    {
        var items = new ObservableCollection<Row> { new("a", "x"), new("b", "y") };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Row.Group)));
        var column = new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Row.Name)) };
        var grid = new DataGrid { AutoGenerateColumns = false, ItemsSource = view, HeadersVisibility = DataGridHeadersVisibility.Column };
        grid.ColumnsInternal.Add(column);
        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        grid.ApplyTemplate();

        try
        {
            grid.UpdateLayout();
            Assert.Equal(20, grid.ColumnsInternal.RowGroupSpacerColumn.Width.Value);
            var before = HeaderLeft(grid, column);

            grid.RowGroupSublevelIndent = 35;
            grid.UpdateLayout();

            Assert.Equal(35, grid.ColumnsInternal.RowGroupSpacerColumn.Width.Value);
            Assert.Equal(15, HeaderLeft(grid, column) - before);
        }
        finally
        {
            window.Close();
        }
    }

    private static double HeaderLeft(DataGrid grid, DataGridColumn column)
    {
        var header = grid.GetVisualDescendants().OfType<DataGridColumnHeader>().First(h => ReferenceEquals(h.OwningColumn, column));
        return header.TranslatePoint(default, grid)!.Value.X;
    }

    private sealed record Row(string Name, string Group);
}
