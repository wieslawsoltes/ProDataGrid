// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Utilities;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionIndexResolutionTests
{
    [AvaloniaFact]
    public void ReferenceIndexResolver_Takes_Precedence_Over_Fast_Interface()
    {
        var first = new object();
        var target = new object();
        var third = new object();
        var items = new FastTrackingObservableList { first, target, third };
        var grid = CreateGrid(items);

        grid.ReferenceIndexResolver = (_, item) =>
        {
            Assert.Same(target, item);
            return 1;
        };

        items.ResetCounters();
        var index = grid.DataConnection.IndexOf(target);

        Assert.Equal(1, index);
        Assert.Equal(0, items.FastLookupCallCount);
        Assert.InRange(items.IndexerGetCount, 1, 1);
    }

    [AvaloniaFact]
    public void Fast_Interface_Used_When_Resolver_Does_Not_Resolve()
    {
        var first = new object();
        var second = new object();
        var target = new object();
        var items = new FastTrackingObservableList { first, second, target };
        var grid = CreateGrid(items);

        grid.ReferenceIndexResolver = (_, _) => -1;

        items.ResetCounters();
        var index = grid.DataConnection.IndexOf(target);

        Assert.Equal(2, index);
        Assert.Equal(1, items.FastLookupCallCount);
        Assert.InRange(items.IndexerGetCount, 1, 1);
    }

    [AvaloniaFact]
    public void ReferenceIndexResolver_Falls_Back_When_Resolved_Index_Does_Not_Match_Current_View_Order()
    {
        var beta = new SortItem("Beta");
        var alpha = new SortItem("Alpha");
        var gamma = new SortItem("Gamma");
        var source = new ObservableCollection<SortItem> { beta, alpha, gamma };
        var sourceIndex = new Dictionary<object, int>(ReferenceEqualityComparer.Instance)
        {
            [beta] = 0,
            [alpha] = 1,
            [gamma] = 2
        };

        var view = new DataGridCollectionView(source);
        var grid = new DataGrid
        {
            ItemsSource = view,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            ReferenceIndexResolver = (_, item) => sourceIndex.TryGetValue(item, out var index) ? index : -1
        };

        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(SortItem.Name), ListSortDirection.Ascending));

        Assert.Equal(0, grid.DataConnection.IndexOf(alpha));
    }

    [AvaloniaFact]
    public void Default_Reference_Cache_Reuses_Lookup_And_Invalidates_On_Collection_Change()
    {
        var first = new object();
        var second = new object();
        var target = new object();
        var items = new TrackingObservableList { first, second, target };
        var grid = CreateGrid(items);
        var connection = grid.DataConnection;

        ForceDenseLookups(connection, items);
        var firstIndex = grid.DataConnection.IndexOf(target);
        var firstLookup = GetReferenceLookup(connection);

        var secondIndex = grid.DataConnection.IndexOf(target);
        var secondLookup = GetReferenceLookup(connection);

        Assert.Equal(2, firstIndex);
        Assert.Equal(2, secondIndex);
        Assert.NotNull(firstLookup);
        Assert.Same(firstLookup, secondLookup);

        items.Insert(0, new object());
        Assert.Null(GetReferenceLookup(connection));
        ForceDenseLookups(connection, items);
        var lookupAfterMutation = GetReferenceLookup(connection);
        Assert.NotNull(lookupAfterMutation);
        Assert.NotSame(firstLookup, lookupAfterMutation);

        var indexAfterInsert = grid.DataConnection.IndexOf(target);
        var lookupAfterInsert = GetReferenceLookup(connection);

        Assert.Equal(3, indexAfterInsert);
        Assert.NotNull(lookupAfterInsert);
        Assert.Same(lookupAfterMutation, lookupAfterInsert);
    }

    [AvaloniaFact]
    public void Duplicate_Reference_Items_Resolve_To_First_Occurrence_After_Mutations()
    {
        var a = new object();
        var duplicate = new object();
        var b = new object();
        var items = new TrackingObservableList { a, duplicate, b, duplicate };
        var grid = CreateGrid(items);

        var firstIndex = grid.DataConnection.IndexOf(duplicate);
        Assert.Equal(1, firstIndex);

        items.RemoveAt(1);
        items.ResetCounters();

        var secondIndex = grid.DataConnection.IndexOf(duplicate);
        Assert.Equal(2, secondIndex);
    }

    [AvaloniaFact]
    public void ReferenceIndexResolver_Exception_Is_Not_Swallowed()
    {
        var target = new object();
        var items = new TrackingObservableList { new object(), target };
        var grid = CreateGrid(items);

        grid.ReferenceIndexResolver = (_, _) => throw new InvalidOperationException("resolver failure");

        var exception = Assert.Throws<InvalidOperationException>(() => grid.DataConnection.IndexOf(target));
        Assert.Equal("resolver failure", exception.Message);
    }

    [AvaloniaFact]
    public void Default_Cache_Does_Not_Root_Previous_ItemsSource_After_Swap()
    {
        var (grid, weakOldSource) = CreateGridWithSwappedItemsSource();

        ForceGc();

        Assert.False(weakOldSource.TryGetTarget(out _));
        GC.KeepAlive(grid);
    }

    [AvaloniaFact]
    public void Default_Cache_Does_Not_Root_Previous_Lookup_After_Rebuild()
    {
        var (grid, weakLookup) = CreateGridWithRebuiltLookup();

        ForceGc();

        Assert.False(weakLookup.TryGetTarget(out _));
        GC.KeepAlive(grid);
    }

    private static DataGrid CreateGrid(TrackingObservableList items)
    {
        return new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };
    }

    private static (DataGrid Grid, WeakReference<TrackingObservableList> WeakOldSource) CreateGridWithSwappedItemsSource()
    {
        var oldSource = new TrackingObservableList { new object(), new object(), new object() };
        var target = oldSource[1];
        var grid = CreateGrid(oldSource);

        _ = grid.DataConnection.IndexOf(target);
        grid.ItemsSource = new TrackingObservableList { new object(), new object(), new object() };

        return (grid, new WeakReference<TrackingObservableList>(oldSource));
    }

    private static (DataGrid Grid, WeakReference<IDictionary> WeakLookup) CreateGridWithRebuiltLookup()
    {
        var target = new object();
        var items = new TrackingObservableList { new object(), target, new object() };
        var grid = CreateGrid(items);
        var connection = grid.DataConnection;

        ForceDenseLookups(connection, items);
        _ = connection.IndexOf(target);
        var firstLookup = GetReferenceLookup(connection);
        Assert.NotNull(firstLookup);

        items.Insert(0, new object());
        ForceDenseLookups(connection, items);
        _ = connection.IndexOf(target);

        var secondLookup = GetReferenceLookup(connection);
        Assert.NotNull(secondLookup);
        Assert.NotSame(firstLookup, secondLookup);

        return (grid, new WeakReference<IDictionary>(firstLookup!));
    }

    private static void ForceDenseLookups(DataGridDataConnection connection, TrackingObservableList items)
    {
        for (int i = 0; i < 32; i++)
        {
            _ = connection.IndexOf(items[i % items.Count]);
        }
    }

    private static void ForceGc()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static IDictionary? GetReferenceLookup(DataGridDataConnection connection)
    {
        var field = typeof(DataGridDataConnection).GetField("_referenceIndexLookup", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (IDictionary?)field!.GetValue(connection);
    }

    private class TrackingObservableList : IList, INotifyCollectionChanged
    {
        private readonly List<object> _items = new();

        public int IndexerGetCount { get; private set; }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        protected IList<object> Items => _items;

        public int Count => _items.Count;

        bool IList.IsReadOnly => false;

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public object this[int index]
        {
            get
            {
                IndexerGetCount++;
                return _items[index];
            }
            set
            {
                var oldItem = _items[index];
                _items[index] = value;
                CollectionChanged?.Invoke(
                    this,
                    new NotifyCollectionChangedEventArgs(
                        NotifyCollectionChangedAction.Replace,
                        value,
                        oldItem,
                        index));
            }
        }

        public int Add(object? value)
        {
            _items.Add(value!);
            var index = _items.Count - 1;
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    value,
                    index));
            return index;
        }

        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(object? value)
        {
            return _items.Contains(value!);
        }

        public int IndexOf(object? value)
        {
            return _items.IndexOf(value!);
        }

        public void Insert(int index, object? value)
        {
            _items.Insert(index, value!);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    value,
                    index));
        }

        public void Remove(object? value)
        {
            var index = _items.IndexOf(value!);
            if (index < 0)
            {
                return;
            }

            var removed = _items[index];
            _items.RemoveAt(index);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    removed,
                    index));
        }

        public void RemoveAt(int index)
        {
            var removed = _items[index];
            _items.RemoveAt(index);
            CollectionChanged?.Invoke(
                this,
                new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    removed,
                    index));
        }

        public void CopyTo(Array array, int index)
        {
            ((ICollection)_items).CopyTo(array, index);
        }

        public IEnumerator GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public void ResetCounters()
        {
            IndexerGetCount = 0;
        }
    }

    private sealed class FastTrackingObservableList : TrackingObservableList, IDataGridIndexOf
    {
        public int FastLookupCallCount { get; private set; }

        public bool TryGetReferenceIndex(object item, out int index)
        {
            FastLookupCallCount++;

            for (var i = 0; i < Items.Count; i++)
            {
                if (ReferenceEquals(Items[i], item))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public new void ResetCounters()
        {
            base.ResetCounters();
            FastLookupCallCount = 0;
        }
    }

    private sealed class SortItem
    {
        public SortItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
