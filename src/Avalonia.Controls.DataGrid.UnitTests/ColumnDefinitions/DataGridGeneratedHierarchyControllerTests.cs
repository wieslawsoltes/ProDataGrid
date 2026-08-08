// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedHierarchyControllerTests
{
    [Fact]
    public void Validation_distinguishes_duplicate_keys_cycles_and_depth_limits()
    {
        Node duplicateRoot = new(1, "root", new Node(2, "first"), new Node(2, "second"));
        DataGridGeneratedHierarchyController<Node, int> controller = CreateController();

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() =>
            controller.Validate(new[] { duplicateRoot }));
        Assert.Contains("duplicate", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        Node cycleRoot = new(10, "cycle");
        cycleRoot.Children.Add(cycleRoot);
        InvalidOperationException cycle = Assert.Throws<InvalidOperationException>(() =>
            controller.Validate(new[] { cycleRoot }));
        Assert.Contains("cycle", cycle.Message, StringComparison.OrdinalIgnoreCase);

        Node deep = new(20, "root", new Node(21, "child", new Node(22, "leaf")));
        Assert.Throws<InvalidOperationException>(() => controller.Validate(new[] { deep }, maxDepth: 1));
    }

    [Fact]
    public void Expansion_is_captured_restored_and_expanded_to_stable_key()
    {
        Node leaf = new(3, "leaf");
        Node child = new(2, "child", leaf);
        Node root = new(1, "root", child);
        DataGridGeneratedHierarchyController<Node, int> controller = CreateController();

        Assert.True(controller.ExpandToKey(new[] { root }, 3));
        Assert.True(root.IsExpanded);
        Assert.True(child.IsExpanded);
        Assert.False(leaf.IsExpanded);

        HashSet<int> captured = controller.CaptureExpanded(new[] { root });
        controller.CollapseAll(new[] { root });
        Assert.All(new[] { root, child, leaf }, node => Assert.False(node.IsExpanded));

        controller.RestoreExpanded(new[] { root }, captured);
        Assert.True(root.IsExpanded);
        Assert.True(child.IsExpanded);
        Assert.False(leaf.IsExpanded);
    }

    [Fact]
    public void Hierarchy_filter_modes_include_expected_related_nodes()
    {
        Node match = new(3, "match", new Node(4, "descendant"));
        Node root = new(1, "root", new Node(2, "other"), match);
        DataGridGeneratedHierarchyController<Node, int> controller = CreateController();

        HashSet<int> matchOnly = controller.BuildFilterKeys(
            new[] { root }, node => node.Name == "match", DataGridGeneratedHierarchyFilterMode.MatchOnly);
        HashSet<int> ancestors = controller.BuildFilterKeys(
            new[] { root }, node => node.Name == "match", DataGridGeneratedHierarchyFilterMode.AncestorsOfMatch);
        HashSet<int> descendants = controller.BuildFilterKeys(
            new[] { root }, node => node.Name == "match", DataGridGeneratedHierarchyFilterMode.DescendantsOfMatch);

        Assert.Equal(new[] { 3 }, matchOnly.OrderBy(static key => key));
        Assert.Equal(new[] { 1, 3 }, ancestors.OrderBy(static key => key));
        Assert.Equal(new[] { 3, 4 }, descendants.OrderBy(static key => key));
    }

    [Fact]
    public void Projection_is_typed_sorted_and_can_respect_expansion()
    {
        Node hiddenChild = new(4, "hidden");
        Node collapsed = new(3, "b", hiddenChild);
        Node expanded = new(2, "a", new Node(5, "z")) { IsExpanded = true };
        Node root = new(1, "root", collapsed, expanded) { IsExpanded = true };
        DataGridGeneratedHierarchyController<Node, int> controller = CreateController();

        IReadOnlyList<DataGridGeneratedNode<Node, int>> projection = controller.Project(
            new[] { root },
            Comparer<Node>.Create(static (left, right) => string.CompareOrdinal(left.Name, right.Name)),
            expandedOnly: true);

        Assert.Equal(new[] { 1, 2, 5, 3 }, projection.Select(static node => node.Key));
        Assert.Equal(new[] { 0, 1, 2, 1 }, projection.Select(static node => node.Depth));
        Assert.Equal(2, projection[2].ParentKey);
        Assert.True(projection[2].HasParent);
        Assert.DoesNotContain(projection, node => node.Key == 4);
    }

    [Fact]
    public async Task Async_loader_is_used_when_supplied_and_sync_children_are_fallback()
    {
        Node loaded = new(9, "loaded");
        Node root = new(1, "root", new Node(2, "sync"));
        DataGridGeneratedHierarchyController<Node, int> asynchronous = new(
            s_key,
            static node => node.Children,
            childLoader: (_, _) => ValueTask.FromResult<IReadOnlyList<Node>>(new[] { loaded }));
        DataGridGeneratedHierarchyController<Node, int> synchronous = CreateController();

        Assert.True(asynchronous.CanLoadChildren);
        Assert.Equal(9, Assert.Single(await asynchronous.LoadChildrenAsync(root)).Id);
        Assert.False(synchronous.CanLoadChildren);
        Assert.Equal(2, Assert.Single(await synchronous.LoadChildrenAsync(root)).Id);
    }

    private static readonly NodeKey s_key = new();

    private static DataGridGeneratedHierarchyController<Node, int> CreateController() =>
        new(s_key, static node => node.Children, static node => node.IsExpanded, static (node, value) => node.IsExpanded = value);

    private sealed class Node
    {
        public Node(int id, string name, params Node[] children)
        {
            Id = id;
            Name = name;
            Children.AddRange(children);
        }

        public int Id { get; }
        public string Name { get; }
        public List<Node> Children { get; } = new();
        public bool IsExpanded { get; set; }
    }

    private sealed class NodeKey : IDataGridItemKey<Node, int>
    {
        public int GetKey(Node item) => item.Id;
    }
}
