// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Media;

namespace Avalonia.Controls
{
    internal interface IDataGridDrawnCellValueProvider
    {
        object GetDrawnCellValue(object item);
    }

    internal interface IDataGridDrawnCellValueChangeTracking
    {
        bool TrackDrawnCellValueChanges { get; }
    }

    internal interface IDataGridBuiltInCellRenderer
    {
        bool TryMeasure(DataGridCustomDrawingCell cell, Size availableSize, out Size desiredSize);

        void Render(DataGridCustomDrawingCell cell, DrawingContext context);
    }

    internal sealed class DataGridProgressCellRenderer : IDataGridBuiltInCellRenderer
    {
        internal static DataGridProgressCellRenderer Instance { get; } = new DataGridProgressCellRenderer();

        private DataGridProgressCellRenderer()
        {
        }

        public bool TryMeasure(DataGridCustomDrawingCell cell, Size availableSize, out Size desiredSize)
        {
            var column = (DataGridProgressBarColumn)cell.OwningColumn;
            var height = double.IsNaN(column.Height) ? 4d : Math.Max(0d, column.Height);
            var padding = cell.Padding;
            desiredSize = new Size(
                Math.Min(padding.Left + padding.Right, availableSize.Width),
                Math.Min(height + padding.Top + padding.Bottom, availableSize.Height));
            return true;
        }

        public void Render(DataGridCustomDrawingCell cell, DrawingContext context)
        {
            var column = (DataGridProgressBarColumn)cell.OwningColumn;
            var barBounds = GetBarBounds(cell, column);
            var background = column.Background ?? Brushes.Transparent;
            var foreground = column.Foreground ?? cell.Foreground ?? Brushes.DodgerBlue;

            context.DrawRectangle(background, null, barBounds);

            var value = TryConvertDouble(cell.Value, out var numericValue) ? numericValue : column.Minimum;
            var range = column.Maximum - column.Minimum;
            var ratio = range > 0d ? Math.Clamp((value - column.Minimum) / range, 0d, 1d) : 0d;
            if (ratio > 0d && barBounds.Width > 0d)
            {
                context.DrawRectangle(
                    foreground,
                    null,
                    new Rect(barBounds.X, barBounds.Y, barBounds.Width * ratio, barBounds.Height));
            }
        }

        internal static Rect GetBarBounds(DataGridCustomDrawingCell cell, DataGridProgressBarColumn column)
        {
            var padding = cell.Padding;
            var bounds = new Rect(
                padding.Left,
                padding.Top,
                Math.Max(0d, cell.Bounds.Width - padding.Left - padding.Right),
                Math.Max(0d, cell.Bounds.Height - padding.Top - padding.Bottom));
            var height = double.IsNaN(column.Height)
                ? Math.Min(4d, bounds.Height)
                : Math.Min(Math.Max(0d, column.Height), bounds.Height);
            return new Rect(
                bounds.X,
                bounds.Y + Math.Max(0d, (bounds.Height - height) * 0.5d),
                bounds.Width,
                Math.Max(0d, height));
        }

        private static bool TryConvertDouble(object value, out double result)
        {
            if (value is double doubleValue)
            {
                result = doubleValue;
                return true;
            }

            if (value is IConvertible convertible)
            {
                try
                {
                    result = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            result = 0d;
            return false;
        }
    }

    internal sealed class DataGridImageCellRenderer : IDataGridBuiltInCellRenderer
    {
        internal static DataGridImageCellRenderer Instance { get; } = new DataGridImageCellRenderer();

        private DataGridImageCellRenderer()
        {
        }

        public bool TryMeasure(DataGridCustomDrawingCell cell, Size availableSize, out Size desiredSize)
        {
            var column = (DataGridImageColumn)cell.OwningColumn;
            var padding = cell.Padding;
            desiredSize = new Size(
                Math.Min(Math.Max(0d, column.ImageWidth) + padding.Left + padding.Right, availableSize.Width),
                Math.Min(Math.Max(0d, column.ImageHeight) + padding.Top + padding.Bottom, availableSize.Height));
            return true;
        }

        public void Render(DataGridCustomDrawingCell cell, DrawingContext context)
        {
            if (cell.Value is not IImage image)
            {
                return;
            }

            var column = (DataGridImageColumn)cell.OwningColumn;
            var sourceSize = image.Size;
            if (sourceSize.Width <= 0d || sourceSize.Height <= 0d)
            {
                return;
            }

            var padding = cell.Padding;
            var contentBounds = new Rect(
                padding.Left,
                padding.Top,
                Math.Max(0d, cell.Bounds.Width - padding.Left - padding.Right),
                Math.Max(0d, cell.Bounds.Height - padding.Top - padding.Bottom));
            var target = new Size(
                Math.Min(Math.Max(0d, column.ImageWidth), contentBounds.Width),
                Math.Min(Math.Max(0d, column.ImageHeight), contentBounds.Height));
            var targetViewport = new Rect(
                contentBounds.X + (contentBounds.Width - target.Width) * 0.5d,
                contentBounds.Y + (contentBounds.Height - target.Height) * 0.5d,
                target.Width,
                target.Height);
            var scaleX = target.Width / sourceSize.Width;
            var scaleY = target.Height / sourceSize.Height;
            var scale = column.Stretch switch
            {
                Stretch.None => 1d,
                Stretch.UniformToFill => Math.Max(scaleX, scaleY),
                _ => Math.Min(scaleX, scaleY)
            };

            double width;
            double height;
            if (column.Stretch == Stretch.Fill)
            {
                width = sourceSize.Width * ApplyStretchDirection(scaleX, column.StretchDirection);
                height = sourceSize.Height * ApplyStretchDirection(scaleY, column.StretchDirection);
            }
            else
            {
                scale = ApplyStretchDirection(scale, column.StretchDirection);
                width = sourceSize.Width * scale;
                height = sourceSize.Height * scale;
            }

            var destination = new Rect(
                targetViewport.X + (targetViewport.Width - width) * 0.5d,
                targetViewport.Y + (targetViewport.Height - height) * 0.5d,
                Math.Max(0d, width),
                Math.Max(0d, height));
            using (context.PushClip(targetViewport))
            {
                context.DrawImage(image, new Rect(sourceSize), destination);
            }
        }

        private static double ApplyStretchDirection(double scale, StretchDirection direction)
        {
            return direction switch
            {
                StretchDirection.UpOnly => Math.Max(1d, scale),
                StretchDirection.DownOnly => Math.Min(1d, scale),
                _ => scale
            };
        }
    }
}
