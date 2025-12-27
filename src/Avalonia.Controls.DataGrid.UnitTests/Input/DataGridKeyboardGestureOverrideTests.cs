using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridKeyboardGestureOverrideTests
{
    [AvaloniaFact]
    public void KeyDown_Handler_Can_Block_BuiltIn_When_AfterHandlers_For_NonDirectionalKeys()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        SetDisplayedScrollingElements(grid, 1);

        var invoked = false;
        grid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.PageDown)
            {
                invoked = true;
                e.Handled = true;
            }
        };

        var handledArgs = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = Key.PageDown,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        grid.RaiseEvent(handledArgs);
        InvokeDataGridKeyDown(grid, handledArgs);

        Assert.True(invoked);
        Assert.Equal(0, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void DirectionalKeys_Run_Before_AfterHandlers()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var invoked = false;
        grid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Down)
            {
                invoked = true;
                e.Handled = true;
            }
        };

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = Key.Down,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        grid.RaiseEvent(args);

        Assert.False(invoked);
        Assert.True(args.Handled);
        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void BuiltIn_Still_Runs_When_Not_Handled_AfterHandlers()
    {
        var (grid, _) = CreateGrid();
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Route = InputElement.KeyDownEvent.RoutingStrategies,
            Key = Key.Down,
            Source = grid,
            KeyDeviceType = KeyDeviceType.Keyboard
        };

        InvokeDataGridKeyDown(grid, args);

        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_Replace_Default_Mapping()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveDown = new KeyGesture(Key.J)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.Down);
        Assert.Equal(0, grid.SelectedIndex);

        PressKey(grid, Key.J);
        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_Tab_Allows_Ctrl_When_Configured()
    {
        var (grid, _) = CreateGrid(columnCount: 3);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var ctrl = GetCtrlOrCmdModifier(grid);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            Tab = new KeyGesture(Key.T, ctrl)
        };

        PressKey(grid, Key.Tab);
        Assert.Equal(0, grid.CurrentColumnIndex);

        PressKey(grid, Key.T, ctrl);
        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_MoveUp_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveUp = new KeyGesture(Key.K)
        };
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        PressKey(grid, Key.Up);
        Assert.Equal(1, grid.SelectedIndex);

        PressKey(grid, Key.K);
        Assert.Equal(0, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_MoveLeftRight_Use_Custom_Gestures()
    {
        var (grid, _) = CreateGrid(columnCount: 3);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveLeft = new KeyGesture(Key.H),
            MoveRight = new KeyGesture(Key.L)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);

        PressKey(grid, Key.Left);
        Assert.Equal(1, grid.CurrentColumnIndex);

        PressKey(grid, Key.H);
        Assert.Equal(0, grid.CurrentColumnIndex);

        PressKey(grid, Key.L);
        Assert.Equal(1, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_MoveHomeEnd_Use_Custom_Gestures()
    {
        var (grid, _) = CreateGrid(columnCount: 3);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveHome = new KeyGesture(Key.G),
            MoveEnd = new KeyGesture(Key.M)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);

        PressKey(grid, Key.Home);
        Assert.Equal(1, grid.CurrentColumnIndex);

        PressKey(grid, Key.G);
        Assert.Equal(0, grid.CurrentColumnIndex);

        PressKey(grid, Key.M);
        Assert.Equal(2, grid.CurrentColumnIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_PageDown_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MovePageDown = new KeyGesture(Key.D)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        SetDisplayedScrollingElements(grid, 1);

        PressKey(grid, Key.PageDown);
        Assert.Equal(0, grid.SelectedIndex);

        PressKey(grid, Key.D);
        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_PageUp_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid(rowCount: 6);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MovePageUp = new KeyGesture(Key.U)
        };
        SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);
        SetDisplayedScrollingElements(grid, 1);

        PressKey(grid, Key.PageUp);
        Assert.Equal(2, grid.SelectedIndex);

        PressKey(grid, Key.U);
        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_Enter_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            Enter = new KeyGesture(Key.N)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.Enter);
        Assert.Equal(0, grid.SelectedIndex);

        PressKey(grid, Key.N);
        Assert.Equal(1, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_BeginEdit_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            BeginEdit = new KeyGesture(Key.B)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        PressKey(grid, Key.F2);
        Assert.Equal(-1, grid.EditingColumnIndex);

        PressKey(grid, Key.B);
        Assert.Equal(0, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_CancelEdit_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            CancelEdit = new KeyGesture(Key.Q)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        PressKey(grid, Key.Escape);
        Assert.NotEqual(-1, grid.EditingColumnIndex);

        PressKey(grid, Key.Q);
        Assert.Equal(-1, grid.EditingColumnIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_SelectAll_Uses_Custom_Gesture()
    {
        var (grid, items) = CreateGrid(selectionMode: DataGridSelectionMode.Extended);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            SelectAll = new KeyGesture(Key.S)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var ctrl = GetCtrlOrCmdModifier(grid);

        PressKey(grid, Key.A, ctrl);
        Assert.Equal(1, grid.SelectedItems.Count);

        PressKey(grid, Key.S);
        Assert.Equal(items.Count, grid.SelectedItems.Count);
    }

    [AvaloniaFact]
    public void GestureOverrides_Copy_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            Copy = new KeyGesture(Key.P),
            CopyAlternate = new KeyGesture(Key.O)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;

        var copyCount = 0;
        grid.CopyingRowClipboardContent += (_, _) => copyCount++;

        var ctrl = GetCtrlOrCmdModifier(grid);
        PressKey(grid, Key.C, ctrl);
        PressKey(grid, Key.Insert, ctrl);
        Assert.Equal(0, copyCount);

        PressKey(grid, Key.P);
        Assert.True(copyCount > 0);

        copyCount = 0;
        PressKey(grid, Key.O);
        Assert.True(copyCount > 0);
    }

    [AvaloniaFact]
    public void GestureOverrides_Delete_Uses_Custom_Gesture()
    {
        var (grid, items) = CreateGrid(canUserDeleteRows: true);
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            Delete = new KeyGesture(Key.X)
        };
        grid.SelectedIndex = 1;
        grid.UpdateLayout();

        PressKey(grid, Key.Delete);
        Assert.Equal(5, items.Count);

        PressKey(grid, Key.X);
        Assert.Equal(4, items.Count);
    }

    [AvaloniaFact]
    public void GestureOverrides_ExpandAll_Uses_Custom_Gesture()
    {
        var (grid, _) = CreateGrid(rowCount: 1, columnCount: 1);
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

        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            ExpandAll = new KeyGesture(Key.E)
        };

        Assert.False(adapter.NodeAt(0).IsExpanded);

        PressKey(grid, Key.Multiply);
        Assert.False(adapter.NodeAt(0).IsExpanded);

        PressKey(grid, Key.E);
        Assert.True(adapter.NodeAt(0).IsExpanded);
    }

    [AvaloniaFact]
    public void GestureOverrides_Disable_Action_With_KeyNone()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveDown = new KeyGesture(Key.None)
        };
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var args = PressKey(grid, Key.Down);

        Assert.False(args.Handled);
        Assert.Equal(0, grid.SelectedIndex);
    }

    [AvaloniaFact]
    public void GestureOverrides_Conflicting_Gestures_Use_First_Match()
    {
        var (grid, _) = CreateGrid();
        grid.KeyboardGestureOverrides = new DataGridKeyboardGestures
        {
            MoveUp = new KeyGesture(Key.G),
            MoveDown = new KeyGesture(Key.G)
        };
        SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

        PressKey(grid, Key.G);

        Assert.Equal(0, grid.SelectedIndex);
    }

    private static (DataGrid grid, ObservableCollection<InputRow> items) CreateGrid(
        int rowCount = 5,
        int columnCount = 3,
        DataGridSelectionMode selectionMode = DataGridSelectionMode.Single,
        DataGridSelectionUnit selectionUnit = DataGridSelectionUnit.FullRow,
        bool canUserDeleteRows = false)
    {
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
            CanUserDeleteRows = canUserDeleteRows,
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

    private static void SetDisplayedScrollingElements(DataGrid grid, int count)
    {
        var property = typeof(DataGrid).GetProperty("DisplayData", BindingFlags.Instance | BindingFlags.NonPublic);
        var displayData = property?.GetValue(grid);
        var elementsProperty = displayData?.GetType().GetProperty("NumTotallyDisplayedScrollingElements");
        elementsProperty?.SetValue(displayData, count);
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
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
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
