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

    private sealed class SelectionBindingViewModel
    {
        public ObservableCollection<Item> SelectedItems { get; } = new();

        public ObservableCollection<DataGridCellInfo> SelectedCells { get; } = new();
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
    public void SelectedItemsBinding_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var vm = new SelectionBindingViewModel();
        var grid = CreateBasicGrid(items);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Bind(DataGrid.SelectedItemsProperty, new Binding(nameof(SelectionBindingViewModel.SelectedItems))
        {
            Mode = BindingMode.TwoWay,
            Source = vm
        });

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        grid.SelectedItems.Add(items[1]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.SelectedItems.Count);
        Assert.Contains(items[0], vm.SelectedItems);
        Assert.Contains(items[1], vm.SelectedItems);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        vm.SelectedItems.Clear();
        vm.SelectedItems.Add(items[2]);

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[2], grid.SelectedItems.Cast<object>());
        Assert.Same(items[2], grid.SelectedItem);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectedCellsBinding_Reattaches_After_Detach_Attach()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var vm = new SelectionBindingViewModel();
        var grid = CreateBasicGrid(items);
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.Bind(DataGrid.SelectedCellsProperty, new Binding(nameof(SelectionBindingViewModel.SelectedCells))
        {
            Mode = BindingMode.TwoWay,
            Source = vm
        });

        var window = Attach(grid);
        var column = grid.Columns[0];

        vm.SelectedCells.Add(new DataGridCellInfo(items[1], column, 1, column.Index, isValid: true));
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Single(grid.SelectedCells);
        Assert.Same(items[1], grid.SelectedItem);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        vm.SelectedCells.Clear();
        vm.SelectedCells.Add(new DataGridCellInfo(items[0], column, 0, column.Index, isValid: true));

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Single(grid.SelectedCells);
        Assert.Same(items[0], grid.SelectedItem);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == column.Index);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionMode_Tracks_SelectionModel_When_Model_Changes_While_Detached()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = true };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Single;

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        Dispatcher.UIThread.RunJobs();

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.SingleSelect = false;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.False(selection.SingleSelect);
        Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Tracks_SelectionMode_When_Mode_Changes_While_Detached_With_HierarchicalProxy()
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

        var selection = new SelectionModel<HierarchicalNode> { SingleSelect = false };

        var grid = new DataGrid
        {
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        grid.SelectionMode = DataGridSelectionMode.Single;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.True(selection.SingleSelect);
        Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);

        InvokeMouseSelection(grid, model, childA);
        AssertSelection(selection, childA);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionMode_Tracks_SelectionModel_When_Model_Changes_While_Detached_With_HierarchicalProxy()
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

        var selection = new SelectionModel<HierarchicalNode> { SingleSelect = false };

        var grid = new DataGrid
        {
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            Selection = selection,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.SingleSelect = true;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.True(selection.SingleSelect);
        Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);

        InvokeMouseSelection(grid, model, childB);
        AssertSelection(selection, childB);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionMode_Wins_When_SelectionModel_Changes_Opposite_Direction_While_Detached()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = true };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Single;

        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        grid.SelectionMode = DataGridSelectionMode.Extended;
        selection.SingleSelect = false;
        selection.SingleSelect = true;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Equal(DataGridSelectionMode.Extended, grid.SelectionMode);
        Assert.False(selection.SingleSelect);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionMode_Wins_When_SelectionModel_Changes_Opposite_Direction_ToSingle_While_Detached()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = false };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Extended;

        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        grid.SelectionMode = DataGridSelectionMode.Single;
        selection.SingleSelect = true;
        selection.SingleSelect = false;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);
        Assert.True(selection.SingleSelect);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Tracks_SelectionMode_When_Mode_Changes_While_Detached()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = false };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Extended;

        var window = Attach(grid);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        grid.SelectionMode = DataGridSelectionMode.Single;

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.True(selection.SingleSelect);
        Assert.Equal(DataGridSelectionMode.Single, grid.SelectionMode);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Clear_While_Detached_Is_Respected()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = true };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Single;

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        Dispatcher.UIThread.RunJobs();

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.Clear();
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        Assert.Empty(selection.SelectedItems);
        Assert.Null(grid.SelectedItem);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_Changes_While_Detached_Are_Respected_With_HierarchicalProxy()
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

        var window = Attach(grid);

        InvokeMouseSelection(grid, model, childA);
        AssertSelection(selection, childA);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.Clear();
        selection.Select(model.IndexOf(childB));
        Dispatcher.UIThread.RunJobs();

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        PumpLayout(grid);

        AssertSelection(selection, childB);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_SelectionChanged_Items_Are_Not_Null_When_Detached()
    {
        var items = new ObservableCollection<Item>
        {
            new("Alpha"),
            new("Beta"),
            new("Gamma")
        };

        var selection = new SelectionModel<object?> { SingleSelect = true };
        var grid = CreateBasicGrid(items);
        grid.Selection = selection;
        grid.SelectionMode = DataGridSelectionMode.Single;

        var window = Attach(grid);

        grid.SelectedItem = items[0];
        Dispatcher.UIThread.RunJobs();

        SelectionModelSelectionChangedEventArgs<object?>? args = null;
        selection.SelectionChanged += (_, e) => args = e;

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.Select(1);
        Dispatcher.UIThread.RunJobs();

        var captured = args ?? throw new InvalidOperationException("Expected selection change.");
        Assert.Single(captured.SelectedItems);
        Assert.Same(items[1], captured.SelectedItems[0]);
        Assert.Single(captured.DeselectedItems);
        Assert.Same(items[0], captured.DeselectedItems[0]);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectionModel_SelectionChanged_Items_Are_Not_Null_When_Detached_With_HierarchicalProxy()
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

        var window = Attach(grid);

        InvokeMouseSelection(grid, model, childA);
        Dispatcher.UIThread.RunJobs();

        SelectionModelSelectionChangedEventArgs<HierarchicalNode>? args = null;
        selection.SelectionChanged += (_, e) => args = e;

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        selection.Select(model.IndexOf(childB));
        Dispatcher.UIThread.RunJobs();

        var captured = args ?? throw new InvalidOperationException("Expected selection change.");
        Assert.Single(captured.SelectedItems);
        var selected = captured.SelectedItems[0] ?? throw new InvalidOperationException("Expected selected item.");
        Assert.Same(childB, selected.Item);
        Assert.Single(captured.DeselectedItems);
        var deselected = captured.DeselectedItems[0] ?? throw new InvalidOperationException("Expected deselected item.");
        Assert.Same(childA, deselected.Item);

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
