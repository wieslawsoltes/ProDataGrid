// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedSnapshotReconcilerTests
{
    [Fact]
    public void Reconcile_applies_keyed_changes_without_resetting_collection()
    {
        var target = new ObservableCollection<Row>
        {
            new(1, "one"),
            new(2, "two"),
            new(3, "three")
        };
        var reconciler = new DataGridGeneratedSnapshotReconciler<Row, int>(new RowKey());

        DataGridGeneratedSnapshotMetrics metrics = reconciler.Reconcile(
            target,
            new[] { new Row(3, "three"), new Row(1, "updated"), new Row(4, "four") },
            revision: 8);

        Assert.Equal(new[] { 3, 1, 4 }, target.Select(static row => row.Id));
        Assert.Equal("updated", target[1].Name);
        Assert.Equal(1, metrics.Added);
        Assert.Equal(1, metrics.Removed);
        Assert.Equal(1, metrics.Moved);
        Assert.Equal(1, metrics.Replaced);
        Assert.False(metrics.IsStale);
    }

    [Fact]
    public void Reconcile_rejects_stale_revision_without_mutation()
    {
        var target = new ObservableCollection<Row> { new(1, "one") };
        var reconciler = new DataGridGeneratedSnapshotReconciler<Row, int>(new RowKey());
        reconciler.Reconcile(target, new[] { new Row(2, "two") }, revision: 5);

        DataGridGeneratedSnapshotMetrics metrics = reconciler.Reconcile(
            target,
            new[] { new Row(3, "three") },
            revision: 4);

        Assert.True(metrics.IsStale);
        Assert.Single(target);
        Assert.Equal(2, target[0].Id);
    }

    [Fact]
    public void Duplicate_snapshot_keys_are_rejected_before_target_changes()
    {
        var target = new ObservableCollection<Row> { new(1, "one") };
        var reconciler = new DataGridGeneratedSnapshotReconciler<Row, int>(new RowKey());

        Assert.Throws<InvalidOperationException>(() => reconciler.Reconcile(
            target,
            new[] { new Row(2, "two"), new Row(2, "duplicate") },
            revision: 1));
        Assert.Single(target);
        Assert.Equal(1, target[0].Id);
    }

    private sealed record Row(int Id, string Name);

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }
}
