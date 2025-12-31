// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridKeyboardHelperGuardTests
{
    [AvaloniaFact]
    public void PointerPressed_On_Detached_Cell_Does_Not_Throw()
    {
        var items = new ObservableCollection<RowItem>
        {
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        };

        var window = new Window
        {
            Width = 400,
            Height = 200
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsTabStop = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        var slot = grid.SlotFromRowIndex(0);
        var row = grid.DisplayData.GetDisplayedElement(slot) as DataGridRow;
        Assert.NotNull(row);

        var cell = row!.Cells[0];
        Assert.NotNull(cell);

        window.Content = null;
        Assert.Null(TopLevel.GetTopLevel(cell));

        var args = CreateLeftPointerArgs(cell);

        var exception = Record.Exception(() => cell.RaiseEvent(args));

        Assert.Null(exception);

        window.Close();
    }

    private static PointerPressedEventArgs CreateLeftPointerArgs(Control target)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(target, pointer, target, new Point(2, 2), 0, properties, KeyModifiers.None);
    }

    private class RowItem
    {
        public string Name { get; set; } = string.Empty;
    }
}
