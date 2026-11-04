// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using System.Linq;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridDatePickerColumnHeadlessTests
{
    [AvaloniaFact]
    public void DatePickerColumn_Binds_Value()
    {
        var vm = new DatePickerTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Date", 0);
        var textBlock = Assert.IsType<TextBlock>(cell.Content);

        Assert.NotNull(textBlock.Text);
    }

    [AvaloniaFact]
    public void DatePickerColumn_Respects_SelectedDateFormat()
    {
        var column = new DataGridDatePickerColumn
        {
            Header = "Date",
            SelectedDateFormat = CalendarDatePickerFormat.Long
        };

        Assert.Equal(CalendarDatePickerFormat.Long, column.SelectedDateFormat);
    }

    [AvaloniaFact]
    public void DatePickerColumn_Respects_IsTodayHighlighted()
    {
        var column = new DataGridDatePickerColumn
        {
            Header = "Date",
            IsTodayHighlighted = false
        };

        Assert.False(column.IsTodayHighlighted);
    }

    [AvaloniaFact]
    public void DatePickerColumn_Default_IsTodayHighlighted_IsTrue()
    {
        var column = new DataGridDatePickerColumn();

        Assert.True(column.IsTodayHighlighted);
    }

    [AvaloniaFact]
    public void DatePickerColumn_Respects_DisplayDateRange()
    {
        var startDate = new DateTime(2020, 1, 1);
        var endDate = new DateTime(2030, 12, 31);

        var column = new DataGridDatePickerColumn
        {
            Header = "Date",
            DisplayDateStart = startDate,
            DisplayDateEnd = endDate
        };

        Assert.Equal(startDate, column.DisplayDateStart);
        Assert.Equal(endDate, column.DisplayDateEnd);
    }

    [AvaloniaFact]
    public void DatePickerColumn_Applies_ContentAlignment()
    {
        var column = new TestDatePickerColumn
        {
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Bottom
        };

        var displayElement = column.CreateDisplayElement(new DataGridCell(), new object());
        var editingElement = column.CreateEditingElement(new DataGridCell(), new object());

        Assert.Equal(TextAlignment.Right, displayElement.TextAlignment);
        Assert.Equal(VerticalAlignment.Bottom, displayElement.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Right, editingElement.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Bottom, editingElement.VerticalContentAlignment);
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(editingElement));
    }

    [AvaloniaFact]
    public void DatePickerColumn_Virtual_Value_Uses_Configured_Format()
    {
        var column = new DataGridDatePickerColumn
        {
            Binding = new Binding(nameof(DateItem.Date)),
            SelectedDateFormat = CalendarDatePickerFormat.Custom,
            CustomDateFormatString = "yyyy-MM-dd",
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<DateItem, DateTime?>(item => item.Date));
        var item = new DateItem { Date = new DateTime(2024, 6, 15) };

        Assert.True(column.SupportsVirtualCellSurface);
        var provider = (IDataGridDrawnCellValueProvider)column;
        Assert.Equal("2024-06-15", provider.GetDrawnCellValue(item));

        column.SelectedDateFormat = CalendarDatePickerFormat.Short;
        Assert.Equal(
            item.Date.Value.ToString("d", CultureInfo.CurrentCulture),
            provider.GetDrawnCellValue(item));

        column.SelectedDateFormat = CalendarDatePickerFormat.Long;
        Assert.Equal(
            item.Date.Value.ToString("D", CultureInfo.CurrentCulture),
            provider.GetDrawnCellValue(item));

        item.Date = null;
        Assert.Null(provider.GetDrawnCellValue(item));
    }

    [AvaloniaFact]
    public void DatePickerColumn_Virtual_Surface_Requires_Direct_Typed_Date_Access()
    {
        var column = new DataGridDatePickerColumn
        {
            Binding = new Binding(nameof(DateItem.Date)),
        };

        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<DateItem, string>(_ => "not a date"));
        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<DateItem, DateTime?>(item => item.Date));
        Assert.True(column.SupportsVirtualCellSurface);

        column.Binding = new Binding(nameof(DateItem.Date))
        {
            StringFormat = "d",
        };
        Assert.False(column.SupportsVirtualCellSurface);

        var derived = new DerivedDatePickerColumn
        {
            Binding = new Binding(nameof(DateItem.Date)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            derived,
            new DataGridColumnValueAccessor<DateItem, DateTime?>(item => item.Date));
        Assert.False(derived.SupportsVirtualCellSurface);
    }

    private static (Window window, DataGrid grid) CreateWindow(DatePickerTestViewModel vm)
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
                new DataGridDatePickerColumn
                {
                    Header = "Date",
                    Binding = new Binding("Date")
                }
            }
        };

        window.Content = grid;
        return (window, grid);
    }

    private static DataGridCell GetCell(DataGrid grid, string header, int rowIndex)
    {
        return grid
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => c.OwningColumn?.Header?.ToString() == header && c.OwningRow?.Index == rowIndex);
    }

    private sealed class DatePickerTestViewModel
    {
        public DatePickerTestViewModel()
        {
            Items = new ObservableCollection<DateItem>
            {
                new() { Name = "Event 1", Date = new DateTime(2024, 1, 15) },
                new() { Name = "Event 2", Date = new DateTime(2024, 6, 20) },
                new() { Name = "Event 3", Date = new DateTime(2024, 12, 25) }
            };
        }

        public ObservableCollection<DateItem> Items { get; }
    }

    private sealed class DateItem
    {
        public string Name { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }

    private sealed class TestDatePickerColumn : DataGridDatePickerColumn
    {
        public TextBlock CreateDisplayElement(DataGridCell cell, object dataItem) =>
            (TextBlock)GenerateElement(cell, dataItem);

        public CalendarDatePicker CreateEditingElement(DataGridCell cell, object dataItem) =>
            (CalendarDatePicker)GenerateEditingElementDirect(cell, dataItem);
    }

    private sealed class DerivedDatePickerColumn : DataGridDatePickerColumn
    {
    }
}
