// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Avalonia.Interactivity;

namespace Avalonia.Controls
{
    internal class DataGridSelectedItemsCollection : IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs s_countPropertyChanged =
            new(nameof(Count));
        private static readonly PropertyChangedEventArgs s_indexerPropertyChanged =
            new("Item[]");
        private List<object> _oldSelectedItemsCache;
        private List<object> _selectedItemsCache;
        private SortedSet<int> _selectedSlots;

        public DataGridSelectedItemsCollection(DataGrid owningGrid)
        {
            OwningGrid = owningGrid;
            _oldSelectedItemsCache = new List<object>();
            _selectedItemsCache = new List<object>();
            _selectedSlots = new SortedSet<int>();
        }

        public object this[int index]
        {
            get
            {
                int count = OwningGrid.UseCachedSelectedItemsDuringSourceMutation
                    ? _selectedItemsCache.Count
                    : _selectedSlots.Count;
                if (index < 0 || index >= count)
                {
                    throw DataGridError.DataGrid.ValueMustBeBetween("index", "Index", 0, true, count, false);
                }
                if (OwningGrid.UseCachedSelectedItemsDuringSourceMutation)
                {
                    return _selectedItemsCache[index];
                }
                int slot = GetNthSlot(index);
                Debug.Assert(slot > -1);
                return OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(slot));
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public int Add(object dataItem)
        {
            using var _ = OwningGrid.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);

            if (OwningGrid.SelectionMode == DataGridSelectionMode.Single)
            {
                throw DataGridError.DataGridSelectedItemsCollection.CannotChangeSelectedItemsCollectionInSingleMode();
            }

            int itemIndex = OwningGrid.DataConnection.IndexOf(dataItem);
            if (itemIndex == -1)
            {
                throw DataGridError.DataGrid.ItemIsNotContainedInTheItemsSource("dataItem");
            }
            Debug.Assert(itemIndex >= 0);

            int slot = OwningGrid.SlotFromRowIndex(itemIndex);
            if (_selectedSlots.Count == 0)
            {
                OwningGrid.SelectedItem = dataItem;
            }
            else
            {
                if (!OwningGrid.TryPreviewSetRowSelection(slot, isSelected: true, setAnchorSlot: false))
                {
                    return -1;
                }

                using var commit = OwningGrid.BeginSelectionCommit();
                OwningGrid.SetRowSelection(slot, true /*isSelected*/, false /*setAnchorSlot*/);
            }
            return IndexOf(dataItem);
        }

        public void Clear()
        {
            using var _ = OwningGrid.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);

            if (OwningGrid.SelectionMode == DataGridSelectionMode.Single)
            {
                throw DataGridError.DataGridSelectedItemsCollection.CannotChangeSelectedItemsCollectionInSingleMode();
            }

