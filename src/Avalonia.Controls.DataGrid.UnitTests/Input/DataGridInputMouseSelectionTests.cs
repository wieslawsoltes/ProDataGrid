using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridInputMouseSelectionTests
{
    [AvaloniaFact]
    public void MouseLeft_RowSelection_Covers_Modifier_Branches()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var ctrl = GetCtrlOrCmdModifier(grid);
        var slot0 = grid.SlotFromRowIndex(0);
        var slot1 = grid.SlotFromRowIndex(1);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);
        Assert.Single(grid.SelectedItems);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, ctrl), columnIndex: 0, slot: slot1, allowEdit: false);
        Assert.Equal(2, grid.SelectedItems.Count);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);
        Assert.Single(grid.SelectedItems);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, KeyModifiers.Shift), columnIndex: 0, slot: slot1, allowEdit: false);
        Assert.True(grid.SelectedItems.Count >= 2);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, ctrl), columnIndex: 0, slot: slot1, allowEdit: false);
        Assert.Single(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseLeft_RowSelection_Does_Nothing_When_Single_Mode_Selected()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Single);
        var slot0 = grid.SlotFromRowIndex(0);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);

        Assert.Single(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseLeft_RowSelection_Begins_Edit_When_Allowed()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var slot = grid.SlotFromRowIndex(0);
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: true);

        Assert.NotEqual(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_RowSelection_Defers_When_Editing_Cell_Has_Focus()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);

        SetFocusedObject(grid, editingElement);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);
        lostFocusQueue.Clear();

        var lostFocusRaised = false;
        editingElement.LostFocus += (_, _) => lostFocusRaised = true;

        var slot = grid.SlotFromRowIndex(1);
        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void MouseLeft_RowSelection_Returns_When_Slot_Out_Of_Bounds()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(0);
        CollapseSlot(grid, slot);

        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Extends_Range()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(endSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Shrinks_When_Reversing()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var midSlot = grid.SlotFromRowIndex(1);
        var endSlot = grid.SlotFromRowIndex(3);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var midRow = grid.DisplayData.GetDisplayedElement(midSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(midRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var midCell = midRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var midPoint = GetCenterPoint(midCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(4, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[3], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(endSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));

        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.DoesNotContain(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.DoesNotContain(items[3], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(midSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Ctrl_Preserves_Existing_And_Shrinks()
    {
        var (grid, items) = CreateGrid(rowCount: 5, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        SetCurrentCell(grid, rowIndex: 4, columnIndex: 0);

        var ctrl = GetCtrlOrCmdModifier(grid);
        var startSlot = grid.SlotFromRowIndex(0);
        var midSlot = grid.SlotFromRowIndex(1);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var midRow = grid.DisplayData.GetDisplayedElement(midSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(midRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var midCell = midRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var midPoint = GetCenterPoint(midCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, ctrl));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, ctrl));

        Assert.Equal(4, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[4], grid.SelectedItems.Cast<RowItem>());

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, midPoint, ctrl));

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[4], grid.SelectedItems.Cast<RowItem>());
        Assert.DoesNotContain(items[2], grid.SelectedItems.Cast<RowItem>());

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, midPoint, ctrl));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Refreshes_PointerOver_On_End()
    {
        var (grid, _) = CreateGrid(rowCount: 3, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var inputRoot = grid.GetVisualRoot() as IInputRoot;
        Assert.NotNull(inputRoot);
        inputRoot!.PointerOverElement = grid;

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        grid.MouseOverRowIndex = startRow!.Index;
        Assert.True(((IPseudoClasses)startRow.Classes).Contains(":pointerover"));

        var startCell = startRow.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(endRow.Index, grid.MouseOverRowIndex);
        Assert.True(((IPseudoClasses)endRow.Classes).Contains(":pointerover"));
        Assert.False(((IPseudoClasses)startRow.Classes).Contains(":pointerover"));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Clears_PointerOver_When_Released_Outside()
    {
        var (grid, _) = CreateGrid(rowCount: 3, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var inputRoot = grid.GetVisualRoot() as IInputRoot;
        Assert.NotNull(inputRoot);
        inputRoot!.PointerOverElement = grid;

        var startSlot = grid.SlotFromRowIndex(0);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        Assert.NotNull(startRow);

        grid.MouseOverRowIndex = startRow!.Index;
        Assert.True(((IPseudoClasses)startRow.Classes).Contains(":pointerover"));

        var startCell = startRow.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var outsidePoint = new Point(-20, -20);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, outsidePoint, KeyModifiers.None));
        inputRoot.PointerOverElement = null;
        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, outsidePoint, KeyModifiers.None));

        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(grid.MouseOverRowIndex);
        Assert.False(((IPseudoClasses)startRow.Classes).Contains(":pointerover"));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Preserves_Start_When_AnchorSlot_Shifts()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var midSlot = grid.SlotFromRowIndex(1);
        var endSlot = grid.SlotFromRowIndex(3);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var midRow = grid.DisplayData.GetDisplayedElement(midSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(midRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var midCell = midRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var midPoint = GetCenterPoint(midCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        SetAnchorSlot(grid, endSlot);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));

        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.DoesNotContain(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.DoesNotContain(items[3], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(midSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_From_Current_Cell_Extends_Range()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        startCell.Focus();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.True(startCell.IsKeyboardFocusWithin);

        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(endSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_RowSelection_Allows_Implicit_Capture()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        pointer.Capture(startCell);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Contains(items[2], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(endSlot, grid.CurrentSlot);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
        pointer.Capture(null);
    }

    [AvaloniaFact]
    public void PointerPressed_When_Handled_Changes_Selection_For_Unselected_Cell()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());

        var slot = grid.SlotFromRowIndex(1);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[0];
        Assert.NotNull(cell);

        var args = CreateLeftPointerArgs(cell);
        cell.AddHandler(InputElement.PointerPressedEvent, (_, eventArgs) => eventArgs.Handled = true, RoutingStrategies.Tunnel);

        cell.RaiseEvent(args);

        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(slot, grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void PointerPressed_When_Handled_Does_Not_Change_Selection_For_Current_Cell()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var slot = grid.SlotFromRowIndex(0);
        Assert.Equal(slot, grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());

        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[0];
        Assert.NotNull(cell);
        cell.Focus();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.True(cell.IsKeyboardFocusWithin);

        cell.AddHandler(InputElement.PointerPressedEvent, (_, eventArgs) => eventArgs.Handled = true, RoutingStrategies.Tunnel);

        var args = CreateLeftPointerArgs(cell);
        cell.RaiseEvent(args);

        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[0], grid.SelectedItems.Cast<RowItem>());
        Assert.Equal(slot, grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void PointerPressed_When_Handled_Allows_Cell_MultiSelect_With_Ctrl()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        var slot0 = grid.SlotFromRowIndex(0);
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);

        Assert.Single(grid.SelectedCells);
        var firstCell = grid.SelectedCells.First();
        Assert.Equal(0, firstCell.RowIndex);
        Assert.Equal(0, firstCell.ColumnIndex);
        Assert.Equal(slot0, grid.CurrentSlot);
        Assert.Equal(0, grid.CurrentColumnIndex);

        var ctrl = GetCtrlOrCmdModifier(grid);
        var slot = grid.SlotFromRowIndex(1);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[1];
        Assert.NotNull(cell);

        cell.AddHandler(InputElement.PointerPressedEvent, (_, eventArgs) => eventArgs.Handled = true, RoutingStrategies.Tunnel);

        var args = CreateLeftPointerArgs(cell, ctrl);
        cell.RaiseEvent(args);

        Assert.Equal(2, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, selected => selected.RowIndex == 0 && selected.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, selected => selected.RowIndex == 1 && selected.ColumnIndex == 1);
        Assert.Equal(slot, grid.CurrentSlot);
        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Single_Replaces_Selection()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Single);
        var slot = grid.SlotFromRowIndex(0);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 1, slot: slot, allowEdit: false);

        Assert.Single(grid.SelectedCells);
        Assert.Equal(1, grid.SelectedCells.First().ColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Extended_Shift_And_Ctrl_Toggles()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        var ctrl = GetCtrlOrCmdModifier(grid);
        var slot0 = grid.SlotFromRowIndex(0);
        var slot1 = grid.SlotFromRowIndex(1);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, KeyModifiers.Shift), columnIndex: 1, slot: slot1, allowEdit: false);
        Assert.True(grid.SelectedCells.Count > 1);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, ctrl | KeyModifiers.Shift), columnIndex: 0, slot: slot1, allowEdit: false);
        Assert.True(grid.SelectedCells.Count > 1);

        var previousCount = grid.SelectedCells.Count;
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, ctrl), columnIndex: 0, slot: slot1, allowEdit: false);
        Assert.True(grid.SelectedCells.Count < previousCount);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_NoCtrl_Covers_Empty_And_Populated_Selection()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        var slot0 = grid.SlotFromRowIndex(0);
        var slot1 = grid.SlotFromRowIndex(1);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot0, allowEdit: false);
        Assert.Single(grid.SelectedCells);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 1, slot: slot1, allowEdit: false);
        Assert.Single(grid.SelectedCells);
        Assert.Equal(1, grid.SelectedCells.First().ColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Shift_Ignored_When_No_Anchor()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        SetCellAnchor(grid, columnIndex: -1, slot: -1);
        var slot = grid.SlotFromRowIndex(0);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, KeyModifiers.Shift), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.Single(grid.SelectedCells);
        Assert.Equal(0, grid.SelectedCells.First().ColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Shift_With_Group_Anchor_Does_Not_Select_Range()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended, rowCount: 2);
        AddRowGroupHeaderSlot(grid, slot: 0, isVisible: true);
        SetCellAnchor(grid, columnIndex: 0, slot: 0);
        var slot = grid.SlotFromRowIndex(0);

        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, KeyModifiers.Shift), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Empty(grid.SelectedCells);
    }

    [AvaloniaFact]
    public void MouseDrag_CellSelection_Extends_Range()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(1);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[1];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(4, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 1);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 1);
        Assert.Equal(endSlot, grid.CurrentSlot);
        Assert.Equal(1, grid.CurrentColumnIndex);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_CellSelection_Shrinks_When_Reversing()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(1);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var midCell = startRow.Cells[1];
        var endCell = endRow!.Cells[1];
        var startPoint = GetCenterPoint(startCell, grid);
        var midPoint = GetCenterPoint(midCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(4, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 1);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 1);
        Assert.Equal(endSlot, grid.CurrentSlot);
        Assert.Equal(1, grid.CurrentColumnIndex);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));

        Assert.Equal(2, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 1);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 0);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 1);
        Assert.Equal(startSlot, grid.CurrentSlot);
        Assert.Equal(1, grid.CurrentColumnIndex);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, midPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void MouseDrag_CellSelection_Ctrl_Preserves_Existing_And_Shrinks()
    {
        var (grid, _) = CreateGrid(rowCount: 4, columnCount: 3, selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var ctrl = GetCtrlOrCmdModifier(grid);

        var preSlot = grid.SlotFromRowIndex(3);
        var preRow = grid.DisplayData.GetDisplayedElement(preSlot) as DataGridRow;
        Assert.NotNull(preRow);

        var preCell = preRow!.Cells[2];
        var prePoint = GetCenterPoint(preCell, grid);
        var prePointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        preCell.RaiseEvent(CreatePointerPressedArgs(preCell, grid, prePointer, prePoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, prePointer, prePoint, KeyModifiers.None));

        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 3 && cell.ColumnIndex == 2);

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(1);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var midCell = startRow.Cells[1];
        var endCell = endRow!.Cells[1];
        var startPoint = GetCenterPoint(startCell, grid);
        var midPoint = GetCenterPoint(midCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, ctrl));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, ctrl));

        Assert.Equal(5, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 1);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 1);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 3 && cell.ColumnIndex == 2);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, midPoint, ctrl));

        Assert.Equal(3, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 1);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 3 && cell.ColumnIndex == 2);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 0);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == 1);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, midPoint, ctrl));
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Begins_Edit_When_Allowed()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var slot = grid.SlotFromRowIndex(0);
        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: true);

        Assert.NotEqual(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Does_Not_Defer_When_Clicking_Current_Editing_Cell()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var slot = grid.SlotFromRowIndex(0);
        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.NotEqual(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Defers_When_Editing_Cell_Has_Focus()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var editingElement = BeginEditAndFocus(grid);

        SetFocusedObject(grid, editingElement);
        SetExecutingLostFocusActions(grid, false);

        var lostFocusQueue = GetLostFocusActions(grid);
        lostFocusQueue.Clear();

        var lostFocusRaised = false;
        editingElement.LostFocus += (_, _) => lostFocusRaised = true;

        var slot = grid.SlotFromRowIndex(1);
        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 1, slot: slot, allowEdit: false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(handled);
        Assert.True(lostFocusRaised || lostFocusQueue.Count > 0);
    }

    [AvaloniaFact]
    public void MouseLeft_CellSelection_Returns_When_Slot_Out_Of_Bounds()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(0);
        CollapseSlot(grid, slot);

        var handled = InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Empty(grid.SelectedCells);
    }

    [AvaloniaFact]
    public void MouseRight_Updates_CurrentCell_For_CellSelection()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var slot0 = grid.SlotFromRowIndex(0);
        var slot1 = grid.SlotFromRowIndex(1);

        InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 1, slot: slot0, allowEdit: false);
        Assert.Equal(1, grid.CurrentColumnIndex);
        Assert.Equal(slot0, grid.CurrentSlot);

        InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 1, slot: slot1, allowEdit: false);
        Assert.Equal(slot1, grid.CurrentSlot);

        InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 1, slot: slot1, allowEdit: false);
        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void MouseRight_Returns_When_Modifier_Pressed()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(0);

        var handled = InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid, KeyModifiers.Shift), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseRight_Returns_When_Slot_Out_Of_Bounds()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(0);
        CollapseSlot(grid, slot);

        var handled = InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Empty(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseRight_Returns_When_Row_Selected()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(0);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var handled = InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Single(grid.SelectedItems);
    }

    [AvaloniaFact]
    public void MouseRight_Selects_Row_When_Unselected()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        var slot = grid.SlotFromRowIndex(1);

        var handled = InvokeUpdateStateOnMouseRightButtonDown(grid, CreateRightPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);

        Assert.True(handled);
        Assert.Single(grid.SelectedItems);
    }

    private static (DataGrid grid, ObservableCollection<RowItem> items) CreateGrid(
        int rowCount = 3,
        int columnCount = 3,
        DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow,
        DataGridSelectionMode selectionMode = DataGridSelectionMode.Extended)
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
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            AutoGenerateColumns = false
        };

        if (columnCount >= 1)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "A",
                Binding = new Binding(nameof(RowItem.A))
            });
        }

        if (columnCount >= 2)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "B",
                Binding = new Binding(nameof(RowItem.B))
            });
        }

        if (columnCount >= 3)
        {
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

    private static void SetCellAnchor(DataGrid grid, int columnIndex, int slot)
    {
        var field = typeof(DataGrid).GetField("_cellAnchor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(grid, new DataGridCellCoordinates(columnIndex, slot));
    }

    private static PointerPressedEventArgs CreateLeftPointerArgs(Control target, KeyModifiers modifiers = KeyModifiers.None)
    {
        return CreatePointerArgs(target, RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed, modifiers);
    }

    private static PointerPressedEventArgs CreateRightPointerArgs(Control target, KeyModifiers modifiers = KeyModifiers.None)
    {
        return CreatePointerArgs(target, RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed, modifiers);
    }

    private static PointerPressedEventArgs CreatePointerArgs(Control target, RawInputModifiers rawModifiers, PointerUpdateKind kind, KeyModifiers modifiers)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(rawModifiers, kind);
        return new PointerPressedEventArgs(target, pointer, target, new Point(0, 0), 0, properties, modifiers);
    }

    private static Point GetCenterPoint(Control control, Visual relativeTo)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        return control.TranslatePoint(center, relativeTo) ?? center;
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, modifiers);
    }

    private static PointerEventArgs CreatePointerMovedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, modifiers);
    }

    private static PointerReleasedEventArgs CreatePointerReleasedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers)
    {
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        return new PointerReleasedEventArgs(source, pointer, root, position, 0, properties, modifiers, MouseButton.Left);
    }

    private static bool InvokeUpdateStateOnMouseLeftButtonDown(DataGrid grid, PointerPressedEventArgs args, int columnIndex, int slot, bool allowEdit)
    {
        return grid.UpdateStateOnMouseLeftButtonDown(args, columnIndex, slot, allowEdit);
    }

    private static bool InvokeUpdateStateOnMouseRightButtonDown(DataGrid grid, PointerPressedEventArgs args, int columnIndex, int slot, bool allowEdit)
    {
        return grid.UpdateStateOnMouseRightButtonDown(args, columnIndex, slot, allowEdit);
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

    private static void SetAnchorSlot(DataGrid grid, int slot)
    {
        var property = typeof(DataGrid).GetProperty("AnchorSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(grid, new object[] { slot });
            return;
        }

        var field = typeof(DataGrid).GetField("<AnchorSlot>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(grid, slot);
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

    private static KeyModifiers GetCtrlOrCmdModifier(Control target)
    {
        return KeyboardHelper.GetPlatformCtrlOrCmdKeyModifier(target);
    }

    private sealed class RowItem
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }
}
