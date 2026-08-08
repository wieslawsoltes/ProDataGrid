// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls.DataGridSelection;

namespace Avalonia.Controls
{
    /// <summary>Identifies the producer of a generated selection change.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedSelectionOrigin
    {
        /// <summary>The origin is not known.</summary>
        Unknown,
        /// <summary>A pointer gesture changed selection.</summary>
        Pointer,
        /// <summary>A keyboard gesture changed selection.</summary>
        Keyboard,
        /// <summary>A binding changed selection.</summary>
        Binding,
        /// <summary>The Avalonia selection model changed selection.</summary>
        Model,
        /// <summary>Persisted state restored selection.</summary>
        Restore,
        /// <summary>Application code changed selection.</summary>
        Programmatic,
        /// <summary>A linked chart changed selection.</summary>
        Chart
    }

    /// <summary>Configures a generated keyed selection controller.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSelectionProfile
    {
        /// <summary>Gets or sets the DataGrid selection mode.</summary>
        public DataGridSelectionMode Mode { get; set; } = DataGridSelectionMode.Extended;

        /// <summary>Gets or sets the DataGrid selection unit.</summary>
        public DataGridSelectionUnit Unit { get; set; } = DataGridSelectionUnit.FullRow;

        /// <summary>Gets or sets whether keys not present in the current page/snapshot are retained.</summary>
        public bool PreserveUnloadedKeys { get; set; } = true;
    }

    /// <summary>Identifies a cell without retaining an item or column instance.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedCellKey<TKey> : IEquatable<DataGridGeneratedCellKey<TKey>>
    {
        /// <summary>Initializes a stable cell key.</summary>
        public DataGridGeneratedCellKey(TKey itemKey, string columnKey)
        {
            ItemKey = itemKey;
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
        }

        /// <summary>Gets the stable item key.</summary>
        public TKey ItemKey { get; }

        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedCellKey<TKey> other) =>
            EqualityComparer<TKey>.Default.Equals(ItemKey, other.ItemKey) &&
            string.Equals(ColumnKey, other.ColumnKey, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridGeneratedCellKey<TKey> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(ItemKey, StringComparer.Ordinal.GetHashCode(ColumnKey));

        /// <summary>Tests two cell keys for equality.</summary>
        public static bool operator ==(DataGridGeneratedCellKey<TKey> left, DataGridGeneratedCellKey<TKey> right) => left.Equals(right);

        /// <summary>Tests two cell keys for inequality.</summary>
        public static bool operator !=(DataGridGeneratedCellKey<TKey> left, DataGridGeneratedCellKey<TKey> right) => !left.Equals(right);
    }

    /// <summary>Represents an immutable keyed selection snapshot.</summary>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSelectionSnapshot<TKey>
    {
        internal DataGridGeneratedSelectionSnapshot(
            IReadOnlyList<TKey> selectedItemKeys,
            IReadOnlyList<string> selectedColumnKeys,
            IReadOnlyList<DataGridGeneratedCellKey<TKey>> selectedCells,
            bool hasCurrentCell,
            DataGridGeneratedCellKey<TKey> currentCell)
        {
            SelectedItemKeys = selectedItemKeys;
            SelectedColumnKeys = selectedColumnKeys;
            SelectedCells = selectedCells;
            HasCurrentCell = hasCurrentCell;
            CurrentCell = currentCell;
        }

        /// <summary>Gets selected stable item keys.</summary>
        public IReadOnlyList<TKey> SelectedItemKeys { get; }

        /// <summary>Gets selected stable column keys.</summary>
        public IReadOnlyList<string> SelectedColumnKeys { get; }

        /// <summary>Gets selected stable cell keys.</summary>
        public IReadOnlyList<DataGridGeneratedCellKey<TKey>> SelectedCells { get; }

        /// <summary>Gets whether a current cell is present.</summary>
        public bool HasCurrentCell { get; }

        /// <summary>Gets the current cell when <see cref="HasCurrentCell"/> is true.</summary>
        public DataGridGeneratedCellKey<TKey> CurrentCell { get; }
    }

