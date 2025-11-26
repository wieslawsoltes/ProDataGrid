// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Controls.Primitives;
using Avalonia.Controls.Utils;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Utilities;
using Avalonia.VisualTree;
using System.Diagnostics;
using System;

namespace Avalonia.Controls
{
    partial class DataGrid
    {
        /// <summary>
        /// Scrolls the DataGrid according to the direction of the delta.
        /// </summary>
        /// <param name="e">PointerWheelEventArgs</param>
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            var delta = e.Delta;

            // KeyModifiers.Shift should scroll in horizontal direction. This does not work on every platform.
            // If Shift-Key is pressed and X is close to 0 we swap the Vector.
            if (e.KeyModifiers == KeyModifiers.Shift && MathUtilities.IsZero(delta.X))
            {
                delta = new Vector(delta.Y, delta.X);
            }

            if(UpdateScroll(delta * DATAGRID_mouseWheelDelta))
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = e.Handled || !ScrollViewer.GetIsScrollChainingEnabled(this);
            }
        }

        internal bool UpdateScroll(Vector delta)
        {
            if (IsEnabled && DisplayData.NumDisplayedScrollingElements > 0)
            {
                var handled = false;
                var ignoreInvalidate = false;
                var scrollHeight = 0d;

                // Vertical scroll handling
                if (delta.Y > 0)
                {
                    scrollHeight = Math.Max(-_verticalOffset, -delta.Y);
                }
                else if (delta.Y < 0)
                {
                    if (HasLegacyVerticalScrollBar && VerticalScrollBarVisibility == ScrollBarVisibility.Visible)
                    {
                        scrollHeight = Math.Min(Math.Max(0, GetLegacyVerticalScrollMaximum() - _verticalOffset), -delta.Y);
                    }
                    else
                    {
                        double maximum = EdgedRowsHeightCalculated - CellsEstimatedHeight;
                        scrollHeight = Math.Min(Math.Max(0, maximum - _verticalOffset), -delta.Y);
                    }
                }

                if (scrollHeight != 0)
                {
                    // Accumulate scroll height to handle rapid scroll events
                    DisplayData.PendingVerticalScrollHeight += scrollHeight;
                    handled = true;

                    var eventType = scrollHeight > 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement;
                    VerticalScroll?.Invoke(this, new ScrollEventArgs(eventType, scrollHeight));
                }

                // Horizontal scroll handling
                if (delta.X != 0)
                {
                    var horizontalOffset = HorizontalOffset - delta.X;
                    var widthNotVisible = Math.Max(0, ColumnsInternal.VisibleEdgedColumnsWidth - CellsWidth);

                    if (horizontalOffset < 0)
                    {
                        horizontalOffset = 0;
                    }
                    if (horizontalOffset > widthNotVisible)
                    {
                        horizontalOffset = widthNotVisible;
                    }

                    if (UpdateHorizontalOffset(horizontalOffset))
                    {
                        // We don't need to invalidate once again after UpdateHorizontalOffset.
                        ignoreInvalidate = true;
                        handled = true;

                        var eventType = horizontalOffset > 0 ? ScrollEventType.SmallIncrement : ScrollEventType.SmallDecrement;
                        HorizontalScroll?.Invoke(this, new ScrollEventArgs(eventType, horizontalOffset));
                    }
                }

                if (handled)
                {
                    if (!ignoreInvalidate)
                    {
                        InvalidateRowsMeasure(invalidateIndividualElements: false);
                    }
                    return true;
                }
            }

            return false;
        }

        //TODO: Ensure left button is checked for
        internal bool UpdateStateOnMouseLeftButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit)
        {
            KeyboardHelper.GetMetaKeyState(this, pointerPressedEventArgs.KeyModifiers, out bool ctrl, out bool shift);
            return UpdateStateOnMouseLeftButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl);
        }

        //TODO: Ensure right button is checked for
        internal bool UpdateStateOnMouseRightButtonDown(PointerPressedEventArgs pointerPressedEventArgs, int columnIndex, int slot, bool allowEdit)
        {
            KeyboardHelper.GetMetaKeyState(this, pointerPressedEventArgs.KeyModifiers, out bool ctrl, out bool shift);
            return UpdateStateOnMouseRightButtonDown(pointerPressedEventArgs, columnIndex, slot, allowEdit, shift, ctrl);
        }

        //TODO: Check
        private void DataGrid_IsEnabledChanged(AvaloniaPropertyChangedEventArgs e)
        {
        }

        /// <summary>
        /// Raises the CellPointerPressed event.
        /// </summary>
        internal virtual void OnCellPointerPressed(DataGridCellPointerPressedEventArgs e)
        {
            CellPointerPressed?.Invoke(this, e);
        }

    }
}
