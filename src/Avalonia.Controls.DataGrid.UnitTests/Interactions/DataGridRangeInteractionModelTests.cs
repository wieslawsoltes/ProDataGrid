// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridInteractions;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Interactions;

public class DataGridRangeInteractionModelTests
{
    [Fact]
    public void DragThreshold_Uses_Default_Values()
    {
        var model = new DataGridRangeInteractionModel();

        Assert.False(model.IsSelectionDragThresholdMet(new Point(0, 0), new Point(3, 0)));
        Assert.True(model.IsSelectionDragThresholdMet(new Point(0, 0), new Point(4, 0)));
    }

    [Fact]
    public void ResolveSelectionAnchor_Uses_Existing_When_Valid()
    {
        var model = new DataGridRangeInteractionModel();
        var grid = new DataGrid();

        var anchor = model.ResolveSelectionAnchor(new DataGridSelectionAnchorContext(
            grid,
            new DataGridCellPosition(1, 2),
            new DataGridCellPosition(5, 6),
            KeyModifiers.None));

        Assert.Equal(new DataGridCellPosition(1, 2), anchor);
    }

    [Fact]
    public void ResolveSelectionAnchor_Falls_Back_To_Current_When_Invalid()
    {
        var model = new DataGridRangeInteractionModel();
        var grid = new DataGrid();

        var anchor = model.ResolveSelectionAnchor(new DataGridSelectionAnchorContext(
            grid,
            new DataGridCellPosition(-1, -1),
            new DataGridCellPosition(3, 4),
            KeyModifiers.None));

        Assert.Equal(new DataGridCellPosition(3, 4), anchor);
    }

    [Fact]
    public void BuildFillHandleRange_Anchors_From_BottomRight()
    {
        var model = new DataGridRangeInteractionModel();
        var grid = new DataGrid();
        var source = new DataGridCellRange(startRow: 0, endRow: 3, startColumn: 0, endColumn: 3);

        var range = model.BuildFillHandleRange(new DataGridFillHandleRangeContext(
            grid,
            source,
            new DataGridCellPosition(1, 2)));

        Assert.Equal(new DataGridCellRange(1, 3, 2, 3), range);
    }

    [Fact]
    public void AutoScrollDirection_Tracks_Presenter_Bounds()
    {
        var model = new DataGridRangeInteractionModel();
        var grid = new DataGrid();

        var direction = model.GetAutoScrollDirection(new DataGridAutoScrollContext(
            grid,
            pointerPosition: new Point(5, -2),
            rowsPresenterPoint: new Point(5, -2),
            rowsPresenterSize: new Size(100, 100),
            rowHeaderWidth: 10,
            cellsWidth: 100,
            isRowSelection: false));

        Assert.Equal(-1, direction.Horizontal);
        Assert.Equal(-1, direction.Vertical);
    }
}
