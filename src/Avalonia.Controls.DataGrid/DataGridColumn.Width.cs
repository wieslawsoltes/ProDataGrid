// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System;

namespace Avalonia.Controls
{
    abstract partial class DataGridColumn
    {
        /// <summary>
        /// Coerces a DataGridLength to a valid value.  If any value components are double.NaN, this method
        /// coerces them to a proper initial value.  For star columns, the desired width is calculated based
        /// on the rest of the star columns.  For pixel widths, the desired value is based on the pixel value.
        /// For auto widths, the desired value is initialized as the column's minimum width.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width">The DataGridLength to coerce.</param>
        /// <returns>The resultant (coerced) DataGridLength.</returns>
        private static DataGridLength CoerceWidth(AvaloniaObject source, DataGridLength width)
        {
            var target = (DataGridColumn)source;

            if (target._setWidthInternalNoCallback)
            {
                return width;
            }

            if (!target.IsSet(WidthProperty))
            {

                return target.OwningGrid?.ColumnWidth ??
                DataGridLength.Auto;
            }

            double desiredValue = width.DesiredValue;
            if (double.IsNaN(desiredValue))
            {
                if (width.IsStar && target.OwningGrid != null && target.OwningGrid.ColumnsInternal != null)
                {
                    double totalStarValues = 0;
                    double totalStarDesiredValues = 0;
                    double totalNonStarDisplayWidths = 0;
                    foreach (DataGridColumn column in target.OwningGrid.ColumnsInternal.GetDisplayedColumns(c => c.IsVisible && c != target && !double.IsNaN(c.Width.DesiredValue)))
                    {
                        if (column.Width.IsStar)
                        {
                            totalStarValues += column.Width.Value;
                            totalStarDesiredValues += column.Width.DesiredValue;
                        }
                        else
                        {
                            totalNonStarDisplayWidths += column.ActualWidth;
                        }
                    }
                    if (totalStarValues == 0)
                    {
                        // Compute the new star column's desired value based on the available space if there are no other visible star columns
                        desiredValue = Math.Max(target.ActualMinWidth, target.OwningGrid.CellsWidth - totalNonStarDisplayWidths);
                    }
                    else
                    {
                        // Otherwise, compute its desired value based on those of other visible star columns
                        desiredValue = totalStarDesiredValues * width.Value / totalStarValues;
                    }
                }
                else if (width.IsAbsolute)
                {
                    desiredValue = width.Value;
                }
                else
                {
                    desiredValue = target.ActualMinWidth;
                }
            }

            double displayValue = width.DisplayValue;
            if (double.IsNaN(displayValue))
            {
                displayValue = desiredValue;
            }
            displayValue = Math.Max(target.ActualMinWidth, Math.Min(target.ActualMaxWidth, displayValue));

            return new DataGridLength(width.Value, width.UnitType, desiredValue, displayValue);
        }

        /// <summary>
        /// If the DataGrid is using layout rounding, the pixel snapping will force all widths to
        /// whole numbers. Since the column widths aren't visual elements, they don't go through the normal
        /// rounding process, so we need to do it ourselves.  If we don't, then we'll end up with some
        /// pixel gaps and/or overlaps between columns.
        /// </summary>
        /// <param name="leftEdge"></param>
        internal void ComputeLayoutRoundedWidth(double leftEdge)
        {
            if (OwningGrid != null && OwningGrid.UseLayoutRounding)
            {
                var scale = LayoutHelper.GetLayoutScale(HeaderCell);
                var roundSize = LayoutHelper.RoundLayoutSizeUp(new Size(leftEdge + ActualWidth, 1), scale);
                LayoutRoundedWidth = roundSize.Width - leftEdge;
            }
            else
            {
                LayoutRoundedWidth = ActualWidth;
            }
        }

        /// <summary>
        /// Ensures that this column's width has been coerced to a valid value.
        /// </summary>
        internal void EnsureWidth()
        {
            SetWidthInternalNoCallback(CoerceWidth(this, Width));
        }

        /// <summary>
        /// Attempts to resize the column's width to the desired DisplayValue, but limits the final size
        /// to the column's minimum and maximum values.  If star sizing is being used, then the column
        /// can only decrease in size by the amount that the columns after it can increase in size.
        /// Likewise, the column can only increase in size if other columns can spare the width.
        /// </summary>
        /// <param name="oldWidth">with before resize.</param>
        /// <param name="newWidth">with after resize.</param>
        /// <param name="userInitiated">Whether or not this resize was initiated by a user action.</param>

