// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layouts;

public class DataGridLayoutIntegrationTests
{
    [AvaloniaFact]
    public void Stack_model_uses_datagrid_container_virtualization()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 10_000);
        try
        {
            grid.LayoutModel = new DataGridStackLayoutModel { Spacing = 3 };
            window.Show();
            window.UpdateLayout();

            DataGridRowsPresenter presenter = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().Single();
            DataGridRow[] realized = GetRealizedRows(grid);
            Assert.NotEmpty(realized);
            Assert.True(realized.Length < 30);
            Assert.True(presenter.Extent.Height > presenter.Viewport.Height);
            Assert.Equal(3, realized[1].Bounds.Y - realized[0].Bounds.Bottom, precision: 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_models_can_be_switched_at_runtime()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 100);
        try
        {
            var stack = new DataGridStackLayoutModel();
            var uniform = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32
            };
            grid.LayoutModel = stack;

            window.Show();
            window.UpdateLayout();
            DataGridRow firstBefore = GetRealizedRows(grid).Single(row => row.Slot == 0);

            grid.LayoutModel = uniform;
            window.UpdateLayout();

            DataGridRow[] tiledRows = GetRealizedRows(grid);
            Assert.Contains(tiledRows, row => row.Bounds.X > 0);
            Assert.Same(firstBefore, tiledRows.Single(row => row.Slot == 0));

            grid.LayoutModel = stack;
            window.UpdateLayout();
            Assert.All(GetRealizedRows(grid), row => Assert.Equal(0, row.Bounds.X));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void User_defined_layout_algorithm_runs_through_public_context()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            var model = new TestLayoutModel();
            grid.LayoutModel = model;

            window.Show();
            window.UpdateLayout();

            Assert.True(model.Algorithm.MeasureCount > 0);
            Assert.Equal(7, GetRealizedRows(grid).Single(row => row.Slot == 0).Bounds.X);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scroll_into_view_uses_layout_geometry()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 500);
        try
        {
            grid.LayoutModel = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32
            };
            window.Show();
            window.UpdateLayout();

            Item target = ((IEnumerable<Item>)grid.ItemsSource!).ElementAt(420);
            grid.ScrollIntoView(target, grid.ColumnsInternal[0]);
            window.UpdateLayout();

            DataGridRow row = GetRealizedRows(grid).Single(candidate => candidate.Index == 420);
            DataGridRowsPresenter presenter = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().Single();
            Assert.InRange(row.Bounds.Y, 0, presenter.Viewport.Height - 1);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(out DataGrid grid, int itemCount)
    {
        var window = new Window { Width = 360, Height = 260 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = Enumerable.Range(0, itemCount).Select(index => new Item($"Item {index}")).ToArray(),
            RowHeight = 24,
            UseLogicalScrollable = true
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Width = new DataGridLength(90),
            Binding = new Binding(nameof(Item.Name))
        });
        window.Content = grid;
        return window;
    }

    private static DataGridRow[] GetRealizedRows(DataGrid grid)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => !row.IsRecycled && row.Slot >= 0)
            .OrderBy(row => row.Slot)
            .ToArray();
    }

    private sealed record Item(string Name);

    private sealed class TestLayoutModel : DataGridLayoutModelBase
    {
        public TestLayoutAlgorithm Algorithm { get; } = new();

        public override IDataGridLayoutAlgorithm CreateAlgorithm() => Algorithm;
    }

    private sealed class TestLayoutAlgorithm : IDataGridLayoutAlgorithm
    {
        public int MeasureCount { get; private set; }

        public void Initialize(IDataGridLayoutContext context)
        {
        }

        public Size Measure(IDataGridLayoutContext context, Size availableSize)
        {
            MeasureCount++;
            int count = System.Math.Min(2, context.ItemCount);
            for (int index = 0; index < count; index++)
            {
                Control element = context.GetOrCreateElementAt(index);
                element.Measure(new Size(80, 30));
                context.SetLayoutBounds(index, new Rect(7 + (index * 82), 5, 80, 30));
            }
            return new Size(171, 35);
        }

        public Size Arrange(IDataGridLayoutContext context, Size finalSize)
        {
            IReadOnlyList<Control> realized = context.RealizedElements;
            for (int index = 0; index < realized.Count; index++)
            {
                Control element = realized[index];
                int itemIndex = context.GetElementIndex(element);
                if (context.TryGetLayoutBounds(itemIndex, out Rect bounds))
                {
                    element.Arrange(bounds);
                }
            }
            return finalSize;
        }

        public void OnItemsChanged(IDataGridLayoutContext context, NotifyCollectionChangedEventArgs change)
        {
        }

        public void Uninitialize(IDataGridLayoutContext context)
        {
        }
    }
}
