// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Layout;

public class DataGridFlatVisualLayoutTests
{
    [AvaloniaTheory]
    [InlineData(DataGridTheme.SimpleFlat)]
    [InlineData(DataGridTheme.FluentFlat)]
    public void Flat_Theme_Promotes_Realized_Cells_To_Rows_Presenter(DataGridTheme theme)
    {
        (Window window, DataGrid grid) = CreateGrid(theme, useFlatTheme: true);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridRow[] rows = presenter.Children.OfType<DataGridRow>().ToArray();
            DataGridCell[] cells = presenter.GetVisualChildren().OfType<DataGridCell>().ToArray();

            Assert.Equal(DataGridVisualLayoutMode.Flat, grid.VisualLayoutMode);
            Assert.NotEmpty(rows);
            Assert.NotEmpty(cells);
            Assert.Equal(cells.Length, presenter.FlatRealizedCellCount);
            Assert.All(cells, cell => Assert.Same(presenter, cell.GetVisualParent()));
            Assert.All(cells, cell => Assert.Same(cell.OwningRow, cell.Parent));
            Assert.All(cells, cell => Assert.Same(cell.OwningRow.DataContext, cell.DataContext));
            Assert.All(cells, cell => Assert.True(cell.IsSet(StyledElement.DataContextProperty)));
            Assert.All(rows, row => Assert.Empty(row.GetVisualDescendants().OfType<DataGridCellsPresenter>()));
            foreach (IGrouping<DataGridRow, DataGridCell> rowCells in cells.GroupBy(cell => cell.OwningRow))
            {
                double top = rowCells.First().Bounds.Y;
                Assert.All(rowCells, cell => Assert.Equal(top, cell.Bounds.Y, precision: 3));
                Assert.All(rowCells, cell => Assert.Equal(32, cell.Bounds.Height, precision: 3));
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Line_Scroll_Retargets_Only_The_Entering_Row()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 50 * rowHeight);
            PumpLayout(grid);
            DataGridRow[] before = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            long retargetedBefore = grid.DisplayData.RetargetedRowCount;
            long fastScrollsBefore = grid.FlatRowRetargetScrollCount;

            presenter.Offset = new Vector(0, 51 * rowHeight);
            PumpLayout(grid);

            DataGridRow[] after = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            Assert.Equal(before.Length, after.Length);
            Assert.Equal(retargetedBefore + 1, grid.DisplayData.RetargetedRowCount);
            Assert.True(grid.FlatRowRetargetScrollCount > fastScrollsBefore);
            Assert.Equal(before.Skip(1), after.Take(after.Length - 1));
            Assert.Same(before[0], after[^1]);
            Assert.All(after, row =>
            {
                Item item = Assert.IsType<Item>(row.DataContext);
                Assert.Equal(row.Index, item.Id);
                foreach (DataGridCell cell in row.Cells)
                {
                    Assert.Same(item, cell.DataContext);
                }
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Discontinuous_Scroll_Retargets_The_Existing_Row_Window()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 50 * rowHeight);
            PumpLayout(grid);
            DataGridRow[] before = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            long retargetedBefore = grid.DisplayData.RetargetedRowCount;
            long fastScrollsBefore = grid.FlatRowRetargetScrollCount;

            presenter.Offset = new Vector(0, 250 * rowHeight);
            PumpLayout(grid);

            DataGridRow[] after = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            Assert.Equal(before.Length, after.Length);
            Assert.Equal(retargetedBefore + after.Length, grid.DisplayData.RetargetedRowCount);
            Assert.True(grid.FlatRowRetargetScrollCount > fastScrollsBefore);
            Assert.All(before, row => Assert.Contains(row, after));
            Assert.All(after, row =>
            {
                Item item = Assert.IsType<Item>(row.DataContext);
                Assert.Equal(row.Index, item.Id);
                foreach (DataGridCell cell in row.Cells)
                {
                    Assert.Same(item, cell.DataContext);
                }
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Fractional_Scroll_Keeps_The_Overscan_Row_Attached()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 50.625 * rowHeight);
            PumpLayout(grid);
            DataGridRow[] before = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            long retargetedBefore = grid.DisplayData.RetargetedRowCount;
            long fastScrollsBefore = grid.FlatRowRetargetScrollCount;

            presenter.Offset = new Vector(0, 50.375 * rowHeight);
            PumpLayout(grid);

            DataGridRow[] after = grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToArray();
            Assert.Equal(before, after);
            Assert.Equal(retargetedBefore, grid.DisplayData.RetargetedRowCount);
            Assert.True(grid.FlatRowRetargetScrollCount > fastScrollsBefore);
            Assert.Equal(rowHeight * 0.375, grid.NegVerticalOffset, precision: 3);
            Assert.All(after, row =>
            {
                foreach (DataGridCell cell in row.Cells)
                {
                    Assert.Same(row.DataContext, cell.DataContext);
                }
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Drawn_Cells_Keep_The_Recycle_Path()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500,
            useDrawnText: true);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 50 * rowHeight);
            PumpLayout(grid);
            long fastScrollsBefore = grid.FlatRowRetargetScrollCount;
            long retargetedBefore = grid.DisplayData.RetargetedRowCount;

            presenter.Offset = new Vector(0, 51 * rowHeight);
            PumpLayout(grid);

            Assert.Equal(fastScrollsBefore, grid.FlatRowRetargetScrollCount);
            Assert.Equal(retargetedBefore, grid.DisplayData.RetargetedRowCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_LoadingRow_Handler_Keeps_The_Recycle_Path()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.LoadingRow += static (_, _) => { };

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 50 * rowHeight);
            PumpLayout(grid);
            long fastScrollsBefore = grid.FlatRowRetargetScrollCount;
            long retargetedBefore = grid.DisplayData.RetargetedRowCount;

            presenter.Offset = new Vector(0, 51 * rowHeight);
            PumpLayout(grid);

            Assert.Equal(fastScrollsBefore, grid.FlatRowRetargetScrollCount);
            Assert.Equal(retargetedBefore, grid.DisplayData.RetargetedRowCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridTheme.SimpleFlat)]
    [InlineData(DataGridTheme.FluentFlat)]
    public void Virtualized_Layout_Uses_One_Surface_And_No_Display_Cell_Controls(DataGridTheme theme)
    {
        (Window window, DataGrid grid) = CreateGrid(theme, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridRow[] rows = presenter.Children.OfType<DataGridRow>().ToArray();

            Assert.Empty(rows);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.NotEmpty(presenter.LightweightVirtualRows);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Equal(3, presenter.VirtualColumnRenderPlanCount);
            Assert.Equal(0, presenter.FlatRealizedCellCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.Equal(
                100 * DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight),
                presenter.Extent.Height,
                precision: 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Materializes_Retained_Rows_For_Automation()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());

            Assert.IsType<DataGridAutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(grid));
            PumpLayout(grid);

            Assert.NotEmpty(presenter.Children.OfType<DataGridRow>());
            Assert.Empty(presenter.LightweightVirtualRows);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Rebuilds_Lightweight_Rows_After_Hierarchical_Toggles()
    {
        var rootItem = new HierarchyItem("Root");
        for (int index = 0; index < 40; index++)
        {
            rootItem.Children.Add(new HierarchyItem($"Child {index}"));
        }

        var model = new HierarchicalModel<HierarchyItem>(new HierarchicalOptions<HierarchyItem>
        {
            ChildrenSelector = item => item.Children,
            IsLeafSelector = item => item.Children.Count == 0,
        });
        model.SetRoot(rootItem);

        var grid = new DataGrid
        {
            Width = 420,
            Height = 220,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            RowHeight = 32,
            UseLogicalScrollable = true,
            VisualLayoutMode = DataGridVisualLayoutMode.Virtualized,
        };
        var column = new DataGridHierarchicalColumn
        {
            Header = "Name",
            Width = new DataGridLength(260),
            Binding = new Binding("Item.Name"),
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                node => ((HierarchyItem)node.Item).Name));
        grid.Columns.Add(column);

        var window = new Window
        {
            Width = 460,
            Height = 260,
            Content = grid,
        };
        window.SetThemeStyles(DataGridTheme.SimpleFlat);
        Assert.True(grid.TryFindResource("DataGridFlatTheme", out object? resource));
        grid.Theme = Assert.IsType<ControlTheme>(resource);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.Single(model.Flattened);

            model.ExpandAll();
            PumpLayout(grid);

            Assert.Equal(41, model.Count);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.NotEmpty(presenter.LightweightVirtualRows);
            Assert.Contains(presenter.LightweightVirtualRows, row => row.RowIndex > 0);

            HierarchicalNode<HierarchyItem> root = model.Root ??
                throw new InvalidOperationException("Expected a hierarchy root.");
            model.Toggle(root);
            PumpLayout(grid);

            Assert.Single(model.Flattened);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.Single(presenter.LightweightVirtualRows);
            Assert.Same(
                rootItem,
                Assert.IsAssignableFrom<IHierarchicalNodeItem>(presenter.LightweightVirtualRows[0].Item).Item);

            model.Toggle(root);
            PumpLayout(grid);

            Assert.Equal(41, model.Count);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.NotEmpty(presenter.LightweightVirtualRows);
            Assert.Contains(presenter.LightweightVirtualRows, row => row.RowIndex > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Rebuilds_Lightweight_Rows_After_ItemsSource_Replacement()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 100);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());

            Item[] replacement = Enumerable.Range(0, 250)
                .Select(index => new Item(index, $"Replacement {index}", index * 2.5))
                .ToArray();
            grid.ItemsSource = replacement;
            PumpLayout(grid);

            Assert.Equal(250, grid.SlotCount);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.NotEmpty(presenter.LightweightVirtualRows);
            Assert.All(
                presenter.LightweightVirtualRows,
                row => Assert.Contains(replacement, item => ReferenceEquals(item, row.Item)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Materializes_Retained_Rows_And_Only_The_Active_Editor_Cell()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            int slot = grid.SlotFromRowIndex(0);
            DataGridColumn column = grid.ColumnsInternal[1];
            Assert.True(grid.UpdateSelectionAndCurrency(
                column.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            PumpLayout(grid);

            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            Assert.NotEmpty(presenter.Children.OfType<DataGridRow>());
            Assert.Empty(presenter.LightweightVirtualRows);
            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(column.Index, editorCell.OwningColumn.Index);
            Assert.IsType<TextBox>(editorCell.Content);
            Assert.True(grid.CommitEdit());
            PumpLayout(grid);

            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.NotEmpty(presenter.LightweightVirtualRows);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Scrolls_Without_Creating_Display_Cells()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            grid.ScrollIntoView(grid.ItemsSource!.Cast<Item>().ElementAt(450), grid.ColumnsInternal[0]);
            PumpLayout(grid);

            Assert.Contains(presenter.LightweightVirtualRows, row => row.RowIndex >= 450);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Reuses_Values_For_Overlapping_Lightweight_Rows()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);
            Render(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            int visibleRows = presenter.LightweightVirtualRowCount;
            Assert.True(visibleRows > 1);
            Assert.Equal(visibleRows, presenter.VirtualRowValueCacheCount);
            Assert.True(presenter.VirtualCellValueCacheMissCount >= visibleRows * 3L);

            long hitsBefore = presenter.VirtualCellValueCacheHitCount;
            long missesBefore = presenter.VirtualCellValueCacheMissCount;
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, rowHeight);
            PumpLayout(grid);
            Render(grid);

            Assert.True(
                presenter.VirtualCellValueCacheHitCount - hitsBefore >= (visibleRows - 1) * 3L);
            Assert.True(presenter.VirtualCellValueCacheMissCount - missesBefore >= 3L);

            missesBefore = presenter.VirtualCellValueCacheMissCount;
            ((DataGridTextColumn)grid.ColumnsInternal[0]).FontSize = 18d;
            Render(grid);

            Assert.True(
                presenter.VirtualCellValueCacheMissCount - missesBefore >= visibleRows * 3L);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Scrolls_Without_Creating_Row_Containers()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            IReadOnlyList<DataGridVirtualRowInfo> rows = presenter.LightweightVirtualRows;
            Assert.NotEmpty(rows);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());

            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 400 * rowHeight);
            PumpLayout(grid);

            Assert.Same(rows, presenter.LightweightVirtualRows);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.Contains(presenter.LightweightVirtualRows, row => row.RowIndex >= 400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Rebinds_Lightweight_Rows_In_Place()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            IReadOnlyList<DataGridVirtualRowInfo> rows = presenter.LightweightVirtualRows;
            DataGridVirtualRowInfo firstRow = rows[0];
            Item initialSelectedItem = Assert.IsType<Item>(firstRow.Item);

            Assert.True(grid.UpdateSelectionAndCurrency(
                grid.ColumnsInternal[0].Index,
                firstRow.Slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Item targetItem = grid.ItemsSource!.Cast<Item>().ElementAt(400);
            grid.SelectedItems.Add(targetItem);
            Assert.Equal(2, grid.SelectedItems.Count);

            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 400 * rowHeight);
            PumpLayout(grid);

            Assert.Same(rows, presenter.LightweightVirtualRows);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());
            Assert.Contains(presenter.LightweightVirtualRows, row => row.RowIndex >= 400);
            Assert.Contains(presenter.LightweightVirtualRows, row => ReferenceEquals(row.Item, targetItem));
            Assert.Equal(2, grid.SelectedItems.Count);
            Assert.True(grid.SelectedItems.Contains(initialSelectedItem));
            Assert.True(grid.SelectedItems.Contains(targetItem));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Retarget_Arranges_When_Fractional_Row_Offset_Changes()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            long initialLightweightScrolls = grid.LightweightVirtualScrollCount;

            presenter.Offset = new Vector(0, 400.5 * rowHeight);
            PumpLayout(grid);

            Assert.True(grid.LightweightVirtualScrollCount > initialLightweightScrolls);
            Assert.Equal(rowHeight * 0.5, grid.NegVerticalOffset, precision: 3);
            Assert.Equal(-rowHeight * 0.5, presenter.LightweightVirtualRows[0].Top, precision: 3);

            long lightweightScrollsAfterFractionChanged = grid.LightweightVirtualScrollCount;
            presenter.Offset = new Vector(0, 450.5 * rowHeight);
            PumpLayout(grid);

            Assert.True(grid.LightweightVirtualScrollCount > lightweightScrollsAfterFractionChanged);
            Assert.Equal(rowHeight * 0.5, grid.NegVerticalOffset, precision: 3);
            Assert.Equal(-rowHeight * 0.5, presenter.LightweightVirtualRows[0].Top, precision: 3);

            int visibleRowCount = presenter.LightweightVirtualRows.Count;
            object[] overlappingItems = presenter.LightweightVirtualRows
                .Skip(1)
                .Select(static row => row.Item)
                .ToArray();
            long resolvedBeforeAdjacentScroll = presenter.LightweightVirtualItemResolveCount;
            long reusedBeforeAdjacentScroll = presenter.LightweightVirtualItemReuseCount;

            presenter.Offset = new Vector(0, 451.5 * rowHeight);
            PumpLayout(grid);

            Assert.Equal(
                resolvedBeforeAdjacentScroll + 1,
                presenter.LightweightVirtualItemResolveCount);
            Assert.Equal(
                reusedBeforeAdjacentScroll + visibleRowCount - 1,
                presenter.LightweightVirtualItemReuseCount);
            Assert.Equal(
                overlappingItems,
                presenter.LightweightVirtualRows
                    .Take(overlappingItems.Length)
                    .Select(static row => row.Item));

            long resolvedBeforeFractionOnlyScroll = presenter.LightweightVirtualItemResolveCount;
            long reusedBeforeFractionOnlyScroll = presenter.LightweightVirtualItemReuseCount;

            presenter.Offset = new Vector(0, 451.75 * rowHeight);
            PumpLayout(grid);

            Assert.Equal(
                resolvedBeforeFractionOnlyScroll,
                presenter.LightweightVirtualItemResolveCount);
            Assert.Equal(
                reusedBeforeFractionOnlyScroll + visibleRowCount,
                presenter.LightweightVirtualItemReuseCount);
            Assert.Equal(rowHeight * 0.75, grid.NegVerticalOffset, precision: 3);
            Assert.Equal(-rowHeight * 0.75, presenter.LightweightVirtualRows[0].Top, precision: 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Uniform_Scroll_Target_Falls_Back_For_Row_Details()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        grid.RowDetailsTemplate = new FuncDataTemplate<Item>(
            static (_, _) => new Border { Height = 48 });
        grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            long uniformScrollTargetsBeforeJump = grid.UniformScrollTargetCount;
            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);

            presenter.Offset = new Vector(0, 400 * rowHeight);
            PumpLayout(grid);

            Assert.Equal(uniformScrollTargetsBeforeJump, grid.UniformScrollTargetCount);
            Assert.Contains(presenter.Children.OfType<DataGridRow>(), row => row.Index >= 400);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Recycled_Rows_Restore_State_And_Raise_Lifecycle_Events()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            itemCount: 500);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        int loadingRows = 0;
        int unloadingRows = 0;
        grid.LoadingRow += (_, args) =>
        {
            loadingRows++;
            Assert.Same(args.Row.DataContext, grid.ItemsSource!.Cast<Item>().ElementAt(args.Row.Index));
            Assert.False(args.Row.IsSelected);
            Assert.True(args.Row.IsValid);
            Assert.Equal(DataGridValidationSeverity.None, args.Row.ValidationSeverity);
        };
        grid.UnloadingRow += (_, _) => unloadingRows++;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridRow recycledRow = presenter.Children.OfType<DataGridRow>().First();
            recycledRow.IsSelected = true;
            recycledRow.IsValid = false;
            recycledRow.ValidationSeverity = DataGridValidationSeverity.Error;
            int loadingRowsBeforeJump = loadingRows;
            long retargetedBeforeJump = grid.DisplayData.RetargetedRowCount;

            double rowHeight = DataGridRow.GetFlatDesiredHeight(grid, grid.RowHeight);
            presenter.Offset = new Vector(0, 400 * rowHeight);
            PumpLayout(grid);

            DataGridRow[] jumpedRows = presenter.Children.OfType<DataGridRow>().ToArray();
            Assert.True(loadingRows > loadingRowsBeforeJump);
            Assert.True(unloadingRows > 0);
            Assert.Equal(retargetedBeforeJump, grid.DisplayData.RetargetedRowCount);
            Assert.Contains(recycledRow, jumpedRows);
            Assert.All(jumpedRows, row =>
            {
                Assert.False(row.IsSelected);
                Assert.True(row.IsValid);
                Assert.Equal(DataGridValidationSeverity.None, row.ValidationSeverity);
                Assert.False(row.IsPlaceholder);
                Assert.Equal(0, row.Cells.Count);
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Falls_Back_To_Flat_Retained_For_Unsupported_Interactive_Columns()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.Columns.Add(new DataGridButtonColumn
        {
            Header = "Interactive",
            Width = new DataGridLength(90),
            Content = "Open",
        });
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(0, presenter.VirtualSurfaceCount);
            Assert.NotEmpty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_CheckBox_Column_And_Materializes_It_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var checkBoxColumn = new DataGridCheckBoxColumn
        {
            Header = "Active",
            Width = new DataGridLength(90),
            Binding = new Binding(nameof(Item.IsActive)),
            IsThreeState = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            checkBoxColumn,
            new DataGridColumnValueAccessor<Item, bool?>(item => item.IsActive));
        grid.Columns.Add(checkBoxColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.IsActive = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                checkBoxColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            var keyArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Route = InputElement.KeyDownEvent.RoutingStrategies,
                Key = Key.Space,
                Source = grid,
                KeyDeviceType = KeyDeviceType.Keyboard,
            };
            grid.RaiseEvent(keyArgs);
            PumpLayout(grid);

            Assert.True(keyArgs.Handled);
            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(checkBoxColumn, editorCell.OwningColumn);
            Assert.IsType<CheckBox>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), checkBoxColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_Date_Column_And_Materializes_It_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var dateColumn = new DataGridDatePickerColumn
        {
            Header = "Date",
            Width = new DataGridLength(120),
            Binding = new Binding(nameof(Item.Date)),
            SelectedDateFormat = CalendarDatePickerFormat.Custom,
            CustomDateFormatString = "yyyy-MM-dd",
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };
        DataGridColumnMetadata.SetValueAccessor(
            dateColumn,
            new DataGridColumnValueAccessor<Item, DateTime?>(item => item.Date));
        grid.Columns.Add(dateColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Date = new DateTime(2030, 12, 31);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                dateColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(dateColumn, editorCell.OwningColumn);
            Assert.IsType<CalendarDatePicker>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), dateColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_Time_Column_And_Materializes_It_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var timeColumn = new DataGridTimePickerColumn
        {
            Header = "Time",
            Width = new DataGridLength(120),
            Binding = new Binding(nameof(Item.Time)),
            ClockIdentifier = "24HourClock",
            UseSeconds = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            timeColumn,
            new DataGridColumnValueAccessor<Item, TimeSpan?>(item => item.Time));
        grid.Columns.Add(timeColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Time = new TimeSpan(23, 59, 58);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                timeColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(timeColumn, editorCell.OwningColumn);
            Assert.IsType<TimePicker>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), timeColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_Masked_Text_Column_And_Materializes_It_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var maskedColumn = new DataGridMaskedTextColumn
        {
            Header = "Phone",
            Width = new DataGridLength(180),
            Binding = new Binding(nameof(Item.Phone)),
            Mask = "(000) 000-0000",
        };
        DataGridColumnMetadata.SetValueAccessor(
            maskedColumn,
            new DataGridColumnValueAccessor<Item, string>(item => item.Phone));
        grid.Columns.Add(maskedColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Phone = "(555) 999-0000";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                maskedColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(maskedColumn, editorCell.OwningColumn);
            Assert.IsType<MaskedTextBox>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), maskedColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_AutoComplete_Column_And_Materializes_It_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var autoCompleteColumn = new DataGridAutoCompleteColumn
        {
            Header = "Category",
            Width = new DataGridLength(180),
            Binding = new Binding(nameof(Item.Category)),
            ItemsSource = new[] { "Hardware", "Software", "Services" },
        };
        DataGridColumnMetadata.SetValueAccessor(
            autoCompleteColumn,
            new DataGridColumnValueAccessor<Item, string>(item => item.Category));
        grid.Columns.Add(autoCompleteColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Category = "Services";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                autoCompleteColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(autoCompleteColumn, editorCell.OwningColumn);
            Assert.IsType<AutoCompleteBox>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), autoCompleteColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_Slider_Text_And_Materializes_Slider_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var sliderColumn = new DataGridSliderColumn
        {
            Header = "Rating",
            Width = new DataGridLength(180),
            Binding = new Binding(nameof(Item.Value)),
            ShowValueText = true,
            ValueTextFormat = "{0:0.0}",
        };
        DataGridColumnMetadata.SetValueAccessor(
            sliderColumn,
            new DataGridColumnValueAccessor<Item, double>(item => item.Value));
        grid.Columns.Add(sliderColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Value = 42.5;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                sliderColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(sliderColumn, editorCell.OwningColumn);
            Assert.IsType<Slider>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), sliderColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Draws_Typed_ComboBox_Text_And_Materializes_ComboBox_Only_For_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var comboBoxColumn = new DataGridComboBoxColumn
        {
            Header = "Category",
            Width = new DataGridLength(180),
            IsEditable = true,
            ItemsSource = new[] { "Category 00", "Category 01", "Category 02" },
            TextBinding = new Binding(nameof(Item.Category)),
        };
        DataGridColumnMetadata.SetValueAccessor(
            comboBoxColumn,
            new DataGridColumnValueAccessor<Item, string>(item => item.Category));
        grid.Columns.Add(comboBoxColumn);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));

            int valueChangeCount = presenter.VirtualValueChangeCount;
            Item firstItem = grid.ItemsSource!.Cast<Item>().First();
            firstItem.Category = "Category 02";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(valueChangeCount + 1, presenter.VirtualValueChangeCount);

            int slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                comboBoxColumn.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editorCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Same(comboBoxColumn, editorCell.OwningColumn);
            Assert.IsType<ComboBox>(editorCell.Content);

            Assert.True(grid.CommitEdit());
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualChildren().OfType<DataGridCell>());
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ScrollIntoView(grid.ItemsSource.Cast<Item>().ElementAt(90), comboBoxColumn);
            PumpLayout(grid);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
            Assert.All(presenter.Children.OfType<DataGridRow>(), row => Assert.Equal(0, row.Cells.Count));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Preserves_Cell_Lifecycle_Events_Through_Retained_Fallback()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        int prepared = 0;
        grid.CellPrepared += (_, _) => prepared++;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.True(prepared > 0);
            Assert.NotEmpty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Pointer_Hit_Selects_The_Cell()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.IsReadOnly = true;
        var selection = new SelectionModel<Item> { SingleSelect = false };
        grid.Selection = selection;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridVirtualCellSurface surface = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridVirtualCellSurface>());
            DataGridVirtualRowInfo row = presenter.LightweightVirtualRows[0];
            Assert.True(presenter.TryGetVirtualCellBounds(row.Slot, grid.ColumnsInternal[0], out Rect bounds));
            Assert.Equal(DataGridSelectionUnit.Cell, grid.SelectionUnit);
            Assert.True(grid.UsesVirtualCellSurface);
            Assert.True(presenter.IsVirtualCellPoint(bounds.Center));

            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var properties = new PointerPointProperties(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed);
            Point rootPoint = surface.TranslatePoint(bounds.Center, window)!.Value;
            var args = new PointerPressedEventArgs(
                surface,
                pointer,
                window,
                rootPoint,
                0,
                properties,
                KeyModifiers.None);
            args.RoutedEvent = InputElement.PointerPressedEvent;
            args.Route = InputElement.PointerPressedEvent.RoutingStrategies;
            Assert.True(args.GetCurrentPoint(surface).Properties.IsLeftButtonPressed);
            Assert.Equal(bounds.Center, args.GetPosition(surface));
            surface.RaiseEvent(args);
            Assert.True(args.Handled);
            Assert.True(grid.IsSelectionDragActive);
            Assert.Single(grid.SelectedCells);
            PumpLayout(grid);

