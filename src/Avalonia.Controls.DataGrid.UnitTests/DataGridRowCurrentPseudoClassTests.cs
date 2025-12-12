// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowCurrentPseudoClassTests
{
    [AvaloniaFact]
    public void Current_PseudoClass_Follows_SelectedItem()
    {
        var items = new ObservableCollection<Person>
        {
            new("Ada", "Engineer"),
            new("Lin", "Designer"),
            new("Sam", "PM"),
        };

        var (grid, root) = CreateGrid(items);
        try
        {
            grid.SelectedItem = items[1];
            grid.UpdateLayout();

            var rowForLin = FindRow(items[1], grid);
            var rowForAda = FindRow(items[0], grid);

            Assert.True(((IPseudoClasses)rowForLin.Classes).Contains(":current"));
            Assert.False(((IPseudoClasses)rowForAda.Classes).Contains(":current"));

            grid.SelectedItem = items[2];
            grid.UpdateLayout();

            var rowForSam = FindRow(items[2], grid);

            Assert.True(((IPseudoClasses)rowForSam.Classes).Contains(":current"));
            Assert.False(((IPseudoClasses)rowForLin.Classes).Contains(":current"));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Current_PseudoClass_Is_Cleared_When_Row_Reused()
    {
        var items = Enumerable.Range(0, 30)
            .Select(i => new Person($"Person {i}", "Role"))
            .ToList();

        var (grid, root) = CreateGrid(new ObservableCollection<Person>(items));
        try
        {
            grid.SelectedItem = items[0];
            grid.UpdateLayout();

            var firstRow = FindRow(items[0], grid);
            Assert.True(((IPseudoClasses)firstRow.Classes).Contains(":current"));

            grid.ScrollIntoView(items[15], grid.Columns[0]);
            grid.SelectedItem = items[15];
            grid.UpdateLayout();

            var recycledRow = FindRow(items[15], grid);
            Assert.True(((IPseudoClasses)recycledRow.Classes).Contains(":current"));
            Assert.False(((IPseudoClasses)firstRow.Classes).Contains(":current"));
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root) CreateGrid(IEnumerable<Person> people)
    {
        var root = new Window
        {
            Width = 320,
            Height = 220,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var grid = new DataGrid
        {
            HeadersVisibility = DataGridHeadersVisibility.All,
            ItemsSource = people
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Person.Name))
        });

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Role",
            Binding = new Binding(nameof(Person.Role))
        });

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return (grid, root);
    }

    private static DataGridRow FindRow(object item, DataGrid grid)
    {
        return grid
            .GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => ReferenceEquals(r.DataContext, item));
    }

    private class Person
    {
        public Person(string name, string role)
        {
            Name = name;
            Role = role;
        }

        public string Name { get; }

        public string Role { get; }
    }
}
