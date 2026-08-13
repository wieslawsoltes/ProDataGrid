// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls.DataGridLayouts;
using Avalonia.Controls.DataGridNavigation;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
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

    [AvaloniaFact]
    public void Uniform_grid_navigation_uses_spatial_neighbors()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 100);
        try
        {
            grid.LayoutModel = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32
            };
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

            Assert.True(grid.Navigate(DataGridNavigationCommand.Right));
            Assert.Equal(1, grid.CurrentCell.RowIndex);

            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
            Assert.True(grid.Navigate(DataGridNavigationCommand.Down));
            Assert.True(grid.CurrentCell.RowIndex > 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stack_cross_axis_navigation_falls_back_to_cell_columns()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20, columnCount: 2);
        try
        {
            grid.LayoutModel = new DataGridStackLayoutModel();
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

            Assert.True(grid.Navigate(DataGridNavigationCommand.Right));
            Assert.Equal(0, grid.CurrentCell.RowIndex);
            Assert.Equal(1, grid.CurrentCell.Column.DisplayIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_navigation_resolves_once_and_extends_selection()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            var model = new NavigationTestLayoutModel();
            grid.LayoutModel = model;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

            Assert.True(grid.Navigate(DataGridNavigationCommand.Down, KeyModifiers.Shift));
            Assert.Equal(2, grid.CurrentCell.RowIndex);
            Assert.Equal(1, model.Algorithm.ResolveCount);
            Assert.Equal(3, grid.SelectedItems.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navigation_query_does_not_mutate_the_persistent_layout_anchor()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            var model = new NavigationTestLayoutModel();
            grid.LayoutModel = model;
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

            Assert.True(grid.CanNavigate(DataGridNavigationCommand.Down));
            Assert.True(grid.Navigate(DataGridNavigationCommand.Down));
            Assert.True(grid.Navigate(DataGridNavigationCommand.Down));

            Assert.Equal(3, model.Algorithm.NavigationAnchors.Count);
            Assert.Equal(new Point(50, 15), model.Algorithm.NavigationAnchors[0]);
            Assert.Equal(model.Algorithm.NavigationAnchors[0], model.Algorithm.NavigationAnchors[1]);
            Assert.Equal(new Point(50, 75), model.Algorithm.NavigationAnchors[2]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Logical_rtl_redirect_is_re_resolved_by_the_layout()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            grid.LayoutModel = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32
            };
            grid.FlowDirection = FlowDirection.RightToLeft;
            grid.NavigationModel = new DataGridNavigationModel
            {
                HorizontalNavigationMode = DataGridHorizontalNavigationMode.Logical
            };
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 1, columnIndex: 0);

            Assert.True(grid.Navigate(DataGridNavigationCommand.Left));
            Assert.Equal(2, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Semantic_commands_map_to_the_expected_layout_directions()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            var model = new NavigationTestLayoutModel();
            model.Algorithm.SupportAllDirections = true;
            grid.LayoutModel = model;
            window.Show();
            window.UpdateLayout();

            var cases = new[]
            {
                (DataGridNavigationCommand.Up, KeyModifiers.None, DataGridLayoutNavigationDirection.Up),
                (DataGridNavigationCommand.Up, KeyModifiers.Control, DataGridLayoutNavigationDirection.First),
                (DataGridNavigationCommand.Down, KeyModifiers.None, DataGridLayoutNavigationDirection.Down),
                (DataGridNavigationCommand.Down, KeyModifiers.Control, DataGridLayoutNavigationDirection.Last),
                (DataGridNavigationCommand.Left, KeyModifiers.None, DataGridLayoutNavigationDirection.Left),
                (DataGridNavigationCommand.Left, KeyModifiers.Control, DataGridLayoutNavigationDirection.LineStart),
                (DataGridNavigationCommand.Right, KeyModifiers.None, DataGridLayoutNavigationDirection.Right),
                (DataGridNavigationCommand.Right, KeyModifiers.Control, DataGridLayoutNavigationDirection.LineEnd),
                (DataGridNavigationCommand.PageUp, KeyModifiers.None, DataGridLayoutNavigationDirection.PageUp),
                (DataGridNavigationCommand.PageDown, KeyModifiers.None, DataGridLayoutNavigationDirection.PageDown),
                (DataGridNavigationCommand.RowStart, KeyModifiers.None, DataGridLayoutNavigationDirection.LineStart),
                (DataGridNavigationCommand.RowEnd, KeyModifiers.None, DataGridLayoutNavigationDirection.LineEnd),
                (DataGridNavigationCommand.ColumnStart, KeyModifiers.None, DataGridLayoutNavigationDirection.First),
                (DataGridNavigationCommand.ColumnEnd, KeyModifiers.None, DataGridLayoutNavigationDirection.Last),
                (DataGridNavigationCommand.GridStart, KeyModifiers.None, DataGridLayoutNavigationDirection.First),
                (DataGridNavigationCommand.GridEnd, KeyModifiers.None, DataGridLayoutNavigationDirection.Last)
            };

            foreach ((DataGridNavigationCommand command, KeyModifiers modifiers, DataGridLayoutNavigationDirection expected) in cases)
            {
                SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
                Assert.True(grid.Navigate(command, modifiers));
                Assert.Equal(expected, model.Algorithm.Directions[^1]);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Owned_layout_boundary_does_not_fall_back_to_linear_navigation()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            var model = new NavigationTestLayoutModel();
            model.Algorithm.ResolveTargets = false;
            grid.LayoutModel = model;
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

            Assert.False(grid.CanNavigate(DataGridNavigationCommand.Down));
            Assert.True(grid.Navigate(DataGridNavigationCommand.Down));
            Assert.Equal(0, grid.CurrentCell.RowIndex);
            Assert.Equal(2, model.Algorithm.ResolveCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridNavigationCommand.Right, 2, 0)]
    [InlineData(DataGridNavigationCommand.Left, 3, 5)]
    [InlineData(DataGridNavigationCommand.Down, 19, 0)]
    [InlineData(DataGridNavigationCommand.Up, 0, 19)]
    public void Owned_layout_boundary_wraps_through_spatial_geometry(
        DataGridNavigationCommand command,
        int sourceRowIndex,
        int expectedRowIndex)
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            grid.LayoutModel = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32,
                MaximumRowsOrColumns = 3
            };
            grid.NavigationModel = new DataGridNavigationModel
            {
                HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap,
                VerticalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
            };
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, sourceRowIndex, columnIndex: 0);

            Assert.True(grid.Navigate(command));
            Assert.Equal(expectedRowIndex, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unsupported_stack_cross_axis_keeps_column_wrap()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20, columnCount: 2);
        try
        {
            grid.LayoutModel = new DataGridStackLayoutModel();
            grid.NavigationModel = new DataGridNavigationModel
            {
                HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
            };
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 4, columnIndex: 1);

            Assert.True(grid.Navigate(DataGridNavigationCommand.Right));
            Assert.Equal(4, grid.CurrentCell.RowIndex);
            Assert.Equal(0, grid.CurrentCell.Column.DisplayIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Logical_rtl_redirect_does_not_run_spatial_boundary_policy_twice()
    {
        Window window = CreateWindow(out DataGrid grid, itemCount: 20);
        try
        {
            grid.LayoutModel = new DataGridUniformGridLayoutModel
            {
                MinItemWidth = 100,
                MinItemHeight = 32,
                MaximumRowsOrColumns = 3
            };
            grid.FlowDirection = FlowDirection.RightToLeft;
            grid.NavigationModel = new DataGridNavigationModel
            {
                HorizontalNavigationMode = DataGridHorizontalNavigationMode.Logical,
                HorizontalBoundaryMode = DataGridNavigationBoundaryMode.Wrap
            };
            window.Show();
            window.UpdateLayout();
            SetCurrentCell(grid, rowIndex: 2, columnIndex: 0);

            Assert.False(grid.Navigate(DataGridNavigationCommand.Left));
            Assert.Equal(2, grid.CurrentCell.RowIndex);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(out DataGrid grid, int itemCount, int columnCount = 1)
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
        if (columnCount > 1)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = "Name 2",
                Width = new DataGridLength(90),
                Binding = new Binding(nameof(Item.Name))
            });
        }
        window.Content = grid;
        return window;
    }

    private static void SetCurrentCell(DataGrid grid, int rowIndex, int columnIndex)
    {
        object item = ((System.Collections.IList)grid.ItemsSource!)[rowIndex]!;
        DataGridColumn column = grid.Columns[columnIndex];
        grid.CurrentCell = new DataGridCellInfo(item, column, rowIndex, column.Index, isValid: true);
        grid.UpdateLayout();
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

    private sealed class NavigationTestLayoutModel : DataGridLayoutModelBase
    {
        public NavigationTestLayoutAlgorithm Algorithm { get; } = new();

        public override IDataGridLayoutAlgorithm CreateAlgorithm() => Algorithm;
    }

    private sealed class NavigationTestLayoutAlgorithm : IDataGridLayoutAlgorithm, IDataGridLayoutNavigation
    {
        public int ResolveCount { get; private set; }

        public List<Point> NavigationAnchors { get; } = [];

        public List<DataGridLayoutNavigationDirection> Directions { get; } = [];

        public bool ResolveTargets { get; set; } = true;

        public bool SupportAllDirections { get; set; }

        public void Initialize(IDataGridLayoutContext context)
        {
        }

        public Size Measure(IDataGridLayoutContext context, Size availableSize)
        {
            int count = System.Math.Min(context.ItemCount, 10);
            for (int index = 0; index < count; index++)
            {
                Control element = context.GetOrCreateElementAt(index);
                element.Measure(new Size(100, 30));
                context.SetLayoutBounds(index, GetBounds(index));
            }

            return new Size(100, context.ItemCount * 30);
        }

        public Size Arrange(IDataGridLayoutContext context, Size finalSize)
        {
            foreach (Control element in context.RealizedElements)
            {
                element.Arrange(GetBounds(context.GetElementIndex(element)));
            }

            return finalSize;
        }

        public void OnItemsChanged(IDataGridLayoutContext context, NotifyCollectionChangedEventArgs change)
        {
        }

        public void Uninitialize(IDataGridLayoutContext context)
        {
        }

        public bool SupportsNavigation(DataGridLayoutNavigationDirection direction) =>
            SupportAllDirections || direction == DataGridLayoutNavigationDirection.Down;

        public bool TryGetNavigationBounds(
            IDataGridLayoutContext context,
            int itemIndex,
            Rect viewport,
            out Rect bounds)
        {
            bounds = GetBounds(itemIndex);
            return itemIndex >= 0 && itemIndex < context.ItemCount;
        }

        public bool TryResolveNavigation(
            IDataGridLayoutContext context,
            in DataGridLayoutNavigationRequest request,
            out DataGridLayoutNavigationResult result)
        {
            ResolveCount++;
            NavigationAnchors.Add(request.NavigationAnchor);
            Directions.Add(request.Direction);
            if (!ResolveTargets)
            {
                result = default;
                return false;
            }

            int target = request.CurrentItemIndex + 2;
            result = new DataGridLayoutNavigationResult(target, GetBounds(target));
            return target < context.ItemCount;
        }

        private static Rect GetBounds(int index) => new(0, index * 30, 100, 30);
    }
}
