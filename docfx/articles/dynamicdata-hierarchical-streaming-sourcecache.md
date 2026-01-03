# DynamicData Hierarchical Streaming with SourceCache

Use `SourceCache<T, TKey>` when you have stable keys and need efficient add/update/remove operations for hierarchical roots. Sorting and filtering still happen in the DynamicData pipeline so the grid stays in the hierarchical fast path.

## Pipeline setup

```csharp
var cache = new SourceCache<TreeItem, int>(item => item.Id);
var sortSubject = new BehaviorSubject<IComparer<TreeItem>>(sortComparer);
var treeFilterSubject = new BehaviorSubject<Func<TreeItem, bool>>(static _ => true);

var subscription = cache.Connect()
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

## Rolling window updates

```csharp
var idQueue = new Queue<int>();

cache.Edit(updater =>
{
    for (var i = 0; i < batchSize; i++)
    {
        var item = CreateItem();
        updater.AddOrUpdate(item);
        idQueue.Enqueue(item.Id);
    }

    while (idQueue.Count > targetCount)
    {
        updater.RemoveKey(idQueue.Dequeue());
    }
});
```

## Sample

See the **DynamicData Hierarchical (SourceCache)** tab in the sample gallery for a full implementation.