            Assert.True(grid.CurrentCell.IsValid);
            Assert.Same(row.Item, grid.CurrentCell.Item);
            Assert.Same(grid.ColumnsInternal[0], grid.CurrentCell.Column);
            DataGridCellInfo selectedCell = Assert.Single(grid.SelectedCells);
            Assert.Same(row.Item, selectedCell.Item);
            Assert.Same(grid.ColumnsInternal[0], selectedCell.Column);
            Assert.Equal(row.RowIndex, selection.SelectedIndex);
            Assert.True(presenter.IsVirtualCellSelected(row, grid.ColumnsInternal[0].Index, hasSelectedCells: true));

            Border selectionOutline = grid.GetVisualDescendants()
                .OfType<Border>()
                .First(border => border.Name == "PART_SelectionOutline");
            Assert.True(selectionOutline.IsVisible);
            Assert.Equal(bounds.Width, selectionOutline.Width, precision: 3);
            Assert.Equal(bounds.Height, selectionOutline.Height, precision: 3);
            Assert.True(grid.IsSelectionDragActive);

            DataGridVirtualRowInfo targetRow = presenter.LightweightVirtualRows[2];
            Assert.True(presenter.TryGetVirtualCellBounds(
                targetRow.Slot,
                grid.ColumnsInternal[1],
                out Rect targetBounds));
            Point targetRootPoint = surface.TranslatePoint(targetBounds.Center, window)!.Value;
            Point targetGridPoint = surface.TranslatePoint(targetBounds.Center, grid)!.Value;
            Point targetPresenterPoint = grid.TranslatePoint(targetGridPoint, presenter)!.Value;
            Assert.True(presenter.TryGetLightweightVirtualRowSlot(targetPresenterPoint.Y, out int targetSlot));
            Assert.Equal(targetRow.Slot, targetSlot);
            var moveProperties = new PointerPointProperties(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.Other);
            var moveArgs = new PointerEventArgs(
                InputElement.PointerMovedEvent,
                grid,
                pointer,
                window,
                targetRootPoint,
                0,
                moveProperties,
                KeyModifiers.None);
            Assert.Equal(targetGridPoint, moveArgs.GetCurrentPoint(grid).Position);
            grid.RaiseEvent(moveArgs);
            PumpLayout(grid);

