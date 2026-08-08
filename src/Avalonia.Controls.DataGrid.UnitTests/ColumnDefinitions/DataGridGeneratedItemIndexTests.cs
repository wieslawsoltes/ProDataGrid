// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedItemIndexTests
{
    private static readonly RowKey s_key = new();

    [Fact]
    public void Reset_builds_key_to_item_and_index_lookup()
    {
        var index = new DataGridGeneratedItemIndex<Row, int>(s_key);
        Row first = new(10, "First");
        Row second = new(20, "Second");

        index.Reset(new[] { first, second });

        Assert.Equal(2, index.Count);
        Assert.True(index.TryGetItem(20, out Row resolved));
        Assert.Same(second, resolved);
        Assert.True(index.TryGetIndex(10, out int firstIndex));
        Assert.Equal(0, firstIndex);
        Assert.Equal(20, index.GetKeyAt(1));
    }

    [Fact]
    public void Incremental_mutations_keep_indexes_and_captured_keys_consistent()
    {
        var index = new DataGridGeneratedItemIndex<Row, int>(s_key);
        Row first = new(1, "First");
        Row second = new(2, "Second");
        Row third = new(3, "Third");
        index.Reset(new[] { first, third });

        index.Insert(1, second);
        index.Move(2, 0);
        Row replacement = new(4, "Replacement");
        Row removed = index.Replace(1, replacement);

        Assert.Same(first, removed);
        Assert.Equal(new[] { third, replacement, second }, index.Items);
        Assert.True(index.TryGetIndex(3, out int thirdIndex));
        Assert.Equal(0, thirdIndex);
        Assert.True(index.TryGetIndex(4, out int replacementIndex));
        Assert.Equal(1, replacementIndex);
        Assert.False(index.TryGetIndex(1, out _));

        Assert.Same(replacement, index.RemoveAt(1));
        Assert.True(index.TryGetIndex(2, out int secondIndex));
        Assert.Equal(1, secondIndex);
    }

    [Fact]
    public void Stored_key_survives_item_key_mutation_until_replace()
    {
        var index = new DataGridGeneratedItemIndex<MutableRow, int>(new MutableRowKey());
        var row = new MutableRow { Id = 1 };
        index.Reset(new[] { row });

        row.Id = 2;

        Assert.True(index.TryGetItem(1, out MutableRow resolved));
        Assert.Same(row, resolved);
        Assert.False(index.TryGetItem(2, out _));
        index.Replace(0, row);
        Assert.False(index.TryGetItem(1, out _));
        Assert.True(index.TryGetItem(2, out _));
    }

    [Fact]
    public void Duplicate_keys_are_rejected_without_mutating_existing_snapshot()
    {
        var index = new DataGridGeneratedItemIndex<Row, int>(s_key);
        Row first = new(1, "First");
        index.Reset(new[] { first });

        Assert.Throws<InvalidOperationException>(() => index.Insert(1, new Row(1, "Duplicate")));
        Assert.Single(index.Items);
        Assert.Same(first, index.Items[0]);
    }

    private sealed record Row(int Id, string Name);

    private sealed class RowKey : IDataGridItemKey<Row, int>
    {
        public int GetKey(Row item) => item.Id;
    }

    private sealed class MutableRow
    {
        public int Id { get; set; }
    }

    private sealed class MutableRowKey : IDataGridItemKey<MutableRow, int>
    {
        public int GetKey(MutableRow item) => item.Id;
    }
}
