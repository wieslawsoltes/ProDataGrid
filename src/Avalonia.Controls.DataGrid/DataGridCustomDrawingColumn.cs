// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using Avalonia.Collections;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Represents a read-only bound column that renders each cell using <see cref="DataGridCustomDrawingCell"/>.
    /// </summary>
public
#else
internal
#endif
    class DataGridCustomDrawingColumn : DataGridBoundColumn
    {
        private readonly Lazy<ControlTheme> _cellCustomDrawingTheme;
        private readonly DataGridCustomDrawingTextLayoutCache _sharedTextLayoutCache;

        protected ControlTheme CellCustomDrawingTheme => GetThemeValue(_cellCustomDrawingTheme);

        /// <summary>
        /// Initializes a new instance of the <see cref="DataGridCustomDrawingColumn"/> class.
        /// </summary>
        public DataGridCustomDrawingColumn()
        {
            BindingTarget = DataGridCustomDrawingCell.ValueProperty;
            IsReadOnly = true;
            _cellCustomDrawingTheme = new Lazy<ControlTheme>(() => GetColumnControlTheme("DataGridCellCustomDrawingTheme"));
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
                change.Property == TextLayoutCacheModeProperty ||
                change.Property == SharedTextLayoutCacheCapacityProperty ||
                change.Property == DrawOperationLayoutFastPathProperty)
            {
                NotifyPropertyChanged(change.Property.Name);
            }
        }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            return CreateElement(cell, dataItem, bindValue: true);
        }

        protected override Control GenerateEditingElementDirect(DataGridCell cell, object dataItem)
        {
            return CreateElement(cell, dataItem, bindValue: false);
        }

        protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            return null;
        }

        protected internal override void RefreshCellContent(Control element, string propertyName)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (element is not DataGridCustomDrawingCell drawingCell)
            {
                throw DataGridError.DataGrid.ValueIsNotAnInstanceOf("element", typeof(DataGridCustomDrawingCell));
            }

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
                case nameof(TextLayoutCacheMode):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.TextLayoutCacheModeProperty, TextLayoutCacheModeProperty);
                    break;
                case nameof(SharedTextLayoutCacheCapacity):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.SharedTextLayoutCacheCapacityProperty, SharedTextLayoutCacheCapacityProperty);
                    break;
                case nameof(DrawOperationLayoutFastPath):
                    DataGridHelper.SyncColumnProperty(this, drawingCell, DataGridCustomDrawingCell.DrawOperationLayoutFastPathProperty, DrawOperationLayoutFastPathProperty);
                    break;
            }

            drawingCell.SharedTextLayoutCache = _sharedTextLayoutCache;
        }

        private DataGridCustomDrawingCell CreateElement(DataGridCell cell, object dataItem, bool bindValue)
        {
            var drawingCell = new DataGridCustomDrawingCell
            {
                Name = "CellCustomDrawing",
                OwningCell = cell
            };

            if (CellCustomDrawingTheme is { } theme)
            {
                drawingCell.Theme = theme;
            }

            SyncProperties(drawingCell);

            if (bindValue && Binding != null && dataItem != DataGridCollectionView.NewItemPlaceholder)
            {
                drawingCell.Bind(DataGridCustomDrawingCell.ValueProperty, Binding);
            }

            return drawingCell;
        }

        private void SyncProperties(AvaloniaObject content)
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
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.TextLayoutCacheModeProperty, TextLayoutCacheModeProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.SharedTextLayoutCacheCapacityProperty, SharedTextLayoutCacheCapacityProperty);
            DataGridHelper.SyncColumnProperty(this, content, DataGridCustomDrawingCell.DrawOperationLayoutFastPathProperty, DrawOperationLayoutFastPathProperty);

            if (content is DataGridCustomDrawingCell drawingCell)
            {
                drawingCell.SharedTextLayoutCache = _sharedTextLayoutCache;
            }
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
    }
}
