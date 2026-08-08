// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using DynamicData;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedHierarchyNode), ProviderName = "GeneratedHierarchyNodeSchema", Streaming = true)]
[GenerateDataGridController(
    typeof(GeneratedHierarchyNode),
    "TreeRoots",
    ProviderName = "GeneratedHierarchyNodeSchema",
    SourceMember = nameof(_source),
    SourceKind = DataGridGeneratedSourceKind.DynamicDataSourceCache,
    Features = DataGridGeneratedFeatures.Columns |
               DataGridGeneratedFeatures.Sorting |
               DataGridGeneratedFeatures.Filtering |
               DataGridGeneratedFeatures.Searching |
               DataGridGeneratedFeatures.Selection |
               DataGridGeneratedFeatures.State |
               DataGridGeneratedFeatures.Hierarchy |
               DataGridGeneratedFeatures.Diagnostics,
    OperationExecution = DataGridOperationExecution.ExternalPipeline,
    Streaming = true)]
[GenerateDataGridView(
    typeof(GeneratedHierarchyNode),
    ViewName = "GeneratedHierarchicalDynamicDataGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.SearchableGrid,
    Title = "Generated hierarchical DynamicData pipeline",
    AutomationId = "generated-hierarchical-dynamic-data-grid",
    ControllerName = "TreeRoots",
    SortingModelPropertyName = nameof(SortingModel),
    FilteringModelPropertyName = nameof(FilteringModel),
    SearchModelPropertyName = nameof(SearchModel),
    SearchTextPropertyName = nameof(Query),
    HierarchicalModelPropertyName = nameof(HierarchicalModel))]
public sealed partial class GeneratedHierarchicalDynamicDataViewModel : ReactiveObject, IDisposable
{
    private static readonly string[] s_desks = ["Warsaw", "London", "New York"];

    private readonly SourceCache<GeneratedHierarchyNode, int> _source = new(static node => node.Id);
    private readonly CompositeDisposable _subscriptions = new();
    private readonly ReadOnlyObservableCollection<GeneratedHierarchyNode> _items;
    private readonly INotifyCollectionChanged _rootNotifications;
    private int _nextId;
    private bool _disposed;

    [Reactive]
    private string _query = string.Empty;

    [Reactive]
    private string _status = "Generated typed children, keys, expansion, wrapper bindings, and root operations are active.";

    [Reactive]
    private int _sourceRootCount;

    [Reactive]
    private int _visibleRootCount;

    [Reactive]
    private int _nodeCount;

    [Reactive]
    private int _visibleNodeCount;

    [Reactive]
    private int _replacementCount;

    [Reactive]
    private int _errorCount;

    public GeneratedHierarchicalDynamicDataViewModel()
    {
        InitializeTreeRoots(CreateTreeRootsController());
        SortingModel.MultiSort = true;
        SortingModel.CycleMode = SortCycleMode.AscendingDescendingNone;
        SearchModel.HighlightMode = SearchHighlightMode.TextAndCell;

        HierarchyController = GeneratedHierarchyNodeSchema.CreateHierarchyController();
        HierarchicalModel = new HierarchicalModel<GeneratedHierarchyNode>(
            GeneratedHierarchyNodeSchema.CreateHierarchicalOptions());

        AddRootBatchCommand = ReactiveCommand.Create(() => AddRootBatch(2));
        AddChildCommand = ReactiveCommand.Create(AddChildToFirstRoot);
        RefreshRootsCommand = ReactiveCommand.Create(RefreshRootsPreservingExpansion);
        ExpandAllCommand = ReactiveCommand.Create(ExpandAll);
        CollapseAllCommand = ReactiveCommand.Create(CollapseAll);
        SortPriceDescendingCommand = ReactiveCommand.Create(SortPriceDescending);
        ApplyWarsawFilterCommand = ReactiveCommand.Create(ApplyWarsawFilter);
        ClearOperationsCommand = ReactiveCommand.Create(ClearOperations);

        _items = ConnectTreeRootsPipeline();
        _rootNotifications = _items;
        _rootNotifications.CollectionChanged += RootCollectionOnChanged;
        HierarchicalModel.FlattenedChangedTyped += HierarchicalModelOnFlattenedChanged;
        HierarchicalModel.SetRoots(_items);

        _subscriptions.Add(Changed
            .Where(static change => change.PropertyName == nameof(Query))
            .Select(_ => Query)
            .Subscribe(ApplySearch));
        _subscriptions.Add(TreeRootsErrors.Subscribe(HandlePipelineError));

        TreeRoots.SetSorting(
        [
            GeneratedHierarchyNodeSchema.Price.Ascending(),
            GeneratedHierarchyNodeSchema.Name.Ascending()
        ]);
        AddRootBatch(4);
        UpdateCounts();
    }

