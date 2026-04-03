// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.VisualTree;
using Xunit;
using AvaloniaDragDrop = Avalonia.Input.DragDrop;

namespace Avalonia.Controls.DataGridTests.DragDrop;

    public class DataGridRowDragDropControllerTests
    {
    [AvaloniaFact]
    public void Row_DragHandle_Allows_Header_Start()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.Row;
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var header = grid.GetVisualDescendants().OfType<DataGridRowHeader>().First();
        var point = header.TranslatePoint(new Point(1, 1), grid) ?? new Point(1, 1);

        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var data = new DataObject();
        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DragOverEvent,
            data,
            grid,
            point,
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DragOverEvent,
            Source = grid
        };

        var dropArgs = InvokeCreateDropArgs(controller, dragInfo, dragEvent, DragDropEffects.Move);
        Assert.NotNull(dropArgs);
        window.Close();
    }

    [AvaloniaFact]
    public void Pointer_On_ScrollBar_Does_Not_Start_Drag()
    {
        var items = Enumerable.Range(0, 100)
            .Select(i => new RowItem($"Item {i}"))
            .ToList();
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.Row;
        grid.UpdateLayout();

        var scrollBar = grid.GetVisualDescendants().OfType<ScrollBar>()
            .FirstOrDefault(sb => sb.Orientation == Orientation.Vertical);
        Assert.NotNull(scrollBar);

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(
            scrollBar!,
            pointer,
            window,
            new Point(1, 1),
            0,
            properties,
            KeyModifiers.None);

        scrollBar!.RaiseEvent(args);

        var pointerIdField = typeof(DataGridRowDragDropController).GetField("_pointerId", BindingFlags.NonPublic | BindingFlags.Instance);
        var pointerId = (int?)pointerIdField!.GetValue(controller);
        Assert.Null(pointerId);

        window.Close();
    }

    [AvaloniaFact]
    public void PointerPressed_Defers_Capture_For_Row_Drag()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.Row;
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var row = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var point = row.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);

        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        row.RaiseEvent(CreatePointerPressedArgs(row, window, pointer, point));

        Assert.Null(pointer.Captured);
        Assert.True(GetCapturePending(controller));

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, window, pointer, point));
        Assert.False(GetCapturePending(controller));

        window.Close();
    }

    [AvaloniaFact]
    public void PointerPressed_On_Selected_Row_Uses_MultiSelection_For_DragInfo_When_Suppression_Is_Enabled()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C"),
            new("D")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.RowDragHandle = DataGridRowDragHandle.Row;
        var options = new DataGridRowDragDropOptions
        {
            SuppressSelectionDragFromDragHandle = true
        };
        grid.RowDragDropOptions = options;
        grid.SelectedItems.Add(items[1]);
        grid.SelectedItems.Add(items[2]);
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, options);

        var row = grid.GetVisualDescendants().OfType<DataGridRow>().Skip(1).First();
        Assert.True(row.IsSelected);

        var cell = row.Cells[0];
        var point = cell.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);
        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

        cell.RaiseEvent(CreatePointerPressedArgs(cell, window, pointer, point));

        var dragInfo = InvokeTryCreateDragInfo(controller);

        Assert.NotNull(dragInfo);
        Assert.True(dragInfo!.FromSelection);
        Assert.Collection(
            dragInfo.Items,
            item => Assert.Same(items[1], item),
            item => Assert.Same(items[2], item));
        Assert.Equal(new[] { 1, 2 }, dragInfo.Indices);

        grid.RaiseEvent(CreatePointerReleasedArgs(grid, window, pointer, point));
        window.Close();
    }

    [AvaloniaFact]
    public void RowHeader_DragHandle_Does_Not_Start_From_Cell()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.HeadersVisibility = DataGridHeadersVisibility.All;
        grid.RowHeaderWidth = 28;
        grid.RowDragHandle = DataGridRowDragHandle.RowHeader;
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var row = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var cell = row.Cells[0];
        var point = cell.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);

        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        cell.RaiseEvent(CreatePointerPressedArgs(cell, window, pointer, point));

        var pointerIdField = typeof(DataGridRowDragDropController).GetField("_pointerId", BindingFlags.NonPublic | BindingFlags.Instance);
        var pointerId = (int?)pointerIdField!.GetValue(controller);
        Assert.Null(pointerId);

        window.Close();
    }

    [AvaloniaFact]
    public void Drop_Reorders_Items()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.CanUserAddRows = false;

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var dropArgs = CreateDropArgs(
            grid,
            items,
            new List<object> { items[0] },
            new List<int> { 0 },
            targetItem: items[2],
            position: DataGridRowDropPosition.After);

        Assert.True(handler.Execute(dropArgs));
        InvokeUpdateSelection(controller, dropArgs);

        Assert.Equal(new[] { "B", "C", "A" }, items.Select(x => x.Value));
        window.Close();
    }

    [AvaloniaFact]
    public void Drop_Multiple_Selected_Rows_Reorders_And_Keeps_Selection()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C"),
            new("D")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.CanUserAddRows = false;

        var b = items[1];
        var c = items[2];
        grid.SelectedItems.Add(b);
        grid.SelectedItems.Add(c);

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var dropArgs = CreateDropArgs(
            grid,
            items,
            new List<object> { b, c },
            new List<int> { 1, 2 },
            targetItem: items[0],
            position: DataGridRowDropPosition.Before);

        Assert.True(handler.Execute(dropArgs));
        InvokeUpdateSelection(controller, dropArgs);

        Assert.Equal(new[] { "B", "C", "A", "D" }, items.Select(x => x.Value));
        Assert.Collection(grid.SelectedItems.Cast<object>(),
            i => Assert.Same(b, i),
            i => Assert.Same(c, i));
        window.Close();
    }

    [AvaloniaFact]
    public void Drop_Onto_Placeholder_Is_Ignored()
    {
        var items = new DataGridCollectionView(new ObservableCollection<PlaceholderItem>
        {
            new() { Value = "A" },
            new() { Value = "B" }
        });
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.CanUserAddRows = true;

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var dropArgs = CreateDropArgs(
            grid,
            (IList)items,
            new List<object> { items[0] },
            new List<int> { 0 },
            targetItem: DataGridCollectionView.NewItemPlaceholder,
            position: DataGridRowDropPosition.Before);

        Assert.False(handler.Validate(dropArgs));
        Assert.False(handler.Execute(dropArgs));
        InvokeUpdateSelection(controller, dropArgs);

        Assert.Equal(new[] { "A", "B" }, items.Cast<PlaceholderItem>().Select(x => x.Value));
        window.Close();
    }

    [AvaloniaFact]
    public void DragOver_On_Dragged_Row_Produces_DropArgs()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var firstRow = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var rowPoint = firstRow.TranslatePoint(new Point(1, 1), grid) ?? new Point(1, 1);

        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var data = new DataObject();
        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DragOverEvent,
            data,
            grid,
            rowPoint,
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DragOverEvent,
            Source = grid
        };

        var dropArgs = InvokeCreateDropArgs(controller, dragInfo, dragEvent, DragDropEffects.Move);

        Assert.NotNull(dropArgs);
        Assert.Equal(0, dropArgs!.InsertIndex);
        Assert.Equal(firstRow, dropArgs.TargetRow);
        Assert.True(handler.Validate(dropArgs));
        window.Close();
    }

    [AvaloniaFact]
    public void DragOver_Above_First_Row_Produces_Before_Target()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        var handler = new DataGridRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var firstRow = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var top = firstRow.TranslatePoint(new Point(0, 0), grid) ?? new Point(0, 0);
        var abovePoint = new Point(top.X + 1, Math.Max(0, top.Y - 2));

        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[1] }, new List<int> { 1 }, fromSelection: false);
        var data = new DataObject();
        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DragOverEvent,
            data,
            grid,
            abovePoint,
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DragOverEvent,
            Source = grid
        };

        var dropArgs = InvokeCreateDropArgs(controller, dragInfo, dragEvent, DragDropEffects.Move);

        Assert.NotNull(dropArgs);
        Assert.Equal(DataGridRowDropPosition.Before, dropArgs!.Position);
        Assert.Equal(0, dropArgs.InsertIndex);
        Assert.Equal(firstRow, dropArgs.TargetRow);
        Assert.True(handler.Validate(dropArgs));
        window.Close();
    }

    [AvaloniaFact]
    public void RowHeader_Does_Not_Toggle_Hierarchical_Node_When_Reorder_Is_Enabled()
    {
        var (grid, window, model, root) = CreateHierarchicalGrid();
        grid.UpdateLayout();

        Assert.False(root.IsExpanded);

        var header = grid.GetVisualDescendants().OfType<DataGridRowHeader>().First();
        var pointer = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var args = new PointerPressedEventArgs(
            header,
            pointer,
            window,
            new Point(1, 1),
            0,
            properties,
            KeyModifiers.None);

        header.RaiseEvent(args);

        Assert.False(root.IsExpanded);
        window.Close();
    }

    [AvaloniaFact]
    public void Hierarchical_DragOver_Middle_Uses_Inside_Position()
    {
        var (grid, window, model, root) = CreateHierarchicalGrid();
        grid.CanUserAddRows = false;
        root.IsExpanded = true;
        grid.UpdateLayout();

        var handler = new DataGridHierarchicalRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var firstRow = grid.GetVisualDescendants().OfType<DataGridRow>().First();
        var point = firstRow.TranslatePoint(new Point(firstRow.Bounds.Width / 2, firstRow.Bounds.Height * 0.5), grid) ?? new Point(1, 1);

        var dragged = firstRow.DataContext!;
        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { dragged }, new List<int> { firstRow.Index }, fromSelection: false);
        var data = new DataObject();
        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DragOverEvent,
            data,
            grid,
            point,
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DragOverEvent,
            Source = grid
        };

        var dropArgs = InvokeCreateDropArgs(controller, dragInfo, dragEvent, DragDropEffects.Move);

        Assert.NotNull(dropArgs);
        Assert.Equal(DataGridRowDropPosition.Inside, dropArgs!.Position);
        window.Close();
    }

    [AvaloniaFact]
    public void Hierarchical_DragInfo_Uses_Nodes_From_Selection()
    {
        var (grid, window, model, rootItem) = CreateHierarchicalSelectionGrid();

        var childA = rootItem.Children[0];
        var childB = rootItem.Children[1];

        grid.SelectedItems.Add(childA);
        grid.SelectedItems.Add(childB);
        grid.UpdateLayout();

        Assert.All(grid.SelectedItems.Cast<object>(), item => Assert.IsType<TreeNode>(item));

        var handler = new DataGridHierarchicalRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var row = FindRowForItem(grid, childA);
        SetDragStartRow(controller, row);

        var dragInfo = InvokeTryCreateDragInfo(controller);

        Assert.NotNull(dragInfo);
        Assert.True(dragInfo!.FromSelection);
        Assert.All(dragInfo.Items, item => Assert.IsType<HierarchicalNode>(item));

        var expectedIndices = dragInfo.Items
            .Cast<HierarchicalNode>()
            .Select(model.IndexOf)
            .ToList();

        Assert.Equal(expectedIndices, dragInfo.Indices);

        window.Close();
    }

    [AvaloniaFact]
    public void Hierarchical_Drop_Reorders_Selected_Nodes_From_DragInfo()
    {
        var (grid, window, model, rootItem) = CreateHierarchicalSelectionGrid();

        var childA = rootItem.Children[0];
        var childB = rootItem.Children[1];
        var childC = rootItem.Children[2];

        grid.SelectedItems.Add(childA);
        grid.SelectedItems.Add(childB);
        grid.UpdateLayout();

        var handler = new DataGridHierarchicalRowReorderHandler();
        using var controller = new DataGridRowDragDropController(grid, handler, new DataGridRowDragDropOptions());

        var row = FindRowForItem(grid, childA);
        SetDragStartRow(controller, row);

        var dragInfo = InvokeTryCreateDragInfo(controller);
        Assert.NotNull(dragInfo);

        var targetNode = FindNode(model, childC);
        var targetIndex = model.IndexOf(targetNode);
        var insertIndex = targetIndex + 1;

        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DropEvent,
            new DataTransfer(),
            grid,
            new Avalonia.Point(),
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DropEvent,
            Source = grid
        };

        var dropArgs = new DataGridRowDropEventArgs(
            grid,
            grid.ItemsSource as IList,
            dragInfo!.Items,
            dragInfo.Indices,
            targetNode,
            targetIndex,
            insertIndex,
            targetRow: null,
            DataGridRowDropPosition.After,
            isSameGrid: true,
            DragDropEffects.Move,
            dragEvent);

        Assert.True(handler.Validate(dropArgs));
        Assert.True(handler.Execute(dropArgs));

        Assert.Equal(new[] { "C", "A", "B" }, rootItem.Children.Select(x => x.Name).ToArray());

        window.Close();
    }

    [AvaloniaFact]
    public void TryPrepareSessionUpdate_Populates_Session_And_Raises_RowDragUpdated()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        var handler = new RecordingDropHandler();
        using var controller = new DataGridRowDragDropController(
            grid,
            handler,
            new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy
            });

        var targetRow = grid.GetVisualDescendants().OfType<DataGridRow>().Skip(1).First();
        var point = targetRow.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);
        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        var dragEvent = CreateDragEventArgs(AvaloniaDragDrop.DragOverEvent, grid, point, KeyModifiers.Control);
        DataGridRowDragUpdatedEventArgs? updatedArgs = null;
        var updatedRequestedEffect = DragDropEffects.None;
        var updatedEffectiveEffect = DragDropEffects.None;
        var updatedIsValidTarget = false;
        grid.RowDragUpdated += (_, e) =>
        {
            updatedArgs = e;
            updatedRequestedEffect = e.Session.RequestedEffect;
            updatedEffectiveEffect = e.Session.EffectiveEffect;
            updatedIsValidTarget = e.Session.IsValidTarget;
        };

        var valid = InvokeTryPrepareSessionUpdate(controller, dragInfo, dragEvent, out var updatedSession, out var dropArgs);

        Assert.True(valid);
        Assert.Same(session, updatedSession);
        Assert.Same(session, grid.ActiveRowDragSession);
        Assert.NotNull(dropArgs);
        Assert.Same(session, dropArgs!.Session);
        Assert.Equal(DragDropEffects.Copy, dropArgs.RequestedEffect);
        Assert.Equal(DragDropEffects.Copy, dropArgs.EffectiveEffect);
        Assert.Equal(DragDropEffects.Copy, session.RequestedEffect);
        Assert.Equal(DragDropEffects.Copy, session.EffectiveEffect);
        Assert.True(session.IsValidTarget);
        Assert.Equal(DragDropEffects.Copy, updatedRequestedEffect);
        Assert.Equal(DragDropEffects.Copy, updatedEffectiveEffect);
        Assert.True(updatedIsValidTarget);
        Assert.Equal(point, session.PointerPosition);
        Assert.Equal(KeyModifiers.Control, session.KeyModifiers);
        Assert.Same(targetRow, session.HoveredRow);
        Assert.Same(targetRow.DataContext, session.HoveredItem);
        Assert.Same(targetRow, session.TargetRow);
        Assert.Same(dropArgs.TargetItem, session.TargetItem);
        Assert.Equal(dropArgs.InsertIndex, session.InsertIndex);
        Assert.Equal(dropArgs.TargetIndex, session.TargetIndex);
        Assert.Equal(dropArgs.Position, session.DropPosition);
        Assert.NotNull(updatedArgs);
        Assert.Same(session, updatedArgs!.Session);
        Assert.Same(dragEvent, updatedArgs.DragEventArgs);
        Assert.Single(handler.ValidateCalls);
        Assert.Same(session, handler.ValidateCalls[0].Session);
        Assert.Equal(DragDropEffects.Copy, handler.ValidateCalls[0].RequestedEffect);
        Assert.Equal(DragDropEffects.Copy, handler.ValidateCalls[0].EffectiveEffect);

        window.Close();
    }

    [AvaloniaFact]
    public void TryPrepareSessionUpdate_Allows_Handler_To_Override_EffectiveEffect()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        var handler = new RecordingDropHandler
        {
            ValidateCallback = args =>
            {
                args.EffectiveEffect = DragDropEffects.Move;
                return true;
            }
        };

        using var controller = new DataGridRowDragDropController(
            grid,
            handler,
            new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy
            });

        var targetRow = grid.GetVisualDescendants().OfType<DataGridRow>().Skip(2).First();
        var point = targetRow.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);
        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        var dragEvent = CreateDragEventArgs(AvaloniaDragDrop.DragOverEvent, grid, point, KeyModifiers.Control);

        var valid = InvokeTryPrepareSessionUpdate(controller, dragInfo, dragEvent, out var updatedSession, out var dropArgs);

        Assert.True(valid);
        Assert.Same(session, updatedSession);
        Assert.NotNull(dropArgs);
        Assert.Equal(DragDropEffects.Copy, dropArgs!.RequestedEffect);
        Assert.Equal(DragDropEffects.Move, dropArgs.EffectiveEffect);
        Assert.Equal(DragDropEffects.Copy, session.RequestedEffect);
        Assert.Equal(DragDropEffects.Move, session.EffectiveEffect);
        Assert.True(session.IsValidTarget);

        window.Close();
    }

    [AvaloniaFact]
    public void TryPrepareSessionUpdate_InvalidTarget_Sets_Effect_To_None()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        var handler = new RecordingDropHandler
        {
            ValidateCallback = args =>
            {
                if (args.Session != null)
                {
                    args.Session.FeedbackCaption = "Protected lane";
                }

                return false;
            }
        };

        using var controller = new DataGridRowDragDropController(
            grid,
            handler,
            new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy
            });

        var targetRow = grid.GetVisualDescendants().OfType<DataGridRow>().Skip(1).First();
        var point = targetRow.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);
        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        var dragEvent = CreateDragEventArgs(AvaloniaDragDrop.DragOverEvent, grid, point, KeyModifiers.Control);

        var valid = InvokeTryPrepareSessionUpdate(controller, dragInfo, dragEvent, out var updatedSession, out var dropArgs);

        Assert.False(valid);
        Assert.Same(session, updatedSession);
        Assert.NotNull(dropArgs);
        Assert.Equal(DragDropEffects.Copy, session.RequestedEffect);
        Assert.Equal(DragDropEffects.None, session.EffectiveEffect);
        Assert.False(session.IsValidTarget);
        Assert.Equal("Protected lane", session.FeedbackCaption);
        Assert.Equal(DragDropEffects.Copy, dropArgs!.RequestedEffect);
        Assert.Equal(DragDropEffects.None, dropArgs.EffectiveEffect);
        Assert.Same(session, grid.ActiveRowDragSession);

        window.Close();
    }

    [AvaloniaFact]
    public void TryPrepareSessionUpdate_DefaultReorderHandler_Rejects_CopyEffect()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B"),
            new("C")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;
        grid.UpdateLayout();

        using var controller = new DataGridRowDragDropController(
            grid,
            new DataGridRowReorderHandler(),
            new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy
            });

        var targetRow = grid.GetVisualDescendants().OfType<DataGridRow>().Skip(1).First();
        var point = targetRow.TranslatePoint(new Point(2, 2), grid) ?? new Point(2, 2);
        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var dragInfo = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        var dragEvent = CreateDragEventArgs(AvaloniaDragDrop.DragOverEvent, grid, point, KeyModifiers.Control);

        var valid = InvokeTryPrepareSessionUpdate(controller, dragInfo, dragEvent, out var updatedSession, out var dropArgs);

        Assert.False(valid);
        Assert.Same(session, updatedSession);
        Assert.NotNull(dropArgs);
        Assert.Equal(DragDropEffects.Copy, dropArgs!.RequestedEffect);
        Assert.Equal(DragDropEffects.None, dropArgs.EffectiveEffect);
        Assert.Equal(DragDropEffects.Copy, session.RequestedEffect);
        Assert.Equal(DragDropEffects.None, session.EffectiveEffect);
        Assert.False(session.IsValidTarget);

        window.Close();
    }

    [AvaloniaFact]
    public void FinishDrag_Canceled_Raises_Only_RowDragCanceled()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;

        using var controller = new DataGridRowDragDropController(grid, new DataGridRowReorderHandler(), new DataGridRowDragDropOptions());

        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var info = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        using var data = new DataTransfer();

        DataGridRowDragCanceledEventArgs? canceledArgs = null;
        DataGridRowDragCompletedEventArgs? completedArgs = null;
        grid.RowDragCanceled += (_, e) => canceledArgs = e;
        grid.RowDragCompleted += (_, e) => completedArgs = e;

        InvokeFinishDrag(controller, info, data, session, DragDropEffects.None);

        Assert.NotNull(canceledArgs);
        Assert.Same(session, canceledArgs!.Session);
        Assert.Null(completedArgs);
        Assert.True(session.IsCanceled);
        Assert.Equal(DragDropEffects.None, session.ResultEffect);

        window.Close();
    }

    [AvaloniaFact]
    public void FinishDrag_Completed_Raises_Only_RowDragCompleted()
    {
        var items = new ObservableCollection<RowItem>
        {
            new("A"),
            new("B")
        };
        var (grid, window) = CreateGrid(items);
        grid.CanUserReorderRows = true;

        using var controller = new DataGridRowDragDropController(grid, new DataGridRowReorderHandler(), new DataGridRowDragDropOptions());

        var session = new DataGridRowDragSession(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false);
        var info = new DataGridRowDragInfo(grid, new List<object> { items[0] }, new List<int> { 0 }, fromSelection: false, session);
        using var data = new DataTransfer();

        DataGridRowDragCanceledEventArgs? canceledArgs = null;
        DataGridRowDragCompletedEventArgs? completedArgs = null;
        grid.RowDragCanceled += (_, e) => canceledArgs = e;
        grid.RowDragCompleted += (_, e) => completedArgs = e;

        InvokeFinishDrag(controller, info, data, session, DragDropEffects.Move);

        Assert.Null(canceledArgs);
        Assert.NotNull(completedArgs);
        Assert.Same(session, completedArgs!.Session);
        Assert.Equal(DragDropEffects.Move, completedArgs.Effect);
        Assert.False(session.IsCanceled);
        Assert.Equal(DragDropEffects.Move, session.ResultEffect);

        window.Close();
    }

    private static DataGridRowDropEventArgs CreateDropArgs(
        DataGrid grid,
        IList list,
        IReadOnlyList<object> items,
        IReadOnlyList<int> indices,
        object? targetItem,
        DataGridRowDropPosition position)
    {
        var data = new DataTransfer();
        var dragEvent = new DragEventArgs(
            AvaloniaDragDrop.DropEvent,
            data,
            grid,
            new Avalonia.Point(),
            KeyModifiers.None)
        {
            RoutedEvent = AvaloniaDragDrop.DropEvent,
            Source = grid
        };

        var targetIndex = targetItem != null ? list.IndexOf(targetItem) : list.Count;
        if (targetIndex < 0)
        {
            targetIndex = list.Count;
        }

        var insertIndex = position switch
        {
            DataGridRowDropPosition.After => Math.Clamp(targetIndex + 1, 0, list.Count),
            DataGridRowDropPosition.Inside => list.Count,
            _ => Math.Clamp(targetIndex, 0, list.Count)
        };

        return new DataGridRowDropEventArgs(
            grid,
            list,
            items,
            indices,
            targetItem,
            targetIndex,
            insertIndex,
            targetRow: null,
            position,
            isSameGrid: true,
            DragDropEffects.Move,
            dragEvent);
    }

    private static void InvokeUpdateSelection(DataGridRowDragDropController controller, DataGridRowDropEventArgs args)
    {
        var method = typeof(DataGridRowDragDropController).GetMethod("UpdateSelectionAfterDrop", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(controller, new object[] { args });
    }

    private static DataGridRowDropEventArgs? InvokeCreateDropArgs(
        DataGridRowDragDropController controller,
        DataGridRowDragInfo info,
        DragEventArgs e,
        DragDropEffects effects)
    {
        var method = typeof(DataGridRowDragDropController).GetMethod("CreateDropArgs", BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(controller, new object[] { info, e, effects }) as DataGridRowDropEventArgs;
    }

    private static DataGridRowDragInfo? InvokeTryCreateDragInfo(DataGridRowDragDropController controller)
    {
        var method = typeof(DataGridRowDragDropController).GetMethod("TryCreateDragInfo", BindingFlags.NonPublic | BindingFlags.Instance);
        return method?.Invoke(controller, Array.Empty<object>()) as DataGridRowDragInfo;
    }

    private static bool InvokeTryPrepareSessionUpdate(
        DataGridRowDragDropController controller,
        DataGridRowDragInfo info,
        DragEventArgs e,
        out DataGridRowDragSession session,
        out DataGridRowDropEventArgs? dropArgs)
    {
        var method = typeof(DataGridRowDragDropController).GetMethod("TryPrepareSessionUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        var arguments = new object?[] { info, e, null, null };
        var result = (bool)(method?.Invoke(controller, arguments) ?? false);
        session = (DataGridRowDragSession)arguments[2]!;
        dropArgs = arguments[3] as DataGridRowDropEventArgs;
        return result;
    }

    private static void SetDragStartRow(DataGridRowDragDropController controller, DataGridRow row)
    {
        var field = typeof(DataGridRowDragDropController).GetField("_dragStartRow", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(controller, row);
    }

    private static void InvokeFinishDrag(
        DataGridRowDragDropController controller,
        DataGridRowDragInfo info,
        IDataTransfer data,
        DataGridRowDragSession session,
        DragDropEffects result)
    {
        var method = typeof(DataGridRowDragDropController).GetMethod("FinishDrag", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(controller, new object[] { info, data, session, result });
    }

    private static bool GetCapturePending(DataGridRowDragDropController controller)
    {
        var field = typeof(DataGridRowDragDropController).GetField("_capturePending", BindingFlags.NonPublic | BindingFlags.Instance);
        return (bool)(field?.GetValue(controller) ?? false);
    }

    private static (DataGrid Grid, Window Window) CreateGrid(IEnumerable items)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            IsReadOnly = false,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding("Value")
        });
        
        window.Content = grid;

        window.Show();
        grid.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        grid.UpdateLayout();
        return (grid, window);
    }

    private static PointerPressedEventArgs CreatePointerPressedArgs(Control source, Visual root, IPointer pointer, Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        return new PointerPressedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None);
    }

    private static PointerReleasedEventArgs CreatePointerReleasedArgs(Control source, Visual root, IPointer pointer, Point position)
    {
        var properties = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
        return new PointerReleasedEventArgs(source, pointer, root, position, 0, properties, KeyModifiers.None, MouseButton.Left);
    }

    private static DragEventArgs CreateDragEventArgs(
        RoutedEvent<DragEventArgs> routedEvent,
        Interactive source,
        Point position,
        KeyModifiers modifiers)
    {
        var data = new DataTransfer();
        return new DragEventArgs(routedEvent, data, source, position, modifiers)
        {
            RoutedEvent = routedEvent,
            Source = source
        };
    }

    private static (DataGrid Grid, Window Window, HierarchicalModel Model, HierarchicalNode Root) CreateHierarchicalGrid()
    {
        var window = new Window
        {
            Width = 400,
            Height = 300,
        };

        window.SetThemeStyles();

        var rootItem = new TreeNode("Root", new ObservableCollection<TreeNode>
        {
            new("Child 1", new ObservableCollection<TreeNode>
            {
                new("Leaf")
            })
        });

        var options = new HierarchicalOptions<TreeNode>
        {
            ChildrenSelector = x => x.Children,
            AutoExpandRoot = false
        };

        var model = new HierarchicalModel<TreeNode>(options);
        model.SetRoot(rootItem);
        HierarchicalModel untyped = model;

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            CanUserReorderRows = true,
            AutoGenerateColumns = false,
            RowHeaderWidth = 28
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        window.Content = grid;
        
        window.Show();
        grid.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        grid.UpdateLayout();

        return (grid, window, untyped, untyped.Root!);
    }

    private static (DataGrid Grid, Window Window, HierarchicalModel Model, TreeNode RootItem) CreateHierarchicalSelectionGrid()
    {
        var window = new Window
        {
            Width = 500,
            Height = 300,
        };

        window.SetThemeStyles();

        var rootItem = new TreeNode("Root", new ObservableCollection<TreeNode>
        {
            new("A", new ObservableCollection<TreeNode>
            {
                new("A1")
            }),
            new("B"),
            new("C")
        });

        var options = new HierarchicalOptions<TreeNode>
        {
            ChildrenSelector = x => x.Children,
            AutoExpandRoot = true,
            VirtualizeChildren = false
        };

        var model = new HierarchicalModel<TreeNode>(options);
        model.SetRoot(rootItem);
        var rootNode = model.Root ?? throw new InvalidOperationException("Root node not created.");
        model.Expand(rootNode);
        HierarchicalModel untyped = model;

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            CanUserReorderRows = true,
            AutoGenerateColumns = false,
            SelectionMode = DataGridSelectionMode.Extended,
            RowHeaderWidth = 28
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        window.Content = grid;
        
        window.Show();
        grid.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        grid.UpdateLayout();

        return (grid, window, untyped, rootItem);
    }

    private static DataGridRow FindRowForItem(DataGrid grid, TreeNode item)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(row => row.DataContext is HierarchicalNode node && ReferenceEquals(node.Item, item));
    }

    private static HierarchicalNode FindNode(HierarchicalModel model, TreeNode item)
    {
        foreach (var node in model.Flattened)
        {
            if (ReferenceEquals(node.Item, item))
            {
                return node;
            }
        }

        throw new InvalidOperationException("Node not found.");
    }

    private class PlaceholderItem
    {
        public string Value { get; set; } = string.Empty;

        public override string ToString() => Value;
    }

    private record RowItem(string Value)
    {
        public override string ToString() => Value;
    }

    private record TreeNode(string Name, ObservableCollection<TreeNode>? Children = null)
    {
        public ObservableCollection<TreeNode> Children { get; } = Children ?? new ObservableCollection<TreeNode>();

        public override string ToString() => Name;
    }

    private sealed class RecordingDropHandler : IDataGridRowDropHandler
    {
        public List<DataGridRowDropEventArgs> ValidateCalls { get; } = new();

        public Func<DataGridRowDropEventArgs, bool>? ValidateCallback { get; set; }

        public bool Validate(DataGridRowDropEventArgs args)
        {
            ValidateCalls.Add(args);
            return ValidateCallback?.Invoke(args) ?? true;
        }

        public bool Execute(DataGridRowDropEventArgs args)
        {
            return true;
        }
    }
}
