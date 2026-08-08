// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Avalonia.Controls
{
    /// <summary>
    /// Identifies DataGrid routed events that a generated view can forward to a ViewModel command.
    /// </summary>
    [Flags]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedViewEventKinds
    {
        /// <summary>No routed events are forwarded.</summary>
        None = 0,

        /// <summary>Forward selection changes.</summary>
        SelectionChanged = 1 << 0,

        /// <summary>Forward current-cell changes.</summary>
        CurrentCellChanged = 1 << 1,

        /// <summary>Forward column sorting requests.</summary>
        Sorting = 1 << 2,

        /// <summary>Forward cancellable beginning-edit events.</summary>
        BeginningEdit = 1 << 3,

        /// <summary>Forward cancellable cell-edit-ending events.</summary>
        CellEditEnding = 1 << 4,

        /// <summary>Forward cell-edit-ended events.</summary>
        CellEditEnded = 1 << 5,

        /// <summary>Forward cancellable row-edit-ending events.</summary>
        RowEditEnding = 1 << 6,

        /// <summary>Forward row-edit-ended events.</summary>
        RowEditEnded = 1 << 7,

        /// <summary>Forward all editing lifecycle events.</summary>
        Editing = BeginningEdit | CellEditEnding | CellEditEnded | RowEditEnding | RowEditEnded,

        /// <summary>Forward every supported generated view event.</summary>
        All = SelectionChanged | CurrentCellChanged | Sorting | Editing
    }

    /// <summary>
    /// Provides a zero-copy typed view over the item lists supplied by a selection event.
    /// </summary>
    /// <typeparam name="TItem">The generated grid item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedItemList<TItem> : IReadOnlyList<TItem>, IEquatable<DataGridGeneratedItemList<TItem>>
    {
        private readonly IList? _items;

        /// <summary>
        /// Initializes a typed list view over an existing non-generic item list.
        /// </summary>
        public DataGridGeneratedItemList(IList items)
        {
            _items = items;
        }

        /// <inheritdoc />
        public int Count => _items?.Count ?? 0;

        /// <inheritdoc />
        public TItem this[int index] => (TItem)_items![index]!;

        /// <summary>
        /// Returns an allocation-free enumerator when used directly in a <c>foreach</c> statement.
        /// </summary>
        public Enumerator GetEnumerator() => new(_items);

        IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public bool Equals(DataGridGeneratedItemList<TItem> other) => ReferenceEquals(_items, other._items);

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is DataGridGeneratedItemList<TItem> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => _items?.GetHashCode() ?? 0;

        /// <summary>Compares whether two projections wrap the same source list.</summary>
        public static bool operator ==(
            DataGridGeneratedItemList<TItem> left,
            DataGridGeneratedItemList<TItem> right) => left.Equals(right);

        /// <summary>Compares whether two projections wrap different source lists.</summary>
        public static bool operator !=(
            DataGridGeneratedItemList<TItem> left,
            DataGridGeneratedItemList<TItem> right) => !left.Equals(right);

        /// <summary>
        /// Enumerates a generated item-list projection.
        /// </summary>
        public struct Enumerator : IEnumerator<TItem>
        {
            private readonly IList? _items;
            private int _index;

            internal Enumerator(IList items)
            {
                _items = items;
                _index = -1;
            }

            /// <inheritdoc />
            public TItem Current => (TItem)_items![_index]!;

            object IEnumerator.Current => Current!;

            /// <inheritdoc />
            public bool MoveNext()
            {
                int next = _index + 1;
                if (_items == null || next >= _items.Count)
                {
                    return false;
                }

                _index = next;
                return true;
            }

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// Carries a typed, reflection-free snapshot from a generated DataGrid routed-event bridge.
    /// </summary>
    /// <typeparam name="TItem">The generated grid item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedViewEvent<TItem>
    {
        private DataGridGeneratedViewEvent(DataGridGeneratedViewEventKinds kind)
        {
            Kind = kind;
            ColumnKey = string.Empty;
            OldColumnKey = string.Empty;
            NewColumnKey = string.Empty;
            RowIndex = -1;
            SelectionSource = DataGridSelectionChangeSource.Unknown;
        }

        /// <summary>Gets the single routed-event kind represented by this snapshot.</summary>
        public DataGridGeneratedViewEventKinds Kind { get; }

        /// <summary>Gets the row item associated with an edit event.</summary>
        public TItem? Item { get; private set; }

        /// <summary>Gets the previous current-cell item.</summary>
        public TItem? OldItem { get; private set; }

        /// <summary>Gets the new current-cell item.</summary>
        public TItem? NewItem { get; private set; }

        /// <summary>Gets the items added by a selection change without copying the source list.</summary>
        public DataGridGeneratedItemList<TItem> AddedItems { get; private set; }

        /// <summary>Gets the items removed by a selection change without copying the source list.</summary>
        public DataGridGeneratedItemList<TItem> RemovedItems { get; private set; }

        /// <summary>Gets the stable column key associated with the event.</summary>
        public string ColumnKey { get; private set; }

        /// <summary>Gets the previous current-cell column key.</summary>
        public string OldColumnKey { get; private set; }

        /// <summary>Gets the new current-cell column key.</summary>
        public string NewColumnKey { get; private set; }

        /// <summary>Gets the realized row index for an edit event, or -1 when unavailable.</summary>
        public int RowIndex { get; private set; }

        /// <summary>Gets the edit action when the event belongs to the editing lifecycle.</summary>
        public DataGridEditAction? EditAction { get; private set; }

        /// <summary>Gets the origin of a selection change.</summary>
        public DataGridSelectionChangeSource SelectionSource { get; private set; }

        /// <summary>Gets a value indicating whether a selection change originated from user input.</summary>
        public bool IsUserInitiated { get; private set; }

        /// <summary>
        /// Gets or sets whether a cancellable edit event should be canceled. Generated views copy this
        /// value back after the command executes.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Gets or sets whether the originating routed event should be marked handled.
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>Creates a selection-change snapshot.</summary>
        public static DataGridGeneratedViewEvent<TItem> CreateSelectionChanged(
            IList addedItems,
            IList removedItems,
            DataGridSelectionChangeSource source,
            bool isUserInitiated)
        {
            return new DataGridGeneratedViewEvent<TItem>(DataGridGeneratedViewEventKinds.SelectionChanged)
            {
                AddedItems = new DataGridGeneratedItemList<TItem>(addedItems),
                RemovedItems = new DataGridGeneratedItemList<TItem>(removedItems),
                SelectionSource = source,
                IsUserInitiated = isUserInitiated
            };
        }

        /// <summary>Creates a current-cell-change snapshot.</summary>
        public static DataGridGeneratedViewEvent<TItem> CreateCurrentCellChanged(
            TItem oldItem,
            string oldColumnKey,
            TItem newItem,
            string newColumnKey)
        {
            return new DataGridGeneratedViewEvent<TItem>(DataGridGeneratedViewEventKinds.CurrentCellChanged)
            {
                OldItem = oldItem,
                OldColumnKey = oldColumnKey ?? string.Empty,
                NewItem = newItem,
                NewColumnKey = newColumnKey ?? string.Empty
            };
        }

        /// <summary>Creates a sorting-request snapshot.</summary>
        public static DataGridGeneratedViewEvent<TItem> CreateSorting(string columnKey)
        {
            return new DataGridGeneratedViewEvent<TItem>(DataGridGeneratedViewEventKinds.Sorting)
            {
                ColumnKey = columnKey ?? string.Empty
            };
        }

        /// <summary>Creates an editing-lifecycle snapshot.</summary>
        public static DataGridGeneratedViewEvent<TItem> CreateEdit(
            DataGridGeneratedViewEventKinds kind,
            TItem item,
            int rowIndex,
            string columnKey,
            DataGridEditAction? editAction,
            bool cancel)
        {
            const DataGridGeneratedViewEventKinds editKinds = DataGridGeneratedViewEventKinds.Editing;
            int kindValue = (int)kind;
            if (kind == DataGridGeneratedViewEventKinds.None ||
                (kind & ~editKinds) != 0 ||
                (kindValue & (kindValue - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new DataGridGeneratedViewEvent<TItem>(kind)
            {
                Item = item,
                RowIndex = rowIndex,
                ColumnKey = columnKey ?? string.Empty,
                EditAction = editAction,
                Cancel = cancel
            };
        }
    }
}
