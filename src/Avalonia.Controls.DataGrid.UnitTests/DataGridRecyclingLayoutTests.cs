// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Linq;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRecyclingLayoutTests
{
    [AvaloniaTheory]
    [InlineData(DataGridTheme.Simple)]
    [InlineData(DataGridTheme.SimpleV2)]
    [InlineData(DataGridTheme.Fluent)]
    [InlineData(DataGridTheme.FluentV2)]
    public void Initial_Auto_Sizing_In_A_Narrow_Viewport_Does_Not_Realize_All_Rows(DataGridTheme theme)
    {
        var factory = new TrackingFactory();
        var items = Enumerable.Range(0, 1000).ToArray();
        var grid = new DataGrid
        {
            Width = 60,
            Height = 240,
            AutoGenerateColumns = false,
            ItemsSource = items,
            RealizationFactory = factory,
        };
        foreach (var width in new[] { new DataGridLength(2, DataGridLengthUnitType.Star), new DataGridLength(100), DataGridLength.Auto })
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Column",
                Width = width,
                CellTemplate = new FuncDataTemplate<int>((item, _) => new TextBlock { Text = $"Value {item}", Height = 24 }),
            });
        }
        var window = new Window { Width = 600, Height = 500, Content = grid };
        window.SetThemeStyles(theme);
        try
        {
            window.Show();
            Assert.True(grid.TryFindResource(typeof(DataGrid), out var resource));
            grid.Theme = Assert.IsType<Avalonia.Styling.ControlTheme>(resource);
            window.UpdateLayout();
            Assert.InRange(factory.CreatedRows, 1, 32);
            var rows = grid.DisplayData.GetScrollingElements().OfType<DataGridRow>().ToArray();
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.True(row.DesiredSize.Height >= 24));
            Assert.False(grid.AutoSizingColumns);

            grid.ScrollIntoView(items[999], grid.Columns[0]);
            window.UpdateLayout();
            Assert.Contains(grid.DisplayData.GetScrollingElements().OfType<DataGridRow>(), row => row.Index == 999);
            Assert.InRange(factory.CreatedRows, 1, 64);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridTheme.Simple)]
    [InlineData(DataGridTheme.SimpleV2)]
    [InlineData(DataGridTheme.Fluent)]
    [InlineData(DataGridTheme.FluentV2)]
    public void Shrinking_Viewport_With_Pending_Auto_Sizing_Does_Not_Reenter_Row_Layout(DataGridTheme theme)
    {
        var grid = new DataGrid
        {
            Width = 380,
            Height = 360,
            AutoGenerateColumns = false,
            ItemsSource = Enumerable.Range(0, 100).ToArray(),
            UseLogicalScrollable = true,
            RowHeight = 24,
            RealizationFactory = new TrackingFactory(),
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Auto", Width = DataGridLength.Auto });
        grid.Columns.Add(new DataGridTextColumn { Header = "Star", Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var window = new Window { Width = 400, Height = 400 };
        window.SetThemeStyles(theme);
        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            var rows = grid.DisplayData.GetScrollingElements().OfType<TrackingRow>().ToArray();
            Assert.True(rows.Length > 5);
            foreach (var row in rows)
            {
                foreach (var child in row.GetVisualDescendants().OfType<Layoutable>())
                {
                    child.InvalidateArrange();
                }
                row.InvalidateArrange();
            }

            grid.AutoSizingColumns = true;
            grid.RowsPresenterAvailableSize = new Size(400, 48);
            grid.OnRowsPresenterViewportChanged(new Size(400, 400), new Size(400, 48));
            Assert.InRange(grid.DisplayData.NumDisplayedScrollingElements, 1, 3);
            Assert.All(rows.Skip(3), row => Assert.False(row.IsVisible));
            grid.UpdateLayout();

            Assert.All(rows, row => Assert.Equal(1, row.MaximumArrangeDepth));
            Assert.False(grid.AutoSizingColumns);
            var displayed = grid.DisplayData.GetScrollingElements().OfType<DataGridRow>().ToArray();
            Assert.Equal(displayed.Length, displayed.Distinct().Count());
            Assert.All(displayed, row => Assert.True(row.IsVisible));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    public void Offscreen_Hiding_Does_Not_Layout_Children_And_Container_Can_Be_Reused(int containerKind, bool initiallyArranged)
    {
        var grid = new DataGrid { RecycledContainerHidingMode = DataGridRecycleHidingMode.MoveOffscreen };
        var child = new LayoutTrackingControl();
        TemplatedControl container = containerKind switch
        {
            0 => new DataGridRow(),
            1 => new DataGridRowGroupHeader(),
            2 => new DataGridRowGroupFooter(),
            _ => throw new ArgumentOutOfRangeException(nameof(containerKind)),
        };
        container.Template = new FuncControlTemplate((_, _) => child);
        var originalBounds = new Rect(0, 0, 100, 24);
        container.Measure(originalBounds.Size);
        if (initiallyArranged)
        {
            container.Arrange(originalBounds);
        }
        int measureCount = child.MeasureCount;
        int arrangeCount = child.ArrangeCount;
        child.InvalidateMeasure();
        container.InvalidateMeasure();

        grid.HideRecycledElement(container);
        grid.HideRecycledElement(container);

        Assert.False(container.IsVisible);
        Assert.Equal(new Rect(-10000, -10000, 100, 24), container.Bounds);
        Assert.Equal(measureCount, child.MeasureCount);
        Assert.Equal(arrangeCount, child.ArrangeCount);

        // Reusing a container at its previous position must restore its bounds even though
        // hiding did not change Avalonia's cached arrange rectangle.
        container.ClearValue(Visual.IsVisibleProperty);
        container.Measure(originalBounds.Size);
        container.Arrange(originalBounds);

        Assert.True(container.IsVisible);
        Assert.Equal(originalBounds, container.Bounds);
        Assert.Equal(originalBounds, child.Bounds);
        Assert.True(child.MeasureCount > measureCount);
        Assert.True(child.ArrangeCount > arrangeCount);
    }

    private sealed class LayoutTrackingControl : Control
    {
        public int MeasureCount { get; private set; }
        public int ArrangeCount { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            MeasureCount++;
            return new Size(100, 24);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ArrangeCount++;
            return finalSize;
        }
    }

    private sealed class TrackingFactory : DataGridRealizationFactory
    {
        public int CreatedRows { get; private set; }
        public override DataGridRow CreateRow(DataGridRowRealizationContext context)
        {
            CreatedRows++;
            return new TrackingRow();
        }
    }

    private sealed class TrackingRow : DataGridRow
    {
        private int _arrangeDepth;
        public int MaximumArrangeDepth { get; private set; }
        protected override Type StyleKeyOverride => typeof(DataGridRow);

        protected override Size ArrangeOverride(Size finalSize)
        {
            _arrangeDepth++;
            try
            {
                MaximumArrangeDepth = Math.Max(MaximumArrangeDepth, _arrangeDepth);
                // Fail before the regression can overflow the test runner's stack.
                Assert.True(_arrangeDepth < 5, "Recycling recursively arranged the same row.");
                return base.ArrangeOverride(finalSize);
            }
            finally
            {
                _arrangeDepth--;
            }
        }
    }
}
