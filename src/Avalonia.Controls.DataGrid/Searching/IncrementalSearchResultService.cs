// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Avalonia.Controls.DataGridSearching
{
    /// <summary>
    /// Maintains incremental search result state and applies queued collection/item deltas.
    /// </summary>
    internal sealed class IncrementalSearchResultService
    {
        internal delegate bool TryResolveRowIndexDelegate(object item, out int rowIndex);
        internal delegate bool TryRemapResetDelegate(
            IReadOnlyList<SearchResult> currentResults,
            out IReadOnlyList<SearchResult> remappedResults);

        private readonly bool _trackItemChanges;
        private readonly List<PendingCollectionChange> _pendingCollectionChanges = new();
        private readonly HashSet<object> _pendingItemChanges = new(ReferenceEqualityComparer.Instance);
        private SearchDescriptor[] _activeDescriptors = Array.Empty<SearchDescriptor>();
        private List<SearchResult> _activeResults;
        private bool _hasPendingReset;

        public IncrementalSearchResultService(bool trackItemChanges)
        {
            _trackItemChanges = trackItemChanges;
        }

        public void RecordCollectionChange(
            NotifyCollectionChangedEventArgs e,
            bool canRemapReset = false)
        {
            if (e == null || _activeResults == null)
            {
                return;
            }

            _pendingCollectionChanges.Add(PendingCollectionChange.From(e, canRemapReset));
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _hasPendingReset = true;
            }
        }

        public void RecordItemChange(object item)
        {
            if (!_trackItemChanges || item == null || _activeResults == null)
            {
                return;
            }

            _pendingItemChanges.Add(item);
        }

        public void SetActiveState(IReadOnlyList<SearchDescriptor> descriptors, IReadOnlyList<SearchResult> results)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                ClearState();
                return;
            }

            _activeDescriptors = descriptors.ToArray();
            _activeResults = results as List<SearchResult> ?? results?.ToList() ?? new List<SearchResult>();
            ClearPendingChanges();
        }

        public void ClearState()
        {
            _activeDescriptors = Array.Empty<SearchDescriptor>();
            _activeResults = null;
            ClearPendingChanges();
        }

        public bool TryApplyPendingChanges(
            IReadOnlyList<SearchDescriptor> descriptors,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults,
            TryResolveRowIndexDelegate tryResolveRowIndex,
            TryRemapResetDelegate tryRemapReset,
            out IReadOnlyList<SearchResult> results)
        {
            if (buildRowResults == null)
            {
                throw new ArgumentNullException(nameof(buildRowResults));
            }

            if (tryResolveRowIndex == null)
            {
                throw new ArgumentNullException(nameof(tryResolveRowIndex));
            }

            if (tryRemapReset == null)
            {
                throw new ArgumentNullException(nameof(tryRemapReset));
            }

            results = null;
            if (descriptors == null || descriptors.Count == 0)
            {
                ClearState();
                results = Array.Empty<SearchResult>();
                return true;
            }

            if (_activeResults == null || !DescriptorSetsEqual(descriptors, _activeDescriptors))
            {
                ClearPendingChanges();
                return false;
            }

            var hasPendingItemChanges = _trackItemChanges && _pendingItemChanges.Count > 0;
            if (_pendingCollectionChanges.Count == 0 && !hasPendingItemChanges)
            {
                results = _activeResults;
                return true;
            }

            if (_hasPendingReset)
            {
                if (_pendingCollectionChanges.Count == 1 &&
                    _pendingCollectionChanges[0].CanRemapReset &&
                    tryRemapReset(_activeResults, out IReadOnlyList<SearchResult> remappedResults))
                {
                    _activeResults = remappedResults as List<SearchResult> ?? remappedResults.ToList();
                    ClearPendingChanges();
                    results = _activeResults;
                    return true;
                }

                ClearPendingChanges();
                return false;
            }

            for (int i = 0; i < _pendingCollectionChanges.Count; i++)
            {
                if (!ApplyCollectionChange(_pendingCollectionChanges[i], buildRowResults))
                {
                    ClearPendingChanges();
                    return false;
                }
            }

            if (hasPendingItemChanges)
            {
                foreach (var item in _pendingItemChanges)
                {
                    if (!ApplyItemChange(item, buildRowResults, tryResolveRowIndex))
                    {
                        ClearPendingChanges();
                        return false;
                    }
                }
            }

            ClearPendingChanges();
            results = _activeResults;
            return true;
        }

        private bool ApplyCollectionChange(
            PendingCollectionChange change,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults)
        {
            switch (change.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    return ApplyAddChange(change, buildRowResults);
                case NotifyCollectionChangedAction.Remove:
                    return ApplyRemoveChange(change);
                case NotifyCollectionChangedAction.Replace:
                    return ApplyReplaceChange(change, buildRowResults);
                case NotifyCollectionChangedAction.Reset:
                    _hasPendingReset = true;
                    return false;
                case NotifyCollectionChangedAction.Move:
                default:
                    return false;
            }
        }

        private bool ApplyAddChange(
            PendingCollectionChange change,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults)
        {
            if (change.NewItems == null || change.NewItems.Length == 0)
            {
                return true;
            }

            if (change.NewStartingIndex < 0)
            {
                return false;
            }

            ShiftResultRowIndexes(change.NewStartingIndex, change.NewItems.Length);
            var added = BuildResultsForItems(change.NewItems, change.NewStartingIndex, buildRowResults);
            InsertResults(change.NewStartingIndex, added);
            return true;
        }

        private bool ApplyRemoveChange(PendingCollectionChange change)
        {
            var oldCount = change.OldItems?.Length ?? 0;
            if (oldCount == 0)
            {
                return true;
            }

            if (change.OldStartingIndex < 0)
            {
                return false;
            }

            RemoveResultsForRowRange(change.OldStartingIndex, oldCount);
            ShiftResultRowIndexes(change.OldStartingIndex + oldCount, -oldCount);
            return true;
        }

        private bool ApplyReplaceChange(
            PendingCollectionChange change,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults)
        {
            var oldCount = change.OldItems?.Length ?? 0;
            var newCount = change.NewItems?.Length ?? 0;
            if (oldCount == 0 && newCount == 0)
            {
                return true;
            }

            var baseIndex = change.NewStartingIndex >= 0 ? change.NewStartingIndex : change.OldStartingIndex;
            if (baseIndex < 0)
            {
                return false;
            }

            if (change.NewStartingIndex >= 0 &&
                change.OldStartingIndex >= 0 &&
                change.NewStartingIndex != change.OldStartingIndex)
            {
                return false;
            }

            RemoveResultsForRowRange(baseIndex, oldCount);
            var delta = newCount - oldCount;
            if (delta != 0)
            {
                ShiftResultRowIndexes(baseIndex + oldCount, delta);
            }

            if (newCount > 0)
            {
                var added = BuildResultsForItems(change.NewItems, baseIndex, buildRowResults);
                InsertResults(baseIndex, added);
            }

            return true;
        }

        private bool ApplyItemChange(
            object item,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults,
            TryResolveRowIndexDelegate tryResolveRowIndex)
        {
            if (!tryResolveRowIndex(item, out var rowIndex))
            {
                return false;
            }

            if (rowIndex < 0)
            {
                return true;
            }

            RemoveResultsForRowRange(rowIndex, 1);
            var updated = buildRowResults(item, rowIndex);
            InsertResults(rowIndex, updated);
            return true;
        }

        private List<SearchResult> BuildResultsForItems(
            object[] items,
            int startRowIndex,
            Func<object, int, IReadOnlyList<SearchResult>> buildRowResults)
        {
            var results = new List<SearchResult>();
            if (items == null || items.Length == 0)
            {
                return results;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var rowResults = buildRowResults(items[i], startRowIndex + i);
                if (rowResults == null || rowResults.Count == 0)
                {
                    continue;
                }

                if (rowResults is List<SearchResult> list)
                {
                    results.AddRange(list);
                    continue;
                }

                for (int j = 0; j < rowResults.Count; j++)
                {
                    results.Add(rowResults[j]);
                }
            }

            return results;
        }

        private void RemoveResultsForRowRange(int startRow, int count)
        {
            if (count <= 0 || _activeResults == null || _activeResults.Count == 0)
            {
                return;
            }

            var start = FindFirstResultIndexAtOrAfter(startRow);
            var end = FindFirstResultIndexAtOrAfter(startRow + count);
            var length = end - start;
            if (length > 0)
            {
                _activeResults.RemoveRange(start, length);
            }
        }

        private void ShiftResultRowIndexes(int startRow, int delta)
        {
            if (delta == 0 || _activeResults == null || _activeResults.Count == 0)
            {
                return;
            }

            var start = FindFirstResultIndexAtOrAfter(startRow);
            for (int i = start; i < _activeResults.Count; i++)
            {
                var current = _activeResults[i];
                _activeResults[i] = new SearchResult(
                    current.Item,
                    current.RowIndex + delta,
                    current.ColumnId,
                    current.ColumnIndex,
                    current.Text,
                    current.Matches);
            }
        }

        private void InsertResults(int startRow, IReadOnlyList<SearchResult> results)
        {
            if (results == null || results.Count == 0 || _activeResults == null)
            {
                return;
            }

            var insertIndex = FindFirstResultIndexAtOrAfter(startRow);
            if (results is List<SearchResult> list)
            {
                _activeResults.InsertRange(insertIndex, list);
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                _activeResults.Insert(insertIndex + i, results[i]);
            }
        }

        private int FindFirstResultIndexAtOrAfter(int rowIndex)
        {
            if (_activeResults == null || _activeResults.Count == 0)
            {
                return 0;
            }

            int low = 0;
            int high = _activeResults.Count;
            while (low < high)
            {
                var mid = low + ((high - low) / 2);
                if (_activeResults[mid].RowIndex < rowIndex)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private static bool DescriptorSetsEqual(IReadOnlyList<SearchDescriptor> descriptors, IReadOnlyList<SearchDescriptor> snapshot)
        {
            if (ReferenceEquals(descriptors, snapshot))
            {
                return true;
            }

            if (descriptors == null || snapshot == null || descriptors.Count != snapshot.Count)
            {
                return false;
            }

            for (int i = 0; i < descriptors.Count; i++)
            {
                if (!Equals(descriptors[i], snapshot[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearPendingChanges()
        {
            _pendingCollectionChanges.Clear();
            _pendingItemChanges.Clear();
            _hasPendingReset = false;
        }

        private sealed class PendingCollectionChange
        {
            public PendingCollectionChange(
                NotifyCollectionChangedAction action,
                int newStartingIndex,
                int oldStartingIndex,
                object[] newItems,
                object[] oldItems,
                bool canRemapReset)
            {
                Action = action;
                NewStartingIndex = newStartingIndex;
                OldStartingIndex = oldStartingIndex;
                NewItems = newItems;
                OldItems = oldItems;
                CanRemapReset = canRemapReset;
            }

            public NotifyCollectionChangedAction Action { get; }

            public int NewStartingIndex { get; }

            public int OldStartingIndex { get; }

            public object[] NewItems { get; }

            public object[] OldItems { get; }

            public bool CanRemapReset { get; }

            public static PendingCollectionChange From(
                NotifyCollectionChangedEventArgs e,
                bool canRemapReset)
            {
                return new PendingCollectionChange(
                    e.Action,
                    e.NewStartingIndex,
                    e.OldStartingIndex,
                    ToArray(e.NewItems),
                    ToArray(e.OldItems),
                    canRemapReset);
            }

            private static object[] ToArray(IList items)
            {
                if (items == null || items.Count == 0)
                {
                    return Array.Empty<object>();
                }

                var array = new object[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    array[i] = items[i];
                }

                return array;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
