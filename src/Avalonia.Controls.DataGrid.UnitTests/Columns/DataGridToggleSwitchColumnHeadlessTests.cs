// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridToggleSwitchColumnHeadlessTests
{
    [Fact]
    public void ToggleSwitchColumn_XamlBinding_Assigns_OnOffContent()
    {
        const string xaml = """
                            <DataGridToggleSwitchColumn xmlns="https://github.com/avaloniaui"
                                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                        OnContent="{Binding OnLabel}"
                                                        OffContent="{Binding OffLabel}" />
                            """;

        var column = AvaloniaRuntimeXamlLoader.Parse<DataGridToggleSwitchColumn>(xaml, typeof(DataGridToggleSwitchColumn).Assembly);

        Assert.IsAssignableFrom<BindingBase>(column.OnContent);
        Assert.IsAssignableFrom<BindingBase>(column.OffContent);
    }

    [Fact]
    public void ToggleSwitchColumn_Content_Properties_Use_AssignBinding()
    {
        AssertHasAssignBinding(nameof(DataGridToggleSwitchColumn.OnContent));
        AssertHasAssignBinding(nameof(DataGridToggleSwitchColumn.OffContent));
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Binds_Value()
    {
        var vm = new ToggleSwitchTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Active", 0);
        var toggleSwitch = Assert.IsType<ToggleSwitch>(cell.Content);

        Assert.True(toggleSwitch.IsChecked);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Respects_OnOffContent()
    {
        var column = new DataGridToggleSwitchColumn
        {
            Header = "Active",
            OnContent = "Yes",
            OffContent = "No"
        };

        Assert.Equal("Yes", column.OnContent);
        Assert.Equal("No", column.OffContent);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Respects_IsThreeState()
    {
        var column = new DataGridToggleSwitchColumn
        {
            Header = "State",
            IsThreeState = true
        };

        Assert.True(column.IsThreeState);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Default_IsThreeState_IsFalse()
    {
        var column = new DataGridToggleSwitchColumn();

        Assert.False(column.IsThreeState);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Binding_OnOffContent_Uses_RowItem()
    {
        var vm = new ToggleSwitchTestViewModel();
        var column = CreateToggleSwitchColumn();
        column.OnContent = new Binding(nameof(ToggleSwitchItem.OnLabel));
        column.OffContent = new Binding(nameof(ToggleSwitchItem.OffLabel));

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Active", 0);
        var toggleSwitch = Assert.IsType<ToggleSwitch>(cell.Content);

        Assert.Equal(vm.Items[0].OnLabel, toggleSwitch.OnContent);
        Assert.Equal(vm.Items[0].OffLabel, toggleSwitch.OffContent);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_CompiledBinding_OnOffContent_Uses_RowItem()
    {
        var vm = new ToggleSwitchTestViewModel();
        var column = CreateToggleSwitchColumn();
        column.OnContent = DataGridBindingDefinition.Create<ToggleSwitchItem, string>(item => item.OnLabel).CreateBinding();
        column.OffContent = DataGridBindingDefinition.Create<ToggleSwitchItem, string>(item => item.OffLabel).CreateBinding();

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Active", 0);
        var toggleSwitch = Assert.IsType<ToggleSwitch>(cell.Content);

        Assert.Equal(vm.Items[0].OnLabel, toggleSwitch.OnContent);
        Assert.Equal(vm.Items[0].OffLabel, toggleSwitch.OffContent);
    }

    [AvaloniaFact]
    public void ToggleSwitchColumn_Default_OnOffContent_Keeps_Control_Defaults()
    {
        var vm = new ToggleSwitchTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Active", 0);
        var toggleSwitch = Assert.IsType<ToggleSwitch>(cell.Content);

        Assert.NotNull(toggleSwitch.OnContent);
        Assert.NotNull(toggleSwitch.OffContent);
    }

    private static (Window window, DataGrid grid) CreateWindow(ToggleSwitchTestViewModel vm, DataGridToggleSwitchColumn? toggleSwitchColumn = null)
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
                toggleSwitchColumn ?? CreateToggleSwitchColumn()
            }
        };

        window.Content = grid;
        return (window, grid);
    }

    private static DataGridToggleSwitchColumn CreateToggleSwitchColumn() =>
        new()
        {
            Header = "Active",
            Binding = new Binding("IsActive")
        };

    private static DataGridCell GetCell(DataGrid grid, string header, int rowIndex)
    {
        return grid
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => c.OwningColumn?.Header?.ToString() == header && c.OwningRow?.Index == rowIndex);
    }

    private sealed class ToggleSwitchTestViewModel
    {
        public ToggleSwitchTestViewModel()
        {
            Items = new ObservableCollection<ToggleSwitchItem>
            {
                new() { Name = "Feature A", OnLabel = "Enabled A", OffLabel = "Disabled A", IsActive = true },
                new() { Name = "Feature B", OnLabel = "Enabled B", OffLabel = "Disabled B", IsActive = false },
                new() { Name = "Feature C", OnLabel = "Enabled C", OffLabel = "Disabled C", IsActive = true }
            };
        }

        public ObservableCollection<ToggleSwitchItem> Items { get; }
    }

    private sealed class ToggleSwitchItem
    {
        public string Name { get; set; } = string.Empty;
        public string OnLabel { get; set; } = string.Empty;
        public string OffLabel { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private static void AssertHasAssignBinding(string propertyName)
    {
        var property = typeof(DataGridToggleSwitchColumn).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<AssignBindingAttribute>());
    }
}
