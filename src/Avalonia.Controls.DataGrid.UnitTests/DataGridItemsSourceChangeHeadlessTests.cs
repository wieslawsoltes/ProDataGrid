// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridItemsSourceChangeHeadlessTests
{
    [AvaloniaTheory]
    [InlineData(DataGridSelectionUnit.FullRow)]
    [InlineData(DataGridSelectionUnit.Cell)]
    public void ItemsSource_Swap_List_To_List_Allows_Click_After_Change(DataGridSelectionUnit selectionUnit)
    {
        var items = CreateItems("A", 3);
        var (window, grid) = CreateGrid(items, selectionUnit);
        var newItems = CreateItems("B", 2);

        grid.SelectedIndex = 0;
        PumpLayout(grid);

        grid.ItemsSource = newItems;
        PumpLayout(grid);

        Assert.Null(grid.SelectedItem);

        ClickCell(GetCell(grid, rowIndex: 0, columnIndex: 0));
        PumpLayout(grid);

        AssertSelection(grid, newItems[0], selectionUnit);
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(DataGridSelectionUnit.FullRow)]
    [InlineData(DataGridSelectionUnit.Cell)]
    public void ItemsSource_Clear_Then_Rebind_Allows_Click(DataGridSelectionUnit selectionUnit)
    {
        var items = CreateItems("A", 2);
        var (window, grid) = CreateGrid(items, selectionUnit);
        var newItems = CreateItems("B", 2);

        grid.ItemsSource = null;
        PumpLayout(grid);

        Assert.Null(grid.SelectedItem);
        Assert.False(grid.CurrentCell.IsValid);

        grid.ItemsSource = newItems;
        PumpLayout(grid);

        ClickCell(GetCell(grid, rowIndex: 0, columnIndex: 0));
        PumpLayout(grid);

        AssertSelection(grid, newItems[0], selectionUnit);
        window.Close();
    }

    [AvaloniaFact]
    public void ItemsSource_Swap_List_To_View_Selects_CurrentItem_And_Allows_Click()
    {
        var items = CreateItems("A", 2);
        var (window, grid) = CreateGrid(items, DataGridSelectionUnit.FullRow);
        var newItems = CreateItems("B", 3);
        var view = new DataGridCollectionView(newItems);

        view.MoveCurrentTo(newItems[1]);

        grid.ItemsSource = view;
        PumpLayout(grid);

        Assert.Same(newItems[1], grid.SelectedItem);

        ClickCell(GetCell(grid, rowIndex: 0, columnIndex: 0));
        PumpLayout(grid);

        Assert.Same(newItems[0], grid.SelectedItem);
        window.Close();
    }

    [AvaloniaFact]
    public void ItemsSource_Swap_Grouped_View_To_List_Removes_Group_Headers_And_Allows_Click()
    {
        var groupedItems = CreateGroupedItems();
        var groupedView = new DataGridCollectionView(groupedItems);
        groupedView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(RowItem.Group)));

        var (window, grid) = CreateGrid(groupedView, DataGridSelectionUnit.FullRow);
        PumpLayout(grid);

        Assert.NotEmpty(GetGroupHeaders(grid));

        var newItems = CreateItems("C", 2);
        grid.ItemsSource = newItems;
        PumpLayout(grid);

        Assert.Empty(GetGroupHeaders(grid));

        ClickCell(GetCell(grid, rowIndex: 0, columnIndex: 0));
        PumpLayout(grid);

        Assert.Same(newItems[0], grid.SelectedItem);
        window.Close();
    }

    [AvaloniaFact]
    public void ItemsSource_Swap_While_Editing_Cancels_Edit_And_Allows_Click()
    {
        var items = CreateItems("A", 2);
        var (window, grid) = CreateGrid(items, DataGridSelectionUnit.Cell);
        var slot = grid.SlotFromRowIndex(0);

        Assert.True(grid.UpdateSelectionAndCurrency(0, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: true));
        PumpLayout(grid);

        Assert.True(grid.BeginEdit());
        PumpLayout(grid);

        Assert.NotEqual(-1, grid.EditingColumnIndex);

        var newItems = CreateItems("B", 2);
        grid.ItemsSource = newItems;
        PumpLayout(grid);

        Assert.Equal(-1, grid.EditingColumnIndex);
        Assert.False(grid.CurrentCell.IsValid);

        ClickCell(GetCell(grid, rowIndex: 0, columnIndex: 0));
        PumpLayout(grid);

        AssertSelection(grid, newItems[0], DataGridSelectionUnit.Cell);
        window.Close();
    }

    [AvaloniaFact]
    public void ItemsSource_Swap_HierarchicalModel_Updates_Owned_Source()
    {
        var (window, grid) = CreateHierarchicalGrid();
        grid.HierarchicalRowsEnabled = true;

        var modelA = CreateHierarchicalModel("A");
        grid.HierarchicalModel = modelA;
        PumpLayout(grid);

        var untypedModelA = (IHierarchicalModel)modelA;
        Assert.Same(untypedModelA.ObservableFlattened, grid.ItemsSource);

        var modelB = CreateHierarchicalModel("B");
        grid.HierarchicalModel = modelB;
        PumpLayout(grid);

        var untypedModelB = (IHierarchicalModel)modelB;
        Assert.Same(untypedModelB.ObservableFlattened, grid.ItemsSource);
        window.Close();
    }

    [AvaloniaFact]
    public void ItemsSource_Swap_From_HierarchicalModel_Rewires_SelectionModel()
    {
        var (window, grid) = CreateHierarchicalGrid();
        grid.HierarchicalRowsEnabled = true;

        var model = CreateHierarchicalModel("A");
        grid.HierarchicalModel = model;

        var selectionModel = new SelectionModel<object> { SingleSelect = false };
        grid.Selection = selectionModel;
        PumpLayout(grid);

        Assert.Same(grid.CollectionView, selectionModel.Source);

        if (selectionModel.Source is IList list && list.Count > 0)
        {
            selectionModel.Select(0);
        }

        var flatItems = CreateItems("Flat", 2);
        grid.ItemsSource = flatItems;
        PumpLayout(grid);

        Assert.Same(grid.CollectionView, selectionModel.Source);
        Assert.Equal(0, selectionModel.SelectedIndex);
        Assert.Same(flatItems[0], selectionModel.SelectedItem);
        Assert.Same(flatItems[0], grid.SelectedItem);

        window.Close();
    }

    private static (Window Window, DataGrid Grid) CreateGrid(IEnumerable? itemsSource, DataGridSelectionUnit selectionUnit)
    {
        var window = new Window
        {
            Width = 640,
            Height = 480
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = itemsSource,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = selectionUnit,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(RowItem.Value))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private static (Window Window, DataGrid Grid) CreateHierarchicalGrid()
    {
        var window = new Window
        {
            Width = 640,
            Height = 480
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = null,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private static ObservableCollection<RowItem> CreateItems(string prefix, int count)
    {
        var items = new ObservableCollection<RowItem>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new RowItem
            {
                Name = $"{prefix}{i}",
                Value = i,
                Group = i % 2 == 0 ? "A" : "B"
            });
        }

        return items;
    }

    private static HierarchicalModel<TreeNode> CreateHierarchicalModel(string prefix)
    {
        var root = new TreeNode($"{prefix}-root");
        root.Children.Add(new TreeNode($"{prefix}-child-1"));
        root.Children.Add(new TreeNode($"{prefix}-child-2"));

        var options = new HierarchicalOptions<TreeNode>
        {
            ChildrenSelector = node => node.Children
        };

        var model = new HierarchicalModel<TreeNode>(options);
        model.SetRoot(root);
        return model;
    }

    private static ObservableCollection<RowItem> CreateGroupedItems()
    {
        return new ObservableCollection<RowItem>
        {
            new() { Name = "A1", Value = 1, Group = "A" },
            new() { Name = "A2", Value = 2, Group = "A" },
            new() { Name = "B1", Value = 3, Group = "B" },
            new() { Name = "B2", Value = 4, Group = "B" }
        };
    }

    private static void PumpLayout(DataGrid grid)
    {
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
    }

    private static DataGridCell GetCell(DataGrid grid, int rowIndex, int columnIndex)
    {
        var slot = grid.SlotFromRowIndex(rowIndex);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;

        Assert.NotNull(row);

        var cell = row!.Cells[columnIndex];

        Assert.NotNull(cell);

        return cell;
    }

    private static void ClickCell(DataGridCell cell)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(cell, pointer, cell, new Point(2, 2), 0, properties, KeyModifiers.None);

        cell.RaiseEvent(args);
    }

    private static void AssertSelection(DataGrid grid, RowItem expected, DataGridSelectionUnit selectionUnit)
    {
        if (selectionUnit == DataGridSelectionUnit.Cell)
        {
            Assert.True(grid.CurrentCell.IsValid);
            Assert.Same(expected, grid.CurrentCell.Item);
            Assert.NotEmpty(grid.SelectedCells);
        }
        else
        {
            Assert.Same(expected, grid.SelectedItem);
            Assert.NotEmpty(grid.SelectedItems);
        }
    }

    private static DataGridRowGroupHeader[] GetGroupHeaders(DataGrid grid)
    {
        return grid.GetSelfAndVisualDescendants()
            .OfType<DataGridRowGroupHeader>()
            .ToArray();
    }

    private class RowItem
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Group { get; set; } = string.Empty;
    }

    private class TreeNode
    {
        public TreeNode(string name)
        {
            Name = name;
            Children = new ObservableCollection<TreeNode>();
        }

        public string Name { get; }
        public ObservableCollection<TreeNode> Children { get; }
    }
}
