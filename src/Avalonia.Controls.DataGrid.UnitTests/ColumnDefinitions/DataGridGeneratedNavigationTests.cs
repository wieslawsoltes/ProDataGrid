// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedNavigationTests
{
    [AvaloniaFact]
    public async Task Handler_sets_and_moves_current_cell_by_stable_column_key()
    {
        Row[] rows =
        [
            new Row(1, "One"),
            new Row(2, "Two"),
            new Row(3, "Three")
        ];
        DataGrid grid = CreateGrid(rows);
        var view = new Control();
        var window = new Window { Width = 640, Height = 360, Content = grid };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var handler = new DataGridGeneratedNavigationHandler<Row>();
            DataGridGeneratedNavigationResult<Row> selected = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.SetCurrentCell(rows[1], "name"));

            Assert.True(selected.Succeeded);
            Assert.Same(rows[1], selected.Item);
            Assert.Equal(1, selected.RowIndex);
            Assert.Equal("name", selected.ColumnKey);
            Assert.Same(rows[1], grid.CurrentCell.Item);
            Assert.Equal("name", grid.CurrentCell.Column.ColumnKey);

            DataGridGeneratedNavigationResult<Row> queried = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.QueryCurrentCell());
            Assert.True(queried.Succeeded);
            Assert.Same(rows[1], queried.Item);
            Assert.Equal("name", queried.ColumnKey);
            Assert.Equal(queried, queried);
            Assert.True(queried == queried);
            Assert.False(queried != queried);

            DataGridGeneratedNavigationResult<Row> scrolled = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.ScrollIntoView(rows[0], "id"));
            Assert.True(scrolled.Succeeded);
            Assert.Same(rows[0], scrolled.Item);
            Assert.Equal("id", scrolled.ColumnKey);

            DataGridGeneratedNavigationResult<Row> moved = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.MoveCurrentCell(columnOffset: -1, rowOffset: 1));

            Assert.True(moved.Succeeded);
            Assert.Same(rows[2], moved.Item);
            Assert.Equal(2, moved.RowIndex);
            Assert.Equal("id", moved.ColumnKey);
            Assert.Equal(0, moved.ColumnDisplayIndex);

            DataGridGeneratedNavigationResult<Row> boundary = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.MoveCurrentCell(columnOffset: -1, rowOffset: 0));

            Assert.False(boundary.Succeeded);
            Assert.Equal(DataGridGeneratedNavigationStatus.BoundaryReached, boundary.Status);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Handler_reports_scroll_and_navigation_failures_without_grid_leakage()
    {
        Row[] rows =
        [
            new Row(1, "One"),
            new Row(2, "Two"),
            new Row(3, "Three")
        ];
        DataGrid grid = CreateGrid(rows);
        var view = new Control();
        var window = new Window { Width = 640, Height = 240, Content = grid };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var handler = new DataGridGeneratedNavigationHandler<Row>();

            DataGridGeneratedNavigationResult<Row> missingColumn = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.ScrollIntoView(rows[0], "missing"));
            Assert.Equal(DataGridGeneratedNavigationStatus.ColumnNotFound, missingColumn.Status);

            DataGridGeneratedNavigationResult<Row> missingItem = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.ScrollIntoView(new Row(99, "Missing"), "id"));
            Assert.Equal(DataGridGeneratedNavigationStatus.ItemNotFound, missingItem.Status);

            DataGridGeneratedNavigationResult<Row> current = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.SetCurrentCell(rows[2], "name"));
            Assert.True(current.Succeeded);

            DataGridGeneratedNavigationResult<Row> unavailable = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.CaptureScrollState());
            Assert.Equal(DataGridGeneratedNavigationStatus.ScrollStateUnavailable, unavailable.Status);

            DataGridGeneratedNavigationResult<Row> invalidRestore = await Handle(
                handler,
                view,
                grid,
                DataGridGeneratedNavigationRequest<Row>.RestoreScrollState(null!));
            Assert.Equal(DataGridGeneratedNavigationStatus.ScrollStateUnavailable, invalidRestore.Status);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            DataGridGeneratedNavigationResult<Row> cancelled = await handler.HandleAsync(
                new DataGridGeneratedViewInteractionContext<DataGridGeneratedNavigationRequest<Row>>(
                    view,
                    grid,
                    DataGridGeneratedNavigationRequest<Row>.QueryCurrentCell(),
                    cancellation.Token));
            Assert.Equal(DataGridGeneratedNavigationStatus.Cancelled, cancelled.Status);

            DataGridGeneratedNavigationResult<Row> invalid = await handler.HandleAsync(
                new DataGridGeneratedViewInteractionContext<DataGridGeneratedNavigationRequest<Row>>(
                    view,
                    grid,
                    null!,
                    CancellationToken.None));
            Assert.Equal(DataGridGeneratedNavigationStatus.InvalidRequest, invalid.Status);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Request_factories_preserve_typed_navigation_arguments()
    {
        var row = new Row(7, "Seven");
        DataGridGeneratedNavigationRequest<Row> current =
            DataGridGeneratedNavigationRequest<Row>.SetCurrentCell(row, "name", focus: true);
        Assert.Equal(DataGridGeneratedNavigationAction.SetCurrentCell, current.Action);
        Assert.True(current.HasItem);
        Assert.Same(row, current.Item);
        Assert.Equal("name", current.ColumnKey);
        Assert.True(current.Focus);

        DataGridGeneratedNavigationRequest<Row> move =
            DataGridGeneratedNavigationRequest<Row>.MoveCurrentCell(-2, 3, focus: true);
        Assert.Equal(-2, move.ColumnOffset);
        Assert.Equal(3, move.RowOffset);
        Assert.True(move.Focus);

        var options = new DataGridStateOptions();
        DataGridGeneratedNavigationRequest<Row> capture =
            DataGridGeneratedNavigationRequest<Row>.CaptureScrollState(options);
        Assert.Same(options, capture.StateOptions);

        var state = new DataGridScrollState();
        DataGridGeneratedNavigationRequest<Row> restore =
            DataGridGeneratedNavigationRequest<Row>.RestoreScrollState(state, options);
        Assert.Same(state, restore.ScrollState);
        Assert.Same(options, restore.StateOptions);
    }

    private static DataGrid CreateGrid(IReadOnlyList<Row> rows)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = rows,
            RowHeight = 28d
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "ID",
            ColumnKey = "id",
            DisplayIndex = 0
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Name",
            ColumnKey = "name",
            DisplayIndex = 1
        });
        return grid;
    }

    private static ValueTask<DataGridGeneratedNavigationResult<Row>> Handle(
        DataGridGeneratedNavigationHandler<Row> handler,
        Control view,
        DataGrid grid,
        DataGridGeneratedNavigationRequest<Row> request) =>
        handler.HandleAsync(
            new DataGridGeneratedViewInteractionContext<DataGridGeneratedNavigationRequest<Row>>(
                view,
                grid,
                request,
                CancellationToken.None));

    private sealed record Row(int Id, string Name);
}
