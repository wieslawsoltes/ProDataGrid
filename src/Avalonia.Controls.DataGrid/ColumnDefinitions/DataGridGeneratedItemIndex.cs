// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;

namespace Avalonia.Controls
{
    /// <summary>
    /// Maintains reflection-free key-to-item and key-to-index lookup for a mutable row sequence.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedItemIndex<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly IEqualityComparer<TKey> _comparer;
        private readonly Dictionary<TKey, Entry> _entries;
        private readonly List<TItem> _items;
        private readonly List<TKey> _keys;

        /// <summary>
        /// Initializes an empty index using a generated key accessor.
        /// </summary>
        public DataGridGeneratedItemIndex(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            IEqualityComparer<TKey> comparer = null,
            int capacity = 0)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new Dictionary<TKey, Entry>(capacity, _comparer);
            _items = new List<TItem>(capacity);
            _keys = new List<TKey>(capacity);
        }

        /// <summary>
        /// Gets the indexed items in current display/source order.
        /// </summary>
        public IReadOnlyList<TItem> Items => _items;

        /// <summary>
        /// Gets the number of indexed items.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets a monotonic version incremented after each successful mutation.
        /// </summary>
        public long Version { get; private set; }

        /// <summary>
        /// Replaces the complete indexed snapshot.
        /// </summary>
        public void Reset(IReadOnlyList<TItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var entries = new Dictionary<TKey, Entry>(items.Count, _comparer);
            var newItems = new List<TItem>(items.Count);
            var newKeys = new List<TKey>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                TItem item = items[index];
                TKey key = GetValidatedKey(item);
                if (!entries.TryAdd(key, new Entry(item, index)))
                {
                    throw CreateDuplicateKeyException(key);
                }

                newItems.Add(item);
                newKeys.Add(key);
            }

            _entries.Clear();
            foreach (KeyValuePair<TKey, Entry> pair in entries)
            {
                _entries.Add(pair.Key, pair.Value);
            }

            _items.Clear();
            _items.AddRange(newItems);
            _keys.Clear();
            _keys.AddRange(newKeys);
            Version++;
        }

        /// <summary>
        /// Inserts an item and updates subsequent indexes.
        /// </summary>
        public void Insert(int index, TItem item)
        {
            if ((uint)index > (uint)_items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            TKey key = GetValidatedKey(item);
            if (_entries.ContainsKey(key))
            {
                throw CreateDuplicateKeyException(key);
            }

            _items.Insert(index, item);
            _keys.Insert(index, key);
            UpdateEntries(index);
            Version++;
        }

        /// <summary>
        /// Removes the item at an index and updates subsequent indexes.
        /// </summary>
        public TItem RemoveAt(int index)
        {
            ValidateExistingIndex(index);
            TItem item = _items[index];
            TKey key = _keys[index];
            _entries.Remove(key);
            _items.RemoveAt(index);
            _keys.RemoveAt(index);
            UpdateEntries(index);
            Version++;
            return item;
        }

        /// <summary>
        /// Moves an item to a new final index.
        /// </summary>
        public void Move(int oldIndex, int newIndex)
        {
            ValidateExistingIndex(oldIndex);
            ValidateExistingIndex(newIndex);
            if (oldIndex == newIndex)
            {
                return;
            }

            TItem item = _items[oldIndex];
            TKey key = _keys[oldIndex];
            _items.RemoveAt(oldIndex);
            _keys.RemoveAt(oldIndex);
            _items.Insert(newIndex, item);
            _keys.Insert(newIndex, key);
            UpdateEntries(Math.Min(oldIndex, newIndex));
            Version++;
        }

        /// <summary>
        /// Replaces an item and updates its key atomically.
        /// </summary>
        public TItem Replace(int index, TItem item)
        {
            ValidateExistingIndex(index);
            TKey oldKey = _keys[index];
            TKey newKey = GetValidatedKey(item);
            if (!_comparer.Equals(oldKey, newKey) && _entries.ContainsKey(newKey))
            {
                throw CreateDuplicateKeyException(newKey);
            }

            TItem oldItem = _items[index];
            _entries.Remove(oldKey);
            _items[index] = item;
            _keys[index] = newKey;
            _entries[newKey] = new Entry(item, index);
            Version++;
            return oldItem;
        }

        /// <summary>
        /// Clears all indexed items.
        /// </summary>
        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _entries.Clear();
            _items.Clear();
            _keys.Clear();
            Version++;
        }

        /// <summary>
        /// Resolves an item by stable key.
        /// </summary>
        public bool TryGetItem(TKey key, out TItem item)
        {
            if (_entries.TryGetValue(key, out Entry entry))
            {
                item = entry.Item;
                return true;
            }

            item = default;
            return false;
        }

        /// <summary>
        /// Resolves the current item index by stable key.
        /// </summary>
        public bool TryGetIndex(TKey key, out int index)
        {
            if (_entries.TryGetValue(key, out Entry entry))
            {
                index = entry.Index;
                return true;
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// Gets the stable key captured for an indexed item.
        /// </summary>
        public TKey GetKeyAt(int index)
        {
            ValidateExistingIndex(index);
            return _keys[index];
        }

        private TKey GetValidatedKey(TItem item)
        {
            TKey key = _keyAccessor.GetKey(item);
            if (key == null)
            {
                throw new InvalidOperationException("Generated item keys cannot be null.");
            }

            return key;
        }

        private void UpdateEntries(int startIndex)
        {
            for (int index = startIndex; index < _items.Count; index++)
            {
                _entries[_keys[index]] = new Entry(_items[index], index);
            }
        }

        private void ValidateExistingIndex(int index)
        {
            if ((uint)index >= (uint)_items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static InvalidOperationException CreateDuplicateKeyException(TKey key) =>
            new InvalidOperationException("Duplicate generated item key '" + key + "'.");

        private readonly struct Entry
        {
            public Entry(TItem item, int index)
            {
                Item = item;
                Index = index;
            }

            public TItem Item { get; }

            public int Index { get; }
        }
    }
}
