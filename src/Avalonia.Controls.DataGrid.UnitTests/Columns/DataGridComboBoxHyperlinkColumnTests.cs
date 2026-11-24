using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridComboBoxHyperlinkColumnTests
{
    [AvaloniaFact]
    public void ComboBox_SortMemberPath_Comes_From_SelectedItemBinding()
    {
        var column = new DataGridComboBoxColumn
        {
            SelectedItemBinding = new Binding("Status")
        };

        Assert.Equal("Status", column.SortMemberPath);
    }

    [AvaloniaFact]
    public void ComboBox_SortMemberPath_Is_Not_Overridden_When_Set()
    {
        var column = new DataGridComboBoxColumn
        {
            SortMemberPath = "Manual"
        };

        column.SelectedItemBinding = new Binding("Status");

        Assert.Equal("Manual", column.SortMemberPath);
    }

    [AvaloniaFact]
    public void ComboBox_Member_Bindings_Take_Precedence_Over_Member_Paths()
    {
        var displayMemberBinding = new Binding("Label");
        var selectedValueMemberBinding = new Binding("Id");
        var column = new DataGridComboBoxColumn
        {
            DisplayMemberPath = "Name",
            SelectedValuePath = "Key",
            DisplayMemberBinding = displayMemberBinding,
            SelectedValueMemberBinding = selectedValueMemberBinding
        };

        var combo = Assert.IsType<ComboBox>(column.GenerateElementInternal(new DataGridCell(), new object()));

        Assert.Same(displayMemberBinding, combo.DisplayMemberBinding);
        Assert.Same(selectedValueMemberBinding, combo.SelectedValueBinding);
    }

    [AvaloniaFact]
    public void ComboBox_Member_Binding_Change_Refreshes_Element()
    {
        var column = new DataGridComboBoxColumn { DisplayMemberPath = "Name" };
        var combo = Assert.IsType<ComboBox>(column.GenerateElementInternal(new DataGridCell(), new object()));
        var displayMemberBinding = new Binding("Label");

        column.DisplayMemberBinding = displayMemberBinding;
        column.RefreshCellContent(combo, nameof(DataGridComboBoxColumn.DisplayMemberBinding));

        Assert.Same(displayMemberBinding, combo.DisplayMemberBinding);
    }

    [AvaloniaFact]
    public void ComboBox_IsReadOnly_When_Binding_Is_OneWay()
    {
        var column = new DataGridComboBoxColumn
        {
            SelectedItemBinding = new Binding("Status") { Mode = BindingMode.OneWay }
        };

        Assert.True(column.IsReadOnly);
    }

    [AvaloniaFact]
    public void ComboBox_PrepareCellForEdit_Opens_Dropdown_On_AltDown()
    {
        var column = new TestComboBoxColumn();
        var combo = new ComboBox { SelectedItem = "Original" };
        var args = new KeyEventArgs
        {
            Key = Key.Down,
            KeyModifiers = KeyModifiers.Alt
        };

        column.InvokePrepareCellForEdit(combo, args);

        Assert.True(combo.IsDropDownOpen);
    }

    [AvaloniaFact]
    public void Hyperlink_ClipboardContentBinding_Prefers_ContentBinding()
    {
        var contentBinding = new Binding("Title");
        var column = new DataGridHyperlinkColumn
        {
            Binding = new Binding("Url"),
            ContentBinding = contentBinding
        };

        Assert.Same(column.ContentBinding, column.ClipboardContentBinding);
    }

    [AvaloniaFact]
    public void Hyperlink_TargetName_Refreshes_Element()
    {
        var column = new DataGridHyperlinkColumn
        {
            TargetName = "_blank"
        };

        var hyperlink = new HyperlinkButton();
        column.RefreshCellContent(hyperlink, nameof(DataGridHyperlinkColumn.TargetName));

        Assert.Equal("_blank", hyperlink.Name);

        column.TargetName = null;
        column.RefreshCellContent(hyperlink, nameof(DataGridHyperlinkColumn.TargetName));

        Assert.True(string.IsNullOrEmpty(hyperlink.Name));
    }

    private sealed class TestComboBoxColumn : DataGridComboBoxColumn
    {
        public object InvokePrepareCellForEdit(Control editingElement, RoutedEventArgs args) =>
            base.PrepareCellForEdit(editingElement, args);
    }
}
