using System;
using System.Data;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridDataTableDisplayTests
{
    [AvaloniaFact]
    public void DataTable_DefaultView_Does_Not_Produce_Duplicate_Visible_Rows()
    {
        var table = BuildTable();
        var view = new DataGridCollectionView(table.DefaultView);

        Assert.Equal(table.Rows.Count, view.Count);

        var root = new Window
        {
            Width = 400,
            Height = 400,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = true,
            CanUserAddRows = false,
            ItemsSource = view,
            HeadersVisibility = DataGridHeadersVisibility.Column
        };

        root.Content = dataGrid;
        root.Show();
        dataGrid.UpdateLayout();

        var rows = dataGrid.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .ToList();

        var visibleIds = rows
            .Select(r => (int)((DataRowView)r.DataContext)["Id"])
            .ToArray();

        var expectedIds = table.Rows
            .Cast<DataRow>()
            .Select(r => (int)r["Id"])
            .ToArray();

        Assert.Equal(expectedIds.Length, visibleIds.Length);
        Assert.Equal(expectedIds, visibleIds);
    }

    private static DataTable BuildTable()
    {
        var table = new DataTable("Sample");
        table.Columns.Add(new DataColumn("Id", typeof(int)));
        table.Columns.Add(new DataColumn("Name", typeof(string)));
        table.Columns.Add(new DataColumn("Balance", typeof(decimal)));
        table.Columns.Add(new DataColumn("Created", typeof(DateTimeOffset)));
        table.Columns.Add(new DataColumn("IsActive", typeof(bool)));

        for (var i = 1; i <= 8; i++)
        {
            var row = table.NewRow();
            row["Id"] = i;
            row["Name"] = $"Row {i}";
            row["Balance"] = Math.Round((i * 137.42m) % 5000 - 1200, 2);
            row["Created"] = DateTimeOffset.Now.AddHours(-i * 3);
            row["IsActive"] = i % 3 != 0;
            table.Rows.Add(row);
        }

        return table;
    }
}
