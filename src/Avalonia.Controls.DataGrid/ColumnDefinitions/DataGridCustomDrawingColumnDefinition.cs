// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia.Media;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    /// <summary>
    /// Defines a <see cref="DataGridCustomDrawingColumn"/> for <see cref="DataGrid.ColumnDefinitionsSource"/>.
    /// </summary>
    public
#else
    internal
#endif
    sealed class DataGridCustomDrawingColumnDefinition : DataGridBoundColumnDefinition
    {
        private FontFamily _fontFamily;
        private double? _fontSize;
        private FontStyle? _fontStyle;
        private FontWeight? _fontWeight;
        private FontStretch? _fontStretch;
        private IBrush _foreground;
        private TextAlignment? _textAlignment;
        private TextTrimming _textTrimming;
        private bool _hasTextTrimming;
        private IDataGridCellDrawOperationFactory _drawOperationFactory;
        private DataGridCustomDrawingMode? _drawingMode;
        private DataGridCustomDrawingRenderBackend? _renderBackend;
        private DataGridCustomDrawingTextLayoutCacheMode? _textLayoutCacheMode;
        private int? _sharedTextLayoutCacheCapacity;
        private bool? _drawOperationLayoutFastPath;

        public FontFamily FontFamily
        {
            get => _fontFamily;
            set => SetProperty(ref _fontFamily, value);
        }

        public double? FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }

        public FontStyle? FontStyle
        {
            get => _fontStyle;
            set => SetProperty(ref _fontStyle, value);
        }

        public FontWeight? FontWeight
        {
            get => _fontWeight;
            set => SetProperty(ref _fontWeight, value);
        }

        public FontStretch? FontStretch
        {
            get => _fontStretch;
            set => SetProperty(ref _fontStretch, value);
        }

        public IBrush Foreground
        {
            get => _foreground;
            set => SetProperty(ref _foreground, value);
        }

        public TextAlignment? TextAlignment
        {
            get => _textAlignment;
            set => SetProperty(ref _textAlignment, value);
        }

        public TextTrimming TextTrimming
        {
            get => _textTrimming;
            set
            {
                _hasTextTrimming = true;
                SetProperty(ref _textTrimming, value);
            }
        }

        public IDataGridCellDrawOperationFactory DrawOperationFactory
        {
            get => _drawOperationFactory;
            set => SetProperty(ref _drawOperationFactory, value);
        }

        public DataGridCustomDrawingMode? DrawingMode
        {
            get => _drawingMode;
            set => SetProperty(ref _drawingMode, value);
        }

        public DataGridCustomDrawingRenderBackend? RenderBackend
        {
            get => _renderBackend;
            set => SetProperty(ref _renderBackend, value);
        }

        public DataGridCustomDrawingTextLayoutCacheMode? TextLayoutCacheMode
        {
            get => _textLayoutCacheMode;
            set => SetProperty(ref _textLayoutCacheMode, value);
        }

        public int? SharedTextLayoutCacheCapacity
        {
            get => _sharedTextLayoutCacheCapacity;
            set => SetProperty(ref _sharedTextLayoutCacheCapacity, value);
        }

        public bool? DrawOperationLayoutFastPath
        {
            get => _drawOperationLayoutFastPath;
            set => SetProperty(ref _drawOperationLayoutFastPath, value);
        }

        protected override DataGridColumn CreateColumnCore()
        {
            return new DataGridCustomDrawingColumn();
        }

        protected override void ApplyColumnProperties(DataGridColumn column, DataGridColumnDefinitionContext context)
        {
            base.ApplyColumnProperties(column, context);

            if (column is not DataGridCustomDrawingColumn drawingColumn)
            {
                return;
            }

            if (FontFamily != null)
            {
                drawingColumn.FontFamily = FontFamily;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontFamilyProperty);
            }

            if (Foreground != null)
            {
                drawingColumn.Foreground = Foreground;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.ForegroundProperty);
            }

            if (DrawOperationFactory != null)
            {
                drawingColumn.DrawOperationFactory = DrawOperationFactory;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawOperationFactoryProperty);
            }

            if (FontSize.HasValue)
            {
                drawingColumn.FontSize = FontSize.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontSizeProperty);
            }

            if (FontStyle.HasValue)
            {
                drawingColumn.FontStyle = FontStyle.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontStyleProperty);
            }

            if (FontWeight.HasValue)
            {
                drawingColumn.FontWeight = FontWeight.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontWeightProperty);
            }

            if (FontStretch.HasValue)
            {
                drawingColumn.FontStretch = FontStretch.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontStretchProperty);
            }

            if (TextAlignment.HasValue)
            {
                drawingColumn.TextAlignment = TextAlignment.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextAlignmentProperty);
            }

            if (_hasTextTrimming)
            {
                drawingColumn.TextTrimming = TextTrimming;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextTrimmingProperty);
            }

            if (DrawingMode.HasValue)
            {
                drawingColumn.DrawingMode = DrawingMode.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawingModeProperty);
            }

            if (RenderBackend.HasValue)
            {
                drawingColumn.RenderBackend = RenderBackend.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.RenderBackendProperty);
            }

            if (TextLayoutCacheMode.HasValue)
            {
                drawingColumn.TextLayoutCacheMode = TextLayoutCacheMode.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextLayoutCacheModeProperty);
            }

            if (SharedTextLayoutCacheCapacity.HasValue)
            {
                drawingColumn.SharedTextLayoutCacheCapacity = SharedTextLayoutCacheCapacity.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.SharedTextLayoutCacheCapacityProperty);
            }

            if (DrawOperationLayoutFastPath.HasValue)
            {
                drawingColumn.DrawOperationLayoutFastPath = DrawOperationLayoutFastPath.Value;
            }
            else
            {
                drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawOperationLayoutFastPathProperty);
            }
        }

        protected override bool ApplyColumnPropertyChange(
            DataGridColumn column,
            DataGridColumnDefinitionContext context,
            string propertyName)
        {
            if (base.ApplyColumnPropertyChange(column, context, propertyName))
            {
                return true;
            }

            if (column is not DataGridCustomDrawingColumn drawingColumn)
            {
                return false;
            }

            switch (propertyName)
            {
                case nameof(FontFamily):
                    if (FontFamily != null)
                    {
                        drawingColumn.FontFamily = FontFamily;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontFamilyProperty);
                    }
                    return true;
                case nameof(FontSize):
                    if (FontSize.HasValue)
                    {
                        drawingColumn.FontSize = FontSize.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontSizeProperty);
                    }
                    return true;
                case nameof(FontStyle):
                    if (FontStyle.HasValue)
                    {
                        drawingColumn.FontStyle = FontStyle.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontStyleProperty);
                    }
                    return true;
                case nameof(FontWeight):
                    if (FontWeight.HasValue)
                    {
                        drawingColumn.FontWeight = FontWeight.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontWeightProperty);
                    }
                    return true;
                case nameof(FontStretch):
                    if (FontStretch.HasValue)
                    {
                        drawingColumn.FontStretch = FontStretch.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.FontStretchProperty);
                    }
                    return true;
                case nameof(Foreground):
                    if (Foreground != null)
                    {
                        drawingColumn.Foreground = Foreground;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.ForegroundProperty);
                    }
                    return true;
                case nameof(TextAlignment):
                    if (TextAlignment.HasValue)
                    {
                        drawingColumn.TextAlignment = TextAlignment.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextAlignmentProperty);
                    }
                    return true;
                case nameof(TextTrimming):
                    if (_hasTextTrimming)
                    {
                        drawingColumn.TextTrimming = TextTrimming;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextTrimmingProperty);
                    }
                    return true;
                case nameof(DrawOperationFactory):
                    if (DrawOperationFactory != null)
                    {
                        drawingColumn.DrawOperationFactory = DrawOperationFactory;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawOperationFactoryProperty);
                    }
                    return true;
                case nameof(DrawingMode):
                    if (DrawingMode.HasValue)
                    {
                        drawingColumn.DrawingMode = DrawingMode.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawingModeProperty);
                    }
                    return true;
                case nameof(RenderBackend):
                    if (RenderBackend.HasValue)
                    {
                        drawingColumn.RenderBackend = RenderBackend.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.RenderBackendProperty);
                    }
                    return true;
                case nameof(TextLayoutCacheMode):
                    if (TextLayoutCacheMode.HasValue)
                    {
                        drawingColumn.TextLayoutCacheMode = TextLayoutCacheMode.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.TextLayoutCacheModeProperty);
                    }
                    return true;
                case nameof(SharedTextLayoutCacheCapacity):
                    if (SharedTextLayoutCacheCapacity.HasValue)
                    {
                        drawingColumn.SharedTextLayoutCacheCapacity = SharedTextLayoutCacheCapacity.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.SharedTextLayoutCacheCapacityProperty);
                    }
                    return true;
                case nameof(DrawOperationLayoutFastPath):
                    if (DrawOperationLayoutFastPath.HasValue)
                    {
                        drawingColumn.DrawOperationLayoutFastPath = DrawOperationLayoutFastPath.Value;
                    }
                    else
                    {
                        drawingColumn.ClearValue(DataGridCustomDrawingColumn.DrawOperationLayoutFastPathProperty);
                    }
                    return true;
            }

            return false;
        }
    }
}
