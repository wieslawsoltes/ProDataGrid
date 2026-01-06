// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridAddDeleteRowsTests
{
    public class TestItem
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void DataGridCollectionView_CanAddNew_Returns_True_For_ObservableCollection()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act & Assert
        Assert.True(view.CanAddNew, "CanAddNew should return true for ObservableCollection<T> with default constructor");
    }

    [Fact]
    public void DataGridCollectionView_CanRemove_Returns_True_For_ObservableCollection()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act & Assert
        Assert.True(view.CanRemove, "CanRemove should return true for ObservableCollection<T>");
    }

    [Fact]
    public void DataGridCollectionView_AddNew_Creates_New_Item()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act
        var newItem = view.AddNew();

        // Assert
        Assert.NotNull(newItem);
        Assert.IsType<TestItem>(newItem);
        Assert.Equal(2, view.Count); // Original + new item
    }

    [Fact]
    public void DataGridCollectionView_CommitNew_Persists_Item()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act
        var newItem = (TestItem)view.AddNew();
        newItem.Name = "New Item";
        view.CommitNew();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Contains(newItem, items);
    }

    [Fact]
    public void DataGridCollectionView_CancelNew_Removes_Item()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act
        var newItem = view.AddNew();
        view.CancelNew();

        // Assert
        Assert.Single(items);
        Assert.DoesNotContain(newItem, items);
    }

    [Fact]
    public void DataGridCollectionView_Remove_Removes_Item()
    {
        // Arrange
        var itemToRemove = new TestItem { Name = "A" };
        var items = new ObservableCollection<TestItem> { itemToRemove, new TestItem { Name = "B" } };
        var view = new DataGridCollectionView(items);

        // Act
        view.Remove(itemToRemove);

        // Assert
        Assert.Single(items);
        Assert.DoesNotContain(itemToRemove, items);
    }

    [Fact]
    public void EditableCollectionView_Is_Accessible_From_DataGridCollectionView()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        // Act
        var editable = view as IDataGridEditableCollectionView;

        // Assert
        Assert.NotNull(editable);
        Assert.True(editable.CanAddNew);
        Assert.True(editable.CanRemove);
    }

    [AvaloniaFact]
    public void DataGrid_Shows_Placeholder_Row_When_CanUserAddRows_Is_True()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act
        var rowCount = grid.GetVisualDescendants().OfType<DataGridRow>().Count();

        // Assert - should have 2 rows: 1 data row + 1 placeholder row
        Assert.True(grid.DataConnection.Count >= 2, $"DataConnection.Count should be at least 2, but was {grid.DataConnection.Count}");

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Hides_Placeholder_Row_When_CanUserAddRows_Is_False()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = false,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act & Assert - should have only 1 row (no placeholder)
        Assert.Equal(1, grid.DataConnection.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Generates_Placeholder_Row_Visual()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act - Count the visual DataGridRow elements
        var rows = grid.GetVisualDescendants().OfType<DataGridRow>().ToList();
        var placeholderRows = rows.Where(r => r.IsPlaceholder).ToList();

        // Assert - should have 2 rows: 1 data row + 1 placeholder row
        Assert.Equal(2, rows.Count);
        Assert.Single(placeholderRows);
        Assert.True(placeholderRows[0].IsPlaceholder);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Placeholder_Has_NewItemPlaceholder_DataContext()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act
        var placeholderRow = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsPlaceholder);

        // Assert
        Assert.NotNull(placeholderRow);
        Assert.Same(DataGridCollectionView.NewItemPlaceholder, placeholderRow.DataContext);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_CanRemove_Is_True_When_CanUserDeleteRows_Is_True()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserDeleteRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act & Assert
        Assert.True(grid.DataConnection.CanRemove);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_CanRemove_Is_False_When_CanUserDeleteRows_Is_False()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserDeleteRows = false,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act & Assert
        Assert.False(grid.DataConnection.CanRemove);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_SlotCount_Includes_Placeholder_When_CanUserAddRows_Is_True()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" }, new TestItem { Name = "B" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act & Assert
        // SlotCount should be 3: 2 data items + 1 placeholder
        Assert.Equal(3, grid.SlotCount);
        Assert.Equal(3, grid.DataConnection.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_SlotCount_Does_Not_Include_Placeholder_When_CanUserAddRows_Is_False()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" }, new TestItem { Name = "B" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = false,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Act & Assert
        // SlotCount should be 2: 2 data items only (no placeholder)
        Assert.Equal(2, grid.SlotCount);
        Assert.Equal(2, grid.DataConnection.Count);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Setting_CanUserAddRows_After_ItemsSource_Adds_Placeholder()
    {
        // Arrange - This tests the scenario where CanUserAddRows is set AFTER ItemsSource
        // Note: CanUserAddRows defaults to true, so we need to explicitly set it to false first
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" }, new TestItem { Name = "B" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            // Set CanUserAddRows to false BEFORE ItemsSource
            CanUserAddRows = false,
            ItemsSource = view,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Verify no placeholder yet
        Assert.Equal(2, grid.DataConnection.Count);
        Assert.Equal(2, grid.SlotCount);

        // Act - Now set CanUserAddRows = true (simulating binding evaluation)
        grid.CanUserAddRows = true;
        PumpLayout(grid);

        // Assert - SlotCount should now be 3: 2 data items + 1 placeholder
        Assert.Equal(3, grid.DataConnection.Count);
        Assert.Equal(3, grid.SlotCount);
        
        // Verify placeholder row is generated
        var placeholderRow = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsPlaceholder);
        Assert.NotNull(placeholderRow);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Setting_CanUserAddRows_False_After_True_Removes_Placeholder()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" }, new TestItem { Name = "B" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Verify placeholder exists
        Assert.Equal(3, grid.SlotCount);

        // Act - Set CanUserAddRows = false
        grid.CanUserAddRows = false;
        PumpLayout(grid);

        // Assert - SlotCount should now be 2: 2 data items only
        Assert.Equal(2, grid.DataConnection.Count);
        Assert.Equal(2, grid.SlotCount);
        
        // Verify no placeholder row
        var placeholderRow = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsPlaceholder);
        Assert.Null(placeholderRow);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_Placeholder_Row_Can_Be_Made_Current()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Get the placeholder row slot
        int placeholderSlot = grid.SlotCount - 1; // Last slot is the placeholder

        // Act - Set current cell to the placeholder row
        grid.CurrentColumn = grid.Columns[0];
        grid.ScrollIntoView(DataGridCollectionView.NewItemPlaceholder, grid.Columns[0]);
        PumpLayout(grid);

        // Get the placeholder row
        var placeholderRow = grid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(r => r.IsPlaceholder);
        Assert.NotNull(placeholderRow);
        
        // Verify the placeholder is visible
        Assert.True(placeholderRow.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_BeginEdit_On_Placeholder_Creates_New_Item()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") });

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        // Initial item count
        Assert.Single(items);

        // Get placeholder slot
        var placeholderRowIndex = grid.DataConnection.Count - 1; // Last index (includes placeholder)
        
        // Set current cell to placeholder
        grid.ScrollIntoView(DataGridCollectionView.NewItemPlaceholder, grid.Columns[0]);
        PumpLayout(grid);
        
        // Select the placeholder row via index (this should be the last row)
        grid.SelectedIndex = placeholderRowIndex;
        grid.CurrentColumn = grid.Columns[0];
        PumpLayout(grid);
        
        // Try to begin edit which should trigger AddNew
        var canEdit = grid.BeginEdit();
        PumpLayout(grid);

        // Assert - BeginEdit should succeed on placeholder row, and trigger AddNew
        Assert.True(canEdit, "BeginEdit should return true for placeholder row");
        Assert.True(view.IsAddingNew, "IsAddingNew should be true after BeginEdit on placeholder");

        window.Close();
    }

    [AvaloniaFact]
    public void DataGrid_BeginEdit_On_Placeholder_Allows_Cell_Editing()
    {
        // Arrange
        var items = new ObservableCollection<TestItem> { new TestItem { Name = "A" } };
        var view = new DataGridCollectionView(items);

        var window = new Window { Width = 400, Height = 300 };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = view,
            CanUserAddRows = true,
            IsReadOnly = false
        };

        var column = new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") };
        grid.Columns.Add(column);

        window.Content = grid;
        window.Show();
        PumpLayout(grid);

        var placeholderSlot = grid.SlotCount - 1;
        grid.UpdateSelectionAndCurrency(columnIndex: 0, slot: placeholderSlot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
        PumpLayout(grid);

        // Act
        Assert.True(grid.BeginEdit());
        PumpLayout(grid);

        var editingRow = grid.EditingRow;
        Assert.NotNull(editingRow);
        Assert.NotSame(DataGridCollectionView.NewItemPlaceholder, editingRow.DataContext);

        var editingCell = editingRow.Cells[column.Index];
        var textBox = Assert.IsType<TextBox>(editingCell.Content);

        textBox.Text = "New";
        BindingOperations.GetBindingExpressionBase(textBox, TextBox.TextProperty)?.UpdateSource();

        Assert.True(grid.CommitEdit());
        PumpLayout(grid);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("New", items.Last().Name);

        window.Close();
    }

    private static void PumpLayout(DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