    /// <summary>Reports a generated keyed selection change.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSelectionChangedEventArgs : EventArgs
    {
        internal DataGridGeneratedSelectionChangedEventArgs(DataGridGeneratedSelectionOrigin origin, long version)
        {
            Origin = origin;
            Version = version;
        }

        /// <summary>Gets the change producer.</summary>
        public DataGridGeneratedSelectionOrigin Origin { get; }

        /// <summary>Gets the monotonic selection version.</summary>
        public long Version { get; }
    }

    /// <summary>
    /// Maintains row, column, cell, and current-cell selection by stable keys and bridges it to Avalonia selection models.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable item key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSelectionController<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly IEqualityComparer<TKey> _keyComparer;
        private readonly DataGridGeneratedItemIndex<TItem, TKey> _index;
        private readonly List<TKey> _selectedItemKeys = new();
        private readonly HashSet<TKey> _selectedItemSet;
        private readonly List<string> _selectedColumnKeys = new();
        private readonly HashSet<string> _selectedColumnSet = new(StringComparer.Ordinal);
        private readonly List<DataGridGeneratedCellKey<TKey>> _selectedCells = new();
        private readonly HashSet<DataGridGeneratedCellKey<TKey>> _selectedCellSet;
        private bool _hasCurrentCell;
        private DataGridGeneratedCellKey<TKey> _currentCell;

        /// <summary>Initializes a generated selection controller.</summary>
        public DataGridGeneratedSelectionController(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            DataGridGeneratedSelectionProfile profile = null,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
            _index = new DataGridGeneratedItemIndex<TItem, TKey>(_keyAccessor, _keyComparer);
            _selectedItemSet = new HashSet<TKey>(_keyComparer);
            _selectedCellSet = new HashSet<DataGridGeneratedCellKey<TKey>>(
                new CellComparer(_keyComparer));
            Profile = profile ?? new DataGridGeneratedSelectionProfile();
        }

        /// <summary>Raised after a successful selection mutation.</summary>
        public event EventHandler<DataGridGeneratedSelectionChangedEventArgs> SelectionChanged;

        /// <summary>Gets the mutable profile applied by adapters.</summary>
        public DataGridGeneratedSelectionProfile Profile { get; }

        /// <summary>Gets the monotonic selection version.</summary>
        public long Version { get; private set; }

        /// <summary>Gets selected item keys in selection order.</summary>
        public IReadOnlyList<TKey> SelectedItemKeys => _selectedItemKeys;

        /// <summary>Gets selected column keys in selection order.</summary>
        public IReadOnlyList<string> SelectedColumnKeys => _selectedColumnKeys;

        /// <summary>Gets selected cell keys in selection order.</summary>
        public IReadOnlyList<DataGridGeneratedCellKey<TKey>> SelectedCells => _selectedCells;

