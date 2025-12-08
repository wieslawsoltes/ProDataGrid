// (c) ProDataGrid selection model scaffolding
// IList bridge over ISelectionModel for binding compatibility.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls.Selection;
using System.Linq;

namespace Avalonia.Controls.DataGridSelection
{
    public class SelectedItemsView : IList, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly ISelectionModel _model;
        private readonly List<object> _pending = new();
        private int _suppressNotifications;

        public SelectedItemsView(ISelectionModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _model.SelectionChanged += OnSelectionChanged;
            _model.SourceReset += (_, __) => RaiseReset();
            _model.PropertyChanged += OnModelPropertyChanged;
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public object this[int index]
        {
            get
            {
                if (!HasSource)
                {
                    if (index < 0 || index >= _pending.Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return _pending[index];
                }

                var items = _model.SelectedItems;
                if (index < 0 || index >= items.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return items[index];
            }
            set => throw new NotSupportedException();
        }

        public bool IsReadOnly => false;

        public bool IsFixedSize => false;

        public int Count => HasSource ? _model.SelectedItems.Count : _pending.Count;

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public int Add(object value)
        {
            if (!HasSource)
            {
                if (_model.SingleSelect)
                {
                    _pending.Clear();
                }

                _pending.Add(value);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value));
                RaisePropertyChanges();
                return _pending.Count - 1;
            }

            int index = ResolveIndex(value);

            if (_model.SingleSelect)
            {
                _model.SelectedIndex = index;
            }
            else
            {
                _model.Select(index);
            }

            return IndexOf(value);
        }

        public void Clear()
        {
            if (!HasSource)
            {
                if (_pending.Count > 0)
                {
                    _pending.Clear();
                    RaiseReset();
                }
                return;
            }

            _model.Clear();
        }

        public bool Contains(object value)
        {
            if (!HasSource)
            {
                return _pending.Contains(value);
            }

            return IndexOf(value) != -1;
        }

        public int IndexOf(object value)
        {
            if (!HasSource)
            {
                return _pending.IndexOf(value);
            }

            var items = _model.SelectedItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (Equals(items[i], value))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, object value) => throw new NotSupportedException();

        public void Remove(object value)
        {
            if (!HasSource)
            {
                if (_pending.Remove(value))
                {
                    RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value));
                    RaisePropertyChanges();
                }
                return;
            }

            int index = ResolveIndex(value);
            if (_model.IsSelected(index))
            {
                _model.Deselect(index);
            }
        }

        public void RemoveAt(int index)
        {
            if (!HasSource)
            {
                if (index < 0 || index >= _pending.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                var value = _pending[index];
                _pending.RemoveAt(index);
                RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, value, index));
                RaisePropertyChanges();
                return;
            }

            var items = _model.SelectedItems;
            if (index < 0 || index >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var item = items[index];
            Remove(item);
        }

        public void CopyTo(Array array, int index)
        {
            if (array is null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            int i = index;
            foreach (var item in this)
            {
                array.SetValue(item, i++);
            }
        }

        public IEnumerator GetEnumerator()
        {
            return HasSource ? _model.SelectedItems.GetEnumerator() : _pending.GetEnumerator();
        }

        public IDisposable SuppressNotifications()
        {
            _suppressNotifications++;
            return new ActionDisposable(() => _suppressNotifications--);
        }

        private int ResolveIndex(object value)
        {
            if (_model.Source is IList list)
            {
                return list.IndexOf(value);
            }

            int i = 0;
            if (_model.Source is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (Equals(item, value))
                    {
                        return i;
                    }
                    i++;
                }
            }

            throw new ArgumentException("Item not found in selection model source.", nameof(value));
        }

        private void OnSelectionChanged(object sender, SelectionModelSelectionChangedEventArgs e)
        {
            if (_suppressNotifications > 0)
            {
                return;
            }

            RaiseDiff(SafeEnumerate(e.SelectedItems), SafeEnumerate(e.DeselectedItems));
        }

        private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ISelectionModel.Source))
            {
                ApplyPending();
            }
        }

        private bool HasSource => _model.Source != null;

        private void ApplyPending()
        {
            if (!HasSource || _pending.Count == 0)
            {
                return;
            }

            using (_model.BatchUpdate())
            {
                if (_model.SingleSelect)
                {
                    var last = _pending.Last();
                    _pending.Clear();
                    Add(last);
                    return;
                }

                foreach (var item in _pending.ToArray())
                {
                    int index = ResolveIndex(item);
                    _model.Select(index);
                }

                _pending.Clear();
            }
        }

        private void RaiseDiff(IEnumerable<object?>? addedItems, IEnumerable<object?>? removedItems)
        {
            var changed = false;

            try
            {

            if (removedItems != null)
            {
                var enumerator = removedItems.GetEnumerator();
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    var item = enumerator.Current;
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                    changed = true;
                }
            }

            if (addedItems != null)
            {
                var enumerator = addedItems.GetEnumerator();
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        break;
                    }

                    if (!moved)
                    {
                        break;
                    }

                    var item = enumerator.Current;
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
                    changed = true;
                }
            }

            if (changed)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
            }
            catch (ArgumentOutOfRangeException)
            {
                // The selection model emitted an index that doesn't currently map; skip the diff.
            }
        }

        private static IEnumerable<object?> SafeEnumerate(IEnumerable? items)
        {
            if (items == null)
            {
                yield break;
            }

            var enumerator = items.GetEnumerator();
            while (true)
            {
                bool moved;
                try
                {
                    moved = enumerator.MoveNext();
                }
                catch (ArgumentOutOfRangeException)
                {
                    yield break;
                }

                if (!moved)
                {
                    yield break;
                }

                object current;
                try
                {
                    current = enumerator.Current;
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                yield return current;
            }
        }

        private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
        }

        private void RaisePropertyChanges()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        private void RaiseReset()
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        private sealed class ActionDisposable : IDisposable
        {
            private Action _onDispose;

            public ActionDisposable(Action onDispose)
            {
                _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
                _onDispose = null;
            }
        }
    }
}