    public ReadOnlyObservableCollection<GeneratedHierarchyNode> Items => _items;

    public SortingModel SortingModel => TreeRoots.SortingModel;

    public FilteringModel FilteringModel => TreeRoots.FilteringModel;

    public SearchModel SearchModel => TreeRoots.SearchModel;

    public DataGridGeneratedHierarchyController<GeneratedHierarchyNode, int> HierarchyController { get; }

    public HierarchicalModel<GeneratedHierarchyNode> HierarchicalModel { get; }

    public bool IsDisposed => _disposed;

    public ReactiveCommand<RxVoid, RxVoid> AddRootBatchCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddChildCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RefreshRootsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ExpandAllCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CollapseAllCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SortPriceDescendingCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ApplyWarsawFilterCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ClearOperationsCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _rootNotifications.CollectionChanged -= RootCollectionOnChanged;
        HierarchicalModel.FlattenedChangedTyped -= HierarchicalModelOnFlattenedChanged;
        HierarchicalModel.SetRoots(Array.Empty<GeneratedHierarchyNode>());
        _subscriptions.Dispose();
        DisposeTreeRoots();
        _source.Dispose();
    }

    private void AddRootBatch(int count)
    {
        _source.Edit(cache =>
        {
            for (int index = 0; index < count; index++)
            {
                cache.AddOrUpdate(CreateRoot());
            }
        });

        SourceRootCount += count;
        UpdateCounts();
        Status = $"Added {count} generated root trees; source now contains {SourceRootCount} roots.";
    }

    private void AddChildToFirstRoot()
    {
        if (Items.Count == 0)
        {
            Status = "No visible root is available for a child update.";
            return;
        }

        GeneratedHierarchyNode root = Items[0];
        int childIndex = root.Children.Count + 1;
        root.Children.Add(CreateNode(
            id: ++_nextId,
            parentId: root.Id,
            name: $"{root.Name} / live child {childIndex}",
            price: root.Price + childIndex * 3.25m,
            quantity: root.Quantity + childIndex * 25,
            isExpanded: false));
        UpdateCounts();
        Status = $"Added child key {_nextId} through the generated typed children selector.";
    }

    private void RefreshRootsPreservingExpansion()
    {
        HashSet<int> expandedKeys = HierarchyController.CaptureExpanded(Items);
        var replacements = new List<GeneratedHierarchyNode>(Items.Count);
        for (int index = 0; index < Items.Count; index++)
        {
            replacements.Add(CloneForRefresh(Items[index]));
        }

        HierarchyController.RestoreExpanded(replacements, expandedKeys);
        _source.Edit(cache =>
        {
            for (int index = 0; index < replacements.Count; index++)
            {
                cache.AddOrUpdate(replacements[index]);
            }
        });

        ReplacementCount += replacements.Count;
        UpdateCounts();
        Status = $"Replaced {replacements.Count} roots and restored {expandedKeys.Count} expansion keys.";
    }

    private void ExpandAll()
    {
        HierarchicalModel.ExpandAll();
        HierarchyController.ExpandAll(Items);
        UpdateCounts();
        Status = $"Expanded all {NodeCount} validated nodes by generated stable keys.";
    }

    private void CollapseAll()
    {
        HierarchicalModel.CollapseAll();
        HierarchyController.CollapseAll(Items);
        UpdateCounts();
        Status = $"Collapsed the hierarchy to {VisibleNodeCount} visible root rows.";
    }

    private void SortPriceDescending()
    {
        TreeRoots.SetSorting(
        [
            GeneratedHierarchyNodeSchema.Price.Descending(),
            GeneratedHierarchyNodeSchema.Name.Ascending()
        ]);
        Status = $"Compiled root price ordering upstream at revision {TreeRoots.Version}.";
    }

    private void ApplyWarsawFilter()
    {
        TreeRoots.SetFiltering(
        [
            GeneratedHierarchyNodeSchema.Desk.EqualTo("Warsaw"),
            GeneratedHierarchyNodeSchema.Price.GreaterThanOrEqual(80m)
        ]);
        Status = $"Compiled Warsaw root filtering upstream at revision {TreeRoots.Version}.";
    }

    private void ClearOperations()
    {
        Query = string.Empty;
        TreeRoots.ClearOperations();
        Status = $"Cleared hierarchical root operations at revision {TreeRoots.Version}.";
    }

    private void ApplySearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            TreeRoots.SetSearching(Array.Empty<SearchDescriptor>());
            return;
        }

        TreeRoots.SetSearching(
        [
            GeneratedHierarchyNodeSchema.Name.Search(query, comparison: StringComparison.OrdinalIgnoreCase),
            GeneratedHierarchyNodeSchema.Desk.Search(query, comparison: StringComparison.OrdinalIgnoreCase)
        ]);
        Status = $"Compiled root search '{query}' upstream at revision {TreeRoots.Version}.";
    }

    private GeneratedHierarchyNode CreateRoot()
    {
        int rootId = ++_nextId;
        var root = CreateNode(
            rootId,
            parentId: null,
            name: $"Root {rootId}",
            price: 45m + rootId * 13.5m,
            quantity: 500 + rootId * 100,
            isExpanded: true);

        for (int childIndex = 1; childIndex <= 3; childIndex++)
        {
            int childId = ++_nextId;
            var child = CreateNode(
                childId,
                rootId,
                $"Root {rootId} / child {childIndex}",
                root.Price + childIndex * 4.75m,
                root.Quantity + childIndex * 50,
                isExpanded: childIndex == 1);
            root.Children.Add(child);

            if (childIndex == 1)
            {
                int grandchildId = ++_nextId;
                child.Children.Add(CreateNode(
                    grandchildId,
                    childId,
                    $"Root {rootId} / child {childIndex} / leaf",
                    child.Price + 2.5m,
                    child.Quantity + 25,
                    isExpanded: false));
            }
        }

        return root;
    }

    private static GeneratedHierarchyNode CreateNode(
        int id,
        int? parentId,
        string name,
        decimal price,
        int quantity,
        bool isExpanded) =>
        new()
        {
            Id = id,
            ParentId = parentId,
            Name = name,
            Desk = s_desks[id % s_desks.Length],
            Price = price,
            Quantity = quantity,
            UpdatedAt = new DateTimeOffset(2026, 8, 8, 15, 0, 0, TimeSpan.Zero).AddSeconds(id),
            IsExpanded = isExpanded
        };

    private static GeneratedHierarchyNode CloneForRefresh(GeneratedHierarchyNode source)
    {
        var replacement = new GeneratedHierarchyNode
        {
            Id = source.Id,
            ParentId = source.ParentId,
            Name = source.Name,
            Desk = source.Desk,
            Price = source.Price + 1m,
            Quantity = source.Quantity + 10,
            UpdatedAt = source.UpdatedAt.AddMinutes(1),
            IsExpanded = source.IsExpanded
        };
        for (int index = 0; index < source.Children.Count; index++)
        {
            replacement.Children.Add(CloneForRefresh(source.Children[index]));
        }
        return replacement;
    }

    private void RootCollectionOnChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCounts();

    private void HierarchicalModelOnFlattenedChanged(object? sender, FlattenedChangedEventArgs<GeneratedHierarchyNode> e) =>
        VisibleNodeCount = HierarchicalModel.ObservableFlattened.Count;

    private void UpdateCounts()
    {
        VisibleRootCount = Items.Count;
        NodeCount = Items.Count == 0 ? 0 : HierarchyController.Validate(Items);
        VisibleNodeCount = HierarchicalModel.ObservableFlattened.Count;
    }

    private void HandlePipelineError(Exception error)
    {
        ErrorCount++;
        Status = $"Generated hierarchy pipeline error {ErrorCount}: {error.Message}";
    }
}
