// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Filtering;

public class FilteredHierarchyChangeTests
{
    [AvaloniaTheory]
    [InlineData(DataGridHierarchyFilterPolicy.SelfOnly)]
    [InlineData(DataGridHierarchyFilterPolicy.KeepAncestorsOfMatches | DataGridHierarchyFilterPolicy.KeepDescendantsOfMatches)]
    public void Collapsing_A_Filtered_Out_Branch_Keeps_The_Rows_In_Step_With_The_View(DataGridHierarchyFilterPolicy policy)
    {
        var root = new Node("root");
        var alpha = root.Add("alpha");
        for (var i = 0; i < 5; i++)
        {
            alpha.Add("a" + i);
        }

        var beta = root.Add("beta");
        beta.Add("b0");
        beta.Add("needle b1");
        root.Add("gamma").Add("needle g0");

        var hierarchy = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = static item => ((Node)item).Children,
            IsLeafSelector = static item => ((Node)item).Children.Count == 0,
        });
        hierarchy.SetRoot(root);
        hierarchy.ExpandAll();

        var filteringModel = new FilteringModel();
        var column = new DataGridHierarchicalColumn { Header = "Name", Binding = new Binding("Item.Name") };
        DataGridColumnMetadata.SetValueAccessor(column, new DataGridColumnValueAccessor<Node, string>(static node => node.Name));
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            FilteringAdapterFactory = new DataGridHierarchicalFilteringAdapterFactory { Policy = policy },
            FilteringModel = filteringModel,
            HierarchicalModel = hierarchy,
            HierarchicalRowsEnabled = true,
            ItemsSource = hierarchy.ObservableFlattened,
        };
        grid.ColumnsInternal.Add(column);
        var window = new Window { Width = 420, Height = 400, Content = grid };
        window.SetThemeStyles();
        window.Show();

        try
        {
            grid.UpdateLayout();
            filteringModel.SetOrUpdate(new FilteringDescriptor(column, FilteringOperator.Contains, value: "needle"));
            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            var view = grid.DataConnection.CollectionView!;
            Assert.Equal(view.Cast<object>().Count(), grid.SlotCount);

            hierarchy.Collapse(hierarchy.Root!.Children.First(n => ((Node)n.Item).Name == "alpha"));
            Assert.Equal(view.Cast<object>().Count(), grid.SlotCount);

            Dispatcher.UIThread.RunJobs();
            grid.UpdateLayout();
            Assert.Equal(view.Cast<object>().Count(), grid.SlotCount);
            Assert.Contains(view.Cast<HierarchicalNode>(), n => ((Node)n.Item).Name == "needle b1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expanding_And_Collapsing_At_The_Start_Middle_And_End_Applies_The_View_Row_Changes()
    {
        using var tree = new FilteredTree();

        tree.Expand("keep 0", "keep 0.0", "keep 0.2");
        tree.Expand("keep 2", "keep 2.0", "keep 2.2");
        tree.Expand("keep 4", "keep 4.0", "keep 4.2");
        tree.Collapse("keep 2", "keep 2.0", "keep 2.2");
        tree.Collapse("keep 4", "keep 4.0", "keep 4.2");
        tree.Collapse("keep 0", "keep 0.0", "keep 0.2");
    }

    [AvaloniaFact]
    public void Expanding_A_Node_Whose_Children_Are_All_Filtered_Out_Changes_No_Rows()
    {
        using var tree = new FilteredTree();

        tree.Expand("keep 3");
        tree.Expand("keep 0", "keep 0.0", "keep 0.2");
        tree.Collapse("keep 3");
        tree.Collapse("keep 0", "keep 0.0", "keep 0.2");
    }

    [AvaloniaFact]
    public void Expanding_And_Collapsing_A_Filtered_Out_Parent_Applies_Its_Visible_Children()
    {
        using var tree = new FilteredTree();

        tree.Expand("drop 1", "keep 1.0");
        tree.Expand("keep 0", "keep 0.0", "keep 0.2");
        tree.Collapse("drop 1", "keep 1.0");
        tree.Expand("drop 1", "keep 1.0");
        tree.Collapse("keep 0", "keep 0.0", "keep 0.2");
        tree.Collapse("drop 1", "keep 1.0");
    }

    [AvaloniaFact]
    public void Nested_Expansion_Applies_The_View_Row_Changes()
    {
        using var tree = new FilteredTree();

        tree.Expand("keep 2", "keep 2.0", "keep 2.2");
        tree.Expand("keep 2.0", "keep 2.0.0", "keep 2.0.2");
        tree.Collapse("keep 2.0", "keep 2.0.0", "keep 2.0.2");
        tree.Expand("keep 2.0", "keep 2.0.0", "keep 2.0.2");
        tree.Collapse("keep 2", "keep 2.0", "keep 2.0.0", "keep 2.0.2", "keep 2.2");
    }

    [AvaloniaFact]
    public void Filter_Changes_Between_Operations_Keep_The_Rows_In_Step_With_The_View()
    {
        using var tree = new FilteredTree();

        tree.Expand("keep 0", "keep 0.0", "keep 0.2");
        tree.SetFilter(".2");
        tree.Expand("keep 4", "keep 4.2");
        tree.Expand("keep 2", "keep 2.2");
        tree.Collapse("keep 0", "keep 0.2");
        tree.ClearFilter();
        tree.Collapse("keep 4", "keep 4.0", "drop 4.1", "keep 4.2");
        tree.Expand("keep 0", "keep 0.0", "drop 0.1", "keep 0.2");
        tree.SetFilter("keep");
        tree.Expand("keep 2.0", "keep 2.0.0", "keep 2.0.2");
        tree.Collapse("keep 2", "keep 2.0", "keep 2.0.0", "keep 2.0.2", "keep 2.2");
        tree.Collapse("keep 0", "keep 0.0", "keep 0.2");
    }

    [AvaloniaFact]
    public void A_Filter_That_Hides_No_Node_Keeps_The_Hierarchy_Bulk_Splice()
    {
        var roots = new List<Node>();
        for (var i = 0; i < 100; i++)
        {
            var root = new Node("node " + i);
            for (var j = 0; j < 10; j++)
            {
                root.Add("node " + i + "." + j);
            }

            roots.Add(root);
        }

        using var tree = new FilteredTree(roots, "node", allRowsFit: false);

        var activities = tree.Capture(() => tree.Hierarchy.ExpandAll());
        Assert.Contains("ProDataGrid.DataGrid.HierarchicalBulkSplice", activities);
        Assert.DoesNotContain("ProDataGrid.DataGrid.RefreshRowsAndColumns", activities);
        Assert.Equal(1100, tree.Grid.SlotCount);
        tree.AssertRowsMatchView();

        activities = tree.Capture(() => tree.Hierarchy.CollapseAll());
        Assert.Contains("ProDataGrid.DataGrid.HierarchicalBulkSplice", activities);
        Assert.DoesNotContain("ProDataGrid.DataGrid.RefreshRowsAndColumns", activities);
        Assert.Equal(100, tree.Grid.SlotCount);
        tree.AssertRowsMatchView();
    }

    private sealed class FilteredTree : IDisposable
    {
        private readonly FilteringModel _filteringModel = new();
        private readonly DataGridHierarchicalColumn _column;
        private readonly Window _window;
        private readonly bool _allRowsFit;

        public FilteredTree()
            : this(CreateRoots(), "keep", allRowsFit: true)
        {
        }

        public FilteredTree(List<Node> roots, string filter, bool allRowsFit)
        {
            _allRowsFit = allRowsFit;
            Hierarchy = new HierarchicalModel(new HierarchicalOptions
            {
                ChildrenSelector = static item => ((Node)item).Children,
                IsLeafSelector = static item => ((Node)item).Children.Count == 0,
            });
            Hierarchy.SetRoots(roots);

            _column = new DataGridHierarchicalColumn { Header = "Name", Binding = new Binding("Item.Name") };
            DataGridColumnMetadata.SetValueAccessor(_column, new DataGridColumnValueAccessor<Node, string>(static node => node.Name));
            Grid = new DataGrid
            {
                AutoGenerateColumns = false,
                FilteringAdapterFactory = new DataGridHierarchicalFilteringAdapterFactory { Policy = DataGridHierarchyFilterPolicy.SelfOnly },
                FilteringModel = _filteringModel,
                HierarchicalModel = Hierarchy,
                HierarchicalRowsEnabled = true,
                ItemsSource = Hierarchy.ObservableFlattened,
            };
            Grid.ColumnsInternal.Add(_column);
            _window = new Window { Width = 420, Height = 600 };
            _window.SetThemeStyles();
            _window.Content = Grid;
            _window.Show();
            Pump();
            SetFilter(filter);
        }

        public HierarchicalModel Hierarchy { get; }

        public DataGrid Grid { get; }

        public void SetFilter(string text)
        {
            _filteringModel.SetOrUpdate(new FilteringDescriptor(_column, FilteringOperator.Contains, value: text));
            Pump();
            AssertRowsMatchView();
        }

        public void ClearFilter()
        {
            _filteringModel.Clear();
            Pump();
            AssertRowsMatchView();
        }

        public void Expand(string name, params string[] shown) =>
            Change(() => Hierarchy.Expand(Find(name)), shown, Array.Empty<string>());

        public void Collapse(string name, params string[] hidden) =>
            Change(() => Hierarchy.Collapse(Find(name)), Array.Empty<string>(), hidden);

        public List<string> Capture(Action action)
        {
            var activities = new List<string>();
            using (var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == DataGridDiagnostics.ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => activities.Add(activity.OperationName)
            })
            {
                ActivitySource.AddActivityListener(listener);
                action();
            }

            return activities;
        }

        public void AssertRowsMatchView()
        {
            var items = Grid.DataConnection.CollectionView!.Cast<object>().ToList();
            Assert.Equal(items.Count, Grid.SlotCount);
            var rows = Grid.DisplayData.GetScrollingRows().OfType<DataGridRow>().ToList();
            Assert.NotEmpty(rows);
            if (_allRowsFit)
            {
                Assert.Equal(items.Count, rows.Count);
            }

            foreach (var row in rows)
            {
                Assert.Same(items[row.Index], row.DataContext);
            }
        }

        public void Dispose() => _window.Close();

        private void Change(Action action, string[] shown, string[] hidden)
        {
            var expected = VisibleNames().Concat(shown).Except(hidden).OrderBy(x => x, StringComparer.Ordinal).ToList();

            var activities = Capture(action);
            Assert.Contains("ProDataGrid.DataGrid.HierarchicalFlattenedChanged", activities);
            Assert.DoesNotContain("ProDataGrid.DataGrid.RefreshRowsAndColumns", activities);
            Assert.Equal(expected, VisibleNames().OrderBy(x => x, StringComparer.Ordinal));
            AssertRowsMatchView();

            Pump();
            AssertRowsMatchView();
        }

        private HierarchicalNode Find(string name) =>
            Hierarchy.Flattened.First(node => ((Node)node.Item).Name == name);

        private IEnumerable<string> VisibleNames() =>
            Grid.DataConnection.CollectionView!.Cast<HierarchicalNode>().Select(node => ((Node)node.Item).Name).ToList();

        private void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            _window.UpdateLayout();
        }

        private static List<Node> CreateRoots()
        {
            var roots = new List<Node>();
            var keep0 = Root(roots, "keep 0");
            keep0.Add("keep 0.0");
            keep0.Add("drop 0.1");
            keep0.Add("keep 0.2");
            var drop1 = Root(roots, "drop 1");
            drop1.Add("keep 1.0");
            drop1.Add("drop 1.1");
            var keep2 = Root(roots, "keep 2");
            var keep20 = keep2.Add("keep 2.0");
            keep20.Add("keep 2.0.0");
            keep20.Add("drop 2.0.1");
            keep20.Add("keep 2.0.2");
            keep2.Add("drop 2.1");
            keep2.Add("keep 2.2");
            var keep3 = Root(roots, "keep 3");
            keep3.Add("drop 3.0");
            keep3.Add("drop 3.1");
            var keep4 = Root(roots, "keep 4");
            keep4.Add("keep 4.0");
            keep4.Add("drop 4.1");
            keep4.Add("keep 4.2");
            return roots;
        }

        private static Node Root(List<Node> roots, string name)
        {
            var node = new Node(name);
            roots.Add(node);
            return node;
        }
    }

    private sealed class Node
    {
        public Node(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ObservableCollection<Node> Children { get; } = new();

        public Node Add(string name)
        {
            var child = new Node(name);
            Children.Add(child);
            return child;
        }
    }
}
