// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridAttachDetachModelTests
{
    private sealed class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class TreeItem
    {
        public TreeItem(string name)
        {
            Name = name;
            Children = new ObservableCollection<TreeItem>();
        }

        public string Name { get; }

        public ObservableCollection<TreeItem> Children { get; }
    }

    [AvaloniaFact]
    public void SelectionModel_Syncs_After_Detach_Attach()
    {
        var root = new TreeItem("root");
        var childA = new TreeItem("childA");
        var childB = new TreeItem("childB");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((TreeItem)item).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1
        });
        model.SetRoot(root);
        model.ExpandAll();

        var selection = new SelectionModel<HierarchicalNode> { SingleSelect = true };

        var grid = new DataGrid
        {
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Single,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var window = CreateWindow();
        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        InvokeMouseSelection(grid, model, childA);
        AssertSelection(selection, childA);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        InvokeMouseSelection(grid, model, childB);
        AssertSelection(selection, childB);

        window.Close();
    }

    [AvaloniaFact]
    public void SortingModel_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("B"),
            new("A"),
            new("C")
        };

        var grid = CreateBasicGrid(items);
        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        grid.ProcessSort(grid.Columns[0], KeyModifiers.None, ListSortDirection.Ascending);
        Dispatcher.UIThread.RunJobs();

        var sorted = grid.CollectionView.Cast<Item>().Select(i => i.Name).ToArray();
        Assert.Equal(new[] { "A", "B", "C" }, sorted);

        window.Close();
    }

    [AvaloniaFact]
    public void FilteringModel_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var grid = CreateBasicGrid(items);
        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        var columnId = DataGridColumnMetadata.GetColumnId(grid.Columns[0]);
        var descriptor = new FilteringDescriptor(
            columnId,
            FilteringOperator.Equals,
            propertyPath: "Name",
            value: "Beta");

        grid.FilteringModel.SetOrUpdate(descriptor);
        Dispatcher.UIThread.RunJobs();

        var filtered = grid.CollectionView.Cast<Item>().Select(i => i.Name).ToArray();
        Assert.Equal(new[] { "Beta" }, filtered);

        window.Close();
    }

    [AvaloniaFact]
    public void SearchModel_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var grid = CreateBasicGrid(items);
        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        grid.SearchModel.SetOrUpdate(new SearchDescriptor("Beta"));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(grid.SearchModel.Results, result => Equals(result.Item, items[1]));

        window.Close();
    }

    [AvaloniaFact]
    public void ConditionalFormattingModel_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var grid = CreateBasicGrid(items);
        var window = Attach(grid);

        var rowTheme = new ControlTheme(typeof(DataGridRow))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.LightGoldenrodYellow)
            }
        };

        window.Resources["RowTheme"] = rowTheme;

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        var descriptor = new ConditionalFormattingDescriptor(
            ruleId: "row-theme",
            @operator: ConditionalFormattingOperator.Equals,
            propertyPath: "Name",
            value: "Beta",
            themeKey: "RowTheme",
            target: ConditionalFormattingTarget.Row);

        grid.ConditionalFormattingModel.SetOrUpdate(descriptor);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        var row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => Equals(r.DataContext, items[1]));

        Assert.Same(rowTheme, row.Theme);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Syncs_After_Panel_Collapse_Expand()
    {
        var root = new TreeItem("root");
        var childA = new TreeItem("childA");
        var childB = new TreeItem("childB");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((TreeItem)item).Children,
            AutoExpandRoot = true,
            MaxAutoExpandDepth = 1
        });
        model.SetRoot(root);
        model.ExpandAll();

        var selection = new SelectionModel<HierarchicalNode> { SingleSelect = true };

        var grid = new DataGrid
        {
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Single,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var host = new ContentPresenter { Content = grid };
        var panel = new Border { Child = host };

        var window = CreateWindow();
        window.Content = panel;
        window.Show();
        PumpLayout(grid);

        InvokeMouseSelection(grid, model, childA);
        AssertSelection(selection, childA);

        host.Content = null;
        Dispatcher.UIThread.RunJobs();

        host.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        InvokeMouseSelection(grid, model, childB);
        AssertSelection(selection, childB);

        window.Close();
    }

    [AvaloniaFact]
    public void ClearRowSelection_Clears_SelectionModel_When_SelectedItemsEmpty()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta")
        };

        var selection = new SelectionModel<object?>();
        var grid = CreateBasicGrid(items);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Selection = selection;

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        Dispatcher.UIThread.RunJobs();

        Assert.Single(selection.SelectedItems);

        var previousSync = grid.PushSelectionSync();
        grid.ClearRowSelection(resetAnchorSlot: true);
        grid.PopSelectionSync(previousSync);

        Assert.Single(selection.SelectedItems);

        grid.ClearRowSelection(resetAnchorSlot: true);

        Assert.Empty(selection.SelectedItems);

        window.Close();
    }

    [AvaloniaFact]
    public void ClearRowSelectionWithException_Replaces_SelectionModel_When_SelectedItemsEmpty()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta")
        };

        var selection = new SelectionModel<object?>();
        var grid = CreateBasicGrid(items);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Selection = selection;

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        Dispatcher.UIThread.RunJobs();

        Assert.Single(selection.SelectedItems);

        var previousSync = grid.PushSelectionSync();
        grid.ClearRowSelection(resetAnchorSlot: true);
        grid.PopSelectionSync(previousSync);

        Assert.Single(selection.SelectedItems);

        var slot = grid.SlotFromRowIndex(1);
        grid.ClearRowSelection(slotException: slot, setAnchorSlot: true);

        var selected = Assert.Single(selection.SelectedItems);
        Assert.Same(items[1], selected);

        window.Close();
    }

    private static DataGrid CreateBasicGrid(ObservableCollection<Item> items)
    {
        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name))
        });

        return grid;
    }

    private static Window Attach(DataGrid grid)
    {
        var window = CreateWindow();
        window.Content = grid;
        window.Show();
        PumpLayout(grid);
        return window;
    }

    private static Window CreateWindow()
    {
        var window = new Window
        {
            Width = 800,
            Height = 600
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        return window;
    }

    private static void PumpLayout(DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        if (grid.GetVisualRoot() is Window window)
        {
            window.ApplyTemplate();
            window.UpdateLayout();
        }
        grid.ApplyTemplate();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void InvokeMouseSelection(DataGrid grid, HierarchicalModel model, TreeItem target)
    {
        var rowIndex = model.IndexOf(target);
        if (rowIndex < 0)
        {
            throw new InvalidOperationException("Target item is not visible in the flattened hierarchy.");
        }

        var slot = grid.SlotFromRowIndex(rowIndex);
        var handled = grid.UpdateStateOnMouseLeftButtonDown(
            CreateLeftPointerArgs(grid),
            columnIndex: 0,
            slot: slot,
            allowEdit: false);

        if (!handled)
        {
            throw new InvalidOperationException("Mouse selection was not handled by the grid.");
        }
    }

    private static PointerPressedEventArgs CreateLeftPointerArgs(Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(target, pointer, target, new Point(0, 0), 0, properties, KeyModifiers.None);
    }

    private static void AssertSelection(SelectionModel<HierarchicalNode> selection, TreeItem expected)
    {
        var node = Assert.IsType<HierarchicalNode>(Assert.Single(selection.SelectedItems));
        Assert.Same(expected, node.Item);
    }
}
