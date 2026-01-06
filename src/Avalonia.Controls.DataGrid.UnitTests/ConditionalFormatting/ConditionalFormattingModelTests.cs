// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class ConditionalFormattingModelTests
{
    [AvaloniaFact]
    public void Applies_Cell_Theme_When_Rule_Matches()
    {
        var positiveTheme = new ControlTheme(typeof(DataGridCell));
        var negativeTheme = new ControlTheme(typeof(DataGridCell));

        var (grid, root, column, items) = CreateGrid();
        try
        {
            var model = new ConditionalFormattingModel();
            model.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "positive",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: column,
                    value: 0d,
                    theme: positiveTheme),
                new ConditionalFormattingDescriptor(
                    ruleId: "negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: column,
                    value: 0d,
                    theme: negativeTheme)
            });

            grid.ConditionalFormattingModel = model;
            grid.UpdateLayout();

            var positiveCell = GetCellForItem(grid, items[0], column);
            var negativeCell = GetCellForItem(grid, items[1], column);

            Assert.Same(positiveTheme, positiveCell.Theme);
            Assert.Same(negativeTheme, negativeCell.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Updates_Themes_When_Item_Changes()
    {
        var positiveTheme = new ControlTheme(typeof(DataGridCell));
        var negativeTheme = new ControlTheme(typeof(DataGridCell));

        var (grid, root, column, items) = CreateGrid();
        try
        {
            var model = new ConditionalFormattingModel();
            model.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "positive",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: column,
                    value: 0d,
                    theme: positiveTheme),
                new ConditionalFormattingDescriptor(
                    ruleId: "negative",
                    @operator: ConditionalFormattingOperator.LessThan,
                    columnId: column,
                    value: 0d,
                    theme: negativeTheme)
            });

            grid.ConditionalFormattingModel = model;
            grid.UpdateLayout();

            items[0].Value = -12;
            grid.UpdateLayout();

            var updatedCell = GetCellForItem(grid, items[0], column);
            Assert.Same(negativeTheme, updatedCell.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Applies_Row_Theme_When_Rule_Matches()
    {
        var rowTheme = new ControlTheme(typeof(DataGridRow));

        var (grid, root, column, items) = CreateGrid();
        try
        {
            var model = new ConditionalFormattingModel();
            model.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "row-alert",
                    @operator: ConditionalFormattingOperator.Equals,
                    propertyPath: nameof(ConditionalItem.Status),
                    value: "Alert",
                    target: ConditionalFormattingTarget.Row,
                    valueSource: ConditionalFormattingValueSource.Item,
                    theme: rowTheme)
            });

            grid.ConditionalFormattingModel = model;
            grid.UpdateLayout();

            var row = GetRowForItem(grid, items[1]);
            Assert.Same(rowTheme, row.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Ignores_Mismatched_Comparison_Types()
    {
        var theme = new ControlTheme(typeof(DataGridCell));

        var (grid, root, column, items) = CreateGrid();
        try
        {
            var model = new ConditionalFormattingModel();
            model.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "mismatch",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: column,
                    value: "not-a-number",
                    theme: theme)
            });

            grid.ConditionalFormattingModel = model;
            grid.UpdateLayout();

            var cell = GetCellForItem(grid, items[0], column);
            Assert.NotSame(theme, cell.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public void Updates_When_Duplicate_Item_Remains_In_View()
    {
        var positiveTheme = new ControlTheme(typeof(DataGridCell));
        var item = new ConditionalItem { Value = 5, Status = "Ok" };
        var items = new ObservableCollection<ConditionalItem>
        {
            item,
            item
        };

        var (grid, root, column, collection) = CreateGrid(items);
        try
        {
            var model = new ConditionalFormattingModel();
            model.Apply(new[]
            {
                new ConditionalFormattingDescriptor(
                    ruleId: "positive",
                    @operator: ConditionalFormattingOperator.GreaterThan,
                    columnId: column,
                    value: 0d,
                    theme: positiveTheme)
            });

            grid.ConditionalFormattingModel = model;
            grid.UpdateLayout();

            collection.RemoveAt(0);
            grid.UpdateLayout();

            item.Value = -5;
            grid.UpdateLayout();

            var cell = GetCellForItem(grid, collection[0], column);
            Assert.NotSame(positiveTheme, cell.Theme);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid grid, Window root, DataGridNumericColumn column, ObservableCollection<ConditionalItem> items) CreateGrid(ObservableCollection<ConditionalItem>? items = null)
    {
        items ??= new ObservableCollection<ConditionalItem>
        {
            new() { Value = 12, Status = "Ok" },
            new() { Value = -3, Status = "Alert" }
        };

        var root = new Window
        {
            Width = 400,
            Height = 200
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items
        };

        var column = new DataGridNumericColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(ConditionalItem.Value))
        };

        grid.ColumnsInternal.Add(column);

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, root, column, items);
    }

    private static DataGridCell GetCellForItem(DataGrid grid, ConditionalItem item, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(cell => ReferenceEquals(cell.OwningRow?.DataContext, item)
                           && ReferenceEquals(cell.OwningColumn, column));
    }

    private static DataGridRow GetRowForItem(DataGrid grid, ConditionalItem item)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(row => ReferenceEquals(row.DataContext, item));
    }

    private sealed class ConditionalItem : INotifyPropertyChanged
    {
        private double _value;
        private string _status;

        public double Value
        {
            get => _value;
            set
            {
                if (_value.Equals(value))
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
