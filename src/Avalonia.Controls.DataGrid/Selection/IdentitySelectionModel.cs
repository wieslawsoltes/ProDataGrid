// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls.Selection;
using Avalonia.Threading;

namespace Avalonia.Controls.DataGridSelection
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class IdentitySelectionModel : ISelectionModel
    {
        private readonly SelectionModel<object?> _inner = new();
        private readonly Func<object, object> _identitySelector;
        private INotifyCollectionChanged _sourceNotifications;
        private List<object> _selectionSnapshot = new();
        private bool _sourceMutationInProgress;
        private bool _suppressSnapshotUpdates;
        private int _sourceChangeVersion;

        public IdentitySelectionModel(Func<object, object> identitySelector)
        {
            _identitySelector = identitySelector ?? throw new ArgumentNullException(nameof(identitySelector));
            _inner.SelectionChanged += InnerSelectionChanged;
            _inner.IndexesChanged += (sender, args) => IndexesChanged?.Invoke(this, args);
            _inner.LostSelection += (sender, args) => LostSelection?.Invoke(this, args);
            _inner.SourceReset += (sender, args) => SourceReset?.Invoke(this, args);
            _inner.PropertyChanged += InnerPropertyChanged;
        }

        public IEnumerable Source
        {
            get => _inner.Source;
            set
            {
                if (ReferenceEquals(_inner.Source, value))
                {
                    return;
                }

                DetachSourceNotifications();
                AttachSourceNotifications(value as INotifyCollectionChanged);
                _inner.Source = value;
                UpdateSelectionSnapshot();
            }
        }

        public bool SingleSelect
        {
            get => _inner.SingleSelect;
            set => _inner.SingleSelect = value;
        }

        public int SelectedIndex
        {
            get => _inner.SelectedIndex;
            set => _inner.SelectedIndex = value;
        }

        public IReadOnlyList<int> SelectedIndexes => _inner.SelectedIndexes;

        public object SelectedItem
        {
            get => _inner.SelectedItem;
            set => _inner.SelectedItem = value;
        }

        public IReadOnlyList<object?> SelectedItems => _inner.SelectedItems;

        public int AnchorIndex
        {
            get => _inner.AnchorIndex;
            set => _inner.AnchorIndex = value;
        }

        public int Count => _inner.Count;

        public event EventHandler<SelectionModelIndexesChangedEventArgs> IndexesChanged;
        public event EventHandler<SelectionModelSelectionChangedEventArgs> SelectionChanged;
        public event EventHandler LostSelection;
        public event EventHandler SourceReset;
        public event PropertyChangedEventHandler PropertyChanged;

        public void BeginBatchUpdate() => _inner.BeginBatchUpdate();
        public void EndBatchUpdate() => _inner.EndBatchUpdate();
        public bool IsSelected(int index) => _inner.IsSelected(index);
        public void Select(int index) => _inner.Select(index);
        public void Deselect(int index) => _inner.Deselect(index);
        public void SelectRange(int start, int end) => _inner.SelectRange(start, end);
        public void DeselectRange(int start, int end) => _inner.DeselectRange(start, end);
        public void SelectAll() => _inner.SelectAll();
        public void Clear() => _inner.Clear();

        private void InnerSelectionChanged(object sender, SelectionModelSelectionChangedEventArgs e)
        {
            if (!_sourceMutationInProgress && !_suppressSnapshotUpdates)
            {
                UpdateSelectionSnapshot();
            }

            SelectionChanged?.Invoke(this, e);
        }

        private void InnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        private void AttachSourceNotifications(INotifyCollectionChanged source)
        {
            _sourceNotifications = source;
            if (_sourceNotifications != null)
            {
                _sourceNotifications.CollectionChanged += SourceOnCollectionChanged;
            }
        }

        private void DetachSourceNotifications()
        {
            if (_sourceNotifications != null)
            {
                _sourceNotifications.CollectionChanged -= SourceOnCollectionChanged;
                _sourceNotifications = null;
            }
        }

        private void SourceOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_selectionSnapshot.Count == 0)
            {
                return;
            }

            var snapshot = new List<object>(_selectionSnapshot);
            var version = ++_sourceChangeVersion;
            _sourceMutationInProgress = true;

            Dispatcher.UIThread.Post(() =>
            {
                if (version != _sourceChangeVersion)
                {
                    return;
                }

                _sourceMutationInProgress = false;
                RestoreSelectionSnapshot(snapshot);
            }, DispatcherPriority.Background);
        }

        private void RestoreSelectionSnapshot(IReadOnlyList<object> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0 || Source == null)
            {
                return;
            }

            if (SelectionMatchesSnapshot(snapshot))
            {
                return;
            }

            var indexes = FindIndexes(snapshot);
            if (indexes.Count == 0)
            {
                return;
            }

            _suppressSnapshotUpdates = true;
            try
            {
                using (_inner.BatchUpdate())
                {
                    _inner.Clear();
                    foreach (var index in indexes)
                    {
                        _inner.Select(index);
                    }
                }
            }
            finally
            {
                _suppressSnapshotUpdates = false;
            }

            UpdateSelectionSnapshot();
        }

        private bool SelectionMatchesSnapshot(IReadOnlyList<object> snapshot)
        {
            if (_inner.SelectedItems.Count != snapshot.Count)
            {
                return false;
            }

            if (snapshot.Count == 1)
            {
                return Equals(GetIdentity(snapshot[0]), GetIdentity(_inner.SelectedItems[0]));
            }

            var matched = new bool[_inner.SelectedItems.Count];
            foreach (var snapshotItem in snapshot)
            {
                var identity = GetIdentity(snapshotItem);
                var found = false;
                for (var i = 0; i < _inner.SelectedItems.Count; i++)
                {
                    if (matched[i])
                    {
                        continue;
                    }

                    if (!Equals(identity, GetIdentity(_inner.SelectedItems[i])))
                    {
                        continue;
                    }

                    matched[i] = true;
                    found = true;
                    break;
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private List<int> FindIndexes(IReadOnlyList<object> snapshot)
        {
            var indexes = new List<int>(snapshot.Count);
            if (Source is IList list)
            {
                foreach (var snapshotItem in snapshot)
                {
                    var identity = GetIdentity(snapshotItem);
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (!Equals(identity, GetIdentity(list[i])))
                        {
                            continue;
                        }

                        indexes.Add(i);
                        break;
                    }
                }

                return indexes;
            }

            foreach (var snapshotItem in snapshot)
            {
                var identity = GetIdentity(snapshotItem);
                var index = 0;
                foreach (var candidate in Source)
                {
                    if (Equals(identity, GetIdentity(candidate)))
                    {
                        indexes.Add(index);
                        break;
                    }

                    index++;
                }
            }

            return indexes;
        }

        private object GetIdentity(object item)
        {
            return item == null ? null : _identitySelector(item);
        }

        private void UpdateSelectionSnapshot()
        {
            _selectionSnapshot.Clear();
            foreach (var item in _inner.SelectedItems)
            {
                var identity = GetIdentity(item);
                if (identity != null)
                {
                    _selectionSnapshot.Add(identity);
                }
            }
        }
    }
}
