// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Avalonia.Controls
{
    /// <summary>Reports the result of one keyed snapshot reconciliation.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedSnapshotMetrics : IEquatable<DataGridGeneratedSnapshotMetrics>
    {
        internal DataGridGeneratedSnapshotMetrics(long revision, int added, int removed, int moved, int replaced, bool stale)
        {
            Revision = revision;
            Added = added;
            Removed = removed;
            Moved = moved;
            Replaced = replaced;
            IsStale = stale;
        }

        /// <summary>Gets the snapshot revision.</summary>
        public long Revision { get; }
        /// <summary>Gets the number of inserted items.</summary>
        public int Added { get; }
        /// <summary>Gets the number of removed items.</summary>
        public int Removed { get; }
        /// <summary>Gets the number of reordered items.</summary>
        public int Moved { get; }
        /// <summary>Gets the number of updated items.</summary>
        public int Replaced { get; }
        /// <summary>Gets whether the snapshot was ignored as stale.</summary>
        public bool IsStale { get; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedSnapshotMetrics other) =>
            Revision == other.Revision && Added == other.Added && Removed == other.Removed &&
            Moved == other.Moved && Replaced == other.Replaced && IsStale == other.IsStale;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridGeneratedSnapshotMetrics other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Revision, Added, Removed, Moved, Replaced, IsStale);

        /// <summary>Compares two metric values.</summary>
        public static bool operator ==(DataGridGeneratedSnapshotMetrics left, DataGridGeneratedSnapshotMetrics right) => left.Equals(right);

        /// <summary>Compares two metric values.</summary>
        public static bool operator !=(DataGridGeneratedSnapshotMetrics left, DataGridGeneratedSnapshotMetrics right) => !left.Equals(right);
    }

    /// <summary>
    /// Reconciles complete snapshots into an existing list by stable key without clearing the collection.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSnapshotReconciler<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private readonly IEqualityComparer<TItem> _itemComparer;
        private readonly Dictionary<TKey, int> _incomingIndexes;
        private readonly Dictionary<TKey, int> _currentIndexes;
        private long _lastRevision = -1;

        /// <summary>Initializes a keyed snapshot reconciler.</summary>
        public DataGridGeneratedSnapshotReconciler(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            IEqualityComparer<TKey> keyComparer = null,
            IEqualityComparer<TItem> itemComparer = null)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _itemComparer = itemComparer ?? EqualityComparer<TItem>.Default;
            _incomingIndexes = new Dictionary<TKey, int>(_keyComparer);
            _currentIndexes = new Dictionary<TKey, int>(_keyComparer);
        }

        /// <summary>Gets the latest accepted snapshot revision.</summary>
        public long LastRevision => _lastRevision;

        /// <summary>
        /// Applies additions, removals, moves, and replacements needed to match a complete snapshot.
        /// </summary>
        public DataGridGeneratedSnapshotMetrics Reconcile(
            IList<TItem> target,
            IReadOnlyList<TItem> snapshot,
            long revision)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            if (revision <= _lastRevision)
            {
                return new DataGridGeneratedSnapshotMetrics(revision, 0, 0, 0, 0, true);
            }

            BuildUniqueIndex(snapshot, _incomingIndexes, "snapshot");
            BuildUniqueTargetIndex(target);
            int removed = 0;
            for (int index = target.Count - 1; index >= 0; index--)
            {
                TKey key = _keyAccessor.GetKey(target[index]);
                if (!_incomingIndexes.ContainsKey(key))
                {
                    target.RemoveAt(index);
                    removed++;
                }
            }

            int added = 0;
            int moved = 0;
            int replaced = 0;
            RebuildCurrentIndexes(target);
            for (int desiredIndex = 0; desiredIndex < snapshot.Count; desiredIndex++)
            {
                TItem desired = snapshot[desiredIndex];
                TKey desiredKey = _keyAccessor.GetKey(desired);
                if (!_currentIndexes.TryGetValue(desiredKey, out int currentIndex))
                {
                    target.Insert(desiredIndex, desired);
                    added++;
                    ReindexRange(target, desiredIndex, target.Count - 1);
                    continue;
                }

                if (currentIndex != desiredIndex)
                {
                    Move(target, currentIndex, desiredIndex);
                    moved++;
                    ReindexRange(target, Math.Min(currentIndex, desiredIndex), Math.Max(currentIndex, desiredIndex));
                }

                if (!_itemComparer.Equals(target[desiredIndex], desired))
                {
                    target[desiredIndex] = desired;
                    replaced++;
                }
            }

            _lastRevision = revision;
            return new DataGridGeneratedSnapshotMetrics(revision, added, removed, moved, replaced, false);
        }

        private void BuildUniqueIndex(IReadOnlyList<TItem> items, Dictionary<TKey, int> destination, string sourceName)
        {
            destination.Clear();
            for (int index = 0; index < items.Count; index++)
            {
                TKey key = _keyAccessor.GetKey(items[index]);
                if (key == null || !destination.TryAdd(key, index))
                {
                    throw new InvalidOperationException("Generated " + sourceName + " contains a null or duplicate stable key.");
                }
            }
        }

        private void RebuildCurrentIndexes(IList<TItem> items)
        {
            _currentIndexes.Clear();
            for (int index = 0; index < items.Count; index++)
            {
                _currentIndexes.Add(_keyAccessor.GetKey(items[index]), index);
            }
        }

        private void BuildUniqueTargetIndex(IList<TItem> items)
        {
            _currentIndexes.Clear();
            for (int index = 0; index < items.Count; index++)
            {
                TKey key = _keyAccessor.GetKey(items[index]);
                if (key == null || !_currentIndexes.TryAdd(key, index))
                {
                    throw new InvalidOperationException("Generated target contains a null or duplicate stable key.");
                }
            }
        }

        private void ReindexRange(IList<TItem> items, int start, int end)
        {
            for (int index = start; index <= end; index++)
            {
                _currentIndexes[_keyAccessor.GetKey(items[index])] = index;
            }
        }

        private static void Move(IList<TItem> target, int oldIndex, int newIndex)
        {
            if (target is ObservableCollection<TItem> observable)
            {
                observable.Move(oldIndex, newIndex);
                return;
            }

            TItem item = target[oldIndex];
            target.RemoveAt(oldIndex);
            target.Insert(newIndex, item);
        }
    }
}
