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

public class DataGridMaskedTextColumnHeadlessTests
{
    [AvaloniaFact]
    public void MaskedTextColumn_Binds_Value()
    {
        var vm = new MaskedTextTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Phone", 0);
        // In read-only mode, content is a TextBlock
        var textBlock = Assert.IsType<TextBlock>(cell.Content);
        Assert.Equal("(555) 123-4567", textBlock.Text);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_Mask()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Phone",
            Mask = "(000) 000-0000"
        };

        Assert.Equal("(000) 000-0000", column.Mask);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_PromptChar()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Phone",
            PromptChar = '#'
        };

        Assert.Equal('#', column.PromptChar);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Default_PromptChar_IsUnderscore()
    {
        var column = new DataGridMaskedTextColumn();

        Assert.Equal('_', column.PromptChar);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_AsciiOnly()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Code",
            AsciiOnly = true
        };

        Assert.True(column.AsciiOnly);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Default_AsciiOnly_IsFalse()
    {
        var column = new DataGridMaskedTextColumn();

        Assert.False(column.AsciiOnly);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_HidePromptOnLeave()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Code",
            HidePromptOnLeave = true
        };

        Assert.True(column.HidePromptOnLeave);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Default_HidePromptOnLeave_IsTrue()
    {
        var column = new DataGridMaskedTextColumn();

        Assert.True(column.HidePromptOnLeave);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_ResetOnPrompt()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Code",
            ResetOnPrompt = false
        };

        Assert.False(column.ResetOnPrompt);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Default_ResetOnPrompt_IsTrue()
    {
        var column = new DataGridMaskedTextColumn();

        Assert.True(column.ResetOnPrompt);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_ResetOnSpace()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Code",
            ResetOnSpace = false
        };

        Assert.False(column.ResetOnSpace);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Default_ResetOnSpace_IsTrue()
    {
        var column = new DataGridMaskedTextColumn();

        Assert.True(column.ResetOnSpace);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_Culture()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var column = new DataGridMaskedTextColumn
        {
            Header = "Number",
            Culture = culture
        };

        Assert.Equal(culture, column.Culture);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Respects_Watermark()
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = "Phone",
            Watermark = "Enter phone..."
        };

        Assert.Equal("Enter phone...", column.Watermark);
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Virtual_Value_Uses_Typed_Text_Accessor()
    {
        var column = new DataGridMaskedTextColumn
        {
            Binding = new Binding(nameof(MaskedTextItem.Phone))
            {
                StringFormat = "Phone: {0}",
            },
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<MaskedTextItem, string>(item => item.Phone));
        var item = new MaskedTextItem { Phone = "(555) 123-4567" };

        Assert.True(column.SupportsVirtualCellSurface);
        Assert.Equal(
            "Phone: (555) 123-4567",
            ((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));

        item.Phone = null!;
        Assert.Equal(
            "Phone: ",
            ((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));

        column.Binding = new Binding(nameof(MaskedTextItem.Phone));
        Assert.Null(((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));
    }

    [AvaloniaFact]
    public void MaskedTextColumn_Virtual_Surface_Requires_Direct_Typed_Text_Access()
    {
        var column = new DataGridMaskedTextColumn
        {
            Binding = new Binding(nameof(MaskedTextItem.Phone)),
        };

        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<MaskedTextItem, string>(item => item.Phone));
        Assert.True(column.SupportsVirtualCellSurface);

        column.Binding = new Binding(nameof(MaskedTextItem.Phone))
        {
            Source = new MaskedTextItem(),
        };
        Assert.False(column.SupportsVirtualCellSurface);

        var derived = new DerivedMaskedTextColumn
        {
            Binding = new Binding(nameof(MaskedTextItem.Phone)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            derived,
            new DataGridColumnValueAccessor<MaskedTextItem, string>(item => item.Phone));
        Assert.False(derived.SupportsVirtualCellSurface);
    }

    private static (Window window, DataGrid grid) CreateWindow(MaskedTextTestViewModel vm)
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
                new DataGridMaskedTextColumn
                {
                    Header = "Phone",
                    Binding = new Binding("Phone"),
                    Mask = "(000) 000-0000"
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

    private sealed class MaskedTextTestViewModel
    {
        public MaskedTextTestViewModel()
        {
            Items = new ObservableCollection<MaskedTextItem>
            {
                new() { Name = "John", Phone = "(555) 123-4567" },
                new() { Name = "Jane", Phone = "(555) 234-5678" },
                new() { Name = "Bob", Phone = "(555) 345-6789" }
            };
        }

        public ObservableCollection<MaskedTextItem> Items { get; }
    }

    private sealed class MaskedTextItem
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    private sealed class DerivedMaskedTextColumn : DataGridMaskedTextColumn
    {
    }
}
