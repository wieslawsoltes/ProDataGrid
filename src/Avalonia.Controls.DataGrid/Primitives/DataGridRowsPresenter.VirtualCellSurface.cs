// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls.Primitives;

internal readonly record struct DataGridVirtualRowInfo(
    DataGridRow? Container,
    int Slot,
    int RowIndex,
    object Item,
    double Top,
    double Height);

sealed partial class DataGridRowsPresenter
{
    private const double VirtualCellHorizontalPadding = 12d;
    private const double VirtualExpanderSize = 16d;
    private const double VirtualComboBoxGlyphWidth = 20d;
    // Keep several viewports of shaped text so discontinuous scrolling does not
    // evict the entire working set between render passes.
    private const int VirtualTextLayoutCacheCapacity = 4096;

    private struct DataGridVirtualSurfaceRenderCounters
    {
        public int Rows;
        public int Cells;
        public int Clips;
        public int VerticalGridLines;
        public int TextLayoutCacheHits;
        public int TextLayoutCacheMisses;
        public int TextDrawOperations;
        public int TextGlyphRuns;
        public int ExpanderDrawOperations;
        public int ValueCacheHits;
        public int ValueCacheMisses;
    }

    private readonly record struct DataGridVirtualCellOverlay(
        Pen Pen,
        Rect? Rectangle,
        Point LineStart,
        Point LineEnd,
        Rect? Clip);

    private DataGridVirtualCellSurface? _virtualCellSurface;
    private readonly DataGridVirtualTextLayoutCache _virtualTextLayoutCache =
        new(VirtualTextLayoutCacheCapacity);
    private readonly List<DataGridVirtualTextDrawCommand> _virtualTextDrawCommands = new();
    private readonly List<DataGridVirtualCellOverlay> _virtualCellOverlays = new();
    private List<DataGridVirtualColumnRenderPlan> _virtualColumnRenderPlans = new();
    private List<DataGridVirtualColumnRenderPlan> _nextVirtualColumnRenderPlans = new();
    private int _virtualColumnRenderPlanVersion;
    private DataGridVirtualRowValueCacheEntry?[] _virtualRowValueCache =
        Array.Empty<DataGridVirtualRowValueCacheEntry?>();
    private DataGridVirtualRowValueCacheEntry?[] _nextVirtualRowValueCache =
        Array.Empty<DataGridVirtualRowValueCacheEntry?>();
    private int _virtualRowValueCacheCount;
    private int _virtualRowValueCacheFirstSlot = -1;
    private HashSet<INotifyPropertyChanged> _virtualValueNotifiers =
        new(ReferenceEqualityComparer.Instance);
    private HashSet<INotifyPropertyChanged> _nextVirtualValueNotifiers =
        new(ReferenceEqualityComparer.Instance);
    private int _virtualValueChangeCount;
    private readonly List<DataGridVirtualRowInfo> _lightweightVirtualRows = new();
    private DataGridVirtualRowInfo[] _lightweightVirtualRowBuffer =
        Array.Empty<DataGridVirtualRowInfo>();
    private int _lightweightVirtualRowBufferCount;

    internal int VirtualSurfaceCount => _virtualCellSurface?.GetVisualParent() == this ? 1 : 0;

    internal int VirtualColumnRenderPlanCount => _virtualColumnRenderPlans.Count;

    internal int VirtualRowValueCacheCount => _virtualRowValueCacheCount;

    internal int VirtualTrackedValueNotifierCount => _virtualValueNotifiers.Count;

    internal int VirtualValueChangeCount => Volatile.Read(ref _virtualValueChangeCount);

    internal long VirtualCellValueCacheHitCount { get; private set; }

    internal long VirtualCellValueCacheMissCount { get; private set; }

    internal int LightweightVirtualRowCount => _lightweightVirtualRows.Count;

    internal IReadOnlyList<DataGridVirtualRowInfo> LightweightVirtualRows => _lightweightVirtualRows;

    internal long LightweightVirtualItemResolveCount { get; private set; }

    internal long LightweightVirtualItemReuseCount { get; private set; }

    internal void InvalidateVirtualCellSurface() => _virtualCellSurface?.InvalidateVisual();

    internal bool TryUpdateLightweightVirtualRows(
        int firstSlot,
        int lastSlot,
        int count,
        double rowHeight)
    {
        return TryBuildLightweightVirtualRows(
            firstSlot,
            lastSlot,
            count,
            rowHeight,
            reuseExistingItems: false);
    }

    internal bool TryScrollLightweightVirtualRows(
        int firstSlot,
        int lastSlot,
        int count,
        double rowHeight)
    {
        return TryBuildLightweightVirtualRows(
            firstSlot,
            lastSlot,
            count,
            rowHeight,
            reuseExistingItems: true);
    }

    private bool TryBuildLightweightVirtualRows(
        int firstSlot,
        int lastSlot,
        int count,
        double rowHeight,
        bool reuseExistingItems)
    {
        DataGrid? grid = OwningGrid;
        if (grid is null || count <= 0)
        {
            return false;
        }

        if (_lightweightVirtualRowBuffer.Length < count)
        {
            _lightweightVirtualRowBuffer = new DataGridVirtualRowInfo[count];
        }

        int previousFirstSlot = _lightweightVirtualRows.Count > 0
            ? _lightweightVirtualRows[0].Slot
            : -1;
        double top = -grid.NegVerticalOffset;
        int resolvedItems = 0;
        int reusedItems = 0;
        for (int index = 0; index < count; index++)
        {
            int slot = firstSlot + index;
            Debug.Assert(slot <= lastSlot);

            object item;
            int previousIndex = slot - previousFirstSlot;
            if (reuseExistingItems &&
                (uint)previousIndex < (uint)_lightweightVirtualRows.Count &&
                _lightweightVirtualRows[previousIndex].Slot == slot)
            {
                item = _lightweightVirtualRows[previousIndex].Item;
                reusedItems++;
            }
            else
            {
                // Lightweight eligibility guarantees an ungrouped, uncollapsed slot
                // range, so the slot is also the row index and the range is contiguous.
                item = grid.DataConnection.GetDataItem(slot);
                resolvedItems++;
                if (item is DataGridRow)
                {
                    Array.Clear(
                        _lightweightVirtualRowBuffer,
                        0,
                        Math.Max(_lightweightVirtualRowBufferCount, index + 1));
                    _lightweightVirtualRowBufferCount = 0;
                    _lightweightVirtualRows.Clear();
                    grid.RequireRetainedVirtualRowsForItems();
                    return false;
                }
            }

            _lightweightVirtualRowBuffer[index] = new DataGridVirtualRowInfo(
                null,
                slot,
                slot,
                item,
                top,
                rowHeight);
            top += rowHeight;
        }

        if (_lightweightVirtualRows.Capacity < count)
        {
            _lightweightVirtualRows.Capacity = count;
        }

        _lightweightVirtualRows.Clear();
        for (int index = 0; index < count; index++)
        {
            _lightweightVirtualRows.Add(_lightweightVirtualRowBuffer[index]);
        }

        if (_lightweightVirtualRowBufferCount > count)
        {
            Array.Clear(
                _lightweightVirtualRowBuffer,
                count,
                _lightweightVirtualRowBufferCount - count);
        }
        _lightweightVirtualRowBufferCount = count;
        LightweightVirtualItemResolveCount += resolvedItems;
        LightweightVirtualItemReuseCount += reusedItems;
        return true;
    }

