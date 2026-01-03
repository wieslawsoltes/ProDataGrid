# DynamicData Hierarchical Streaming with SourceList

This guide shows how to drive hierarchical roots from a DynamicData `SourceList<T>` while keeping sorting, filtering, and search upstream. The grid stays in the hierarchical fast path because view transforms never touch the flattened list.

## Pipeline setup

```csharp
var source = new SourceList<TreeItem>();
var sortSubject = new BehaviorSubject<IComparer<TreeItem>>(sortComparer);
var treeFilterSubject = new BehaviorSubject<Func<TreeItem, bool>>(static _ => true);

var subscription = source.Connect()
    .Filter(treeFilterSubject)
    .Sort(sortSubject)
    .Bind(out ReadOnlyObservableCollection<TreeItem> roots)
    .Subscribe();
```

## Hierarchical model setup

```csharp
var options = new HierarchicalOptions<TreeItem>
{
    ChildrenSelector = item => item.Children,
    IsLeafSelector = item => item.Children.Count == 0,
    IsExpandedSelector = item => item.IsExpanded,
    IsExpandedSetter = (item, value) => item.IsExpanded = value
};

Model = new HierarchicalModel<TreeItem>(options);
Model.SetRoots(roots);
```

## Wiring sorting, filtering, and search

Use adapter factories to translate model descriptors into DynamicData predicates, then combine them into a tree-aware predicate:

```csharp
void UpdateTreePredicate()
{
    var filterItem = filteringFactory.FilterItemPredicate;
    var searchItem = searchFactory.SearchItemPredicate;

    bool NodeMatches(TreeItem item) => filterItem(item) && searchItem(item);
    Func<TreeItem, bool> treePredicate = item => MatchesAny(item, NodeMatches);

    treeFilterSubject.OnNext(treePredicate);
    Model.Refresh();
}
```

`MatchesAny` is a helper that returns true when the node or any descendant satisfies the predicate.

When filtering is active, use the same tree predicate in your `ChildrenSelector` to trim branches so only matching nodes (and their ancestors) remain visible.

## Sample

See the **DynamicData Hierarchical (SourceList)** tab in the sample gallery for a full implementation.
