// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridLayouts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layouts;

public class DataGridUniformGridLayoutModelTests
{
    [Fact]
    public void Defaults_match_items_repeater_uniform_grid()
    {
        var model = new DataGridUniformGridLayoutModel();

        Assert.Equal(DataGridLayoutOrientation.Horizontal, model.Orientation);
        Assert.True(double.IsNaN(model.MinItemWidth));
        Assert.True(double.IsNaN(model.MinItemHeight));
        Assert.Equal(int.MaxValue, model.MaximumRowsOrColumns);
        Assert.Equal(DataGridUniformGridItemsJustification.Start, model.ItemsJustification);
        Assert.Equal(DataGridUniformGridItemsStretch.None, model.ItemsStretch);
    }

    [Fact]
    public void Horizontal_flow_realizes_only_intersecting_rows()
    {
        var model = new DataGridUniformGridLayoutModel
        {
            MinItemWidth = 50,
            MinItemHeight = 20
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(1000, new Size(50, 20), new Rect(0, 100, 200, 40), new Vector(0, 100));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(200, 40));
        algorithm.Arrange(context, new Size(200, 40));

        Assert.Equal(new Size(200, 5000), extent);
        Assert.Equal(Enumerable.Range(20, 8), context.RealizedIndices);
        Assert.Equal(0, context.GetArrangedBounds(20).Y);
    }

    [Fact]
    public void Vertical_flow_virtualizes_columns()
    {
        var model = new DataGridUniformGridLayoutModel
        {
            Orientation = DataGridLayoutOrientation.Vertical,
            MinItemWidth = 40,
            MinItemHeight = 25
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(40, new Size(40, 25), new Rect(80, 0, 40, 100), new Vector(80, 0));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(40, 100));

        Assert.Equal(new Size(400, 100), extent);
        Assert.Equal(Enumerable.Range(8, 4), context.RealizedIndices);
    }

    [Fact]
    public void Fill_stretches_cells_across_the_available_line()
    {
        var model = new DataGridUniformGridLayoutModel
        {
            MinItemWidth = 100,
            MinItemHeight = 30,
            MinColumnSpacing = 10,
            ItemsStretch = DataGridUniformGridItemsStretch.Fill
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(2, new Size(100, 30), new Rect(0, 0, 220, 30), default);
        algorithm.Initialize(context);

        algorithm.Measure(context, new Size(220, 30));

        Assert.Equal(105, context.GetLayoutBounds(0).Width);
        Assert.Equal(115, context.GetLayoutBounds(1).X);
    }

    [Fact]
    public void Center_justification_aligns_a_partial_final_line()
    {
        var model = new DataGridUniformGridLayoutModel
        {
            MinItemWidth = 100,
            MinItemHeight = 30,
            MaximumRowsOrColumns = 2,
            ItemsJustification = DataGridUniformGridItemsJustification.Center
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(3, new Size(100, 30), new Rect(0, 0, 300, 60), default);
        algorithm.Initialize(context);

        algorithm.Measure(context, new Size(300, 60));

        Assert.Equal(100, context.GetLayoutBounds(2).X);
    }

    [Fact]
    public void Natural_cell_size_is_measured_when_minimums_are_unspecified()
    {
        var model = new DataGridUniformGridLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(10, new Size(75, 25), new Rect(0, 0, 150, 25), default);
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(150, 25));

        Assert.Equal(new Size(150, 125), extent);
        Assert.Equal(2, context.RealizedIndices.Count);
    }

    [Fact]
    public void Navigation_follows_grid_geometry_and_reports_estimated_bounds()
    {
        var model = new DataGridUniformGridLayoutModel
        {
            MinItemWidth = 50,
            MinItemHeight = 20
        };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var navigation = Assert.IsAssignableFrom<IDataGridLayoutNavigation>(algorithm);
        var context = new TestContext(100, new Size(50, 20), new Rect(0, 0, 200, 20), default);
        algorithm.Initialize(context);
        algorithm.Measure(context, new Size(200, 20));

        Assert.True(navigation.TryResolveNavigation(
            context,
            new DataGridLayoutNavigationRequest(2, DataGridLayoutNavigationDirection.Down, new Rect(0, 0, 200, 20), new Point(125, 10)),
            out DataGridLayoutNavigationResult down));
        Assert.Equal(6, down.ItemIndex);
        Assert.Equal(new Rect(100, 20, 50, 20), down.EstimatedBounds);

        Assert.True(navigation.TryResolveNavigation(
            context,
            new DataGridLayoutNavigationRequest(6, DataGridLayoutNavigationDirection.LineStart, new Rect(0, 0, 200, 20), new Point(125, 30)),
            out DataGridLayoutNavigationResult lineStart));
        Assert.Equal(4, lineStart.ItemIndex);
    }

    private sealed class TestContext : IDataGridLayoutContext
    {
        private readonly Size _itemSize;
        private readonly List<Control> _realized = new();
        private readonly Dictionary<int, TestControl> _elements = new();
        private readonly Dictionary<int, Rect> _layoutBounds = new();

        public TestContext(int itemCount, Size itemSize, Rect realizationRect, Vector scrollOffset)
        {
            ItemCount = itemCount;
            _itemSize = itemSize;
            RealizationRect = realizationRect;
            ScrollOffset = scrollOffset;
        }

        public int ItemCount { get; }
        public Rect RealizationRect { get; }
        public Vector ScrollOffset { get; }
        public int RecommendedAnchorIndex => -1;
        public Point LayoutOrigin { get; set; }
        public object? LayoutState { get; set; }
        public IReadOnlyList<Control> RealizedElements => _realized;
        public IReadOnlyList<int> RealizedIndices => _elements.Keys.OrderBy(index => index).ToArray();

        public Control GetOrCreateElementAt(int index)
        {
            if (!_elements.TryGetValue(index, out TestControl? element))
            {
                element = new TestControl(index, _itemSize);
                _elements.Add(index, element);
                _realized.Add(element);
            }
            return element;
        }

        public void RecycleElement(Control element)
        {
            int index = GetElementIndex(element);
            if (index >= 0)
            {
                _elements.Remove(index);
                _layoutBounds.Remove(index);
                _realized.Remove(element);
            }
        }

        public int GetElementIndex(Control element) => element is TestControl test ? test.Index : -1;
        public Size GetEstimatedItemSize(int index) => _itemSize;

        public double GetEstimatedItemOffset(int index, DataGridLayoutOrientation orientation) =>
            index * (orientation == DataGridLayoutOrientation.Vertical ? _itemSize.Height : _itemSize.Width);

        public void SetLayoutBounds(int index, Rect bounds) => _layoutBounds[index] = bounds;
        public bool TryGetLayoutBounds(int index, out Rect bounds) => _layoutBounds.TryGetValue(index, out bounds);
        public Rect GetLayoutBounds(int index) => _layoutBounds[index];
        public Rect GetArrangedBounds(int index) => _elements[index].Bounds;
    }

    private sealed class TestControl : Control
    {
        private readonly Size _size;

        public TestControl(int index, Size size)
        {
            Index = index;
            _size = size;
        }

        public int Index { get; }

        protected override Size MeasureOverride(Size availableSize) => _size;
    }
}
