using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridInputNavigationTests
{
    [AvaloniaFact]
    public void ProcessDataGridKey_Handles_All_Key_Cases()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var keys = new[]
        {
            Key.Tab,
            Key.Up,
            Key.Down,
            Key.PageDown,
            Key.PageUp,
            Key.Left,
            Key.Right,
            Key.F2,
            Key.Home,
            Key.End,
            Key.Enter,
            Key.Escape,
            Key.A,
            Key.C,
            Key.Insert,
            Key.Delete,
            Key.Multiply
        };

        var results = new List<bool>();
        foreach (var key in keys)
        {
            results.Add(InvokeProcessDataGridKey(grid, key, KeyModifiers.None));
        }

        Assert.Contains(results, handled => handled);
        Assert.Contains(results, handled => !handled);
    }

    [AvaloniaFact]
    public void KeyUp_Tab_Selects_Row_When_No_SelectedItem()
    {
        var (grid, items) = CreateGrid();
        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: grid.SlotFromRowIndex(0));
        Assert.Null(grid.SelectedItem);

        InvokeKeyUp(grid, Key.Tab, source: grid);

        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void KeyUp_Tab_Does_Not_Change_When_Already_Selected()
    {
        var (grid, items) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.Equal(items[0], grid.SelectedItem);

        InvokeKeyUp(grid, Key.Tab, source: grid);

        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void KeyUp_Ignores_Non_Tab_Key()
    {
        var (grid, items) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        InvokeKeyUp(grid, Key.Enter, source: grid);

        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void Tab_Returns_False_When_Ctrl_Pressed()
    {
        var (grid, _) = CreateGrid();
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, ctrl);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Tab_Returns_False_When_Not_Editing()
    {
        var (grid, _) = CreateGrid();

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Tab_Returns_False_When_ReadOnly_Even_When_Editing()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());
        grid.IsReadOnly = true;

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Tab_Moves_To_Next_Column_In_Single_Selection()
    {
        var (grid, _) = CreateGrid(selectionMode: DataGridSelectionMode.Single);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(1, grid.CurrentColumnIndex);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void Tab_Moves_To_Next_Row_When_No_Next_Writable_Column()
    {
        var (grid, _) = CreateGrid(columnCount: 1);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(1), grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void Shift_Tab_Moves_To_Previous_Row_When_No_Previous_Column()
    {
        var (grid, _) = CreateGrid(columnCount: 1, rowCount: 3);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void Tab_Returns_False_When_No_Neighbor_Row_Or_Column()
    {
        var (grid, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Tab_Defers_When_Editing_Element_Has_Focus()
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

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void Tab_Returns_True_When_TargetSlot_Is_Collapsed()
    {
        var (grid, _) = CreateGrid(columnCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());
        CollapseSlot(grid, grid.CurrentSlot);

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.True(handled);
    }

    [AvaloniaFact]
    public void ShiftTab_Skips_GroupSlots_When_Editing()
    {
        var (grid, _) = CreateGrid(rowCount: 2, columnCount: 1);
        AddRowGroupHeaderSlot(grid, slot: 0, isVisible: false);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.Shift);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Tab_Skips_GroupSlots_When_Editing()
    {
        var (grid, _) = CreateGrid(rowCount: 2, columnCount: 1);
        AddRowGroupHeaderSlot(grid, slot: 1, isVisible: false);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var handled = InvokeKeyHandler(grid, "ProcessTabKey", Key.Tab, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(1), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void WaitForLostFocus_Defers_KeyHandlers()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);
        grid.Focusable = false;

        SetFocusedObject(grid, editingElement);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);

        var methods = new (string Method, Key Key)[]
        {
            ("ProcessTabKey", Key.Tab),
            ("ProcessUpKey", Key.Up),
            ("ProcessLeftKey", Key.Left),
            ("ProcessRightKey", Key.Right),
            ("ProcessHomeKey", Key.Home),
            ("ProcessEndKey", Key.End),
            ("ProcessEnterKey", Key.Enter),
            ("ProcessNextKey", Key.PageDown),
            ("ProcessPriorKey", Key.PageUp)
        };

        foreach (var (method, key) in methods)
        {
            lostFocusQueue.Clear();
            var handled = InvokeKeyHandler(grid, method, key, KeyModifiers.None);
            Assert.True(handled);
            Assert.NotEmpty(lostFocusQueue);
        }
    }

    [AvaloniaFact]
    public void UpKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void UpKey_Selects_First_Cell_When_No_Current_Cell()
    {
        var (grid, items) = CreateGrid();
        ResetCurrentCell(grid);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void UpKey_Shift_Extends_Selection()
    {
        var (grid, items) = CreateGrid(rowCount: 3);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Contains(items[0], grid.SelectedItems.Cast<InputRow>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void UpKey_Moves_To_Previous_Row_When_No_Modifiers()
    {
        var (grid, items) = CreateGrid(rowCount: 3);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(1), grid.CurrentSlot);
        Assert.Equal(items[1], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void UpKey_Ctrl_Jumps_To_First_Row()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, ctrl);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void UpKey_CtrlShift_Extends_To_First_Row()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, ctrl | KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.True(grid.SelectedItems.Count >= 2);
        Assert.Contains(items[0], grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void UpKey_CtrlShift_Uses_SingleSelection_When_Not_Extended()
    {
        var (grid, _) = CreateGrid(rowCount: 4, selectionMode: DataGridSelectionMode.Single);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, ctrl | KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Equal(1, grid.SelectedItems.Count);
    }

    [AvaloniaFact]
    public void UpKey_At_First_Row_Does_Not_Move()
    {
        var (grid, items) = CreateGrid(rowCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var handled = InvokeKeyHandler(grid, "ProcessUpKey", Key.Up, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(items[0], grid.SelectedItem);
    }

    [AvaloniaFact]
    public void LeftRight_Return_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        Assert.False(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.False(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void LeftRight_Return_False_When_No_Rows()
    {
        var (grid, _) = CreateGrid();
        ForceSlotCount(grid, 0);

        Assert.False(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.False(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void LeftRight_Ctrl_Moves_To_Edges()
    {
        var (grid, _) = CreateGrid();
        var ctrl = GetCtrlOrCmdModifier(grid);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, ctrl));
        Assert.Equal(0, grid.CurrentColumnIndex);

        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, ctrl));
        Assert.Equal(grid.ColumnsInternal.LastVisibleColumn!.Index, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void LeftRight_Returns_True_When_No_Adjacent_Column()
    {
        var (grid, _) = CreateGrid(columnCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.Equal(0, grid.CurrentColumnIndex);

        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void LeftRight_Selects_First_Cell_When_CurrentColumn_Unset()
    {
        var (grid, _) = CreateGrid(columnCount: 2);
        var slot = grid.SlotFromRowIndex(0);
        SetCurrentCellCoordinates(grid, columnIndex: -1, slot: slot);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.Equal(0, grid.CurrentColumnIndex);
        Assert.Equal(slot, grid.CurrentSlot);

        SetCurrentCellCoordinates(grid, columnIndex: -1, slot: slot);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
        Assert.Equal(0, grid.CurrentColumnIndex);
        Assert.Equal(slot, grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void LeftRight_GroupHeader_ExpandCollapse()
    {
        var (grid, _) = CreateGrid(rowCount: 2, columnCount: 1);
        AddRowGroupHeaderSlot(grid, slot: 0, isVisible: false);
        AddRowGroupHeaderSlot(grid, slot: 2, isVisible: true);

        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: 0);
        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));

        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: 2);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void GroupSlots_Treated_As_Nodes_For_Hierarchical_Keys()
    {
        var (grid, _) = CreateGrid(rowCount: 1, columnCount: 1);
        var group = AddRowGroupHeaderSlot(grid, slot: 0, isVisible: true);
        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: 0);

        EnableHierarchicalAdapter(grid, group, _ => null, treatGroupsAsNodes: true);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.Alt));
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.Alt));
        Assert.True(InvokeKeyHandler(grid, "ProcessMultiplyKey", Key.Multiply, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MultiplyKey_Returns_False_When_HierarchicalAdapter_Missing()
    {
        var (grid, _) = CreateGrid();
        SetPrivateField(grid, "_hierarchicalRowsEnabled", true);
        SetPrivateField(grid, "_hierarchicalAdapter", null);

        Assert.False(InvokeKeyHandler(grid, "ProcessMultiplyKey", Key.Multiply, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MultiplyKey_Returns_False_When_No_HierarchicalIndex()
    {
        var (grid, _) = CreateGrid();
        var root = new HierarchyItem("root");
        EnableHierarchicalAdapter(grid, root, item => ((HierarchyItem)item).Children, treatGroupsAsNodes: false);
        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: -1);

        Assert.False(InvokeKeyHandler(grid, "ProcessMultiplyKey", Key.Multiply, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MultiplyKey_Expands_Node_When_HierarchicalIndex_Available()
    {
        var (grid, _) = CreateGrid();
        var root = new HierarchyItem("root");
        var child = new HierarchyItem("child");
        root.Children.Add(child);
        var (_, adapter) = EnableHierarchicalAdapter(
            grid,
            root,
            item => ((HierarchyItem)item).Children,
            treatGroupsAsNodes: false,
            isLeafSelector: item => item is HierarchyItem node && node.Children.Count == 0);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.True(InvokeKeyHandler(grid, "ProcessMultiplyKey", Key.Multiply, KeyModifiers.None));
        Assert.True(adapter.NodeAt(0).IsExpanded);
    }

    [AvaloniaFact]
    public void Hierarchical_LeftRight_ExpandCollapse()
    {
        var (grid, _) = CreateGrid();
        var root = new HierarchyItem("root");
        var child = new HierarchyItem("child");
        child.Children.Add(new HierarchyItem("grand"));
        root.Children.Add(child);

        var (_, adapter) = EnableHierarchicalAdapter(
            grid,
            root,
            item => ((HierarchyItem)item).Children,
            treatGroupsAsNodes: false,
            isLeafSelector: item => item is HierarchyItem node && node.Children.Count == 0);
        adapter.Expand(0);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
        Assert.True(adapter.NodeAt(1).IsExpanded);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.Alt));
        Assert.False(adapter.NodeAt(1).IsExpanded);
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.Alt));
        Assert.True(adapter.NodeAt(1).IsExpanded);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.False(adapter.NodeAt(1).IsExpanded);
        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void Hierarchical_Left_Does_Not_Collapse_When_Node_Not_Expanded()
    {
        var (grid, _) = CreateGrid();
        var root = new HierarchyItem("root");
        root.Children.Add(new HierarchyItem("child"));

        var (_, adapter) = EnableHierarchicalAdapter(grid, root, item => ((HierarchyItem)item).Children, treatGroupsAsNodes: false);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.False(adapter.NodeAt(0).IsExpanded);
        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.False(adapter.NodeAt(0).IsExpanded);
    }

    [AvaloniaFact]
    public void LeftRight_Ignores_Leaf_Node_When_Hierarchical()
    {
        var (grid, _) = CreateGrid();
        var root = new HierarchyItem("root");
        var child = new HierarchyItem("child");
        root.Children.Add(child);
        var (_, adapter) = EnableHierarchicalAdapter(
            grid,
            root,
            item => ((HierarchyItem)item).Children,
            treatGroupsAsNodes: false,
            isLeafSelector: item => item is HierarchyItem node && node.Children.Count == 0);
        adapter.Expand(0);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void LeftRight_Ignore_Invalid_Hierarchical_Slot()
    {
        var (grid, _) = CreateGrid(rowCount: 3);
        var root = new HierarchyItem("root");
        EnableHierarchicalAdapter(grid, root, item => ((HierarchyItem)item).Children, treatGroupsAsNodes: false);
        var slot = grid.SlotFromRowIndex(2);
        SetCurrentCellCoordinates(grid, columnIndex: 0, slot: slot);

        Assert.True(InvokeKeyHandler(grid, "ProcessLeftKey", Key.Left, KeyModifiers.None));
        Assert.True(InvokeKeyHandler(grid, "ProcessRightKey", Key.Right, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void HomeKey_ShiftCtrl_Extends_Selection()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 1);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessHomeKey", Key.Home, ctrl | KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.True(grid.SelectedItems.Count >= 2);
        Assert.Contains(items[0], grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void HomeKey_Ctrl_Selects_First_Cell_Without_Shift()
    {
        var (grid, _) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 1);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessHomeKey", Key.Home, ctrl);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void EndKey_ShiftCtrl_Extends_Selection()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessEndKey", Key.End, ctrl | KeyModifiers.Shift);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.True(grid.SelectedItems.Count >= 2);
        Assert.Contains(items.Last(), grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void EndKey_Ctrl_Selects_Last_Cell_Without_Shift()
    {
        var (grid, items) = CreateGrid(rowCount: 4);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessEndKey", Key.End, ctrl);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(items.Count - 1), grid.CurrentSlot);
        Assert.Equal(grid.ColumnsInternal.LastVisibleColumn!.Index, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void HomeKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessHomeKey", Key.Home, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void EndKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessEndKey", Key.End, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Enter_Returns_False_When_Focused_TextBox_AcceptsReturn()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid) as TextBox;
        Assert.NotNull(editingElement);
        editingElement!.AcceptsReturn = true;
        editingElement.Focus();

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Enter_Continues_When_Focused_TextBox_Does_Not_Accept_Return()
    {
        var (grid, _) = CreateGrid(rowCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid) as TextBox;
        Assert.NotNull(editingElement);
        editingElement!.AcceptsReturn = false;
        editingElement.Focus();

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, KeyModifiers.None);

        Assert.True(handled);
    }

    [AvaloniaFact]
    public void Enter_Returns_False_When_ProcessDownKeyInternal_Fails()
    {
        var (grid, _) = CreateGrid();
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void Enter_Returns_False_When_FocusManager_Missing()
    {
        var grid = new DataGrid
        {
            ItemsSource = new ObservableCollection<InputRow> { new InputRow { A = 1, B = 2, C = 3 } },
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            AutoGenerateColumns = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "A",
            Binding = new Binding(nameof(InputRow.A))
        });

        grid.UpdateLayout();

        var method = typeof(DataGrid).GetMethod(
            "ProcessEnterKey",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(bool), typeof(bool) },
            modifiers: null);
        var handled = method != null && (bool)method.Invoke(grid, new object[] { false, false })!;

        Assert.True(handled);
    }

    [AvaloniaFact]
    public void Enter_With_Ctrl_Returns_True()
    {
        var (grid, _) = CreateGrid();
        var ctrl = GetCtrlOrCmdModifier(grid);

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, ctrl);

        Assert.True(handled);
    }

    [AvaloniaFact]
    public void Enter_Defers_When_Ctrl_And_Editing_Element_Has_Focus()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);
        var ctrl = GetCtrlOrCmdModifier(grid);

        SetFocusedObject(grid, editingElement);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);
        lostFocusQueue.Clear();

        var lostFocusRaised = false;
        editingElement.LostFocus += (_, _) => lostFocusRaised = true;

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, ctrl);

        Assert.True(handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void Enter_Commits_Row_When_Editing_On_Last_Row()
    {
        var (grid, _) = CreateGrid(rowCount: 2);
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);
        Assert.True(grid.BeginEdit());
        Assert.NotNull(grid.EditingRow);

        var handled = InvokeKeyHandler(grid, "ProcessEnterKey", Key.Enter, KeyModifiers.None);

        Assert.True(handled);
        Assert.Null(grid.EditingRow);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void NextKey_Returns_False_When_No_Rows()
    {
        var (grid, _) = CreateGrid(rowCount: 0);
        ResetCurrentCell(grid);

        var handled = InvokeKeyHandler(grid, "ProcessNextKey", Key.PageDown, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void NextKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid(rowCount: 3);
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessNextKey", Key.PageDown, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void NextKey_Selects_Page_Down_With_Shift()
    {
        var (grid, items) = CreateGrid(rowCount: 10);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var handled = InvokeKeyHandler(grid, "ProcessNextKey", Key.PageDown, KeyModifiers.Shift);

        Assert.True(handled);
        Assert.True(grid.SelectedItems.Count > 1);
        Assert.Contains(items[0], grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void NextKey_Uses_FirstSlot_When_No_Current_Cell()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        ResetCurrentCell(grid);

        var handled = InvokeKeyHandler(grid, "ProcessNextKey", Key.PageDown, KeyModifiers.None);

        Assert.True(handled);
        Assert.NotEqual(-1, grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void NextKey_Does_Not_Page_When_NoDisplayedElements()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        SetDisplayedScrollingElements(grid, 0);

        var handled = InvokeKeyHandler(grid, "ProcessNextKey", Key.PageDown, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void PriorKey_Returns_False_When_No_Rows()
    {
        var (grid, _) = CreateGrid(rowCount: 0);
        ResetCurrentCell(grid);

        var handled = InvokeKeyHandler(grid, "ProcessPriorKey", Key.PageUp, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void PriorKey_Returns_False_When_No_Columns()
    {
        var (grid, _) = CreateGrid(rowCount: 3);
        grid.ColumnsInternal.Clear();
        grid.ColumnsInternal.ItemsInternal.Clear();
        grid.ColumnsInternal.DisplayIndexMap.Clear();

        var handled = InvokeKeyHandler(grid, "ProcessPriorKey", Key.PageUp, KeyModifiers.None);

        Assert.False(handled);
    }

    [AvaloniaFact]
    public void PriorKey_Selects_Page_Up_With_Shift()
    {
        var (grid, items) = CreateGrid(rowCount: 10);
        SetCurrentCell(grid, rowIndex: 5, columnIndex: 0);

        var handled = InvokeKeyHandler(grid, "ProcessPriorKey", Key.PageUp, KeyModifiers.Shift);

        Assert.True(handled);
        Assert.True(grid.SelectedItems.Count > 1);
        Assert.Contains(items[5], grid.SelectedItems.Cast<InputRow>());
    }

    [AvaloniaFact]
    public void PriorKey_Uses_FirstSlot_When_No_Current_Cell()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        ResetCurrentCell(grid);

        var handled = InvokeKeyHandler(grid, "ProcessPriorKey", Key.PageUp, KeyModifiers.None);

        Assert.True(handled);
        Assert.NotEqual(-1, grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void PriorKey_Does_Not_Page_When_NoDisplayedElements()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        SetCurrentCell(grid, rowIndex: 3, columnIndex: 0);
        SetDisplayedScrollingElements(grid, 0);

        var handled = InvokeKeyHandler(grid, "ProcessPriorKey", Key.PageUp, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(grid.SlotFromRowIndex(3), grid.CurrentSlot);
    }

    [AvaloniaFact]
    public void XYFocus_NavigationModes_Inherits_From_TopLevel()
    {
        var (grid, _) = CreateGrid(rowCount: 3);
        var root = TopLevel.GetTopLevel(grid);
        Assert.NotNull(root);

        XYFocus.SetNavigationModes(root!, XYFocusNavigationModes.Enabled);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(XYFocusNavigationModes.Enabled, XYFocus.GetNavigationModes(grid));
    }

    [AvaloniaFact]
    public void XYFocus_NavigationModes_Local_Value_Overrides_Inherited()
    {
        var (grid, _) = CreateGrid(rowCount: 3);
        var root = TopLevel.GetTopLevel(grid);
        Assert.NotNull(root);

        XYFocus.SetNavigationModes(root!, XYFocusNavigationModes.Gamepad);
        XYFocus.SetNavigationModes(grid, XYFocusNavigationModes.Enabled);
        grid.UpdateLayout();

        Assert.Equal(XYFocusNavigationModes.Enabled, XYFocus.GetNavigationModes(grid));
    }

    [AvaloniaFact]
    public void KeyDown_Respects_Handled_Flag()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var handledArgs = CreateKeyArgs(grid, Key.Down, KeyModifiers.None, InputElement.KeyDownEvent);
        handledArgs.Handled = true;
        InvokeKeyDown(grid, handledArgs);
        Assert.True(handledArgs.Handled);

        var args = CreateKeyArgs(grid, Key.Down, KeyModifiers.None, InputElement.KeyDownEvent);
        InvokeKeyDown(grid, args);
        Assert.True(args.Handled);
    }

    private static (DataGrid grid, ObservableCollection<InputRow> items) CreateGrid(
        int rowCount = 5,
        int columnCount = 3,
        DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow,
        DataGridSelectionMode selectionMode = DataGridSelectionMode.Extended)
    {
        if (columnCount < 1 || columnCount > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        var items = new ObservableCollection<InputRow>();
        for (var i = 0; i < rowCount; i++)
        {
            items.Add(new InputRow { A = i, B = i + 100, C = i + 200 });
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
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            AutoGenerateColumns = false
        };

        if (columnCount >= 1)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "A",
                Binding = new Binding(nameof(InputRow.A))
            });
        }

        if (columnCount >= 2)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "B",
                Binding = new Binding(nameof(InputRow.B))
            });
        }

        if (columnCount >= 3)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "C",
                Binding = new Binding(nameof(InputRow.C))
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

    private static KeyEventArgs CreateKeyArgs(Control target, Key key, KeyModifiers modifiers, RoutedEvent routedEvent)
    {
        return new KeyEventArgs
        {
            RoutedEvent = routedEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = target,
            KeyDeviceType = KeyDeviceType.Keyboard
        };
    }

    private static bool InvokeProcessDataGridKey(DataGrid grid, Key key, KeyModifiers modifiers)
    {
        var method = typeof(DataGrid).GetMethod("ProcessDataGridKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var args = CreateKeyArgs(grid, key, modifiers, InputElement.KeyDownEvent);
        return method != null && (bool)method.Invoke(grid, new object[] { args })!;
    }

    private static bool InvokeKeyHandler(DataGrid grid, string methodName, Key key, KeyModifiers modifiers)
    {
        var method = typeof(DataGrid).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(KeyEventArgs) },
            modifiers: null);
        var args = CreateKeyArgs(grid, key, modifiers, InputElement.KeyDownEvent);
        return method != null && (bool)method.Invoke(grid, new object[] { args })!;
    }

    private static void InvokeKeyUp(DataGrid grid, Key key, object? source)
    {
        var method = typeof(DataGrid).GetMethod(
            "DataGrid_KeyUp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var args = CreateKeyArgs(grid, key, KeyModifiers.None, InputElement.KeyUpEvent);
        args.Source = source ?? grid;
        method?.Invoke(grid, new object[] { grid, args });
        grid.UpdateLayout();
    }

    private static void InvokeKeyDown(DataGrid grid, KeyEventArgs args)
    {
        var method = typeof(DataGrid).GetMethod(
            "DataGrid_KeyDown",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(grid, new object[] { grid, args });
    }

    private static void SetDisplayedScrollingElements(DataGrid grid, int count)
    {
        var property = typeof(DataGrid).GetProperty("DisplayData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var displayData = property?.GetValue(grid);
        var elementsProperty = displayData?.GetType().GetProperty("NumTotallyDisplayedScrollingElements");
        elementsProperty?.SetValue(displayData, count);
    }

    private static KeyModifiers GetCtrlOrCmdModifier(Control target)
    {
        return KeyboardHelper.GetPlatformCtrlOrCmdKeyModifier(target);
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

    private static void CollapseSlot(DataGrid grid, int slot)
    {
        var field = typeof(DataGrid).GetField("_collapsedSlotsTable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var table = field?.GetValue(grid);
        var addMethod = table?.GetType().GetMethod("AddValue");
        addMethod?.Invoke(table, new object[] { slot, true });
    }

    private static object AddRowGroupHeaderSlot(DataGrid grid, int slot, bool isVisible)
    {
        var table = GetRowGroupHeadersTable(grid);
        var group = CreateCollectionViewGroupInternal($"group-{slot}", parent: null);
        var groupInfo = CreateRowGroupInfo(group, isVisible, level: 0, slot: slot, lastSubItemSlot: slot);
        var addMethod = table.GetType().GetMethod("AddValue");
        addMethod?.Invoke(table, new object[] { slot, groupInfo });
        EnsureSlotCount(grid, slot);
        return group;
    }

    private static object GetRowGroupHeadersTable(DataGrid grid)
    {
        var property = typeof(DataGrid).GetProperty("RowGroupHeadersTable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return property!.GetValue(grid)!;
    }

    private static object CreateCollectionViewGroupInternal(object key, object? parent)
    {
        var groupType = typeof(DataGrid).Assembly.GetType("Avalonia.Collections.DataGridCollectionViewGroupInternal");
        return Activator.CreateInstance(
            groupType!,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: new[] { key, parent },
            culture: null)!;
    }

    private static object CreateRowGroupInfo(object group, bool isVisible, int level, int slot, int lastSubItemSlot)
    {
        var infoType = typeof(DataGrid).Assembly.GetType("Avalonia.Controls.DataGridRowGroupInfo");
        return Activator.CreateInstance(
            infoType!,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: new object[] { group, isVisible, level, slot, lastSubItemSlot },
            culture: null)!;
    }

    private static void EnsureSlotCount(DataGrid grid, int slot)
    {
        var slotCountProperty = typeof(DataGrid).GetProperty("SlotCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var currentSlotCount = (int?)slotCountProperty?.GetValue(grid) ?? 0;
        var newSlotCount = Math.Max(currentSlotCount + 1, slot + 1);
        slotCountProperty?.SetValue(grid, newSlotCount);
        grid.VisibleSlotCount = newSlotCount;
        grid.UpdateLayout();
    }

    private static (HierarchicalModel Model, DataGridHierarchicalAdapter Adapter) EnableHierarchicalAdapter(
        DataGrid grid,
        object root,
        Func<object, IEnumerable?>? childrenSelector,
        bool treatGroupsAsNodes,
        Func<object, bool>? isLeafSelector = null)
    {
        var options = new HierarchicalOptions
        {
            ChildrenSelector = childrenSelector,
            TreatGroupsAsNodes = treatGroupsAsNodes,
            IsLeafSelector = isLeafSelector
        };
        var model = new HierarchicalModel(options);
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);

        SetPrivateField(grid, "_hierarchicalModel", model);
        SetPrivateField(grid, "_hierarchicalAdapter", adapter);
        SetPrivateField(grid, "_hierarchicalRowsEnabled", true);

        return (model, adapter);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private sealed class HierarchyItem
    {
        public HierarchyItem(string name)
        {
            Name = name;
            Children = new List<HierarchyItem>();
        }

        public string Name { get; }

        public List<HierarchyItem> Children { get; }
    }

    private sealed class InputRow : IEditableObject
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
}