            Assert.True(moveArgs.Handled);
            Assert.Equal(
                new[] { (0, 0), (0, 1), (1, 0), (1, 1), (2, 0), (2, 1) },
                grid.SelectedCells
                    .Select(cell => (cell.RowIndex, cell.ColumnIndex))
                    .OrderBy(cell => cell.RowIndex)
                    .ThenBy(cell => cell.ColumnIndex));
            Assert.Equal(new[] { 0, 1, 2 }, selection.SelectedIndexes.OrderBy(index => index));
            Assert.Equal(targetBounds.Right - bounds.Left, selectionOutline.Width, precision: 3);
            Assert.Equal(targetBounds.Bottom - bounds.Top, selectionOutline.Height, precision: 3);

            var releaseProperties = new PointerPointProperties(
                RawInputModifiers.None,
                PointerUpdateKind.LeftButtonReleased);
            var releaseArgs = new PointerReleasedEventArgs(
                grid,
                pointer,
                window,
                targetRootPoint,
                0,
                releaseProperties,
                KeyModifiers.None,
                MouseButton.Left);
            grid.RaiseEvent(releaseArgs);
            Assert.False(grid.IsSelectionDragActive);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Uses_Idiomatic_SelectionModel_For_FullRow_Selection()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var selection = new SelectionModel<Item> { SingleSelect = false };
        selection.Select(1);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Selection = selection;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(grid.DisplayData.HasVirtualScrollingElements);
            Assert.Empty(presenter.Children.OfType<DataGridRow>());