        /// <summary>Gets currently loaded selected items without allocating when none are selected.</summary>
        public IReadOnlyList<TItem> GetSelectedItems()
        {
            var result = new List<TItem>(_selectedItemKeys.Count);
            for (int index = 0; index < _selectedItemKeys.Count; index++)
            {
                if (_index.TryGetItem(_selectedItemKeys[index], out TItem item))
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>Replaces the loaded source index while optionally preserving off-page selection.</summary>
        public void ResetSource(IReadOnlyList<TItem> items, DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Model)
        {
            _index.Reset(items ?? throw new ArgumentNullException(nameof(items)));
            if (!Profile.PreserveUnloadedKeys)
            {
                for (int index = _selectedItemKeys.Count - 1; index >= 0; index--)
                {
                    TKey key = _selectedItemKeys[index];
                    if (!_index.TryGetIndex(key, out _))
                    {
                        _selectedItemKeys.RemoveAt(index);
                        _selectedItemSet.Remove(key);
                    }
                }
            }
            Publish(origin);
        }

        /// <summary>Selects an item by stable key.</summary>
        public bool SelectKey(TKey key, DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            if (Profile.Mode == DataGridSelectionMode.Single && _selectedItemKeys.Count != 0)
            {
                if (_selectedItemKeys.Count == 1 && _keyComparer.Equals(_selectedItemKeys[0], key))
                {
                    return false;
                }
                _selectedItemKeys.Clear();
                _selectedItemSet.Clear();
            }
            if (!_selectedItemSet.Add(key))
            {
                return false;
            }
            _selectedItemKeys.Add(key);
            Publish(origin);
            return true;
        }

        /// <summary>Deselects an item by stable key.</summary>
        public bool DeselectKey(TKey key, DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            if (!_selectedItemSet.Remove(key))
            {
                return false;
            }
            RemoveKey(_selectedItemKeys, key);
            Publish(origin);
            return true;
        }

        /// <summary>Selects an inclusive range in the currently loaded source.</summary>
        public void SelectRange(int startIndex, int endIndex, DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            if ((uint)startIndex >= (uint)_index.Count || (uint)endIndex >= (uint)_index.Count)
            {
                throw new ArgumentOutOfRangeException(startIndex < 0 || startIndex >= _index.Count ? nameof(startIndex) : nameof(endIndex));
            }
            if (Profile.Mode == DataGridSelectionMode.Single)
            {
                SelectKey(_index.GetKeyAt(endIndex), origin);
                return;
            }
            int first = Math.Min(startIndex, endIndex);
            int last = Math.Max(startIndex, endIndex);
            bool changed = false;
            for (int index = first; index <= last; index++)
            {
                TKey key = _index.GetKeyAt(index);
                if (_selectedItemSet.Add(key))
                {
                    _selectedItemKeys.Add(key);
                    changed = true;
                }
            }
            if (changed)
            {
                Publish(origin);
            }
        }

        /// <summary>Selects a stable column key.</summary>
        public bool SelectColumn(string columnKey, DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            if (columnKey == null)
            {
                throw new ArgumentNullException(nameof(columnKey));
            }
            if (!_selectedColumnSet.Add(columnKey))
            {
                return false;
            }
            _selectedColumnKeys.Add(columnKey);
            Publish(origin);
            return true;
        }

        /// <summary>Selects a stable cell and optionally makes it current.</summary>
        public bool SelectCell(
            TKey itemKey,
            string columnKey,
            bool makeCurrent = true,
            DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            var cell = new DataGridGeneratedCellKey<TKey>(itemKey, columnKey);
            bool changed = _selectedCellSet.Add(cell);
            if (changed)
            {
                _selectedCells.Add(cell);
            }
            if (makeCurrent && (!_hasCurrentCell || !_currentCell.Equals(cell)))
            {
                _currentCell = cell;
                _hasCurrentCell = true;
                changed = true;
            }
            if (changed)
            {
                Publish(origin);
            }
            return changed;
        }

        /// <summary>Clears every generated selection dimension.</summary>
        public void Clear(DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Programmatic)
        {
            if (_selectedItemKeys.Count == 0 && _selectedColumnKeys.Count == 0 && _selectedCells.Count == 0 && !_hasCurrentCell)
            {
                return;
            }
            _selectedItemKeys.Clear();
            _selectedItemSet.Clear();
            _selectedColumnKeys.Clear();
            _selectedColumnSet.Clear();
            _selectedCells.Clear();
            _selectedCellSet.Clear();
            _hasCurrentCell = false;
            _currentCell = default;
            Publish(origin);
        }

        /// <summary>Captures a detached keyed snapshot.</summary>
        public DataGridGeneratedSelectionSnapshot<TKey> Capture() =>
            new(
                _selectedItemKeys.ToArray(),
                _selectedColumnKeys.ToArray(),
                _selectedCells.ToArray(),
                _hasCurrentCell,
                _currentCell);

        /// <summary>Restores a detached keyed snapshot.</summary>
        public void Restore(
            DataGridGeneratedSelectionSnapshot<TKey> snapshot,
            DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Restore)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            _selectedItemKeys.Clear();
            _selectedItemSet.Clear();
            AddUnique(snapshot.SelectedItemKeys, _selectedItemKeys, _selectedItemSet);
            _selectedColumnKeys.Clear();
            _selectedColumnSet.Clear();
            AddUnique(snapshot.SelectedColumnKeys, _selectedColumnKeys, _selectedColumnSet);
            _selectedCells.Clear();
            _selectedCellSet.Clear();
            AddUnique(snapshot.SelectedCells, _selectedCells, _selectedCellSet);
            _hasCurrentCell = snapshot.HasCurrentCell;
            _currentCell = snapshot.CurrentCell;
            Publish(origin);
        }

        /// <summary>Creates and initializes an identity-preserving Avalonia selection model.</summary>
        public IdentitySelectionModel CreateIdentitySelectionModel(IEnumerable source)
        {
            var model = new IdentitySelectionModel(item => _keyAccessor.GetKey((TItem)item));
            model.SingleSelect = Profile.Mode == DataGridSelectionMode.Single;
            model.Source = source;
            ApplyTo(model);
            return model;
        }

        /// <summary>Applies loaded keyed row selection to an Avalonia selection model.</summary>
        public void ApplyTo(IdentitySelectionModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            model.SupersedePendingIdentityRestore();
            model.BeginBatchUpdate();
            try
            {
                model.Clear();
                int sourceIndex = 0;
                if (model.Source != null)
                {
                    foreach (object candidate in model.Source)
                    {
                        if (candidate is TItem item && _selectedItemSet.Contains(_keyAccessor.GetKey(item)))
                        {
                            model.Select(sourceIndex);
                        }
                        sourceIndex++;
                    }
                }
            }
            finally
            {
                model.EndBatchUpdate();
            }
        }

        /// <summary>Imports current Avalonia row selection into the keyed controller.</summary>
        public void CaptureFrom(
            IdentitySelectionModel model,
            DataGridGeneratedSelectionOrigin origin = DataGridGeneratedSelectionOrigin.Model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }
            _selectedItemKeys.Clear();
            _selectedItemSet.Clear();
            foreach (object selected in model.SelectedItems)
            {
                if (selected is TItem item)
                {
                    TKey key = _keyAccessor.GetKey(item);
                    if (_selectedItemSet.Add(key))
                    {
                        _selectedItemKeys.Add(key);
                    }
                }
            }
            Publish(origin);
        }

