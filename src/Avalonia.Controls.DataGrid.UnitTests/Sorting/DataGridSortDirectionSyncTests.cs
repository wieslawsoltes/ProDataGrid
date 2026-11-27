// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Sorting;

public class DataGridSortDirectionSyncTests
{
    [AvaloniaFact]
    public void SortDirection_Uses_Custom_Comparer_When_Set()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var grid = CreateGrid(items);
        var column = grid.ColumnsInternal.First(c => Equals(c.Header, "Name"));
        column.CustomSortComparer = new ReverseComparer();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Same(column.CustomSortComparer, descriptor.Comparer);
        Assert.Equal(ListSortDirection.Ascending, column.SortDirection);

        var view = (IDataGridCollectionView)grid.ItemsSource!;
        var sort = Assert.Single(view.SortDescriptions);
        var comparerSort = Assert.IsType<DataGridComparerSortDescription>(sort);
        Assert.Same(column.CustomSortComparer, comparerSort.SourceComparer);
    }

    [AvaloniaFact]
    public void SortDirection_Persists_After_Reattach()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A")
        };

        var view = new DataGridCollectionView(items);
        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            SortMemberPath = nameof(Item.Name)
        };

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var sort = Assert.Single(view.SortDescriptions);
        Assert.Equal(nameof(Item.Name), sort.PropertyPath);
        Assert.Equal(ListSortDirection.Ascending, sort.Direction);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        sort = Assert.Single(view.SortDescriptions);
        Assert.Equal(nameof(Item.Name), sort.PropertyPath);
        Assert.Equal(ListSortDirection.Ascending, sort.Direction);

        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Equal(nameof(Item.Name), descriptor.PropertyPath);
        Assert.Equal(ListSortDirection.Ascending, descriptor.Direction);

        window.Close();
    }

    [AvaloniaFact]
    public void SortDirection_Ignored_When_No_Path_Or_Comparer()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);
        var column = new DataGridTextColumn { Header = "NoPath" };

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        Assert.Null(column.SortDirection);
        Assert.Empty(grid.SortingModel.Descriptors);
        Assert.Empty(view.SortDescriptions);
    }

    [AvaloniaFact]
    public void SortDirection_Uses_ValueAccessor_When_Definition_Has_Binding_Without_Path()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var definition = new DataGridTextColumnDefinition
        {
            Header = "Name",
            Binding = DataGridBindingDefinition.Create<Item, string>(x => x.Name)
        };

        var root = new Window
        {
            Width = 400,
            Height = 300
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            ColumnDefinitionsSource = new[] { definition },
            SelectionMode = DataGridSelectionMode.Extended
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        var column = grid.ColumnsInternal.First(c => Equals(c.Header, "Name"));
        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.True(descriptor.HasComparer);
        Assert.Equal(definition, descriptor.ColumnId);

        var sort = Assert.IsType<DataGridComparerSortDescription>(Assert.Single(view.SortDescriptions));
        Assert.NotNull(GetAccessor(sort.SourceComparer));
    }

    [AvaloniaFact]
    public void SortDirection_Uses_SortValueAccessor_When_Set()
    {
        var items = new ObservableCollection<Person>
        {
            new("Alpha", 2),
            new("Beta", 1)
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);

        var column = new DataGridTextColumn { Header = "Age" };
        DataGridColumnSort.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Age));

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var ordered = view.Cast<Person>().Select(p => p.Age).ToArray();
        Assert.Equal(new[] { 1, 2 }, ordered);
    }

    [AvaloniaFact]
    public void SortDirection_Uses_SortValueComparer_When_Set()
    {
        var items = new ObservableCollection<Person>
        {
            new("Alpha", 2),
            new("Beta", 1)
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);

        var column = new DataGridTextColumn { Header = "Age" };
        DataGridColumnSort.SetValueAccessor(column, new DataGridColumnValueAccessor<Person, int>(p => p.Age));
        DataGridColumnSort.SetValueComparer(column, Comparer<int>.Create((x, y) => y.CompareTo(x)));

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var ordered = view.Cast<Person>().Select(p => p.Age).ToArray();
        Assert.Equal(new[] { 2, 1 }, ordered);
    }

    [AvaloniaFact]
    public void CustomSortComparer_Change_Updates_Active_Descriptor()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var grid = CreateGrid(items);
        var column = grid.ColumnsInternal.First(c => Equals(c.Header, "Name"));

        column.CustomSortComparer = new ReverseComparer();
        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var initialComparer = Assert.IsType<DataGridComparerSortDescription>(
            Assert.Single(((IDataGridCollectionView)grid.ItemsSource!).SortDescriptions)).SourceComparer;

        Assert.Same(column.CustomSortComparer, initialComparer);

        var newComparer = new ReverseComparer();
        column.CustomSortComparer = newComparer;
        grid.UpdateLayout();

        var updatedComparer = Assert.IsType<DataGridComparerSortDescription>(
            Assert.Single(((IDataGridCollectionView)grid.ItemsSource!).SortDescriptions)).SourceComparer;

        Assert.Same(newComparer, updatedComparer);
        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Same(newComparer, descriptor.Comparer);
    }

    [AvaloniaFact]
    public void SortDirection_Stays_In_Sync_In_Observe_Mode_With_External_View_Update()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithColumns(view);
        grid.OwnsSortDescriptions = false;
        grid.UpdateLayout();

        var column = grid.ColumnsInternal.First(c => Equals(c.Header, "Name"));
        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        // Simulate external update to SortDescriptions
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Item.Name), ListSortDirection.Descending));
        }

        grid.UpdateLayout();

        Assert.Equal(ListSortDirection.Descending, column.SortDirection);
        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Equal(ListSortDirection.Descending, descriptor.Direction);
        Assert.Equal(nameof(Item.Name), descriptor.PropertyPath);
    }

    [AvaloniaFact]
    public void Column_Added_After_Model_Syncs_SortDirection_From_Path_Descriptor()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);

        grid.SortingModel.Apply(new[]
        {
            new SortingDescriptor("NameKey", ListSortDirection.Descending, nameof(Item.Name))
        });

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            SortMemberPath = nameof(Item.Name)
        };

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        Assert.Equal(ListSortDirection.Descending, column.SortDirection);
        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Equal("NameKey", descriptor.ColumnId);
        Assert.Equal(nameof(Item.Name), descriptor.PropertyPath);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(1)]
    public void Column_Added_With_SortDirection_Sorts_After_Display_Index_Is_Assigned(int precedingColumns)
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);
        for (var i = 0; i < precedingColumns; i++)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "Other",
                Binding = new Binding(nameof(Item.Name))
            });
        }

        var countsWhileSorting = new List<(int Columns, int DisplayIndexes)>();
        view.CollectionChanged += (_, _) =>
            countsWhileSorting.Add((grid.ColumnsInternal.ItemsInternal.Count, grid.ColumnsInternal.DisplayIndexMap.Count));

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            SortMemberPath = nameof(Item.Name),
            SortDirection = ListSortDirection.Ascending
        };

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        Assert.Equal(new[] { "A", "B" }, view.Cast<Item>().Select(item => item.Name));
        Assert.Equal(ListSortDirection.Ascending, column.SortDirection);
        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Equal(nameof(Item.Name), descriptor.PropertyPath);
        Assert.NotEmpty(countsWhileSorting);
        Assert.All(countsWhileSorting, counts => Assert.Equal(counts.Columns, counts.DisplayIndexes));
    }

    [AvaloniaFact]
    public void Clearing_SortDirection_Removes_Descriptor_With_External_ColumnId()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var grid = CreateGrid(items);
        grid.SortingModel.Apply(new[]
        {
            new SortingDescriptor("NameKey", ListSortDirection.Ascending, nameof(Item.Name))
        });
        grid.UpdateLayout();

        var column = grid.ColumnsInternal.First(c => Equals(c.Header, "Name"));
        Assert.Equal(ListSortDirection.Ascending, column.SortDirection);

        column.SortDirection = null;
        grid.UpdateLayout();

        Assert.Empty(grid.SortingModel.Descriptors);
        var view = (IDataGridCollectionView)grid.ItemsSource!;
        Assert.Empty(view.SortDescriptions);
    }

    [AvaloniaFact]
    public void PreAttach_Comparer_Swap_Applies_Latest_On_First_Sort()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
        };

        var view = new DataGridCollectionView(items);
        var grid = CreateGridWithoutColumns(view);

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            SortMemberPath = nameof(Item.Name),
            CustomSortComparer = new ReverseComparer()
        };

        var latestComparer = new ReverseComparer();
        column.CustomSortComparer = latestComparer;

        grid.ColumnsInternal.Add(column);
        grid.UpdateLayout();

        column.SortDirection = ListSortDirection.Ascending;
        grid.UpdateLayout();

        var sort = Assert.Single(view.SortDescriptions);
        var comparerSort = Assert.IsType<DataGridComparerSortDescription>(sort);
        Assert.Same(latestComparer, comparerSort.SourceComparer);

        var descriptor = Assert.Single(grid.SortingModel.Descriptors);
        Assert.Same(latestComparer, descriptor.Comparer);
    }

    private static DataGrid CreateGrid(IEnumerable<Item> items)
    {
        var view = new DataGridCollectionView(items);
        return CreateGridWithColumns(view);
    }

    private static DataGrid CreateGridWithColumns(DataGridCollectionView view)
    {
        var root = new Window
        {
            Width = 400,
            Height = 300,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            SelectionMode = DataGridSelectionMode.Extended
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            SortMemberPath = nameof(Item.Name)
        });

        root.Content = grid;
        root.Show();

        grid.UpdateLayout();
        return grid;
    }

    private static DataGrid CreateGridWithoutColumns(DataGridCollectionView view)
    {
        var grid = new DataGrid
        {
            ItemsSource = view,
            SelectionMode = DataGridSelectionMode.Extended
        };

        var root = new Window
        {
            Width = 400,
            Height = 300,
            Content = grid
        };

        root.SetThemeStyles();

        root.Show();
        grid.UpdateLayout();
        return grid;
    }

    private static IDataGridColumnValueAccessor GetAccessor(object comparer)
    {
        var property = comparer.GetType().GetProperty("Accessor", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<IDataGridColumnValueAccessor>(property.GetValue(comparer));
    }

    public record Item(string Name);

    private sealed class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }

        public int Age { get; }
    }

    private sealed class ReverseComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            var sx = x?.ToString();
            var sy = y?.ToString();
            return string.Compare(sy, sx, StringComparison.Ordinal);
        }
    }
}
