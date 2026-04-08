using System;
using System.Collections;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ProDataGrid.ExcelSample.Models;

namespace ProDataGrid.ExcelSample.Controls;

public sealed class SheetTabStrip : TemplatedControl
{
    private const string ListBoxPartName = "PART_List";
    private static readonly DataFormat<string> DragDataFormat =
        DataFormat.CreateStringApplicationFormat("ProDataGrid.ExcelSample.SheetTab");

    public static readonly DirectProperty<SheetTabStrip, IEnumerable?> ItemsProperty =
        AvaloniaProperty.RegisterDirect<SheetTabStrip, IEnumerable?>(
            nameof(Items),
            o => o.Items,
            (o, v) => o.Items = v);

    public static readonly DirectProperty<SheetTabStrip, object?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<SheetTabStrip, object?>(
            nameof(SelectedItem),
            o => o.SelectedItem,
            (o, v) => o.SelectedItem = v);

    public static readonly DirectProperty<SheetTabStrip, ICommand?> ReorderCommandProperty =
        AvaloniaProperty.RegisterDirect<SheetTabStrip, ICommand?>(
            nameof(ReorderCommand),
            o => o.ReorderCommand,
            (o, v) => o.ReorderCommand = v);

    private IEnumerable? _items;
    private object? _selectedItem;
    private ICommand? _reorderCommand;
    private ListBox? _listBox;
    private object? _dragItem;
    private Point? _dragStart;
    private int _dragPointerId;
    private PointerPressedEventArgs? _dragStartEventArgs;
    private bool _dragPending;
    private bool _isDragging;

    public IEnumerable? Items
    {
        get => _items;
        set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
    }

    public ICommand? ReorderCommand
    {
        get => _reorderCommand;
        set => SetAndRaise(ReorderCommandProperty, ref _reorderCommand, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_listBox != null)
        {
            DetachListBoxHandlers(_listBox);
        }

        _listBox = e.NameScope.Find<ListBox>(ListBoxPartName);
        if (_listBox != null)
        {
            AttachListBoxHandlers(_listBox);
        }
    }

    private void AttachListBoxHandlers(ListBox listBox)
    {
        DragDrop.SetAllowDrop(listBox, true);
        listBox.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        listBox.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        listBox.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        listBox.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
        listBox.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        listBox.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void DetachListBoxHandlers(ListBox listBox)
    {
        listBox.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        listBox.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        listBox.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
        listBox.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
        listBox.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
        listBox.RemoveHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_listBox == null || _isDragging)
        {
            return;
        }

        var point = e.GetPosition(_listBox);
        if (!e.GetCurrentPoint(_listBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!TryGetItemContainer(e.Source as Visual, out var container))
        {
            return;
        }

        _dragItem = container.DataContext;
        if (_dragItem == null)
        {
            return;
        }

        _dragStart = point;
        _dragPointerId = e.Pointer.Id;
        _dragStartEventArgs = e;
        _dragPending = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_listBox == null || !_dragPending || _dragStart == null || _dragItem == null)
        {
            return;
        }

        if (e.Pointer.Id != _dragPointerId)
        {
            return;
        }

        var current = e.GetPosition(_listBox);
        if (!IsDragThresholdMet(_dragStart.Value, current))
        {
            return;
        }

        _dragPending = false;
        if (_dragStartEventArgs != null)
        {
            BeginDrag(_dragStartEventArgs);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ResetDragState();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetDragState();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (_listBox == null || !TryGetDragIndex(e, out _))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (_listBox == null)
        {
            return;
        }

        if (!TryResolveIndexes(e, out var fromIndex, out var toIndex))
        {
            ResetDragState();
            return;
        }

        if (fromIndex == toIndex)
        {
            ResetDragState();
            return;
        }

        var request = new SheetTabReorderRequest(fromIndex, toIndex);
        if (ReorderCommand?.CanExecute(request) == true)
        {
            ReorderCommand.Execute(request);
        }
        else if (Items is IList list)
        {
            ApplyFallbackReorder(list, fromIndex, toIndex);
        }

        ResetDragState();
        e.Handled = true;
    }

    private async void BeginDrag(PointerPressedEventArgs e)
    {
        if (_listBox == null || _dragItem == null || _isDragging)
        {
            return;
        }

        _isDragging = true;
        if (Items is not IList list)
        {
            return;
        }

        var fromIndex = list.IndexOf(_dragItem);
        if (fromIndex < 0)
        {
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(DragDataFormat, fromIndex.ToString(CultureInfo.InvariantCulture)));
        try
        {
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        finally
        {
            ResetDragState();
        }
    }

    private void ResetDragState()
    {
        _dragPending = false;
        _isDragging = false;
        _dragStart = null;
        _dragPointerId = 0;
        _dragStartEventArgs = null;
        _dragItem = null;
    }

    private static bool IsDragThresholdMet(Point start, Point current)
    {
        var delta = current - start;
        return Math.Abs(delta.X) >= 4 || Math.Abs(delta.Y) >= 4;
    }

    private static bool TryGetDragIndex(DragEventArgs e, out int index)
    {
        index = -1;
        var transfer = e.DataTransfer;
        if (transfer == null)
        {
            return false;
        }

        foreach (var entry in transfer.Items)
        {
            var raw = entry.TryGetRaw(DragDataFormat) as string;
            if (!string.IsNullOrWhiteSpace(raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryResolveIndexes(DragEventArgs e, out int fromIndex, out int toIndex)
    {
        fromIndex = -1;
        toIndex = -1;

        if (_listBox == null || Items is not IList list)
        {
            return false;
        }

        if (!TryGetDragIndex(e, out fromIndex))
        {
            return false;
        }

        if (fromIndex < 0 || fromIndex >= list.Count)
        {
            return false;
        }

        if (!TryGetItemContainer(e.Source as Visual, out var container))
        {
            toIndex = list.Count;
            return true;
        }

        var targetItem = container.DataContext;
        if (targetItem == null)
        {
            toIndex = list.Count;
            return true;
        }

        var targetIndex = list.IndexOf(targetItem);
        if (targetIndex < 0)
        {
            toIndex = list.Count;
            return true;
        }

        var pointer = e.GetPosition(container);
        var insertAfter = pointer.X >= container.Bounds.Width * 0.5;
        toIndex = insertAfter ? targetIndex + 1 : targetIndex;
        return true;
    }

    private static bool TryGetItemContainer(Visual? visual, out ListBoxItem container)
    {
        var resolved = visual?.FindAncestorOfType<ListBoxItem>();
        if (resolved == null)
        {
            container = null!;
            return false;
        }

        container = resolved;
        return true;
    }

    private static void ApplyFallbackReorder(IList list, int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= list.Count)
        {
            return;
        }

        if (toIndex < 0)
        {
            toIndex = 0;
        }
        else if (toIndex > list.Count)
        {
            toIndex = list.Count;
        }

        if (fromIndex == toIndex)
        {
            return;
        }

        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        if (fromIndex < toIndex)
        {
            toIndex--;
        }

        list.Insert(toIndex, item);
    }
}
