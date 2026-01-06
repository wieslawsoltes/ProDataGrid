// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowHeaderNumberTests
{
    [AvaloniaFact]
    public void ShowRowNumbers_Toggle_Restores_RowHeader_Binding()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("Alpha"),
            new("Beta")
        };

        var root = new Window
        {
            Width = 320,
            Height = 240
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        try
        {
            foreach (var row in GetRows(grid, items))
            {
                row.Bind(DataGridRow.HeaderProperty, new Binding(nameof(RowItem.Name)));
            }

            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            var firstRow = GetRow(grid, items[0]);
            Assert.Equal(items[0].Name, firstRow.Header);

            grid.ShowRowNumbers = true;
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(firstRow.Index + 1, firstRow.Header);

            grid.ShowRowNumbers = false;
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(items[0].Name, firstRow.Header);
        }
        finally
        {
            root.Close();
        }
    }

    private static DataGridRow GetRow(DataGrid grid, RowItem item)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => ReferenceEquals(r.DataContext, item));
    }

    private static DataGridRow[] GetRows(DataGrid grid, ObservableCollection<RowItem> items)
    {
        return items.Select(item => GetRow(grid, item)).ToArray();
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
