# Hierarchical High-Frequency Updates

HierarchicalModel maintains a flattened list of visible nodes. When child collections report incremental changes with indices, the model can update the flattened view in constant time instead of rebuilding entire subtrees.

## Fast-path requirements

Each expanded node stays on the fast path when:

- Children collections implement `IList` and `INotifyCollectionChanged`.
- Change notifications include indices:
  - `Add` uses `NewStartingIndex`.
  - `Remove`/`Replace` use `OldStartingIndex`.
  - `Move` supplies both old and new indices.
- No per-parent sorting is applied (`SiblingComparer`/`SiblingComparerSelector` are null).
- Collections avoid `Reset` in favor of incremental add/remove/move/replace.

## Update patterns that stay fast

Batch updates with `ObservableRangeCollection<T>` to avoid per-item churn:

```csharp
var roots = new ObservableRangeCollection<TreeItem>();

var options = new HierarchicalOptions<TreeItem>
{
    ChildrenSelector = item => item.Children,
    IsExpandedSelector = item => item.IsExpanded,
    IsExpandedSetter = (item, value) => item.IsExpanded = value
};

var model = new HierarchicalModel<TreeItem>(options);
model.SetRoots(roots);

roots.AddRange(CreateBatch());
roots.RemoveRange(0, 25);
```

## Expanded vs collapsed nodes

When a parent node is collapsed, the model updates its expanded counts but does not touch the flattened list until the node becomes visible. This keeps hidden updates cheap while preserving correct counts for future expansion.

## When the model refreshes

The model falls back to a refresh when:

- A child collection raises `Reset`.
- The change notification lacks indices.
- A `SiblingComparer` or `SiblingComparerSelector` is active.
- The children source is not list-like or does not notify changes.

## Sample

See the **Hierarchical Streaming Updates** tab in the sample gallery for a full implementation.

## Related topics

- [ObservableRangeCollection and Performance](observable-range-collection.md)
- [Hierarchical Data](hierarchical-data.md)
- [High-Frequency Updates](high-frequency-updates.md)
