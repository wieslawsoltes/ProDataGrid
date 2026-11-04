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

            var notifyItems = materialized as IList ?? materialized.ToList();
            RaiseChange(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                notifyItems,
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

            var notifyItems = (IList)removed;
            RaiseChange(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                notifyItems,
                index));
        }

        /// <summary>
        /// Replaces a contiguous range and exposes only the final collection state to observers.
        /// </summary>
        /// <param name="index">Index of the first item to replace.</param>
        /// <param name="count">Number of existing items to remove.</param>
        /// <param name="items">Replacement items.</param>
        public void ReplaceRange(int index, int count, IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (index < 0 || count < 0 || index + count > Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            var materialized = Materialize(items);
            if (ReferenceEquals(items, this))
            {
                // The mutation below must not change the replacement source while it is read.
                materialized = new List<T>(materialized);
            }
            if (count == 0 && materialized.Count == 0)
            {
                return;
            }

            CheckReentrancy();

            // A differing-size replacement raises Reset, whose event payload contains no old
            // items. Avoid snapshotting a potentially huge removed range that no observer can
            // consume. Remove and equal-size replace still require the old items in their event.
            var captureRemovedItems = count > 0 &&
                (materialized.Count == 0 || count == materialized.Count);
            List<T>? removed = captureRemovedItems ? new List<T>(count) : null;
            for (var i = 0; i < count && captureRemovedItems; i++)
            {
                removed!.Add(Items[index + i]);
            }

            if (Items is List<T> list)
            {
                if (count == materialized.Count)
                {
                    // Equal-size replacement does not need to move the stable suffix at all.
                    for (var i = 0; i < materialized.Count; i++)
                    {
                        list[index + i] = materialized[i];
                    }
                }
                else
                {
                    // List<T>'s range operations move the stable suffix at most once each. Repeated
                    // RemoveAt/Insert calls make a middle subtree expansion quadratic.
                    if (count > 0)
                    {
                        list.RemoveRange(index, count);
                    }

                    if (materialized.Count > 0)
                    {
                        list.InsertRange(index, materialized);
                    }
                }
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    Items.RemoveAt(index);
                }

                for (var i = 0; i < materialized.Count; i++)
                {
                    Items.Insert(index + i, materialized[i]);
                }
            }

            if (count == 0)
            {
                var addedItems = materialized as IList ?? materialized.ToList();
                RaiseChange(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    addedItems,
                    index));
            }
            else if (materialized.Count == 0)
            {
                RaiseChange(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    (IList)removed!,
                    index));
            }
            else if (count == materialized.Count)
            {
                var replacementItems = materialized as IList ?? materialized.ToList();
                RaiseReplace(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace,
                    replacementItems,
                    (IList)removed!,
                    index));
            }
            else
            {
                // NotifyCollectionChanged has no range-replace representation for differing
                // counts. A single reset keeps every observer on one coherent final snapshot.
                RaiseReset();
            }
        }

        public void ResetWith(IEnumerable<T> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            CheckReentrancy();

            Items.Clear();
            if (items is IList<T> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Items.Add(list[i]);
                }
            }
            else
            {
                foreach (var item in items)
                {
                    Items.Add(item);
                }
            }

            RaiseReset();
        }

        private void RaiseChange(NotifyCollectionChangedEventArgs args)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(args);
        }

        private void RaiseReplace(NotifyCollectionChangedEventArgs args)
        {
            // Match ObservableCollection<T>.SetItem: replacing items invalidates the indexer,
            // but the unchanged Count must not trigger avoidable collection-view/layout work.
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
