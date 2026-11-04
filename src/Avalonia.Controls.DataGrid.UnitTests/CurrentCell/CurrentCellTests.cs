// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.CurrentCell;

public class CurrentCellTests
{
    [AvaloniaTheory]
    [InlineData(DataGridSelectionMode.Single, false)]
    [InlineData(DataGridSelectionMode.Single, true)]
    [InlineData(DataGridSelectionMode.Extended, false)]
    [InlineData(DataGridSelectionMode.Extended, true)]
    public void CurrentCell_Can_Move_And_Edit_Without_Changing_Selection(DataGridSelectionMode mode, bool markFirstRow)
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "First" },
            new() { Name = "Second" },
            new() { Name = "Third" }
        };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        grid.SelectionMode = mode;
        grid.SelectedItems.Clear();
        if (markFirstRow)
            grid.SelectedIndex = 0;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var expectedSelection = grid.SelectedItems.Cast<object>().ToArray();
        var selectionChanges = 0;
        grid.SelectionChanged += (_, _) => selectionChanges++;
        try
        {
            var column = grid.Columns[0];
            Assert.True(grid.TrySetCurrentCell(new DataGridCellInfo(items[2], column, 2, 0), updateSelection: false));
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(items[2], grid.CurrentCell.Item);
            Assert.Equal(expectedSelection, grid.SelectedItems.Cast<object>().ToArray());
            Assert.Equal(0, selectionChanges);

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var row = grid.GetVisualDescendants().OfType<DataGridRow>()
                .Single(candidate => ReferenceEquals(candidate.DataContext, items[2]));
            Assert.False(row.IsSelected);
            var editor = row.GetVisualDescendants().OfType<TextBox>().Single();
            editor.Text = "Changed";
            Assert.True(grid.CommitEdit());
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Changed", items[2].Name);
            Assert.Equal(expectedSelection, grid.SelectedItems.Cast<object>().ToArray());
            Assert.Equal(0, selectionChanges);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridSelectionMode.Single, false)]
    [InlineData(DataGridSelectionMode.Single, true)]
    [InlineData(DataGridSelectionMode.Extended, false)]
    [InlineData(DataGridSelectionMode.Extended, true)]
    public void CurrentCell_Can_Select_Already_Current_Cell(DataGridSelectionMode mode, bool markFirstRow)
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "First" },
            new() { Name = "Second" }
        };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        try
        {
            grid.SelectionMode = mode;
            grid.SelectedItems.Clear();
            if (markFirstRow)
                grid.SelectedIndex = 0;
            var expectedSelection = grid.SelectedItems.Cast<object>().ToArray();
            var selectionChanges = 0;
            grid.SelectionChanged += (_, _) => selectionChanges++;

            var column = grid.Columns[0];
            var cell = new DataGridCellInfo(items[1], column, 1, column.Index);
            Assert.True(grid.TrySetCurrentCell(cell, updateSelection: false));
            Assert.Equal(expectedSelection, grid.SelectedItems.Cast<object>().ToArray());
            Assert.Equal(0, selectionChanges);

            var currentCellChanges = 0;
            grid.CurrentCellChanged += (_, _) => currentCellChanges++;
            Assert.True(grid.TrySetCurrentCell(cell, updateSelection: true));
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(items[1], Assert.Single(grid.SelectedItems.Cast<object>()));
            Assert.Equal(1, grid.SelectedIndex);
            Assert.Equal(1, selectionChanges);
            Assert.Equal(0, currentCellChanges);
            Assert.Equal(cell, grid.CurrentCell);

            Assert.True(grid.TrySetCurrentCell(cell, updateSelection: true));
            Assert.Equal(1, selectionChanges);
            Assert.Equal(0, currentCellChanges);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentCell_Reassigning_Selected_Current_Cell_Preserves_Extended_Selection(bool usePropertySetter)
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "First" },
            new() { Name = "Second" }
        };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        try
        {
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.SelectedItems.Clear();
            grid.SelectedItems.Add(items[0]);
            grid.SelectedItems.Add(items[1]);
            var column = grid.Columns[0];
            var cell = new DataGridCellInfo(items[1], column, 1, column.Index);
            Assert.True(grid.TrySetCurrentCell(cell, updateSelection: false));
            var expectedSelection = grid.SelectedItems.Cast<object>().ToArray();
            Assert.Equal(2, expectedSelection.Length);
            var selectionChanges = 0;
            grid.SelectionChanged += (_, _) => selectionChanges++;

            if (usePropertySetter)
                grid.CurrentCell = cell;
            else
                Assert.True(grid.TrySetCurrentCell(cell, updateSelection: true));
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(expectedSelection, grid.SelectedItems.Cast<object>().ToArray());
            Assert.Equal(0, selectionChanges);
            Assert.Equal(cell, grid.CurrentCell);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridSelectionMode.Single, false)]
    [InlineData(DataGridSelectionMode.Single, true)]
    [InlineData(DataGridSelectionMode.Extended, false)]
    [InlineData(DataGridSelectionMode.Extended, true)]
    public void CurrentCell_Column_Move_Preserves_Unselected_Row_Edit_For_Cancel(DataGridSelectionMode mode, bool markFirstRow)
    {
        var items = new ObservableCollection<EditableItem>
        {
            new() { First = "First row", Second = "Other value" },
            new() { First = "Original first", Second = "Original second" }
        };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        try
        {
            grid.SelectionMode = mode;
            grid.SelectedItems.Clear();
            if (markFirstRow)
                grid.SelectedIndex = 0;
            var expectedSelection = grid.SelectedItems.Cast<object>().ToArray();
            var selectionChanges = 0;
            grid.SelectionChanged += (_, _) => selectionChanges++;

            var firstColumn = grid.Columns.Single(column => Equals(column.Header, nameof(EditableItem.First)));
            var secondColumn = grid.Columns.Single(column => Equals(column.Header, nameof(EditableItem.Second)));
            Assert.True(grid.TrySetCurrentCell(new DataGridCellInfo(items[1], firstColumn, 1, firstColumn.Index), updateSelection: false));
            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var row = grid.GetVisualDescendants().OfType<DataGridRow>()
                .Single(candidate => ReferenceEquals(candidate.DataContext, items[1]));
            row.GetVisualDescendants().OfType<TextBox>().Single().Text = "Changed first";

            Assert.True(grid.TrySetCurrentCell(new DataGridCellInfo(items[1], secondColumn, 1, secondColumn.Index), updateSelection: false));
            Assert.Equal("Changed first", items[1].First);
            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            row.GetVisualDescendants().OfType<TextBox>().Single().Text = "Changed second";
            Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
            Assert.Equal("Changed second", items[1].Second);

            Assert.True(grid.CancelEdit(DataGridEditingUnit.Row));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Original first", items[1].First);
            Assert.Equal("Original second", items[1].Second);
            Assert.Null(grid.EditingRow);
            Assert.False(row.IsSelected);
            Assert.Equal(expectedSelection, grid.SelectedItems.Cast<object>().ToArray());
            Assert.Equal(0, selectionChanges);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CurrentCell_Can_Move_Before_First_Row_Layout_Without_Selecting_Inserted_Rows()
    {
        var items = new ObservableCollection<Item>();
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        grid.AutoGenerateColumns = false;
        var column = new DataGridTextColumn { Binding = new Avalonia.Data.Binding(nameof(Item.Name)) };
        grid.Columns.Add(column);
        try
        {
            for (var index = 0; index < 3; index++)
            {
                var item = new Item { Name = index.ToString() };
                items.Add(item);
                Assert.True(grid.TrySetCurrentCell(new DataGridCellInfo(item, column, index, 0), updateSelection: false));
            }

            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(items[2], grid.CurrentCell.Item);
            Assert.Empty(grid.SelectedItems);

            grid.SelectedIndex = 2;
            grid.UpdateLayout();
            Assert.Single(grid.SelectedItems);
            Assert.Same(items[2], grid.SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TrySetCurrentCell_Reports_Cancelled_Move_Or_Clear(bool clear, bool updateSelection)
    {
        var items = new ObservableCollection<Item> { new() { Name = "A" }, new() { Name = "B" } };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        try
        {
            grid.SelectedIndex = 0;
            var originalCell = grid.CurrentCell;
            var originalSelection = grid.SelectedItems.Cast<object>().ToArray();
            var target = clear ? DataGridCellInfo.Unset : new DataGridCellInfo(items[1], grid.Columns[0], 1, 0);
            grid.SelectionChanging += (_, args) => args.Cancel = true;

            Assert.False(grid.TrySetCurrentCell(target, updateSelection));
            Assert.Equal(originalCell, grid.CurrentCell);
            Assert.Equal(originalSelection, grid.SelectedItems.Cast<object>().ToArray());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TrySetCurrentCell_Reports_Unresolvable_Item_And_Successful_Clear()
    {
        var items = new ObservableCollection<Item> { new() { Name = "A" } };
        var grid = CreateGrid(items);
        var window = (Window)grid.GetVisualRoot()!;
        try
        {
            grid.SelectedIndex = 0;
            var originalCell = grid.CurrentCell;
            Assert.False(grid.TrySetCurrentCell(new DataGridCellInfo(new Item(), grid.Columns[0], 10, 0), false));
            Assert.Equal(originalCell, grid.CurrentCell);
            Assert.True(grid.TrySetCurrentCell(DataGridCellInfo.Unset, false));
            Assert.False(grid.CurrentCell.IsValid);
            Assert.Same(items[0], Assert.Single(grid.SelectedItems.Cast<object>()));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CurrentCell_Property_Changes_When_Selection_Moves()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
            new() { Name = "C" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        var changes = new List<DataGridCellInfo>();
        grid.PropertyChanged += (_, e) =>
        {
            if (e.Property == DataGrid.CurrentCellProperty)
            {
                changes.Add(e.GetNewValue<DataGridCellInfo>());
            }
        };

        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.CurrentCell.Item);
        Assert.Contains(changes, c => Equals(c.Item, items[1]) && c.RowIndex == 1);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_Moves_Currency_And_Raises_Event()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
            new() { Name = "C" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var targetColumn = grid.Columns.First();
        var eventHits = 0;
        grid.CurrentCellChanged += (_, e) =>
        {
            eventHits++;
            Assert.Same(targetColumn, e.NewColumn);
            Assert.Equal(items[1], e.NewItem);
        };

        grid.CurrentCell = new DataGridCellInfo(items[1], targetColumn, 1, targetColumn.Index, isValid: true);
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(targetColumn, grid.CurrentColumn);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.SelectedItem);
        Assert.True(eventHits >= 1);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_To_Unset_Clears_Currency_But_Not_Selection()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        Assert.True(grid.CurrentCell.IsValid);

        grid.CurrentCell = DataGridCellInfo.Unset;
        grid.UpdateLayout();

        Assert.False(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.SelectedIndex);
        Assert.Equal(items[1], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Foreign_Column_Throws()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.UpdateLayout();

        var foreignColumn = new DataGridTextColumn();

        Assert.Throws<ArgumentException>(() =>
            grid.CurrentCell = new DataGridCellInfo(items[0], foreignColumn, 0, 0, isValid: true));
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Out_Of_Range_Row_Does_Not_Change_Current()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[0], column, 0, columnIndex: 0, isValid: true);
        var baseline = grid.CurrentCell;

        grid.CurrentCell = new DataGridCellInfo(items[0], column, rowIndex: 5, columnIndex: 0, isValid: true);

        Assert.Equal(baseline, grid.CurrentCell);
        Assert.Equal(0, grid.CurrentCell.RowIndex);
    }

    [AvaloniaFact]
    public void Setting_CurrentCell_With_Item_Recalculates_RowIndex()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[1], column, rowIndex: 0, columnIndex: 0, isValid: true);

        Assert.True(grid.CurrentCell.IsValid);
        Assert.Equal(1, grid.CurrentCell.RowIndex);
        Assert.Equal(items[1], grid.CurrentCell.Item);
    }

    [AvaloniaFact]
    public void Clearing_ItemsSource_Resets_CurrentCell()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[0], column, 0, 0, isValid: true);
        Assert.True(grid.CurrentCell.IsValid);

        grid.ItemsSource = null;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.False(grid.CurrentCell.IsValid);
    }

    [AvaloniaFact]
    public void Resetting_CurrentCell_Handles_Stale_CurrentSlot_When_SlotCount_Is_Zero()
    {
        var items = new ObservableCollection<Item>
        {
            new() { Name = "A" },
            new() { Name = "B" },
        };

        var grid = CreateGrid(items);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.UpdateLayout();

        var column = grid.Columns.First();
        grid.CurrentCell = new DataGridCellInfo(items[0], column, 0, 0, isValid: true);
        Assert.True(grid.CurrentCell.IsValid);

        // Recreate the state from issue #269: SlotCount cleared while CurrentSlot still points
        // to a previously selected row.
        SetPrivateProperty(grid, "SlotCount", 0);
        SetPrivateProperty(grid, "CurrentSlot", 1);

        grid.CurrentCell = DataGridCellInfo.Unset;
        grid.UpdateLayout();

        Assert.False(grid.CurrentCell.IsValid);
        Assert.Equal(-1, grid.CurrentSlot);
    }

    private static DataGrid CreateGrid<T>(IEnumerable<T> items)
    {
        var root = new Window
        {
            Width = 320,
            Height = 240,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = true,
            CanUserAddRows = false,
        };

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();
        return grid;
    }

    private static void SetPrivateProperty(object target, string propertyName, object value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private sealed class EditableItem : IEditableObject
    {
        private (string First, string Second)? _snapshot;

        public string First { get; set; } = string.Empty;
        public string Second { get; set; } = string.Empty;

        public void BeginEdit()
        {
            _snapshot ??= (First, Second);
        }

        public void CancelEdit()
        {
            if (_snapshot is { } snapshot)
            {
                First = snapshot.First;
                Second = snapshot.Second;
            }
            _snapshot = null;
        }

        public void EndEdit()
        {
            _snapshot = null;
        }
    }

    private class Item
    {
        public string Name { get; set; } = string.Empty;
    }
}