            selection.Select(3);
            PumpLayout(grid);

            Assert.Equal(new[] { 1, 3 }, selection.SelectedIndexes.OrderBy(index => index));
            Assert.Equal(new[] { 1, 3 }, grid.SelectedItems.Cast<Item>().Select(item => item.Id).OrderBy(id => id));

            foreach (DataGridVirtualRowInfo row in presenter.LightweightVirtualRows)
            {
                bool expected = row.RowIndex is 1 or 3;
                Assert.Equal(
                    expected,
                    presenter.IsVirtualCellSelected(row, grid.ColumnsInternal[0].Index, hasSelectedCells: false));
                Assert.Equal(
                    expected,
                    presenter.IsVirtualCellSelected(row, grid.ColumnsInternal[1].Index, hasSelectedCells: false));
            }

            selection.Clear();
            PumpLayout(grid);

            Assert.Empty(grid.SelectedItems);
            Assert.All(
                presenter.LightweightVirtualRows,
                row => Assert.False(
                    presenter.IsVirtualCellSelected(row, grid.ColumnsInternal[0].Index, hasSelectedCells: false)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Keyboard_Extends_Idiomatic_Row_Selection()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        var selection = new SelectionModel<Item> { SingleSelect = false };
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.Selection = selection;

        try
        {
            window.Show();
            PumpLayout(grid);

            int firstSlot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(
                grid.ColumnsInternal[0].Index,
                firstSlot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));

            var keyArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Route = InputElement.KeyDownEvent.RoutingStrategies,
                Key = Key.Down,
                KeyModifiers = KeyModifiers.Shift,
                Source = grid,
                KeyDeviceType = KeyDeviceType.Keyboard,
            };
            grid.RaiseEvent(keyArgs);
            PumpLayout(grid);

            Assert.True(keyArgs.Handled);
            Assert.Equal(new[] { 0, 1 }, selection.SelectedIndexes.OrderBy(index => index));
            Assert.Equal(1, grid.CurrentCell.Item is Item item ? item.Id : -1);
            Assert.Empty(GetRowsPresenter(grid).Children.OfType<DataGridRow>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Uses_Precomputed_Frozen_Column_Clips_For_Bounds_And_Hit_Testing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.Width = 320;
        grid.FrozenColumnCount = 1;
        grid.FrozenColumnCountRight = 1;
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);
            grid.UpdateHorizontalOffset(50);
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridVirtualCellSurface surface = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridVirtualCellSurface>());
            DataGridVirtualRowInfo row = presenter.LightweightVirtualRows[0];
            DataGridColumn leftColumn = grid.ColumnsInternal[0];
            DataGridColumn scrollingColumn = grid.ColumnsInternal[1];
            DataGridColumn rightColumn = grid.ColumnsInternal[2];