        private void Publish(DataGridGeneratedSelectionOrigin origin)
        {
            Version++;
            SelectionChanged?.Invoke(this, new DataGridGeneratedSelectionChangedEventArgs(origin, Version));
        }

        private void RemoveKey(List<TKey> keys, TKey key)
        {
            for (int index = 0; index < keys.Count; index++)
            {
                if (_keyComparer.Equals(keys[index], key))
                {
                    keys.RemoveAt(index);
                    return;
                }
            }
        }

        private static void AddUnique<T>(IReadOnlyList<T> source, List<T> target, HashSet<T> set)
        {
            for (int index = 0; index < source.Count; index++)
            {
                T value = source[index];
                if (set.Add(value))
                {
                    target.Add(value);
                }
            }
        }

        private sealed class CellComparer : IEqualityComparer<DataGridGeneratedCellKey<TKey>>
        {
            private readonly IEqualityComparer<TKey> _comparer;

            public CellComparer(IEqualityComparer<TKey> comparer) => _comparer = comparer;

            public bool Equals(DataGridGeneratedCellKey<TKey> left, DataGridGeneratedCellKey<TKey> right) =>
                _comparer.Equals(left.ItemKey, right.ItemKey) &&
                string.Equals(left.ColumnKey, right.ColumnKey, StringComparison.Ordinal);

            public int GetHashCode(DataGridGeneratedCellKey<TKey> value) =>
                HashCode.Combine(_comparer.GetHashCode(value.ItemKey), StringComparer.Ordinal.GetHashCode(value.ColumnKey));
        }
    }
}
