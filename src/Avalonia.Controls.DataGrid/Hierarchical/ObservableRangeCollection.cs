// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Observable collection with basic range helpers for the flattened hierarchical view.
    /// </summary>
    internal sealed class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        public ObservableRangeCollection()
        {
        }

        public ObservableRangeCollection(IEnumerable<T> items)
            : base(items)
        {
        }

        public void AddRange(IEnumerable<T> items)
        {
            InsertRange(Count, items);
        }

        public void InsertRange(int index, IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var materialized = Materialize(items);
            if (materialized.Count == 0)
            {
                return;
            }

            CheckReentrancy();

            for (var i = 0; i < materialized.Count; i++)
            {
                Items.Insert(index + i, materialized[i]);
            }

            RaiseChange(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                materialized,
                index));
        }

        public IList<T> GetRange(int index, int count)
        {
            if (index < 0 || count < 0 || index + count > Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            var buffer = new List<T>(count);
            for (var i = 0; i < count; i++)
            {
                buffer.Add(Items[index + i]);
            }

            return buffer;
        }

        public void RemoveRange(int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (index < 0 || index + count > Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            CheckReentrancy();
            var removed = new List<T>(count);
            for (var i = 0; i < count; i++)
            {
                removed.Add(Items[index]);
                Items.RemoveAt(index);
            }

            RaiseChange(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removed,
                index));
        }

        public void ResetWith(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            CheckReentrancy();

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            RaiseReset();
        }

        private void RaiseChange(NotifyCollectionChangedEventArgs args)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(args);
        }

        private void RaiseReset()
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private static IList<T> Materialize(IEnumerable<T> items)
        {
            if (items is IList<T> list)
            {
                return list;
            }

            return items.ToList();
        }
    }
}
