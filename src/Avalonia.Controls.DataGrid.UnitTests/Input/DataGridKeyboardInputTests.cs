using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridKeyboardInputTests
{
    [AvaloniaFact]
    public void ArrowKeys_Move_Between_Rows()
    {
        var (grid, items) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.Down);

        Assert.Equal(grid.SlotFromRowIndex(1), grid.CurrentSlot);
        Assert.Equal(items[1], grid.SelectedItem);

        PressKey(grid, Key.Up);

        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Ctrl_ArrowKeys_Jump_To_First_And_Last_Rows()
    {
        var (grid, items) = CreateGrid(rowCount: 6);
        var ctrl = GetCtrlOrCmdModifier(grid);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);

        PressKey(grid, Key.Down, ctrl);

        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.Equal(items.Last(), grid.SelectedItem);

        PressKey(grid, Key.Up, ctrl);

        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Shift_Down_Extends_FullRow_Selection()
    {
        var (grid, items) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.Down, KeyModifiers.Shift);

        var selected = grid.SelectedItems.Cast<RowItem>().ToList();
        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.Contains(items[0], selected);
        Assert.Contains(items[1], selected);
    }

    [AvaloniaFact]
    public void Home_End_Move_Between_Columns()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 2);

        PressKey(grid, Key.Home);

        Assert.Equal(0, grid.CurrentColumnIndex);
        Assert.Equal(grid.SlotFromRowIndex(2), grid.CurrentSlot);

        PressKey(grid, Key.End);

        Assert.Equal(2, grid.CurrentColumnIndex);
        Assert.Equal(grid.SlotFromRowIndex(2), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void Ctrl_Home_End_Jump_To_First_And_Last_Cells()
    {
        var (grid, items) = CreateGrid(rowCount: 7);
        var ctrl = GetCtrlOrCmdModifier(grid);
        SetCurrentCell(grid, rowIndex: 3, columnIndex: 1);

        PressKey(grid, Key.Home, ctrl);

        Assert.Equal(0, grid.CurrentColumnIndex);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);

        PressKey(grid, Key.End, ctrl);

        Assert.Equal(2, grid.CurrentColumnIndex);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void Enter_Behaves_Like_Down_Arrow()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.Enter);

        Assert.Equal(grid.SlotFromRowIndex(1), grid.CurrentSlot);
        Assert.Equal(items[1], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Left_Right_Move_Between_Columns()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 1);

        PressKey(grid, Key.Left);

        Assert.Equal(0, grid.CurrentColumnIndex);

        PressKey(grid, Key.Right);

        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void Ctrl_A_Selects_All_Rows_For_FullRow_Selection()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow);
        var ctrl = GetCtrlOrCmdModifier(grid);

        PressKey(grid, Key.A, ctrl);

        Assert.Equal(items.Count, grid.SelectedItems.Count);
        Assert.True(items.All(item => grid.SelectedItems.Contains(item)));
    }

    [AvaloniaFact]
    public void Ctrl_A_Selects_All_Cells_For_Cell_Selection()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell);
        var ctrl = GetCtrlOrCmdModifier(grid);

        PressKey(grid, Key.A, ctrl);

        Assert.Equal(items.Count * 3, grid.SelectedCells.Count);
    }

    [AvaloniaFact]
    public void Ctrl_A_Is_Ignored_With_Modifier_Mismatch()
    {
        var (grid, _) = CreateGrid(selectionMode: DataGridSelectionMode.Extended);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.A, ctrl | KeyModifiers.Shift);

        Assert.False(args.Handled);
        Assert.Empty(grid.SelectedItems);

        args = PressKey(grid, Key.A, ctrl | KeyModifiers.Alt);

        Assert.False(args.Handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void Ctrl_A_Is_Ignored_When_SelectionMode_Not_Extended()
    {
        var (grid, _) = CreateGrid(selectionMode: DataGridSelectionMode.Single);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.A, ctrl);

        Assert.False(args.Handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void A_Key_Without_Ctrl_Is_Ignored()
    {
        var (grid, _) = CreateGrid();

        var args = PressKey(grid, Key.A);

        Assert.False(args.Handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void Ctrl_C_Copies_When_Selection_Present()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.C, ctrl);

        Assert.True(args.Handled);
    }

    [AvaloniaFact]
    public void Ctrl_Insert_Copies_When_Selection_Present()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.Insert, ctrl);

        Assert.True(args.Handled);
    }

    [AvaloniaFact]
    public void DownKey_Selects_First_Cell_When_No_Current_Cell()
    {
        var (grid, items) = CreateGrid();
        ResetCurrentCell(grid);

        var args = PressKey(grid, Key.Down);

        Assert.True(args.Handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void DownKey_Returns_False_When_No_Rows()
    {
        var (grid, _) = CreateGrid();
        ForceSlotCount(grid, 0);

        var handled = InvokeProcessDownKeyInternal(grid, shift: false, ctrl: false);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void DownKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeProcessDownKeyInternal(grid, shift: false, ctrl: false);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void DownKey_At_Last_Row_Does_Not_Move()
    {
        var (grid, items) = CreateGrid(rowCount: 2);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        var args = PressKey(grid, Key.Down);

        Assert.True(args.Handled);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.Equal(items.Last(), grid.SelectedItem);
    }

    [AvaloniaFact]
    public void CtrlShiftDown_Uses_Extended_Selection_Mode()
    {
        var (grid, items) = CreateGrid(selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.Down, ctrl | KeyModifiers.Shift);

        Assert.True(args.Handled);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.True(grid.SelectedItems.Count > 1);
    }

    [AvaloniaFact]
    public void CtrlShiftDown_Does_Not_Extend_When_Single_Select()
    {
        var (grid, items) = CreateGrid(selectionMode: DataGridSelectionMode.Single);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.Down, ctrl | KeyModifiers.Shift);

        Assert.True(args.Handled);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.Equal(1, grid.SelectedItems.Count);
    }

    [AvaloniaFact]
    public void DownKey_Defers_When_Editing_Element_Has_Focus()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);
        var editingRow = grid.EditingRow;
        Assert.NotNull(editingRow);
        Assert.NotEqual(-1, grid.EditingColumnIndex);

        var editingColumn = grid.ColumnsItemsInternal[grid.EditingColumnIndex];
        var columnContent = editingColumn.GetCellContent(editingRow!);
        Assert.NotNull(columnContent);

        SetFocusedObject(grid, columnContent!);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);
        lostFocusQueue.Clear();

        var lostFocusRaised = false;
        columnContent!.LostFocus += (_, _) => lostFocusRaised = true;

        var handled = InvokeProcessDownKeyInternal(grid, shift: false, ctrl: false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void Escape_Cancels_Cell_Edit()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.True(grid.BeginEdit());
        Assert.NotEqual(-1, grid.EditingColumnIndex);

        var args = PressKey(grid, Key.Escape);

        Assert.True(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void Escape_Cancels_Row_Edit_When_Cell_Edit_Ended()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.True(grid.BeginEdit());
        Assert.NotNull(grid.EditingRow);
        Assert.NotEqual(-1, grid.EditingColumnIndex);

        Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true));
        Assert.NotNull(grid.EditingRow);
        Assert.Equal(-1, grid.EditingColumnIndex);

        var args = PressKey(grid, Key.Escape);

        Assert.True(args.Handled);
        Assert.Null(grid.EditingRow);
    }

    [AvaloniaFact]
    public void Escape_Returns_False_When_Not_Editing()
    {
        var (grid, _) = CreateGrid();

        var args = PressKey(grid, Key.Escape);

        Assert.False(args.Handled);
    }

    [AvaloniaFact]
    public void Escape_Defers_When_Editing_Element_Has_Focus()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);

        SetFocusedObject(grid, editingElement);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);
        lostFocusQueue.Clear();

        var lostFocusRaised = false;
        editingElement.LostFocus += (_, _) => lostFocusRaised = true;

        var args = PressKey(grid, Key.Escape);
        Dispatcher.UIThread.RunJobs();

        Assert.True(args.Handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void Ctrl_LeftMost_Selects_First_Cell_When_No_Current_Cell()
    {
        var (grid, _) = CreateGrid();
        ResetCurrentCell(grid);

        var firstVisibleSlot = grid.SlotFromRowIndex(0);
        var firstVisibleColumnIndex = grid.ColumnsInternal.FirstVisibleNonFillerColumn!.Index;

        var handled = InvokeProcessLeftMost(grid, firstVisibleColumnIndex, firstVisibleSlot);

        Assert.True(handled);
        Assert.Equal(firstVisibleSlot, grid.CurrentSlot);
        Assert.Equal(firstVisibleColumnIndex, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void Ctrl_RightMost_Selects_First_Cell_When_No_Current_Cell()
    {
        var (grid, _) = CreateGrid();
        ResetCurrentCell(grid);

        var firstVisibleSlot = grid.SlotFromRowIndex(0);
        var lastVisibleColumnIndex = grid.ColumnsInternal.LastVisibleColumn!.Index;

        var handled = InvokeProcessRightMost(grid, lastVisibleColumnIndex, firstVisibleSlot);

        Assert.True(handled);
        Assert.Equal(firstVisibleSlot, grid.CurrentSlot);
        Assert.Equal(lastVisibleColumnIndex, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void Home_End_Select_First_Row_When_No_Current_Cell()
    {
        var (homeGrid, _) = CreateGrid();

        var homeHandled = InvokeProcessHomeKey(homeGrid, shift: false, ctrl: false);

        Assert.True(homeHandled);
        Assert.Equal(0, homeGrid.CurrentColumnIndex);
        Assert.Equal(homeGrid.SlotFromRowIndex(0), homeGrid.CurrentSlot);

        var (endGrid, _) = CreateGrid();

        var endHandled = InvokeProcessEndKey(endGrid, shift: false, ctrl: false);

        Assert.True(endHandled);
        Assert.Equal(2, endGrid.CurrentColumnIndex);
        Assert.Equal(endGrid.SlotFromRowIndex(0), endGrid.CurrentSlot);
    }

    [AvaloniaFact]
    public void DeleteKey_Ignored_When_CanRemove_False()
    {
        var (grid, items) = CreateGrid(rowCount: 1, canUserDeleteRows: false);
        grid.SelectedIndex = 0;
        grid.UpdateLayout();

        var args = PressKey(grid, Key.Delete);

        Assert.False(args.Handled);
        Assert.Single(items);
    }

    [AvaloniaFact]
    public void DeleteKey_Ignored_When_No_Selection()
    {
        var (grid, items) = CreateGrid(canUserDeleteRows: true);

        var args = PressKey(grid, Key.Delete);

        Assert.False(args.Handled);
        Assert.Equal(5, items.Count);
    }

    [AvaloniaFact]
    public void DeleteKey_Removes_Selected_Item()
    {
        var (grid, items) = CreateGrid(canUserDeleteRows: true);
        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        var args = PressKey(grid, Key.Delete);

        Assert.True(args.Handled);
        Assert.Equal(4, items.Count);
    }

    [AvaloniaFact]
    public void DeleteKey_Returns_False_When_Commit_Fails()
    {
        var items = new ObservableCollection<RowItem>
        {
            new() { A = 1, B = 2, C = 3 }
        };

        var root = new Window
        {
            Width = 640,
            Height = 480
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            CanUserAddRows = false,
            CanUserDeleteRows = true
        };

        grid.ColumnsInternal.Add(new FailingCommitColumn());

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var args = PressKey(grid, Key.Delete);

        Assert.False(args.Handled);
        Assert.Single(items);
    }

    [AvaloniaFact]
    public void F2_Starts_Edit_When_Cell_Is_Editable()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = PressKey(grid, Key.F2);

        Assert.True(args.Handled);
        Assert.Equal(0, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Starts_Edit_On_NewItemPlaceholder()
    {
        var (grid, items) = CreateGrid(rowCount: 2, canUserAddRows: true);
        var slot = grid.SlotFromRowIndex(items.Count);
        grid.UpdateSelectionAndCurrency(columnIndex: 0, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
        grid.UpdateLayout();

        var args = PressKey(grid, Key.F2);

        Assert.True(args.Handled);
        Assert.Equal(0, grid.EditingColumnIndex);
        Assert.NotNull(grid.EditingRow);
    }

    [AvaloniaFact]
    public void F2_On_Placeholder_Does_Not_Shift_Vertical_Offset()
    {
        var (grid, items) = CreateGrid(rowCount: 50, canUserAddRows: true);
        grid.AutoScrollToSelectedItem = false;

        SetCurrentCell(grid, rowIndex: items.Count, columnIndex: 0);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        var offsetBefore = grid.GetVerticalOffset();
        Assert.True(offsetBefore > 0, $"Expected scroll offset to be > 0, got {offsetBefore}.");

        var args = PressKey(grid, Key.F2);
        Dispatcher.UIThread.RunJobs();
        grid.UpdateLayout();

        var offsetAfter = grid.GetVerticalOffset();
        Assert.True(args.Handled);
        Assert.NotNull(grid.EditingRow);
        Assert.InRange(offsetAfter, offsetBefore - 0.5, offsetBefore + 0.5);
    }

    [AvaloniaFact]
    public void F2_Starts_Edit_When_Alt_Pressed()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = PressKey(grid, Key.F2, KeyModifiers.Alt);

        Assert.True(args.Handled);
        Assert.Equal(0, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_Modifier_Pressed()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var args = PressKey(grid, Key.F2, ctrl);

        Assert.False(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_Shift_Pressed()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = PressKey(grid, Key.F2, KeyModifiers.Shift);

        Assert.False(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_Row_Not_Selected()
    {
        var (grid, _) = CreateGrid();
        var slot = grid.SlotFromRowIndex(0);
        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: slot);

        var args = PressKey(grid, Key.F2);

        Assert.False(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_No_Current_Cell()
    {
        var (grid, _) = CreateGrid();
        ResetCurrentCell(grid);

        var args = PressKey(grid, Key.F2);

        Assert.False(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_ReadOnly()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal[0].IsReadOnly = true;
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = PressKey(grid, Key.F2);

        Assert.False(args.Handled);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void F2_Does_Not_Edit_When_Already_Editing()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var args = PressKey(grid, Key.F2);

        Assert.False(args.Handled);
        Assert.NotEqual(-1, grid.EditingColumnIndex);
    }

    private static (DataGrid grid, ObservableCollection<RowItem> items) CreateGrid(
        int rowCount = 5,
        DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow,
        DataGridSelectionMode selectionMode = DataGridSelectionMode.Extended,
        bool autoGenerateColumns = false,
        bool addColumns = true,
        bool canUserDeleteRows = false,
        bool canUserAddRows = false)
    {
        var items = new ObservableCollection<RowItem>();
        for (var i = 0; i < rowCount; i++)
        {
            items.Add(new RowItem { A = i, B = i + 100, C = i + 200 });
        }

        var root = new Window
        {
            Width = 640,
            Height = 480
        };

        root.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = selectionMode,
            SelectionUnit = selectionUnit,
            CanUserAddRows = canUserAddRows,
            CanUserDeleteRows = canUserDeleteRows,
            AutoGenerateColumns = autoGenerateColumns
        };

        if (addColumns)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "A",
                Binding = new Binding(nameof(RowItem.A))
            });
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "B",
                Binding = new Binding(nameof(RowItem.B))
            });
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "C",
                Binding = new Binding(nameof(RowItem.C))
            });
        }

        root.Content = grid;
        root.Show();
        grid.UpdateLayout();

        return (grid, items);
    }

    private static void SetCurrentCell(DataGrid grid, int rowIndex, int columnIndex)
    {
        var slot = grid.SlotFromRowIndex(rowIndex);
        grid.UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: true);
        grid.UpdateLayout();
    }

    private static void SetCurrentCellCoordinates(DataGrid grid, int columnIndex, int slot)
    {
        var slotProperty = typeof(DataGrid).GetProperty("CurrentSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var columnProperty = typeof(DataGrid).GetProperty("CurrentColumnIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        slotProperty?.SetValue(grid, slot);
        columnProperty?.SetValue(grid, columnIndex);
        grid.UpdateLayout();
    }

    private static KeyEventArgs PressKey(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = key,
            KeyModifiers = modifiers,
            Source = target,
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        if (target is DataGrid grid)
        {
            InvokeDataGridKeyDown(grid, args);
        }
        target.UpdateLayout();
        return args;
    }

    private static void InvokeDataGridKeyDown(DataGrid grid, KeyEventArgs args)
    {
        var method = typeof(DataGrid).GetMethod(
            "DataGrid_KeyDown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(grid, new object[] { grid, args });
    }

    private static KeyModifiers GetCtrlOrCmdModifier(Control target)
    {
        return KeyboardHelper.GetPlatformCtrlOrCmdKeyModifier(target);
    }

    private static Control BeginEditAndFocus(DataGrid grid)
    {
        Assert.True(grid.BeginEdit());
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var editingRow = grid.EditingRow;
        Assert.NotNull(editingRow);

        var cell = editingRow!.Cells[grid.CurrentColumnIndex];
        Assert.NotNull(cell);

        var editingElement = cell.Content as Control;
        Assert.NotNull(editingElement);

        editingElement!.Focus();
        Assert.True(editingElement.IsFocused);

        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return editingElement;
    }

    private static void ResetCurrentCell(DataGrid grid)
    {
        var method = typeof(DataGrid).GetMethod("ResetCurrentCellCore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(grid, Array.Empty<object>());
        grid.UpdateLayout();
    }

    private static void ForceSlotCount(DataGrid grid, int slotCount)
    {
        var slotCountProperty = typeof(DataGrid).GetProperty("SlotCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        slotCountProperty?.SetValue(grid, slotCount);
        grid.VisibleSlotCount = slotCount;
        grid.UpdateLayout();
    }

    private static bool InvokeProcessDownKeyInternal(DataGrid grid, bool shift, bool ctrl)
    {
        var method = typeof(DataGrid).GetMethod("ProcessDownKeyInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return method != null && (bool)method.Invoke(grid, new object[] { shift, ctrl })!;
    }

    private static bool InvokeProcessLeftMost(DataGrid grid, int firstVisibleColumnIndex, int firstVisibleSlot)
    {
        var method = typeof(DataGrid).GetMethod("ProcessLeftMost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return method != null && (bool)method.Invoke(grid, new object[] { firstVisibleColumnIndex, firstVisibleSlot })!;
    }

    private static bool InvokeProcessRightMost(DataGrid grid, int lastVisibleColumnIndex, int firstVisibleSlot)
    {
        var method = typeof(DataGrid).GetMethod("ProcessRightMost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return method != null && (bool)method.Invoke(grid, new object[] { lastVisibleColumnIndex, firstVisibleSlot })!;
    }

    private static bool InvokeProcessHomeKey(DataGrid grid, bool shift, bool ctrl)
    {
        var method = typeof(DataGrid).GetMethod(
            "ProcessHomeKey",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(bool), typeof(bool) },
            modifiers: null);
        return method != null && (bool)method.Invoke(grid, new object[] { shift, ctrl })!;
    }

    private static bool InvokeProcessEndKey(DataGrid grid, bool shift, bool ctrl)
    {
        var method = typeof(DataGrid).GetMethod(
            "ProcessEndKey",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(bool), typeof(bool) },
            modifiers: null);
        return method != null && (bool)method.Invoke(grid, new object[] { shift, ctrl })!;
    }

    private static Queue<Action> GetLostFocusActions(DataGrid grid)
    {
        var field = typeof(DataGrid).GetField("_lostFocusActions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (Queue<Action>)field!.GetValue(grid)!;
    }

    private static void SetFocusedObject(DataGrid grid, Control focusedObject)
    {
        var focusedObjectField = typeof(DataGrid).GetField("_focusedObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        focusedObjectField?.SetValue(grid, focusedObject);
    }

    private static void SetExecutingLostFocusActions(DataGrid grid, bool value)
    {
        var field = typeof(DataGrid).GetField("_executingLostFocusActions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(grid, value);
    }

    private sealed class RowItem : IEditableObject
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }

        private (int A, int B, int C)? _backup;

        public void BeginEdit()
        {
            _backup = (A, B, C);
        }

        public void CancelEdit()
        {
            if (_backup.HasValue)
            {
                var value = _backup.Value;
                A = value.A;
                B = value.B;
                C = value.C;
            }
            _backup = null;
        }

        public void EndEdit()
        {
            _backup = null;
        }
    }

    private sealed class FailingCommitColumn : DataGridColumn
    {
        public FailingCommitColumn()
        {
            Header = "Fail";
        }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            return new TextBlock { Text = "x" };
        }

        protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out ICellEditBinding binding)
        {
            binding = new FailingEditBinding();
            return new TextBox { Text = "x" };
        }

        protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            return string.Empty;
        }
    }

    private sealed class FailingEditBinding : ICellEditBinding
    {
        public bool IsValid => false;
        public IEnumerable<Exception> ValidationErrors => new[] { new InvalidOperationException("Commit failed.") };
        public IObservable<bool> ValidationChanged { get; } = new EmptyObservable();

        public bool CommitEdit() => false;
    }

    private sealed class EmptyObservable : IObservable<bool>
    {
        public IDisposable Subscribe(IObserver<bool> observer)
        {
            return new EmptyDisposable();
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