            Assert.True(presenter.TryGetVirtualCellBounds(row.Slot, leftColumn, out Rect leftBounds));
            Assert.True(presenter.TryGetVirtualCellBounds(row.Slot, scrollingColumn, out Rect scrollingBounds));
            Assert.True(presenter.TryGetVirtualCellBounds(row.Slot, rightColumn, out Rect rightBounds));

            double frozenLeftWidth = grid.GetVisibleFrozenColumnsWidthLeft();
            double rightFrozenStart = grid.CellsWidth - grid.GetVisibleFrozenColumnsWidthRight();
            Assert.Equal(0, leftBounds.Left, precision: 3);
            Assert.Equal(frozenLeftWidth, leftBounds.Right, precision: 3);
            Assert.Equal(frozenLeftWidth, scrollingBounds.Left, precision: 3);
            Assert.Equal(rightFrozenStart, scrollingBounds.Right, precision: 3);
            Assert.Equal(rightFrozenStart, rightBounds.Left, precision: 3);
            Assert.Equal(grid.CellsWidth, rightBounds.Right, precision: 3);
            Assert.True(((Avalonia.Rendering.ICustomHitTest)surface).HitTest(scrollingBounds.Center));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Tracks_Visible_Item_Value_Changes()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(presenter.VirtualTrackedValueNotifierCount > 0);
            int changeCount = presenter.VirtualValueChangeCount;