    internal void ClearLightweightVirtualRows()
    {
        _lightweightVirtualRows.Clear();
        Array.Clear(_lightweightVirtualRowBuffer, 0, _lightweightVirtualRowBufferCount);
        _lightweightVirtualRowBufferCount = 0;
        ClearVirtualRowValueCache();
        ClearVirtualValueNotifiers();
    }

    private double RefreshLightweightVirtualRowGeometry()
    {
        DataGrid? grid = OwningGrid;
        if (grid is null || _lightweightVirtualRows.Count == 0)
        {
            return 0;
        }

        double top = -grid.NegVerticalOffset;
        double height = _lightweightVirtualRows[0].Height;
        for (int index = 0; index < _lightweightVirtualRows.Count; index++)
        {
            DataGridVirtualRowInfo row = _lightweightVirtualRows[index];
            _lightweightVirtualRows[index] = row with { Top = top };
            top += height;
        }

        return _lightweightVirtualRows.Count * height;
    }

    private void SyncVirtualCellSurface()
    {
        if (OwningGrid?.UsesVirtualCellSurface != true)
        {
            DetachVirtualCellSurface();
            return;
        }

        _virtualCellSurface ??= new DataGridVirtualCellSurface
        {
            Owner = this,
            Focusable = true,
            ClipToBounds = true,
        };

        if (!ReferenceEquals(_virtualCellSurface.GetVisualParent(), this))
        {
            _virtualCellSurface.SetValue(Panel.ZIndexProperty, 0);
            VisualChildren.Add(_virtualCellSurface);
        }
    }

    private void DetachVirtualCellSurface()
    {
        _virtualColumnRenderPlans.Clear();
        _nextVirtualColumnRenderPlans.Clear();
        ClearVirtualRowValueCache();

        if (_virtualCellSurface is null)
        {
            return;
        }

        if (ReferenceEquals(_virtualCellSurface.GetVisualParent(), this))
        {
            VisualChildren.Remove(_virtualCellSurface);
        }

        _virtualCellSurface.Owner = null;
        _virtualCellSurface = null;
        _virtualTextLayoutCache.Clear();
        ClearVirtualValueNotifiers();
    }

    private void MeasureVirtualCellSurface(Size availableSize)
    {
        if (_virtualCellSurface is null)
        {
            return;
        }

        double width = double.IsFinite(availableSize.Width) ? Math.Max(0d, availableSize.Width) : 0d;
        double height = double.IsFinite(availableSize.Height) ? Math.Max(0d, availableSize.Height) : 0d;
        _virtualCellSurface.Measure(new Size(width, height));
    }

    private void ArrangeVirtualCellSurface(double width, double height)
    {
        if (_virtualCellSurface is null)
        {
            return;
        }

        _virtualCellSurface.Arrange(new Rect(0d, 0d, Math.Max(0d, width), Math.Max(0d, height)));
        SyncVirtualValueNotifiers();
        _virtualCellSurface.InvalidateVisual();
    }

