// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class SelectedItemsViewTests
{
    [Fact]
    public void SelectedItemsView_Raises_Add_And_Remove_On_Model_Selection()
    {
        var model = new SelectionModel<string>
        {
            SingleSelect = false,
            Source = new[] { "a", "b", "c" }
        };

        var view = new SelectedItemsView(model);
        var changes = new List<NotifyCollectionChangedEventArgs>();
        ((INotifyCollectionChanged)view).CollectionChanged += (_, e) => changes.Add(e);

        model.Select(1);
        model.Select(2);
        model.Deselect(1);

        Assert.Collection(
            changes,
            add =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Add, add.Action);
                var items = Assert.IsAssignableFrom<IList>(add.NewItems);
                Assert.Equal("b", Assert.Single(items.Cast<string>()));
            },
            add =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Add, add.Action);
                var items = Assert.IsAssignableFrom<IList>(add.NewItems);
                Assert.Equal("c", Assert.Single(items.Cast<string>()));
            },
            remove =>
            {
                Assert.Equal(NotifyCollectionChangedAction.Remove, remove.Action);
                var items = Assert.IsAssignableFrom<IList>(remove.OldItems);
                Assert.Equal("b", Assert.Single(items.Cast<string>()));
            });
    }

    [Fact]
    public void SelectedItemsView_Add_Selects_Item_By_Value()
    {
        var model = new SelectionModel<string>
        {
            SingleSelect = false,
            Source = new[] { "a", "b", "c" }
        };
        var view = new SelectedItemsView(model);

        view.Add("c");
        view.Add("a");

        Assert.True(model.IsSelected(2));
        Assert.True(model.IsSelected(0));
        Assert.Equal(new[] { "a", "c" }, view.Cast<string>().OrderBy(x => x));
    }

    [Fact]
    public void SelectedItemsView_Remove_Deselects_Item()
    {
        var model = new SelectionModel<string>
        {
            SingleSelect = false,
            Source = new[] { "a", "b", "c" }
        };
        var view = new SelectedItemsView(model);

        model.Select(0);
        model.Select(1);

        view.Remove("a");

        Assert.False(model.IsSelected(0));
        Assert.True(model.IsSelected(1));
        Assert.Equal(new[] { "b" }, view.Cast<string>());
    }

    [Fact]
    public void SelectedItemsView_Clear_Resets_Model_Selection()
    {
        var model = new SelectionModel<string>
        {
            SingleSelect = false,
            Source = new[] { "a", "b", "c" }
        };
        var view = new SelectedItemsView(model);

        model.SelectRange(0, 2);
        view.Clear();

        Assert.Empty(model.SelectedIndexes);
        Assert.Empty(view);
    }

    [Fact]
    public void SelectionModel_Shifts_On_Insert_Before_Selected()
    {
        var source = new ObservableCollection<string> { "A", "B", "C" };
        var model = new SelectionModel<string>
        {
            SingleSelect = false,
            Source = source
        };

        model.Select(1);
        source.Insert(0, "Z");

        Assert.Equal("B", model.SelectedItem);
        Assert.Equal(new[] { 2 }, model.SelectedIndexes.ToArray());
    }

    [Fact]
    public void SelectedItemsView_Projects_Items_Using_Selector()
    {
        var nodes = new[]
        {
            new Node("a"),
            new Node("b")
        };

        var model = new SelectionModel<Node>
        {
            SingleSelect = false,
            Source = nodes
        };

        var view = new SelectedItemsView(
            model,
            item => item is Node node ? node.Value : item,
            null);

        model.Select(1);

        Assert.Equal("b", view[0]);
        Assert.Contains("b", view.Cast<string>());
    }

    [Fact]
    public void SelectedItemsView_Uses_IndexResolver_For_Add_And_Remove()
    {
        var nodes = new[]
        {
            new Node("a"),
            new Node("b"),
            new Node("c")
        };

        var model = new SelectionModel<Node>
        {
            SingleSelect = false,
            Source = nodes
        };

        var view = new SelectedItemsView(
            model,
            item => item is Node node ? node.Value : item,
            value => Array.FindIndex(nodes, node => Equals(node.Value, value)));

        view.Add("c");
        Assert.True(model.IsSelected(2));

        view.Remove("c");
        Assert.False(model.IsSelected(2));
    }

    private sealed class Node
    {
        public Node(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

}
