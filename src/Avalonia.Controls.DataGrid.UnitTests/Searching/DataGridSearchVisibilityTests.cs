// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Searching;

public class DataGridSearchVisibilityTests
{
    [AvaloniaFact]
    public void VisibleColumns_Scope_Reacts_To_Column_Visibility_Changes()
    {
        var items = new ObservableCollection<Person>
        {
            new("Alpha", "Hidden")
        };

        var (grid, root, hiddenColumn) = CreateGrid(items);
        try
        {
            hiddenColumn.IsVisible = false;

            grid.SearchModel.SetOrUpdate(new SearchDescriptor(
                "Hidden",
                scope: SearchScope.VisibleColumns,
                comparison: StringComparison.OrdinalIgnoreCase));

            grid.UpdateLayout();
            Assert.Empty(grid.SearchModel.Results);

            hiddenColumn.IsVisible = true;
            grid.UpdateLayout();

            Assert.Contains(grid.SearchModel.Results, r => ReferenceEquals(r.ColumnId, hiddenColumn));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridColumn hiddenColumn) CreateGrid(ObservableCollection<Person> items)
    {
        var root = new Window
        {
            Width = 320,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            HeadersVisibility = DataGridHeadersVisibility.All,
            ItemsSource = items
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Person.Name))
        });

        var hiddenColumn = new DataGridTextColumn
        {
            Header = "Region",
            Binding = new Binding(nameof(Person.Region))
        };
        grid.ColumnsInternal.Add(hiddenColumn);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, hiddenColumn);
    }

    private sealed class Person
    {
        public Person(string name, string region)
        {
            Name = name;
            Region = region;
        }

        public string Name { get; }
        public string Region { get; }
    }
}
