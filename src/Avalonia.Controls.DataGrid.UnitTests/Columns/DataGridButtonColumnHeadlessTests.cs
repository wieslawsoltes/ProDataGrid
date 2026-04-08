// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Avalonia.Controls;
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

public class DataGridButtonColumnHeadlessTests
{
    [Fact]
    public void ButtonColumn_XamlBinding_Assigns_Content_And_CommandParameter()
    {
        const string xaml = """
                            <DataGridButtonColumn xmlns="https://github.com/avaloniaui"
                                                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                  Content="{Binding Name}"
                                                  CommandParameter="{Binding .}" />
                            """;

        var column = AvaloniaRuntimeXamlLoader.Parse<DataGridButtonColumn>(xaml, typeof(DataGridButtonColumn).Assembly);

        Assert.IsAssignableFrom<BindingBase>(column.Content);
        Assert.IsAssignableFrom<BindingBase>(column.CommandParameter);
    }

    [Fact]
    public void ButtonColumn_Content_Properties_Use_AssignBinding()
    {
        AssertHasAssignBinding(nameof(DataGridButtonColumn.Content));
        AssertHasAssignBinding(nameof(DataGridButtonColumn.CommandParameter));
    }

    [AvaloniaFact]
    public void ButtonColumn_Creates_Button()
    {
        var vm = new ButtonTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.Equal("Delete", button.Content);
    }

    [AvaloniaFact]
    public void ButtonColumn_Respects_Content()
    {
        var column = new DataGridButtonColumn
        {
            Header = "Action",
            Content = "Edit"
        };

        Assert.Equal("Edit", column.Content);
    }

    [AvaloniaFact]
    public void ButtonColumn_Respects_Command()
    {
        var command = new TypedButtonItemCommand();
        var column = new DataGridButtonColumn
        {
            Header = "Action",
            Content = "Delete",
            Command = command
        };

        Assert.Same(command, column.Command);
    }

    [AvaloniaFact]
    public void ButtonColumn_Respects_ClickMode()
    {
        var column = new DataGridButtonColumn
        {
            Header = "Action",
            ClickMode = ClickMode.Press
        };

        Assert.Equal(ClickMode.Press, column.ClickMode);
    }

    [AvaloniaFact]
    public void ButtonColumn_Default_ClickMode_IsRelease()
    {
        var column = new DataGridButtonColumn();

        Assert.Equal(ClickMode.Release, column.ClickMode);
    }

    [AvaloniaFact]
    public void ButtonColumn_IsAlwaysReadOnly()
    {
        var column = new DataGridButtonColumn();

        Assert.True(column.IsReadOnly);

        // Even if we try to set it to false, it should remain true
        column.IsReadOnly = false;
        Assert.True(column.IsReadOnly);
    }

    [AvaloniaFact]
    public void ButtonColumn_Respects_HotKey()
    {
        var hotKey = KeyGesture.Parse("Delete");
        var column = new DataGridButtonColumn
        {
            Header = "Action",
            HotKey = hotKey
        };

        Assert.Equal(hotKey, column.HotKey);
    }

    [AvaloniaFact]
    public void ButtonColumn_Binding_CommandParameter_Uses_RowItem()
    {
        var vm = new ButtonTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.True(button.IsEnabled);
        Assert.Same(vm.Items[0], button.CommandParameter);
    }

    [AvaloniaFact]
    public void ButtonColumn_CompiledBinding_CommandParameter_Uses_RowItem()
    {
        var vm = new ButtonTestViewModel();
        var column = CreateButtonColumn(vm);
        column.CommandParameter = DataGridBindingDefinition.Create<ButtonItem, ButtonItem>(item => item).CreateBinding();

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.True(button.IsEnabled);
        Assert.Same(vm.Items[0], button.CommandParameter);
    }

    [AvaloniaFact]
    public void ButtonColumn_Binding_Content_Uses_RowItem()
    {
        var vm = new ButtonTestViewModel();
        var column = CreateButtonColumn(vm);
        column.Content = new Binding(nameof(ButtonItem.Name));

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.Equal(vm.Items[0].Name, button.Content);
    }

    [AvaloniaFact]
    public void ButtonColumn_CompiledBinding_Content_Uses_RowItem()
    {
        var vm = new ButtonTestViewModel();
        var column = CreateButtonColumn(vm);
        column.Content = DataGridBindingDefinition.Create<ButtonItem, string>(item => item.Name).CreateBinding();

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.Equal(vm.Items[0].Name, button.Content);
    }

    [AvaloniaFact]
    public void ButtonColumn_CommandExecuted_OnClick()
    {
        var vm = new ButtonTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.True(button.IsEnabled);
        button.Command?.Execute(button.CommandParameter);

        Assert.True(vm.DeleteCommand.WasExecuted);
        Assert.Same(vm.Items[0], vm.DeleteCommand.LastParameter);
    }

    [AvaloniaFact]
    public void ButtonColumn_DefaultCommandParameter_Falls_Back_To_RowItem()
    {
        var vm = new ButtonTestViewModel();
        var column = CreateButtonColumn(vm);
        column.CommandParameter = null;

        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Action", 0);
        var button = Assert.IsType<Button>(cell.Content);

        Assert.True(button.IsEnabled);
        Assert.Same(vm.Items[0], button.CommandParameter);
    }

    private static (Window window, DataGrid grid) CreateWindow(ButtonTestViewModel vm, DataGridButtonColumn? buttonColumn = null)
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
                buttonColumn ?? CreateButtonColumn(vm)
            }
        };

        window.Content = grid;
        return (window, grid);
    }

    private static DataGridButtonColumn CreateButtonColumn(ButtonTestViewModel vm) =>
        new()
        {
            Header = "Action",
            Content = "Delete",
            Command = vm.DeleteCommand,
            CommandParameter = new Binding(".")
        };

    private static DataGridCell GetCell(DataGrid grid, string header, int rowIndex)
    {
        return grid
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => c.OwningColumn?.Header?.ToString() == header && c.OwningRow?.Index == rowIndex);
    }

    private sealed class ButtonTestViewModel
    {
        public ButtonTestViewModel()
        {
            Items = new ObservableCollection<ButtonItem>
            {
                new() { Name = "Item A" },
                new() { Name = "Item B" },
                new() { Name = "Item C" }
            };

            DeleteCommand = new TypedButtonItemCommand();
        }

        public ObservableCollection<ButtonItem> Items { get; }
        public TypedButtonItemCommand DeleteCommand { get; }
    }

    private sealed class ButtonItem
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TypedButtonItemCommand : ICommand
    {
        public bool WasExecuted { get; private set; }
        public object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => parameter is ButtonItem;

        public void Execute(object? parameter)
        {
            WasExecuted = true;
            LastParameter = parameter;
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AssertHasAssignBinding(string propertyName)
    {
        var property = typeof(DataGridButtonColumn).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<AssignBindingAttribute>());
    }
}
