// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
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
