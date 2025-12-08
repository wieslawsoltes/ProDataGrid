// (c) ProDataGrid selection model scaffolding
// Exposes a paged DataGridCollectionView as a flat, unpaged enumerable for selection.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace Avalonia.Controls.DataGridSelection
{
    internal sealed class DataGridPagedSelectionSource : IReadOnlyList<object>, IList, INotifyCollectionChanged, IDisposable
    {
        private readonly DataGridCollectionView _view;
        private bool _disposed;
        private bool _suppressNextReset;

        public DataGridPagedSelectionSource(DataGridCollectionView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            if (_view is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += OnViewCollectionChanged;
            }
            _view.PageChanged += OnPageChanged;
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public int Count => _view.ItemCount;

        public object this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _view.GetGlobalItemAt(index);
            }
            set => throw new NotSupportedException();
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public IEnumerator<object> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return _view.GetGlobalItemAt(i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Add(object value) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(object value) => _view.GetGlobalIndexOf(value) >= 0;

        public int IndexOf(object value) => _view.GetGlobalIndexOf(value);

        public void Insert(int index, object value) => throw new NotSupportedException();

        public void Remove(object value) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            int i = index;
            foreach (var item in this)
            {
                array.SetValue(item, i++);
            }
        }

        private void OnPageChanged(object sender, EventArgs e)
        {
            // Page navigation on the view triggers a Reset notification even though the underlying
            // unpaged list is unchanged. Suppress that Reset so the selection model doesn't clear
            // persisted selections that live on other pages.
            _suppressNextReset = true;
        }

        private void OnViewCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset && _suppressNextReset)
            {
                _suppressNextReset = false;
                return;
            }

            _suppressNextReset = false;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_view is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged -= OnViewCollectionChanged;
            }
            _view.PageChanged -= OnPageChanged;

            _disposed = true;
        }
    }
}
