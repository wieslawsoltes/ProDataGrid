using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
    public void MouseLeft_RowDetails_Collapses_On_Ctrl_Deselect()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
        grid.RowDetailsTemplate = new FuncDataTemplate<RowItem>((_, _) => new Border { Height = 24 });
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var ctrl = GetCtrlOrCmdModifier(grid);
        var slot = grid.SlotFromRowIndex(0);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid), columnIndex: 0, slot: slot, allowEdit: false);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.AreDetailsVisible);

        InvokeUpdateStateOnMouseLeftButtonDown(grid, CreateLeftPointerArgs(grid, ctrl), columnIndex: 0, slot: slot, allowEdit: false);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.False(row.AreDetailsVisible);
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
    public void SelectionDrag_Defers_Pointer_Capture_Until_Threshold()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[0];
        var startPoint = GetCenterPoint(cell, grid);
        var movePoint = startPoint + new Point(10, 0);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        cell.RaiseEvent(CreatePointerPressedArgs(cell, grid, pointer, startPoint, KeyModifiers.None));
        Assert.Null(pointer.Captured);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, movePoint, KeyModifiers.None));
        Assert.Same(grid, pointer.Captured);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, movePoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void RowHeaderSelectionDrag_Defers_Pointer_Capture_Until_Threshold()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var header = row.HeaderCell;
        var startPoint = GetCenterPoint(header, grid);
        var movePoint = startPoint + new Point(10, 0);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, startPoint, KeyModifiers.None));
        Assert.Null(pointer.Captured);

        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, movePoint, KeyModifiers.None));
        Assert.Same(grid, pointer.Captured);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, movePoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void RowHeader_Click_Does_Not_Select_When_CanUserSelectRows_False()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.CanUserSelectRows = false;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var header = row.HeaderCell;
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Empty(grid.SelectedItems.Cast<object>());
    }

    [AvaloniaFact]
    public void RowHeader_Click_Selects_Row_Cells_When_CellUnit()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var header = row.HeaderCell;
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Equal(grid.ColumnsInternal.Count, grid.SelectedCells.Count);
        Assert.Single(grid.SelectedItems.Cast<object>());
    }

    [AvaloniaFact]
    public void RowHeader_CtrlClick_Toggles_Selected_Row_Cells()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var header = row.HeaderCell;
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var ctrl = GetCtrlOrCmdModifier(grid);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Equal(grid.ColumnsInternal.Count, grid.SelectedCells.Count);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, ctrl));
        grid.UpdateLayout();

        Assert.Empty(grid.SelectedCells);
        Assert.Empty(grid.SelectedItems.Cast<object>());
    }

    [AvaloniaFact]
    public void RowHeader_Drag_Selects_Row_Cell_Range_When_CellUnit()
    {
        var (grid, _) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);
        Assert.True(startRow!.HasHeaderCell);
        Assert.True(endRow!.HasHeaderCell);

        var startHeader = startRow.HeaderCell;
        var endHeader = endRow.HeaderCell;
        var startPoint = GetCenterPoint(startHeader, grid);
        var endPoint = GetCenterPoint(endHeader, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startHeader.RaiseEvent(CreatePointerPressedArgs(startHeader, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        var expectedCells = grid.ColumnsInternal.Count * 3;
        Assert.Equal(expectedCells, grid.SelectedCells.Count);
        Assert.Equal(3, grid.SelectedItems.Count);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void ColumnHeader_Click_Selects_Column_When_Enabled()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[1];
        var header = GetColumnHeader(grid, column);
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Contains(column, grid.SelectedColumns);
        Assert.Equal(items.Count, grid.SelectedCells.Count);
    }

    [AvaloniaFact]
    public void ColumnHeader_Click_Then_Cell_Click_Clears_Header_Selected_PseudoClass()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[0];
        var header = GetColumnHeader(grid, column);
        var headerPoint = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, headerPoint, KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(((IPseudoClasses)header.Classes).Contains(":selected"));

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[1];
        var cellPoint = GetCenterPoint(cell, grid);
        cell.RaiseEvent(CreatePointerPressedArgs(cell, grid, pointer, cellPoint, KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.False(((IPseudoClasses)header.Classes).Contains(":selected"));
    }

    [AvaloniaFact]
    public void CellDragSelection_Works_With_SelectedCellsBinding()
    {
        var (grid, _) = CreateGrid(rowCount: 4, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrRowOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.SelectedCells = new AvaloniaList<DataGridCellInfo>();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[2];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startCell.RaiseEvent(CreatePointerPressedArgs(startCell, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        Assert.Equal(9, grid.SelectedCells.Count);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void CellDragSelection_Ignores_Touch_By_Default()
    {
        var (grid, _) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrRowOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        Assert.NotNull(startRow);

        var startCell = startRow!.Cells[0];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = new Point(startPoint.X + 60, startPoint.Y + 40);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, isPrimary: true);

        startCell.RaiseEvent(CreateTouchPointerPressedArgs(startCell, grid, pointer, startPoint));
        grid.RaiseEvent(CreateTouchPointerMovedArgs(grid, grid, pointer, endPoint));

        Assert.Empty(grid.SelectedCells);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void CellDragSelection_Allows_Touch_When_Enabled()
    {
        var (grid, _) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrRowOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.AllowTouchDragSelection = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startSlot = grid.SlotFromRowIndex(0);
        var endSlot = grid.SlotFromRowIndex(2);
        var startRow = grid.DisplayData.GetDisplayedElement(startSlot) as DataGridRow;
        var endRow = grid.DisplayData.GetDisplayedElement(endSlot) as DataGridRow;
        Assert.NotNull(startRow);
        Assert.NotNull(endRow);

        var startCell = startRow!.Cells[0];
        var endCell = endRow!.Cells[2];
        var startPoint = GetCenterPoint(startCell, grid);
        var endPoint = GetCenterPoint(endCell, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, isPrimary: true);

        startCell.RaiseEvent(CreateTouchPointerPressedArgs(startCell, grid, pointer, startPoint));
        grid.RaiseEvent(CreateTouchPointerMovedArgs(grid, grid, pointer, endPoint));

        Assert.Equal(9, grid.SelectedCells.Count);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void ColumnHeader_CtrlClick_Toggles_Selected_Column()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[0];
        var header = GetColumnHeader(grid, column);
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var ctrl = GetCtrlOrCmdModifier(grid);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Contains(column, grid.SelectedColumns);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, ctrl));
        grid.UpdateLayout();

        Assert.DoesNotContain(column, grid.SelectedColumns);
        Assert.Empty(grid.SelectedCells);
    }

    [AvaloniaFact]
    public void ColumnHeader_CtrlClick_Deselect_Preserves_RowHeader_Selection()
    {
        var (grid, _) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrRowOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.CanUserSelectRows = true;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var rowHeader = row.HeaderCell;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        rowHeader.RaiseEvent(CreatePointerPressedArgs(rowHeader, grid, pointer, GetCenterPoint(rowHeader, grid), KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[1];
        var columnHeader = GetColumnHeader(grid, column);
        var ctrl = GetCtrlOrCmdModifier(grid);
        columnHeader.RaiseEvent(CreatePointerPressedArgs(columnHeader, grid, pointer, GetCenterPoint(columnHeader, grid), ctrl));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(column, grid.SelectedColumns);

        columnHeader.RaiseEvent(CreatePointerPressedArgs(columnHeader, grid, pointer, GetCenterPoint(columnHeader, grid), ctrl));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(column, grid.SelectedColumns);
        Assert.Equal(grid.ColumnsInternal.Count, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == column.Index);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 1 && cell.ColumnIndex == column.Index);
    }

    [AvaloniaFact]
    public void RowHeader_CtrlClick_Deselect_Preserves_ColumnHeader_Selection()
    {
        var (grid, items) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrRowOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.CanUserSelectRows = true;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[1];
        var columnHeader = GetColumnHeader(grid, column);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        columnHeader.RaiseEvent(CreatePointerPressedArgs(columnHeader, grid, pointer, GetCenterPoint(columnHeader, grid), KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(column, grid.SelectedColumns);

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        Assert.True(row!.HasHeaderCell);

        var rowHeader = row.HeaderCell;
        var ctrl = GetCtrlOrCmdModifier(grid);
        rowHeader.RaiseEvent(CreatePointerPressedArgs(rowHeader, grid, pointer, GetCenterPoint(rowHeader, grid), ctrl));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        rowHeader.RaiseEvent(CreatePointerPressedArgs(rowHeader, grid, pointer, GetCenterPoint(rowHeader, grid), ctrl));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(column, grid.SelectedColumns);
        Assert.Equal(items.Count, grid.SelectedCells.Count);
        Assert.Contains(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == column.Index);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.RowIndex == 0 && cell.ColumnIndex == 0);
    }

    [AvaloniaFact]
    public void ColumnDragHandle_Press_Ignores_Modifiers_And_Selects_Single_Column()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.CanUserReorderColumns = true;
        grid.ColumnDragHandle = DataGridColumnDragHandle.DragHandle;
        grid.ColumnDragHandleVisible = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column0 = grid.ColumnsInternal[0];
        var column1 = grid.ColumnsInternal[1];
        var header0 = GetColumnHeader(grid, column0);
        var header1 = GetColumnHeader(grid, column1);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var ctrl = GetCtrlOrCmdModifier(grid);

        header0.RaiseEvent(CreatePointerPressedArgs(header0, grid, pointer, GetCenterPoint(header0, grid), KeyModifiers.None));
        header1.RaiseEvent(CreatePointerPressedArgs(header1, grid, pointer, GetCenterPoint(header1, grid), ctrl));
        grid.UpdateLayout();

        Assert.Equal(2, grid.SelectedColumns.Count);

        SetPointerOver(header1, true);
        header1.RaiseEvent(CreatePointerMovedArgs(header1, grid, pointer, GetCenterPoint(header1, grid), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        var dragGrip = GetDragGrip(header1);
        dragGrip.RaiseEvent(CreatePointerPressedArgs(dragGrip, grid, pointer, GetCenterPoint(dragGrip, grid), ctrl));
        grid.UpdateLayout();

        Assert.Single(grid.SelectedColumns);
        Assert.Equal(column1.DisplayIndex, grid.SelectedColumns[0].DisplayIndex);
    }

    [AvaloniaFact]
    public void ColumnHeader_Drag_Selects_Column_Cell_Range_When_Reorder_Disabled()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.CanUserReorderColumns = false;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startColumn = grid.ColumnsInternal[0];
        var endColumn = grid.ColumnsInternal[2];
        var startHeader = GetColumnHeader(grid, startColumn);
        var endHeader = GetColumnHeader(grid, endColumn);
        var startPoint = GetCenterPoint(startHeader, grid);
        var endPoint = GetCenterPoint(endHeader, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startHeader.RaiseEvent(CreatePointerPressedArgs(startHeader, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        var expectedCells = items.Count * 3;
        Assert.Equal(expectedCells, grid.SelectedCells.Count);
        Assert.Equal(3, grid.SelectedColumns.Count);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void ColumnHeader_Drag_Uses_DisplayIndex_Order()
    {
        var (grid, _) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.CanUserReorderColumns = false;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var columnA = grid.ColumnsInternal[0];
        var columnB = grid.ColumnsInternal[1];
        var columnC = grid.ColumnsInternal[2];

        columnC.DisplayIndex = 0;
        columnA.DisplayIndex = 1;
        columnB.DisplayIndex = 2;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startHeader = GetColumnHeader(grid, columnA);
        var endHeader = GetColumnHeader(grid, columnB);
        var startPoint = GetCenterPoint(startHeader, grid);
        var endPoint = GetCenterPoint(endHeader, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startHeader.RaiseEvent(CreatePointerPressedArgs(startHeader, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(columnA, grid.SelectedColumns);
        Assert.Contains(columnB, grid.SelectedColumns);
        Assert.DoesNotContain(columnC, grid.SelectedColumns);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.ColumnIndex == columnC.Index);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void ColumnHeader_ShiftClick_Uses_DisplayIndex_Order()
    {
        var (grid, _) = CreateGrid(rowCount: 3, columnCount: 3, selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var columnA = grid.ColumnsInternal[0];
        var columnB = grid.ColumnsInternal[1];
        var columnC = grid.ColumnsInternal[2];

        columnC.DisplayIndex = 0;
        columnA.DisplayIndex = 1;
        columnB.DisplayIndex = 2;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var headerA = GetColumnHeader(grid, columnA);
        var headerB = GetColumnHeader(grid, columnB);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var shift = KeyModifiers.Shift;

        headerA.RaiseEvent(CreatePointerPressedArgs(headerA, grid, pointer, GetCenterPoint(headerA, grid), KeyModifiers.None));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        headerB.RaiseEvent(CreatePointerPressedArgs(headerB, grid, pointer, GetCenterPoint(headerB, grid), shift));
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(columnA, grid.SelectedColumns);
        Assert.Contains(columnB, grid.SelectedColumns);
        Assert.DoesNotContain(columnC, grid.SelectedColumns);
        Assert.DoesNotContain(grid.SelectedCells, cell => cell.ColumnIndex == columnC.Index);
    }

    [AvaloniaFact]
    public void ColumnHeader_Drag_Selects_Column_Cell_Range_When_Reorder_Uses_Drag_Handle()
    {
        var (grid, items) = CreateGrid(rowCount: 4, selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.CanUserReorderColumns = true;
        grid.ColumnDragHandle = DataGridColumnDragHandle.DragHandle;
        grid.ColumnDragHandleVisible = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var startColumn = grid.ColumnsInternal[0];
        var endColumn = grid.ColumnsInternal[2];
        var startHeader = GetColumnHeader(grid, startColumn);
        var endHeader = GetColumnHeader(grid, endColumn);
        var startPoint = GetCenterPoint(startHeader, grid);
        var endPoint = GetCenterPoint(endHeader, grid);

        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        startHeader.RaiseEvent(CreatePointerPressedArgs(startHeader, grid, pointer, startPoint, KeyModifiers.None));
        grid.RaiseEvent(CreatePointerMovedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));

        var expectedCells = items.Count * 3;
        Assert.Equal(expectedCells, grid.SelectedCells.Count);
        Assert.Equal(3, grid.SelectedColumns.Count);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, grid, pointer, endPoint, KeyModifiers.None));
    }

    [AvaloniaFact]
    public void ColumnDragHandle_Visible_Only_On_Header_PointerOver()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrColumnHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserReorderColumns = true;
        grid.ColumnDragHandle = DataGridColumnDragHandle.DragHandle;
        grid.ColumnDragHandleVisible = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[0];
        var header = GetColumnHeader(grid, column);
        var dragGrip = GetDragGrip(header);
        var dragGripIcon = GetDragGripIcon(header);

        Assert.Equal(VerticalAlignment.Top, dragGrip.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Stretch, dragGrip.HorizontalAlignment);
        Assert.Equal(HorizontalAlignment.Center, dragGripIcon.HorizontalAlignment);
        Assert.False(dragGrip.IsVisible);

        SetPointerOver(header, true);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var point = GetCenterPoint(header, grid);
        header.RaiseEvent(CreatePointerMovedArgs(header, grid, pointer, point, KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Assert.True(dragGrip.IsVisible);
        Assert.True(dragGrip.Bounds.Width > dragGripIcon.Bounds.Width);

        SetPointerOver(header, false);

        Assert.False(dragGrip.IsVisible);
    }

    [AvaloniaFact]
    public void RowDragHandle_Visible_Only_On_RowHeader_PointerOver()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.RowHeader;
        grid.RowDragHandleVisible = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);
        var header = row!.HeaderCell;
        var dragGrip = GetDragGrip(header);
        var dragGripIcon = GetDragGripIcon(header);

        Assert.Equal(VerticalAlignment.Stretch, dragGrip.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Center, dragGrip.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, dragGripIcon.VerticalAlignment);
        Assert.False(dragGrip.IsVisible);

        SetPointerOver(header, true);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var point = GetCenterPoint(header, grid);
        header.RaiseEvent(CreatePointerMovedArgs(header, grid, pointer, point, KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        Assert.True(dragGrip.IsVisible);
        Assert.True(dragGrip.Bounds.Height > dragGripIcon.Bounds.Height);

        SetPointerOver(header, false);

        Assert.False(dragGrip.IsVisible);
    }

    [AvaloniaFact]
    public void RowDragHandle_Press_Ignores_Modifiers_And_Selects_Single_Row()
    {
        var (grid, items) = CreateGrid(selectionUnit: DataGridSelectionUnit.CellOrRowHeader, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.RowHeader;
        grid.RowDragHandleVisible = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var row0Slot = grid.SlotFromRowIndex(0);
        var row1Slot = grid.SlotFromRowIndex(1);
        var row0 = grid.DisplayData.GetDisplayedElement(row0Slot) as DataGridRow;
        var row1 = grid.DisplayData.GetDisplayedElement(row1Slot) as DataGridRow;
        Assert.NotNull(row0);
        Assert.NotNull(row1);

        var header0 = row0!.HeaderCell;
        var header1 = row1!.HeaderCell;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var ctrl = GetCtrlOrCmdModifier(grid);

        header0.RaiseEvent(CreatePointerPressedArgs(header0, grid, pointer, GetCenterPoint(header0, grid), KeyModifiers.None));
        header1.RaiseEvent(CreatePointerPressedArgs(header1, grid, pointer, GetCenterPoint(header1, grid), ctrl));
        grid.UpdateLayout();

        Assert.Equal(2, grid.SelectedItems.Count);

        SetPointerOver(header1, true);
        header1.RaiseEvent(CreatePointerMovedArgs(header1, grid, pointer, GetCenterPoint(header1, grid), KeyModifiers.None));
        Dispatcher.UIThread.RunJobs();

        var dragGrip = GetDragGrip(header1);
        dragGrip.RaiseEvent(CreatePointerPressedArgs(dragGrip, grid, pointer, GetCenterPoint(dragGrip, grid), ctrl));
        grid.UpdateLayout();

        Assert.Single(grid.SelectedItems);
        Assert.Contains(items[1], grid.SelectedItems.Cast<RowItem>());
    }

    [AvaloniaFact]
    public void ColumnHeader_Click_Does_Not_Select_When_SelectionUnit_Disallows_Columns()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.Cell, selectionMode: DataGridSelectionMode.Extended);
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.CanUserSelectColumns = true;
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var column = grid.ColumnsInternal[0];
        var header = GetColumnHeader(grid, column);
        var point = GetCenterPoint(header, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        header.RaiseEvent(CreatePointerPressedArgs(header, grid, pointer, point, KeyModifiers.None));
        grid.UpdateLayout();

        Assert.Empty(grid.SelectedColumns);
        Assert.Empty(grid.SelectedCells);
    }

    [AvaloniaFact]
    public void DoubleTapped_Raises_On_Unselected_Row()
    {
        var (grid, _) = CreateGrid(selectionUnit: DataGridSelectionUnit.FullRow, selectionMode: DataGridSelectionMode.Extended);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var slot = grid.SlotFromRowIndex(1);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[0];
        var point = GetCenterPoint(cell, grid);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        var doubleTapped = 0;
        row.DoubleTapped += (_, _) => doubleTapped++;

        cell.RaiseEvent(CreatePointerPressedArgs(cell, grid, pointer, point, KeyModifiers.None, clickCount: 1));
        cell.RaiseEvent(CreatePointerReleasedArgs(cell, grid, pointer, point, KeyModifiers.None));

        cell.RaiseEvent(CreatePointerPressedArgs(cell, grid, pointer, point, KeyModifiers.None, clickCount: 2));
        cell.RaiseEvent(CreatePointerReleasedArgs(cell, grid, pointer, point, KeyModifiers.None));

        Assert.Equal(1, doubleTapped);
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

    private static DataGridColumnHeader GetColumnHeader(DataGrid grid, DataGridColumn column)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridColumnHeader>()
            .First(header => ReferenceEquals(header.OwningColumn, column));
    }

    private static Control GetDragGrip(Control header)
    {
        return header.GetVisualDescendants()
            .OfType<Control>()
            .First(control => control.Name == "DragGrip");
    }

    private static Path GetDragGripIcon(Control header)
    {
        return header.GetVisualDescendants()
            .OfType<Path>()
            .First(path => path.Name == "DragGripIcon");
    }

    private static void SetPointerOver(Control target, bool isOver)
    {
        if (target.Classes is IPseudoClasses pseudoClasses)
        {
            if (isOver)
            {
                pseudoClasses.Add(":pointerover");
            }
            else
            {
                pseudoClasses.Remove(":pointerover");
            }
        }
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers, int clickCount = 1)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, modifiers, clickCount);
    }

    private static PointerPressedEventArgs CreateTouchPointerPressedArgs(Control source, Visual root, IPointer pointer, Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, clickCount: 1);
    }

    private static PointerEventArgs CreatePointerMovedArgs(Control source, Visual root, IPointer pointer, Point position, KeyModifiers modifiers)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other);
        return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, modifiers);
    }

    private static PointerEventArgs CreateTouchPointerMovedArgs(Control source, Visual root, IPointer pointer, Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
        return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, root, position, 0, properties, KeyModifiers.None);
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
