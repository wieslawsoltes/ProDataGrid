// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridToggleButtonColumnHeadlessTests
{
    [Fact]
    public void ToggleButtonColumn_XamlBinding_Assigns_Content_States()
    {
        const string xaml = """
                            <DataGridToggleButtonColumn xmlns="https://github.com/avaloniaui"
                                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                        Content="{Binding Symbol}"
                                                        CheckedContent="{Binding CheckedLabel}"
                                                        UncheckedContent="{Binding UncheckedLabel}" />
                            """;

        var column = AvaloniaRuntimeXamlLoader.Parse<DataGridToggleButtonColumn>(xaml, typeof(DataGridToggleButtonColumn).Assembly);

        Assert.IsAssignableFrom<BindingBase>(column.Content);
        Assert.IsAssignableFrom<BindingBase>(column.CheckedContent);
        Assert.IsAssignableFrom<BindingBase>(column.UncheckedContent);
    }

    [Fact]
    public void ToggleButtonColumn_Content_Properties_Use_AssignBinding()
    {
        AssertHasAssignBinding(nameof(DataGridToggleButtonColumn.Content));
        AssertHasAssignBinding(nameof(DataGridToggleButtonColumn.CheckedContent));
        AssertHasAssignBinding(nameof(DataGridToggleButtonColumn.UncheckedContent));
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Binds_Value()
    {
        var vm = new ToggleButtonTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Favorite", 0);
        var toggleButton = Assert.IsType<ToggleButton>(cell.Content);

        Assert.True(toggleButton.IsChecked);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Respects_Content()
    {
        var column = new DataGridToggleButtonColumn
        {
            Header = "Favorite",
            Content = "★"
        };

        Assert.Equal("★", column.Content);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Respects_CheckedContent()
    {
        var column = new DataGridToggleButtonColumn
        {
            Header = "Favorite",
            CheckedContent = "★"
        };

        Assert.Equal("★", column.CheckedContent);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Respects_UncheckedContent()
    {
        var column = new DataGridToggleButtonColumn
        {
            Header = "Favorite",
            UncheckedContent = "☆"
        };

        Assert.Equal("☆", column.UncheckedContent);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Respects_IsThreeState()
    {
        var column = new DataGridToggleButtonColumn
        {
            Header = "State",
            IsThreeState = true
        };

        Assert.True(column.IsThreeState);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Default_IsThreeState_IsFalse()
    {
        var column = new DataGridToggleButtonColumn();

        Assert.False(column.IsThreeState);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Respects_ClickMode()
    {
        var column = new DataGridToggleButtonColumn
        {
            Header = "Favorite",
            ClickMode = ClickMode.Press
        };

        Assert.Equal(ClickMode.Press, column.ClickMode);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Default_ClickMode_IsRelease()
    {
        var column = new DataGridToggleButtonColumn();

        Assert.Equal(ClickMode.Release, column.ClickMode);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_Binding_Content_Uses_RowItem()
    {
        var vm = new ToggleButtonTestViewModel();
        var column = CreateToggleButtonColumn();
        column.Content = new Binding(nameof(ToggleButtonItem.Symbol));

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Favorite", 0);
        var toggleButton = Assert.IsType<ToggleButton>(cell.Content);

        Assert.Equal(vm.Items[0].Symbol, toggleButton.Content);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_CompiledBinding_Content_Uses_RowItem()
    {
        var vm = new ToggleButtonTestViewModel();
        var column = CreateToggleButtonColumn();
        column.Content = DataGridBindingDefinition.Create<ToggleButtonItem, string>(item => item.Symbol).CreateBinding();

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Favorite", 0);
        var toggleButton = Assert.IsType<ToggleButton>(cell.Content);

        Assert.Equal(vm.Items[0].Symbol, toggleButton.Content);
    }

    [AvaloniaFact]
    public void ToggleButtonColumn_CheckedUncheckedContent_Shows_Initial_Content_For_NonCurrent_Cell()
    {
        var vm = new ToggleButtonTestViewModel();
        var column = new DataGridToggleButtonColumn
        {
            Header = "Favorite",
            Binding = new Binding(nameof(ToggleButtonItem.IsFavorite)),
            CheckedContent = new Binding(nameof(ToggleButtonItem.CheckedLabel)),
            UncheckedContent = new Binding(nameof(ToggleButtonItem.UncheckedLabel))
        };

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Favorite", 1);
        var toggleButton = Assert.IsType<ToggleButton>(cell.Content);

        Assert.False(toggleButton.IsEnabled);
        Assert.Equal(vm.Items[1].UncheckedLabel, toggleButton.Content);
    }

    private static (Window window, DataGrid grid) CreateWindow(ToggleButtonTestViewModel vm, DataGridToggleButtonColumn? toggleButtonColumn = null)
    {
        var window = new Window
        {
            Width = 600,
            Height = 400,
            DataContext = vm
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = vm.Items,
            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new Binding("Name")
                },
                toggleButtonColumn ?? CreateToggleButtonColumn()
            }
        };

        window.Content = grid;
        return (window, grid);
    }

    private static DataGridToggleButtonColumn CreateToggleButtonColumn() =>
        new()
        {
            Header = "Favorite",
            Binding = new Binding("IsFavorite"),
            Content = "★"
        };

    private static DataGridCell GetCell(DataGrid grid, string header, int rowIndex)
    {
        return grid
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => c.OwningColumn?.Header?.ToString() == header && c.OwningRow?.Index == rowIndex);
    }

    private sealed class ToggleButtonTestViewModel
    {
        public ToggleButtonTestViewModel()
        {
            Items = new ObservableCollection<ToggleButtonItem>
            {
                new() { Name = "Item A", Symbol = "A", CheckedLabel = "On A", UncheckedLabel = "Off A", IsFavorite = true },
                new() { Name = "Item B", Symbol = "B", CheckedLabel = "On B", UncheckedLabel = "Off B", IsFavorite = false },
                new() { Name = "Item C", Symbol = "C", CheckedLabel = "On C", UncheckedLabel = "Off C", IsFavorite = true }
            };
        }

        public ObservableCollection<ToggleButtonItem> Items { get; }
    }

    private sealed class ToggleButtonItem
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string CheckedLabel { get; set; } = string.Empty;
        public string UncheckedLabel { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }

    private static void AssertHasAssignBinding(string propertyName)
    {
        var property = typeof(DataGridToggleButtonColumn).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<AssignBindingAttribute>());
    }
}
