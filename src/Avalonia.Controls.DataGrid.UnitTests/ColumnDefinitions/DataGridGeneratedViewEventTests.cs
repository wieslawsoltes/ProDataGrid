// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedViewEventTests
{
    [Fact]
    public void Event_flags_are_stable_and_all_contains_every_supported_event()
    {
        Assert.Equal(1, (int)DataGridGeneratedViewEventKinds.SelectionChanged);
        Assert.Equal(2, (int)DataGridGeneratedViewEventKinds.CurrentCellChanged);
        Assert.Equal(4, (int)DataGridGeneratedViewEventKinds.Sorting);
        Assert.Equal(8, (int)DataGridGeneratedViewEventKinds.BeginningEdit);
        Assert.Equal(16, (int)DataGridGeneratedViewEventKinds.CellEditEnding);
        Assert.Equal(32, (int)DataGridGeneratedViewEventKinds.CellEditEnded);
        Assert.Equal(64, (int)DataGridGeneratedViewEventKinds.RowEditEnding);
        Assert.Equal(128, (int)DataGridGeneratedViewEventKinds.RowEditEnded);
        Assert.Equal(255, (int)DataGridGeneratedViewEventKinds.All);
    }

    [Fact]
    public void Selection_snapshot_exposes_zero_copy_typed_item_lists()
    {
        var first = new Item(1);
        var second = new Item(2);
        var added = new ArrayList { first };
        var removed = new ArrayList { second };

        DataGridGeneratedViewEvent<Item> snapshot =
            DataGridGeneratedViewEvent<Item>.CreateSelectionChanged(
                added,
                removed,
                DataGridSelectionChangeSource.Pointer,
                isUserInitiated: true);
        added.Add(second);
        var sameProjection = new DataGridGeneratedItemList<Item>(added);
        var differentProjection = new DataGridGeneratedItemList<Item>(removed);

        Assert.Equal(DataGridGeneratedViewEventKinds.SelectionChanged, snapshot.Kind);
        Assert.Equal(2, snapshot.AddedItems.Count);
        Assert.Same(first, snapshot.AddedItems[0]);
        Assert.Same(second, snapshot.AddedItems[1]);
        Assert.Same(second, Assert.Single((IEnumerable<Item>)snapshot.RemovedItems));
        Assert.Equal(DataGridSelectionChangeSource.Pointer, snapshot.SelectionSource);
        Assert.True(snapshot.IsUserInitiated);
        Assert.Equal(snapshot.AddedItems, sameProjection);
        Assert.True(snapshot.AddedItems == sameProjection);
        Assert.True(snapshot.AddedItems != differentProjection);
    }

    [Fact]
    public void Current_cell_and_edit_snapshots_preserve_typed_context_and_feedback()
    {
        var oldItem = new Item(1);
        var newItem = new Item(2);
        DataGridGeneratedViewEvent<Item> current =
            DataGridGeneratedViewEvent<Item>.CreateCurrentCellChanged(
                oldItem,
                "old",
                newItem,
                "new");

        DataGridGeneratedViewEvent<Item> edit =
            DataGridGeneratedViewEvent<Item>.CreateEdit(
                DataGridGeneratedViewEventKinds.CellEditEnding,
                newItem,
                rowIndex: 7,
                columnKey: "price",
                DataGridEditAction.Commit,
                cancel: false);
        edit.Cancel = true;
        edit.Handled = true;

        Assert.Same(oldItem, current.OldItem);
        Assert.Equal("old", current.OldColumnKey);
        Assert.Same(newItem, current.NewItem);
        Assert.Equal("new", current.NewColumnKey);
        Assert.Equal(DataGridGeneratedViewEventKinds.CellEditEnding, edit.Kind);
        Assert.Same(newItem, edit.Item);
        Assert.Equal(7, edit.RowIndex);
        Assert.Equal("price", edit.ColumnKey);
        Assert.Equal(DataGridEditAction.Commit, edit.EditAction);
        Assert.True(edit.Cancel);
        Assert.True(edit.Handled);
    }

    [Fact]
    public void Edit_factory_rejects_non_edit_and_composite_kinds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DataGridGeneratedViewEvent<Item>.CreateEdit(
                DataGridGeneratedViewEventKinds.Sorting,
                new Item(1),
                0,
                "id",
                null,
                false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DataGridGeneratedViewEvent<Item>.CreateEdit(
                DataGridGeneratedViewEventKinds.Editing,
                new Item(1),
                0,
                "id",
                null,
                false));
    }

    private sealed record Item(int Id);
}
