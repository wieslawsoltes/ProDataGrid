// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Styling;
using Avalonia.Threading;
using SkiaSharp;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridCustomDrawingColumnTests
{
    [AvaloniaFact]
    public void CustomDrawingCell_Measures_Text_Value()
    {
        var cell = new DataGridCustomDrawingCell
        {
            Value = "hello"
        };

        cell.Measure(Size.Infinity);

        Assert.True(cell.DesiredSize.Width > 0);
        Assert.True(cell.DesiredSize.Height > 0);
    }

    [AvaloniaFact]
    public void CustomDrawingCell_Content_Change_Keeps_Measure_Valid_For_Fixed_Geometry()
    {
        AssertContentChangeMeasureValidity(new DataGridLength(120), expectedMeasureValid: true);
    }

    [AvaloniaFact]
    public void CustomDrawingCell_Content_Change_Invalidates_Measure_For_Auto_Width()
    {
        AssertContentChangeMeasureValidity(DataGridLength.Auto, expectedMeasureValid: false);
    }

    private static void AssertContentChangeMeasureValidity(DataGridLength width, bool expectedMeasureValid)
    {
        var item = new Person { Name = "before" };
        var column = new DataGridTextColumn
        {
            Width = width,
            Binding = new Binding(nameof(Person.Name)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
        };
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            AutoGenerateColumns = false,
            ItemsSource = new[] { item },
            UseLogicalScrollable = true,
        };
        grid.ColumnsInternal.Add(column);
        var window = new Window
        {
            Width = 280,
            Height = 160,
        };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;

        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            DataGridRow row = Assert.IsType<DataGridRow>(Assert.Single(grid.DisplayData.GetScrollingRows()));
            DataGridCustomDrawingCell cell = Assert.IsType<DataGridCustomDrawingCell>(row.Cells[0]);
            Assert.True(cell.IsMeasureValid);

            cell.Value = "after";

            Assert.Equal(expectedMeasureValid, cell.IsMeasureValid);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_GenerateElement_Applies_Theme_And_Properties()
    {
        var theme = new ControlTheme(typeof(DataGridCustomDrawingCell));
        var drawFactory = new TestDrawOperationFactory();

        var grid = new DataGrid();
        grid.Resources.Add("DataGridCellCustomDrawingTheme", theme);

        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            DrawOperationFactory = drawFactory,
            DrawingMode = DataGridCustomDrawingMode.TextAndDrawOperation,
            RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
            Foreground = Brushes.Red,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
            SharedTextLayoutCacheCapacity = 256,
            DrawOperationLayoutFastPath = true
        };

        grid.ColumnsInternal.Add(column);

        var content = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });

        Assert.Same(theme, content.Theme);
        Assert.Same(drawFactory, content.DrawOperationFactory);
        Assert.Equal(DataGridCustomDrawingMode.TextAndDrawOperation, content.DrawingMode);
        Assert.Equal(DataGridCustomDrawingRenderBackend.CompositionCustomVisual, content.RenderBackend);
        Assert.Equal(Brushes.Red, content.Foreground);
        Assert.Equal(TextAlignment.Right, content.TextAlignment);
        Assert.Equal(TextTrimming.CharacterEllipsis, content.TextTrimming);
        Assert.Equal(DataGridCustomDrawingTextLayoutCacheMode.Shared, content.TextLayoutCacheMode);
        Assert.Equal(256, content.SharedTextLayoutCacheCapacity);
        Assert.True(content.DrawOperationLayoutFastPath);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_RefreshCellContent_Updates_DrawFactory()
    {
        var oldFactory = new TestDrawOperationFactory();
        var newFactory = new TestDrawOperationFactory();

        var column = new TestCustomDrawingColumn
        {
            Binding = new Binding("Name"),
            DrawOperationFactory = oldFactory
        };

        var content = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        Assert.Same(oldFactory, content.DrawOperationFactory);

        column.DrawOperationFactory = newFactory;
        column.RefreshCellContentPublic(content, nameof(DataGridCustomDrawingColumn.DrawOperationFactory));

        Assert.Same(newFactory, content.DrawOperationFactory);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_RefreshCellContent_Updates_RenderBackend()
    {
        var column = new TestCustomDrawingColumn
        {
            Binding = new Binding("Name"),
            RenderBackend = DataGridCustomDrawingRenderBackend.ImmediateDrawOperation
        };

        var content = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        Assert.Equal(DataGridCustomDrawingRenderBackend.ImmediateDrawOperation, content.RenderBackend);

        column.RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual;
        column.RefreshCellContentPublic(content, nameof(DataGridCustomDrawingColumn.RenderBackend));

        Assert.Equal(DataGridCustomDrawingRenderBackend.CompositionCustomVisual, content.RenderBackend);
    }

    [AvaloniaFact]
    public void CustomDrawingCell_RetainedTemplate_TearsDownCompositionVisual()
    {
        var cell = new DataGridCustomDrawingCell
        {
            DrawOperationFactory = new TestDrawOperationFactory(),
            DrawingMode = DataGridCustomDrawingMode.DrawOperation,
            RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual
        };
        var window = new Window { Width = 160, Height = 80, Content = cell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(cell.HasCompositionVisualForTesting);

            cell.UseRetainedTemplate();

            Assert.False(cell.HasCompositionVisualForTesting);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_SharedTextCache_Reuses_FormattedText_Between_Cells()
    {
        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
            SharedTextLayoutCacheCapacity = 64
        };

        var grid = new DataGrid();
        grid.ColumnsInternal.Add(column);

        var first = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        var second = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        first.Value = "Ada";
        second.Value = "Ada";

        var available = new Size(180, 24);
        first.Measure(available);
        second.Measure(available);

        var firstFormattedText = GetFormattedText(first);
        var secondFormattedText = GetFormattedText(second);

        Assert.NotNull(firstFormattedText);
        Assert.Same(firstFormattedText, secondFormattedText);
    }

    [AvaloniaFact]
    public void CustomDrawingTextLayoutCache_TryGet_And_Add_Reuses_Entry()
    {
        var cache = new DataGridCustomDrawingTextLayoutCache(capacity: 2);
        var key = new DataGridCustomDrawingTextLayoutCache.CacheKey(
            "Ada",
            FontFamily.Default.Name,
            FontStyle.Normal,
            FontWeight.Normal,
            FontStretch.Normal,
            14,
            TextAlignment.Left,
            TextTrimming.CharacterEllipsis,
            FlowDirection.LeftToRight,
            CultureInfo.InvariantCulture.LCID,
            180,
            24,
            foregroundKind: 1,
            foregroundColor: Colors.Black,
            foregroundOpacity: 1,
            foregroundIdentityHash: 0);
        var text = new FormattedText(
            "Ada",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            Brushes.Black);

        Assert.False(cache.TryGet(key, out _));
        Assert.Same(text, cache.Add(key, text));
        Assert.True(cache.TryGet(key, out FormattedText cached));
        Assert.Same(text, cached);
    }

    [AvaloniaFact]
    public void VirtualTextLayoutCache_Reuses_Entry_And_Evicts_Least_Recently_Used()
    {
        var cache = new DataGridVirtualTextLayoutCache(capacity: 1);
        DataGridCustomDrawingTextLayoutCache.CacheKey firstKey = CreateTextLayoutCacheKey("first");
        DataGridCustomDrawingTextLayoutCache.CacheKey secondKey = CreateTextLayoutCacheKey("second");
        var firstLayout = new TextLayout("first", Typeface.Default, 14, Brushes.Black);
        var secondLayout = new TextLayout("second", Typeface.Default, 14, Brushes.Black);

        Assert.False(cache.TryGet(firstKey, out _));
        DataGridVirtualTextLayout firstCached = cache.Add(firstKey, firstLayout);
        Assert.Same(firstLayout, firstCached.Layout);
        Assert.True(cache.TryGet(firstKey, out DataGridVirtualTextLayout? cached));
        Assert.Same(firstCached, cached);

        DataGridVirtualTextLayout secondCached = cache.Add(secondKey, secondLayout);
        Assert.Same(secondLayout, secondCached.Layout);
        Assert.False(cache.TryGet(firstKey, out _));
        Assert.True(cache.TryGet(secondKey, out cached));
        Assert.Same(secondCached, cached);

        cache.Clear();
        Assert.False(cache.TryGet(secondKey, out _));
    }

    [AvaloniaFact]
    public void VirtualTextDrawOperation_Holds_Render_Data_After_Cache_Clear()
    {
        var cache = new DataGridVirtualTextLayoutCache(capacity: 1);
        DataGridCustomDrawingTextLayoutCache.CacheKey key = CreateTextLayoutCacheKey("rendered");
        var layout = new TextLayout("rendered", Typeface.Default, 14, Brushes.Black);
        DataGridVirtualTextLayout cached = cache.Add(key, layout);
        DataGridVirtualTextRenderData renderData = Assert.IsType<DataGridVirtualTextRenderData>(cached.RenderData);
        var commands = new List<DataGridVirtualTextDrawCommand>
        {
            new(renderData, new Point(2, 3), null),
        };
        var operation = new DataGridVirtualTextDrawOperation(new Rect(new Size(100, 24)), commands);

        Assert.Equal(2, renderData.ReferenceCount);
        cache.Clear();
        Assert.Equal(1, renderData.ReferenceCount);

        operation.Dispose();
        Assert.Equal(0, renderData.ReferenceCount);
        operation.Dispose();
    }

    [AvaloniaFact]
    public void VirtualTextDrawOperation_Renders_The_Same_Glyph_Pixels_As_TextLayout()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR")))
        {
            return;
        }

        using var control = new VirtualTextParityControl();
        var size = new Size(240, 80);
        control.Measure(size);
        control.Arrange(new Rect(size));
        using var frame = new RenderTargetBitmap(new PixelSize(240, 80), new Vector(96, 96));
        frame.Render(control);
        using var stream = new MemoryStream();
        frame.Save(stream);
        stream.Position = 0;
        using SKBitmap bitmap = SKBitmap.Decode(stream);
        Assert.NotNull(bitmap);

        const int halfHeight = 40;
        int mismatches = 0;
        for (int y = 0; y < halfHeight; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != bitmap.GetPixel(x, y + halfHeight))
                {
                    mismatches++;
                }
            }
        }

        Assert.Equal(0, mismatches);
    }

    private static DataGridCustomDrawingTextLayoutCache.CacheKey CreateTextLayoutCacheKey(string text) =>
        new(
            text,
            FontFamily.Default.Name,
            FontStyle.Normal,
            FontWeight.Normal,
            FontStretch.Normal,
            14,
            TextAlignment.Left,
            TextTrimming.CharacterEllipsis,
            FlowDirection.LeftToRight,
            CultureInfo.InvariantCulture.LCID,
            180,
            24,
            foregroundKind: 1,
            foregroundColor: Colors.Black,
            foregroundOpacity: 1,
            foregroundIdentityHash: 0);

    private sealed class VirtualTextParityControl : Control, IDisposable
    {
        private readonly DataGridVirtualTextLayout _layout = new(new TextLayout(
            "Long virtual text that trims",
            Typeface.Default,
            14,
            Brushes.Black,
            TextAlignment.Center,
            TextWrapping.NoWrap,
            TextTrimming.CharacterEllipsis,
            maxWidth: 150,
            maxHeight: 28,
            maxLines: 1));

        public override void Render(DrawingContext context)
        {
            context.DrawRectangle(Brushes.White, null, new Rect(Bounds.Size));
            using (context.PushClip(new Rect(30, 0, 80, 40)))
            {
                _layout.Layout.Draw(context, new Point(12, 6));
            }
            DataGridVirtualTextRenderData renderData = _layout.RenderData!;
            var commands = new List<DataGridVirtualTextDrawCommand>
            {
                new(renderData, new Point(12, 46), new Rect(30, 40, 80, 40)),
            };
            context.Custom(new DataGridVirtualTextDrawOperation(new Rect(Bounds.Size), commands));
        }

        public void Dispose() => _layout.Dispose();
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_SharedTextCache_Does_Not_Reuse_FormattedText_For_Different_Foreground()
    {
        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
            SharedTextLayoutCacheCapacity = 64
        };

        var grid = new DataGrid();
        grid.ColumnsInternal.Add(column);

        var first = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        var second = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        first.Value = "Ada";
        second.Value = "Ada";
        first.Foreground = Brushes.Black;
        second.Foreground = Brushes.White;

        var available = new Size(180, 24);
        first.Measure(available);
        second.Measure(available);

        var firstFormattedText = GetFormattedText(first);
        var secondFormattedText = GetFormattedText(second);

        Assert.NotNull(firstFormattedText);
        Assert.NotNull(secondFormattedText);
        Assert.NotSame(firstFormattedText, secondFormattedText);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_DrawOperationFastPath_Uses_MeasureProvider()
    {
        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            DrawingMode = DataGridCustomDrawingMode.DrawOperation,
            DrawOperationFactory = new FastPathDrawOperationFactory(),
            DrawOperationLayoutFastPath = true
        };

        var grid = new DataGrid();
        grid.ColumnsInternal.Add(column);

        var cell = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        cell.Value = "Ada";
        cell.Measure(new Size(200, 80));
        cell.Arrange(new Rect(0, 0, 200, 80));

        Assert.Equal(123, cell.DesiredSize.Width);
        Assert.Equal(17, cell.DesiredSize.Height);
        Assert.Null(GetFormattedText(cell));
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_GenerateEditingElement_Applies_Theme_And_Text_Properties()
    {
        var editorTheme = new ControlTheme(typeof(TextBox));
        var grid = new DataGrid();
        grid.Resources.Add("DataGridCellTextBoxTheme", editorTheme);

        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            FontSize = 18,
            Foreground = Brushes.Blue,
            TextAlignment = TextAlignment.Right
        };

        grid.ColumnsInternal.Add(column);

        var editor = column.CreateEditingElement(new DataGridCell(), new Person { Name = "Ada" });

        Assert.Same(editorTheme, editor.Theme);
        Assert.Equal(18, editor.FontSize);
        Assert.Equal(Brushes.Blue, editor.Foreground);
        Assert.Equal(TextAlignment.Right, editor.TextAlignment);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_Prepare_And_Cancel_CellEdit_Uses_TextBox_Text()
    {
        var column = new TestCustomDrawingColumn();
        var editor = new TextBox { Text = "Ada" };

        var unedited = column.PrepareCellForEditPublic(editor, new RoutedEventArgs());
        Assert.Equal("Ada", unedited);

        editor.Text = "Changed";
        column.CancelCellEditPublic(editor, unedited);

        Assert.Equal("Ada", editor.Text);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_PrepareCellForEdit_WithF2_PlacesCaretAtEnd()
    {
        var column = new TestCustomDrawingColumn();
        var editor = new TextBox { Text = "Ada" };
        var args = new KeyEventArgs { Key = Key.F2 };

        var unedited = column.PrepareCellForEditPublic(editor, args);

        Assert.Equal("Ada", unedited);
        Assert.Equal(3, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionEnd);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_InvalidateCustomDrawingCells_Increments_RenderToken()
    {
        var column = new TestCustomDrawingColumn();

        Assert.Equal(0, column.RenderInvalidationToken);
        Assert.Equal(0, column.LayoutInvalidationToken);

        column.InvalidateCustomDrawingCells();

        Assert.Equal(1, column.RenderInvalidationToken);
        Assert.Equal(0, column.LayoutInvalidationToken);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_InvalidateCustomDrawingCells_From_BackgroundThread_Marshals_To_UI()
    {
        var column = new TestCustomDrawingColumn();

        Assert.Equal(0, column.RenderInvalidationToken);

        Task.Run(() => column.InvalidateCustomDrawingCells()).GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, column.RenderInvalidationToken);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_InvalidateCustomDrawingCells_Refreshes_DisplayCell_Tokens()
    {
        var column = new TestCustomDrawingColumn
        {
            Header = "Name",
            Binding = new Binding("Name")
        };

        var cell = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });
        Assert.Equal(0, cell.RenderInvalidationToken);
        Assert.Equal(0, cell.LayoutInvalidationToken);

        column.InvalidateCustomDrawingCells(invalidateMeasure: true);
        column.RefreshCellContentPublic(cell, nameof(DataGridCustomDrawingColumn.RenderInvalidationToken));
        column.RefreshCellContentPublic(cell, nameof(DataGridCustomDrawingColumn.LayoutInvalidationToken));

        Assert.Equal(1, cell.RenderInvalidationToken);
        Assert.Equal(1, cell.LayoutInvalidationToken);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_InvalidationSourceFactory_Updates_InvalidationTokens()
    {
        var factory = new InvalidatingDrawOperationFactory();
        var column = new TestCustomDrawingColumn
        {
            DrawOperationFactory = factory
        };
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(column);
        _ = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });

        Assert.Equal(0, column.RenderInvalidationToken);
        Assert.Equal(0, column.LayoutInvalidationToken);

        factory.RaiseInvalidated(new DataGridCellDrawOperationInvalidatedEventArgs(invalidateMeasure: true));

        Assert.Equal(1, column.RenderInvalidationToken);
        Assert.Equal(1, column.LayoutInvalidationToken);
    }

    [AvaloniaFact]
    public void CustomDrawingColumn_InvalidationSourceFactory_Unsubscribes_When_Column_Removed()
    {
        var factory = new InvalidatingDrawOperationFactory();
        var column = new TestCustomDrawingColumn
        {
            DrawOperationFactory = factory
        };
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(column);
        _ = column.CreateDisplayElement(new DataGridCell(), new Person { Name = "Ada" });

        factory.RaiseInvalidated(new DataGridCellDrawOperationInvalidatedEventArgs());
        Assert.Equal(1, column.RenderInvalidationToken);

        grid.ColumnsInternal.Remove(column);
        factory.RaiseInvalidated(new DataGridCellDrawOperationInvalidatedEventArgs());

        Assert.Equal(1, column.RenderInvalidationToken);
    }

    [AvaloniaFact]
    public void CustomDrawingCompositionHandler_Drops_Equivalent_Operation_Without_Replacing_Current()
    {
        var handler = new DataGridCustomDrawingCompositionVisualHandler();
        var first = new EquatableTestDrawOperation(new Rect(0, 0, 120, 24), key: 1);
        var equivalent = new EquatableTestDrawOperation(new Rect(0, 0, 120, 24), key: 1);

        Assert.True(SetHandlerOperation(handler, first));
        Assert.False(SetHandlerOperation(handler, equivalent));

        Assert.Equal(1, equivalent.DisposeCount);
        Assert.Equal(0, first.DisposeCount);
        Assert.Same(first, GetHandlerOperation(handler));
    }

    [AvaloniaFact]
    public void CustomDrawingCompositionHandler_Replaces_NonEquivalent_Operation_And_Disposes_Previous()
    {
        var handler = new DataGridCustomDrawingCompositionVisualHandler();
        var first = new EquatableTestDrawOperation(new Rect(0, 0, 120, 24), key: 1);
        var second = new EquatableTestDrawOperation(new Rect(0, 0, 120, 24), key: 2);

        Assert.True(SetHandlerOperation(handler, first));
        Assert.True(SetHandlerOperation(handler, second));

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
        Assert.Same(second, GetHandlerOperation(handler));

        Assert.True(SetHandlerOperation(handler, null));
        Assert.Equal(1, second.DisposeCount);
    }

    private sealed class TestCustomDrawingColumn : DataGridCustomDrawingColumn
    {
        public DataGridCustomDrawingCell CreateDisplayElement(DataGridCell cell, object dataItem)
        {
            return (DataGridCustomDrawingCell)GenerateElement(cell, dataItem);
        }

        public TextBox CreateEditingElement(DataGridCell cell, object dataItem)
        {
            return (TextBox)GenerateEditingElementDirect(cell, dataItem);
        }

        public object PrepareCellForEditPublic(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            return PrepareCellForEdit(editingElement, editingEventArgs);
        }

        public void CancelCellEditPublic(Control editingElement, object uneditedValue)
        {
            CancelCellEdit(editingElement, uneditedValue);
        }

        public void RefreshCellContentPublic(Control element, string propertyName)
        {
            RefreshCellContent(element, propertyName);
        }
    }

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDrawOperationFactory : IDataGridCellDrawOperationFactory
    {
        public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
        {
            return new TestDrawOperation(context.Bounds);
        }
    }

    private sealed class FastPathDrawOperationFactory :
        IDataGridCellDrawOperationFactory,
        IDataGridCellDrawOperationMeasureProvider,
        IDataGridCellDrawOperationArrangeProvider
    {
        public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
        {
            return new TestDrawOperation(context.Bounds);
        }

        public bool TryMeasure(DataGridCellDrawOperationMeasureContext context, out Size desiredSize)
        {
            desiredSize = new Size(123, 17);
            return true;
        }

        public bool TryArrange(DataGridCellDrawOperationArrangeContext context, out Size arrangedSize)
        {
            arrangedSize = context.FinalSize;
            return true;
        }
    }

    private sealed class InvalidatingDrawOperationFactory :
        IDataGridCellDrawOperationFactory,
        IDataGridCellDrawOperationInvalidationSource
    {
        public event EventHandler<DataGridCellDrawOperationInvalidatedEventArgs>? Invalidated;

        public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
        {
            return new TestDrawOperation(context.Bounds);
        }

        public void RaiseInvalidated(DataGridCellDrawOperationInvalidatedEventArgs args)
        {
            Invalidated?.Invoke(this, args);
        }
    }

    private sealed class TestDrawOperation : ICustomDrawOperation
    {
        public TestDrawOperation(Rect bounds)
        {
            Bounds = bounds;
        }

        public Rect Bounds { get; }

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public bool HitTest(Point p)
        {
            return Bounds.Contains(p);
        }

        public void Render(ImmediateDrawingContext context)
        {
        }
    }

    private sealed class EquatableTestDrawOperation : ICustomDrawOperation
    {
        public EquatableTestDrawOperation(Rect bounds, int key)
        {
            Bounds = bounds;
            Key = key;
        }

        public Rect Bounds { get; }

        public int Key { get; }

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is EquatableTestDrawOperation operation &&
                   Key == operation.Key &&
                   Bounds.Equals(operation.Bounds);
        }

        public bool HitTest(Point p)
        {
            return Bounds.Contains(p);
        }

        public void Render(ImmediateDrawingContext context)
        {
        }
    }

    private static FormattedText? GetFormattedText(DataGridCustomDrawingCell cell)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(DataGridCustomDrawingCell).GetField("_formattedText", flags);
        return field?.GetValue(cell) as FormattedText;
    }

    private static ICustomDrawOperation? GetHandlerOperation(DataGridCustomDrawingCompositionVisualHandler handler)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(DataGridCustomDrawingCompositionVisualHandler).GetField("_drawOperation", flags);
        return field?.GetValue(handler) as ICustomDrawOperation;
    }

    private static bool SetHandlerOperation(
        DataGridCustomDrawingCompositionVisualHandler handler,
        ICustomDrawOperation? operation)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var method = typeof(DataGridCustomDrawingCompositionVisualHandler).GetMethod("SetDrawOperation", flags);
        return method?.Invoke(handler, new object?[] { operation }) is bool changed && changed;
    }
}