    internal void RenderVirtualCells(DrawingContext context)
    {
        DataGrid? grid = OwningGrid;
        if (grid?.UsesVirtualCellSurface != true)
        {
            return;
        }

        using var renderScope = DataGridDiagnostics.BeginVirtualSurfaceRender();
        DataGridVirtualSurfaceRenderCounters counters = default;
        _virtualTextDrawCommands.Clear();
        _virtualCellOverlays.Clear();

        ResolveVirtualTextStyle(out FontFamily fontFamily, out double fontSize, out IBrush foreground);
        PrepareVirtualColumnRenderPlans(fontFamily, fontSize, foreground);
        if (grid.DisplayData.HasVirtualScrollingElements)
        {
            PrepareVirtualRowValueCache(ref counters);
        }
        IBrush? selectedBackground = FindVirtualResource<IBrush>(
            grid.IsKeyboardFocusWithin
                ? "DataGridCellSelectedBackgroundBrush"
                : "DataGridCellSelectedUnfocusedBackgroundBrush");
        IBrush? currentBorder = FindVirtualResource<IBrush>("DataGridCurrencyVisualPrimaryBrush");
        Pen? currentPen = currentBorder is null ? null : new Pen(currentBorder, 1d);
        Pen? verticalGridPen = grid.VerticalGridLinesBrush is null ? null : new Pen(grid.VerticalGridLinesBrush, 1d);
        Pen expanderPen = new(foreground, 1.5d, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        bool drawVerticalGridLines = grid.GridLinesVisibility is DataGridGridLinesVisibility.Vertical or DataGridGridLinesVisibility.All;
        bool hasSelectedCells = grid.HasSelectedCells;

        if (grid.DisplayData.HasVirtualScrollingElements)
        {
            for (int index = 0; index < _lightweightVirtualRows.Count; index++)
            {
                DrawVirtualRow(context, grid, _lightweightVirtualRows[index],
                    _virtualRowValueCache[index], expanderPen,
                    selectedBackground, currentPen, verticalGridPen,
                    drawVerticalGridLines, hasSelectedCells, ref counters);
            }
        }
        else
        {
            foreach (DataGridRow row in grid.DisplayData.GetScrollingRows())
            {
                var rowInfo = new DataGridVirtualRowInfo(
                    row,
                    row.Slot,
                    row.Index,
                    row.DataContext!,
                    row.Bounds.Top,
                    row.GetFlatCellsHeight());
                DrawVirtualRow(context, grid, rowInfo, null, expanderPen, selectedBackground,
                    currentPen, verticalGridPen,
                    drawVerticalGridLines, hasSelectedCells, ref counters);
            }
        }

        if (_virtualTextDrawCommands.Count > 0)
        {
            var textOperation = new DataGridVirtualTextDrawOperation(new Rect(Bounds.Size), _virtualTextDrawCommands);
            try
            {
                context.Custom(textOperation);
                counters.TextDrawOperations++;
            }
            catch
            {
                textOperation.Dispose();
                throw;
            }
            finally
            {
                _virtualTextDrawCommands.Clear();
            }
        }

        for (int index = 0; index < _virtualCellOverlays.Count; index++)
        {
            DataGridVirtualCellOverlay overlay = _virtualCellOverlays[index];
            if (overlay.Clip is { } clip)
            {
                using (context.PushClip(clip))
                {
                    DrawVirtualCellOverlay(context, overlay);
                }
            }
            else
            {
                DrawVirtualCellOverlay(context, overlay);
            }
        }

        _virtualCellOverlays.Clear();

        DataGridDiagnostics.RecordVirtualSurfaceRender(
            counters.Rows,
            counters.Cells,
            counters.Clips,
            counters.VerticalGridLines,
            counters.TextLayoutCacheHits,
            counters.TextLayoutCacheMisses,
            counters.TextDrawOperations,
            counters.TextGlyphRuns,
            counters.ExpanderDrawOperations,
            counters.ValueCacheHits,
            counters.ValueCacheMisses);
    }

    private void DrawVirtualRow(
        DrawingContext context,
        DataGrid grid,
        DataGridVirtualRowInfo row,
        DataGridVirtualRowValueCacheEntry? valueCache,
        Pen expanderPen,
        IBrush? selectedBackground,
        Pen? currentPen,
        Pen? verticalGridPen,
        bool drawVerticalGridLines,
        bool hasSelectedCells,
        ref DataGridVirtualSurfaceRenderCounters counters)
    {
        double rowHeight = row.Height;
        if (rowHeight <= 0d || row.Top + rowHeight < 0d || row.Top > Bounds.Height)
        {
            return;
        }

        counters.Rows++;
        int currentColumnIndex = grid.CurrentSlot == row.Slot ? grid.CurrentColumnIndex : -1;
        bool rowSelected =
            grid.SelectionUnit == DataGridSelectionUnit.FullRow &&
            grid.GetRowSelection(row.Slot);

        ReadOnlySpan<DataGridVirtualColumnRenderPlan> renderPlans =
            CollectionsMarshal.AsSpan(_virtualColumnRenderPlans);
        for (int planIndex = 0; planIndex < renderPlans.Length; planIndex++)
        {
            ref readonly DataGridVirtualColumnRenderPlan plan = ref renderPlans[planIndex];
            FlatColumnLayout layout = plan.Layout;
            DataGridColumn column = layout.Column;
            if (row.Container is not null && grid.IsVirtualCompatibilityCell(row.Container, column))
            {
                continue;
            }

            Rect cellBounds = GetVirtualCellBounds(row, layout);
            Rect visibleBounds = GetVisibleVirtualCellBounds(row, layout);
            if (visibleBounds.Width <= 0d || visibleBounds.Height <= 0d)
            {
                continue;
            }

            counters.Cells++;
            object? value = valueCache is null
                ? GetVirtualCellValue(plan, row.Item)
                : valueCache.Values[planIndex];

            if (AreClose(cellBounds, visibleBounds))
            {
                DrawVirtualCell(context, grid, row, plan, value, cellBounds, expanderPen,
                    selectedBackground, currentPen, verticalGridPen,
                    drawVerticalGridLines, hasSelectedCells, rowSelected, currentColumnIndex, null, ref counters);
                continue;
            }

            counters.Clips++;
            using (context.PushClip(visibleBounds))
            {
                DrawVirtualCell(context, grid, row, plan, value, cellBounds, expanderPen,
                    selectedBackground, currentPen, verticalGridPen,
                    drawVerticalGridLines, hasSelectedCells, rowSelected, currentColumnIndex, visibleBounds, ref counters);
            }
        }
    }

    private void DrawVirtualCell(
        DrawingContext context,
        DataGrid grid,
        DataGridVirtualRowInfo row,
        in DataGridVirtualColumnRenderPlan plan,
        object? value,
        Rect cellBounds,
        Pen expanderPen,
        IBrush? selectedBackground,
        Pen? currentPen,
        Pen? verticalGridPen,
        bool drawVerticalGridLines,
        bool hasSelectedCells,
        bool rowSelected,
        int currentColumnIndex,
        Rect? textClip,
        ref DataGridVirtualSurfaceRenderCounters counters)
    {
        DataGridColumn column = plan.Layout.Column;
        bool selected = grid.SelectionUnit == DataGridSelectionUnit.FullRow
            ? rowSelected
            : hasSelectedCells && grid.IsCellSelected(row.RowIndex, column.Index);
        bool current = currentColumnIndex == column.Index;
        if (selected && selectedBackground is not null)
        {
            context.DrawRectangle(selectedBackground, null, cellBounds);
        }

        DrawVirtualCellContent(
            context,
            grid,
            row,
            plan,
            value,
            cellBounds,
            expanderPen,
            textClip,
            ref counters);
        if (current && currentPen is not null)
        {
            _virtualCellOverlays.Add(new DataGridVirtualCellOverlay(
                currentPen,
                cellBounds.Deflate(0.5d),
                default,
                default,
                textClip));
        }

        if (drawVerticalGridLines && verticalGridPen is not null &&
            (!ReferenceEquals(column, grid.ColumnsInternal.LastVisibleColumn) || grid.ColumnsInternal.FillerColumn.IsActive))
        {
            double x = Math.Max(cellBounds.Left, cellBounds.Right - 0.5d);
            _virtualCellOverlays.Add(new DataGridVirtualCellOverlay(
                verticalGridPen,
                null,
                new Point(x, cellBounds.Top),
                new Point(x, cellBounds.Bottom),
                textClip));
            counters.VerticalGridLines++;
        }
    }

    internal bool IsVirtualCellSelected(
        DataGridVirtualRowInfo row,
        int columnIndex,
        bool hasSelectedCells)
    {
        DataGrid? grid = OwningGrid;
        if (grid is null)
        {
            return false;
        }

        return grid.SelectionUnit == DataGridSelectionUnit.FullRow
            ? grid.GetRowSelection(row.Slot)
            : hasSelectedCells && grid.IsCellSelected(row.RowIndex, columnIndex);
    }

    private static void DrawVirtualCellOverlay(DrawingContext context, DataGridVirtualCellOverlay overlay)
    {
        if (overlay.Rectangle is { } rectangle)
        {
            context.DrawRectangle(null, overlay.Pen, rectangle);
        }
        else
        {
            context.DrawLine(overlay.Pen, overlay.LineStart, overlay.LineEnd);
        }
    }

    private void DrawVirtualCellContent(
        DrawingContext context,
        DataGrid grid,
        DataGridVirtualRowInfo row,
        in DataGridVirtualColumnRenderPlan plan,
        object? value,
        Rect bounds,
        Pen expanderPen,
        Rect? textClip,
        ref DataGridVirtualSurfaceRenderCounters counters)
    {
        DataGridColumn column = plan.Layout.Column;
        object? item = row.Item;

        if (plan.Kind == DataGridVirtualCellKind.Progress)
        {
            DrawVirtualProgress(context, (DataGridProgressBarColumn)column, value, bounds, plan.Foreground);
            return;
        }

        if (plan.Kind == DataGridVirtualCellKind.CheckBox)
        {
            DrawVirtualCheckBox(context, (DataGridCheckBoxColumn)column, value, bounds, expanderPen);
            return;
        }

        if (plan.Kind == DataGridVirtualCellKind.Image && value is IImage image)
        {
            DrawVirtualImage(context, (DataGridImageColumn)column, image, bounds);
            return;
        }

        string text = value?.ToString() ?? string.Empty;
        double left = bounds.Left + VirtualCellHorizontalPadding;
        double right = bounds.Right - VirtualCellHorizontalPadding;
        if (plan.Kind == DataGridVirtualCellKind.Hierarchical && item is HierarchicalNode node)
        {
            var hierarchicalColumn = (DataGridHierarchicalColumn)column;
            double indent = Math.Max(0, node.Level) * hierarchicalColumn.Indent;
            counters.ExpanderDrawOperations += DrawVirtualExpander(
                context,
                node,
                bounds.Left + indent,
                bounds.Top,
                bounds.Height,
                expanderPen);
            left += indent + VirtualExpanderSize;
        }
        else if (plan.Kind == DataGridVirtualCellKind.ComboBox)
        {
            DrawVirtualComboBoxChevron(context, bounds, expanderPen);
            right -= VirtualComboBoxGlyphWidth;
        }

        double maxWidth = Math.Max(0d, right - left);
        if (maxWidth <= 0d || string.IsNullOrEmpty(text))
        {
            return;
        }

        DataGridVirtualTextLayout textLayout = GetVirtualTextLayout(
            text,
            plan,
            maxWidth,
            bounds.Height,
            ref counters);
        double top = bounds.Top + Math.Max(0d, (bounds.Height - textLayout.Layout.Height) * 0.5d);
        if (textLayout.RenderData is { } renderData)
        {
            _virtualTextDrawCommands.Add(new DataGridVirtualTextDrawCommand(
                renderData,
                new Point(left, top),
                textClip));
            counters.TextGlyphRuns += renderData.GlyphRunCount;
        }
        else
        {
            textLayout.Layout.Draw(context, new Point(left, top));
            counters.TextDrawOperations++;
        }
    }

    private object? GetVirtualCellValue(in DataGridVirtualColumnRenderPlan plan, object? item)
    {
        DataGridColumn column = plan.Layout.Column;
        if (item is not null &&
            plan.ValueAccessor is IDataGridColumnTextAccessor textAccessor &&
            (plan.Kind == DataGridVirtualCellKind.Hierarchical || column is DataGridTextColumn))
        {
            return textAccessor.TryGetText(
                item,
                plan.TextConverter,
                plan.TextConverterParameter,
                plan.TextStringFormat,
                plan.TextCulture,
                plan.TextCulture,
                out string? text)
                ? text
                : null;
        }

        if (item is not null && plan.ValueProvider is { } provider)
        {
            return provider.GetDrawnCellValue(item);
        }

        IDataGridColumnValueAccessor? accessor = plan.ValueAccessor;
        if (item is not null && accessor is not null && accessor.ItemType.IsInstanceOfType(item))
        {
            return accessor.GetValue(item);
        }

        return null;
    }

    private void PrepareVirtualRowValueCache(ref DataGridVirtualSurfaceRenderCounters counters)
    {
        int rowCount = _lightweightVirtualRows.Count;
        int columnCount = _virtualColumnRenderPlans.Count;
        if (rowCount == 0 || columnCount == 0)
        {
            ClearVirtualRowValueCache();
            return;
        }

        EnsureVirtualRowValueCacheCapacity(rowCount);

        int previousCount = _virtualRowValueCacheCount;
        int previousFirstSlot = _virtualRowValueCacheFirstSlot;
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            DataGridVirtualRowInfo row = _lightweightVirtualRows[rowIndex];
            int previousIndex = row.Slot - previousFirstSlot;
            if ((uint)previousIndex >= (uint)previousCount)
            {
                continue;
            }

            DataGridVirtualRowValueCacheEntry? entry = _virtualRowValueCache[previousIndex];
            if (entry is null || !ReferenceEquals(entry.Item, row.Item))
            {
                continue;
            }

            _nextVirtualRowValueCache[rowIndex] = entry;
            _virtualRowValueCache[previousIndex] = null;
        }

        int reusableIndex = 0;
        int valueChangeVersion = VirtualValueChangeCount;
        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            DataGridVirtualRowInfo row = _lightweightVirtualRows[rowIndex];
            DataGridVirtualRowValueCacheEntry? entry = _nextVirtualRowValueCache[rowIndex];
            if (entry is null)
            {
                while (reusableIndex < previousCount && _virtualRowValueCache[reusableIndex] is null)
                {
                    reusableIndex++;
                }

                if (reusableIndex < previousCount)
                {
                    entry = _virtualRowValueCache[reusableIndex];
                    _virtualRowValueCache[reusableIndex] = null;
                    reusableIndex++;
                }
                else
                {
                    entry = new DataGridVirtualRowValueCacheEntry();
                }

                _nextVirtualRowValueCache[rowIndex] = entry;
            }

            if (entry.IsValid(
                    row.Item,
                    _virtualColumnRenderPlanVersion,
                    valueChangeVersion,
                    columnCount))
            {
                counters.ValueCacheHits += columnCount;
                continue;
            }

            entry.Prepare(
                row.Item,
                _virtualColumnRenderPlanVersion,
                valueChangeVersion,
                columnCount);
            for (int planIndex = 0; planIndex < columnCount; planIndex++)
            {
                entry.Values[planIndex] = GetVirtualCellValue(
                    _virtualColumnRenderPlans[planIndex],
                    row.Item);
            }

            counters.ValueCacheMisses += columnCount;
        }

