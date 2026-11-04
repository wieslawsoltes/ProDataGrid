// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using System.Linq;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridTimePickerColumnHeadlessTests
{
    [AvaloniaFact]
    public void TimePickerColumn_Binds_Value()
    {
        var vm = new TimePickerTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Time", 0);
        var textBlock = Assert.IsType<TextBlock>(cell.Content);

        Assert.Equal(new TimeSpan(9, 0, 0).ToString(), textBlock.Text);
    }

    [AvaloniaFact]
    public void TimePickerColumn_Respects_ClockIdentifier()
    {
        var column = new DataGridTimePickerColumn
        {
            Header = "Time",
            ClockIdentifier = "24HourClock"
        };

        Assert.Equal("24HourClock", column.ClockIdentifier);
    }

    [AvaloniaFact]
    public void TimePickerColumn_Respects_MinuteIncrement()
    {
        var column = new DataGridTimePickerColumn
        {
            Header = "Time",
            MinuteIncrement = 15
        };

        Assert.Equal(15, column.MinuteIncrement);
    }

    [AvaloniaFact]
    public void TimePickerColumn_Respects_UseSeconds()
    {
        var column = new DataGridTimePickerColumn
        {
            Header = "Time",
            UseSeconds = true
        };

        Assert.True(column.UseSeconds);
    }

    [AvaloniaFact]
    public void TimePickerColumn_Default_ClockIdentifier_Is12Hour()
    {
        var column = new DataGridTimePickerColumn();

        Assert.Equal("12HourClock", column.ClockIdentifier);
    }

    [AvaloniaFact]
    public void TimePickerColumn_Virtual_Value_Uses_Configured_Format()
    {
        var column = new DataGridTimePickerColumn
        {
            Binding = new Binding(nameof(TimeItem.Time)),
            FormatString = @"hh\:mm\:ss",
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<TimeItem, TimeSpan?>(item => item.Time));
        var item = new TimeItem { Time = new TimeSpan(14, 5, 9) };

        Assert.True(column.SupportsVirtualCellSurface);
        var provider = (IDataGridDrawnCellValueProvider)column;
        Assert.Equal("14:05:09", provider.GetDrawnCellValue(item));

        column.FormatString = null;
        column.ClockIdentifier = "24HourClock";
        column.UseSeconds = false;
        Assert.Equal("14:05", provider.GetDrawnCellValue(item));

        column.UseSeconds = true;
        Assert.Equal("14:05:09", provider.GetDrawnCellValue(item));

        column.ClockIdentifier = "12HourClock";
        column.UseSeconds = false;
        Assert.Equal(
            new DateTime(1, 1, 1, 14, 5, 9).ToString("h:mm tt", CultureInfo.CurrentCulture),
            provider.GetDrawnCellValue(item));

        column.UseSeconds = true;
        Assert.Equal(
            new DateTime(1, 1, 1, 14, 5, 9).ToString("h:mm:ss tt", CultureInfo.CurrentCulture),
            provider.GetDrawnCellValue(item));

        item.Time = null;
        Assert.Null(provider.GetDrawnCellValue(item));
    }

    [AvaloniaFact]
    public void TimePickerColumn_Virtual_Surface_Requires_Direct_Typed_Time_Access()
    {
        var column = new DataGridTimePickerColumn
        {
            Binding = new Binding(nameof(TimeItem.Time)),
        };

        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<TimeItem, string>(_ => "not a time"));
        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<TimeItem, TimeSpan?>(item => item.Time));
        Assert.True(column.SupportsVirtualCellSurface);

        column.Binding = new Binding(nameof(TimeItem.Time))
        {
            StringFormat = "{0:c}",
        };
        Assert.False(column.SupportsVirtualCellSurface);

        var derived = new DerivedTimePickerColumn
        {
            Binding = new Binding(nameof(TimeItem.Time)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            derived,
            new DataGridColumnValueAccessor<TimeItem, TimeSpan?>(item => item.Time));
        Assert.False(derived.SupportsVirtualCellSurface);
    }

    private static (Window window, DataGrid grid) CreateWindow(TimePickerTestViewModel vm)
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
                new DataGridTimePickerColumn
                {
                    Header = "Time",
                    Binding = new Binding("Time")
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

    private sealed class TimePickerTestViewModel
    {
        public TimePickerTestViewModel()
        {
            Items = new ObservableCollection<TimeItem>
            {
                new() { Name = "Morning", Time = new TimeSpan(9, 0, 0) },
                new() { Name = "Afternoon", Time = new TimeSpan(14, 30, 0) },
                new() { Name = "Evening", Time = new TimeSpan(18, 45, 0) }
            };
        }

        public ObservableCollection<TimeItem> Items { get; }
    }

    private sealed class TimeItem
    {
        public string Name { get; set; } = string.Empty;
        public TimeSpan? Time { get; set; }
    }

    private sealed class DerivedTimePickerColumn : DataGridTimePickerColumn
    {
    }
}
