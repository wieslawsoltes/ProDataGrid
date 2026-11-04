// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using System.Linq;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridAutoCompleteColumnHeadlessTests
{
    [AvaloniaFact]
    public void AutoCompleteColumn_Binds_Value()
    {
        var vm = new AutoCompleteTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Category", 0);
        // In read-only mode, content is a TextBlock
        var textBlock = Assert.IsType<TextBlock>(cell.Content);
        Assert.Equal("Electronics", textBlock.Text);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_ItemsSource()
    {
        var suggestions = new List<string> { "Option1", "Option2", "Option3" };
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            ItemsSource = suggestions
        };

        Assert.Equal(suggestions, column.ItemsSource);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Applies_ItemTemplate()
    {
        var template = new FuncDataTemplate<string>((_, _) => new TextBlock());
        var column = new TestAutoCompleteColumn
        {
            ItemTemplate = template
        };

        var autoComplete = column.CreateEditingElement(new DataGridCell(), "Item");

        Assert.Same(template, autoComplete.ItemTemplate);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_FilterMode()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            FilterMode = AutoCompleteFilterMode.Contains
        };

        Assert.Equal(AutoCompleteFilterMode.Contains, column.FilterMode);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Default_FilterMode_IsContains()
    {
        var column = new DataGridAutoCompleteColumn();

        Assert.Equal(AutoCompleteFilterMode.Contains, column.FilterMode);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_MinimumPrefixLength()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            MinimumPrefixLength = 3
        };

        Assert.Equal(3, column.MinimumPrefixLength);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Default_MinimumPrefixLength_Is1()
    {
        var column = new DataGridAutoCompleteColumn();

        Assert.Equal(1, column.MinimumPrefixLength);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_MinimumPopulateDelay()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            MinimumPopulateDelay = TimeSpan.FromMilliseconds(500)
        };

        Assert.Equal(TimeSpan.FromMilliseconds(500), column.MinimumPopulateDelay);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_MaxDropDownHeight()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            MaxDropDownHeight = 300
        };

        Assert.Equal(300, column.MaxDropDownHeight);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_IsTextCompletionEnabled()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            IsTextCompletionEnabled = true
        };

        Assert.True(column.IsTextCompletionEnabled);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Respects_Watermark()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            Watermark = "Search..."
        };

        Assert.Equal("Search...", column.Watermark);
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Editor_Uses_One_Tab_Stop()
    {
        var column = new TestAutoCompleteColumn();

        var autoComplete = column.CreateEditingElement(new DataGridCell(), new object());

        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(autoComplete));
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Virtual_Value_Uses_Typed_Text_Accessor()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Binding = new Binding(nameof(AutoCompleteItem.Category))
            {
                StringFormat = "Category: {0}",
            },
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<AutoCompleteItem, string>(item => item.Category));
        var item = new AutoCompleteItem { Category = "Electronics" };

        Assert.True(column.SupportsVirtualCellSurface);
        Assert.Equal(
            "Category: Electronics",
            ((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));

        item.Category = null!;
        Assert.Equal(
            "Category: ",
            ((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));

        column.Binding = new Binding(nameof(AutoCompleteItem.Category));
        Assert.Null(((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(item));
    }

    [AvaloniaFact]
    public void AutoCompleteColumn_Virtual_Surface_Requires_Direct_Typed_Text_Access()
    {
        var column = new DataGridAutoCompleteColumn
        {
            Binding = new Binding(nameof(AutoCompleteItem.Category)),
        };

        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<AutoCompleteItem, string>(item => item.Category));
        Assert.True(column.SupportsVirtualCellSurface);

        column.Binding = new Binding(nameof(AutoCompleteItem.Category))
        {
            Source = new AutoCompleteItem(),
        };
        Assert.False(column.SupportsVirtualCellSurface);

        var derived = new DerivedAutoCompleteColumn
        {
            Binding = new Binding(nameof(AutoCompleteItem.Category)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            derived,
            new DataGridColumnValueAccessor<AutoCompleteItem, string>(item => item.Category));
        Assert.False(derived.SupportsVirtualCellSurface);
    }

    private static (Window window, DataGrid grid) CreateWindow(AutoCompleteTestViewModel vm)
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
                new DataGridAutoCompleteColumn
                {
                    Header = "Category",
                    Binding = new Binding("Category"),
                    ItemsSource = vm.CategorySuggestions
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

    private sealed class AutoCompleteTestViewModel
    {
        public AutoCompleteTestViewModel()
        {
            CategorySuggestions = new List<string>
            {
                "Electronics",
                "Computers",
                "Accessories",
                "Audio"
            };

            Items = new ObservableCollection<AutoCompleteItem>
            {
                new() { Name = "Laptop", Category = "Electronics" },
                new() { Name = "Mouse", Category = "Accessories" },
                new() { Name = "Headphones", Category = "Audio" }
            };
        }

        public List<string> CategorySuggestions { get; }
        public ObservableCollection<AutoCompleteItem> Items { get; }
    }

    private sealed class AutoCompleteItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    private sealed class TestAutoCompleteColumn : DataGridAutoCompleteColumn
    {
        public AutoCompleteBox CreateEditingElement(DataGridCell cell, object dataItem) =>
            (AutoCompleteBox)GenerateEditingElementDirect(cell, dataItem);
    }

    private sealed class DerivedAutoCompleteColumn : DataGridAutoCompleteColumn
    {
    }
}
