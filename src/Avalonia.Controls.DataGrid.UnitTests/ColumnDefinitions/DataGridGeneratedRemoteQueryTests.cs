// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedRemoteQueryTests
{
    [Fact]
    public async Task Newer_query_cancels_and_suppresses_older_response()
    {
        var provider = new PendingProvider();
        using var controller = new DataGridGeneratedRemoteQueryController<Row, int>(provider);

        ValueTask<DataGridQueryPage<Row, int>> first = controller.ExecuteLatestAsync(CreateQuery);
        await provider.WaitForCountAsync(1);
        ValueTask<DataGridQueryPage<Row, int>> second = controller.ExecuteLatestAsync(CreateQuery);
        await provider.WaitForCountAsync(2);

        provider.Complete(1, new Row(2, "second"));
        DataGridQueryPage<Row, int> accepted = await second;
        provider.Complete(0, new Row(1, "first"));
        DataGridQueryPage<Row, int> stale = await first;

        Assert.NotNull(accepted);
        Assert.Equal(2, accepted.Revision);
        Assert.Equal(2, accepted.Items[0].Id);
        Assert.Null(stale);
        Assert.Same(accepted, controller.LastPage);
    }

    [Fact]
    public async Task Page_cache_reuses_data_with_current_revision()
    {
        var provider = new ImmediateProvider();
        using var controller = new DataGridGeneratedRemoteQueryController<Row, int>(
            provider,
            pageCacheCapacity: 2);

        DataGridQueryPage<Row, int> first = await controller.ExecuteLatestAsync(CreateQuery, "page-0");
        DataGridQueryPage<Row, int> cached = await controller.ExecuteLatestAsync(CreateQuery, "page-0");

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, first.Revision);
        Assert.Equal(2, cached.Revision);
        Assert.Same(first.Items, cached.Items);
    }

    [Fact]
    public async Task Provider_errors_are_exposed_and_rethrown()
    {
        var expected = new InvalidOperationException("remote failure");
        using var controller = new DataGridGeneratedRemoteQueryController<Row, int>(new FailingProvider(expected));
        Exception observed = null;
        controller.StateChanged += (_, args) => observed = args.Error ?? observed;

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await controller.ExecuteLatestAsync(CreateQuery));

        Assert.Same(expected, thrown);
        Assert.Same(expected, observed);
        Assert.Same(expected, controller.LastError);
        Assert.False(controller.IsLoading);
    }

    [Fact]
    public void Page_requests_support_offset_and_cursor_modes()
    {
        DataGridPageRequest offset = DataGridPageRequest.FromOffset(20, 10);
        DataGridPageRequest cursor = DataGridPageRequest.FromCursor("next", 25);

        Assert.Equal(DataGridPageMode.Offset, offset.Mode);
        Assert.Equal(20, offset.Offset);
        Assert.Equal(DataGridPageMode.Cursor, cursor.Mode);
        Assert.Equal("next", cursor.Cursor);
        Assert.Throws<ArgumentOutOfRangeException>(() => DataGridPageRequest.FromOffset(0, 0));
    }

    private static DataGridRemoteQuery<Row> CreateQuery(long revision) =>
        new(revision, null, null, null, DataGridPageRequest.FromOffset(0, 25));

    private sealed record Row(int Id, string Name);

    private sealed class PendingProvider : IDataGridQueryProvider<Row, int>
    {
        private readonly List<(long Revision, TaskCompletionSource<DataGridQueryPage<Row, int>> Completion)> _requests = new();

        public ValueTask<DataGridQueryPage<Row, int>> ExecuteAsync(
            DataGridRemoteQuery<Row> query,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<DataGridQueryPage<Row, int>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_requests)
            {
                _requests.Add((query.Revision, completion));
            }
            return new ValueTask<DataGridQueryPage<Row, int>>(completion.Task);
        }

        public void Complete(int index, Row item)
        {
            (long revision, TaskCompletionSource<DataGridQueryPage<Row, int>> completion) = _requests[index];
            completion.SetResult(new DataGridQueryPage<Row, int>(revision, new[] { item }));
        }

        public async Task WaitForCountAsync(int count)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                lock (_requests)
                {
                    if (_requests.Count >= count)
                    {
                        return;
                    }
                }
                await Task.Delay(1);
            }
            throw new TimeoutException();
        }
    }

    private sealed class ImmediateProvider : IDataGridQueryProvider<Row, int>
    {
        public int CallCount { get; private set; }

        public ValueTask<DataGridQueryPage<Row, int>> ExecuteAsync(
            DataGridRemoteQuery<Row> query,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult(new DataGridQueryPage<Row, int>(
                query.Revision,
                new[] { new Row(1, "cached") }));
        }
    }

    private sealed class FailingProvider : IDataGridQueryProvider<Row, int>
    {
        private readonly Exception _error;

        public FailingProvider(Exception error) => _error = error;

        public ValueTask<DataGridQueryPage<Row, int>> ExecuteAsync(
            DataGridRemoteQuery<Row> query,
            CancellationToken cancellationToken) => ValueTask.FromException<DataGridQueryPage<Row, int>>(_error);
    }
}
