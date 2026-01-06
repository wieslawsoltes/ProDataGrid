// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Editing;

public class DataGridEditingInteractionModelTests
{
    [AvaloniaFact]
    public void IsTextInputFromGrid_Returns_True_For_Grid_Source()
    {
        var model = new DataGridEditingInteractionModel();
        var grid = new DataGrid();

        var result = model.IsTextInputFromGrid(new DataGridTextInputContext(grid, grid, restrictToCells: true));

        Assert.True(result);
    }

    [AvaloniaFact]
    public void ShouldBeginEditOnPointer_Respects_Triggers()
    {
        var model = new DataGridEditingInteractionModel();
        var grid = new DataGrid();

        var none = model.ShouldBeginEditOnPointer(new DataGridPointerEditContext(
            grid,
            isDoubleClick: false,
            editTriggers: DataGridEditTriggers.None,
            modifiers: KeyModifiers.None));

        var click = model.ShouldBeginEditOnPointer(new DataGridPointerEditContext(
            grid,
            isDoubleClick: false,
            editTriggers: DataGridEditTriggers.CellClick,
            modifiers: KeyModifiers.None));

        Assert.False(none);
        Assert.True(click);
    }

    [AvaloniaFact]
    public void GetTextInputForEdit_Returns_Text_When_Editable()
    {
        var model = new DataGridEditingInteractionModel();
        var grid = new DataGrid();

        var text = model.GetTextInputForEdit(new DataGridTextInputEditContext(
            grid,
            text: "A",
            isEditing: false,
            isReadOnly: false,
            canEditCurrentCell: true,
            editTriggers: DataGridEditTriggers.TextInput,
            modifiers: KeyModifiers.None));

        Assert.Equal("A", text);
    }

    [AvaloniaFact]
    public void TryApplyTextInput_Sets_TextBox_Text_And_Caret()
    {
        var model = new DataGridEditingInteractionModel();
        var textBox = new TextBox();

        var result = model.TryApplyTextInput(new DataGridTextInputApplyContext(textBox, "Hello"));

        Assert.True(result);
        Assert.Equal("Hello", textBox.Text);
        Assert.Equal(5, textBox.CaretIndex);
    }

    [AvaloniaFact]
    public void TryApplyTextInput_Uses_Nested_TextBox()
    {
        var model = new DataGridEditingInteractionModel();
        var nested = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(nested);

        var result = model.TryApplyTextInput(new DataGridTextInputApplyContext(panel, "Nested"));

        Assert.True(result);
        Assert.Equal("Nested", nested.Text);
    }
}
