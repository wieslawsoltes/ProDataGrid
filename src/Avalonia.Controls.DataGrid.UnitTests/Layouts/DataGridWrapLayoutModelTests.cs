// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.DataGridLayouts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layouts;

public class DataGridWrapLayoutModelTests
{
    [Fact]
    public void Defaults_match_items_repeater_wrap_layout()
    {
        var model = new DataGridWrapLayoutModel();

        Assert.Equal(DataGridLayoutOrientation.Horizontal, model.Orientation);
        Assert.Equal(0, model.HorizontalSpacing);
        Assert.Equal(0, model.VerticalSpacing);
        Assert.Equal(256, model.MaximumCachedLines);
    }

    [Fact]
    public void Horizontal_flow_wraps_variable_size_items()
    {
        var model = new DataGridWrapLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        Size[] sizes = { new(60, 20), new(60, 30), new(40, 10) };
        var context = new TestContext(sizes.Length, index => sizes[index], new Rect(0, 0, 100, 100), default);
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(100, 100));

        Assert.Equal(new Size(100, 50), extent);
        Assert.Equal(new Rect(0, 0, 60, 20), context.GetLayoutBounds(0));
        Assert.Equal(new Rect(0, 20, 60, 30), context.GetLayoutBounds(1));
        Assert.Equal(new Rect(60, 20, 40, 10), context.GetLayoutBounds(2));
    }

    [Fact]
    public void Random_jump_realizes_only_intersecting_lines()
    {
        var model = new DataGridWrapLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(10_000, _ => new Size(40, 20), new Rect(0, 1000, 100, 40), new Vector(0, 1000));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(100, 40));
        algorithm.Arrange(context, new Size(100, 40));

        Assert.Equal(new Size(100, 100_000), extent);
        Assert.Equal(Enumerable.Range(100, 4), context.RealizedIndices);
        Assert.Equal(0, context.GetArrangedBounds(100).Y);
    }

    [Fact]
    public void Vertical_flow_wraps_into_virtualized_columns()
    {
        var model = new DataGridWrapLayoutModel { Orientation = DataGridLayoutOrientation.Vertical };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(100, _ => new Size(20, 40), new Rect(40, 0, 20, 100), new Vector(40, 0));
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(20, 100));

        Assert.Equal(new Size(1000, 100), extent);
        Assert.Equal(Enumerable.Range(4, 2), context.RealizedIndices);
    }

    [Fact]
    public void Spacing_participates_in_wrap_and_extent_math()
    {
        var model = new DataGridWrapLayoutModel { HorizontalSpacing = 10, VerticalSpacing = 5 };
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var context = new TestContext(4, _ => new Size(45, 20), new Rect(0, 0, 100, 100), default);
        algorithm.Initialize(context);

        Size extent = algorithm.Measure(context, new Size(100, 100));

        Assert.Equal(new Size(100, 45), extent);
        Assert.Equal(55, context.GetLayoutBounds(1).X);
        Assert.Equal(25, context.GetLayoutBounds(2).Y);
    }

    [Fact]
    public void Model_changes_request_layout_invalidation()
    {
        var model = new DataGridWrapLayoutModel();
        DataGridLayoutInvalidationKind? kind = null;
        model.LayoutInvalidated += (_, args) => kind = args.Kind;

        model.MaximumCachedLines = 32;

        Assert.Equal(DataGridLayoutInvalidationKind.Measure, kind);
    }

    [Fact]
    public void Navigation_uses_variable_line_geometry_and_preserves_cross_axis_anchor()
    {
        var model = new DataGridWrapLayoutModel();
        IDataGridLayoutAlgorithm algorithm = model.CreateAlgorithm();
        var navigation = Assert.IsAssignableFrom<IDataGridLayoutNavigation>(algorithm);
        Size[] sizes = { new(100, 20), new(40, 30), new(60, 20), new(50, 20), new(50, 20) };
        var context = new TestContext(sizes.Length, index => sizes[index], new Rect(0, 0, 100, 100), default);
        algorithm.Initialize(context);
        algorithm.Measure(context, new Size(100, 100));

        Assert.True(navigation.TryResolveNavigation(
            context,
            new DataGridLayoutNavigationRequest(0, DataGridLayoutNavigationDirection.Down, new Rect(0, 0, 100, 100), new Point(85, 10)),
            out DataGridLayoutNavigationResult down));
        Assert.Equal(2, down.ItemIndex);
        Assert.Equal(context.GetLayoutBounds(2), down.EstimatedBounds);

        Assert.True(navigation.TryResolveNavigation(
            context,
            new DataGridLayoutNavigationRequest(2, DataGridLayoutNavigationDirection.LineStart, new Rect(0, 0, 100, 100), new Point(85, 35)),
            out DataGridLayoutNavigationResult lineStart));
        Assert.Equal(1, lineStart.ItemIndex);
    }

    private sealed class TestContext : IDataGridLayoutContext
    {
        private readonly Func<int, Size> _sizeSelector;
        private readonly List<Control> _realized = new();
        private readonly Dictionary<int, TestControl> _elements = new();
        private readonly Dictionary<int, Rect> _layoutBounds = new();

        public TestContext(int itemCount, Func<int, Size> sizeSelector, Rect realizationRect, Vector scrollOffset)
        {
            ItemCount = itemCount;
            _sizeSelector = sizeSelector;
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
                element = new TestControl(index, _sizeSelector(index));
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
        public Size GetEstimatedItemSize(int index) => _sizeSelector(index);

        public double GetEstimatedItemOffset(int index, DataGridLayoutOrientation orientation)
        {
            Size estimate = _sizeSelector(index);
            return index * (orientation == DataGridLayoutOrientation.Vertical ? estimate.Height : estimate.Width);
        }

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