        Array.Clear(_virtualRowValueCache, 0, previousCount);
        (_virtualRowValueCache, _nextVirtualRowValueCache) =
            (_nextVirtualRowValueCache, _virtualRowValueCache);
        _virtualRowValueCacheCount = rowCount;
        _virtualRowValueCacheFirstSlot = _lightweightVirtualRows[0].Slot;
        VirtualCellValueCacheHitCount += counters.ValueCacheHits;
        VirtualCellValueCacheMissCount += counters.ValueCacheMisses;
    }

    private void EnsureVirtualRowValueCacheCapacity(int count)
    {
        if (_virtualRowValueCache.Length < count)
        {
            Array.Resize(ref _virtualRowValueCache, count);
        }

        if (_nextVirtualRowValueCache.Length < count)
        {
            Array.Resize(ref _nextVirtualRowValueCache, count);
        }
        else
        {
            Array.Clear(_nextVirtualRowValueCache, 0, count);
        }
    }

    private void ClearVirtualRowValueCache()
    {
        for (int index = 0; index < _virtualRowValueCache.Length; index++)
        {
            _virtualRowValueCache[index]?.Clear();
            _virtualRowValueCache[index] = null;
        }

        for (int index = 0; index < _nextVirtualRowValueCache.Length; index++)
        {
            _nextVirtualRowValueCache[index]?.Clear();
            _nextVirtualRowValueCache[index] = null;
        }

        _virtualRowValueCacheCount = 0;
        _virtualRowValueCacheFirstSlot = -1;
    }

    private void PrepareVirtualColumnRenderPlans(
        FontFamily defaultFontFamily,
        double defaultFontSize,
        IBrush defaultForeground)
    {
        _nextVirtualColumnRenderPlans.Clear();
        for (int layoutIndex = 0; layoutIndex < _flatColumnLayouts.Count; layoutIndex++)
        {
            FlatColumnLayout layout = _flatColumnLayouts[layoutIndex];
            if (!layout.ShouldDisplay)
            {
                continue;
            }

            DataGridColumn column = layout.Column;
            FontFamily fontFamily = defaultFontFamily;
            double fontSize = defaultFontSize;
            FontStyle fontStyle = FontStyle.Normal;
            FontWeight fontWeight = FontWeight.Normal;
            FontStretch fontStretch = FontStretch.Normal;
            IBrush foreground = defaultForeground;
            if (column is DataGridTextColumn textColumn)
            {
                fontFamily = textColumn.FontFamily ?? fontFamily;
                fontSize = double.IsFinite(textColumn.FontSize) && textColumn.FontSize > 0d
                    ? textColumn.FontSize
                    : fontSize;
                fontStyle = textColumn.FontStyle;
                fontWeight = textColumn.FontWeight;
                fontStretch = textColumn.FontStretch;
                foreground = textColumn.Foreground ?? foreground;
            }

            GetBrushIdentity(
                foreground,
                out byte foregroundKind,
                out Color foregroundColor,
                out double foregroundOpacity,
                out int foregroundIdentity);
            ResolveVirtualTextValueFormat(
                column,
                out IValueConverter? textConverter,
                out object? textConverterParameter,
                out string? textStringFormat,
                out CultureInfo textCulture);
            _nextVirtualColumnRenderPlans.Add(new DataGridVirtualColumnRenderPlan(
                layout,
                GetVirtualCellKind(column),
                column as IDataGridDrawnCellValueProvider,
                DataGridColumnMetadata.GetValueAccessor(column),
                fontFamily,
                fontFamily.Name,
                fontSize,
                fontStyle,
                fontWeight,
                fontStretch,
                foreground,
                foregroundKind,
                foregroundColor,
                foregroundOpacity,
                foregroundIdentity,
                CultureInfo.CurrentCulture.LCID,
                GetVirtualTextAlignment(column),
                textConverter,
                textConverterParameter,
                textStringFormat,
                textCulture));
        }

        if (!HasSameVirtualColumnRenderPlans())
        {
            _virtualColumnRenderPlanVersion++;
        }

        (_virtualColumnRenderPlans, _nextVirtualColumnRenderPlans) =
            (_nextVirtualColumnRenderPlans, _virtualColumnRenderPlans);
    }

    private bool HasSameVirtualColumnRenderPlans()
    {
        if (_virtualColumnRenderPlans.Count != _nextVirtualColumnRenderPlans.Count)
        {
            return false;
        }

        for (int index = 0; index < _virtualColumnRenderPlans.Count; index++)
        {
            if (_virtualColumnRenderPlans[index] != _nextVirtualColumnRenderPlans[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void ResolveVirtualTextValueFormat(
        DataGridColumn column,
        out IValueConverter? converter,
        out object? converterParameter,
        out string? stringFormat,
        out CultureInfo culture)
    {
        BindingBase? binding = column switch
        {
            DataGridTextColumn textColumn => textColumn.Binding,
            DataGridHierarchicalColumn hierarchicalColumn => hierarchicalColumn.Binding,
            _ => null,
        };
        converter = BindingCloneHelper.GetConverter(binding);
        converterParameter = BindingCloneHelper.GetConverterParameter(binding);
        stringFormat = BindingCloneHelper.GetStringFormat(binding);
        culture = BindingCloneHelper.GetConverterCulture(binding) ?? CultureInfo.CurrentCulture;
    }

    private static DataGridVirtualCellKind GetVirtualCellKind(DataGridColumn column) => column switch
    {
        DataGridProgressBarColumn => DataGridVirtualCellKind.Progress,
        DataGridCheckBoxColumn => DataGridVirtualCellKind.CheckBox,
        DataGridImageColumn => DataGridVirtualCellKind.Image,
        DataGridHierarchicalColumn => DataGridVirtualCellKind.Hierarchical,
        DataGridComboBoxColumn => DataGridVirtualCellKind.ComboBox,
        _ => DataGridVirtualCellKind.Text,
    };

    private static TextAlignment GetVirtualTextAlignment(DataGridColumn column) => column switch
    {
        DataGridNumericColumn => TextAlignment.Right,
        DataGridDatePickerColumn dateColumn => dateColumn.GetTextAlignment(),
        DataGridSliderColumn { ShowValueText: true } => TextAlignment.Center,
        DataGridComboBoxColumn comboBoxColumn => comboBoxColumn.GetTextAlignment(),
        _ => TextAlignment.Left,
    };

    private enum DataGridVirtualCellKind : byte
    {
        Text,
        Hierarchical,
        Progress,
        CheckBox,
        Image,
        ComboBox,
    }

    private readonly record struct DataGridVirtualColumnRenderPlan(
        FlatColumnLayout Layout,
        DataGridVirtualCellKind Kind,
        IDataGridDrawnCellValueProvider? ValueProvider,
        IDataGridColumnValueAccessor? ValueAccessor,
        FontFamily FontFamily,
        string FontFamilyName,
        double FontSize,
        FontStyle FontStyle,
        FontWeight FontWeight,
        FontStretch FontStretch,
        IBrush Foreground,
        byte ForegroundKind,
        Color ForegroundColor,
        double ForegroundOpacity,
        int ForegroundIdentity,
        int CultureLcid,
        TextAlignment TextAlignment,
        IValueConverter? TextConverter,
        object? TextConverterParameter,
        string? TextStringFormat,
        CultureInfo TextCulture);

    private sealed class DataGridVirtualRowValueCacheEntry
    {
        public object? Item { get; private set; }

        public int PlanVersion { get; private set; }

        public int ValueChangeVersion { get; private set; }

        public object?[] Values { get; private set; } = Array.Empty<object?>();

        public bool IsValid(
            object item,
            int planVersion,
            int valueChangeVersion,
            int valueCount)
        {
            return ReferenceEquals(Item, item) &&
                   PlanVersion == planVersion &&
                   ValueChangeVersion == valueChangeVersion &&
                   Values.Length == valueCount;
        }

        public void Prepare(
            object item,
            int planVersion,
            int valueChangeVersion,
            int valueCount)
        {
            if (Values.Length != valueCount)
            {
                Values = new object?[valueCount];
            }
            else
            {
                Array.Clear(Values);
            }

            Item = item;
            PlanVersion = planVersion;
            ValueChangeVersion = valueChangeVersion;
        }

        public void Clear()
        {
            Array.Clear(Values);
            Item = null;
            PlanVersion = 0;
            ValueChangeVersion = 0;
        }
    }

    private DataGridVirtualTextLayout GetVirtualTextLayout(
        string text,
        in DataGridVirtualColumnRenderPlan plan,
        double maxWidth,
        double maxHeight,
        ref DataGridVirtualSurfaceRenderCounters counters)
    {
        var key = new DataGridCustomDrawingTextLayoutCache.CacheKey(
            text,
            plan.FontFamilyName,
            plan.FontStyle,
            plan.FontWeight,
            plan.FontStretch,
            plan.FontSize,
            plan.TextAlignment,
            TextTrimming.CharacterEllipsis,
            FlowDirection.LeftToRight,
            plan.CultureLcid,
            maxWidth,
            maxHeight,
            plan.ForegroundKind,
            plan.ForegroundColor,
            plan.ForegroundOpacity,
            plan.ForegroundIdentity);

        if (_virtualTextLayoutCache.TryGet(key, out DataGridVirtualTextLayout? textLayout))
        {
            counters.TextLayoutCacheHits++;
            return textLayout;
        }

        counters.TextLayoutCacheMisses++;

        var layout = new TextLayout(
            text,
            new Typeface(plan.FontFamily, plan.FontStyle, plan.FontWeight, plan.FontStretch),
            plan.FontSize,
            plan.Foreground,
            plan.TextAlignment,
            TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis,
            flowDirection: FlowDirection.LeftToRight,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            maxLines: 1);
        return _virtualTextLayoutCache.Add(key, layout);
    }

    private static void DrawVirtualProgress(
        DrawingContext context,
        DataGridProgressBarColumn column,
        object? value,
        Rect bounds,
        IBrush fallbackForeground)
    {
        double height = double.IsNaN(column.Height) ? 4d : Math.Max(0d, column.Height);
        Rect bar = new(
            bounds.Left + VirtualCellHorizontalPadding,
            bounds.Top + Math.Max(0d, (bounds.Height - height) * 0.5d),
            Math.Max(0d, bounds.Width - (VirtualCellHorizontalPadding * 2d)),
            Math.Min(height, bounds.Height));
        context.DrawRectangle(column.Background ?? Brushes.Transparent, null, bar);

        double numeric = column.Minimum;
        if (value is IConvertible convertible)
        {
            try
            {
                numeric = convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
            }
        }

        double range = column.Maximum - column.Minimum;
        double ratio = range > 0d ? Math.Clamp((numeric - column.Minimum) / range, 0d, 1d) : 0d;
        context.DrawRectangle(
            column.Foreground ?? fallbackForeground,
            null,
            new Rect(bar.X, bar.Y, bar.Width * ratio, bar.Height));
    }

    private static void DrawVirtualCheckBox(
        DrawingContext context,
        DataGridCheckBoxColumn column,
        object? value,
        Rect bounds,
        Pen pen)
    {
        const double indicatorSize = 14d;
        double size = Math.Min(indicatorSize, Math.Max(0d, Math.Min(bounds.Width, bounds.Height) - 4d));
        if (size <= 0d)
        {
            return;
        }

        Rect indicator = new(
            bounds.Left + ((bounds.Width - size) * 0.5d),
            bounds.Top + ((bounds.Height - size) * 0.5d),
            size,
            size);
        context.DrawRectangle(null, pen, indicator.Deflate(0.75d));

        if (value is true)
        {
            double left = indicator.Left + (size * 0.22d);
            double middle = indicator.Left + (size * 0.43d);
            double right = indicator.Left + (size * 0.80d);
            double center = indicator.Top + (size * 0.52d);
            context.DrawLine(pen, new Point(left, center), new Point(middle, indicator.Top + (size * 0.72d)));
            context.DrawLine(pen, new Point(middle, indicator.Top + (size * 0.72d)), new Point(right, indicator.Top + (size * 0.28d)));
        }
        else if (value is null && column.IsThreeState)
        {
            double inset = size * 0.25d;
            double center = indicator.Top + (size * 0.5d);
            context.DrawLine(
                pen,
                new Point(indicator.Left + inset, center),
                new Point(indicator.Right - inset, center));
        }
    }

    private static void DrawVirtualComboBoxChevron(
        DrawingContext context,
        Rect bounds,
        Pen pen)
    {
        double centerX = bounds.Right - VirtualCellHorizontalPadding - 5d;
        double centerY = bounds.Top + (bounds.Height * 0.5d);
        context.DrawLine(
            pen,
            new Point(centerX - 3.5d, centerY - 1.75d),
            new Point(centerX, centerY + 1.75d));
        context.DrawLine(
            pen,
            new Point(centerX, centerY + 1.75d),
            new Point(centerX + 3.5d, centerY - 1.75d));
    }

    private static void DrawVirtualImage(
        DrawingContext context,
        DataGridImageColumn column,
        IImage image,
        Rect bounds)
    {
        Size source = image.Size;
        if (source.Width <= 0d || source.Height <= 0d)
        {
            return;
        }

        Rect content = bounds.Deflate(new Thickness(VirtualCellHorizontalPadding, 2d));
        double targetWidth = Math.Min(Math.Max(0d, column.ImageWidth), content.Width);
        double targetHeight = Math.Min(Math.Max(0d, column.ImageHeight), content.Height);
        double scaleX = targetWidth / source.Width;
        double scaleY = targetHeight / source.Height;
        double scale = column.Stretch switch
        {
            Stretch.None => 1d,
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY),
        };
        double width;
        double height;
        if (column.Stretch == Stretch.Fill)
        {
            width = source.Width * ApplyVirtualStretchDirection(scaleX, column.StretchDirection);
            height = source.Height * ApplyVirtualStretchDirection(scaleY, column.StretchDirection);
        }
        else
        {
            scale = ApplyVirtualStretchDirection(scale, column.StretchDirection);
            width = source.Width * scale;
            height = source.Height * scale;
        }
        Rect target = new(
            content.X + Math.Max(0d, (content.Width - width) * 0.5d),
            content.Y + Math.Max(0d, (content.Height - height) * 0.5d),
            width,
            height);
        context.DrawImage(image, new Rect(source), target);
    }

    private static double ApplyVirtualStretchDirection(double scale, StretchDirection direction) => direction switch
    {
        StretchDirection.UpOnly => Math.Max(1d, scale),
        StretchDirection.DownOnly => Math.Min(1d, scale),
        _ => scale,
    };

    private static int DrawVirtualExpander(
        DrawingContext context,
        HierarchicalNode node,
        double left,
        double top,
        double height,
        Pen pen)
    {
        if (node.IsLeaf)
        {
            return 0;
        }

        double centerX = left + (VirtualExpanderSize * 0.5d);
        double centerY = top + (height * 0.5d);
        if (node.IsExpanded)
        {
            context.DrawLine(pen, new Point(centerX - 4d, centerY - 2d), new Point(centerX, centerY + 2d));
            context.DrawLine(pen, new Point(centerX, centerY + 2d), new Point(centerX + 4d, centerY - 2d));
        }
        else
        {
            context.DrawLine(pen, new Point(centerX - 2d, centerY - 4d), new Point(centerX + 2d, centerY));
            context.DrawLine(pen, new Point(centerX + 2d, centerY), new Point(centerX - 2d, centerY + 4d));
        }

        return 2;
    }

    internal bool IsVirtualCellPoint(Point point) => TryHitVirtualCell(point, out _, out _);

    internal bool TryGetLightweightVirtualRowSlot(double y, out int slot)
    {
        slot = -1;
        if (OwningGrid?.DisplayData.HasVirtualScrollingElements != true ||
            _lightweightVirtualRows.Count == 0)
        {
            return false;
        }

        DataGridVirtualRowInfo firstRow = _lightweightVirtualRows[0];
        if (firstRow.Height <= 0d)
        {
            return false;
        }

        double relativeIndex = (y - firstRow.Top) / firstRow.Height;
        int index = relativeIndex <= 0d
            ? 0
            : relativeIndex >= _lightweightVirtualRows.Count
                ? _lightweightVirtualRows.Count - 1
                : (int)relativeIndex;
        slot = _lightweightVirtualRows[index].Slot;
        return true;
    }

    internal bool HandleVirtualCellPointerPressed(PointerPressedEventArgs e)
    {
        DataGrid? grid = OwningGrid;
        PointerPoint point = e.GetCurrentPoint(_virtualCellSurface);
        bool isTouchLike = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
        bool isPrimaryPressed = point.Properties.IsLeftButtonPressed ||
            (isTouchLike && grid?.AllowTouchDragSelection == true);
        if (grid?.UsesVirtualCellSurface != true ||
            (!isPrimaryPressed && !point.Properties.IsRightButtonPressed) ||
            !TryHitVirtualCell(e.GetPosition(_virtualCellSurface), out DataGridVirtualRowInfo row, out DataGridColumn? column))
        {
            return false;
        }

        Point position = e.GetPosition(_virtualCellSurface);
        if (isPrimaryPressed &&
            column is DataGridHierarchicalColumn &&
            row.Item is HierarchicalNode node &&
            !node.IsLeaf)
        {
            FlatColumnLayout layout = FindFlatColumnLayout(column);
            double expanderLeft = GetVirtualCellBounds(row, layout).Left +
                (Math.Max(0, node.Level) * ((DataGridHierarchicalColumn)column).Indent);
            if (position.X >= expanderLeft && position.X <= expanderLeft + VirtualExpanderSize)
            {
                return grid.TryToggleHierarchicalAtSlot(row.Slot);
            }
        }

        bool allowEdit = !grid.IsReadOnly && !column.IsReadOnly;
        bool handled = isPrimaryPressed
            ? grid.UpdateStateOnMouseLeftButtonDown(e, column.Index, row.Slot, allowEdit)
            : grid.UpdateStateOnMouseRightButtonDown(e, column.Index, row.Slot, allowEdit);
        if (grid.SuccessfullyUpdatedSelection)
        {
            if (grid.IsTabStop)
            {
                grid.Focus();
            }

            if (isPrimaryPressed &&
                !grid.ShouldSuppressSelectionDragFromRowDragHandle(column.Index))
            {
                grid.TryBeginSelectionDrag(e, column.Index, startDragging: true);
            }
        }

        return handled;
    }

    private bool TryHitVirtualCell(Point point, out DataGridVirtualRowInfo row, out DataGridColumn? column)
    {
        row = default;
        column = null;
        DataGrid? grid = OwningGrid;
        if (grid?.UsesVirtualCellSurface != true)
        {
            return false;
        }

        double headerWidth = grid.AreRowHeadersVisible ? grid.RowHeadersDesiredWidth : 0d;
        if (point.X < headerWidth)
        {
            return false;
        }

        if (grid.DisplayData.HasVirtualScrollingElements)
        {
            for (int index = 0; index < _lightweightVirtualRows.Count; index++)
            {
                if (TryHitVirtualRow(point, _lightweightVirtualRows[index], out column))
                {
                    row = _lightweightVirtualRows[index];
                    return true;
                }
            }
            return false;
        }

        foreach (DataGridRow candidate in grid.DisplayData.GetScrollingRows())
        {
            var candidateInfo = new DataGridVirtualRowInfo(
                candidate,
                candidate.Slot,
                candidate.Index,
                candidate.DataContext!,
                candidate.Bounds.Top,
                candidate.GetFlatCellsHeight());
            if (TryHitVirtualRow(point, candidateInfo, out column))
            {
                row = candidateInfo;
                return true;
            }
        }

        return false;
    }

    private bool TryHitVirtualRow(
        Point point,
        DataGridVirtualRowInfo candidate,
        out DataGridColumn? column)
    {
        column = null;
        if (point.Y < candidate.Top || point.Y >= candidate.Top + candidate.Height)
        {
            return false;
        }

        for (int layoutIndex = _flatColumnLayouts.Count - 1; layoutIndex >= 0; layoutIndex--)
        {
            FlatColumnLayout layout = _flatColumnLayouts[layoutIndex];
            if (!layout.ShouldDisplay)
            {
                continue;
            }

            if (GetVisibleVirtualCellBounds(candidate, layout).Contains(point))
            {
                column = layout.Column;
                return true;
            }
        }

        return false;
    }

    private FlatColumnLayout FindFlatColumnLayout(DataGridColumn column)
    {
        for (int index = 0; index < _flatColumnLayouts.Count; index++)
        {
            FlatColumnLayout layout = _flatColumnLayouts[index];
            if (ReferenceEquals(layout.Column, column))
            {
                return layout;
            }
        }

        return default;
    }

    private Rect GetVirtualCellBounds(
        DataGridVirtualRowInfo row,
        FlatColumnLayout layout)
    {
        return new Rect(
            _flatHeaderWidth + layout.Left,
            row.Top,
            layout.Column.LayoutRoundedWidth,
            row.Height);
    }

    private Rect GetVisibleVirtualCellBounds(
        DataGridVirtualRowInfo row,
        FlatColumnLayout layout)
    {
        return new Rect(
            _flatHeaderWidth + layout.VisibleLeft,
            row.Top,
            layout.VisibleWidth,
            row.Height);
    }

    internal bool TryGetVirtualCellBounds(DataGridRow row, DataGridColumn column, out Rect bounds)
    {
        bounds = default;
        DataGrid? grid = OwningGrid;
        if (grid?.UsesVirtualCellSurface != true)
        {
            return false;
        }

        FlatColumnLayout layout = FindFlatColumnLayout(column);
        if (!ReferenceEquals(layout.Column, column) || !layout.ShouldDisplay)
        {
            return false;
        }

        bounds = GetVisibleVirtualCellBounds(
            new DataGridVirtualRowInfo(
                row,
                row.Slot,
                row.Index,
                row.DataContext!,
                row.Bounds.Top,
                row.GetFlatCellsHeight()),
            layout);
        return bounds.Width > 0d && bounds.Height > 0d;
    }

    internal bool TryGetVirtualCellBounds(int slot, DataGridColumn column, out Rect bounds)
    {
        bounds = default;
        DataGrid? grid = OwningGrid;
        if (grid?.DisplayData.HasVirtualScrollingElements != true)
        {
            return false;
        }

        FlatColumnLayout layout = FindFlatColumnLayout(column);
        if (!ReferenceEquals(layout.Column, column) || !layout.ShouldDisplay)
        {
            return false;
        }

        for (int index = 0; index < _lightweightVirtualRows.Count; index++)
        {
            DataGridVirtualRowInfo row = _lightweightVirtualRows[index];
            if (row.Slot != slot)
            {
                continue;
            }

            bounds = GetVisibleVirtualCellBounds(row, layout);
            return bounds.Width > 0d && bounds.Height > 0d;
        }

        return false;
    }

    private void SyncVirtualValueNotifiers()
    {
        DataGrid? grid = OwningGrid;
        if (grid?.UsesVirtualCellSurface != true)
        {
            ClearVirtualValueNotifiers();
            return;
        }

        if (!ShouldTrackVirtualValueChanges())
        {
            ClearVirtualValueNotifiers();
            return;
        }

        _nextVirtualValueNotifiers.Clear();
        if (grid.DisplayData.HasVirtualScrollingElements)
        {
            for (int index = 0; index < _lightweightVirtualRows.Count; index++)
            {
                AddVirtualValueNotifier(_lightweightVirtualRows[index].Item);
            }
        }
        else
        {
            foreach (DataGridRow row in grid.DisplayData.GetScrollingRows())
            {
                AddVirtualValueNotifier(row.DataContext);
            }
        }

        foreach (INotifyPropertyChanged notifier in _virtualValueNotifiers)
        {
            if (!_nextVirtualValueNotifiers.Contains(notifier))
            {
                notifier.PropertyChanged -= OnVirtualValuePropertyChanged;
            }
        }

        foreach (INotifyPropertyChanged notifier in _nextVirtualValueNotifiers)
        {
            if (!_virtualValueNotifiers.Contains(notifier))
            {
                notifier.PropertyChanged += OnVirtualValuePropertyChanged;
            }
        }

        (_virtualValueNotifiers, _nextVirtualValueNotifiers) =
            (_nextVirtualValueNotifiers, _virtualValueNotifiers);
    }

    private void AddVirtualValueNotifier(object? item)
    {
        if (item is INotifyPropertyChanged rowNotifier)
        {
            _nextVirtualValueNotifiers.Add(rowNotifier);
        }

        if (item is HierarchicalNode node &&
            node.Item is INotifyPropertyChanged itemNotifier)
        {
            _nextVirtualValueNotifiers.Add(itemNotifier);
        }
    }

    private bool ShouldTrackVirtualValueChanges()
    {
        for (int index = 0; index < _flatColumnLayouts.Count; index++)
        {
            DataGridColumn column = _flatColumnLayouts[index].Column;
            if (column is IDataGridDrawnCellValueChangeTracking tracking)
            {
                if (tracking.TrackDrawnCellValueChanges)
                {
                    return true;
                }

                continue;
            }

            if (column is DataGridHierarchicalColumn hierarchicalColumn)
            {
                if (hierarchicalColumn.TrackDirectTextValueChanges)
                {
                    return true;
                }

                continue;
            }

            return true;
        }

        return false;
    }

    private void ClearVirtualValueNotifiers()
    {
        foreach (INotifyPropertyChanged notifier in _virtualValueNotifiers)
        {
            notifier.PropertyChanged -= OnVirtualValuePropertyChanged;
        }

        _virtualValueNotifiers.Clear();
        _nextVirtualValueNotifiers.Clear();
    }

    private void OnVirtualValuePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Interlocked.Increment(ref _virtualValueChangeCount);
        if (Dispatcher.UIThread.CheckAccess())
        {
            InvalidateVirtualCellSurface();
        }
        else
        {
            Dispatcher.UIThread.Post(InvalidateVirtualCellSurface, DispatcherPriority.Render);
        }
    }

    private void ResolveVirtualTextStyle(out FontFamily fontFamily, out double fontSize, out IBrush foreground)
    {
        fontFamily = _virtualCellSurface?.GetValue(TextElement.FontFamilyProperty) ?? FontFamily.Default;
        fontSize = FindVirtualResource<double>("DataGridCellFontSize");
        if (!double.IsFinite(fontSize) || fontSize <= 0d)
        {
            fontSize = 14d;
        }

        foreground = _virtualCellSurface?.GetValue(TextElement.ForegroundProperty) ?? Brushes.Black;
    }

    private T? FindVirtualResource<T>(string key)
    {
        return OwningGrid?.TryFindResource(key, out object? resource) == true && resource is T value
            ? value
            : default;
    }

    private static void GetBrushIdentity(
        IBrush brush,
        out byte kind,
        out Color color,
        out double opacity,
        out int identity)
    {
        if (brush is ISolidColorBrush solid)
        {
            kind = 1;
            color = solid.Color;
            opacity = solid.Opacity;
            identity = 0;
            return;
        }

        kind = 2;
        color = default;
        opacity = brush.Opacity;
        identity = RuntimeHelpers.GetHashCode(brush);
    }
}
