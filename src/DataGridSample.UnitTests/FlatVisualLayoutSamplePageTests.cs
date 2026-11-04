using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Models;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class FlatVisualLayoutSamplePageTests
{
    [AvaloniaFact]
    public void Flat_Data_Pages_Use_Matched_Workloads_With_All_Visual_Ownership_Modes()
    {
        AssertMatchedFlatDataPage(new VirtualSurfaceFlatDataPage(), DataGridVisualLayoutMode.Virtualized);
        AssertMatchedFlatDataPage(new FlatSurfaceFlatDataPage(), DataGridVisualLayoutMode.Flat);
        AssertMatchedFlatDataPage(new NestedSurfaceFlatDataPage(), DataGridVisualLayoutMode.Nested);
    }

    [AvaloniaFact]
    public void Hierarchy_Pages_Use_Matched_Models_With_All_Visual_Ownership_Modes()
    {
        AssertMatchedHierarchyPage(new VirtualSurfaceHierarchyPage(), DataGridVisualLayoutMode.Virtualized);
        AssertMatchedHierarchyPage(new FlatSurfaceHierarchyPage(), DataGridVisualLayoutMode.Flat);
        AssertMatchedHierarchyPage(new NestedSurfaceHierarchyPage(), DataGridVisualLayoutMode.Nested);
    }

    [AvaloniaFact]
    public void Virtual_Surface_Pages_Keep_The_Grid_Below_Status_Content_And_Inside_The_Viewport()
    {
        AssertVirtualSurfacePageLayout(new VirtualSurfaceFlatDataPage());
        AssertVirtualSurfacePageLayout(new VirtualSurfaceHierarchyPage());
    }

    [AvaloniaFact]
    public void Virtual_Flat_Data_Page_Covers_All_Supported_Renderer_Modes()
    {
        var page = new VirtualSurfaceFlatDataPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedFlatCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            Assert.Same(viewModel.SelectionModel, grid.Selection);
            viewModel.SelectionModel.Select(2);
            PumpLayout(window);
            Assert.Equal(2, viewModel.SelectionModel.SelectedIndex);
            Assert.Single(grid.SelectedItems);
            foreach (VirtualSurfaceModeOption mode in viewModel.VirtualModes)
            {
                viewModel.SelectedVirtualMode = mode;
                PumpLayout(window);
                Assert.Equal(DataGridVisualLayoutMode.Virtualized, grid.VisualLayoutMode);
                AssertCellOwnership(
                    grid,
                    DataGridVisualLayoutMode.Virtualized,
                    mode.ExpectsRowlessSurface,
                    mode.Key);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtual_Hierarchy_Page_Covers_All_Supported_Renderer_Modes()
    {
        var page = new VirtualSurfaceHierarchyPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedHierarchyCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            Assert.NotNull(grid.Selection);
            Assert.NotSame(viewModel.SelectionModel, grid.Selection);
            viewModel.SelectionModel.Select(0);
            PumpLayout(window);
            Assert.Equal(0, viewModel.SelectionModel.SelectedIndex);
            Assert.Single(grid.SelectedItems);
            Assert.Same(viewModel.Model.GetTypedNode(0).Item, grid.Selection.SelectedItem);

            grid.Selection.SelectedIndex = 1;
            PumpLayout(window);
            Assert.Equal(1, viewModel.SelectionModel.SelectedIndex);
            Assert.Same(viewModel.Model.GetTypedNode(1).Item, grid.Selection.SelectedItem);

            grid.Selection.SelectedIndex = 0;
            PumpLayout(window);
            viewModel.Model.ExpandAll();
            PumpLayout(window);

            HierarchicalNode<OptimizedHierarchyCellSampleNode> root = viewModel.Model.GetTypedNode(0);
            int expandedCount = viewModel.Model.Count;
            viewModel.Model.Toggle(root);
            PumpLayout(window);

            Assert.True(viewModel.Model.Count < expandedCount);
            AssertCellOwnership(
                grid,
                DataGridVisualLayoutMode.Virtualized,
                expectsRowlessSurface: true,
                context: "manual-collapse");

            viewModel.Model.Toggle(root);
            PumpLayout(window);

            Assert.Equal(expandedCount, viewModel.Model.Count);
            AssertCellOwnership(
                grid,
                DataGridVisualLayoutMode.Virtualized,
                expectsRowlessSurface: true,
                context: "manual-expand");

            foreach (VirtualSurfaceModeOption mode in viewModel.VirtualModes)
            {
                viewModel.SelectedVirtualMode = mode;
                PumpLayout(window);
                Assert.Equal(DataGridVisualLayoutMode.Virtualized, grid.VisualLayoutMode);
                AssertCellOwnership(
                    grid,
                    DataGridVisualLayoutMode.Virtualized,
                    mode.ExpectsRowlessSurface,
                    mode.Key);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtual_Flat_Data_Page_All_Columns_Have_Writable_Accessors_And_Remain_Rowless()
    {
        var page = new VirtualSurfaceFlatDataPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedFlatCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            foreach (VirtualSurfaceModeOption mode in viewModel.VirtualModes)
            {
                viewModel.SelectedVirtualMode = mode;
                PumpLayout(window);
                AssertAllColumnsEditable(grid, viewModel.Items[0]);
                AssertCellOwnership(
                    grid,
                    DataGridVisualLayoutMode.Virtualized,
                    expectsRowlessSurface: true,
                    context: mode.Key);
            }

            viewModel.SelectedVirtualMode = viewModel.VirtualModes.Single(mode => mode.Key == "virtual");
            PumpLayout(window);
            IDataGridColumnValueAccessor nameAccessor = Assert.IsAssignableFrom<IDataGridColumnValueAccessor>(
                DataGridColumnMetadata.GetValueAccessor(grid.Columns[1]));
            nameAccessor.SetValue(viewModel.Items[0], "Edited flat virtual cell");
            Assert.Equal("Edited flat virtual cell", viewModel.Items[0].Name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtual_Hierarchy_Page_All_Columns_Have_Writable_Accessors_And_Remain_Rowless()
    {
        var page = new VirtualSurfaceHierarchyPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedHierarchyCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            IHierarchicalModel untypedModel = viewModel.Model;
            foreach (VirtualSurfaceModeOption mode in viewModel.VirtualModes)
            {
                viewModel.SelectedVirtualMode = mode;
                PumpLayout(window);
                AssertAllColumnsEditable(grid, untypedModel.Flattened[0]);
                AssertCellOwnership(
                    grid,
                    DataGridVisualLayoutMode.Virtualized,
                    expectsRowlessSurface: true,
                    context: mode.Key);
            }

            viewModel.SelectedVirtualMode = viewModel.VirtualModes.Single(mode => mode.Key == "virtual");
            PumpLayout(window);
            HierarchicalNode node = untypedModel.Flattened[0];
            IDataGridColumnValueAccessor nameAccessor = Assert.IsAssignableFrom<IDataGridColumnValueAccessor>(
                DataGridColumnMetadata.GetValueAccessor(grid.Columns[0]));
            nameAccessor.SetValue(node, "Edited hierarchy virtual cell");
            Assert.Equal(
                "Edited hierarchy virtual cell",
                Assert.IsType<OptimizedHierarchyCellSampleNode>(node.Item).Name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtual_Flat_Data_Page_Integrates_Sorting_Filtering_And_Search()
    {
        var page = new VirtualSurfaceFlatDataPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedFlatCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            VirtualSurfaceDataOperationsViewModel operations = viewModel.Operations;

            Assert.Same(operations.SortingModel, grid.SortingModel);
            Assert.Same(operations.FilteringModel, grid.FilteringModel);
            Assert.Same(operations.SearchModel, grid.SearchModel);
            Assert.Same(operations.FastPathOptions, grid.FastPathOptions);
            Assert.True(grid.CanUserSortColumns);

            operations.SortingModel.SetOrUpdate(new SortingDescriptor(
                nameof(OptimizedCellSampleRow.Name),
                ListSortDirection.Descending,
                nameof(OptimizedCellSampleRow.Name)));
            PumpLayout(window);

            OptimizedCellSampleRow[] sorted = grid.CollectionView.Cast<OptimizedCellSampleRow>().ToArray();
            Assert.Equal("Work item 0001000", sorted[0].Name);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "flat-sort");

            operations.OwnerFilterText = "Atlas";
            Execute(operations.ApplyOwnerFilterCommand);
            PumpLayout(window);

            OptimizedCellSampleRow[] filtered = grid.CollectionView.Cast<OptimizedCellSampleRow>().ToArray();
            Assert.NotEmpty(filtered);
            Assert.True(filtered.Length < viewModel.Items.Count);
            Assert.All(filtered, item => Assert.Equal("Atlas", item.Owner));
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "flat-filter");

            Execute(operations.ClearOwnerFilterCommand);
            operations.SearchQuery = "Work item 00000";
            Execute(operations.ApplySearchCommand);
            PumpLayout(window);

            Assert.NotEmpty(operations.SearchModel.Results);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "flat-search");

            Execute(operations.NextSearchResultCommand);
            PumpLayout(window);
            Assert.Same(operations.SearchModel.CurrentResult!.Item, grid.SelectedItem);

            operations.SearchHighlightingEnabled = true;
            PumpLayout(window);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, false, "flat-search-highlights");

            operations.SearchHighlightingEnabled = false;
            PumpLayout(window);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "flat-search-no-highlights");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtual_Hierarchy_Page_Integrates_Sibling_Sorting_Ancestor_Filtering_And_Search()
    {
        var page = new VirtualSurfaceHierarchyPage();
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedHierarchyCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            VirtualSurfaceDataOperationsViewModel operations = viewModel.Operations;

            Assert.Same(operations.SortingModel, grid.SortingModel);
            Assert.Same(operations.FilteringModel, grid.FilteringModel);
            Assert.Same(operations.SearchModel, grid.SearchModel);
            Assert.Same(operations.FastPathOptions, grid.FastPathOptions);
            Assert.IsType<DataGridHierarchicalFilteringAdapterFactory>(grid.FilteringAdapterFactory);
            Assert.True(grid.CanUserSortColumns);

            operations.SortingModel.SetOrUpdate(new SortingDescriptor(
                nameof(OptimizedHierarchyCellSampleNode.Name),
                ListSortDirection.Descending,
                nameof(OptimizedHierarchyCellSampleNode.Name)));
            PumpLayout(window);

            OptimizedHierarchyCellSampleNode first = viewModel.Model.GetTypedNode(0).Item;
            OptimizedHierarchyCellSampleNode lastRoot = viewModel.Model.Root!.Value.Children[^1].Item;
            Assert.True(string.CompareOrdinal(first.Name, lastRoot.Name) > 0);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "hierarchy-sort");

            int unfilteredCount = grid.CollectionView.Cast<object>().Count();
            operations.OwnerFilterText = "Atlas";
            Execute(operations.ApplyOwnerFilterCommand);
            PumpLayout(window);

            int filteredCount = grid.CollectionView.Cast<object>().Count();
            Assert.InRange(filteredCount, 1, unfilteredCount - 1);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "hierarchy-filter");

            Execute(operations.ClearOwnerFilterCommand);
            operations.SearchQuery = "Portfolio";
            Execute(operations.ApplySearchCommand);
            PumpLayout(window);

            Assert.NotEmpty(operations.SearchModel.Results);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "hierarchy-search");

            Execute(operations.NextSearchResultCommand);
            PumpLayout(window);
            Assert.Same(
                Assert.IsType<HierarchicalNode>(operations.SearchModel.CurrentResult!.Item).Item,
                grid.SelectedItem);

            operations.SearchHighlightingEnabled = true;
            PumpLayout(window);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, false, "hierarchy-search-highlights");

            operations.SearchHighlightingEnabled = false;
            PumpLayout(window);
            AssertCellOwnership(grid, DataGridVisualLayoutMode.Virtualized, true, "hierarchy-search-no-highlights");
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertMatchedFlatDataPage(Control page, DataGridVisualLayoutMode expectedMode)
    {
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedFlatCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            Assert.Equal(1_000, viewModel.Items.Count);
            Assert.Equal(expectedMode, grid.VisualLayoutMode);
            AssertCellOwnership(grid, expectedMode, expectedMode == DataGridVisualLayoutMode.Virtualized);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertVirtualSurfacePageLayout(Control page)
    {
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            Border status = page.FindControl<Border>("StatusPanel")!;
            DataGrid grid = page.GetVisualDescendants().OfType<DataGrid>().Single();
            Rect statusBounds = GetBoundsRelativeTo(status, page);
            Rect gridBounds = GetBoundsRelativeTo(grid, page);

            Assert.True(
                gridBounds.Top >= statusBounds.Bottom,
                $"Grid starts at {gridBounds.Top:F1}, above status bottom {statusBounds.Bottom:F1}.");
            Assert.True(gridBounds.Height > 0, "Grid must retain a visible viewport.");
            Assert.True(
                gridBounds.Bottom <= page.Bounds.Height + 0.5,
                $"Grid bottom {gridBounds.Bottom:F1} exceeds page height {page.Bounds.Height:F1}.");

            SaveScreenshotWhenRequested(window, page.GetType().Name);
        }
        finally
        {
            window.Close();
        }
    }

    private static Rect GetBoundsRelativeTo(Control control, Visual relativeTo)
    {
        Matrix transform = control.TransformToVisual(relativeTo) ?? Matrix.Identity;
        return new Rect(control.Bounds.Size).TransformToAABB(transform);
    }

    private static void SaveScreenshotWhenRequested(Window window, string fileName)
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            return;
        }

        Directory.CreateDirectory(outputDirectory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using FileStream stream = File.Create(Path.Combine(outputDirectory, $"{fileName}.png"));
        frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }

    private static void AssertMatchedHierarchyPage(Control page, DataGridVisualLayoutMode expectedMode)
    {
        Window window = CreateHostWindow(page);
        try
        {
            window.Show();
            PumpLayout(window);

            var viewModel = Assert.IsType<OptimizedHierarchyCellPathsViewModel>(page.DataContext);
            DataGrid grid = Assert.Single(page.GetVisualDescendants().OfType<DataGrid>());
            Assert.True(grid.HierarchicalRowsEnabled);
            Assert.Equal(expectedMode, grid.VisualLayoutMode);
            AssertCellOwnership(grid, expectedMode, expectedMode == DataGridVisualLayoutMode.Virtualized);

            viewModel.Model.ExpandAll();
            PumpLayout(window);
            Assert.True(viewModel.Model.Flattened.Count > 4);
            AssertCellOwnership(grid, expectedMode, expectedMode == DataGridVisualLayoutMode.Virtualized);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertCellOwnership(
        DataGrid grid,
        DataGridVisualLayoutMode mode,
        bool expectsRowlessSurface,
        string? context = null)
    {
        if (expectsRowlessSurface)
        {
            Assert.Empty(grid.GetVisualDescendants().OfType<DataGridRow>());
            Assert.Empty(grid.GetVisualDescendants().OfType<DataGridCell>());
            int surfaceCount = grid.GetVisualDescendants().Count(
                candidate => candidate.GetType().Name == "DataGridVirtualCellSurface");
            string columnSummary = string.Join(
                ", ",
                grid.Columns.Select(column =>
                    $"{column.GetType().Name}(accessor={DataGridColumnMetadata.GetValueAccessor(column)?.GetType().Name ?? "none"}, width={column.Width.UnitType})"));
            Assert.True(
                surfaceCount == 1,
                $"Expected one virtual surface for '{context}', found {surfaceCount}. " +
                $"Definitions: {grid.ColumnDefinitionsSource?.Count ?? -1}. Columns: {columnSummary}.");
            return;
        }

        DataGridRow row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .First(candidate => candidate.Index >= 0);
        DataGridCell cell = grid.GetVisualDescendants().OfType<DataGridCell>().First();

        if (mode != DataGridVisualLayoutMode.Nested)
        {
            Assert.Same(row.GetVisualParent(), cell.GetVisualParent());
        }
        else
        {
            Assert.NotSame(row.GetVisualParent(), cell.GetVisualParent());
            Assert.Contains(
                grid.GetVisualDescendants().OfType<DataGridRow>(),
                candidate => candidate.GetVisualDescendants().Any(descendant => ReferenceEquals(descendant, cell)));
        }
    }

    private static void AssertAllColumnsEditable(DataGrid grid, object item)
    {
        Assert.False(grid.IsReadOnly);
        Assert.NotEmpty(grid.Columns);
        Assert.All(
            grid.Columns,
            column =>
            {
                Assert.False(column.IsReadOnly);
                IDataGridColumnValueAccessor accessor = Assert.IsAssignableFrom<IDataGridColumnValueAccessor>(
                    DataGridColumnMetadata.GetValueAccessor(column));
                Assert.True(accessor.CanWrite, $"Column '{column.Header}' does not have a writable typed accessor.");
                object value = accessor.GetValue(item);
                accessor.SetValue(item, value);
            });
    }

    private static void Execute(ICommand command)
    {
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static Window CreateHostWindow(Control content)
    {
        var window = new Window
        {
            Width = 1_200,
            Height = 760,
            Content = content,
        };
        window.ApplySampleTheme();
        return window;
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
