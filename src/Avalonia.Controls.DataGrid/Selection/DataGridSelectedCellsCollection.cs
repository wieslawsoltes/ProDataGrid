// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Avalonia.Controls
{
    /// <summary>
    /// Observable selected-cell storage with an atomic replace operation for source remaps.
    /// </summary>
    internal sealed class DataGridSelectedCellsCollection : IList<DataGridCellInfo>,
        INotifyCollectionChanged,
        INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs s_countPropertyChanged =
            new(nameof(Count));
        private static readonly PropertyChangedEventArgs s_indexerPropertyChanged =
            new("Item[]");
        private readonly List<DataGridCellInfo> _items = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public DataGridCellInfo this[int index]
        {
            get => _items[index];
            set
            {
                DataGridCellInfo oldItem = _items[index];
                _items[index] = value;
                PropertyChanged?.Invoke(this, s_indexerPropertyChanged);
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace,
                        value,
                        oldItem,
                        index));
            }
        }

        public void Add(DataGridCellInfo item)
        {
            int index = _items.Count;
            _items.Add(item);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(DataGridCellInfo item) => _items.Contains(item);

        public void CopyTo(DataGridCellInfo[] array, int arrayIndex) =>
            _items.CopyTo(array, arrayIndex);

        public IEnumerator<DataGridCellInfo> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(DataGridCellInfo item) => _items.IndexOf(item);

        public void Insert(int index, DataGridCellInfo item)
        {
            _items.Insert(index, item);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public bool Remove(DataGridCellInfo item)
        {
            int index = _items.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            DataGridCellInfo item = _items[index];
            _items.RemoveAt(index);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal bool ReplaceAll(IReadOnlyList<DataGridCellInfo> items)
        {
            if (!ReplaceAllSilently(items))
            {
                return false;
            }

            RaiseResetNotification();
            return true;
        }

        internal bool ReplaceAllSilently(IReadOnlyList<DataGridCellInfo> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (_items.Count == items.Count)
            {
                bool equal = true;
                for (int i = 0; i < items.Count; i++)
                {
                    DataGridCellInfo current = _items[i];
                    DataGridCellInfo replacement = items[i];
                    if (!current.Equals(replacement) ||
                        !HaveSameItemIdentity(current.Item, replacement.Item) ||
                        current.RowIndex != replacement.RowIndex ||
                        current.ColumnIndex != replacement.ColumnIndex ||
                        current.IsValid != replacement.IsValid)
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                {
                    return false;
                }
            }

            _items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
            }

            return true;
        }

        private static bool HaveSameItemIdentity(object first, object second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            Type firstType = first.GetType();
            return firstType.IsValueType && firstType == second.GetType() && first.Equals(second);
        }

        internal void RaiseResetNotification()
        {
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void RaiseCountAndIndexerChanged()
        {
            PropertyChanged?.Invoke(this, s_countPropertyChanged);
            PropertyChanged?.Invoke(this, s_indexerPropertyChanged);
        }
    }

    /// <summary>
    /// Observable selected-column storage with a silent replace used by coordinated source
    /// mutation transactions.
    /// </summary>
    internal sealed class DataGridSelectedColumnsCollection : IList<DataGridColumn>,
        INotifyCollectionChanged,
        INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs s_countPropertyChanged =
            new(nameof(Count));
        private static readonly PropertyChangedEventArgs s_indexerPropertyChanged =
            new("Item[]");
        private readonly List<DataGridColumn> _items = new();

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public DataGridColumn this[int index]
        {
            get => _items[index];
            set
            {
                DataGridColumn oldItem = _items[index];
                _items[index] = value;
                PropertyChanged?.Invoke(this, s_indexerPropertyChanged);
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace,
                        value,
                        oldItem,
                        index));
            }
        }

        public void Add(DataGridColumn item)
        {
            int index = _items.Count;
            _items.Add(item);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(DataGridColumn item) => _items.Contains(item);

        public void CopyTo(DataGridColumn[] array, int arrayIndex) =>
            _items.CopyTo(array, arrayIndex);

        public IEnumerator<DataGridColumn> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(DataGridColumn item) => _items.IndexOf(item);

        public void Insert(int index, DataGridColumn item)
        {
            _items.Insert(index, item);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public bool Remove(DataGridColumn item)
        {
            int index = _items.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            DataGridColumn item = _items[index];
            _items.RemoveAt(index);
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal bool ReplaceAllSilently(IReadOnlyList<DataGridColumn> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (_items.Count == items.Count)
            {
                bool equal = true;
                for (int i = 0; i < items.Count; i++)
                {
                    if (!ReferenceEquals(_items[i], items[i]))
                    {
                        equal = false;
                        break;
                    }
                }

                if (equal)
                {
                    return false;
                }
            }

            _items.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
            }

            return true;
        }

        internal void RaiseResetNotification()
        {
            RaiseCountAndIndexerChanged();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void RaiseCountAndIndexerChanged()
        {
            PropertyChanged?.Invoke(this, s_countPropertyChanged);
            PropertyChanged?.Invoke(this, s_indexerPropertyChanged);
        }
    }
}
