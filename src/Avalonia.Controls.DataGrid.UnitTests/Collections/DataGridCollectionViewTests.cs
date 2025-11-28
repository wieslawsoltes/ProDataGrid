using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Linq;
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
}
