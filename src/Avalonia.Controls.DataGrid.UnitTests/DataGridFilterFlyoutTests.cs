// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridFilterFlyoutTests
{
    [AvaloniaFact]
    public void Column_TryShowFilterFlyout_Returns_False_When_No_Flyout()
    {
        var (grid, root, column) = CreateGrid(null);

        try
        {
            var result = column.TryShowFilterFlyout();

            Assert.False(result);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Column_TryShowFilterFlyout_Shows_Flyout_When_Configured()
    {
        var flyout = new Flyout { Content = new TextBlock { Text = "Filter" } };
        var (grid, root, column) = CreateGrid(flyout);

        try
        {
            var result = column.TryShowFilterFlyout();
            Dispatcher.UIThread.RunJobs();

            Assert.True(result);
            Assert.True(flyout.IsOpen);

            flyout.Hide();
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_TryShowFilterFlyout_By_ColumnId_Shows_Flyout()
    {
        var flyout = new Flyout { Content = new TextBlock { Text = "Filter" } };
        var (grid, root, column) = CreateGrid(flyout);

        try
        {
            column.ColumnKey = "Name";

            var result = grid.TryShowFilterFlyout("Name");
            Dispatcher.UIThread.RunJobs();

            Assert.True(result);
            Assert.True(flyout.IsOpen);

            flyout.Hide();
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Grid_ClearFilterByColumnId_Removes_Descriptor()
    {
        var (grid, root, column) = CreateGrid(null);

        try
        {
            column.ColumnKey = "Name";
            grid.FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: "Name",
                @operator: FilteringOperator.Contains,
                propertyPath: nameof(Item.Name),
                value: "A",
                stringComparison: System.StringComparison.OrdinalIgnoreCase));

            var result = grid.ClearFilterByColumnId("Name");

            Assert.True(result);
            Assert.Empty(grid.FilteringModel.Descriptors);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void FilteringModel_RequestShowFilterFlyout_Opens_Flyout()
    {
        var flyout = new Flyout { Content = new TextBlock { Text = "Filter" } };
        var (grid, root, column) = CreateGrid(flyout);

        try
        {
            column.ColumnKey = "Name";
            var interaction = grid.FilteringModel as Avalonia.Controls.DataGridFiltering.IFilteringModelInteraction;

            Assert.NotNull(interaction);

            interaction.RequestShowFilterFlyout("Name");
            Dispatcher.UIThread.RunJobs();

            Assert.True(flyout.IsOpen);

            flyout.Hide();
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Header_TryShowFilterFlyout_Closes_ContextMenu_And_Opens_On_Next_Tick()
    {
        var flyout = new Flyout { Content = new TextBlock { Text = "Filter" } };
        var (grid, root, column) = CreateGrid(flyout);

        try
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Show filter" });
            grid.ColumnHeaderContextMenu = menu;
            Dispatcher.UIThread.RunJobs();

            var header = GetHeaderForColumn(grid, column);
            menu.Open(header);
            Dispatcher.UIThread.RunJobs();

            var result = header.TryShowFilterFlyout();

            Assert.True(result);
            Assert.False(flyout.IsOpen);

            Dispatcher.UIThread.RunJobs();

            Assert.False(menu.IsOpen);
            Assert.True(flyout.IsOpen);

            flyout.Hide();
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Header_Shows_Filter_Status_Icon_When_Filtered_And_Button_Hidden()
    {
        var (grid, root, column) = CreateGrid(null);

        try
        {
            grid.FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: column,
                @operator: FilteringOperator.Contains,
                propertyPath: nameof(Item.Name),
                value: "A",
                stringComparison: System.StringComparison.OrdinalIgnoreCase));

            Dispatcher.UIThread.RunJobs();

            var header = GetHeaderForColumn(grid, column);

            Assert.True(header.ShowFilterStatusIcon);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridTextColumn column) CreateGrid(Flyout? flyout)
    {
        var items = new ObservableCollection<Item>
        {
            new("Ada"),
            new("Grace")
        };

        var root = new Window
        {
            Width = 400,
            Height = 200,
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items
        };

        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(Item.Name)),
            FilterFlyout = flyout
        };

        grid.ColumnsInternal.Add(column);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, column);
    }

    private static DataGridColumnHeader GetHeaderForColumn(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(h => ReferenceEquals(h.OwningColumn, column));
    }

    private sealed class Item
    {
        public Item(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