            Item item = grid.ItemsSource!.Cast<Item>().First();
            long missesBefore = presenter.VirtualCellValueCacheMissCount;
            item.Name = "Updated";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(changeCount + 1, presenter.VirtualValueChangeCount);
            PumpLayout(grid);
            Render(grid);
            Assert.True(presenter.VirtualCellValueCacheMissCount > missesBefore);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Skips_Value_Tracking_When_All_Columns_Opt_Out()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        foreach (DataGridTextColumn column in grid.ColumnsInternal.OfType<DataGridTextColumn>())
        {
            column.TrackDirectTextValueChanges = false;
        }
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.Equal(0, presenter.VirtualTrackedValueNotifierCount);
            int changeCount = presenter.VirtualValueChangeCount;

            Item item = grid.ItemsSource!.Cast<Item>().First();
            item.Name = "Updated";
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(changeCount, presenter.VirtualValueChangeCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Surface_Updates_Value_Tracking_When_Column_Policy_Changes()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.True(presenter.VirtualTrackedValueNotifierCount > 0);

            foreach (DataGridTextColumn column in grid.ColumnsInternal.OfType<DataGridTextColumn>())
            {
                column.TrackDirectTextValueChanges = false;
            }
            PumpLayout(grid);
            Assert.Equal(0, presenter.VirtualTrackedValueNotifierCount);

            DataGridTextColumn firstColumn = grid.ColumnsInternal.OfType<DataGridTextColumn>().First();
            firstColumn.TrackDirectTextValueChanges = true;
            PumpLayout(grid);
            Assert.True(presenter.VirtualTrackedValueNotifierCount > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Falls_Back_When_A_Typed_Accessor_Is_Not_Available()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        DataGridColumnMetadata.ClearValueAccessor(grid.ColumnsInternal[0]);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.NotEmpty(GetRowsPresenter(grid).GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Search_Only_Uses_Retained_Fallback_When_Highlights_Are_Visible()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;
        var searchModel = new SearchModel
        {
            HighlightMode = SearchHighlightMode.None,
            HighlightCurrent = false,
        };
        grid.SearchModel = searchModel;
        searchModel.HighlightMode = SearchHighlightMode.None;
        searchModel.HighlightCurrent = false;

        try
        {
            window.Show();
            PumpLayout(grid);

            searchModel.SetOrUpdate(new SearchDescriptor("Item 1", scope: SearchScope.VisibleColumns));
            PumpLayout(grid);

            Assert.NotEmpty(searchModel.Results);
            Assert.Equal(SearchHighlightMode.None, searchModel.HighlightMode);
            Assert.True(grid.UsesVirtualCellSurface);
            Assert.Empty(GetRowsPresenter(grid).GetVisualDescendants().OfType<DataGridCell>());

            searchModel.HighlightMode = SearchHighlightMode.TextAndCell;
            PumpLayout(grid);

            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.NotEmpty(GetRowsPresenter(grid).GetVisualDescendants().OfType<DataGridCell>());

            searchModel.HighlightMode = SearchHighlightMode.None;
            PumpLayout(grid);

            Assert.True(grid.UsesVirtualCellSurface);
            Assert.Empty(GetRowsPresenter(grid).GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Falls_Back_For_Auto_Cell_Sizing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.ColumnsInternal[0].Width = DataGridLength.SizeToCells;
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);

            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.NotEmpty(GetRowsPresenter(grid).GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Transitions_Between_Surface_And_Retained_Fallback()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);
            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.ColumnsInternal[0].Width = DataGridLength.SizeToCells;
            PumpLayout(grid);
            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(0, presenter.VirtualColumnRenderPlanCount);
            Assert.Equal(0, presenter.VirtualRowValueCacheCount);
            Assert.NotEmpty(presenter.GetVisualDescendants().OfType<DataGridCell>());

            grid.ColumnsInternal[0].Width = new DataGridLength(80);
            PumpLayout(grid);
            Assert.False(grid.UsesVirtualCellSurfaceFallback);
            Assert.Equal(1, presenter.VirtualSurfaceCount);
            Assert.Empty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Virtualized_Layout_Uses_Retained_Fallback_For_Custom_Grid_Cell_Theme()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.VisualLayoutMode = DataGridVisualLayoutMode.Virtualized;

        try
        {
            window.Show();
            PumpLayout(grid);
            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.Equal(1, presenter.VirtualSurfaceCount);

            grid.CellTheme = new ControlTheme(typeof(DataGridCell));
            PumpLayout(grid);

            Assert.True(grid.UsesVirtualCellSurfaceFallback);
            Assert.NotEmpty(presenter.GetVisualDescendants().OfType<DataGridCell>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nested_Theme_Preserves_Per_Row_Cells_Presenter()
    {
        // Loading Flat.xaml is inert until the keyed DataGridFlatTheme is selected.
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: false);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridRow[] rows = presenter.Children.OfType<DataGridRow>().ToArray();
            DataGridCell[] directCells = presenter.GetVisualChildren().OfType<DataGridCell>().ToArray();

            Assert.Equal(DataGridVisualLayoutMode.Nested, grid.VisualLayoutMode);
            Assert.NotEmpty(rows);
            Assert.Empty(directCells);
            Assert.Equal(0, presenter.FlatRealizedCellCount);
            Assert.All(rows, row => Assert.NotEmpty(row.GetVisualDescendants().OfType<DataGridCellsPresenter>()));
            Assert.All(rows, row =>
            {
                for (int columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    Assert.IsType<DataGridCellsPresenter>(row.Cells[columnIndex].GetVisualParent());
                }
            });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Recycles_Direct_Cells_When_Scrolling()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true, itemCount: 500);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridCell[] initialCells = presenter.GetVisualChildren().OfType<DataGridCell>().ToArray();
            Assert.NotEmpty(initialCells);

            grid.ScrollIntoView(grid.ItemsSource!.Cast<Item>().ElementAt(450), grid.ColumnsInternal[0]);
            PumpLayout(grid);

            DataGridCell[] scrolledCells = presenter.GetVisualChildren().OfType<DataGridCell>().ToArray();
            Assert.NotEmpty(scrolledCells);
            Assert.Equal(scrolledCells.Length, presenter.FlatRealizedCellCount);
            Assert.All(scrolledCells, cell => Assert.Same(presenter, cell.GetVisualParent()));
            Assert.All(scrolledCells, cell => Assert.Same(cell.OwningRow, cell.Parent));
            Assert.All(scrolledCells, cell => Assert.Same(cell.OwningRow.DataContext, cell.DataContext));
            Assert.All(scrolledCells, cell => Assert.True(cell.IsSet(StyledElement.DataContextProperty)));
            Assert.Contains(presenter.Children.OfType<DataGridRow>(), row => row.Index >= 450);
            Assert.True(scrolledCells.Length <= initialCells.Length + 6,
                $"Expected recycling to keep the direct-cell set bounded. Initial={initialCells.Length}, Scrolled={scrolledCells.Length}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Preserves_Cell_Editing()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            int slot = grid.SlotFromRowIndex(0);
            DataGridColumn column = grid.ColumnsInternal[1];
            Assert.True(grid.UpdateSelectionAndCurrency(
                column.Index,
                slot,
                DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false));
            PumpLayout(grid);

            Assert.True(grid.BeginEdit());
            PumpLayout(grid);

            DataGridCell editedCell = Assert.Single(
                presenter.GetVisualChildren().OfType<DataGridCell>(),
                cell => cell.OwningRow.Index == 0 && cell.OwningColumn.Index == column.Index);
            Assert.Same(presenter, editedCell.GetVisualParent());
            Assert.IsType<TextBox>(editedCell.Content);
            Assert.True(grid.CommitEdit());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Uses_Equivalent_Row_And_Cell_Height_With_Horizontal_Grid_Lines()
    {
        (Window window, DataGrid grid) = CreateGrid(DataGridTheme.SimpleFlat, useFlatTheme: true);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            Assert.All(
                presenter.Children.OfType<DataGridRow>(),
                row => Assert.Equal(33, row.Bounds.Height, precision: 3));
            Assert.All(
                presenter.GetVisualChildren().OfType<DataGridCell>(),
                cell => Assert.Equal(32, cell.Bounds.Height, precision: 3));

            grid.GridLinesVisibility = DataGridGridLinesVisibility.None;
            PumpLayout(grid);

            Assert.All(
                presenter.Children.OfType<DataGridRow>(),
                row => Assert.Equal(32, row.Bounds.Height, precision: 3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flat_Layout_Shares_Drawn_Text_Cache_And_Restores_Cell_Settings_On_Reset()
    {
        (Window window, DataGrid grid) = CreateGrid(
            DataGridTheme.SimpleFlat,
            useFlatTheme: true,
            useDrawnText: true);

        try
        {
            window.Show();
            PumpLayout(grid);

            DataGridRowsPresenter presenter = GetRowsPresenter(grid);
            DataGridCustomDrawingCell[] cells = presenter.GetVisualChildren()
                .OfType<DataGridCustomDrawingCell>()
                .ToArray();
            Assert.NotEmpty(cells);
            DataGridCustomDrawingTextLayoutCache cache = Assert.IsType<DataGridCustomDrawingTextLayoutCache>(
                cells[0].SharedTextLayoutCache);
            Assert.All(cells, cell =>
            {
                Assert.Equal(DataGridCustomDrawingTextLayoutCacheMode.Shared, cell.TextLayoutCacheMode);
                Assert.Same(cache, cell.SharedTextLayoutCache);
            });

            presenter.ResetFlatVisualLayout();

            Assert.All(cells, cell =>
            {
                Assert.Equal(DataGridCustomDrawingTextLayoutCacheMode.Shared, cell.TextLayoutCacheMode);
                Assert.NotNull(cell.SharedTextLayoutCache);
                Assert.NotSame(cache, cell.SharedTextLayoutCache);
            });
            Assert.All(
                cells.GroupBy(cell => cell.OwningColumn),
                columnCells => Assert.Single(columnCells.Select(cell => cell.SharedTextLayoutCache).Distinct()));
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid) CreateGrid(
        DataGridTheme theme,
        bool useFlatTheme,
        int itemCount = 100,
        bool useDrawnText = false)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(index => new Item(index, $"Item {index}", index * 1.5))
            .ToList();
        var grid = new DataGrid
        {
            Width = 420,
            Height = 220,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = items,
            RowHeight = 32,
            UseLogicalScrollable = true,
        };
        var idColumn = new DataGridTextColumn
        {
            Header = "Id",
            Width = new DataGridLength(80),
            Binding = new Binding(nameof(Item.Id)),
            DisplayMode = useDrawnText ? DataGridColumnDisplayMode.Drawn : DataGridColumnDisplayMode.Retained,
        };
        DataGridColumnMetadata.SetValueAccessor(
            idColumn,
            new DataGridColumnValueAccessor<Item, int>(item => item.Id));
        grid.Columns.Add(idColumn);
        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Width = new DataGridLength(180),
            Binding = new Binding(nameof(Item.Name)),
            DisplayMode = useDrawnText ? DataGridColumnDisplayMode.Drawn : DataGridColumnDisplayMode.Retained,
        };
        DataGridColumnMetadata.SetValueAccessor(
            nameColumn,
            new DataGridColumnValueAccessor<Item, string>(item => item.Name));
        grid.Columns.Add(nameColumn);
        var valueColumn = new DataGridTextColumn
        {
            Header = "Value",
            Width = new DataGridLength(120),
            Binding = new Binding(nameof(Item.Value)),
            DisplayMode = useDrawnText ? DataGridColumnDisplayMode.Drawn : DataGridColumnDisplayMode.Retained,
        };
        DataGridColumnMetadata.SetValueAccessor(
            valueColumn,
            new DataGridColumnValueAccessor<Item, double>(item => item.Value));
        grid.Columns.Add(valueColumn);

        var window = new Window
        {
            Width = 460,
            Height = 260,
            Content = grid,
        };
        window.SetThemeStyles(theme);
        if (useFlatTheme)
        {
            Assert.True(grid.TryFindResource("DataGridFlatTheme", out object? resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
        }
        else
        {
            Assert.True(grid.TryFindResource(typeof(DataGrid), out object? resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
        }

        return (window, grid);
    }

    private static DataGridRowsPresenter GetRowsPresenter(DataGrid grid)
    {
        return Assert.Single(grid.GetVisualDescendants().OfType<DataGridRowsPresenter>());
    }

    private static void PumpLayout(Control control)
    {
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
    }

    private static void Render(Control control)
    {
        int width = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width));
        int height = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(control);
    }

    private sealed class Item : INotifyPropertyChanged
    {
        private string _name;
        private bool? _isActive;
        private DateTime? _date;
        private TimeSpan? _time;
        private string _phone;
        private string _category;
        private double _value;

        public Item(int id, string name, double value)
        {
            Id = id;
            _name = name;
            _value = value;
            _isActive = id % 2 == 0;
            _date = new DateTime(2024, 1, 1).AddDays(id);
            _time = TimeSpan.FromSeconds(id % 86_400);
            _phone = $"(555) {id % 1_000:D3}-{id % 10_000:D4}";
            _category = $"Category {id % 32:D2}";
        }

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public double Value
        {
            get => _value;
            set
            {
                if (_value == value)
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public bool? IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                {
                    return;
                }

                _isActive = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
            }
        }

        public DateTime? Date
        {
            get => _date;
            set
            {
                if (_date == value)
                {
                    return;
                }

                _date = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));
            }
        }

        public TimeSpan? Time
        {
            get => _time;
            set
            {
                if (_time == value)
                {
                    return;
                }

                _time = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Time)));
            }
        }

        public string Phone
        {
            get => _phone;
            set
            {
                if (_phone == value)
                {
                    return;
                }

                _phone = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Phone)));
            }
        }

        public string Category
        {
            get => _category;
            set
            {
                if (_category == value)
                {
                    return;
                }

                _category = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class HierarchyItem
    {
        public HierarchyItem(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public List<HierarchyItem> Children { get; } = new();
    }
}
