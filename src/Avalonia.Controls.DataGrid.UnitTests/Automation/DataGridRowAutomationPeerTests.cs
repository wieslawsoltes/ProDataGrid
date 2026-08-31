// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Automation;

public sealed class DataGridRowAutomationPeerTests
{
    [AvaloniaFact]
    public void HierarchicalRow_ExposesExpandCollapseProvider()
    {
        var rootItem = new TreeItem("Root", new TreeItem("Child"));
        var model = CreateModel(rootItem);
        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
        };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = model.Root
        };
        var peer = new DataGridRowAutomationPeer(row);
        var gridPeer = Assert.IsType<DataGridAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(grid));
        IExpandCollapseProvider provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());
        var structureCounts = new List<int>();
        gridPeer.ChildrenChanged += (_, _) => structureCounts.Add(grid.DataConnection.Count);

        Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);

        provider.Expand();

        Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);
        Assert.True(model.Root!.IsExpanded);
        Assert.Equal(new[] { 2 }, structureCounts);

        provider.Collapse();

        Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        Assert.False(model.Root.IsExpanded);
        Assert.Equal(new[] { 2, 1 }, structureCounts);
    }

    [AvaloniaFact]
    public void Provider_Is_Stable_While_Hierarchical_Row_Data_Is_Recycled()
    {
        var model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        model.Expand(model.Root!);
        var grid = new DataGrid { HierarchicalModel = model };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = model.GetNode(1)
        };
        var peer = new DataGridRowAutomationPeer(row);

        IExpandCollapseProvider provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());
        ISelectionItemProvider selectionProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
            peer.GetProvider<ISelectionItemProvider>());
        Assert.Equal(ExpandCollapseState.LeafNode, provider.ExpandCollapseState);

        row.DataContext = model.Root;

        Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

        row.DataContext = "flat row";

        Assert.Same(provider, peer.GetProvider<IExpandCollapseProvider>());
        Assert.Same(selectionProvider, peer.GetProvider<ISelectionItemProvider>());
        Assert.Equal(ExpandCollapseState.LeafNode, provider.ExpandCollapseState);
        Assert.False(selectionProvider.IsSelected);
    }

    [AvaloniaFact]
    public void Provider_Does_Not_Expose_Or_Mutate_A_Node_From_A_Replaced_Model()
    {
        HierarchicalModel oldModel = CreateModel(new TreeItem("Old", new TreeItem("Old child")));
        HierarchicalModel newModel = CreateModel(new TreeItem("New", new TreeItem("New child")));
        var grid = new DataGrid { HierarchicalModel = oldModel };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = oldModel.Root,
        };
        var peer = new DataGridRowAutomationPeer(row);
        IExpandCollapseProvider staleProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());

        grid.HierarchicalModel = newModel;

        Assert.Same(staleProvider, peer.GetProvider<IExpandCollapseProvider>());
        Assert.Equal(ExpandCollapseState.LeafNode, staleProvider.ExpandCollapseState);
        staleProvider.Expand();
        Assert.False(oldModel.Root!.IsExpanded);
        Assert.False(newModel.Root!.IsExpanded);

        row.DataContext = newModel.Root;
        IExpandCollapseProvider currentProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());
        currentProvider.Expand();

        Assert.True(newModel.Root.IsExpanded);
        Assert.False(oldModel.Root.IsExpanded);
    }

    [AvaloniaFact]
    public void Provider_Allows_An_Ownerless_Node_That_Is_The_Current_Model_Root()
    {
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        HierarchicalNode root = model.Root!;
        root.Owner = null;
        var grid = new DataGrid { HierarchicalModel = model };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = root,
        };
        var peer = new DataGridRowAutomationPeer(row);

        IExpandCollapseProvider provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());

        Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
    }

    [AvaloniaFact]
    public void ExpandCollapse_Rejects_Disabled_Rows()
    {
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        var grid = new DataGrid { HierarchicalModel = model };
        var row = new DataGridRow
        {
            IsEnabled = false,
            OwningGrid = grid,
            DataContext = model.Root,
        };
        var peer = new DataGridRowAutomationPeer(row);
        IExpandCollapseProvider provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            peer.GetProvider<IExpandCollapseProvider>());

        Assert.Throws<ElementNotEnabledException>(() => provider.Expand());
        Assert.False(model.Root!.IsExpanded);
    }

    [AvaloniaFact]
    public void RowAndGrid_ExposeStableSelectionAutomation()
    {
        var items = new ObservableCollection<TreeItem>
        {
            new("First"),
            new("Second"),
            new("Third"),
        };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            UseLogicalScrollable = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 420,
            Height = 180,
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(TreeItem.Name)),
        });
        var window = new Window
        {
            Width = 420,
            Height = 180,
        };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        window.UpdateLayout();

        Assert.True(
            grid.DisplayData.NumDisplayedScrollingElements >= 2,
            $"Expected realized rows; actual={grid.DisplayData.NumDisplayedScrollingElements}, " +
            $"gridBounds={grid.Bounds}, windowBounds={window.Bounds}, " +
            $"columns={grid.ColumnsInternal.Count}, items={grid.DataConnection.Count}.");

        var gridPeer = Assert.IsType<DataGridAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(grid));
        ISelectionProvider gridProvider = Assert.IsAssignableFrom<ISelectionProvider>(
            gridPeer.GetProvider<ISelectionProvider>());
        DataGridRow firstRow = Assert.IsType<DataGridRow>(grid.DisplayData.GetDisplayedRow(0));
        DataGridRow secondRow = Assert.IsType<DataGridRow>(grid.DisplayData.GetDisplayedRow(1));
        var firstPeer = Assert.IsType<DataGridRowAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(firstRow));
        var secondPeer = Assert.IsType<DataGridRowAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(secondRow));
        ISelectionItemProvider firstProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
            firstPeer.GetProvider<ISelectionItemProvider>());
        ISelectionItemProvider secondProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
            secondPeer.GetProvider<ISelectionItemProvider>());
        int gridSelectionNotifications = 0;
        int rowSelectionNotifications = 0;
        gridPeer.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectionPatternIdentifiers.SelectionProperty)
            {
                gridSelectionNotifications++;
            }
        };
        secondPeer.PropertyChanged += (_, e) =>
        {
            if (e.Property == SelectionItemPatternIdentifiers.IsSelectedProperty)
            {
                rowSelectionNotifications++;
            }
        };

        firstProvider.Select();
        secondProvider.AddToSelection();

        Assert.True(gridProvider.CanSelectMultiple);
        Assert.True(firstProvider.IsSelected);
        Assert.True(secondProvider.IsSelected);
        Assert.Same(gridProvider, secondProvider.SelectionContainer);
        Assert.Equal(
            new AutomationPeer[] { firstPeer, secondPeer },
            gridProvider.GetSelection().ToArray());
        Assert.True(gridSelectionNotifications >= 2);
        Assert.Equal(1, rowSelectionNotifications);

        secondProvider.RemoveFromSelection();

        Assert.False(secondProvider.IsSelected);
        Assert.Equal(new AutomationPeer[] { firstPeer }, gridProvider.GetSelection().ToArray());
        Assert.Equal(2, rowSelectionNotifications);

        secondProvider.AddToSelection();
        firstProvider.Select();

        Assert.True(firstProvider.IsSelected);
        Assert.False(secondProvider.IsSelected);
        Assert.Equal(new AutomationPeer[] { firstPeer }, gridProvider.GetSelection().ToArray());

        grid.SelectionUnit = DataGridSelectionUnit.Cell;

        Assert.Null(gridPeer.GetProvider<ISelectionProvider>());
        Assert.Null(firstPeer.GetProvider<ISelectionItemProvider>());
        window.Close();
    }

    [AvaloniaFact]
    public void DeepOffscreenSelection_UsesStableLogicalPeer_WithoutRealizingRows()
    {
        var children = Enumerable.Range(0, 180)
            .Select(index => new TreeItem($"Sibling {index}"))
            .ToList();
        var deepParent = new TreeItem("Deep parent", new TreeItem("Deep child"));
        children.Add(deepParent);
        HierarchicalModel model = CreateModel(new TreeItem("Root", children.ToArray()));
        model.Expand(model.Root!);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            SelectionMode = DataGridSelectionMode.Extended,
            UseLogicalScrollable = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Width = 420,
            Height = 120,
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name"),
        });
        var window = new Window
        {
            Width = 420,
            Height = 120,
        };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        window.UpdateLayout();

        Assert.NotEmpty(grid.DisplayData.GetScrollingRows());
        int deepIndex = model.IndexOf(deepParent);
        Assert.True(deepIndex > grid.DisplayData.LastScrollingSlot);
        grid.Selection.Select(deepIndex);

        var gridPeer = Assert.IsType<DataGridAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(grid));
        ISelectionProvider selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(
            gridPeer.GetProvider<ISelectionProvider>());

        AutomationPeer first = Assert.Single(selectionProvider.GetSelection());
        AutomationPeer second = Assert.Single(selectionProvider.GetSelection());
        var offscreenPeer = Assert.IsType<DataGridUnrealizedRowAutomationPeer>(first);
        Assert.Same(first, second);
        Assert.True(offscreenPeer.IsOffscreen());
        Assert.Equal(AutomationControlType.TreeItem, offscreenPeer.GetAutomationControlType());
        ISelectionItemProvider itemProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(
            offscreenPeer.GetProvider<ISelectionItemProvider>());
        IExpandCollapseProvider expandProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            offscreenPeer.GetProvider<IExpandCollapseProvider>());
        Assert.True(itemProvider.IsSelected);
        Assert.Equal(ExpandCollapseState.Collapsed, expandProvider.ExpandCollapseState);
        Assert.Same(selectionProvider, itemProvider.SelectionContainer);

        int realizedRowsBefore = grid.DisplayData.GetScrollingRows().Count();
        offscreenPeer.BringIntoView();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        int targetSlot = grid.SlotFromRowIndex(deepIndex);
        Assert.True(
            targetSlot >= grid.DisplayData.FirstScrollingSlot &&
            targetSlot <= grid.DisplayData.LastScrollingSlot,
            $"Target slot {targetSlot} was not realized; viewport is " +
            $"[{grid.DisplayData.FirstScrollingSlot}, {grid.DisplayData.LastScrollingSlot}], " +
            $"count={grid.DataConnection.Count}, estimatedHeight={grid.CellsEstimatedHeight}, " +
            $"attached={grid.IsAttachedToVisualTree()}, visible={grid.IsVisible}, " +
            $"bounds={grid.Bounds}, presenterHeight={grid.ActualRowsPresenterHeight}, " +
            $"presenterAvailable={grid.RowsPresenterAvailableSize}.");
        DataGridRow targetRow = Assert.IsType<DataGridRow>(
            grid.DisplayData.GetDisplayedElement(targetSlot));
        Assert.Same(model.FindNode(deepParent), targetRow.DataContext);
        AutomationPeer promotedPeer = Assert.Single(selectionProvider.GetSelection());
        Assert.IsType<DataGridRowAutomationPeer>(promotedPeer);
        Assert.NotSame(offscreenPeer, promotedPeer);
        Assert.False(promotedPeer.IsOffscreen());
        Assert.InRange(
            grid.DisplayData.GetScrollingRows().Count(),
            1,
            realizedRowsBefore + 1);

        grid.IsEnabled = false;
        Assert.Throws<ElementNotEnabledException>(() => expandProvider.Expand());
        Assert.Throws<ElementNotEnabledException>(() =>
            itemProvider.RemoveFromSelection());
        grid.IsEnabled = true;

        expandProvider.Expand();

        Assert.True(model.FindNode(deepParent)!.IsExpanded);
        Assert.Equal(ExpandCollapseState.Expanded, expandProvider.ExpandCollapseState);

        window.Close();
    }

    [AvaloniaFact]
    public void RemovingLogicalPeerFromCache_ReleasesNodeSubscription()
    {
        var target = new TreeItem("Target", new TreeItem("Child"));
        var children = Enumerable.Range(0, 80)
            .Select(index => new TreeItem($"Sibling {index}"))
            .Append(target)
            .ToArray();
        HierarchicalModel model = CreateModel(new TreeItem("Root", children));
        model.Expand(model.Root!);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            SelectionMode = DataGridSelectionMode.Extended,
        };
        int targetIndex = model.IndexOf(target);
        grid.Selection.Select(targetIndex);
        var gridPeer = new DataGridAutomationPeer(grid);
        var peer = Assert.IsType<DataGridUnrealizedRowAutomationPeer>(
            Assert.Single(gridPeer.GetSelection()));
        int expandCollapseNotifications = 0;
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
            {
                expandCollapseNotifications++;
            }
        };

        ISelectionItemProvider selection = Assert.IsAssignableFrom<ISelectionItemProvider>(
            peer.GetProvider<ISelectionItemProvider>());
        selection.RemoveFromSelection();

        Assert.Empty(gridPeer.GetSelection());
        model.FindNode(target)!.IsLoading = true;
        Assert.Equal(0, expandCollapseNotifications);

        grid.Selection.Select(targetIndex);
        var replacement = Assert.IsType<DataGridUnrealizedRowAutomationPeer>(
            Assert.Single(gridPeer.GetSelection()));
        Assert.NotSame(peer, replacement);
        int replacementNotifications = 0;
        replacement.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
            {
                replacementNotifications++;
            }
        };

        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        model.FindNode(target)!.IsLoading = false;

        Assert.Equal(0, replacementNotifications);
        Assert.Null(gridPeer.GetProvider<ISelectionProvider>());
    }

    [AvaloniaFact]
    public void OffscreenLeaf_ExposesStableLeafExpandCollapseProvider()
    {
        var children = Enumerable.Range(0, 80)
            .Select(index => new TreeItem($"Leaf {index}"))
            .ToArray();
        HierarchicalModel model = CreateModel(new TreeItem("Root", children));
        model.Expand(model.Root!);
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            UseLogicalScrollable = true,
            Width = 320,
            Height = 100,
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name"),
        });
        var window = new Window
        {
            Width = 320,
            Height = 100,
            Content = grid,
        };
        window.SetThemeStyles();
        window.Show();
        window.UpdateLayout();

        int leafIndex = model.IndexOf(children[^1]);
        grid.Selection.Select(leafIndex);
        var gridPeer = Assert.IsType<DataGridAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(grid));
        ISelectionProvider selectionProvider = Assert.IsAssignableFrom<ISelectionProvider>(
            gridPeer.GetProvider<ISelectionProvider>());
        var leafPeer = Assert.IsType<DataGridUnrealizedRowAutomationPeer>(
            Assert.Single(selectionProvider.GetSelection()));

        Assert.Equal(AutomationControlType.TreeItem, leafPeer.GetAutomationControlType());
        IExpandCollapseProvider expandCollapse = Assert.IsAssignableFrom<
            IExpandCollapseProvider>(leafPeer.GetProvider<IExpandCollapseProvider>());
        Assert.Equal(ExpandCollapseState.LeafNode, expandCollapse.ExpandCollapseState);
        Assert.NotNull(leafPeer.GetProvider<ISelectionItemProvider>());

        window.Close();
    }

    [AvaloniaFact]
    public void OffscreenNodeWorkerCallback_IsMarshaledToOwnerThread()
    {
        int ownerThreadId = Environment.CurrentManagedThreadId;
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            Width = 320,
            Height = 100,
        };
        var window = new Window
        {
            Width = 320,
            Height = 100,
            Content = grid,
        };
        window.SetThemeStyles();
        window.Show();
        window.UpdateLayout();
        Assert.True(grid.IsAttachedToVisualTree());
        var gridPeer = new DataGridAutomationPeer(grid);
        var peer = new DataGridUnrealizedRowAutomationPeer(gridPeer, model.Root!, rowIndex: 0);
        int callbackThreadId = -1;
        int stateNotifications = 0;
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
            {
                callbackThreadId = Environment.CurrentManagedThreadId;
                stateNotifications++;
            }
        };

        using var changed = new ManualResetEventSlim();
        Task worker = Task.Run(() =>
        {
            model.Root!.IsLoading = true;
            changed.Set();
        });
        Assert.True(changed.Wait(TimeSpan.FromSeconds(5)));
        worker.GetAwaiter().GetResult();

        Assert.Equal(0, stateNotifications);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, stateNotifications);
        Assert.Equal(ownerThreadId, callbackThreadId);
        Assert.Equal(
            ExpandCollapseState.PartiallyExpanded,
            Assert.IsAssignableFrom<IExpandCollapseProvider>(
                peer.GetProvider<IExpandCollapseProvider>()).ExpandCollapseState);
        window.Close();
    }

    [AvaloniaFact]
    public void ModelCallbackThreadAccess_FollowsDetachAndReattach()
    {
        int ownerThreadId = Environment.CurrentManagedThreadId;
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        var grid = new DataGrid
        {
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            Width = 320,
            Height = 100,
        };
        var window = new Window
        {
            Width = 320,
            Height = 100,
            Content = grid,
        };
        window.SetThemeStyles();
        window.Show();
        window.UpdateLayout();

        Assert.True(grid.IsAttachedToVisualTree());
        Assert.True(grid.HasModelCallbackThreadAccess());
        (int attachedThreadId, bool attachedAccess, bool dispatcherAccess) =
            RunOnDedicatedThread(() =>
                (grid.HasModelCallbackThreadAccess(), Dispatcher.UIThread.CheckAccess()));
        Assert.NotEqual(ownerThreadId, attachedThreadId);
        Assert.Equal(dispatcherAccess, attachedAccess);

        window.Content = null;
        window.UpdateLayout();

        Assert.False(grid.IsAttachedToVisualTree());
        Assert.True(grid.HasModelCallbackThreadAccess());
        (int detachedThreadId, bool detachedAccess, _) = RunOnDedicatedThread(() =>
            (grid.HasModelCallbackThreadAccess(), Dispatcher.UIThread.CheckAccess()));
        Assert.NotEqual(ownerThreadId, detachedThreadId);
        Assert.False(detachedAccess);

        window.Content = grid;
        window.UpdateLayout();

        Assert.True(grid.IsAttachedToVisualTree());
        Assert.True(grid.HasModelCallbackThreadAccess());
        (int reattachedThreadId, bool reattachedAccess, bool reattachedDispatcherAccess) =
            RunOnDedicatedThread(() =>
                (grid.HasModelCallbackThreadAccess(), Dispatcher.UIThread.CheckAccess()));
        Assert.NotEqual(ownerThreadId, reattachedThreadId);
        Assert.Equal(reattachedDispatcherAccess, reattachedAccess);

        window.Close();
    }

    [AvaloniaFact]
    public void HierarchicalFilter_RaisesCommittedStructureChanges_ForCountAndIdentity()
    {
        var alpha = new TreeItem("alpha");
        var beta = new TreeItem("beta");
        var gamma = new TreeItem("gamma");
        HierarchicalModel hierarchy = CreateModel(
            new TreeItem("root", alpha, beta, gamma));
        hierarchy.Expand(hierarchy.Root!);
        var filtering = new FilteringModel();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            FilteringModel = filtering,
            HierarchicalModel = hierarchy,
            HierarchicalRowsEnabled = true,
            ItemsSource = hierarchy.ObservableFlattened,
        };
        var column = new DataGridHierarchicalColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name"),
        };
        grid.ColumnsInternal.Add(column);
        var gridPeer = Assert.IsType<DataGridAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(grid));
        var committedCounts = new List<int>();
        gridPeer.ChildrenChanged += (_, _) => committedCounts.Add(grid.DataConnection.Count);
        gridPeer.RaiseHierarchyStructureChanged();
        committedCounts.Clear();

        filtering.SetOrUpdate(CreateNameFilter(column, "alpha"));

        Assert.Equal(1, grid.DataConnection.Count);
        Assert.Equal(new[] { "alpha" }, VisibleNames(grid));

        filtering.SetOrUpdate(CreateNameFilter(column, "beta"));

        Assert.Equal(1, grid.DataConnection.Count);
        Assert.Equal(new[] { "beta" }, VisibleNames(grid));

        filtering.Clear();

        Assert.Equal(4, grid.DataConnection.Count);
        Assert.Equal(new[] { "root", "alpha", "beta", "gamma" }, VisibleNames(grid));
        Assert.Equal(new[] { 1, 1, 4 }, committedCounts);
    }

    [AvaloniaFact]
    public void WorkerNodeCallbacks_AreMarshaled_AndIgnoreRecycledIdentity()
    {
        int ownerThreadId = Environment.CurrentManagedThreadId;
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        var grid = new DataGrid
        {
            HierarchicalModel = model,
        };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = model.Root,
        };
        var rowPeer = new DataGridRowAutomationPeer(row);
        int stateNotifications = 0;
        int callbackThreadId = -1;
        rowPeer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
            {
                callbackThreadId = Environment.CurrentManagedThreadId;
                stateNotifications++;
            }
        };

        int workerThreadId = -1;
        using var firstChanged = new ManualResetEventSlim();
        using var firstRelease = new ManualResetEventSlim();
        Task firstWorker = Task.Run(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            model.Root!.IsLoading = true;
            firstChanged.Set();
            firstRelease.Wait();
        });
        Assert.True(firstChanged.Wait(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(ownerThreadId, workerThreadId);

        // Recycle the peer to a leaf before queued root notifications run. Only the grid's
        // committed structure event may survive; stale root state must not update the row peer.
        HierarchicalModel leafModel = CreateModel(new TreeItem("Leaf"));
        row.DataContext = leafModel.Root;
        stateNotifications = 0;
        firstRelease.Set();
        firstWorker.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, stateNotifications);
        Assert.Equal(
            ExpandCollapseState.LeafNode,
            Assert.IsAssignableFrom<IExpandCollapseProvider>(
                rowPeer.GetProvider<IExpandCollapseProvider>()).ExpandCollapseState);

        row.DataContext = model.Root;
        stateNotifications = 0;
        callbackThreadId = -1;

        using var secondChanged = new ManualResetEventSlim();
        Task secondWorker = Task.Run(() =>
        {
            workerThreadId = Environment.CurrentManagedThreadId;
            model.Root!.IsLoading = false;
            secondChanged.Set();
        });
        Assert.True(secondChanged.Wait(TimeSpan.FromSeconds(5)));
        secondWorker.GetAwaiter().GetResult();

        Assert.Equal(0, stateNotifications);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, stateNotifications);
        Assert.Equal(ownerThreadId, callbackThreadId);
    }

    [AvaloniaFact]
    public void External_Model_Does_Not_Retain_Automation_Peers_Or_Controls()
    {
        HierarchicalModel model = CreateModel(new TreeItem("Root", new TreeItem("Child")));
        (WeakReference<DataGrid> grid, WeakReference<DataGridRow> row,
            WeakReference<DataGridAutomationPeer> gridPeer,
            WeakReference<DataGridRowAutomationPeer> rowPeer,
            WeakReference<DataGrid> unrealizedGrid,
            WeakReference<DataGridAutomationPeer> unrealizedGridPeer,
            WeakReference<DataGridUnrealizedRowAutomationPeer> unrealizedPeer) =
            CreateAutomationReferences(model);

        Dispatcher.UIThread.RunJobs();
        ForceGc();

        bool gridAlive = grid.TryGetTarget(out _);
        bool rowAlive = row.TryGetTarget(out _);
        bool gridPeerAlive = gridPeer.TryGetTarget(out _);
        bool rowPeerAlive = rowPeer.TryGetTarget(out _);
        bool unrealizedGridAlive = unrealizedGrid.TryGetTarget(out _);
        bool unrealizedGridPeerAlive = unrealizedGridPeer.TryGetTarget(out _);
        bool unrealizedPeerAlive = unrealizedPeer.TryGetTarget(out _);
        Assert.False(
            gridAlive || rowAlive || gridPeerAlive || rowPeerAlive ||
            unrealizedGridAlive || unrealizedGridPeerAlive || unrealizedPeerAlive,
            $"Unexpected retained objects: grid={gridAlive}, row={rowAlive}, " +
            $"gridPeer={gridPeerAlive}, rowPeer={rowPeerAlive}, " +
            $"unrealizedGrid={unrealizedGridAlive}, " +
            $"unrealizedGridPeer={unrealizedGridPeerAlive}, " +
            $"unrealizedPeer={unrealizedPeerAlive}.");

        // Exercise the surviving external event source after collection. A stale strong
        // subscription would either keep the controls alive or invoke a leaked peer here.
        model.Expand(model.Root!);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (
        WeakReference<DataGrid> Grid,
        WeakReference<DataGridRow> Row,
        WeakReference<DataGridAutomationPeer> GridPeer,
        WeakReference<DataGridRowAutomationPeer> RowPeer,
        WeakReference<DataGrid> UnrealizedGrid,
        WeakReference<DataGridAutomationPeer> UnrealizedGridPeer,
        WeakReference<DataGridUnrealizedRowAutomationPeer> UnrealizedPeer) CreateAutomationReferences(
            HierarchicalModel model)
    {
        var grid = new DataGrid { HierarchicalModel = model };
        var row = new DataGridRow
        {
            OwningGrid = grid,
            DataContext = model.Root,
        };
        var gridPeer = new DataGridAutomationPeer(grid);
        var rowPeer = new DataGridRowAutomationPeer(row);
        _ = rowPeer.GetProvider<IExpandCollapseProvider>();
        var unrealizedGrid = new DataGrid();
        object[] unrealizedSource = { new object() };
        unrealizedGrid.DataConnection.DataSource = unrealizedSource;
        unrealizedGrid.Selection.Source = unrealizedSource;
        var unrealizedGridPeer = new DataGridAutomationPeer(unrealizedGrid);
        unrealizedGrid.Selection.Select(0);
        var unrealizedPeer = Assert.IsType<DataGridUnrealizedRowAutomationPeer>(
            Assert.Single(unrealizedGridPeer.GetSelection()));
        // Keep the peer in the grid's cache while attaching it to the externally-owned node.
        // This isolates the peer's weak node subscription from collection-view item tracking.
        unrealizedPeer.Update(model.Root!, rowIndex: 0);
        var result = (
            Grid: new WeakReference<DataGrid>(grid),
            Row: new WeakReference<DataGridRow>(row),
            GridPeer: new WeakReference<DataGridAutomationPeer>(gridPeer),
            RowPeer: new WeakReference<DataGridRowAutomationPeer>(rowPeer),
            UnrealizedGrid: new WeakReference<DataGrid>(unrealizedGrid),
            UnrealizedGridPeer: new WeakReference<DataGridAutomationPeer>(unrealizedGridPeer),
            UnrealizedPeer: new WeakReference<DataGridUnrealizedRowAutomationPeer>(unrealizedPeer));
        grid = null!;
        row = null!;
        gridPeer = null!;
        rowPeer = null!;
        unrealizedGrid = null!;
        unrealizedGridPeer = null!;
        unrealizedPeer = null!;
        return result;
    }

    private static FilteringDescriptor CreateNameFilter(
        DataGridColumn column,
        string expectedName)
    {
        return new FilteringDescriptor(
            column,
            FilteringOperator.Custom,
            predicate: item =>
                item is HierarchicalNode node &&
                node.Item is TreeItem treeItem &&
                string.Equals(treeItem.Name, expectedName, StringComparison.Ordinal));
    }

    private static string[] VisibleNames(DataGrid grid)
    {
        return grid.DataConnection.CollectionView
            .Cast<HierarchicalNode>()
            .Select(node => ((TreeItem)node.Item).Name)
            .ToArray();
    }

    private static void ForceGc()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static (int ThreadId, bool Access, bool DispatcherAccess) RunOnDedicatedThread(
        Func<(bool Access, bool DispatcherAccess)> action)
    {
        (int ThreadId, bool Access, bool DispatcherAccess) result = default;
        var thread = new Thread(() =>
        {
            (bool access, bool dispatcherAccess) = action();
            result = (Environment.CurrentManagedThreadId, access, dispatcherAccess);
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        return result;
    }

    private static HierarchicalModel CreateModel(TreeItem root)
    {
        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((TreeItem)item).Children,
            IsLeafSelector = item => ((TreeItem)item).Children.Count == 0
        });
        model.SetRoot(root);
        return model;
    }

    private sealed class TreeItem
    {
        public TreeItem(string name, params TreeItem[] children)
        {
            Name = name;
            Children = children;
        }

        public string Name { get; }

        public IReadOnlyList<TreeItem> Children { get; }
    }
}
