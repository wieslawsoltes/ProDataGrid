// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedStreamBufferTests
{
    [Fact]
    public void Coalescing_replaces_pending_key_without_growing_queue()
    {
        var buffer = new DataGridGeneratedStreamBuffer<Row, int>(
            2,
            DataGridGeneratedStreamOverflowPolicy.CoalesceByKey);

        Assert.True(buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Upsert(1, 7, new Row(7, "first"))));
        Assert.True(buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Upsert(2, 7, new Row(7, "latest"))));

        Assert.True(buffer.TryDequeue(out DataGridGeneratedStreamUpdate<Row, int> update));
        Assert.Equal("latest", update.Item.Name);
        Assert.Equal(2, update.Revision);
        Assert.Equal(1, buffer.Metrics.Coalesced);
        Assert.Equal(0, buffer.Metrics.Queued);
    }

    [Fact]
    public void Overflow_and_stale_revisions_are_accounted_for()
    {
        var buffer = new DataGridGeneratedStreamBuffer<Row, int>(
            2,
            DataGridGeneratedStreamOverflowPolicy.DropOldest);
        buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Upsert(1, 1, new Row(1, "one")));
        buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Upsert(2, 2, new Row(2, "two")));
        buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Upsert(3, 3, new Row(3, "three")));

        Assert.True(buffer.TryDequeue(out DataGridGeneratedStreamUpdate<Row, int> first));
        Assert.Equal(2, first.Key);
        buffer.MarkApplied(3, 2);
        Assert.False(buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Remove(2, 2)));

        DataGridGeneratedStreamMetrics metrics = buffer.Metrics;
        Assert.Equal(1, metrics.Dropped);
        Assert.Equal(1, metrics.Stale);
        Assert.Equal(2, metrics.Applied);
        Assert.Equal(3, metrics.LastAppliedRevision);
    }

    [Fact]
    public void Drain_uses_caller_storage_and_preserves_order()
    {
        var buffer = new DataGridGeneratedStreamBuffer<Row, int>(4);
        buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Append(1, new Row(1, "one")));
        buffer.TryEnqueue(DataGridGeneratedStreamUpdate<Row, int>.Append(2, new Row(2, "two")));
        var updates = new DataGridGeneratedStreamUpdate<Row, int>[2];

        int count = buffer.Drain(updates);

        Assert.Equal(2, count);
        Assert.Equal(1, updates[0].Item.Id);
        Assert.Equal(2, updates[1].Item.Id);
        Assert.Equal(0, buffer.Metrics.Queued);
    }

    [Fact]
    public async Task Async_pump_batches_keyed_updates_and_reports_completion()
    {
        var applied = new List<DataGridGeneratedStreamUpdate<Row, int>>();
        using var pump = new DataGridGeneratedAsyncStreamPump<Row, int>(
            new RowKey(),
            (batch, _) =>
            {
                applied.AddRange(batch.ToArray());
                return ValueTask.CompletedTask;
            },
            capacity: 4,
            batchSize: 2);
        bool completed = false;
        pump.Completed += (_, _) => completed = true;

        await pump.RunAsync(GetRows(), initialRevision: 10);

        Assert.True(completed);
        Assert.Equal(3, applied.Count);
        Assert.Equal(new long[] { 11, 12, 13 }, applied.ConvertAll(static update => update.Revision));
        Assert.Equal(3, pump.Metrics.Applied);
        Assert.Equal(13, pump.Metrics.LastAppliedRevision);
    }

    private static async IAsyncEnumerable<Row> GetRows()
    {
        await Task.Yield();
        yield return new Row(1, "one");
        yield return new Row(2, "two");
        yield return new Row(3, "three");
    }

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }

    private sealed record Row(int Id, string Name);
}