        //  double value, DataGridLengthUnitType unitType, double desiredValue, double displayValue
        internal void Resize(DataGridLength oldWidth, DataGridLength newWidth, bool userInitiated)
        {
            double newValue = newWidth.Value;
            double newDesiredValue = newWidth.DesiredValue;
            double newDisplayValue = Math.Max(ActualMinWidth, Math.Min(ActualMaxWidth, newWidth.DisplayValue));
            DataGridLengthUnitType newUnitType = newWidth.UnitType;

            int starColumnsCount = 0;
            double totalDisplayWidth = 0;
            foreach (DataGridColumn column in OwningGrid.ColumnsInternal.GetVisibleColumns())
            {
                column.EnsureWidth();
                totalDisplayWidth += column.ActualWidth;
                starColumnsCount += (column != this && column.Width.IsStar) ? 1 : 0;
            }
            bool hasInfiniteAvailableWidth = !OwningGrid.RowsPresenterAvailableSize.HasValue || double.IsPositiveInfinity(OwningGrid.RowsPresenterAvailableSize.Value.Width);

            // If we're using star sizing, we can only resize the column as much as the columns to the
            // right will allow (i.e. until they hit their max or min widths).
            if (!hasInfiniteAvailableWidth && (starColumnsCount > 0 || (newUnitType == DataGridLengthUnitType.Star && newWidth.IsStar && userInitiated)))
            {
                double limitedDisplayValue = oldWidth.DisplayValue;
                double availableIncrease = Math.Max(0, OwningGrid.CellsWidth - totalDisplayWidth);
                double desiredChange = newDisplayValue - oldWidth.DisplayValue;
                if (desiredChange > availableIncrease)
                {
                    // The desired change is greater than the amount of available space,
                    // so we need to decrease the widths of columns to the right to make room.
                    desiredChange -= availableIncrease;
                    double actualChange = desiredChange + OwningGrid.DecreaseColumnWidths(DisplayIndex + 1, -desiredChange, userInitiated);
                    limitedDisplayValue += availableIncrease + actualChange;
                }
                else if (desiredChange > 0)
                {
                    // The desired change is positive but less than the amount of available space,
                    // so there's no need to decrease the widths of columns to the right.
                    limitedDisplayValue += desiredChange;
                }
                else
                {
                    // The desired change is negative, so we need to increase the widths of columns to the right.
                    limitedDisplayValue += desiredChange + OwningGrid.IncreaseColumnWidths(DisplayIndex + 1, -desiredChange, userInitiated);
                }
                if (ActualCanUserResize || (oldWidth.IsStar && !userInitiated))
                {
                    newDisplayValue = limitedDisplayValue;
                }
            }

            if (userInitiated)
            {
                newDesiredValue = newDisplayValue;
                if (!Width.IsStar)
                {
                    InheritsWidth = false;
                    newValue = newDisplayValue;
                    newUnitType = DataGridLengthUnitType.Pixel;
                }
                else if (starColumnsCount > 0 && !hasInfiniteAvailableWidth)
                {
                    // Recalculate star weight of this column based on the new desired value
                    InheritsWidth = false;
                    newValue = Width.Value * newDisplayValue / ActualWidth;
                }
            }

            newDisplayValue = Math.Min(double.MaxValue, newValue);
            newWidth = new DataGridLength(newDisplayValue, newUnitType, newDesiredValue, newDisplayValue);
            SetWidthInternalNoCallback(newWidth);
            if (newWidth != oldWidth)
            {
                OwningGrid.OnColumnWidthChanged(this);
            }
        }

        /// <summary>
        /// Sets the column's Width to a new DataGridLength with a different DesiredValue.
        /// </summary>
        /// <param name="desiredValue">The new DesiredValue.</param>
        internal void SetWidthDesiredValue(double desiredValue)
        {
            SetWidthInternalNoCallback(new DataGridLength(Width.Value, Width.UnitType, desiredValue, Width.DisplayValue));
        }

        /// <summary>
        /// Sets the column's Width to a new DataGridLength with a different DisplayValue.
        /// </summary>
        /// <param name="displayValue">The new DisplayValue.</param>
        internal void SetWidthDisplayValue(double displayValue)
        {
            SetWidthInternalNoCallback(new DataGridLength(Width.Value, Width.UnitType, Width.DesiredValue, displayValue));
        }

        /// <summary>
        /// Set the column's Width without breaking inheritance.
        /// </summary>
        /// <param name="width">The new Width.</param>
        internal void SetWidthInternal(DataGridLength width)
        {
            bool originalValue = _settingWidthInternally;
            _settingWidthInternally = true;
            try
            {
                SetCurrentValue(WidthProperty, width);
            }
            finally
            {
                _settingWidthInternally = originalValue;
            }
        }

        /// <summary>
        /// Sets the column's Width directly, without any callback effects.
        /// </summary>
        /// <param name="width">The new Width.</param>
        internal void SetWidthInternalNoCallback(DataGridLength width)
        {
            var originalValue = _setWidthInternalNoCallback;
            _setWidthInternalNoCallback = true;
            try
            {
                SetCurrentValue(WidthProperty, width);
            }
            finally
            {
                _setWidthInternalNoCallback = originalValue;
            }

        }

        /// <summary>
        /// Set the column's star value.  Whenever the star value changes, width inheritance is broken.
        /// </summary>
        /// <param name="value">The new star value.</param>
        internal void SetWidthStarValue(double value)
        {
            InheritsWidth = false;
            SetWidthInternalNoCallback(new DataGridLength(value, Width.UnitType, Width.DesiredValue, Width.DisplayValue));
        }

    }
}
