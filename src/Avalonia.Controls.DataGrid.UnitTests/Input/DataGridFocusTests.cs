// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridFocusTests
{
    [Fact]
    public void IsFocusWithinDataGrid_Treats_LogicalDescendant_As_InternalFocus()
    {
        var grid = new DataGrid();
        var logicalHost = new Border();
        var popupEditor = new TextBox();

        SetLogicalParent(logicalHost, grid);
        SetLogicalParent(popupEditor, logicalHost);

        var containsFocus = grid.IsFocusWithinDataGrid(popupEditor, out var dataGridWillReceiveRoutedEvent);

        Assert.True(containsFocus);
        Assert.False(dataGridWillReceiveRoutedEvent);
    }

    [Fact]
    public void IsFocusWithinDataGrid_Breaks_LogicalParent_Cycles()
    {
        var grid = new DataGrid();
        var first = new Border();
        var second = new Border();

        SetLogicalParent(first, second);
        SetLogicalParent(second, first);

        var containsFocus = grid.IsFocusWithinDataGrid(first, out var dataGridWillReceiveRoutedEvent);

        Assert.False(containsFocus);
        Assert.True(dataGridWillReceiveRoutedEvent);
    }

    private static void SetLogicalParent(StyledElement element, StyledElement? parent)
    {
        var property = typeof(StyledElement).GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(element, parent);
    }
}
