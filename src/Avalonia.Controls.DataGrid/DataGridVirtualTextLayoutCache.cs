// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;
using Avalonia.Media.TextFormatting;

namespace Avalonia.Controls;

internal sealed class DataGridVirtualTextLayoutCache
{
    private readonly Dictionary<DataGridCustomDrawingTextLayoutCache.CacheKey, LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _lru = new();
    private readonly int _capacity;

    public DataGridVirtualTextLayoutCache(int capacity)
    {
        _capacity = capacity > 0 ? capacity : 1;
    }

    public bool TryGet(in DataGridCustomDrawingTextLayoutCache.CacheKey key, out DataGridVirtualTextLayout? layout)
    {
        if (_entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
        {
            _lru.Remove(node);
            _lru.AddFirst(node);
            layout = node.Value.Layout;
            return true;
        }

        layout = null;
        return false;
    }

    public DataGridVirtualTextLayout Add(in DataGridCustomDrawingTextLayoutCache.CacheKey key, TextLayout layout)
    {
        var cachedLayout = new DataGridVirtualTextLayout(layout);
        var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, cachedLayout));
        _lru.AddFirst(node);
        _entries.Add(key, node);
        TrimToCapacity();
        return cachedLayout;
    }

    public void Clear()
    {
        foreach (CacheEntry entry in _lru)
        {
            entry.Layout.Dispose();
        }

        _entries.Clear();
        _lru.Clear();
    }

    private void TrimToCapacity()
    {
        while (_entries.Count > _capacity && _lru.Last is { } node)
        {
            _entries.Remove(node.Value.Key);
            _lru.RemoveLast();
            node.Value.Layout.Dispose();
        }
    }

    private readonly record struct CacheEntry(
        DataGridCustomDrawingTextLayoutCache.CacheKey Key,
        DataGridVirtualTextLayout Layout);
}
