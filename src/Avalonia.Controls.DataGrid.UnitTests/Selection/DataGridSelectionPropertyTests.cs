// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Collections;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionPropertyTests
{
    [AvaloniaFact]
    public void Custom_SelectionModel_Applies_Selection_To_Grid()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var selectionModel = new SelectionModel<string> { SingleSelect = false };
        selectionModel.Select(1); // preselect before wiring

        var grid = CreateGrid(items);
        grid.Selection = selectionModel;
        grid.UpdateLayout();

        Assert.Equal("B", grid.SelectedItem);
        Assert.Equal(new[] { "B" }, grid.SelectedItems.Cast<string>());

        var rows = GetRows(grid);
        Assert.True(rows.First(r => Equals(r.DataContext, "B")).IsSelected);
        Assert.All(rows.Where(r => !Equals(r.DataContext, "B")), r => Assert.False(r.IsSelected));

        Assert.Same(grid.Selection, selectionModel);
        Assert.Same(grid.Selection.Source, grid.CollectionView);
    }

    [AvaloniaFact]
    public void OneTime_Selection_Binding_Updates_After_DataContext_Assigned()
    {
        var selectionModel = new SelectionModel<string> { SingleSelect = true };
        var grid = new DataGrid();

        grid.Bind(DataGrid.SelectionProperty, new Binding("Selection") { Mode = BindingMode.OneTime });
        grid.DataContext = new { Selection = selectionModel };

        Assert.Same(selectionModel, grid.Selection);
    }

    [AvaloniaFact]
    public void Selection_Model_With_Mismatched_Source_Is_Retargeted()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var grid = CreateGrid(items);

        // Source is the raw collection, not the view wrapped by the grid.
        var selectionModel = new SelectionModel<object> { Source = items };

        selectionModel.Select(1);

        grid.Selection = selectionModel;
        grid.UpdateLayout();

        Assert.Same(grid.CollectionView, selectionModel.Source);
        Assert.Equal(1, selectionModel.SelectedIndex);
        Assert.Equal(items[1], selectionModel.SelectedItem);
        Assert.Equal(items[1], grid.SelectedItem);

        selectionModel.Select(2);
        grid.UpdateLayout();

        Assert.Equal(new[] { 1, 2 }, selectionModel.SelectedIndexes.OrderBy(x => x));
        Assert.Contains(items[1], grid.SelectedItems.Cast<object>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<object>());
    }

    [AvaloniaFact]
    public void Selection_Model_Source_Updates_When_ItemsSource_Changes()
    {
        var items1 = new ObservableCollection<string> { "A", "B" };
        var items2 = new ObservableCollection<string> { "X", "Y" };
        var selectionModel = new SelectionModel<string> { SingleSelect = false };

        var grid = CreateGrid(items1);
        grid.Selection = selectionModel;
        grid.UpdateLayout();

        Assert.Same(grid.CollectionView, selectionModel.Source);

        grid.ItemsSource = items2;
        grid.UpdateLayout();

        Assert.Same(grid.CollectionView, selectionModel.Source);
        Assert.Equal(-1, selectionModel.SelectedIndex);
        Assert.Null(selectionModel.SelectedItem);
    }

    [AvaloniaFact]
    public void Hiding_Current_Column_Moves_CurrentCell_To_Visible_Column()
    {
        var items = new ObservableCollection<VisibleColumnItem>
        {
            new("A", 1),
            new("B", 2)
        };

        var root = new Window
        {
            Width = 250,
            Height = 150
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Single
        };

        var firstColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(VisibleColumnItem.Name))
        };
        var secondColumn = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(VisibleColumnItem.Value))
        };

        grid.ColumnsInternal.Add(firstColumn);
        grid.ColumnsInternal.Add(secondColumn);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        grid.SelectedIndex = 0;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(firstColumn.Index, grid.CurrentCell.ColumnIndex);

        firstColumn.IsVisible = false;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.NotEqual(firstColumn.Index, grid.CurrentCell.ColumnIndex);
        Assert.True(grid.CurrentCell.Column.IsVisible);

        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.True(grid.CurrentCell.Column.IsVisible);
    }

    [AvaloniaFact]
    public void UpdateSelectionAndCurrency_Coerces_Hidden_Column()
    {
        var items = new ObservableCollection<VisibleColumnItem>
        {
            new("A", 1),
            new("B", 2)
        };

        var root = new Window
        {
            Width = 250,
            Height = 150
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Single
        };

        var hiddenColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(VisibleColumnItem.Name))
        };
        var visibleColumn = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(VisibleColumnItem.Value))
        };

        grid.ColumnsInternal.Add(hiddenColumn);
        grid.ColumnsInternal.Add(visibleColumn);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        hiddenColumn.IsVisible = false;
        grid.UpdateLayout();

        Assert.True(grid.UpdateSelectionAndCurrency(hiddenColumn.Index, slot: 0, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.True(grid.CurrentCell.Column.IsVisible);
        Assert.Equal(visibleColumn.Index, grid.CurrentCell.ColumnIndex);
    }

    [AvaloniaFact]
    public void Replacing_Selection_Raises_Removed_SelectionChanged()
    {
        var items = new ObservableCollection<string> { "A", "B" };
        var grid = CreateGrid(items);
        grid.SelectedItem = items[0];
        grid.UpdateLayout();

        SelectionChangedEventArgs? args = null;
        grid.SelectionChanged += (_, e) => args = e;

        grid.Selection = new SelectionModel<object>();
        grid.UpdateLayout();

        Assert.NotNull(args);
        var removed = args!.RemovedItems.Cast<object>().ToArray();
        Assert.Equal(new[] { items[0] }, removed);
        Assert.Empty(args.AddedItems);
    }

    [AvaloniaFact]
    public void SelectionModel_Shifts_On_Insert_Before_Selected_Item()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var selectionModel = new SelectionModel<string> { SingleSelect = false };
        selectionModel.Select(1); // select "B" before source to verify deferred selection

        var grid = CreateGrid(items);
        grid.Selection = selectionModel;
        grid.UpdateLayout();

        var selected = items[1];
        Assert.Equal(selected, grid.SelectedItem);
        Assert.Equal(1, selectionModel.SelectedIndex);

        items.Insert(0, "Z");
        grid.UpdateLayout();

        Assert.Equal(selected, grid.SelectedItem);
        Assert.Contains(selected, grid.SelectedItems.Cast<object>());
        Assert.Equal(items.IndexOf(selected), selectionModel.SelectedIndex);
    }

    [AvaloniaFact]
    public void SelectionModel_Reflects_Grid_Selection_Changes()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var selectionModel = new SelectionModel<string> { SingleSelect = false };

        var grid = CreateGrid(items);
        grid.Selection = selectionModel;
        grid.UpdateLayout();

        grid.SelectedItem = items[2];
        grid.UpdateLayout();
        Assert.Equal(items[2], selectionModel.SelectedItem);
        Assert.Equal(2, selectionModel.SelectedIndex);

        selectionModel.Clear();
        selectionModel.Select(0);
        grid.UpdateLayout();

        Assert.Equal(items[0], grid.SelectedItem);
        Assert.Equal(0, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void Sorting_Does_Not_Clear_Selection_Model_Selection()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "Beta" },
            new() { Name = "Alpha" },
            new() { Name = "Gamma" },
        };

        var view = new DataGridCollectionView(items);
        var selectionModel = new SelectionModel<Item> { SingleSelect = false };

        var grid = CreateGrid(view, selectionModel);
        grid.UpdateLayout();

        selectionModel.Select(1); // select "Alpha" after binding
        grid.UpdateLayout();

        ApplySort(view, nameof(Item.Name), ListSortDirection.Ascending);
        grid.UpdateLayout();

        Assert.Contains(items[1], selectionModel.SelectedItems);
        Assert.Contains(items[1], grid.SelectedItems.Cast<object>());
        Assert.Equal(items[1], grid.SelectedItem);
        Assert.Equal(0, selectionModel.SelectedIndex); // moved to first after sort
    }

    [AvaloniaFact]
    public void Deferred_SelectedIndex_Before_ItemsSource_Is_Applied()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };

        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true
        };

        grid.Selection.SelectedIndex = 1;

        root.Content = grid;
        root.Show();

        grid.ItemsSource = items;
        grid.UpdateLayout();

        Assert.Equal("B", grid.SelectedItem);
        Assert.Equal(1, grid.SelectedIndex);
        Assert.Equal(new[] { "B" }, grid.SelectedItems.Cast<string>());
    }

    [AvaloniaFact]
    public void Deferred_SelectedItems_Before_ItemsSource_Are_Applied()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };

        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true
        };

        grid.Selection.SingleSelect = false;
        grid.Selection.Select(0);
        grid.Selection.Select(2);

        root.Content = grid;
        root.Show();

        grid.ItemsSource = items;
        grid.UpdateLayout();

        var selected = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "C" }, selected);
        Assert.Equal(new[] { 0, 2 }, grid.Selection.SelectedIndexes.ToArray());
    }

    [AvaloniaFact]
    public void Selection_With_Duplicate_Values_Preserves_Instance_On_Insert()
    {
        var items = new ObservableCollection<DupItem>
        {
            new("keep"),
            new("dup"),
            new("other"),
            new("dup")
        };

        var target = items[3];

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        grid.SelectedItem = target;
        grid.UpdateLayout();

        items.Insert(0, new DupItem("dup"));
        grid.UpdateLayout();

        var selected = Assert.IsType<DupItem>(grid.SelectedItem);
        Assert.True(ReferenceEquals(target, selected));
        Assert.Equal(FindIndexByReference(items, target), grid.Selection.SelectedIndex);
    }

    [AvaloniaFact]
    public void Toggling_SingleSelect_Updates_SelectionMode()
    {
        var items = new ObservableCollection<string> { "A", "B" };
        var grid = CreateGrid(items);
        grid.UpdateLayout();

        // Start in Extended
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Selection.SingleSelect = true;
        grid.UpdateLayout();

        Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);

        grid.Selection.SingleSelect = false;
        grid.UpdateLayout();

        Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
    }

    [AvaloniaFact]
    public void Binding_SelectedItems_Before_ItemsSource_Is_Replayed()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var selected = new ObservableCollection<object>();

        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true,
            SelectedItems = selected
        };

        selected.Add(items[1]);

        root.Content = grid;
        root.Show();

        grid.ItemsSource = items;
        grid.UpdateLayout();

        Assert.Equal("B", grid.SelectedItem);
        Assert.Equal(new[] { "B" }, grid.SelectedItems.Cast<string>());
    }

    [AvaloniaFact]
    public void Multiple_Rows_Remain_Selected_After_Scrolling_Away_And_Back()
    {
        var items = Enumerable.Range(0, 200).Select(i => $"Item {i}").ToList();

        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true,
            Height = 150,
            Width = 280
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        var log = new List<string>();
        grid.Selection.SelectionChanged += (_, e) =>
        {
            log.Add(
                $"add:{string.Join(",", e.SelectedIndexes)} remove:{string.Join(",", e.DeselectedIndexes)}");
            if (e.DeselectedIndexes.Count > 0)
            {
                var frames = new System.Diagnostics.StackTrace(skipFrames: 0, fNeedFileInfo: true)
                    .GetFrames()?
                    .Take(12)
                    .Select(f => $"{f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}")
                    .ToArray() ?? Array.Empty<string>();
                log.Add("stack:" + string.Join(" > ", frames));
            }
        };

        grid.Selection.SelectRange(5, 7);
        grid.UpdateLayout();

        Assert.Equal(new[] { 5, 6, 7 }, grid.Selection.SelectedIndexes.OrderBy(x => x));

        var model = grid.Selection;
        var sourceBeforeScroll = model.Source;

        grid.ScrollIntoView(items[150], grid.Columns[0]);
        grid.UpdateLayout();
        Assert.Same(sourceBeforeScroll, model.Source);
        var afterScroll = model.SelectedIndexes.OrderBy(x => x).ToArray();
        Assert.True(afterScroll.SequenceEqual(new[] { 5, 6, 7 }),
            $"Selection after scroll: [{string.Join(",", afterScroll)}]; log: {string.Join(" | ", log)}");

        grid.ScrollIntoView(items[5], grid.Columns[0]);
        grid.UpdateLayout();

        var afterReturn = grid.Selection.SelectedIndexes.OrderBy(x => x).ToArray();
        Assert.True(afterReturn.SequenceEqual(new[] { 5, 6, 7 }),
            $"Selection after return: [{string.Join(",", afterReturn)}]; log: {string.Join(" | ", log)}");
    }

    [AvaloniaFact]
    public void Selection_Visuals_Restore_After_Reattach()
    {
        var items = new ObservableCollection<string> { "A", "B", "C", "D", "E", "F" };

        var root = new Window
        {
            Width = 300,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true,
            Height = 150,
            Width = 280
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        grid.Selection.SelectRange(2, 4);
        grid.UpdateLayout();

        void AssertSelected()
        {
            grid.ApplyTemplate();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var selectedIndexes = grid.Selection.SelectedIndexes.OrderBy(x => x).ToArray();
            Assert.Equal(new[] { 2, 3, 4 }, selectedIndexes);

            var selectedRows = grid.GetSelfAndVisualDescendants()
                .OfType<DataGridRow>()
                .Where(r => r.IsSelected)
                .Select(r => r.DataContext)
                .OfType<string>()
                .OrderBy(x => x)
                .ToArray();
            Assert.Equal(new[] { "C", "D", "E" }, selectedRows);
        }

        AssertSelected();

        root.Content = null;
        Dispatcher.UIThread.RunJobs();

        root.Content = grid;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        AssertSelected();
    }

    [AvaloniaFact]
    public void RefreshSelectionFromModel_Resets_Invalid_CurrentSlot()
    {
        var items = new ObservableCollection<string> { "A", "B" };
        var grid = CreateGrid(items);
        grid.UpdateLayout();

        grid.SelectedIndex = 0;
        grid.UpdateLayout();

        SetPrivateProperty(grid, "CurrentColumnIndex", 0);
        SetPrivateProperty(grid, "CurrentSlot", grid.SlotCount);

        grid.RefreshSelectionFromModel();
        grid.UpdateLayout();

        Assert.True(grid.CurrentSlot >= 0 && grid.CurrentSlot < grid.SlotCount);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
    }

    private static DataGrid CreateGrid(IEnumerable items)
    {
        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(".")
        });

        root.Content = grid;
        root.Show();
        return grid;
    }

    private static void SetPrivateProperty<TValue>(object target, string propertyName, TValue value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class VisibleColumnItem
    {
        public VisibleColumnItem(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public int Value { get; }
    }

    private static DataGrid CreateGrid(IEnumerable items, SelectionModel<Item> selection)
    {
        var root = new Window
        {
            Width = 250,
            Height = 150,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        root.Content = grid;
        root.Show();
        return grid;
    }

    private static void ApplySort(DataGridCollectionView view, string propertyPath, ListSortDirection direction)
    {
        var assembly = typeof(DataGrid).Assembly;
        var sortType = assembly.GetType("Avalonia.Collections.DataGridSortDescription+DataGridPathSortDescription")
                       ?? throw new InvalidOperationException("Could not locate DataGridPathSortDescription type.");

        var sortDescription = Activator.CreateInstance(
            sortType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { propertyPath, direction, null, CultureInfo.InvariantCulture },
            culture: null) as DataGridSortDescription;

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(sortDescription!);
    }

    private class Item
    {
        public string Name { get; set; } = string.Empty;
    }

    private class DupItem
    {
        public DupItem(string name) => Name = name;

        public string Name { get; }

        public override bool Equals(object? obj) =>
            obj is DupItem other && string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

        public override string ToString() => Name;
    }

    private static int FindIndexByReference<T>(IEnumerable<T> source, T target) where T : class
    {
        var i = 0;
        foreach (var item in source)
        {
            if (ReferenceEquals(item, target))
            {
                return i;
            }
            i++;
        }

        return -1;
    }

    private static IReadOnlyList<DataGridRow> GetRows(DataGrid grid)
    {
        return grid.GetSelfAndVisualDescendants().OfType<DataGridRow>().ToList();
    }
}
