// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Reflection;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridFillHandleRangeTests
{
    [Fact]
    public void FillHandle_Shrinks_End_When_Target_Inside_Source()
    {
        var source = new DataGridCellRange(startRow: 0, endRow: 3, startColumn: 0, endColumn: 3);

        var grid = CreateGrid();
        var method = GetFillTargetRangeMethod();

        var result = (DataGridCellRange)method.Invoke(grid, new object[] { source, 1, 2 })!;

        Assert.Equal(new DataGridCellRange(1, 3, 2, 3), result);
    }

    [Fact]
    public void FillHandle_Uses_Anchor_For_Diagonal_Target()
    {
        var source = new DataGridCellRange(startRow: 2, endRow: 3, startColumn: 2, endColumn: 3);
        var grid = CreateGrid();
        var method = GetFillTargetRangeMethod();

        var result = (DataGridCellRange)method.Invoke(grid, new object[] { source, 1, 1 })!;

        Assert.Equal(new DataGridCellRange(1, 3, 1, 3), result);
    }

    [Fact]
    public void FillHandle_Extends_Target_Left_And_Down()
    {
        var source = new DataGridCellRange(startRow: 0, endRow: 1, startColumn: 2, endColumn: 3);
        var grid = CreateGrid();
        var method = GetFillTargetRangeMethod();

        var result = (DataGridCellRange)method.Invoke(grid, new object[] { source, 2, 0 })!;

        Assert.Equal(new DataGridCellRange(0, 2, 0, 3), result);
    }

    [Fact]
    public void FillHandle_Extends_Target_Above_And_Right()
    {
        var source = new DataGridCellRange(startRow: 2, endRow: 3, startColumn: 0, endColumn: 1);
        var grid = CreateGrid();
        var method = GetFillTargetRangeMethod();

        var result = (DataGridCellRange)method.Invoke(grid, new object[] { source, 0, 2 })!;

        Assert.Equal(new DataGridCellRange(0, 3, 0, 2), result);
    }

    private static MethodInfo GetFillTargetRangeMethod()
    {
        var method = typeof(DataGrid).GetMethod(
            "GetFillTargetRange",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        return method!;
    }

    private static DataGrid CreateGrid() => new DataGrid();
}
