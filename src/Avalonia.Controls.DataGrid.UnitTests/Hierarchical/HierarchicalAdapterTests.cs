// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridHierarchical;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class HierarchicalAdapterTests
{
    private class Item
    {
        public Item(string name)
        {
            Name = name;
            Children = new ObservableCollection<Item>();
        }

        public string Name { get; }

        public ObservableCollection<Item> Children { get; }
    }

    private static HierarchicalModel CreateModel()
    {
        return new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((Item)item).Children
        });
    }

    [Fact]
    public void CountAndIndex_UseModel()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);

        var model = CreateModel();
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);
        adapter.Expand(0);

        Assert.Equal(2, adapter.Count);
        Assert.Same(child, adapter.ItemAt(1));
        Assert.Equal(1, adapter.LevelAt(1));
        Assert.Equal(1, adapter.IndexOfItem(child));
        Assert.Equal(1, adapter.IndexOfNode(adapter.NodeAt(1)));
    }

    [Fact]
    public void ToggleAndCollapse_DelegateToModel()
    {
        var root = new Item("root");
        var child = new Item("child");
        child.Children.Add(new Item("grand"));
        root.Children.Add(child);

        var model = CreateModel();
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);
        adapter.Expand(0);

        Assert.False(adapter.NodeAt(1).IsExpanded);
        adapter.Toggle(1);
        Assert.True(adapter.NodeAt(1).IsExpanded);
        adapter.Collapse(1);
        Assert.False(adapter.NodeAt(1).IsExpanded);
    }

    [Fact]
    public void ExpandAll_And_CollapseAll_Work()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = CreateModel();
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);

        adapter.ExpandAll();
        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
        Assert.Equal(3, adapter.Count);

        adapter.CollapseAll(minDepth: 1);
        Assert.True(model.Root!.IsExpanded);
        Assert.False(model.GetNode(1).IsExpanded);
        Assert.Equal(2, adapter.Count);
    }

    [Fact]
    public async Task ExpandAllAsync_DelegatesToModel()
    {
        var root = new Item("root");
        var child = new Item("child");
        root.Children.Add(child);
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelectorAsync = (item, _) =>
                Task.FromResult<IEnumerable?>(((Item)item).Children)
        });
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);

        await adapter.ExpandAllAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, adapter.Count);
        Assert.True(model.Root!.IsExpanded);
        Assert.True(model.GetNode(1).IsExpanded);
    }

    [Fact]
    public void FlattenedChanged_IsRaised()
    {
        var root = new Item("root");
        root.Children.Add(new Item("child"));

        var model = CreateModel();
        var adapter = new DataGridHierarchicalAdapter(model);
        adapter.SetRoot(root);

        int calls = 0;
        adapter.FlattenedChanged += (_, __) => calls++;

        adapter.Expand(0);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void DataGrid_Uses_Typed_Adapter_Factory_For_Typed_Model()
    {
        var model = new HierarchicalModel<Item>(new HierarchicalOptions<Item>
        {
            ChildrenSelector = item => item.Children
        });
        var factory = new TypedAdapterFactory();
        var grid = new AdapterTestGrid { HierarchicalAdapterFactory = factory };

        var adapter = grid.CreateAdapter(model);

        Assert.Same(model, factory.TypedModel);
        Assert.Same(factory.CreatedAdapter!.InnerAdapter, adapter);
        Assert.Equal(0, factory.UntypedCalls);
    }

    [Fact]
    public void DataGrid_Falls_Back_To_Untyped_Factory_When_Item_Type_Differs()
    {
        var model = new HierarchicalModel<string>(new HierarchicalOptions<string>
        {
            ChildrenSelector = _ => null
        });
        var factory = new TypedAdapterFactory();
        var grid = new AdapterTestGrid { HierarchicalAdapterFactory = factory };

        var adapter = grid.CreateAdapter(model);

        Assert.Null(factory.TypedModel);
        Assert.Equal(1, factory.UntypedCalls);
        Assert.NotNull(adapter);
    }

    private sealed class AdapterTestGrid : DataGrid
    {
        public DataGridHierarchicalAdapter CreateAdapter(IHierarchicalModel model) => CreateHierarchicalAdapter(model);
    }

    private sealed class TypedAdapterFactory : IDataGridHierarchicalAdapterFactory<Item>
    {
        public IHierarchicalModel<Item>? TypedModel { get; private set; }

        public DataGridHierarchicalAdapter<Item>? CreatedAdapter { get; private set; }

        public int UntypedCalls { get; private set; }

        public DataGridHierarchicalAdapter<Item> Create(DataGrid grid, IHierarchicalModel<Item> model)
        {
            TypedModel = model;
            CreatedAdapter = new DataGridHierarchicalAdapter<Item>(model);
            return CreatedAdapter;
        }

        public DataGridHierarchicalAdapter Create(DataGrid grid, IHierarchicalModel model)
        {
            UntypedCalls++;
            return new DataGridHierarchicalAdapter(model);
        }
    }
}
