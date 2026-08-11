// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Utils;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using Avalonia.VisualTree;

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
    sealed class DataGridCustomDrawingCell : DataGridCell
    {
        internal const int DefaultSharedTextLayoutCacheCapacity = 1024;

        private object _value;
        private string _text;
        private FormattedText _formattedText;
        private Size? _availableSize;
        private DataGridCustomDrawingTextLayoutCache _sharedTextLayoutCache;
        private Typeface? _resolvedTypeface;
        private CompositionCustomVisual _compositionVisual;
        private bool _compositionHostUnavailable;
        private bool _hasCompositionDrawOperation;
        private bool _hasCompositionVisualSize;
        private float _compositionVisualWidth;
        private float _compositionVisualHeight;
        private IDataGridDrawnCellValueProvider _valueProvider;
        private IDataGridBuiltInCellRenderer _builtInRenderer;
        private bool _observeWrappedItemValueChanges;
        private INotifyPropertyChanged _valueNotifier;
        private INotifyPropertyChanged _wrappedItemNotifier;
        private int _configurationDepth;
        private bool _configurationDirty;
        private readonly WeakPropertyChangedListener<DataGridCustomDrawingCell> _valuePropertyChangedListener;

        internal bool DisplayPropertiesInitialized { get; set; }

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

        public static readonly StyledProperty<DataGridCustomDrawingRenderBackend> RenderBackendProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, DataGridCustomDrawingRenderBackend>(
                nameof(RenderBackend),
                DataGridCustomDrawingRenderBackend.ImmediateDrawOperation);

        public new static readonly AttachedProperty<FontFamily> FontFamilyProperty =
            TextElement.FontFamilyProperty.AddOwner<DataGridCustomDrawingCell>();

        public new static readonly AttachedProperty<double> FontSizeProperty =
            TextElement.FontSizeProperty.AddOwner<DataGridCustomDrawingCell>();

        public new static readonly AttachedProperty<FontStyle> FontStyleProperty =
            TextElement.FontStyleProperty.AddOwner<DataGridCustomDrawingCell>();

        public new static readonly AttachedProperty<FontWeight> FontWeightProperty =
            TextElement.FontWeightProperty.AddOwner<DataGridCustomDrawingCell>();

        public new static readonly AttachedProperty<FontStretch> FontStretchProperty =
            TextElement.FontStretchProperty.AddOwner<DataGridCustomDrawingCell>();

        public new static readonly AttachedProperty<IBrush> ForegroundProperty =
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

        public static readonly StyledProperty<int> RenderInvalidationTokenProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, int>(
                nameof(RenderInvalidationToken),
                0);

        public static readonly StyledProperty<int> LayoutInvalidationTokenProperty =
            AvaloniaProperty.Register<DataGridCustomDrawingCell, int>(
                nameof(LayoutInvalidationToken),
                0);

        static DataGridCustomDrawingCell()
        {
            UseDirectChromeProperty.OverrideDefaultValue<DataGridCustomDrawingCell>(true);
            AffectsRender<DataGridCustomDrawingCell>(
                BackgroundProperty,
                BorderBrushProperty,
                BorderThicknessProperty,
                ForegroundProperty,
                RenderInvalidationTokenProperty);
        }

        public DataGridCustomDrawingCell()
        {
            _valuePropertyChangedListener = new WeakPropertyChangedListener<DataGridCustomDrawingCell>(
                this,
                static (cell, sender, e) => cell.OnValueProviderPropertyChanged(sender, e));
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

        /// <summary>
        /// Gets or sets draw-operation rendering backend for this cell.
        /// </summary>
        public DataGridCustomDrawingRenderBackend RenderBackend
        {
            get => GetValue(RenderBackendProperty);
            set => SetValue(RenderBackendProperty, value);
        }

        public new FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public new double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public new FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public new FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public new FontStretch FontStretch
        {
            get => GetValue(FontStretchProperty);
            set => SetValue(FontStretchProperty, value);
        }

        public new IBrush Foreground
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

        /// <summary>
        /// Gets or sets the render invalidation token for this realized cell.
        /// Incrementing this value forces redraw for the custom drawing content.
        /// </summary>
        public int RenderInvalidationToken
        {
            get => GetValue(RenderInvalidationTokenProperty);
            set => SetValue(RenderInvalidationTokenProperty, value);
        }

        /// <summary>
        /// Gets or sets the layout invalidation token for this realized cell.
        /// Incrementing this value forces measure/arrange and redraw.
        /// </summary>
        public int LayoutInvalidationToken
        {
            get => GetValue(LayoutInvalidationTokenProperty);
            set => SetValue(LayoutInvalidationTokenProperty, value);
        }

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
                if (_configurationDepth > 0)
                {
                    _configurationDirty = true;
                }
                else
                {
                    ApplySharedCacheCapacity();
                    InvalidateTextLayout();
                }
            }
        }

        internal IDataGridBuiltInCellRenderer BuiltInRendererForTesting => _builtInRenderer;

        internal bool HasCompositionVisualForTesting => _compositionVisual != null;

        internal bool UsesValueProvider => _valueProvider != null;

        internal void RefreshValueProviderSubscription()
        {
            UpdateValueProviderSubscription();
        }

        internal void BeginDisplayConfiguration()
        {
            _configurationDepth++;
        }

        internal void EndDisplayConfiguration()
        {
            if (_configurationDepth == 0 || --_configurationDepth != 0 || !_configurationDirty)
            {
                return;
            }

            _configurationDirty = false;
            InvalidateResolvedTypeface();
            ApplySharedCacheCapacity();
            InvalidateTextLayout();
            InvalidateMeasure();
            InvalidateVisual();

            if (ShouldUseCompositionDrawOperationBackend)
            {
                _compositionHostUnavailable = false;
                EnsureCompositionVisualHost();
            }
            else
            {
                TearDownCompositionVisualHost();
            }
        }

        internal void UseDrawingTemplate()
        {
            Content = null;
            ClearValue(TemplateProperty);
        }

        internal void UseRetainedTemplate()
        {
            TearDownCompositionVisualHost();
            ClearValue(TemplateProperty);
        }

        internal void ConfigureBuiltInRenderer(
            IDataGridDrawnCellValueProvider valueProvider,
            IDataGridBuiltInCellRenderer renderer,
            bool observeWrappedItemValueChanges = false)
        {
            _valueProvider = valueProvider;
            _builtInRenderer = renderer;
            _observeWrappedItemValueChanges = valueProvider != null && observeWrappedItemValueChanges;
            UseDrawingTemplate();
            UpdateValueProviderSubscription();
            if (_configurationDepth > 0)
            {
                _configurationDirty = true;
            }
            else
            {
                InvalidateMeasure();
                InvalidateVisual();
            }
        }

        internal void ClearBuiltInRenderer()
        {
            _valueProvider = null;
            _builtInRenderer = null;
            _observeWrappedItemValueChanges = false;
            UpdateValueProviderSubscription();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Content != null)
            {
                return base.MeasureOverride(availableSize);
            }

            if (_builtInRenderer != null &&
                _builtInRenderer.TryMeasure(this, availableSize, out Size rendererSize))
            {
                return rendererSize;
            }

            var text = GetDisplayText();
            if (TryMeasureWithDrawOperationFastPath(availableSize, text, out Size fastPathSize))
            {
                return fastPathSize;
            }

            if (string.IsNullOrEmpty(text))
            {
                return InflateSize(default, Padding, availableSize);
            }

            var contentAvailableSize = DeflateSize(availableSize, Padding);
            EnsureFormattedText(contentAvailableSize, text);
            if (_formattedText is null)
            {
                return InflateSize(default, Padding, availableSize);
            }

            return InflateSize(
                new Size(_formattedText.Width, _formattedText.Height),
                Padding,
                availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Content != null)
            {
                return base.ArrangeOverride(finalSize);
            }

            var text = GetDisplayText();
            if (TryArrangeWithDrawOperationFastPath(finalSize, text, out Size arrangedSize))
            {
                UpdateCompositionVisualSize(arrangedSize);
                return arrangedSize;
            }

            var size = base.ArrangeOverride(finalSize);
            UpdateCompositionVisualSize(size);
            return size;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Content != null)
            {
                ClearCompositionDrawOperation();
                return;
            }

            if (_builtInRenderer != null)
            {
                ClearCompositionDrawOperation();
                _builtInRenderer.Render(this, context);
                return;
            }

            var text = GetDisplayText();
            var shouldDrawOperation = IsDrawOperationEnabled;
            var shouldDrawText = DrawingMode == DataGridCustomDrawingMode.Text ||
                                 DrawingMode == DataGridCustomDrawingMode.TextAndDrawOperation ||
                                 !shouldDrawOperation;

            if (shouldDrawText && !string.IsNullOrEmpty(text))
            {
                var contentBounds = DeflateRect(new Rect(Bounds.Size), Padding);
                EnsureFormattedText(contentBounds.Size, text);
                if (_formattedText != null)
                {
                    var y = contentBounds.Y + Math.Max(0, (contentBounds.Height - _formattedText.Height) * 0.5);
                    context.DrawText(_formattedText, new Point(contentBounds.X, y));
                }
            }

            if (!shouldDrawOperation)
            {
                ClearCompositionDrawOperation();
                return;
            }

            var drawContext = CreateDrawOperationContext(text);
            var drawOperation = DrawOperationFactory.CreateDrawOperation(drawContext);
            if (ShouldUseCompositionDrawOperationBackend)
            {
                if (TrySendCompositionDrawOperation(drawOperation))
                {
                    return;
                }
            }

            ClearCompositionDrawOperation();
            if (drawOperation != null)
            {
                context.Custom(drawOperation);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateValueProviderSubscription();
            _compositionHostUnavailable = false;
            EnsureCompositionVisualHost();
            UpdateCompositionVisualSize(Bounds.Size);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DetachValueProviderSubscription();
            TearDownCompositionVisualHost();
            _compositionHostUnavailable = false;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            UpdateValueProviderSubscription();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (_configurationDepth > 0)
            {
                _configurationDirty = true;
                return;
            }

            if (change.Property == FontFamilyProperty ||
                change.Property == FontStyleProperty ||
                change.Property == FontWeightProperty ||
                change.Property == FontStretchProperty)
            {
                InvalidateResolvedTypeface();
            }

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
            else if (change.Property == RenderInvalidationTokenProperty)
            {
                InvalidateVisual();
            }
            else if (change.Property == LayoutInvalidationTokenProperty)
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
                     change.Property == DrawingModeProperty ||
                     change.Property == RenderBackendProperty)
            {
                InvalidateTextLayout();
                InvalidateMeasure();
                InvalidateVisual();
                if (ShouldUseCompositionDrawOperationBackend)
                {
                    _compositionHostUnavailable = false;
                    EnsureCompositionVisualHost();
                }
                else
                {
                    TearDownCompositionVisualHost();
                }
            }
            else if (change.Property == ForegroundProperty)
            {
                InvalidateTextLayout();
                InvalidateVisual();
            }
        }

        private static Size DeflateSize(Size size, Thickness padding)
        {
            return new Size(
                Math.Max(0d, size.Width - padding.Left - padding.Right),
                Math.Max(0d, size.Height - padding.Top - padding.Bottom));
        }

        private static Size InflateSize(Size size, Thickness padding, Size availableSize)
        {
            var width = size.Width + padding.Left + padding.Right;
            var height = size.Height + padding.Top + padding.Bottom;
            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width),
                double.IsPositiveInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
        }

        private static Rect DeflateRect(Rect rect, Thickness padding)
        {
            return new Rect(
                padding.Left,
                padding.Top,
                Math.Max(0d, rect.Width - padding.Left - padding.Right),
                Math.Max(0d, rect.Height - padding.Top - padding.Bottom));
        }

        private bool TrySendCompositionDrawOperation(ICustomDrawOperation drawOperation)
        {
            EnsureCompositionVisualHost();
            if (_compositionVisual == null)
            {
                return false;
            }

            if (drawOperation == null)
            {
                ClearCompositionDrawOperation();
                return true;
            }

            _compositionVisual.SendHandlerMessage(drawOperation);
            _hasCompositionDrawOperation = true;
            return true;
        }

        private void ClearCompositionDrawOperation()
        {
            if (_compositionVisual == null || !_hasCompositionDrawOperation)
            {
                return;
            }

            _compositionVisual.SendHandlerMessage(DataGridCustomDrawingCompositionVisualHandler.ClearMessage);
            _hasCompositionDrawOperation = false;
        }

        private void EnsureCompositionVisualHost()
        {
            if (!ShouldUseCompositionDrawOperationBackend)
            {
                TearDownCompositionVisualHost();
                return;
            }

            if (_compositionHostUnavailable)
            {
                return;
            }

            if (_compositionVisual != null)
            {
                return;
            }

            var elementVisual = ElementComposition.GetElementVisual(this);
            var compositor = elementVisual?.Compositor;
            if (compositor == null)
            {
                if (VisualRoot != null)
                {
                    _compositionHostUnavailable = true;
                }

                return;
            }

            _compositionVisual = compositor.CreateCustomVisual(new DataGridCustomDrawingCompositionVisualHandler());
            ElementComposition.SetElementChildVisual(this, _compositionVisual);
            _compositionHostUnavailable = false;
            _hasCompositionVisualSize = false;
        }

        private void TearDownCompositionVisualHost()
        {
            ClearCompositionDrawOperation();
            if (_compositionVisual == null)
            {
                return;
            }

            ElementComposition.SetElementChildVisual(this, null);
            _compositionVisual = null;
            _hasCompositionDrawOperation = false;
            _hasCompositionVisualSize = false;
            _compositionVisualWidth = 0f;
            _compositionVisualHeight = 0f;
        }

        private void UpdateCompositionVisualSize(Size size)
        {
            if (!ShouldUseCompositionDrawOperationBackend)
            {
                return;
            }

            if (_compositionVisual == null)
            {
                EnsureCompositionVisualHost();
            }

            if (_compositionVisual == null)
            {
                return;
            }

            var width = NormalizeVisualDimension(size.Width);
            var height = NormalizeVisualDimension(size.Height);
            if (_hasCompositionVisualSize &&
                _compositionVisualWidth.Equals(width) &&
                _compositionVisualHeight.Equals(height))
            {
                return;
            }

            _compositionVisual.Size = new Vector2(width, height);
            _compositionVisualWidth = width;
            _compositionVisualHeight = height;
            _hasCompositionVisualSize = true;
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
            cell = this;
            foreground = Foreground ?? Brushes.Black;
            typeface = GetResolvedTypeface();
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

            // Recycled cells can remain in the visual tree with a transient zero-sized
            // arrange while their display mode is being changed. FormattedText rejects a
            // zero MaxTextHeight, and there is no drawable text surface in this state.
            if (maxTextWidth <= 0 || maxTextHeight <= 0)
            {
                _formattedText = null;
                _text = text;
                _availableSize = availableSize;
                return;
            }

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
                GetResolvedTypeface(),
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

        private void UpdateValueProviderSubscription()
        {
            DetachValueProviderSubscription();

            if (_valueProvider == null)
            {
                return;
            }

            UpdateValueFromProvider();
            if (_valueProvider is IDataGridDrawnCellValueChangeTracking
                {
                    TrackDrawnCellValueChanges: false
                })
            {
                return;
            }

            // Recycled cells may receive a new DataContext while detached. Refresh the
            // value above, but defer the external event subscription until reattachment.
            if (VisualRoot == null)
            {
                return;
            }

            if (DataContext is INotifyPropertyChanged notifier)
            {
                _valueNotifier = notifier;
                _valueNotifier.PropertyChanged += _valuePropertyChangedListener.Handler;
            }

            if (_observeWrappedItemValueChanges &&
                DataContext is IHierarchicalNodeItem node &&
                node.Item is INotifyPropertyChanged itemNotifier &&
                !ReferenceEquals(itemNotifier, _valueNotifier))
            {
                _wrappedItemNotifier = itemNotifier;
                _wrappedItemNotifier.PropertyChanged += _valuePropertyChangedListener.Handler;
            }
        }

        private void DetachValueProviderSubscription()
        {
            if (_valueNotifier != null)
            {
                _valueNotifier.PropertyChanged -= _valuePropertyChangedListener.Handler;
                _valueNotifier = null;
            }

            if (_wrappedItemNotifier != null)
            {
                _wrappedItemNotifier.PropertyChanged -= _valuePropertyChangedListener.Handler;
                _wrappedItemNotifier = null;
            }
        }

        private void OnValueProviderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ReferenceEquals(sender, _valueNotifier) ||
                ReferenceEquals(sender, _wrappedItemNotifier))
            {
                UpdateValueFromProvider();
            }
        }

        private void UpdateValueFromProvider()
        {
            Value = _valueProvider?.GetDrawnCellValue(DataContext);
        }

        private void InvalidateTextLayout()
        {
            _formattedText = null;
            _text = null;
            _availableSize = null;
        }

        private void InvalidateResolvedTypeface()
        {
            _resolvedTypeface = null;
        }

        private Typeface GetResolvedTypeface()
        {
            if (_resolvedTypeface.HasValue)
            {
                return _resolvedTypeface.Value;
            }

            _resolvedTypeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            return _resolvedTypeface.Value;
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
            if (double.IsNaN(value) || double.IsNegativeInfinity(value) || value <= 0)
            {
                return 0;
            }

            return double.IsPositiveInfinity(value) ? double.PositiveInfinity : value;
        }

        private bool ShouldUseDrawOperationLayoutFastPath =>
            DrawOperationLayoutFastPath &&
            DrawOperationFactory != null &&
            DrawingMode == DataGridCustomDrawingMode.DrawOperation;

        private bool IsDrawOperationEnabled =>
            DrawOperationFactory != null &&
            (DrawingMode == DataGridCustomDrawingMode.DrawOperation ||
             DrawingMode == DataGridCustomDrawingMode.TextAndDrawOperation);

        private bool ShouldUseCompositionDrawOperationBackend =>
            RenderBackend == DataGridCustomDrawingRenderBackend.CompositionCustomVisual &&
            IsDrawOperationEnabled;

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

        private static float NormalizeVisualDimension(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                return 0f;
            }

            if (value >= float.MaxValue)
            {
                return float.MaxValue;
            }

            return (float)value;
        }
    }
}
