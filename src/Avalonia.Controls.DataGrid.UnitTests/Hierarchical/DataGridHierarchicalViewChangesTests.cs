// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class DataGridHierarchicalViewChangesTests
{
    [Fact]
    public void Descending_Removals_Become_One_Removed_Range()
    {
        var changes = new DataGridHierarchicalViewChanges();

        for (var index = 6; index >= 3; index--)
        {
            changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, "row", index));
        }

        AssertChange(changes, 3, 4, 0);
    }

    [Fact]
    public void Ascending_Insertions_Become_One_Inserted_Range()
    {
        var changes = new DataGridHierarchicalViewChanges();

        for (var index = 2; index < 5; index++)
        {
            changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", index));
        }

        AssertChange(changes, 2, 0, 3);
    }

    [Fact]
    public void Removing_And_Reinserting_The_Same_Rows_Is_A_Replacement()
    {
        var changes = new DataGridHierarchicalViewChanges();

        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, "row", 5));
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, "row", 4));
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", 4));
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", 5));

        AssertChange(changes, 4, 2, 2);
    }

    [Fact]
    public void An_Insertion_Removed_Again_Changes_No_Rows()
    {
        var changes = new DataGridHierarchicalViewChanges();

        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", 3));
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, "row", 3));

        Assert.Empty(changes.ToChanges());
        Assert.False(changes.RequiresRefresh);
    }

    [Theory]
    [InlineData(NotifyCollectionChangedAction.Reset)]
    [InlineData(NotifyCollectionChangedAction.Move)]
    [InlineData(NotifyCollectionChangedAction.Replace)]
    public void Other_Notifications_Require_A_Refresh_Until_Cleared(NotifyCollectionChangedAction action)
    {
        var changes = new DataGridHierarchicalViewChanges();
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", 0));

        changes.Record(action switch
        {
            NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(action, "row", 1, 0),
            NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(action, "new", "old", 0),
            _ => new NotifyCollectionChangedEventArgs(action),
        });
        changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, "row", 0));

        Assert.True(changes.RequiresRefresh);

        changes.Clear();

        Assert.False(changes.RequiresRefresh);
        Assert.Empty(changes.ToChanges());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Any_Notification_Sequence_Becomes_A_Change_That_Keeps_The_Untouched_Rows(int seed)
    {
        var random = new Random(seed);
        var original = Enumerable.Range(0, 20).Select(i => "old " + i).ToList();
        var rows = original.ToList();
        var changes = new DataGridHierarchicalViewChanges();

        for (var step = 0; step < 10; step++)
        {
            if (rows.Count > 0 && random.Next(2) == 0)
            {
                var index = random.Next(rows.Count);
                var row = rows[index];
                rows.RemoveAt(index);
                changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, row, index));
            }
            else
            {
                var index = random.Next(rows.Count + 1);
                var row = "new " + step;
                rows.Insert(index, row);
                changes.Record(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row, index));
            }
        }

        var recorded = changes.ToChanges();
        var change = recorded.Count == 0 ? new FlattenedChange(0, 0, 0) : Assert.Single(recorded);
        Assert.Equal(original.Count - change.OldCount + change.NewCount, rows.Count);
        Assert.Equal(original.Take(change.Index), rows.Take(change.Index));
        Assert.Equal(original.Skip(change.Index + change.OldCount), rows.Skip(change.Index + change.NewCount));
    }

    private static void AssertChange(DataGridHierarchicalViewChanges changes, int index, int oldCount, int newCount)
    {
        var change = Assert.Single(changes.ToChanges());
        Assert.Equal(index, change.Index);
        Assert.Equal(oldCount, change.OldCount);
        Assert.Equal(newCount, change.NewCount);
        Assert.True(changes.Matches(new[] { new FlattenedChange(index, oldCount, newCount) }));
        Assert.False(changes.RequiresRefresh);
    }
}
