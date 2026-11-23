// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowHeaderNumberTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowRowNumbers_Toggle_Restores_RowHeader_Binding(bool useTypedBinding)
    {
        var items = new ObservableCollection<RowItem>
        {
            new("Alpha"),
            new("Beta")
        };

        var property = new ClrPropertyInfo<RowItem, string>(nameof(RowItem.Name), item => item.Name, setter: null);
        var path = new CompiledBindingPathBuilder()
            .Property(property, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor, acceptsNull: false)
            .Build();
        var binding = new CompiledBinding(path) { StringFormat = useTypedBinding ? null : "{0}" };
        var grid = CreateGrid(items, binding);
        var root = CreateWindow(grid);

        try
        {
            root.Show();
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            var firstRow = GetRow(grid, items[0]);
            var expression = BindingOperations.GetBindingExpressionBase(firstRow, DataGridRow.HeaderProperty);
            Assert.NotNull(expression);
            Assert.Equal(useTypedBinding, expression is not UntypedBindingExpressionBase);
            AssertHeader(items[0].Name, firstRow);

            grid.ShowRowNumbers = true;
            items[0].Name = "Updated while numbered";
            Dispatcher.UIThread.RunJobs();
            AssertHeader(firstRow.Index + 1, firstRow);

            grid.ShowRowNumbers = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Same(expression, BindingOperations.GetBindingExpressionBase(firstRow, DataGridRow.HeaderProperty));
            AssertHeader(items[0].Name, firstRow);

            items[0].Name = "Updated after numbering";
            Dispatcher.UIThread.RunJobs();
            AssertHeader(items[0].Name, firstRow);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void ShowRowNumbers_Toggle_Preserves_Local_Header_Set_While_Numbered()
    {
        var items = new ObservableCollection<RowItem> { new("Alpha") };
        var grid = CreateGrid(items, "Styled header");
        var root = CreateWindow(grid);

        try
        {
            root.Show();
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();

            var row = GetRow(grid, items[0]);
            AssertHeader("Styled header", row);

            grid.ShowRowNumbers = true;
            row.Header = "Local header";
            AssertHeader(1, row);

            grid.ShowRowNumbers = false;
            AssertHeader("Local header", row);

            row.ClearValue(DataGridRow.HeaderProperty);
            AssertHeader("Styled header", row);
        }
        finally
        {
            root.Close();
        }
    }

    private static DataGrid CreateGrid(ObservableCollection<RowItem> items, object header)
    {
        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.All
        };
        grid.Styles.Add(new Style(selector => selector.OfType<DataGridRow>())
        {
            Setters = { new Setter(DataGridRow.HeaderProperty, header) }
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = CompiledBinding.Create<RowItem, string>(item => item.Name)
        });
        return grid;
    }

    private static Window CreateWindow(DataGrid grid)
    {
        var root = new Window { Width = 320, Height = 240 };
        root.SetThemeStyles(DataGridTheme.SimpleV2);
        root.Content = grid;
        return root;
    }

    private static void AssertHeader(object expected, DataGridRow row)
    {
        Assert.Equal(expected, row.Header);
        Assert.NotNull(row.HeaderCell);
        Assert.Equal(expected, row.HeaderCell.Content);
    }

    private static DataGridRow GetRow(DataGrid grid, RowItem item)
    {
        var row = grid.GetRowFromItem(item);
        Assert.NotNull(row);
        return row;
    }

    private sealed class RowItem : INotifyPropertyChanged
    {
        private string _name;

        public RowItem(string name)
        {
            _name = name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }
}
