// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Lightweight cell content control that supports direct text rendering and custom draw operations.
    /// </summary>
    public
#else
    internal
#endif
    sealed class DataGridCustomDrawingCell : Control
    {
        internal const int DefaultSharedTextLayoutCacheCapacity = 1024;

        private object _value;
        private string _text;
        private FormattedText _formattedText;
        private Size? _availableSize;
        private DataGridCustomDrawingTextLayoutCache _sharedTextLayoutCache;

        public static readonly DirectProperty<DataGridCustomDrawingCell, object> ValueProperty =
            AvaloniaProperty.RegisterDirect<DataGridCustomDrawingCell, object>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v);

        public static readonly StyledProperty<IDataGridCellDrawOperationFactory> DrawOperationFactoryProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, IDataGridCellDrawOperationFactory>(
                nameof(DrawOperationFactory));

        public static readonly StyledProperty<DataGridCustomDrawingMode> DrawingModeProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, DataGridCustomDrawingMode>(
                nameof(DrawingMode),
                DataGridCustomDrawingMode.Text);

        public static readonly AttachedProperty<FontFamily> FontFamilyProperty =
            TextElement.FontFamilyProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly AttachedProperty<double> FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly AttachedProperty<FontStyle> FontStyleProperty =
            TextElement.FontStyleProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly AttachedProperty<FontWeight> FontWeightProperty =
            TextElement.FontWeightProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly AttachedProperty<FontStretch> FontStretchProperty =
            TextElement.FontStretchProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly AttachedProperty<IBrush> ForegroundProperty =
            TextElement.ForegroundProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
            TextBlock.TextAlignmentProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly StyledProperty<TextTrimming> TextTrimmingProperty =
            TextBlock.TextTrimmingProperty.AddOwner<DataGridCustomDrawingCell>();

        public static readonly StyledProperty<DataGridCustomDrawingTextLayoutCacheMode> TextLayoutCacheModeProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, DataGridCustomDrawingTextLayoutCacheMode>(
                nameof(TextLayoutCacheMode),
                DataGridCustomDrawingTextLayoutCacheMode.PerCell);

        public static readonly StyledProperty<int> SharedTextLayoutCacheCapacityProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, int>(
                nameof(SharedTextLayoutCacheCapacity),
                DefaultSharedTextLayoutCacheCapacity);

        public static readonly StyledProperty<bool> DrawOperationLayoutFastPathProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, bool>(
                nameof(DrawOperationLayoutFastPath),
                false);

        static DataGridCustomDrawingCell()
        {
            AffectsRender<DataGridCustomDrawingCell>(ForegroundProperty);
        }

        /// <summary>
        /// Gets or sets the bound value used for rendering.
        /// </summary>
        public object Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }

        /// <summary>
        /// Gets or sets the factory that creates custom draw operations.
        /// </summary>
        public IDataGridCellDrawOperationFactory DrawOperationFactory
        {
            get => GetValue(DrawOperationFactoryProperty);
            set => SetValue(DrawOperationFactoryProperty, value);
        }

        /// <summary>
        /// Gets or sets the rendering mode for this cell.
        /// </summary>
        public DataGridCustomDrawingMode DrawingMode
        {
            get => GetValue(DrawingModeProperty);
            set => SetValue(DrawingModeProperty, value);
        }

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public FontStretch FontStretch
        {
            get => GetValue(FontStretchProperty);
            set => SetValue(FontStretchProperty, value);
        }

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public TextTrimming TextTrimming
        {
            get => GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        /// <summary>
        /// Gets or sets text layout cache mode for this cell.
        /// </summary>
        public DataGridCustomDrawingTextLayoutCacheMode TextLayoutCacheMode
        {
            get => GetValue(TextLayoutCacheModeProperty);
            set => SetValue(TextLayoutCacheModeProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum shared text layout cache entry count.
        /// </summary>
        public int SharedTextLayoutCacheCapacity
        {
            get => GetValue(SharedTextLayoutCacheCapacityProperty);
            set => SetValue(SharedTextLayoutCacheCapacityProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether draw-operation layout fast path is enabled.
        /// </summary>
        public bool DrawOperationLayoutFastPath
        {
            get => GetValue(DrawOperationLayoutFastPathProperty);
            set => SetValue(DrawOperationLayoutFastPathProperty, value);
        }

        internal DataGridCell OwningCell { get; set; }

        internal DataGridCustomDrawingTextLayoutCache SharedTextLayoutCache
        {
            get => _sharedTextLayoutCache;
            set
            {
                if (ReferenceEquals(_sharedTextLayoutCache, value))
                {
                    return;
                }

                _sharedTextLayoutCache = value;
                ApplySharedCacheCapacity();
                InvalidateTextLayout();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = GetDisplayText();
            if (TryMeasureWithDrawOperationFastPath(availableSize, text, out Size fastPathSize))
            {
                return fastPathSize;
            }

            if (string.IsNullOrEmpty(text))
            {
                return default;
            }

            EnsureFormattedText(availableSize, text);
            if (_formattedText is null)
            {
                return default;
            }

            return new Size(_formattedText.Width, _formattedText.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = GetDisplayText();
            if (TryArrangeWithDrawOperationFastPath(finalSize, text, out Size arrangedSize))
            {
                return arrangedSize;
            }

            return base.ArrangeOverride(finalSize);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var text = GetDisplayText();
            var shouldDrawOperation = DrawOperationFactory != null &&
                                      (DrawingMode == DataGridCustomDrawingMode.DrawOperation ||
                                       DrawingMode == DataGridCustomDrawingMode.TextAndDrawOperation);
            var shouldDrawText = DrawingMode == DataGridCustomDrawingMode.Text ||
                                 DrawingMode == DataGridCustomDrawingMode.TextAndDrawOperation ||
                                 !shouldDrawOperation;

            if (shouldDrawText && !string.IsNullOrEmpty(text))
            {
                EnsureFormattedText(Bounds.Size, text);
                if (_formattedText != null)
                {
                    var y = Math.Max(0, (Bounds.Height - _formattedText.Height) * 0.5);
                    context.DrawText(_formattedText, new Point(0, y));
                }
            }

            if (!shouldDrawOperation)
            {
                return;
            }

            var drawContext = CreateDrawOperationContext(text);
            var drawOperation = DrawOperationFactory.CreateDrawOperation(drawContext);
            if (drawOperation != null)
            {
                context.Custom(drawOperation);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty ||
                change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == FontStyleProperty ||
                change.Property == FontWeightProperty ||
                change.Property == FontStretchProperty ||
                change.Property == TextAlignmentProperty ||
                change.Property == TextTrimmingProperty ||
                change.Property == FlowDirectionProperty ||
                change.Property == TextLayoutCacheModeProperty ||
                change.Property == DrawOperationLayoutFastPathProperty)
            {
                InvalidateTextLayout();
                InvalidateMeasure();
                InvalidateVisual();
            }
            else if (change.Property == SharedTextLayoutCacheCapacityProperty)
            {
                ApplySharedCacheCapacity();
                InvalidateTextLayout();
                InvalidateMeasure();
                InvalidateVisual();
            }
            else if (change.Property == DrawOperationFactoryProperty ||
                     change.Property == DrawingModeProperty)
            {
                InvalidateTextLayout();
                InvalidateMeasure();
                InvalidateVisual();
            }
            else if (change.Property == ForegroundProperty)
            {
                InvalidateTextLayout();
                InvalidateVisual();
            }
        }

        private DataGridCellDrawOperationContext CreateDrawOperationContext(string text)
        {
            ResolveCellState(out DataGridCell cell, out IBrush foreground, out Typeface typeface, out bool isCurrent, out bool isSelected);

            return new DataGridCellDrawOperationContext(
                cell,
                cell?.OwningColumn,
                DataContext,
                Value,
                text,
                new Rect(Bounds.Size),
                foreground,
                typeface,
                FontSize,
                isCurrent,
                isSelected);
        }

        private bool TryMeasureWithDrawOperationFastPath(Size availableSize, string text, out Size desiredSize)
        {
            desiredSize = default;

            if (!ShouldUseDrawOperationLayoutFastPath ||
                DrawOperationFactory is not IDataGridCellDrawOperationMeasureProvider measureProvider)
            {
                return false;
            }

            var measureContext = CreateMeasureContext(text, availableSize);
            if (!measureProvider.TryMeasure(measureContext, out desiredSize))
            {
                return false;
            }

            desiredSize = NormalizeLayoutSize(desiredSize, availableSize);
            _text = text;
            _availableSize = availableSize;
            _formattedText = null;
            return true;
        }

        private bool TryArrangeWithDrawOperationFastPath(Size finalSize, string text, out Size arrangedSize)
        {
            arrangedSize = default;

            if (!ShouldUseDrawOperationLayoutFastPath ||
                DrawOperationFactory is not IDataGridCellDrawOperationArrangeProvider arrangeProvider)
            {
                return false;
            }

            var arrangeContext = CreateArrangeContext(text, finalSize);
            if (!arrangeProvider.TryArrange(arrangeContext, out arrangedSize))
            {
                return false;
            }

            arrangedSize = NormalizeLayoutSize(arrangedSize, finalSize);
            return true;
        }

        private DataGridCellDrawOperationMeasureContext CreateMeasureContext(string text, Size availableSize)
        {
            ResolveCellState(out DataGridCell cell, out IBrush foreground, out Typeface typeface, out bool isCurrent, out bool isSelected);
            return new DataGridCellDrawOperationMeasureContext(
                cell,
                cell?.OwningColumn,
                DataContext,
                Value,
                text,
                availableSize,
                foreground,
                typeface,
                FontSize,
                TextAlignment,
                TextTrimming,
                FlowDirection,
                isCurrent,
                isSelected);
        }

        private DataGridCellDrawOperationArrangeContext CreateArrangeContext(string text, Size finalSize)
        {
            ResolveCellState(out DataGridCell cell, out IBrush foreground, out Typeface typeface, out bool isCurrent, out bool isSelected);
            return new DataGridCellDrawOperationArrangeContext(
                cell,
                cell?.OwningColumn,
                DataContext,
                Value,
                text,
                finalSize,
                foreground,
                typeface,
                FontSize,
                TextAlignment,
                TextTrimming,
                FlowDirection,
                isCurrent,
                isSelected);
        }

        private void ResolveCellState(
            out DataGridCell cell,
            out IBrush foreground,
            out Typeface typeface,
            out bool isCurrent,
            out bool isSelected)
        {
            cell = OwningCell;
            foreground = Foreground ?? Brushes.Black;
            typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            isCurrent = cell is { OwningGrid: { }, OwningRow: { }, OwningColumn: { } } && cell.IsCurrent;
            isSelected = false;

            if (cell is { OwningGrid: { } owningGrid, OwningRow: { } owningRow, OwningColumn: { } owningColumn } &&
                owningRow.Slot >= 0)
            {
                isSelected = owningGrid.SelectionUnit == DataGridSelectionUnit.FullRow
                    ? owningRow.IsSelected
                    : owningGrid.GetCellSelectionFromSlot(owningRow.Slot, owningColumn.Index);
            }
        }

        private void EnsureFormattedText(Size availableSize, string text)
        {
            var maxTextWidth = NormalizeConstraint(availableSize.Width);
            var maxTextHeight = NormalizeConstraint(availableSize.Height);

            if (TextLayoutCacheMode == DataGridCustomDrawingTextLayoutCacheMode.Shared &&
                SharedTextLayoutCache != null)
            {
                var foreground = Foreground ?? Brushes.Black;
                var cacheKey = CreateSharedCacheKey(text, maxTextWidth, maxTextHeight);
                _formattedText = SharedTextLayoutCache.GetOrCreate(
                    cacheKey,
                    () => CreateFormattedText(text, maxTextWidth, maxTextHeight, foreground));
                _text = text;
                _availableSize = availableSize;
                return;
            }

            if (_formattedText != null &&
                _availableSize == availableSize &&
                string.Equals(_text, text, StringComparison.Ordinal))
            {
                return;
            }

            _text = text;
            _availableSize = availableSize;
            _formattedText = CreateFormattedText(text, maxTextWidth, maxTextHeight, Foreground ?? Brushes.Black);
        }

        private FormattedText CreateFormattedText(string text, double maxTextWidth, double maxTextHeight, IBrush foreground)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                foreground)
            {
                TextAlignment = TextAlignment,
                Trimming = TextTrimming
            };

            if (!double.IsInfinity(maxTextWidth))
            {
                formattedText.MaxTextWidth = maxTextWidth;
            }

            if (!double.IsInfinity(maxTextHeight))
            {
                formattedText.MaxTextHeight = maxTextHeight;
            }

            return formattedText;
        }

        private string GetDisplayText()
        {
            if (Value == null)
            {
                return string.Empty;
            }

            if (Value is string stringValue)
            {
                return stringValue;
            }

            return Convert.ToString(Value, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private void InvalidateTextLayout()
        {
            _formattedText = null;
            _text = null;
            _availableSize = null;
        }

        private DataGridCustomDrawingTextLayoutCache.CacheKey CreateSharedCacheKey(
            string text,
            double maxTextWidth,
            double maxTextHeight)
        {
            var foreground = Foreground ?? Brushes.Black;
            var (foregroundKind, foregroundColor, foregroundOpacity, foregroundIdentityHash) = GetForegroundCacheSignature(foreground);

            return new DataGridCustomDrawingTextLayoutCache.CacheKey(
                text,
                FontFamily?.Name ?? string.Empty,
                FontStyle,
                FontWeight,
                FontStretch,
                FontSize,
                TextAlignment,
                TextTrimming,
                FlowDirection,
                CultureInfo.CurrentCulture.LCID,
                maxTextWidth,
                maxTextHeight,
                foregroundKind,
                foregroundColor,
                foregroundOpacity,
                foregroundIdentityHash);
        }

        private static (byte Kind, Color Color, double Opacity, int IdentityHash) GetForegroundCacheSignature(IBrush foreground)
        {
            if (foreground is ISolidColorBrush solidBrush)
            {
                return (1, solidBrush.Color, solidBrush.Opacity, 0);
            }

            return foreground != null
                ? ((byte)2, default, 0, RuntimeHelpers.GetHashCode(foreground))
                : ((byte)0, default, 0, 0);
        }

        private void ApplySharedCacheCapacity()
        {
            if (_sharedTextLayoutCache != null)
            {
                _sharedTextLayoutCache.Capacity = SharedTextLayoutCacheCapacity;
            }
        }

        private static double NormalizeConstraint(double value)
        {
            return double.IsInfinity(value) ? double.PositiveInfinity : Math.Max(0, value);
        }

        private bool ShouldUseDrawOperationLayoutFastPath =>
            DrawOperationLayoutFastPath &&
            DrawOperationFactory != null &&
            DrawingMode == DataGridCustomDrawingMode.DrawOperation;

        private static Size NormalizeLayoutSize(Size value, Size limit)
        {
            return new Size(
                NormalizeDimension(value.Width, limit.Width),
                NormalizeDimension(value.Height, limit.Height));
        }

        private static double NormalizeDimension(double value, double limit)
        {
            if (double.IsNaN(value) || value < 0)
            {
                value = 0;
            }

            if (double.IsInfinity(value))
            {
                value = double.IsInfinity(limit) ? 0 : Math.Max(0, limit);
            }

            if (!double.IsInfinity(limit))
            {
                value = Math.Min(value, Math.Max(0, limit));
            }

            return value;
        }
    }
}
