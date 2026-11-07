// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Themes.Fluent;
using Avalonia.Media;
using System.Linq;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridImageColumnHeadlessTests
{
    [AvaloniaFact]
    public void ImageColumn_Generates_ImageElement()
    {
        var vm = new ImageTestViewModel();
        var (window, grid) = CreateWindow(vm);

        window.Show();
        grid.ApplyTemplate();
        grid.UpdateLayout();

        var cell = GetCell(grid, "Avatar", 0);
        var image = Assert.IsType<Image>(cell.Content);

        Assert.NotNull(image);
    }

    [AvaloniaFact]
    public void ImageColumn_Respects_Dimensions()
    {
        var column = new DataGridImageColumn
        {
            Header = "Avatar",
            ImageWidth = 32,
            ImageHeight = 32
        };

        Assert.Equal(32, column.ImageWidth);
        Assert.Equal(32, column.ImageHeight);
    }

    [AvaloniaFact]
    public void ImageColumn_Respects_Stretch()
    {
        var column = new DataGridImageColumn
        {
            Header = "Avatar",
            Stretch = Stretch.UniformToFill
        };

        Assert.Equal(Stretch.UniformToFill, column.Stretch);
    }

    [AvaloniaFact]
    public void ImageColumn_IsReadOnlyByDefault()
    {
        var column = new DataGridImageColumn
        {
            Header = "Avatar"
        };

        Assert.True(column.IsReadOnly);
    }

    [AvaloniaFact]
    public void ImageColumn_AllowEditing_MakesEditable()
    {
        var column = new DataGridImageColumn
        {
            Header = "Avatar",
            AllowEditing = true
        };

        Assert.False(column.IsReadOnly);
    }

    [AvaloniaFact]
    public void ImageColumn_Respects_Watermark()
    {
        var column = new TestImageColumn
        {
            AllowEditing = true,
            Watermark = "Enter image URI"
        };

        var editingElement = column.CreateEditingElement(new DataGridCell(), new object());
        var textBox = Assert.IsType<TextBox>(editingElement);

        Assert.Equal("Enter image URI", textBox.Watermark);
    }

    [AvaloniaFact]
    public void ImageColumn_Default_Stretch()
    {
        var column = new DataGridImageColumn();

        Assert.Equal(Stretch.Uniform, column.Stretch);
    }

    [AvaloniaTheory]
    [InlineData(Stretch.UniformToFill, 28d, 8d, 24d, 24d)]
    [InlineData(Stretch.None, 30d, 10d, 20d, 20d)]
    public void Drawn_Image_Uses_Expected_Source_Destination_And_Retained_Viewport(
        Stretch stretch,
        double destinationX,
        double destinationY,
        double destinationWidth,
        double destinationHeight)
    {
        var source = new RecordingImage(new Size(20, 20));
        var item = new RenderImageItem(source);
        TestImageColumn retainedColumn = CreateRenderColumn(
            "Retained",
            DataGridColumnDisplayMode.Retained,
            stretch);
        TestImageColumn drawnColumn = CreateRenderColumn(
            "Drawn",
            DataGridColumnDisplayMode.Drawn,
            stretch);
        var retainedCell = new DataGridCell { DataContext = item };
        Image retainedImage = Assert.IsType<Image>(retainedColumn.CreateDisplay(retainedCell, item));
        retainedImage.Measure(new Size(80, 40));
        Assert.Equal(new Size(24, 12), retainedImage.DesiredSize);

        var drawnCell = Assert.IsType<DataGridCustomDrawingCell>(drawnColumn.CreateCell());
        drawnCell.OwningColumn = drawnColumn;
        drawnCell.DataContext = item;
        Assert.Null(drawnColumn.CreateDisplay(drawnCell, item));
        drawnCell.Measure(new Size(80, 40));
        drawnCell.Arrange(new Rect(0, 0, 80, 40));

        var recorded = new DrawingGroup();
        using (DrawingContext context = recorded.Open())
        {
            DataGridImageCellRenderer.Instance.Render(drawnCell, context);
        }

        DrawingGroup clippedImage = Assert.IsType<DrawingGroup>(Assert.Single(recorded.Children));
        RectangleGeometry clip = Assert.IsType<RectangleGeometry>(clippedImage.ClipGeometry);
        Assert.Equal(new Rect(28, 14, retainedImage.DesiredSize.Width, retainedImage.DesiredSize.Height), clip.Rect);
        Assert.Equal(1, source.DrawCount);
        Assert.Equal(new Rect(0, 0, 20, 20), source.SourceRect);
        Assert.Equal(
            new Rect(destinationX, destinationY, destinationWidth, destinationHeight),
            source.DestinationRect);
        Assert.NotEmpty(clippedImage.Children);
    }

    private static TestImageColumn CreateRenderColumn(
        object header,
        DataGridColumnDisplayMode displayMode,
        Stretch stretch)
    {
        var column = new TestImageColumn
        {
            Header = header,
            Binding = new Binding(nameof(RenderImageItem.Source)),
            DisplayMode = displayMode,
            ImageWidth = 24,
            ImageHeight = 12,
            Stretch = stretch,
            Width = new DataGridLength(80),
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<RenderImageItem, IImage>(
                static item => item.Source));
        return column;
    }

    private sealed class RecordingImage : IImage
    {
        public RecordingImage(Size size)
        {
            Size = size;
        }

        public Size Size { get; }

        public int DrawCount { get; private set; }

        public Rect SourceRect { get; private set; }

        public Rect DestinationRect { get; private set; }

        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
            DrawCount++;
            SourceRect = sourceRect;
            DestinationRect = destRect;
            context.DrawRectangle(Brushes.Red, null, destRect);
        }
    }

    private static (Window window, DataGrid grid) CreateWindow(ImageTestViewModel vm)
    {
        var window = new Window
        {
            Width = 600,
            Height = 400,
            DataContext = vm
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = vm.Items,
            Columns = new ObservableCollection<DataGridColumn>
            {
                new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new Binding("Name")
                },
                new DataGridImageColumn
                {
                    Header = "Avatar",
                    Binding = new Binding("ImagePath"),
                    ImageWidth = 32,
                    ImageHeight = 32
                }
            }
        };

        window.Content = grid;
        return (window, grid);
    }

    private static DataGridCell GetCell(DataGrid grid, string header, int rowIndex)
    {
        return grid
            .GetVisualDescendants()
            .OfType<DataGridCell>()
            .First(c => c.OwningColumn?.Header?.ToString() == header && c.OwningRow?.Index == rowIndex);
    }

    private sealed class ImageTestViewModel
    {
        public ImageTestViewModel()
        {
            Items = new ObservableCollection<ImageItem>
            {
                new() { Name = "User 1", ImagePath = "avares://DataGridSample/Assets/user1.png" },
                new() { Name = "User 2", ImagePath = "avares://DataGridSample/Assets/user2.png" },
                new() { Name = "User 3", ImagePath = "avares://DataGridSample/Assets/user3.png" }
            };
        }

        public ObservableCollection<ImageItem> Items { get; }
    }

    private sealed class ImageItem
    {
        public string Name { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }

    private sealed record RenderImageItem(IImage Source);

    private sealed class TestImageColumn : DataGridImageColumn
    {
        public Control? CreateDisplay(DataGridCell cell, object dataItem) =>
            GenerateElement(cell, dataItem);

        public Control CreateEditingElement(DataGridCell cell, object dataItem) =>
            GenerateEditingElementDirect(cell, dataItem);
    }
}
