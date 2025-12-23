// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
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
    public class SelectedItemsView : IList, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
        private readonly ISelectionModel _model;
        private readonly List<object> _pending = new();
        private readonly Func<object?, object?> _itemSelector;
        private readonly Func<object?, int>? _indexResolver;
        private int _suppressNotifications;
        private bool _isDisposed;

        public SelectedItemsView(ISelectionModel model)
            : this(model, null, null)
        {
        }

        public SelectedItemsView(
            ISelectionModel model,
            Func<object?, object?>? itemSelector,
            Func<object?, int>? indexResolver)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _itemSelector = itemSelector ?? (item => item);
            _indexResolver = indexResolver;
            _model.SelectionChanged += OnSelectionChanged;
            _model.SourceReset += OnSourceReset;
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

                    return ProjectItem(_pending[index]);
                }

                var items = _model.SelectedItems;
                if (index < 0 || index >= items.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return ProjectItem(items[index]);
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
                    if (_pending.Count > 0)
                    {
                        var removed = _pending.ToArray();
                        _pending.Clear();
                        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, 0));
                    }
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
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (Equals(ProjectItem(_pending[i]), value))
                    {
                        return true;
                    }
                }
                return false;
            }

            return IndexOf(value) != -1;
        }

        public int IndexOf(object value)
        {
            if (!HasSource)
            {
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (Equals(ProjectItem(_pending[i]), value))
                    {
                        return i;
                    }
                }
                return -1;
            }

            var items = _model.SelectedItems;
            for (int i = 0; i < items.Count; i++)
            {
                if (Equals(ProjectItem(items[i]), value))
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
            return HasSource ? ProjectItems(_model.SelectedItems).GetEnumerator() : ProjectItems(_pending).GetEnumerator();
        }

        public IDisposable SuppressNotifications()
        {
            _suppressNotifications++;
            return new ActionDisposable(() => _suppressNotifications--);
        }

        private int ResolveIndex(object value)
        {
            if (_indexResolver != null)
            {
                var resolved = _indexResolver(value);
                if (resolved >= 0)
                {
                    return resolved;
                }

                throw new ArgumentException("Item not found in selection model source.", nameof(value));
            }

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

        private object? ProjectItem(object? item) => _itemSelector(item);

        private IEnumerable<object?> ProjectItems(IEnumerable? items)
        {
            foreach (var item in SafeEnumerate(items))
            {
                yield return ProjectItem(item);
            }
        }

        private void OnSelectionChanged(object sender, SelectionModelSelectionChangedEventArgs e)
        {
            if (_suppressNotifications > 0)
            {
                return;
            }

            RaiseDiff(ProjectItems(e.SelectedItems), ProjectItems(e.DeselectedItems));
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

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _model.SelectionChanged -= OnSelectionChanged;
            _model.PropertyChanged -= OnModelPropertyChanged;
            _model.SourceReset -= OnSourceReset;

            _isDisposed = true;
        }

        private void OnSourceReset(object sender, EventArgs e)
        {
            RaiseReset();
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
