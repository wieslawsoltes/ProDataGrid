using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using Avalonia.Collections;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Collections;

public class DataGridCollectionViewTests
{
    [Fact]
    public void Move_Reorders_View_And_Raises_Remove_Then_Add()
    {
        var items = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items.Move(1, 3);

        Assert.Equal(new[] { 1, 3, 4, 2 }, view.Cast<int>().ToArray());

        Assert.Collection(
            changes,
            e =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
                Assert.Equal(1, e.OldStartingIndex);
                var oldItems = Assert.IsAssignableFrom<IList>(e.OldItems);
                Assert.Equal(2, Assert.Single(oldItems.Cast<int>()));
            },
            e =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
                Assert.Equal(3, e.NewStartingIndex);
                var newItems = Assert.IsAssignableFrom<IList>(e.NewItems);
                Assert.Equal(2, Assert.Single(newItems.Cast<int>()));
            });
    }

    [Fact]
    public void Uses_SourceList_For_ObservableCollection_When_No_Local_Transforms()
    {
        var items = new ObservableCollection<int> { 1, 2, 3 };
        var view = new DataGridCollectionView(items);

        var internalList = GetInternalList(view);

        Assert.Same(items, internalList);
    }

    [Fact]
    public void Uses_Local_Array_When_Sorting_And_Reverts_When_Cleared()
    {
        var items = new ObservableCollection<int> { 2, 1 };
        var view = new DataGridCollectionView(items);

        view.SortDescriptions.Add(DataGridSortDescription.FromComparer(Comparer<int>.Default));

        var sortedInternal = GetInternalList(view);
        Assert.NotSame(items, sortedInternal);

        view.SortDescriptions.Clear();

        var restoredInternal = GetInternalList(view);
        Assert.Same(items, restoredInternal);
    }

    [Fact]
    public void Move_Preserves_Current_Item()
    {
        var items = new ObservableCollection<string> { "a", "b", "c", "d" };
        var view = new DataGridCollectionView(items);

        Assert.True(view.MoveCurrentTo("c"));
        Assert.Equal("c", view.CurrentItem);
        Assert.Equal(2, view.CurrentPosition);

        items.Move(2, 0);

        Assert.Equal("c", view.CurrentItem);
        Assert.Equal(0, view.CurrentPosition);
    }

    [Fact]
    public void Swap_With_Remove_And_Insert_Updates_View()
    {
        var items = new ObservableCollection<int> { 10, 20, 30, 40 };
        var view = new DataGridCollectionView(items);

        Swap(items, 1, 3);

        Assert.Equal(new[] { 10, 40, 30, 20 }, view.Cast<int>().ToArray());
    }

    [Fact]
    public void Move_Reapplies_Sorting()
    {
        var items = new ObservableCollection<Row>
        {
            new() { Value = 3 },
            new() { Value = 1 },
            new() { Value = 2 }
        };

        var view = new DataGridCollectionView(items);
        view.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Row.Value), ListSortDirection.Ascending));

        Assert.Equal(new[] { 1, 2, 3 }, view.Cast<Row>().Select(x => x.Value).ToArray());

        items.Move(0, 2);

        Assert.Equal(new[] { 1, 2, 3 }, view.Cast<Row>().Select(x => x.Value).ToArray());
    }

    [Fact]
    public void Add_Raises_Add_And_Updates_View()
    {
        var items = new ObservableCollection<int> { 1, 2 };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items.Add(3);

        Assert.Equal(new[] { 1, 2, 3 }, view.Cast<int>().ToArray());

        var add = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Add, add.Action);
        var newItems = Assert.IsAssignableFrom<IList>(add.NewItems);
        Assert.Equal(3, Assert.Single(newItems.Cast<int>()));
        Assert.Equal(2, add.NewStartingIndex);
    }

    [Fact]
    public void AddNew_Uses_PreAdd_Count_For_Index_When_Using_SourceList()
    {
        var items = new ObservableCollection<SimpleItem>
        {
            new() { Value = 1 },
            new() { Value = 2 }
        };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        var newItem = Assert.IsType<SimpleItem>(view.AddNew());

        Assert.Equal(3, view.Count);
        Assert.Same(newItem, items[2]);
        Assert.Equal(2, view.IndexOf(newItem));

        var add = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Add, add.Action);
        Assert.Equal(2, add.NewStartingIndex);

        view.CommitNew();
    }

    [Fact]
    public void Remove_Raises_Remove_And_Updates_View()
    {
        var items = new ObservableCollection<int> { 1, 2, 3 };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items.RemoveAt(1);

        Assert.Equal(new[] { 1, 3 }, view.Cast<int>().ToArray());

        var remove = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Remove, remove.Action);
        var oldItems = Assert.IsAssignableFrom<IList>(remove.OldItems);
        Assert.Equal(2, Assert.Single(oldItems.Cast<int>()));
        Assert.Equal(1, remove.OldStartingIndex);
    }

    [Fact]
    public void RemoveAt_Using_SourceList_Removes_Only_One_Duplicate()
    {
        var items = new ObservableCollection<int> { 1, 2, 2, 3 };
        var view = new DataGridCollectionView(items);

        view.RemoveAt(1);

        Assert.Equal(new[] { 1, 2, 3 }, items.ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, view.Cast<int>().ToArray());
    }

    [Fact]
    public void Remove_Filtered_Out_Item_Does_Not_Raise_Remove()
    {
        var items = new ObservableCollection<int> { 1, 2, 3, 4 };
        var view = new DataGridCollectionView(items)
        {
            Filter = item => (int)item % 2 == 0
        };

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items.Remove(1);

        Assert.Empty(changes);
        Assert.Equal(new[] { 2, 4 }, view.Cast<int>().ToArray());
    }

    [Fact]
    public void Replace_Raises_Remove_Then_Add_And_Updates_View()
    {
        var items = new ObservableCollection<int> { 1, 2, 3 };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items[1] = 5;

        Assert.Equal(new[] { 1, 5, 3 }, view.Cast<int>().ToArray());

        Assert.Collection(
            changes,
            e =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
                var oldItems = Assert.IsAssignableFrom<IList>(e.OldItems);
                Assert.Equal(2, Assert.Single(oldItems.Cast<int>()));
                Assert.Equal(1, e.OldStartingIndex);
            },
            e =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
                var newItems = Assert.IsAssignableFrom<IList>(e.NewItems);
                Assert.Equal(5, Assert.Single(newItems.Cast<int>()));
                Assert.Equal(1, e.NewStartingIndex);
            });
    }

    [Fact]
    public void Reset_Raises_Reset_And_Clears_View()
    {
        var items = new ObservableCollection<int> { 1, 2, 3 };
        var view = new DataGridCollectionView(items);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        items.Clear();

        Assert.Empty(view.Cast<int>());
        var reset = Assert.Single(changes);
        Assert.Equal(NotifyCollectionChangedAction.Reset, reset.Action);
    }

    private static void Swap<T>(ObservableCollection<T> items, int sourceIndex, int targetIndex)
    {
        var first = items[sourceIndex];
        var second = items[targetIndex];

        items.RemoveAt(sourceIndex);
        items.Insert(sourceIndex, second);

        items.RemoveAt(targetIndex);
        items.Insert(targetIndex, first);
    }

    private class Row
    {
        public int Value { get; set; }
    }

    private class SimpleItem
    {
        public int Value { get; set; }
    }

    private static IList GetInternalList(DataGridCollectionView view)
    {
        var field = typeof(DataGridCollectionView)
            .GetField("_internalList", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IList>(field!.GetValue(view));
    }

    [Fact]
    public void AddNew_On_BindingList_Adds_Row()
    {
        var table = new DataTable();
        table.Columns.Add(new DataColumn("Value", typeof(int)));
        var view = new DataGridCollectionView(table.DefaultView);

        Assert.Equal(0, table.Rows.Count);
        var newRow = Assert.IsType<DataRowView>(view.AddNew());
        Assert.Equal(0, table.Rows.Count);
        newRow["Value"] = 42;
        view.CommitNew();

        var values = table.Rows.Cast<DataRow>().Select(r => (int)r["Value"]).ToArray();

        Assert.Equal(new[] { 42 }, values);
        Assert.Equal(1, view.Count);
    }

    [Fact]
    public void BindingList_Add_Updates_View()
    {
        var table = new DataTable();
        table.Columns.Add(new DataColumn("Value", typeof(int)));
        var view = new DataGridCollectionView(table.DefaultView);

        table.Rows.Add(1);
        table.Rows.Add(2);

        Assert.Equal(2, view.Count);
        Assert.Equal(new[] { 1, 2 }, view.Cast<DataRowView>().Select(r => (int)r["Value"]).ToArray());
    }

    [Fact]
    public void BindingList_SortDescriptions_Apply_Sort()
    {
        var table = new DataTable();
        table.Columns.Add(new DataColumn("Value", typeof(int)));
        table.Rows.Add(2);
        table.Rows.Add(1);
        var view = new DataGridCollectionView(table.DefaultView);

        view.SortDescriptions.Add(DataGridSortDescription.FromPath("Value", ListSortDirection.Ascending));

        Assert.Equal(new[] { 1, 2 }, view.Cast<DataRowView>().Select(r => (int)r["Value"]).ToArray());
    }

    [Fact]
    public void BindingList_ItemChanged_Raises_Remove_Then_Add()
    {
        var source = new BindingList<int> { 1 };
        var view = new DataGridCollectionView(source);

        var changes = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (_, e) => changes.Add(e);

        source[0] = 42;

        Assert.NotEmpty(changes);
        if (changes.Count == 1)
        {
            Assert.Equal(NotifyCollectionChangedAction.Reset, changes[0].Action);
        }
        else
        {
            Assert.Collection(
                changes,
                e =>
                {
                    Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
                    Assert.Equal(0, e.OldStartingIndex);
                    var oldItems = Assert.IsAssignableFrom<IList>(e.OldItems);
                    Assert.Single(oldItems);
                },
                e =>
                {
                    Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
                    Assert.Equal(0, e.NewStartingIndex);
                    var newItems = Assert.IsAssignableFrom<IList>(e.NewItems);
                    Assert.Single(newItems);
                });
        }

        Assert.Equal(42, (int)view[0]);
    }

    [Fact]
    public void WeakCollectionChangedHandler_Unsubscribes_When_Target_Lost()
    {
        var source = new TrackingNotifyCollectionChanged();
        var weak = new WeakReference<DataGridCollectionView>(null!);
        var handler = CreateWeakCollectionChangedHandler(weak);

        source.CollectionChanged += handler;
        Assert.Equal(1, source.SubscriptionCount);

        source.RaiseReset();

        Assert.Equal(0, source.SubscriptionCount);
    }

    [Fact]
    public void WeakListChangedHandler_Unsubscribes_When_Target_Lost()
    {
        var source = new TrackingBindingList();
        var weak = new WeakReference<DataGridCollectionView>(null!);
        var handler = CreateWeakListChangedHandler(weak);

        source.ListChanged += handler;
        Assert.Equal(1, source.SubscriptionCount);

        source.RaiseReset();

        Assert.Equal(0, source.SubscriptionCount);
    }

    private static NotifyCollectionChangedEventHandler CreateWeakCollectionChangedHandler(
        WeakReference<DataGridCollectionView> weak)
    {
        var method = typeof(DataGridCollectionView)
            .GetMethod("CreateWeakCollectionChangedHandler", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<NotifyCollectionChangedEventHandler>(method!.Invoke(null, new object[] { weak }));
    }

    private static ListChangedEventHandler CreateWeakListChangedHandler(
        WeakReference<DataGridCollectionView> weak)
    {
        var method = typeof(DataGridCollectionView)
            .GetMethod("CreateWeakListChangedHandler", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ListChangedEventHandler>(method!.Invoke(null, new object[] { weak }));
    }

    private sealed class TrackingNotifyCollectionChanged : INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _collectionChanged;

        public int SubscriptionCount { get; private set; }

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add
            {
                _collectionChanged += value;
                SubscriptionCount = _collectionChanged?.GetInvocationList().Length ?? 0;
            }
            remove
            {
                _collectionChanged -= value;
                SubscriptionCount = _collectionChanged?.GetInvocationList().Length ?? 0;
            }
        }

        public void RaiseReset()
        {
            _collectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    private sealed class TrackingBindingList : IBindingList
    {
        private readonly List<object?> _items = new();
        private ListChangedEventHandler? _listChanged;

        public int SubscriptionCount { get; private set; }

        public event ListChangedEventHandler? ListChanged
        {
            add
            {
                _listChanged += value;
                SubscriptionCount = _listChanged?.GetInvocationList().Length ?? 0;
            }
            remove
            {
                _listChanged -= value;
                SubscriptionCount = _listChanged?.GetInvocationList().Length ?? 0;
            }
        }

        public void RaiseReset()
        {
            _listChanged?.Invoke(this, new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        public bool AllowNew => true;
        public bool AllowEdit => true;
        public bool AllowRemove => true;
        public bool SupportsChangeNotification => true;
        public bool SupportsSearching => false;
        public bool SupportsSorting => false;
        public bool IsSorted => false;
        public ListSortDirection SortDirection => ListSortDirection.Ascending;
        public PropertyDescriptor? SortProperty => null;

        public object AddNew() => throw new NotSupportedException();
        public void AddIndex(PropertyDescriptor property) => throw new NotSupportedException();
        public void ApplySort(PropertyDescriptor property, ListSortDirection direction) => throw new NotSupportedException();
        public int Find(PropertyDescriptor property, object key) => -1;
        public void RemoveIndex(PropertyDescriptor property) => throw new NotSupportedException();
        public void RemoveSort() => throw new NotSupportedException();

        public int Add(object? value)
        {
            _items.Add(value);
            return _items.Count - 1;
        }

        public void Clear() => _items.Clear();
        public bool Contains(object? value) => _items.Contains(value);
        public int IndexOf(object? value) => _items.IndexOf(value);
        public void Insert(int index, object? value) => _items.Insert(index, value);
        public void Remove(object? value) => _items.Remove(value);
        public void RemoveAt(int index) => _items.RemoveAt(index);
        public object? this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public int Count => _items.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}
