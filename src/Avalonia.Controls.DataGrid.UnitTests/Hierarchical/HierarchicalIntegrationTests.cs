// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Input;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Hierarchical;

public class HierarchicalIntegrationTests
{
    private class Item
    {
        public Item(string name)
        {
            Name = name;
            Children = new ObservableCollection<Item>();
        }

        public string Name { get; set; }

        public ObservableCollection<Item> Children { get; set; }

        public long Size { get; set; }
    }

    private class CustomCollection<T> : ObservableCollection<T>
    {
        public void ReplaceRange(int index, IList oldItems, IList newItems)
        {
            for (int i = 0; i < oldItems.Count && index < Items.Count; i++)
            {
                Items.RemoveAt(index);
            }

            for (int i = 0; i < newItems.Count; i++)
            {
                Items.Insert(index + i, (T)newItems[i]!);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Replace,
                newItems,
                oldItems,
                index));
        }
    }

    private static HierarchicalModel CreateModel()
    {
        return new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children
        });
    }

    [Fact]
    public void ReplaceRange_AddsItems_EmitsDiff()
    {
        var root = new Item("root");
        var children = new CustomCollection<Item> { new Item("a"), new Item("b") };
        root.Children = children;

        FlattenedChangedEventArgs? lastArgs = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => lastArgs = e;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var newItems = new[] { new Item("c"), new Item("d") };
        children.ReplaceRange(1, new[] { children[1] }, newItems);

        Assert.Equal(4, model.Count); // root + a + c + d
        Assert.NotNull(lastArgs);
        var change = Assert.Single(lastArgs!.Changes);
        Assert.Equal(2, change.Index);
        Assert.Equal(1, change.OldCount);
        Assert.Equal(2, change.NewCount);
        Assert.Equal(4, lastArgs.IndexMap.NewCount);
    }

    [Fact]
    public void ReplaceRange_RemovesItems_EmitsDiff()
    {
        var root = new Item("root");
        var children = new CustomCollection<Item> { new Item("a"), new Item("b"), new Item("c") };
        root.Children = children;

        FlattenedChangedEventArgs? lastArgs = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => lastArgs = e;
        model.SetRoot(root);
        model.Expand(model.Root!);

        children.ReplaceRange(1, new[] { children[1], children[2] }, new[] { new Item("d") });

        Assert.Equal(3, model.Count); // root + a + d
        Assert.NotNull(lastArgs);
        var change = Assert.Single(lastArgs!.Changes);
        Assert.Equal(2, change.Index);
        Assert.Equal(2, change.OldCount);
        Assert.Equal(1, change.NewCount);
        Assert.Equal(3, lastArgs.IndexMap.NewCount);
    }

    private static IComparer<object> BuildComparer(IReadOnlyList<SortingDescriptor> descriptors)
    {
        return Comparer<object>.Create((x, y) =>
        {
            var left = x as Item;
            var right = y as Item;

            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            foreach (var descriptor in descriptors)
            {
                var path = descriptor.PropertyPath;
                var leftValue = GetPropertyValue(left, path);
                var rightValue = GetPropertyValue(right, path);
                var result = StringComparer.OrdinalIgnoreCase.Compare(leftValue, rightValue);

                if (result != 0)
                {
                    return descriptor.Direction == ListSortDirection.Descending ? -result : result;
                }
            }

            return 0;
        });
    }

    private static string? GetPropertyValue(Item item, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var type = typeof(Item);
        PropertyInfo? property = type.GetProperty(path) ?? type.GetProperty(path.Replace("Item.", string.Empty));
        var value = property?.GetValue(item);
        return value?.ToString();
    }

    private static IReadOnlyList<int>? BuildPath(Item root, Item target)
    {
        var path = new List<int>();
        if (!TryBuildPath(root, target, path))
        {
            return null;
        }

        path.Insert(0, 0);
        return path;
    }

    private static bool TryBuildPath(Item current, Item target, List<int> path)
    {
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        for (int i = 0; i < current.Children.Count; i++)
        {
            path.Add(i);
            if (TryBuildPath(current.Children[i], target, path))
            {
                return true;
            }

            path.RemoveAt(path.Count - 1);
        }

        return false;
    }

    [Fact]
    public void HeaderClick_SortsHierarchyAscending()
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            ItemPathSelector = o => BuildPath(root, (Item)o)
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var sorting = new SortingModel();
        sorting.SortingChanged += (_, e) =>
        {
            var comparer = BuildComparer(e.NewDescriptors);
            model.ApplySiblingComparer(comparer, recursive: true);
        };

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var adapter = new LocalHierarchicalSortingAdapter(
            sorting,
            () => new[] { column });

        adapter.HandleHeaderClick(column, KeyModifiers.None);

        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void TreatGroupsAsNodes_ProjectsGroups()
    {
        var items = new[]
        {
            new Item("a"),
            new Item("a"),
            new Item("b")
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription("Name"));
        var groups = view.Groups.Cast<DataGridCollectionViewGroup>().ToArray();
        Assert.Equal(2, groups.Length);

        var options = new HierarchicalOptions
        {
            TreatGroupsAsNodes = true,
            AutoExpandRoot = true
        };

        var model = new HierarchicalModel(options);
        model.SetRoot(view);
        model.Expand(model.Root!);

        foreach (var group in groups)
        {
            var idx = model.IndexOf(group);
            Assert.True(idx >= 0);
            model.Expand(model.GetNode(idx));
        }

        var flattened = model.Flattened.Select(n => n.Item).ToArray();
        Assert.Equal(view, flattened[0]);
        Assert.Equal(groups[0], flattened[1]);
        Assert.Equal(items[0], flattened[2]);
        Assert.Equal(items[1], flattened[3]);
        Assert.Equal(groups[1], flattened[4]);
        Assert.Equal(items[2], flattened[5]);
        Assert.Equal(0, model.GetNode(0).Level);
        Assert.Equal(1, model.GetNode(1).Level);
        Assert.Equal(2, model.GetNode(2).Level);
    }

    [Fact]
    public void TreatGroupsAsNodes_Selection_And_Expansion()
    {
        var items = new[]
        {
            new Item("a") { Size = 1 },
            new Item("a") { Size = 2 },
            new Item("b") { Size = 3 }
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription("Name"));
        var groups = view.Groups.Cast<DataGridCollectionViewGroup>().ToArray();

        var options = new HierarchicalOptions
        {
            TreatGroupsAsNodes = true,
            AutoExpandRoot = true
        };

        var model = new HierarchicalModel(options);
        model.SetRoot(view);
        model.Expand(model.Root!);

        foreach (var group in groups)
        {
            var idx = model.IndexOf(group);
            model.Expand(model.GetNode(idx));
        }

        Assert.Equal(view, model.GetItem(0));
        Assert.Equal(groups[0], model.GetItem(1));
        Assert.Equal(items[0], model.GetItem(2));
        Assert.Equal(items[1], model.GetItem(3));
        Assert.Equal(groups[1], model.GetItem(4));
        Assert.Equal(items[2], model.GetItem(5));

        var selection = new SelectionModel<object>();
        selection.Source = model.Flattened.Select(x => x.Item).ToArray();
        selection.Select(3); // select a2
        Assert.Contains(3, selection.SelectedIndexes);
        Assert.Same(items[1], selection.SelectedItem);
    }

    [Fact]
    public void TreatGroupsAsNodes_SiblingComparer_SortsGroups()
    {
        var items = new[]
        {
            new Item("b") { Size = 1 },
            new Item("a") { Size = 2 }
        };

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription("Name"));
        var groups = view.Groups.Cast<DataGridCollectionViewGroup>().ToArray();

        var options = new HierarchicalOptions
        {
            TreatGroupsAsNodes = true,
            AutoExpandRoot = true,
            SiblingComparer = Comparer<object>.Create((x, y) =>
            {
                var left = (x as DataGridCollectionViewGroup)?.Key?.ToString();
                var right = (y as DataGridCollectionViewGroup)?.Key?.ToString();
                return StringComparer.OrdinalIgnoreCase.Compare(right, left); // descending
            })
        };

        var model = new HierarchicalModel(options);
        model.SetRoot(view);
        model.Expand(model.Root!);

        var groupNodes = model.Flattened.Where(n => n.Item is DataGridCollectionViewGroup).ToArray();
        Assert.Equal(2, groupNodes.Length);
        Assert.Equal("b", ((DataGridCollectionViewGroup)groupNodes[0].Item).Key);
        Assert.Equal("a", ((DataGridCollectionViewGroup)groupNodes[1].Item).Key);
    }

    [Fact]
    public void HeaderClick_TogglesDescendingOnSecondClick()
    {
        var root = new Item("root");
        root.Children.Add(new Item("a"));
        root.Children.Add(new Item("b"));
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            ItemPathSelector = o => BuildPath(root, (Item)o)
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var sorting = new SortingModel();
        sorting.SortingChanged += (_, e) =>
        {
            var comparer = BuildComparer(e.NewDescriptors);
            model.ApplySiblingComparer(comparer, recursive: true);
        };

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var adapter = new LocalHierarchicalSortingAdapter(
            sorting,
            () => new[] { column });

        adapter.HandleHeaderClick(column, KeyModifiers.None); // ascending
        adapter.HandleHeaderClick(column, KeyModifiers.None); // descending

        Assert.Equal("b", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("a", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public void HierarchicalSortingAdapter_AppliesDescriptorsToModel()
    {
        var root = new Item("root");
        root.Children.Add(new Item("b"));
        root.Children.Add(new Item("a"));

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            ItemPathSelector = o => BuildPath(root, (Item)o)
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var sorting = new SortingModel();
        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var adapter = new Avalonia.Controls.DataGridHierarchical.HierarchicalSortingAdapter(
            model,
            sorting,
            () => new[] { column });

        var view = new DataGridCollectionView(new List<Item> { root });
        adapter.AttachView(view);

        adapter.HandleHeaderClick(column, KeyModifiers.None);

        Assert.Equal("a", ((Item)model.GetItem(1)!).Name);
        Assert.Equal("b", ((Item)model.GetItem(2)!).Name);
    }

    [Fact]
    public async Task Selection_Remaps_OnMove_List()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        var adapterField = typeof(DataGrid).GetField("_hierarchicalAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(adapterField?.GetValue(grid));
        var adapter = adapterField!.GetValue(grid)!;
        bool adapterEventFired = false;
        ((DataGridHierarchicalAdapter)adapter).FlattenedChanged += (_, __) => adapterEventFired = true;
        var hierarchicalEnabledField = typeof(DataGrid).GetField("_hierarchicalRowsEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
        var modelHandlersField = typeof(HierarchicalModel).GetField("FlattenedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        var modelHandlers = ((MulticastDelegate?)modelHandlersField?.GetValue(model))?.GetInvocationList().Select(h => h.Method.Name).ToArray() ?? Array.Empty<string>();
        Assert.Contains("HierarchicalAdapter_FlattenedChanged", modelHandlers);

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.NotNull(grid.Selection.Source);

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);
        root.Children.Move(1, 0); // move childB before childA (index map should remap)
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        Assert.True(adapterEventFired);
        Assert.Equal(true, (bool)hierarchicalEnabledField!.GetValue(grid)!);
        var suppressionField = typeof(DataGrid).GetField("_hierarchicalRefreshSuppressionCount", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal(0, (int)suppressionField!.GetValue(grid)!);
        var selectionAdapterField = typeof(DataGrid).GetField("_selectionModelAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(selectionAdapterField!.GetValue(grid));
        Assert.Contains(1, grid.Selection.SelectedIndexes);
        Assert.NotNull(indexMap);
        Assert.Equal(1, indexMap!.MapOldIndexToNew(2));
        Assert.Equal(1, grid.Selection.SelectedIndex);
        Assert.Same(childB, grid.Selection.SelectedItem);
    }

    [Fact]
    public async Task Selection_Remaps_OnMove_DataGridCollectionView()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        var childC = new Item("c");
        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        var adapterField = typeof(DataGrid).GetField("_hierarchicalAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(adapterField?.GetValue(grid));
        var adapter = (DataGridHierarchicalAdapter)adapterField!.GetValue(grid)!;
        bool adapterEventFired = false;
        adapter.FlattenedChanged += (_, __) => adapterEventFired = true;

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.NotNull(grid.Selection.Source);

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);
        root.Children.Move(1, 2); // move childB after childC
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        Assert.True(adapterEventFired);
        var selectionAdapterField = typeof(DataGrid).GetField("_selectionModelAdapter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(selectionAdapterField!.GetValue(grid));

        Assert.NotNull(indexMap);
        Assert.Equal(3, indexMap!.MapOldIndexToNew(2));
        Assert.Equal(3, grid.Selection.SelectedIndex);
        Assert.Same(childB, grid.Selection.SelectedItem);
    }

    [Fact]
    public async Task Selection_Remaps_OnReplace_List_IndexShift()
    {
        var root = new Item("root");
        var children = new CustomCollection<Item>
        {
            new Item("a"),
            new Item("b"),
            new Item("c"),
            new Item("d")
        };
        root.Children = children;

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.Selection.Select(4); // select "d"
        Assert.Contains(4, grid.Selection.SelectedIndexes);

        children.ReplaceRange(1, new[] { children[1] }, new[] { new Item("b1"), new Item("b2") });

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(5, indexMap!.MapOldIndexToNew(4));
        Assert.Contains(5, grid.Selection.SelectedIndexes);
        Assert.Same(children[4], grid.Selection.SelectedItem); // "d" shifts down but stays selected
    }

    [Fact]
    public async Task Selection_Remaps_OnReplace_DataGridCollectionView()
    {
        var root = new Item("root");
        var children = new CustomCollection<Item>
        {
            new Item("a"),
            new Item("b"),
            new Item("c"),
            new Item("d")
        };
        root.Children = children;

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.Selection.Select(4); // select "d"
        Assert.Contains(4, grid.Selection.SelectedIndexes);

        children.ReplaceRange(1, new[] { children[1] }, new[] { new Item("b1"), new Item("b2") });

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(5, indexMap!.MapOldIndexToNew(4));
        Assert.Contains(5, grid.Selection.SelectedIndexes);
        Assert.Same(children[4], grid.Selection.SelectedItem);
    }

    [Fact]
    public async Task Selection_Remaps_OnMove_DataGridCollectionView_Paged()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        var childC = new Item("c");
        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened)
        {
            PageSize = 10
        };
        view.MoveToPage(0);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.NotNull(grid.Selection.Source);

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);
        root.Children.Move(1, 2); // move childB after childC
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(3, indexMap!.MapOldIndexToNew(2));
        Assert.Equal(3, grid.Selection.SelectedIndex);
        Assert.Same(childB, grid.Selection.SelectedItem);
    }

    [Fact]
    public async Task Selection_Remaps_OnMove_DataGridCollectionView_Grouped()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        var childC = new Item("c");
        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription("Item.Name"));

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.NotNull(grid.Selection.Source);

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);
        root.Children.Move(1, 2); // move childB after childC
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(3, indexMap!.MapOldIndexToNew(2));
        Assert.Equal(3, grid.Selection.SelectedIndex);
        Assert.Same(childB, grid.Selection.SelectedItem);
    }

    [Fact]
    public async Task Selection_Remaps_OnReplace_DataGridCollectionView_Paged()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        var childC = new Item("c");
        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened)
        {
            PageSize = 10
        };
        view.MoveToPage(0);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);

        var lostSelectionCount = 0;
        var indexesChangedCount = 0;
        grid.Selection.LostSelection += (_, __) => lostSelectionCount++;
        grid.Selection.IndexesChanged += (_, __) => indexesChangedCount++;

        var replacement = new Item("b2");
        root.Children[1] = replacement; // replace childB

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(-1, indexMap!.MapOldIndexToNew(2)); // replace currently drops the old index
        Assert.DoesNotContain(2, grid.Selection.SelectedIndexes);
        Assert.Null(grid.Selection.SelectedItem);
        Assert.Null(view.CurrentItem);
        Assert.InRange(lostSelectionCount, 0, 2);
        Assert.InRange(indexesChangedCount, 0, 2);
    }

    [Fact]
    public async Task Selection_Remaps_OnReplace_DataGridCollectionView_Grouped()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        var childC = new Item("c");
        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription("Item.Name"));

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.Selection.Select(2); // select childB
        Assert.Contains(2, grid.Selection.SelectedIndexes);

        var lostSelectionCount = 0;
        var indexesChangedCount = 0;
        grid.Selection.LostSelection += (_, __) => lostSelectionCount++;
        grid.Selection.IndexesChanged += (_, __) => indexesChangedCount++;

        var replacement = new Item("b2");
        root.Children[1] = replacement; // replace childB

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.NotNull(indexMap);
        Assert.Equal(-1, indexMap!.MapOldIndexToNew(2)); // replace currently drops the old index
        Assert.DoesNotContain(2, grid.Selection.SelectedIndexes);
        Assert.Null(grid.Selection.SelectedItem);
        Assert.InRange(lostSelectionCount, 0, 2);
        Assert.InRange(indexesChangedCount, 0, 2);
    }

    [Fact]
    public async Task Selection_Persists_OnRefresh_DataGridCollectionView_Paged()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        FlattenedIndexMap? indexMap = null;
        var model = CreateModel();
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;
        model.SetRoot(root);
        model.Expand(model.Root!);

        var view = new DataGridCollectionView(model.Flattened)
        {
            PageSize = 10
        };
        view.MoveToPage(0);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = view
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.Selection.Select(1); // select childA
        Assert.Contains(1, grid.Selection.SelectedIndexes);

        var lostSelectionCount = 0;
        var indexesChangedCount = 0;
        grid.Selection.LostSelection += (_, __) => lostSelectionCount++;
        grid.Selection.IndexesChanged += (_, __) => indexesChangedCount++;

        view.Refresh(); // forces reset/no index map

        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        Assert.Contains(1, grid.Selection.SelectedIndexes);
        Assert.Same(childA, grid.Selection.SelectedItem);
        Assert.Equal(0, lostSelectionCount);
        Assert.InRange(indexesChangedCount, 0, 1);
    }

    [Fact]
    public void Selection_CanBeRemappedAfterSort()
    {
        var root = new Item("root");
        var childA = new Item("b");
        var childB = new Item("a");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            ItemPathSelector = o => BuildPath(root, (Item)o)
        });
        model.SetRoot(root);
        model.Expand(model.Root!);

        var selection = new SelectionModel<object>();
        selection.Select(1); // selects childA in initial order

        var sorting = new SortingModel();
        sorting.SortingChanged += (_, e) =>
        {
            var comparer = BuildComparer(e.NewDescriptors);
            model.ApplySiblingComparer(comparer, recursive: true);
        };

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var adapter = new LocalHierarchicalSortingAdapter(
            sorting,
            () => new[] { column });

        adapter.HandleHeaderClick(column, KeyModifiers.None); // ascending

        var newIndex = model.IndexOf(childA);
        Assert.Equal(2, newIndex); // childA moved after sorting
        selection.Clear();
        selection.Select(newIndex);
        Assert.True(selection.IsSelected(newIndex));
    }

    [Fact]
    public void Selection_Reapplies_AfterSortAndExpansion()
    {
        var root = new Item("root");
        var childA = new Item("b");
        childA.Children.Add(new Item("z"));
        var childB = new Item("a");
        childB.Children.Add(new Item("y"));
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);
        model.Expand(model.GetNode(1));
        model.Expand(model.GetNode(3)); // expand both children

        var selection = new SelectionModel<object>();
        var targetItem = childA.Children[0];
        var initialIndex = model.IndexOf(targetItem);
        selection.Select(initialIndex);
        Assert.True(selection.IsSelected(initialIndex));

        var sorting = new SortingModel();
        sorting.SortingChanged += (_, e) =>
        {
            var comparer = BuildComparer(e.NewDescriptors);
            model.ApplySiblingComparer(comparer, recursive: true);
        };

        var column = new DataGridTextColumn { SortMemberPath = "Name" };
        var adapter = new LocalHierarchicalSortingAdapter(
            sorting,
            () => new[] { column });

        adapter.HandleHeaderClick(column, KeyModifiers.None); // ascending sort

        var newIndex = model.IndexOf(targetItem);
        Assert.NotEqual(initialIndex, newIndex); // moved due to sort
        selection.Clear();
        selection.Select(newIndex);
        Assert.True(selection.IsSelected(newIndex));
        Assert.False(selection.IsSelected(initialIndex));
    }

    [Fact]
    public void FilteringModel_Filters_By_Name_Contains()
    {
        var items = new[]
        {
            new Item("alpha"),
            new Item("beta"),
            new Item("alphabet")
        };

        var filtering = new FilteringModel();
        filtering.SetOrUpdate(new FilteringDescriptor(
            columnId: "col",
            @operator: FilteringOperator.Contains,
            propertyPath: "Name",
            value: "alpha",
            values: null,
            predicate: null,
            culture: null,
            stringComparison: StringComparison.OrdinalIgnoreCase));

        var descriptor = Assert.Single(filtering.Descriptors);
        bool Predicate(Item item)
        {
            var name = item.Name ?? string.Empty;
            return name.IndexOf(descriptor.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var filtered = items.Where(Predicate).ToArray();
        Assert.Equal(2, filtered.Length);
        Assert.Contains(filtered, x => x.Name == "alpha");
        Assert.Contains(filtered, x => x.Name == "alphabet");
    }

    [Fact]
    public void SelectedItem_Maps_To_Underlying_Item_In_Hierarchical_Mode()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.SelectedItem = childB;

        Assert.Same(childB, grid.SelectedItem);
        Assert.Equal(model.IndexOf(childB), grid.SelectedIndex);
        Assert.Same(childB, grid.Selection.SelectedItem);
        Assert.Contains(childB, grid.SelectedItems.Cast<object>());
    }

    [Fact]
    public void SelectedItem_Expands_Ancestors_When_AutoExpandSelectedItem_Enabled()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grand = new Item("grand");
        child.Children.Add(grand);
        root.Children.Add(child);

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = o => ((Item)o).Children,
            ItemPathSelector = o => BuildPath(root, (Item)o)
        });
        model.SetRoot(root);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            AutoExpandSelectedItem = true,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        Assert.Equal(-1, model.IndexOf(grand));

        grid.SelectedItem = grand;

        Assert.NotEqual(-1, model.IndexOf(grand));
        Assert.Same(grand, grid.SelectedItem);
        Assert.Equal(model.IndexOf(grand), grid.SelectedIndex);
    }

    [Fact]
    public void Selection_Persists_On_Rebuild_For_Child_Items()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var roots = new ObservableCollection<Item> { root };
        var model = CreateModel();
        model.SetRoots(roots);
        model.Expand(model.Flattened[0]);

        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            AutoGenerateColumns = false,
            ItemsSource = model.Flattened
        };

        grid.ColumnsInternal.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Avalonia.Data.Binding("Item.Name")
        });

        grid.ApplyTemplate();
        grid.UpdateLayout();

        grid.SelectedItem = childB;
        Assert.Same(childB, grid.SelectedItem);

        model.SetRoots(roots);
        grid.UpdateLayout();

        Assert.Same(childB, grid.SelectedItem);
        Assert.Equal(model.IndexOf(childB), grid.SelectedIndex);
    }

    [Fact]
    public void Refresh_VirtualRoot_Rebuilds_Flattened()
    {
        var roots = new List<Item> { new Item("a"), new Item("b") };
        var model = CreateModel();
        model.SetRoots(roots);

        Assert.Equal(2, model.Count);

        roots.Add(new Item("c"));
        model.Refresh();

        Assert.Equal(3, model.Count);
    }

    [Fact]
    public void Refresh_Maps_Indexes_By_Item()
    {
        var root = new Item("root");
        var childA = new Item("a");
        var childB = new Item("b");
        root.Children.Add(childA);
        root.Children.Add(childB);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);

        var oldIndex = model.IndexOf(childB);

        FlattenedIndexMap? indexMap = null;
        model.FlattenedChanged += (_, e) => indexMap = e.IndexMap;

        model.Refresh(model.Root);

        Assert.NotNull(indexMap);
        Assert.Equal(model.IndexOf(childB), indexMap!.MapOldIndexToNew(oldIndex));
    }

    [Fact]
    public void Rebuild_Preserves_Expanded_State_For_SetRoot()
    {
        var root = new Item("root");
        var child = new Item("child");
        var grandChild = new Item("grand");
        child.Children.Add(grandChild);
        root.Children.Add(child);

        var model = CreateModel();
        model.SetRoot(root);
        model.Expand(model.Root!);
        model.Expand(model.FindNode(child)!);

        Assert.Contains(grandChild, model.Flattened.Select(node => node.Item));

        model.SetRoot(root);

        Assert.Contains(grandChild, model.Flattened.Select(node => node.Item));
        Assert.True(model.FindNode(child)!.IsExpanded);
    }

    private sealed class LocalHierarchicalSortingAdapter : DataGridSortingAdapter
    {
        public LocalHierarchicalSortingAdapter(
            ISortingModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider)
            : base(model, columnProvider, null, null)
        {
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<SortingDescriptor> descriptors,
            IReadOnlyList<SortingDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = true;
            return true;
        }
    }
}
