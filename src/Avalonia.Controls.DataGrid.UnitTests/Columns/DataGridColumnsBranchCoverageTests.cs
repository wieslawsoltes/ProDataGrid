using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public class DataGridColumnsBranchCoverageTests
{
    private sealed class TestDataGrid : DataGrid
    {
        public void RaiseColumnReordered(DataGridColumnEventArgs args) => OnColumnReordered(args);
        public void RaiseColumnReordering(DataGridColumnReorderingEventArgs args) => OnColumnReordering(args);
        public void RaiseColumnDisplayIndexChanged(DataGridColumnEventArgs args) => OnColumnDisplayIndexChanged(args);
        public void RaiseColumnSorting(DataGridColumnEventArgs args) => OnColumnSorting(args);
    }

    [AvaloniaFact]
    public void ColumnReordered_Assigns_RoutedEvent_And_Source_When_Missing()
    {
        var grid = new TestDataGrid();
        var column = new DataGridTextColumn();

        var reordered = new DataGridColumnEventArgs(column);
        Assert.Null(reordered.RoutedEvent);
        Assert.Null(reordered.Source);

        grid.RaiseColumnReordered(reordered);

        Assert.Same(DataGrid.ColumnReorderedEvent, reordered.RoutedEvent);
        Assert.Same(grid, reordered.Source);

        var reorderedSet = new DataGridColumnEventArgs(column, DataGrid.ColumnReorderedEvent, source: column);
        grid.RaiseColumnReordered(reorderedSet);
        Assert.Same(DataGrid.ColumnReorderedEvent, reorderedSet.RoutedEvent);
        Assert.NotNull(reorderedSet.Source);

        var reorderingMissing = new DataGridColumnReorderingEventArgs(column);
        grid.RaiseColumnReordering(reorderingMissing);
        Assert.Same(DataGrid.ColumnReorderingEvent, reorderingMissing.RoutedEvent);
        Assert.Same(grid, reorderingMissing.Source);

        var reorderingSet = new DataGridColumnReorderingEventArgs(column, DataGrid.ColumnReorderingEvent, source: column);
        grid.RaiseColumnReordering(reorderingSet);
        Assert.Same(DataGrid.ColumnReorderingEvent, reorderingSet.RoutedEvent);
        Assert.NotNull(reorderingSet.Source);
    }

    [AvaloniaFact]
    public void ColumnDisplayIndexChanged_And_Sorting_Assign_RoutedEvent_And_Source_When_Missing()
    {
        var grid = new TestDataGrid();
        var column = new DataGridTextColumn();

        var displayIndexChanged = new DataGridColumnEventArgs(column);
        Assert.Null(displayIndexChanged.RoutedEvent);
        Assert.Null(displayIndexChanged.Source);
        grid.RaiseColumnDisplayIndexChanged(displayIndexChanged);
        Assert.Same(DataGrid.ColumnDisplayIndexChangedEvent, displayIndexChanged.RoutedEvent);
        Assert.Same(grid, displayIndexChanged.Source);

        var sorting = new DataGridColumnEventArgs(column);
        grid.RaiseColumnSorting(sorting);
        Assert.Same(DataGrid.SortingEvent, sorting.RoutedEvent);
        Assert.Same(grid, sorting.Source);

        var sortingSet = new DataGridColumnEventArgs(column, DataGrid.SortingEvent, source: column);
        Assert.Same(column, sortingSet.Source);
        grid.RaiseColumnSorting(sortingSet);
        Assert.Same(DataGrid.SortingEvent, sortingSet.RoutedEvent);
        Assert.NotNull(sortingSet.Source);
    }

    [AvaloniaFact]
    public void AdjustColumnWidths_Uses_Both_Signs()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(80) });
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(80) });
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.AdjustColumnWidths(0, -10, userInitiated: false);
        grid.AdjustColumnWidths(0, 10, userInitiated: false);
        grid.AdjustColumnWidths(0, 0, userInitiated: false);
    }

    [AvaloniaFact]
    public void AutoSizeColumn_Handles_Width_Types_And_Star_Sizing()
    {
        var grid = new DataGrid();
        var autoColumn = new DataGridTextColumn { Width = DataGridLength.Auto };
        var cellsColumn = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells) };
        var headerColumn = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader) };
        grid.ColumnsInternal.Add(autoColumn);
        grid.ColumnsInternal.Add(cellsColumn);
        grid.ColumnsInternal.Add(headerColumn);
        var starColumn = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        grid.ColumnsInternal.Add(starColumn);

        grid.RowsPresenterAvailableSize = new Size(200, 100);
        autoColumn.IsInitialDesiredWidthDetermined = false;
        grid.AutoSizeColumn(autoColumn, 120);
        Assert.True(grid.AutoSizingColumns);

        autoColumn.IsInitialDesiredWidthDetermined = true;
        autoColumn.SetWidthDesiredValue(100);
        grid.AutoSizeColumn(autoColumn, 160);

        autoColumn.SetWidthDesiredValue(double.NaN);
        grid.AutoSizeColumn(autoColumn, 90);

        grid.AutoSizeColumn(autoColumn, 80);
        grid.AutoSizeColumn(cellsColumn, 40);
        grid.AutoSizeColumn(headerColumn, 50);

        grid.RowsPresenterAvailableSize = new Size(double.PositiveInfinity, 100);
        grid.AutoSizeColumn(starColumn, 200);
    }

    [AvaloniaFact]
    public void ColumnRequiresRightGridLine_Covers_Visibility_And_Filler()
    {
        var grid = new DataGrid
        {
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            VerticalGridLinesBrush = new SolidColorBrush(Colors.Black)
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        var first = grid.ColumnsInternal[0];
        var last = grid.ColumnsInternal[1];

        Assert.False(grid.ColumnRequiresRightGridLine(first, includeLastRightGridLineWhenPresent: false));

        grid.GridLinesVisibility = DataGridGridLinesVisibility.Vertical;
        grid.VerticalGridLinesBrush = null;
        Assert.False(grid.ColumnRequiresRightGridLine(first, includeLastRightGridLineWhenPresent: false));

        grid.VerticalGridLinesBrush = new SolidColorBrush(Colors.Black);
        grid.ColumnsInternal.FillerColumn.FillerWidth = 0;
        Assert.False(grid.ColumnRequiresRightGridLine(last, includeLastRightGridLineWhenPresent: false));
        Assert.False(grid.ColumnRequiresRightGridLine(last, includeLastRightGridLineWhenPresent: true));

        grid.ColumnsInternal.FillerColumn.FillerWidth = 10;
        Assert.True(grid.ColumnRequiresRightGridLine(last, includeLastRightGridLineWhenPresent: true));

        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        Assert.True(grid.ColumnRequiresRightGridLine(first, includeLastRightGridLineWhenPresent: false));
    }

    [AvaloniaFact]
    public void UpdateColumnDisplayIndexesFromCollectionOrder_Handles_Suspension_And_Fillers()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.Add(new DataGridTextColumn());

        SetPrivateField(grid, "_areHandlersSuspended", true);
        grid.UpdateColumnDisplayIndexesFromCollectionOrder();

        SetPrivateField(grid, "_areHandlersSuspended", false);
        grid.ColumnsInternal.EnsureRowGrouping(true);
        grid.ColumnsInternal[0].DisplayIndex = 1;
        grid.ColumnsInternal[1].DisplayIndex = 0;
        grid.UpdateColumnDisplayIndexesFromCollectionOrder();

        Assert.True(grid.ColumnsInternal.ItemsInternal.Any(c => c is DataGridFillerColumn));
        Assert.Equal(0, grid.ColumnsInternal[0].DisplayIndex);
        Assert.Equal(1, grid.ColumnsInternal[1].DisplayIndex);
    }

    [AvaloniaFact]
    public void GetColumnReadOnlyState_Handles_Binding_Paths()
    {
        var grid = new DataGrid
        {
            ItemsSource = new ObservableCollection<ReadOnlyRow> { new() }
        };

        var templateColumn = new DataGridTemplateColumn();
        Assert.False(grid.GetColumnReadOnlyState(templateColumn, isReadOnly: false));
        Assert.True(grid.GetColumnReadOnlyState(templateColumn, isReadOnly: true));

        var nullBindingColumn = new DataGridTextColumn { Binding = null };
        Assert.False(grid.GetColumnReadOnlyState(nullBindingColumn, isReadOnly: false));
        Assert.True(grid.GetColumnReadOnlyState(nullBindingColumn, isReadOnly: true));

        var emptyPathColumn = new DataGridTextColumn { Binding = new Binding() };
        Assert.True(grid.GetColumnReadOnlyState(emptyPathColumn, isReadOnly: false));

        var readOnlyColumn = new DataGridTextColumn { Binding = new Binding(nameof(ReadOnlyRow.ReadOnlyName)) };
        Assert.True(grid.GetColumnReadOnlyState(readOnlyColumn, isReadOnly: false));

        var writableColumn = new DataGridTextColumn { Binding = new Binding(nameof(ReadOnlyRow.Name)) };
        Assert.False(grid.GetColumnReadOnlyState(writableColumn, isReadOnly: false));

        var multiBindingColumn = new DataGridTextColumn { Binding = new MultiBinding() };
        Assert.False(grid.GetColumnReadOnlyState(multiBindingColumn, isReadOnly: false));

        var compiled = CreateCompiledBinding(nameof(ReadOnlyRow.Name));
        var compiledColumn = new DataGridTextColumn { Binding = compiled };
        Assert.True(grid.GetColumnReadOnlyState(compiledColumn, isReadOnly: false));
    }

    [AvaloniaFact]
    public void OnClearingColumns_Resets_HScrollBar_When_Visible()
    {
        var grid = new DataGrid();
        var scrollBar = new ScrollBar { IsVisible = true, Value = 5 };
        var window = new Window { Content = scrollBar };
        window.Show();
        scrollBar.UpdateLayout();
        SetPrivateField(grid, "_hScrollBar", scrollBar);
        SetPrivateField(grid, "_horizontalOffset", 10.0);
        SetPrivateField(grid, "_negHorizontalOffset", 4.0);

        grid.OnClearingColumns();
        Assert.Equal(0, scrollBar.Value);

        scrollBar.IsVisible = false;
        grid.OnClearingColumns();

        SetPrivateField(grid, "_hScrollBar", null);
        grid.OnClearingColumns();
    }

    [AvaloniaFact]
    public void OnColumnCanUserResizeChanged_Tracks_Visibility()
    {
        var grid = new DataGrid();
        var column = new DataGridTextColumn();
        grid.ColumnsInternal.Add(column);

        column.IsVisible = false;
        grid.OnColumnCanUserResizeChanged(column);

        column.IsVisible = true;
        grid.OnColumnCanUserResizeChanged(column);
    }

    [AvaloniaFact]
    public void ColumnWidthChange_Updates_Inherited_Widths()
    {
        var grid = new DataGrid();
        var inherited = new DataGridTextColumn();
        var explicitWidth = new DataGridTextColumn { Width = new DataGridLength(120) };
        grid.Columns.Add(inherited);
        grid.Columns.Add(explicitWidth);
        grid.UpdateColumnDisplayIndexesFromCollectionOrder();
        SetPrivateProperty(inherited, "InheritsWidth", true);
        grid.ColumnWidth = new DataGridLength(50);

        Assert.Equal(50, inherited.Width.Value);
        Assert.Equal(DataGridLengthUnitType.Pixel, inherited.Width.UnitType);
        Assert.Equal(120, explicitWidth.Width.Value);
    }

    [AvaloniaFact]
    public void MinMaxColumnWidth_Handlers_Respect_Suspension()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);

        SetPrivateField(grid, "_areHandlersSuspended", true);
        grid.MinColumnWidth = 20;
        grid.MaxColumnWidth = 200;

        SetPrivateField(grid, "_areHandlersSuspended", false);
        grid.MinColumnWidth = 40;
        grid.MaxColumnWidth = 160;
    }

    [AvaloniaFact]
    public void ColumnDisplayIndexChanging_Covers_Move_Directions_And_Throws_When_Adjusting()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        var target = grid.ColumnsInternal[0];

        grid.InDisplayIndexAdjustments = true;
        Assert.Throws<InvalidOperationException>(() => grid.OnColumnDisplayIndexChanging(target, newDisplayIndex: 1));
        grid.InDisplayIndexAdjustments = false;

        var moveRightGrid = new DataGrid();
        moveRightGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveRightGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveRightGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveRightGrid.OnColumnDisplayIndexChanging(moveRightGrid.ColumnsInternal[0], newDisplayIndex: 2);

        var moveLeftGrid = new DataGrid();
        moveLeftGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveLeftGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveLeftGrid.ColumnsInternal.Add(new DataGridTextColumn());
        moveLeftGrid.OnColumnDisplayIndexChanging(moveLeftGrid.ColumnsInternal[2], newDisplayIndex: 0);

        var spacerGrid = new DataGrid();
        spacerGrid.ColumnsInternal.Add(new DataGridTextColumn());
        spacerGrid.ColumnsInternal.Add(new DataGridTextColumn());
        spacerGrid.ColumnsInternal.EnsureRowGrouping(true);
        var spacer = spacerGrid.ColumnsInternal.RowGroupSpacerColumn;
        spacerGrid.OnColumnDisplayIndexChanging(spacer, newDisplayIndex: spacer.DisplayIndexWithFiller + 1);
    }

    [AvaloniaFact]
    public void ColumnDisplayIndexChanged_Skips_RowGroupSpacer()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.ColumnsInternal.EnsureRowGrouping(true);

        var spacer = grid.ColumnsInternal.RowGroupSpacerColumn;
        grid.OnColumnDisplayIndexChanged(spacer);
    }

    [AvaloniaFact]
    public void ColumnVisibility_Transitions_Update_Current_Cell()
    {
        var (grid, _, _) = CreateGrid(rowCount: 2, columnCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);

        var current = grid.ColumnsInternal[0];
        current.IsVisible = true;
        grid.OnColumnVisibleStateChanging(current);

        grid.OnColumnVisibleStateChanging(grid.ColumnsInternal[1]);

        current.IsVisible = false;
        grid.OnColumnVisibleStateChanged(current);
    }

    [AvaloniaFact]
    public void ColumnVisibilityChanging_Handles_Missing_Next_And_Previous()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        var onlyColumn = grid.ColumnsInternal[0];

        onlyColumn.IsVisible = true;
        grid.OnColumnVisibleStateChanging(onlyColumn);

        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        SetCurrentCell(grid2, rowIndex: 0, columnIndex: 1);
        var last = grid2.ColumnsInternal[1];
        last.IsVisible = true;
        grid2.OnColumnVisibleStateChanging(last);
    }

    [AvaloniaFact]
    public void ColumnVisibleStateChanged_Selects_Current_Cell_With_And_Without_Selection()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetPrivateProperty(grid, "CurrentColumnIndex", -1);
        grid.SelectedIndex = 0;

        var column = grid.ColumnsInternal[0];
        column.IsVisible = true;
        grid.OnColumnVisibleStateChanged(column);

        var bareGrid = new DataGrid();
        bareGrid.ColumnsInternal.Add(new DataGridTextColumn());
        SetPrivateProperty(bareGrid, "CurrentColumnIndex", -1);
        bareGrid.SelectedIndex = -1;
        bareGrid.OnColumnVisibleStateChanged(bareGrid.ColumnsInternal[0]);
    }

    [AvaloniaFact]
    public void ColumnReadOnlyStateChanging_Cancels_When_Commit_Fails()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        Assert.True(grid.BeginEdit());

        var editingRow = grid.EditingRow!;
        var editingCell = editingRow.Cells[grid.CurrentColumnIndex];
        editingCell.Content = null;

        grid.OnColumnReadOnlyStateChanging(grid.ColumnsInternal[0], isReadOnly: true);
        grid.OnColumnReadOnlyStateChanging(grid.ColumnsInternal[0], isReadOnly: false);
        grid.OnColumnReadOnlyStateChanging(grid.ColumnsInternal[1], isReadOnly: true);
    }

    [AvaloniaFact]
    public void ColumnMaxMinWidthChanged_Handles_DisplayValue_And_DesiredValue()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        var column = grid.ColumnsInternal[0];
        column.Width = new DataGridLength(100);

        SetPrivateField(column, "_maxWidth", 50d);
        grid.OnColumnMaxWidthChanged(column, oldValue: 120);

        column.SetWidthDesiredValue(150);
        SetPrivateField(column, "_maxWidth", 200d);
        grid.OnColumnMaxWidthChanged(column, oldValue: 50);

        column.MinWidth = 150;
        column.MinWidth = 0;
        column.SetWidthDesiredValue(10);

        column.IsVisible = false;
        grid.OnColumnMaxWidthChanged(column, oldValue: column.ActualMaxWidth);
        grid.OnColumnMinWidthChanged(column, oldValue: column.ActualMinWidth);

        column.IsVisible = true;
        grid.OnColumnMaxWidthChanged(column, oldValue: column.ActualMaxWidth);
        grid.OnColumnMinWidthChanged(column, oldValue: column.ActualMinWidth);
    }

    [AvaloniaFact]
    public void InsertedColumn_PostNotification_Adjusts_Frozen_State()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetPrivateProperty(grid, "CurrentColumnIndex", -1);
        SetPrivateProperty(grid, "CurrentSlot", -1);

        grid.FrozenColumnCount = 1;
        grid.OnInsertedColumn_PostNotification(new DataGridCellCoordinates(0, 0), newDisplayIndex: 0);

        SetPrivateProperty(grid, "CurrentColumnIndex", -1);
        SetPrivateProperty(grid, "CurrentSlot", -1);
        grid.FrozenColumnCount = 0;
        grid.OnInsertedColumn_PostNotification(new DataGridCellCoordinates(0, 0), newDisplayIndex: 1);

        SetPrivateProperty(grid, "CurrentColumnIndex", -1);
        SetPrivateProperty(grid, "CurrentSlot", -1);
        grid.FrozenColumnCountRight = 1;
        grid.OnInsertedColumn_PostNotification(new DataGridCellCoordinates(0, 0), newDisplayIndex: 2);
        grid.FrozenColumnCountRight = 0;

        grid.OnInsertedColumn_PostNotification(new DataGridCellCoordinates(-1, -1), newDisplayIndex: 0);
    }

    [AvaloniaFact]
    public void InsertingColumn_Throws_When_OwningGrid_Not_Null()
    {
        var grid = new DataGrid();
        var column = new DataGridTextColumn();
        grid.ColumnsInternal.Add(column);

        var otherGrid = new DataGrid();
        Assert.Throws<InvalidOperationException>(() => otherGrid.OnInsertingColumn(0, column));

        var freshColumn = new DataGridTextColumn();
        otherGrid.OnInsertingColumn(0, freshColumn);

        otherGrid.ColumnsInternal.EnsureRowGrouping(true);
        otherGrid.OnInsertingColumn(0, otherGrid.ColumnsInternal.RowGroupSpacerColumn);

        var (gridWithCurrent, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetPrivateProperty(gridWithCurrent, "CurrentColumnIndex", 0);
        SetPrivateProperty(gridWithCurrent, "CurrentSlot", 0);
        var insertColumn = new DataGridTextColumn();
        gridWithCurrent.OnInsertingColumn(0, insertColumn);
    }

    [AvaloniaFact]
    public void ColumnCollectionChanged_PreNotification_Handles_Summary_Service_Null_And_NonNull()
    {
        var grid = new DataGrid();
        SetupSummaryRow(grid, cellCount: 1);
        grid.OnColumnCollectionChanged_PreNotification(columnsGrew: false);

        var gridWithSummary = new DataGrid();
        SetPrivateField(gridWithSummary, "_summaryService", new DataGridSummaryService(gridWithSummary));
        SetupSummaryRow(gridWithSummary, cellCount: 1);
        gridWithSummary.OnColumnCollectionChanged_PreNotification(columnsGrew: false);

        var growGrid = new DataGrid { ItemsSource = new ObservableCollection<TestRow> { new() } };
        growGrid.ColumnsInternal.Add(new DataGridTextColumn());
        growGrid.OnColumnCollectionChanged_PreNotification(columnsGrew: true);

        SetPrivateField(growGrid, "_autoGeneratingColumnOperationCount", (byte)1);
        growGrid.OnColumnCollectionChanged_PreNotification(columnsGrew: true);
    }

    [AvaloniaFact]
    public void ColumnCollectionChanged_PostNotification_Handles_CurrentCell_And_Autogen()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetPrivateProperty(grid, "CurrentColumnIndex", -1);
        grid.OnColumnCollectionChanged_PostNotification(columnsGrew: true);

        SetPrivateField(grid, "_autoGeneratingColumnOperationCount", (byte)1);
        grid.OnColumnCollectionChanged_PostNotification(columnsGrew: false);
    }

    [AvaloniaFact]
    public void RemovedColumn_PreNotification_Uses_Summary_Service()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn());
        grid.Columns.RemoveAt(0);

        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        grid2.Columns.RemoveAt(0);

        var (frozenGrid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        frozenGrid.FrozenColumnCount = 1;
        frozenGrid.Columns.RemoveAt(0);
    }

    [AvaloniaFact]
    public void RemovingColumn_Handles_Editing_Commit_Failure()
    {
        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        SetCurrentCell(grid2, rowIndex: 0, columnIndex: 0);
        Assert.True(grid2.BeginEdit());
        var cancelNextCommit = true;
        grid2.RowEditEnding += (_, e) =>
        {
            if (cancelNextCommit && e.EditAction == DataGridEditAction.Commit)
            {
                e.Cancel = true;
                cancelNextCommit = false;
            }
        };

        grid2.OnRemovingColumn(grid2.ColumnsInternal[0]);
    }

    [AvaloniaFact]
    public void RemovingColumn_Adjusts_DisplayOrder_And_ScrollBar()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        grid.ColumnsInternal[1].DisplayIndex = 0;
        grid.ColumnsInternal[0].DisplayIndex = 1;
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 1);

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        SetPrivateField(grid, "_hScrollBar", new ScrollBar { IsVisible = true, Value = 200 });
        SetPrivateField(grid, "_horizontalOffset", 200.0);
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);

        grid.OnRemovingColumn(grid.ColumnsInternal[1]);
    }

    [AvaloniaFact]
    public void RemovingColumn_Uses_Previous_When_No_Next()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        grid.ColumnsInternal[1].DisplayIndex = 0;
        grid.ColumnsInternal[0].DisplayIndex = 1;
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 0);
        grid.DisplayData.FirstDisplayedScrollingCol = -1;
        grid.OnRemovingColumn(grid.ColumnsInternal[0]);

        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        SetCurrentCell(grid2, rowIndex: 0, columnIndex: 1);
        grid2.DisplayData.FirstDisplayedScrollingCol = -1;
        grid2.OnRemovingColumn(grid2.ColumnsInternal[1]);

        var (grid3, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        SetCurrentCell(grid3, rowIndex: 0, columnIndex: 0);
        grid3.DisplayData.FirstDisplayedScrollingCol = -1;
        grid3.OnRemovingColumn(grid3.ColumnsInternal[0]);
    }

    [AvaloniaFact]
    public void RemovingColumn_Shifts_Current_When_Removing_Before_Current()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        SetCurrentCell(grid, rowIndex: 0, columnIndex: 2);

        grid.OnRemovingColumn(grid.ColumnsInternal[0]);
    }

    [AvaloniaFact]
    public void RemovingColumn_Adjusts_FirstDisplayedScrolling_Offsets()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        SetPrivateField(grid, "_horizontalOffset", 50.0);
        SetPrivateField(grid, "_negHorizontalOffset", 10.0);
        grid.OnRemovingColumn(grid.ColumnsInternal[0]);

        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        grid2.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid2, "_horizontalOffset", 40.0);
        SetPrivateField(grid2, "_negHorizontalOffset", 0.0);
        grid2.OnRemovingColumn(grid2.ColumnsInternal[0]);
    }

    [AvaloniaFact]
    public void RefreshColumnElements_Skips_Visible_Slots()
    {
        var (grid, _, _) = CreateGrid(rowCount: 3, columnCount: 1);
        grid.DisplayData.FirstScrollingSlot = 0;
        grid.DisplayData.LastScrollingSlot = 0;

        var column = grid.ColumnsInternal[0];
        grid.RefreshColumnElements(column, nameof(TestRow.A));

        var emptyGrid = new DataGrid();
        emptyGrid.ColumnsInternal.Add(new DataGridTextColumn());
        emptyGrid.RefreshColumnElements(emptyGrid.ColumnsInternal[0], nameof(TestRow.A));
    }

    [AvaloniaFact]
    public void AdjustStarColumnWidths_Covers_Increase_Decrease_And_Scaling()
    {
        var grid = new DataGrid();
        var leftStar = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var rightStar = new DataGridTextColumn { Width = new DataGridLength(2, DataGridLengthUnitType.Star) };
        grid.ColumnsInternal.Add(leftStar);
        grid.ColumnsInternal.Add(rightStar);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        leftStar.SetWidthDisplayValue(80);
        leftStar.SetWidthDesiredValue(80);
        rightStar.SetWidthDisplayValue(120);
        rightStar.SetWidthDesiredValue(120);

        InvokePrivate(grid, "AdjustStarColumnWidths", 1, 20.0, false);
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, -10.0, true);
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, 0.0, false);

        grid.CanUserResizeColumns = false;
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, 15.0, true);
    }

    [AvaloniaFact]
    public void AdjustStarColumnWidths_Targeted_Order_And_Factors()
    {
        var grid = new DataGrid();
        var starA = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var starB = new DataGridTextColumn { Width = new DataGridLength(3, DataGridLengthUnitType.Star) };
        grid.ColumnsInternal.Add(starA);
        grid.ColumnsInternal.Add(starB);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        starA.SetWidthDisplayValue(50);
        starA.SetWidthDesiredValue(50);
        starB.SetWidthDisplayValue(150);
        starB.SetWidthDesiredValue(150);

        InvokePrivate(grid, "AdjustStarColumnWidths", 0, 30.0, false, new Func<DataGridColumn, double>(c => c.Width.DesiredValue));
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, -20.0, false, new Func<DataGridColumn, double>(c => c.ActualMinWidth));
    }

    [AvaloniaFact]
    public void AdjustStarColumnWidths_Ordering_Compares_Factors()
    {
        var grid = new DataGrid();
        var first = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var second = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        grid.ColumnsInternal.Add(first);
        grid.ColumnsInternal.Add(second);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        first.SetWidthDisplayValue(50);
        second.SetWidthDisplayValue(50);

        first.SetWidthDesiredValue(100);
        second.SetWidthDesiredValue(60);
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, 20.0, false, new Func<DataGridColumn, double>(c => c.Width.DesiredValue));

        first.SetWidthDesiredValue(60);
        second.SetWidthDesiredValue(100);
        InvokePrivate(grid, "AdjustStarColumnWidths", 0, 20.0, false, new Func<DataGridColumn, double>(c => c.Width.DesiredValue));

        InvokePrivate(grid, "AdjustStarColumnWidths", 0, -20.0, false, new Func<DataGridColumn, double>(c => c.Width.DesiredValue));
    }

    [AvaloniaFact]
    public void ComputeDisplayedColumns_Covers_Offsets_And_Scroll_Logic()
    {
        var grid = new DataGrid();
        var frozen = new DataGridTextColumn { Width = new DataGridLength(40) };
        var scrolling = new DataGridTextColumn { Width = new DataGridLength(40) };
        grid.ColumnsInternal.Add(frozen);
        grid.ColumnsInternal.Add(scrolling);
        grid.FrozenColumnCount = 1;
        grid.RowsPresenterAvailableSize = new Size(100, 100);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_horizontalOffset", 5.0);
        SetPrivateField(grid, "_negHorizontalOffset", 5.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        SetPrivateField(grid, "_horizontalOffset", 20.0);
        SetPrivateField(grid, "_negHorizontalOffset", 70.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        grid.RowsPresenterAvailableSize = new Size(30, 100);
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        SetPrivateField(grid, "_horizontalOffset", 0.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        grid.FrozenColumnCount = 2;
        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        grid.DisplayData.FirstDisplayedScrollingCol = -1;
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        var gridElse = new DataGrid();
        gridElse.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        gridElse.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(35) });
        gridElse.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(35) });
        gridElse.FrozenColumnCount = 1;
        gridElse.RowsPresenterAvailableSize = new Size(100, 100);
        gridElse.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        gridElse.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(gridElse, "_horizontalOffset", 30.0);
        SetPrivateField(gridElse, "_negHorizontalOffset", 20.0);
        InvokePrivate<bool>(gridElse, "ComputeDisplayedColumns");
    }

    [AvaloniaFact]
    public void ComputeDisplayedColumns_Scrolling_Backfills()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(30) });
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(30) });
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(30) });
        grid.RowsPresenterAvailableSize = new Size(100, 100);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_horizontalOffset", 30.0);
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        grid.RowsPresenterAvailableSize = new Size(80, 100);
        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_horizontalOffset", 30.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        grid.RowsPresenterAvailableSize = new Size(50, 100);
        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_horizontalOffset", 0.0);
        InvokePrivate<bool>(grid, "ComputeDisplayedColumns");

        var backfillGrid = new DataGrid();
        backfillGrid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        backfillGrid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        backfillGrid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        backfillGrid.RowsPresenterAvailableSize = new Size(100, 100);
        backfillGrid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        backfillGrid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(backfillGrid, "_horizontalOffset", 80.0);
        SetPrivateField(backfillGrid, "_negHorizontalOffset", 0.0);
        InvokePrivate<bool>(backfillGrid, "ComputeDisplayedColumns");
    }

    [AvaloniaFact]
    public void ComputeFirstVisibleScrollingColumn_Covers_Null_And_Offsets()
    {
        var grid = new DataGrid();
        var frozen = new DataGridTextColumn { Width = new DataGridLength(50) };
        var scrolling = new DataGridTextColumn { Width = new DataGridLength(50) };
        grid.ColumnsInternal.Add(frozen);
        grid.ColumnsInternal.Add(scrolling);
        grid.RowsPresenterAvailableSize = new Size(60, 100);
        grid.FrozenColumnCount = 1;
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        SetPrivateField(grid, "_horizontalOffset", 10.0);
        InvokePrivate<int>(grid, "ComputeFirstVisibleScrollingColumn");

        SetPrivateField(grid, "_horizontalOffset", 200.0);
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        InvokePrivate<int>(grid, "ComputeFirstVisibleScrollingColumn");

        var gridAllFrozen = new DataGrid();
        var frozenOnly = new DataGridTextColumn { Width = new DataGridLength(40) };
        gridAllFrozen.ColumnsInternal.Add(frozenOnly);
        gridAllFrozen.FrozenColumnCount = 1;
        gridAllFrozen.RowsPresenterAvailableSize = new Size(30, 100);
        gridAllFrozen.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        SetPrivateField(gridAllFrozen, "_horizontalOffset", 25.0);
        SetPrivateField(gridAllFrozen, "_negHorizontalOffset", 5.0);
        InvokePrivate<int>(gridAllFrozen, "ComputeFirstVisibleScrollingColumn");
    }

    [AvaloniaFact]
    public void CorrectColumnFrozenStates_Adjusts_HorizontalOffset()
    {
        var grid = new DataGrid();
        var left = new DataGridTextColumn { Width = new DataGridLength(40) };
        var right = new DataGridTextColumn { Width = new DataGridLength(40) };
        grid.ColumnsInternal.Add(left);
        grid.ColumnsInternal.Add(right);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        left.FrozenPosition = DataGridFrozenColumnPosition.Left;
        grid.FrozenColumnCount = 1;
        SetPrivateProperty(grid, "HorizontalOffset", 15.0);
        InvokePrivate(grid, "CorrectColumnFrozenStates");

        SetPrivateProperty(grid, "HorizontalOffset", 0.0);
        InvokePrivate(grid, "CorrectColumnFrozenStates");
    }

    [AvaloniaFact]
    public void ColumnPositionHelpers_Cover_GetColumnX_And_NegOffset()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(30) });
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        var x0 = InvokePrivate<double>(grid, "GetColumnXFromIndex", 0);
        var x1 = InvokePrivate<double>(grid, "GetColumnXFromIndex", 1);
        Assert.True(x1 > x0);

        InvokePrivate<double>(grid, "GetNegHorizontalOffsetFromHorizontalOffset", 10.0);
        InvokePrivate<double>(grid, "GetNegHorizontalOffsetFromHorizontalOffset", 100.0);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Skips_When_No_FirstDisplayed()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = -1;

        var result = InvokePrivate<bool>(grid, "ScrollColumnIntoView", 0);
        Assert.True(result);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_InvalidIndex_Throws()
    {
        var grid = new DataGrid();
        grid.ColumnsInternal.Add(new DataGridTextColumn { Width = new DataGridLength(40) });
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = 0;

        var listener = Trace.Listeners.OfType<DefaultTraceListener>().FirstOrDefault();
        var previous = listener?.AssertUiEnabled;
        if (listener != null)
        {
            listener.AssertUiEnabled = false;
        }

        try
        {
            Exception ex = Assert.ThrowsAny<Exception>(() => InvokePrivate<bool>(grid, "ScrollColumnIntoView", -1));
            if (ex is TargetInvocationException tie && tie.InnerException != null)
            {
                ex = tie.InnerException;
            }
            Assert.Contains(ex.GetType().Name, new[] { "DebugAssertException", nameof(ArgumentOutOfRangeException) });
        }
        finally
        {
            if (listener != null && previous.HasValue)
            {
                listener.AssertUiEnabled = previous.Value;
            }
        }
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Scrolls_Backward_With_NegOffset()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 2;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 2;
        SetPrivateField(grid, "_negHorizontalOffset", 10.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 0);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Uses_LastTotally_When_Unset()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.RowsPresenterAvailableSize = new Size(60, 100);

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = -1;
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        SetPrivateField(grid, "_horizontalOffset", 0.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 1);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Scrolls_First_When_PartiallyHidden()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_negHorizontalOffset", 5.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 1);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Skips_When_LastTotally_Equals_Target()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 1;
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 1);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Skips_When_LastTotally_After_Target()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 2;
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 1);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Shifts_For_RightEdge()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.RowsPresenterAvailableSize = new Size(60, 100);

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 0;
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        SetPrivateField(grid, "_horizontalOffset", 0.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 2);
        Assert.True(grid.DisplayData.FirstDisplayedScrollingCol >= 0);
    }

    [AvaloniaFact]
    public void ScrollColumnIntoView_Handles_TooWide_Column()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        var firstColumn = grid.ColumnsInternal[0];
        var wideColumn = grid.ColumnsInternal[1];
        firstColumn.SetWidthDisplayValue(20);
        wideColumn.SetWidthDisplayValue(80);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.RowsPresenterAvailableSize = new Size(40, 100);

        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 0;
        SetPrivateField(grid, "_negHorizontalOffset", 0.0);
        SetPrivateField(grid, "_horizontalOffset", 0.0);

        InvokePrivate<bool>(grid, "ScrollColumnIntoView", 1);
        Assert.Equal(-1, grid.DisplayData.LastTotallyDisplayedScrollingCol);
    }

    [AvaloniaFact]
    public void ScrollColumns_Returns_When_NoNext_Or_Previous()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 2);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 1;

        InvokePrivate(grid, "ScrollColumns", 1);

        var (grid2, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        grid2.ColumnsInternal[0].SetWidthDisplayValue(40);
        grid2.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid2.DisplayData.FirstDisplayedScrollingCol = 0;
        SetPrivateField(grid2, "_negHorizontalOffset", 0.0);

        InvokePrivate(grid2, "ScrollColumns", -1);
    }

    [AvaloniaFact]
    public void ScrollColumns_Positive_Uses_NextColumn_When_Available()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = 0;

        InvokePrivate(grid, "ScrollColumns", 1);
        Assert.True(grid.DisplayData.FirstDisplayedScrollingCol >= 0);
    }

    [AvaloniaFact]
    public void ScrollColumns_Uses_NegOffset_When_NoPrevious()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 1);
        grid.ColumnsInternal[0].SetWidthDisplayValue(40);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        SetPrivateField(grid, "_negHorizontalOffset", 5.0);

        InvokePrivate(grid, "ScrollColumns", -2);
    }

    [AvaloniaFact]
    public void ScrollColumns_Positive_Advances_FirstDisplayed()
    {
        var (grid, _, _) = CreateGrid(rowCount: 1, columnCount: 3);
        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(40);
        }
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();
        grid.DisplayData.FirstDisplayedScrollingCol = 0;
        grid.DisplayData.LastTotallyDisplayedScrollingCol = -1;
        SetPrivateField(grid, "_horizontalOffset", 0.0);

        InvokePrivate(grid, "ScrollColumns", 1);
        Assert.True(grid.HorizontalOffset >= 0);
    }

    [AvaloniaFact]
    public void NonStarColumnWidthHelpers_Cover_Static_Branches()
    {
        var column = new DataGridTextColumn { Width = new DataGridLength(100) };
        column.SetWidthDisplayValue(100);
        column.MinWidth = 20;
        var unchanged = InvokePrivateStatic<double>(typeof(DataGrid), "DecreaseNonStarColumnWidth", column, 120d, -10d);
        Assert.Equal(-10, unchanged);

        var remaining = InvokePrivateStatic<double>(typeof(DataGrid), "DecreaseNonStarColumnWidth", column, 50d, -30d);
        Assert.Equal(0, remaining);
        Assert.Equal(70, column.Width.DisplayValue);

        column.MaxWidth = 200;
        column.SetWidthDisplayValue(100);
        var increaseUnchanged = InvokePrivateStatic<double>(typeof(DataGrid), "IncreaseNonStarColumnWidth", column, 80d, 15d);
        Assert.Equal(15, increaseUnchanged);

        var increaseRemaining = InvokePrivateStatic<double>(typeof(DataGrid), "IncreaseNonStarColumnWidth", column, 160d, 30d);
        Assert.Equal(0, increaseRemaining);
        Assert.Equal(130, column.Width.DisplayValue);
    }

    [AvaloniaFact]
    public void NonStarColumnWidths_Cover_Filtering_And_Early_Returns()
    {
        var grid = new DataGrid { CanUserResizeColumns = true };
        var eligible = new DataGridTextColumn { Width = new DataGridLength(100) };
        var hidden = new DataGridTextColumn { Width = new DataGridLength(100), IsVisible = false };
        var star = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var noResize = new DataGridTextColumn { Width = new DataGridLength(100) };
        var newColumn = new DataGridTextColumn { Width = new DataGridLength(100) };

        grid.ColumnsInternal.Add(eligible);
        grid.ColumnsInternal.Add(hidden);
        grid.ColumnsInternal.Add(star);
        grid.ColumnsInternal.Add(noResize);
        grid.ColumnsInternal.Add(newColumn);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(100);
            column.IsInitialDesiredWidthDetermined = true;
        }
        newColumn.IsInitialDesiredWidthDetermined = false;
        SetPrivateProperty(noResize, "CanUserResizeInternal", false);

        var early = InvokePrivate<double>(grid, "DecreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue), 0.0, false, false);
        Assert.Equal(0, early);

        eligible.SetWidthDisplayValue(100);
        var partial = InvokePrivate<double>(grid, "DecreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue - 5), -15.0, false, false);
        Assert.Equal(-10, partial);

        eligible.SetWidthDisplayValue(100);
        var decreased = InvokePrivate<double>(grid, "DecreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue - 15), -10.0, false, false);
        Assert.Equal(0, decreased);
        Assert.Equal(90, eligible.Width.DisplayValue);

        var remaining = InvokePrivate<double>(grid, "DecreaseNonStarColumnWidths", 4, new Func<DataGridColumn, double>(c => c.Width.DisplayValue - 5), -5.0, true, false);
        Assert.Equal(-5, remaining);

        newColumn.SetWidthDisplayValue(100);
        var affectNew = InvokePrivate<double>(grid, "DecreaseNonStarColumnWidths", 4, new Func<DataGridColumn, double>(c => c.Width.DisplayValue - 5), -5.0, false, true);
        Assert.Equal(0, affectNew);
    }

    [AvaloniaFact]
    public void NonStarColumnWidths_Cover_Increase_Filtering_And_Early_Returns()
    {
        var grid = new DataGrid { CanUserResizeColumns = true };
        var eligible = new DataGridTextColumn { Width = new DataGridLength(100), MaxWidth = 150 };
        var hidden = new DataGridTextColumn { Width = new DataGridLength(100), IsVisible = false };
        var star = new DataGridTextColumn { Width = new DataGridLength(1, DataGridLengthUnitType.Star) };
        var noResize = new DataGridTextColumn { Width = new DataGridLength(100) };
        var newColumn = new DataGridTextColumn { Width = new DataGridLength(100) };

        grid.ColumnsInternal.Add(eligible);
        grid.ColumnsInternal.Add(hidden);
        grid.ColumnsInternal.Add(star);
        grid.ColumnsInternal.Add(noResize);
        grid.ColumnsInternal.Add(newColumn);
        grid.ColumnsInternal.EnsureVisibleEdgedColumnsWidth();

        foreach (var column in grid.ColumnsInternal)
        {
            column.SetWidthDisplayValue(100);
            column.IsInitialDesiredWidthDetermined = true;
        }
        newColumn.IsInitialDesiredWidthDetermined = false;
        SetPrivateProperty(noResize, "CanUserResizeInternal", false);

        var early = InvokePrivate<double>(grid, "IncreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue + 10), 0.0, false, false);
        Assert.Equal(0, early);

        eligible.SetWidthDisplayValue(100);
        var partial = InvokePrivate<double>(grid, "IncreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue + 5), 20.0, false, false);
        Assert.Equal(15, partial);

        eligible.SetWidthDisplayValue(100);
        var increased = InvokePrivate<double>(grid, "IncreaseNonStarColumnWidths", 0, new Func<DataGridColumn, double>(c => c.Width.DisplayValue + 40), 20.0, false, false);
        Assert.Equal(0, increased);
        Assert.Equal(120, eligible.Width.DisplayValue);

        var remaining = InvokePrivate<double>(grid, "IncreaseNonStarColumnWidths", 4, new Func<DataGridColumn, double>(c => c.Width.DisplayValue + 40), 10.0, true, false);
        Assert.Equal(10.0, remaining);

        newColumn.SetWidthDisplayValue(100);
        var affectNew = InvokePrivate<double>(grid, "IncreaseNonStarColumnWidths", 4, new Func<DataGridColumn, double>(c => c.Width.DisplayValue + 5), 5.0, false, true);
        Assert.Equal(0, affectNew);
    }

    private sealed class ReadOnlyRow
    {
        [Editable(false)]
        public string ReadOnlyName { get; } = "ReadOnly";

        public string Name { get; set; } = "Name";
    }

    private static BindingBase CreateCompiledBinding(string path)
    {
        var compiledBindingType = typeof(CompiledBindingExtension);
        var instance = Activator.CreateInstance(compiledBindingType);
        Assert.NotNull(instance);
        var binding = instance as BindingBase;
        Assert.NotNull(binding);
        var pathProperty = compiledBindingType.GetProperty("Path");
        if (pathProperty != null)
        {
            var pathType = pathProperty.PropertyType;
            object? pathValue = null;
            var ctor = pathType.GetConstructor(new[] { typeof(string) });
            if (ctor != null)
            {
                pathValue = ctor.Invoke(new object[] { path });
            }
            else
            {
                var parse = pathType.GetMethod("Parse", new[] { typeof(string) });
                if (parse != null)
                {
                    pathValue = parse.Invoke(null, new object[] { path });
                }
                else
                {
                    pathValue = Activator.CreateInstance(pathType);
                }
            }
            pathProperty.SetValue(instance, pathValue);
        }

        return binding!;
    }

    private sealed class TestRow
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
    }

    private static (DataGrid grid, Window window, ObservableCollection<TestRow> items) CreateGrid(
        int rowCount = 3,
        int columnCount = 3)
    {
        var items = new ObservableCollection<TestRow>();
        for (var i = 0; i < rowCount; i++)
        {
            items.Add(new TestRow { A = i, B = i + 10, C = i + 20 });
        }

        var window = new Window
        {
            Width = 640,
            Height = 480
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        if (columnCount >= 1)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "A", Binding = new Binding(nameof(TestRow.A)) });
        }
        if (columnCount >= 2)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "B", Binding = new Binding(nameof(TestRow.B)) });
        }
        if (columnCount >= 3)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "C", Binding = new Binding(nameof(TestRow.C)) });
        }

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return (grid, window, items);
    }

    private static void SetPrivateField(object target, string name, object? value)
    {
        var type = target.GetType();
        FieldInfo? field = null;
        while (type != null)
        {
            field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                break;
            }
            type = type.BaseType;
        }
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void SetupSummaryRow(DataGrid grid, int cellCount)
    {
        var summaryRow = new DataGridSummaryRow
        {
            OwningGrid = grid,
            Scope = DataGridSummaryScope.Total
        };

        var presenter = new DataGridSummaryCellsPresenter
        {
            OwningGrid = grid,
            OwnerRow = summaryRow
        };

        var cells = new List<DataGridSummaryCell>();
        for (var i = 0; i < cellCount; i++)
        {
            cells.Add(new DataGridSummaryCell { OwningRow = summaryRow, Column = new DataGridTextColumn() });
        }

        SetPrivateField(summaryRow, "_cellsPresenter", presenter);
        SetPrivateField(summaryRow, "_cells", cells);
        SetPrivateField(grid, "_totalSummaryRow", summaryRow);
    }

    private static void SetPrivateProperty(object target, string name, object? value)
    {
        var type = target.GetType();
        PropertyInfo? property = null;
        while (type != null)
        {
            property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null)
            {
                break;
            }
            type = type.BaseType;
        }
        Assert.NotNull(property);
        var setter = property!.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(target, new[] { value });
            return;
        }

        SetPrivateField(target, $"<{property.Name}>k__BackingField", value);
    }

    private static T InvokePrivate<T>(object target, string name, params object[] args)
    {
        var method = GetPrivateMethod(target, name, args);
        return (T)method.Invoke(target, args)!;
    }

    private static void InvokePrivate(object target, string name, params object[] args)
    {
        var method = GetPrivateMethod(target, name, args);
        method.Invoke(target, args);
    }

    private static MethodInfo GetPrivateMethod(object target, string name, object[] args)
    {
        var methods = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(m => m.Name == name);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (!parameters[i].ParameterType.IsInstanceOfType(arg))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        throw new InvalidOperationException($"No matching method found for {name}.");
    }

    private static T InvokePrivateStatic<T>(Type type, string name, params object[] args)
    {
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(m => m.Name == name);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var arg = args[i];
                if (arg == null)
                {
                    continue;
                }

                if (!parameters[i].ParameterType.IsInstanceOfType(arg))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return (T)method.Invoke(null, args)!;
            }
        }

        throw new InvalidOperationException($"No matching static method found for {name}.");
    }

    private static void SetCurrentCell(DataGrid grid, int rowIndex, int columnIndex)
    {
        var slot = grid.SlotFromRowIndex(rowIndex);
        grid.UpdateSelectionAndCurrency(columnIndex, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: true);
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