            if (_selectedSlots.Count > 0)
            {
                if (!OwningGrid.TryPreviewClearRowSelection(resetAnchorSlot: true))
                {
                    return;
                }

                // Clearing the selection does not reset the potential current cell.
                if (!OwningGrid.CommitEdit(DataGridEditingUnit.Row, true /*exitEditing*/))
                {
                    // Edited value couldn't be committed or aborted
                    return;
                }
                using var commit = OwningGrid.BeginSelectionCommit();
                OwningGrid.ClearRowSelection(true /*resetAnchorSlot*/);
            }
        }

        public bool Contains(object dataItem)
        {
            int itemIndex = OwningGrid.DataConnection.IndexOf(dataItem);
            if (itemIndex == -1)
            {
                return false;
            }
            Debug.Assert(itemIndex >= 0);

            return ContainsSlot(OwningGrid.SlotFromRowIndex(itemIndex));
        }

        public int IndexOf(object dataItem)
        {
            int itemIndex = OwningGrid.DataConnection.IndexOf(dataItem);
            if (itemIndex == -1)
            {
                return -1;
            }
            Debug.Assert(itemIndex >= 0);
            int slot = OwningGrid.SlotFromRowIndex(itemIndex);
            if (_selectedSlots.Contains(slot))
            {
                return _selectedSlots.TakeWhile(s => s != slot).Count();
            }

            return -1;
        }

        public void Insert(int index, object dataItem)
        {
            throw new NotSupportedException();
        }

        public void Remove(object dataItem)
        {
            using var _ = OwningGrid.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);

            if (OwningGrid.SelectionMode == DataGridSelectionMode.Single)
            {
                throw DataGridError.DataGridSelectedItemsCollection.CannotChangeSelectedItemsCollectionInSingleMode();
            }

            int itemIndex = OwningGrid.DataConnection.IndexOf(dataItem);
            if (itemIndex == -1)
            {
                return;
            }
            Debug.Assert(itemIndex >= 0);

            int slot = OwningGrid.SlotFromRowIndex(itemIndex);
            if (!OwningGrid.TryPreviewSetRowSelection(slot, isSelected: false, setAnchorSlot: false))
            {
                return;
            }

            if (slot == OwningGrid.CurrentSlot &&
                !OwningGrid.CommitEdit(DataGridEditingUnit.Row, true /*exitEditing*/))
            {
                // Edited value couldn't be committed or aborted
                return;
            }

            using var commit = OwningGrid.BeginSelectionCommit();
            OwningGrid.SetRowSelection(slot, false /*isSelected*/, false /*setAnchorSlot*/);
        }

        public void RemoveAt(int index)
        {
            using var _ = OwningGrid.BeginSelectionChangeScope(DataGridSelectionChangeSource.Programmatic);

            if (OwningGrid.SelectionMode == DataGridSelectionMode.Single)
            {
                throw DataGridError.DataGridSelectedItemsCollection.CannotChangeSelectedItemsCollectionInSingleMode();
            }

            if (index < 0 || index >= _selectedSlots.Count)
            {
                throw DataGridError.DataGrid.ValueMustBeBetween("index", "Index", 0, true, _selectedSlots.Count, false);
            }
            int rowIndex = GetNthSlot(index);
            Debug.Assert(rowIndex > -1);

            if (!OwningGrid.TryPreviewSetRowSelection(rowIndex, isSelected: false, setAnchorSlot: false))
            {
                return;
            }

            if (rowIndex == OwningGrid.CurrentSlot &&
                !OwningGrid.CommitEdit(DataGridEditingUnit.Row, true /*exitEditing*/))
            {
                // Edited value couldn't be committed or aborted
                return;
            }

            using var commit = OwningGrid.BeginSelectionCommit();
            OwningGrid.SetRowSelection(rowIndex, false /*isSelected*/, false /*setAnchorSlot*/);
        }

        public int Count
        {
            get
            {
                return OwningGrid.UseCachedSelectedItemsDuringSourceMutation
                    ? _selectedItemsCache.Count
                    : _selectedSlots.Count;
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public object SyncRoot
        {
            get
            {
                return this;
            }
        }

        public void CopyTo(System.Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            Debug.Assert(OwningGrid != null);
            Debug.Assert(OwningGrid.DataConnection != null);
            Debug.Assert(_selectedSlots != null);

            if (OwningGrid.UseCachedSelectedItemsDuringSourceMutation)
            {
                for (int i = 0; i < _selectedItemsCache.Count; i++)
                {
                    yield return _selectedItemsCache[i];
                }
                yield break;
            }

            foreach (int slot in _selectedSlots)
            {
                int rowIndex = OwningGrid.RowIndexFromSlot(slot);
                Debug.Assert(rowIndex > -1);
                yield return OwningGrid.DataConnection.GetDataItem(rowIndex);
            }
        }

        internal DataGrid OwningGrid
        {
            get;
            private set;
        }

        internal List<object> SelectedItemsCache
        {
            get
            {
                return _selectedItemsCache;
            }
            set
            {
                _selectedItemsCache = value ?? new List<object>();
                UpdateIndexes();
            }
        }

        internal void ClearRows()
        {
            _selectedSlots.Clear();
            _selectedItemsCache.Clear();
        }

        internal bool ContainsSlot(int slot)
        {
            return _selectedSlots.Contains(slot);
        }

        internal bool ContainsAll(int startSlot, int endSlot)
        {
            if (startSlot > endSlot)
            {
                return true;
            }

            for (int slot = startSlot; slot <= endSlot; slot++)
            {
                if (!OwningGrid.IsGroupSlot(slot) && !_selectedSlots.Contains(slot))
                {
                    return false;
                }
            }

            return true;
        }

        // Called when an item is deleted from the ItemsSource as opposed to just being unselected
        internal void Delete(int slot, object item)
        {
            if (_selectedSlots.Contains(slot))
            {
                OwningGrid.SelectionHasChanged = true;
                DeleteSlot(slot);
                RemoveMatchingItem(_selectedItemsCache, item);
            }
        }

        internal void DeleteSlot(int slot)
        {
            _selectedSlots.Remove(slot);
            RemoveMatchingItem(
                _oldSelectedItemsCache,
                OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(slot)));
        }

        // Returns the inclusive index count between lowerBound and upperBound of all indexes with the given value
        internal int GetIndexCount(int lowerBound, int upperBound)
        {
            return _selectedSlots.Count(slot => slot >= lowerBound && slot <= upperBound);
        }

        internal IEnumerable<int> GetIndexes()
        {
            return _selectedSlots;
        }

        internal IEnumerable<int> GetSlots(int startSlot)
        {
            foreach (int slot in _selectedSlots)
            {
                if (slot >= startSlot)
                {
                    yield return slot;
                }
            }
        }

        internal SelectionChangedEventArgs GetSelectionChangedEventArgs(
            DataGridSelectionChangeSource source = DataGridSelectionChangeSource.Unknown,
            RoutedEventArgs triggerEvent = null)
        {
            List<object> addedSelectedItems = new List<object>();
            List<object> removedSelectedItems = new List<object>();
            int previousCount = _oldSelectedItemsCache.Count;
            var oldItems = new OldItemsMatcher(_oldSelectedItemsCache);

            foreach (int newSlot in _selectedSlots)
            {
                object newItem = OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(newSlot));
                if (!oldItems.TryConsume(newItem))
                {
                    addedSelectedItems.Add(newItem);
                }
            }

            oldItems.AddRemaining(removedSelectedItems);

            _oldSelectedItemsCache = new List<object>(_selectedItemsCache);

            RaiseCollectionChanged(previousCount, addedSelectedItems, removedSelectedItems);

            var args = new DataGridSelectionChangedEventArgs(
                DataGrid.SelectionChangedEvent,
                removedSelectedItems,
                addedSelectedItems,
                source,
                triggerEvent);

            ((RoutedEventArgs)args).Source = OwningGrid;
            return args;
        }

        private sealed class OldItemsMatcher
        {
            private static readonly object NullKey = new object();
            private readonly List<object> _items;
            private readonly bool[] _consumed;
            private readonly int[] _nextOccurrence;
            private readonly Dictionary<object, int> _firstOccurrence;

            public OldItemsMatcher(List<object> items)
            {
                _items = items;
                _consumed = new bool[items.Count];
                _nextOccurrence = new int[items.Count];
                _firstOccurrence = new Dictionary<object, int>(items.Count, ReferenceEqualityComparer.Instance);
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    object key = items[i] ?? NullKey;
                    _nextOccurrence[i] = _firstOccurrence.TryGetValue(key, out int following) ? following : -1;
                    _firstOccurrence[key] = i;
                }
            }

            public bool TryConsume(object item)
            {
                object key = item ?? NullKey;
                if (_firstOccurrence.TryGetValue(key, out int index))
                {
                    while (index >= 0 && _consumed[index])
                    {
                        index = _nextOccurrence[index];
                    }

                    if (index >= 0)
                    {
                        _consumed[index] = true;
                        _firstOccurrence[key] = _nextOccurrence[index];
                        return true;
                    }
                }

                for (int i = 0; i < _items.Count; i++)
                {
                    if (!_consumed[i] && Equals(_items[i], item))
                    {
                        _consumed[i] = true;
                        return true;
                    }
                }

                return false;
            }

            public void AddRemaining(List<object> removed)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (!_consumed[i])
                    {
                        removed.Add(_items[i]);
                    }
                }
            }
        }

        private static int IndexOfMatchingItem(List<object> items, object item)
        {
            // Preserve occurrence identity when multiple rows compare equal. Equality remains
            // the fallback for reset projections that legitimately replace an item instance.
            for (int i = 0; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], item))
                {
                    return i;
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (Equals(items[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        private void RaiseCollectionChanged(int oldCount, List<object> addedItems, List<object> removedItems)
        {
            if (CollectionChanged != null)
            {
                if (removedItems.Count > 0)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Remove,
                        removedItems,
                        -1));
                }

                if (addedItems.Count > 0)
                {
                    CollectionChanged(this, new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Add,
                        addedItems,
                        -1));
                }
            }

            if (addedItems.Count > 0 || removedItems.Count > 0)
            {
                if (oldCount != _selectedItemsCache.Count)
                {
                    OnPropertyChanged(s_countPropertyChanged);
                }

                OnPropertyChanged(s_indexerPropertyChanged);
            }
        }

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        internal void InsertIndex(int slot)
        {
            if (_selectedSlots.Count == 0 || _selectedSlots.Max < slot)
            {
                return;
            }

            var updated = new List<int>(_selectedSlots.Count);
            foreach (var existing in _selectedSlots)
            {
                updated.Add(existing >= slot ? existing + 1 : existing);
            }

            _selectedSlots = new SortedSet<int>(updated);
        }

        internal void RemoveIndex(int slot, object item)
        {
            if (_selectedSlots.Count == 0 || _selectedSlots.Max < slot)
            {
                return;
            }

            if (_selectedSlots.Contains(slot) && item != null)
            {
                RemoveMatchingItem(_selectedItemsCache, item);
            }

            var updated = new List<int>(_selectedSlots.Count);
            foreach (var existing in _selectedSlots)
            {
                if (existing != slot)
                {
                    updated.Add(existing > slot ? existing - 1 : existing);
                }
            }

            _selectedSlots = new SortedSet<int>(updated);
        }

        internal bool HasSlotsInOrder(List<(int RowIndex, int Slot)> slots)
        {
            if (_selectedSlots.Count != slots.Count || _selectedItemsCache.Count != slots.Count)
            {
                return false;
            }

            int i = 0;
            foreach (int slot in _selectedSlots)
            {
                if (slots[i].Slot != slot ||
                    !ReferenceEquals(_selectedItemsCache[i], OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(slot))))
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        internal void SelectSlot(int slot, bool select)
        {
            if (OwningGrid.IsGroupSlot(slot))
            {
                return;
            }
            if (select)
            {
                if (_selectedSlots.Add(slot))
                {
                    _selectedItemsCache.Add(OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(slot)));
                }
            }
            else
            {
                if (_selectedSlots.Remove(slot))
                {
                    RemoveMatchingItem(
                        _selectedItemsCache,
                        OwningGrid.DataConnection.GetDataItem(OwningGrid.RowIndexFromSlot(slot)));
                }
            }
        }

        private static void RemoveMatchingItem(List<object> items, object item)
        {
            int index = IndexOfMatchingItem(items, item);
            if (index >= 0)
            {
                items.RemoveAt(index);
            }
        }

        internal void SelectSlots(int startSlot, int endSlot, bool select)
        {
            if (startSlot > endSlot)
            {
                return;
            }

            for (int slot = startSlot; slot <= endSlot; slot++)
            {
                if (!OwningGrid.IsGroupSlot(slot))
                {
                    SelectSlot(slot, select);
                }
            }
        }

        internal void UpdateIndexes()
        {
            _selectedSlots.Clear();
            _oldSelectedItemsCache.Clear();

            if (OwningGrid.DataConnection.DataSource == null)
            {
                if (_selectedItemsCache.Count > 0)
                {
                    OwningGrid.SelectionHasChanged = true;
                    _selectedItemsCache.Clear();
                }
                return;
            }

            List<object> tempSelectedItemsCache = new List<object>();
            foreach (object item in _selectedItemsCache)
            {
                int index = OwningGrid.DataConnection.IndexOf(item);
                if (index != -1)
                {
                    tempSelectedItemsCache.Add(item);
                    _selectedSlots.Add(OwningGrid.SlotFromRowIndex(index));
                }
                else
                {
                    OwningGrid.SelectionHasChanged = true;
                }
            }

            _selectedItemsCache = tempSelectedItemsCache;
            _oldSelectedItemsCache = new List<object>(_selectedItemsCache);
        }

        private int GetNthSlot(int index)
        {
            return _selectedSlots.ElementAt(index);
        }
    }
}
