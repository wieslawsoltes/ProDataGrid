// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Collections;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Represents a bound column that renders display cells using <see cref="DataGridCustomDrawingCell"/>.
    /// This column is read-only by default.
    /// </summary>
public
#else
internal
#endif
    class DataGridCustomDrawingColumn : DataGridBoundColumn
    {
        private readonly Lazy<ControlTheme> _cellCustomDrawingTheme;
        private readonly Lazy<ControlTheme> _cellTextBoxTheme;
        private readonly DataGridCustomDrawingTextLayoutCache _sharedTextLayoutCache;
        private IDataGridCellDrawOperationFactory _subscribedInvalidationFactory;

        protected ControlTheme CellCustomDrawingTheme => GetThemeValue(_cellCustomDrawingTheme);
        protected ControlTheme CellTextBoxTheme => GetThemeValue(_cellTextBoxTheme);

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCustomDrawingColumn"/> class.
        /// </summary>
        public DataGridCustomDrawingColumn()
        {
            BindingTarget = TextBox.TextProperty;
            IsReadOnly = true;
            _cellCustomDrawingTheme = new Lazy<ControlTheme>(() => GetColumnControlTheme("DataGridCellCustomDrawingTheme"));
            _cellTextBoxTheme = new Lazy<ControlTheme>(() => GetColumnControlTheme("DataGridCellTextBoxTheme"));
            _sharedTextLayoutCache = new DataGridCustomDrawingTextLayoutCache(DataGridCustomDrawingCell.DefaultSharedTextLayoutCacheCapacity);
        }

        public static readonly AttachedProperty<FontFamily> FontFamilyProperty =
            DataGridCustomDrawingCell.FontFamilyProperty.AddOwner<DataGridCustomDrawingColumn>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly AttachedProperty<double> FontSizeProperty =
            DataGridCustomDrawingCell.FontSizeProperty.AddOwner<DataGridCustomDrawingColumn>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly AttachedProperty<FontStyle> FontStyleProperty =
            DataGridCustomDrawingCell.FontStyleProperty.AddOwner<DataGridCustomDrawingColumn>();

        public FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public static readonly AttachedProperty<FontWeight> FontWeightProperty =
            DataGridCustomDrawingCell.FontWeightProperty.AddOwner<DataGridCustomDrawingColumn>();

        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public static readonly AttachedProperty<FontStretch> FontStretchProperty =
            DataGridCustomDrawingCell.FontStretchProperty.AddOwner<DataGridCustomDrawingColumn>();

        public FontStretch FontStretch
        {
            get => GetValue(FontStretchProperty);
            set => SetValue(FontStretchProperty, value);
        }

        public static readonly AttachedProperty<IBrush> ForegroundProperty =
            DataGridCustomDrawingCell.ForegroundProperty.AddOwner<DataGridCustomDrawingColumn>();

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
            DataGridCustomDrawingCell.TextAlignmentProperty.AddOwner<DataGridCustomDrawingColumn>();

        public TextAlignment TextAlignment
        {
            get => GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public static readonly StyledProperty<TextTrimming> TextTrimmingProperty =
            DataGridCustomDrawingCell.TextTrimmingProperty.AddOwner<DataGridCustomDrawingColumn>();

        public TextTrimming TextTrimming
        {
            get => GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        public static readonly StyledProperty<IDataGridCellDrawOperationFactory> DrawOperationFactoryProperty =
            DataGridCustomDrawingCell.DrawOperationFactoryProperty.AddOwner<DataGridCustomDrawingColumn>();

        public IDataGridCellDrawOperationFactory DrawOperationFactory
        {
            get => GetValue(DrawOperationFactoryProperty);
            set => SetValue(DrawOperationFactoryProperty, value);
        }

        public static readonly StyledProperty<DataGridCustomDrawingMode> DrawingModeProperty =
            DataGridCustomDrawingCell.DrawingModeProperty.AddOwner<DataGridCustomDrawingColumn>();

        public DataGridCustomDrawingMode DrawingMode
        {
            get => GetValue(DrawingModeProperty);
            set => SetValue(DrawingModeProperty, value);
        }

        /// <summary>
        /// Gets or sets draw-operation rendering backend for realized cells.
        /// </summary>
        public static readonly StyledProperty<DataGridCustomDrawingRenderBackend> RenderBackendProperty =
            DataGridCustomDrawingCell.RenderBackendProperty.AddOwner<DataGridCustomDrawingColumn>();

        /// <summary>
        /// Gets or sets draw-operation rendering backend for realized cells.
        /// </summary>
        public DataGridCustomDrawingRenderBackend RenderBackend
        {
            get => GetValue(RenderBackendProperty);
            set => SetValue(RenderBackendProperty, value);
        }

        /// <summary>
        /// Gets or sets text layout cache mode for realized cells.
        /// </summary>
        public static readonly StyledProperty<DataGridCustomDrawingTextLayoutCacheMode> TextLayoutCacheModeProperty =
            DataGridCustomDrawingCell.TextLayoutCacheModeProperty.AddOwner<DataGridCustomDrawingColumn>();

        /// <summary>
        /// Gets or sets text layout cache mode for realized cells.
        /// </summary>
        public DataGridCustomDrawingTextLayoutCacheMode TextLayoutCacheMode
        {
            get => GetValue(TextLayoutCacheModeProperty);
            set => SetValue(TextLayoutCacheModeProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum shared text layout cache entry count.
        /// </summary>
        public static readonly StyledProperty<int> SharedTextLayoutCacheCapacityProperty =
            DataGridCustomDrawingCell.SharedTextLayoutCacheCapacityProperty.AddOwner<DataGridCustomDrawingColumn>();

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
        public static readonly StyledProperty<bool> DrawOperationLayoutFastPathProperty =
            DataGridCustomDrawingCell.DrawOperationLayoutFastPathProperty.AddOwner<DataGridCustomDrawingColumn>();

        /// <summary>
        /// Gets or sets a value indicating whether draw-operation layout fast path is enabled.
        /// </summary>
        public bool DrawOperationLayoutFastPath
        {
            get => GetValue(DrawOperationLayoutFastPathProperty);
            set => SetValue(DrawOperationLayoutFastPathProperty, value);
        }

        /// <summary>
        /// Gets or sets render invalidation token for realized custom drawing cells.
        /// Incrementing this value forces redraw.
        /// </summary>
        public static readonly StyledProperty<int> RenderInvalidationTokenProperty =
            DataGridCustomDrawingCell.RenderInvalidationTokenProperty.AddOwner<DataGridCustomDrawingColumn>();

        /// <summary>
        /// Gets or sets render invalidation token for realized custom drawing cells.
        /// Incrementing this value forces redraw.
        /// </summary>
        public int RenderInvalidationToken
        {
            get => GetValue(RenderInvalidationTokenProperty);
            set => SetValue(RenderInvalidationTokenProperty, value);
        }

        /// <summary>
        /// Gets or sets layout invalidation token for realized custom drawing cells.
        /// Incrementing this value forces measure/arrange and redraw.
        /// </summary>
        public static readonly StyledProperty<int> LayoutInvalidationTokenProperty =
            DataGridCustomDrawingCell.LayoutInvalidationTokenProperty.AddOwner<DataGridCustomDrawingColumn>();

        /// <summary>
        /// Gets or sets layout invalidation token for realized custom drawing cells.
        /// Incrementing this value forces measure/arrange and redraw.
        /// </summary>
        public int LayoutInvalidationToken
        {
            get => GetValue(LayoutInvalidationTokenProperty);
            set => SetValue(LayoutInvalidationTokenProperty, value);
        }

        /// <summary>
        /// Invalidates realized custom drawing cells for this column.
        /// </summary>
        /// <param name="invalidateMeasure">
        /// <c>true</c> to invalidate measure/arrange in addition to render; otherwise <c>false</c>.
        /// </param>
        /// <param name="clearSharedTextLayoutCache">
        /// <c>true</c> to clear shared text-layout cache before refreshing cells; otherwise <c>false</c>.
        /// </param>
        public void InvalidateCustomDrawingCells(
            bool invalidateMeasure = false,
            bool clearSharedTextLayoutCache = false)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(
                    () => InvalidateCustomDrawingCells(invalidateMeasure, clearSharedTextLayoutCache),
                    DispatcherPriority.Render);
                return;
            }

            if (clearSharedTextLayoutCache)
            {
                _sharedTextLayoutCache.Clear();
            }

            unchecked
            {
                RenderInvalidationToken++;
            }

            if (!invalidateMeasure)
            {
                return;
            }

            unchecked
            {
                LayoutInvalidationToken++;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SharedTextLayoutCacheCapacityProperty)
            {
                _sharedTextLayoutCache.Capacity = SharedTextLayoutCacheCapacity;
            }
            else if (change.Property == FontFamilyProperty ||
                     change.Property == FontSizeProperty ||
                     change.Property == FontStyleProperty ||
                     change.Property == FontWeightProperty ||
                     change.Property == FontStretchProperty ||
                     change.Property == TextAlignmentProperty ||
                     change.Property == TextTrimmingProperty)
            {
                _sharedTextLayoutCache.Clear();
            }
            else if (change.Property == DrawOperationFactoryProperty)
            {
                EnsureInvalidationSourceSubscription();
            }

            if (change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == FontStyleProperty ||
                change.Property == FontWeightProperty ||
                change.Property == FontStretchProperty ||
                change.Property == ForegroundProperty ||
                change.Property == TextAlignmentProperty ||
                change.Property == TextTrimmingProperty ||
                change.Property == DrawOperationFactoryProperty ||
                change.Property == DrawingModeProperty ||
                change.Property == RenderBackendProperty ||
                change.Property == TextLayoutCacheModeProperty ||
                change.Property == SharedTextLayoutCacheCapacityProperty ||
                change.Property == DrawOperationLayoutFastPathProperty ||
                change.Property == RenderInvalidationTokenProperty ||
                change.Property == LayoutInvalidationTokenProperty)
            {
                NotifyPropertyChanged(change.Property.Name);
            }
        }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            return CreateDisplayElement(cell, dataItem, bindValue: true);
        }

        protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
        {
            return CreateEditingElement();
        }

        protected override void CancelCellEdit(Control editingElement, object uneditedValue)
        {
            if (editingElement is TextBox textBox)
            {
                string uneditedString = uneditedValue as string;
                textBox.Text = uneditedString ?? string.Empty;
            }
        }

        protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            if (editingElement is TextBox textBox)
            {
                string uneditedText = textBox.Text ?? string.Empty;
                int length = uneditedText.Length;

                if (editingEventArgs is KeyEventArgs keyEventArgs && keyEventArgs.Key == Key.F2)
                {
                    textBox.SelectionStart = length;
                    textBox.SelectionEnd = length;
                }
                else
                {
                    textBox.SelectionStart = 0;
                    textBox.SelectionEnd = length;
                    textBox.CaretIndex = length;
                }

                return uneditedText;
            }

            return string.Empty;
        }

        protected internal override void RefreshCellContent(Control element, string propertyName)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (element is DataGridCustomDrawingCell drawingCell)
            {
                RefreshDisplayCellContent(drawingCell, propertyName);
                return;
            }

            if (element is TextBox textBox)
            {
                RefreshEditingCellContent(textBox, propertyName);
                return;
            }

            throw DataGridError.DataGrid.ValueIsNotAnInstanceOf("element", typeof(Control));
        }

        private DataGridCustomDrawingCell CreateDisplayElement(DataGridCell cell, object dataItem, bool bindValue)
        {
            EnsureInvalidationSourceSubscription();

            var drawingCell = new DataGridCustomDrawingCell
            {
                Name = "CellCustomDrawing",
                OwningCell = cell
            };

            if (CellCustomDrawingTheme is { } theme)
            {
                drawingCell.Theme = theme;
            }

            SyncDisplayProperties(drawingCell);

            if (bindValue && Binding != null && dataItem != DataGridCollectionView.NewItemPlaceholder)
            {
                drawingCell.Bind(DataGridCustomDrawingCell.ValueProperty, Binding);
            }

            return drawingCell;
        }

        private TextBox CreateEditingElement()
        {
            var textBox = new TextBox
            {
                Name = "CellCustomDrawingTextBox"
            };

            if (CellTextBoxTheme is { } theme)
            {
                textBox.Theme = theme;
            }

            SyncEditingProperties(textBox);

            return textBox;
        }

        private void RefreshDisplayCellContent(DataGridCustomDrawingCell drawingCell, string propertyName)
        {
            switch (propertyName)
            {
                case nameof(FontFamily):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.FontFamilyProperty, FontFamilyProperty);
                    break;
                case nameof(FontSize):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.FontSizeProperty, FontSizeProperty);
                    break;
                case nameof(FontStyle):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.FontStyleProperty, FontStyleProperty);
                    break;
                case nameof(FontWeight):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.FontWeightProperty, FontWeightProperty);
                    break;
                case nameof(FontStretch):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.FontStretchProperty, FontStretchProperty);
                    break;
                case nameof(Foreground):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.ForegroundProperty, ForegroundProperty);
                    break;
                case nameof(TextAlignment):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.TextAlignmentProperty, TextAlignmentProperty);
                    break;
                case nameof(TextTrimming):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.TextTrimmingProperty, TextTrimmingProperty);
                    break;
                case nameof(DrawOperationFactory):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.DrawOperationFactoryProperty, DrawOperationFactoryProperty);
                    break;
                case nameof(DrawingMode):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.DrawingModeProperty, DrawingModeProperty);
                    break;
                case nameof(RenderBackend):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.RenderBackendProperty, RenderBackendProperty);
                    break;
                case nameof(TextLayoutCacheMode):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.TextLayoutCacheModeProperty, TextLayoutCacheModeProperty);
                    break;
                case nameof(SharedTextLayoutCacheCapacity):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.SharedTextLayoutCacheCapacityProperty, SharedTextLayoutCacheCapacityProperty);
                    break;
                case nameof(DrawOperationLayoutFastPath):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.DrawOperationLayoutFastPathProperty, DrawOperationLayoutFastPathProperty);
                    break;
                case nameof(RenderInvalidationToken):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.RenderInvalidationTokenProperty, RenderInvalidationTokenProperty);
                    break;
                case nameof(LayoutInvalidationToken):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.LayoutInvalidationTokenProperty, LayoutInvalidationTokenProperty);
                    break;
            }

            drawingCell.SharedTextLayoutCache = _sharedTextLayoutCache;
        }

        private void RefreshEditingCellContent(TextBox textBox, string propertyName)
        {
            switch (propertyName)
            {
                case nameof(FontFamily):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontFamilyProperty, FontFamilyProperty);
                    break;
                case nameof(FontSize):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontSizeProperty, FontSizeProperty);
                    break;
                case nameof(FontStyle):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontStyleProperty, FontStyleProperty);
                    break;
                case nameof(FontWeight):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontWeightProperty, FontWeightProperty);
                    break;
                case nameof(FontStretch):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontStretchProperty, FontStretchProperty);
                    break;
                case nameof(Foreground):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextElement.ForegroundProperty, ForegroundProperty);
                    break;
                case nameof(TextAlignment):
                    DataGridHelper.SyncColumnProperty(this, textBox, TextBox.TextAlignmentProperty, TextAlignmentProperty);
                    break;
            }
        }

        private void SyncDisplayProperties(AvaloniaObject content)
        {
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.FontFamilyProperty, FontFamilyProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.FontSizeProperty, FontSizeProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.FontStyleProperty, FontStyleProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.FontWeightProperty, FontWeightProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.FontStretchProperty, FontStretchProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.ForegroundProperty, ForegroundProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.TextAlignmentProperty, TextAlignmentProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.TextTrimmingProperty, TextTrimmingProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.DrawOperationFactoryProperty, DrawOperationFactoryProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.DrawingModeProperty, DrawingModeProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.RenderBackendProperty, RenderBackendProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.TextLayoutCacheModeProperty, TextLayoutCacheModeProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.SharedTextLayoutCacheCapacityProperty, SharedTextLayoutCacheCapacityProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.DrawOperationLayoutFastPathProperty, DrawOperationLayoutFastPathProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.RenderInvalidationTokenProperty, RenderInvalidationTokenProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.LayoutInvalidationTokenProperty, LayoutInvalidationTokenProperty);

            if (content is DataGridCustomDrawingCell drawingCell)
            {
                drawingCell.SharedTextLayoutCache = _sharedTextLayoutCache;
            }
        }

        private void SyncEditingProperties(TextBox textBox)
        {
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontFamilyProperty, FontFamilyProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontSizeProperty, FontSizeProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontStyleProperty, FontStyleProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontWeightProperty, FontWeightProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.FontStretchProperty, FontStretchProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextElement.ForegroundProperty, ForegroundProperty);
            DataGridHelper.SyncColumnProperty(this, textBox, TextBox.TextAlignmentProperty, TextAlignmentProperty);
        }

        private ControlTheme GetThemeValue(Lazy<ControlTheme> themeCache)
        {
            if (themeCache.IsValueCreated)
            {
                return themeCache.Value;
            }

            // Avoid permanently caching null before the column is attached to a grid.
            return OwningGrid == null ? null : themeCache.Value;
        }

        private void UpdateInvalidationSourceSubscription(
            IDataGridCellDrawOperationFactory oldFactory,
            IDataGridCellDrawOperationFactory newFactory)
        {
            if (ReferenceEquals(oldFactory, newFactory))
            {
                return;
            }

            if (oldFactory is IDataGridCellDrawOperationInvalidationSource oldSource)
            {
                oldSource.Invalidated -= OnDrawOperationFactoryInvalidated;
            }

            if (newFactory is IDataGridCellDrawOperationInvalidationSource newSource)
            {
                newSource.Invalidated += OnDrawOperationFactoryInvalidated;
            }
        }

        private void EnsureInvalidationSourceSubscription()
        {
            var targetFactory = OwningGrid != null ? DrawOperationFactory : null;
            if (ReferenceEquals(_subscribedInvalidationFactory, targetFactory))
            {
                return;
            }

            UpdateInvalidationSourceSubscription(_subscribedInvalidationFactory, targetFactory);
            _subscribedInvalidationFactory = targetFactory;
        }

        private void OnDrawOperationFactoryInvalidated(object sender, DataGridCellDrawOperationInvalidatedEventArgs e)
        {
            e ??= new DataGridCellDrawOperationInvalidatedEventArgs();

            void InvalidateCells()
            {
                InvalidateCustomDrawingCells(e.InvalidateMeasure, e.ClearTextLayoutCache);
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                InvalidateCells();
                return;
            }

            Dispatcher.UIThread.Post(InvalidateCells, DispatcherPriority.Render);
        }

        internal override void ClearElementCache()
        {
            UpdateInvalidationSourceSubscription(_subscribedInvalidationFactory, null);
            _subscribedInvalidationFactory = null;
            base.ClearElementCache();
        }
    }
}
