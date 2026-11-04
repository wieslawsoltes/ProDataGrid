using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridComboBoxHyperlinkColumnHeadlessTests
{
    [AvaloniaFact]
    public void ComboBoxColumn_Binds_ItemsSource_And_SelectedItem()
    {
        var vm = new SampleViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Status", 0);
        var comboBox = Assert.IsType<ComboBox>(cell.Content);

        Assert.Same(vm.Statuses, comboBox.ItemsSource);
        Assert.Equal(vm.Items[0].Status, comboBox.SelectedItem);
    }

    [AvaloniaFact]
    public void HyperlinkColumn_Binds_Content_And_NavigateUri()
    {
        var vm = new SampleViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Link", 0);
        var hyperlink = Assert.IsType<HyperlinkButton>(cell.Content);

        Assert.Equal(vm.Items[0].Link, hyperlink.NavigateUri);
        Assert.Equal(vm.Items[0].Title, hyperlink.Content);
        Assert.Equal("_blank", hyperlink.Name);
    }

    [AvaloniaFact]
    public void ComboBoxColumn_Editable_TextBinding_Displays_Text()
    {
        var vm = new SampleViewModel();
        var column = new DataGridComboBoxColumn
        {
            Header = "Status",
            IsEditable = true,
            ItemsSource = vm.Statuses,
            TextBinding = new Binding(nameof(SampleItem.Status)),
        };
        var (window, grid) = CreateWindow(vm, column);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var comboBox = Assert.IsType<ComboBox>(GetCell(grid, "Status", 0).Content);
        Assert.True(comboBox.IsEditable);
        Assert.Equal(vm.Items[0].Status, comboBox.Text);
    }

    [AvaloniaFact]
    public void ComboBoxColumn_Virtual_Text_Value_Uses_Typed_Accessor()
    {
        var column = new DataGridComboBoxColumn
        {
            IsEditable = true,
            TextBinding = new Binding(nameof(SampleItem.Status))
            {
                StringFormat = "Status: {0}",
            },
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<SampleItem, string>(item => item.Status));

        Assert.True(column.SupportsVirtualCellSurface);
        Assert.Equal(
            "Status: Active",
            ((IDataGridDrawnCellValueProvider)column).GetDrawnCellValue(
                new SampleItem { Status = "Active" }));
    }

    [AvaloniaFact]
    public void ComboBoxColumn_Virtual_Surface_Requires_Direct_TextBinding_Mode()
    {
        var column = new DataGridComboBoxColumn
        {
            TextBinding = new Binding(nameof(SampleItem.Status)),
        };

        Assert.False(column.SupportsVirtualCellSurface);

        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<SampleItem, string>(item => item.Status));
        Assert.False(column.SupportsVirtualCellSurface);

        column.IsEditable = true;
        Assert.True(column.SupportsVirtualCellSurface);

        column.SelectedItemBinding = new Binding(nameof(SampleItem.Status));
        Assert.False(column.SupportsVirtualCellSurface);

        column.SelectedItemBinding = null;
        column.TextBinding = new Binding(nameof(SampleItem.Status))
        {
            Source = new SampleItem(),
        };
        Assert.False(column.SupportsVirtualCellSurface);

        var derived = new DerivedComboBoxColumn
        {
            IsEditable = true,
            TextBinding = new Binding(nameof(SampleItem.Status)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            derived,
            new DataGridColumnValueAccessor<SampleItem, string>(item => item.Status));
        Assert.False(derived.SupportsVirtualCellSurface);
    }

    [AvaloniaFact]
    public void ComboBoxColumn_Pointer_Edit_Opens_DropDown()
    {
        var column = new TestComboBoxColumn();
        var comboBox = new ComboBox();
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(
            RawInputModifiers.LeftMouseButton,
            PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(
            comboBox,
            pointer,
            comboBox,
            new Point(1, 1),
            0,
            properties,
            KeyModifiers.None);

        column.Prepare(comboBox, args);

        Assert.True(comboBox.IsDropDownOpen);
    }

    private static (Window window, DataGrid grid) CreateWindow(
        SampleViewModel vm,
        DataGridComboBoxColumn? comboBoxColumn = null)
    {
        var window = new Window
        {
            Width = 600,
            Height = 400,
            DataContext = vm
        };

        window.SetThemeStyles();

        // Provide base control themes so DataGrid-specific themes can base themselves on them.
        var fluent = new FluentTheme();
        if (fluent.TryGetResource(typeof(ComboBox), ThemeVariant.Default, out var comboTheme))
        {
            window.Resources.Add(typeof(ComboBox), comboTheme);
        }
        if (fluent.TryGetResource(typeof(HyperlinkButton), ThemeVariant.Default, out var hyperlinkTheme))
        {
            window.Resources.Add(typeof(HyperlinkButton), hyperlinkTheme);
        }
        if (fluent.TryGetResource(typeof(TextBox), ThemeVariant.Default, out var textBoxTheme))
        {
            window.Resources.Add(typeof(TextBox), textBoxTheme);
        }

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = vm.Items,
            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn
                {
                    Header = "Title",
                    Binding = new Binding("Title")
                },
                comboBoxColumn ?? new DataGridComboBoxColumn
                {
                    Header = "Status",
                    ItemsSource = vm.Statuses,
                    SelectedItemBinding = new Binding("Status")
                },
                new DataGridHyperlinkColumn
                {
                    Header = "Link",
                    Binding = new Binding("Link"),
                    ContentBinding = new Binding("Title"),
                    TargetName = "_blank"
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

    private sealed class SampleViewModel
    {
        public SampleViewModel()
        {
            Statuses = new List<string> { "Active", "Inactive", "Pending" };
            Items = new ObservableCollection<SampleItem>
            {
                new()
                {
                    Title = "Docs",
                    Status = Statuses[0],
                    Link = new Uri("https://example.com/docs")
                },
                new()
                {
                    Title = "Issues",
                    Status = Statuses[1],
                    Link = new Uri("https://example.com/issues")
                }
            };
        }

        public List<string> Statuses { get; }

        public ObservableCollection<SampleItem> Items { get; }
    }

    private sealed class SampleItem
    {
        public string Title { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public Uri? Link { get; set; }
    }

    private sealed class DerivedComboBoxColumn : DataGridComboBoxColumn
    {
    }

    private sealed class TestComboBoxColumn : DataGridComboBoxColumn
    {
        public object Prepare(Control editor, PointerPressedEventArgs args) =>
            PrepareCellForEdit(editor, args);
    }
}
