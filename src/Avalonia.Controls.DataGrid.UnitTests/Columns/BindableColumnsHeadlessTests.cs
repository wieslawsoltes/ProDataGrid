using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class BindableColumnsHeadlessTests
{
    [AvaloniaFact]
    public void Columns_Binding_Populates_Headers_When_Templated()
    {
        var vm = new ColumnsViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var headers = grid
            .GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .Where(h => h.OwningColumn != null && h.OwningColumn is not DataGridFillerColumn)
            .ToList();

        Assert.Equal(vm.Columns.Count, headers.Count);
        Assert.Equal(vm.Columns, headers.Select(h => h.OwningColumn));
        Assert.Equal(vm.Columns.Select(c => c.Header), headers.Select(h => h.Content));
    }

    [AvaloniaFact]
    public void Columns_TwoWay_DisplayIndex_Change_Reorders_Bound_Source()
    {
        var vm = new ColumnsViewModel();
        var (window, grid) = CreateWindow(vm, twoWay: true);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var first = vm.Columns[0];
        var second = vm.Columns[1];

        second.DisplayIndex = 0;
        grid.UpdateLayout();

        Assert.Same(second, vm.Columns[0]);
        Assert.Same(first, vm.Columns[1]);
    }

    [AvaloniaFact]
    public void BoundColumns_Refresh_After_Reattach()
    {
        var vm = new ColumnsViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(vm.Columns.Count, GetNonFillerColumns(grid).Length);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        vm.Columns.Add(new DataGridTextColumn { Header = "Extra", Binding = new Binding("Name") });

        window.Content = grid;
        Dispatcher.UIThread.RunJobs();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var headers = GetNonFillerColumns(grid).Select(c => c.Header).ToArray();
        Assert.Equal(vm.Columns.Count, headers.Length);
        Assert.Contains("Extra", headers);

        window.Close();
    }

    private static DataGridColumn[] GetNonFillerColumns(DataGrid grid)
    {
        return grid.ColumnsInternal.ItemsInternal
            .Where(column => column is not DataGridFillerColumn)
            .ToArray();
    }

    private static (Window window, DataGrid grid) CreateWindow(ColumnsViewModel vm, bool twoWay = false)
    {
        var root = new Window
        {
            Width = 400,
            Height = 200,
            DataContext = vm
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ColumnsSynchronizationMode = twoWay ? ColumnsSynchronizationMode.TwoWay : ColumnsSynchronizationMode.OneWayToGrid
        };

        grid.Bind(DataGrid.ColumnsProperty, new Binding("Columns")
        {
            Mode = twoWay ? BindingMode.TwoWay : BindingMode.OneWay
        });
        grid.Bind(DataGrid.ItemsSourceProperty, new Binding("Items"));

        root.Content = grid;
        return (root, grid);
    }

    private sealed class ColumnsViewModel
    {
        public ColumnsViewModel()
        {
            Items = new ObservableCollection<Person>
            {
                new() { Name = "First" },
                new() { Name = "Second" }
            };

            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") },
                new DataGridTextColumn { Header = "Length", Binding = new Binding("Name.Length") },
            };
        }

        public ObservableCollection<Person> Items { get; }

        public ObservableCollection<DataGridColumn> Columns { get; }
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
    }
}
