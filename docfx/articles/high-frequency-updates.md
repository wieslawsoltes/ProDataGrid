# High-Frequency Updates and Large Collections

ProDataGrid can handle very large collections, but the update pattern matters. This article explains the fast-path update behavior for `DataGridCollectionView`, when it applies, and how to keep UI updates constant-time for streaming or frequently changing data.

## Why it matters

When a collection changes, `DataGridCollectionView` normally maintains an internal list to support sorting, filtering, grouping, and paging. For large collections with frequent changes, rebuilding or copying that list can become the bottleneck.

When the view is a straight pass-through, `DataGridCollectionView` now reuses the source list directly. That avoids extra list copies and lets add/remove/move updates flow in O(1).

## Fast-path requirements

The source must meet all of these conditions:

- Implements `IList`.
- Implements `INotifyCollectionChanged`.
- The view has no local transforms:
  - `SortDescriptions.Count == 0`
  - `Filter == null`
  - `GroupDescriptions.Count == 0`
  - `PageSize == 0`

If any of these change at runtime, the view switches back to a local list to keep sorted/filtered/grouped/paged behavior correct.

## Update patterns that stay fast

Use incremental change notifications:

- `Add` with a valid `NewStartingIndex` (or no index so the view can locate the item)
- `Remove` with a valid `OldStartingIndex`
- `Move` with valid old/new indices
- `Replace` with valid `OldStartingIndex`

If indices are missing for remove/move/replace (for example, `OldStartingIndex < 0`), the view falls back to a full refresh to remain consistent.

## Example: rolling window updates

```csharp
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;

var items = new ObservableCollection<StreamingItem>();
dataGrid.ItemsSource = items;

void ApplyBatch()
{
    for (var i = 0; i < 50; i++)
    {
        items.Add(CreateItem());
        if (items.Count > 10000)
        {
            items.RemoveAt(0);
        }
    }
}

// Dispatch to UI thread to keep updates safe.
Dispatcher.UIThread.Post(ApplyBatch);
```

This pattern keeps the view and grid updates bounded to the small set of changed rows.

## When the view falls back to a full refresh

The fast path is disabled (or bypassed) when:

- Sorting, filtering, grouping, or paging is enabled.
- The source does not implement `INotifyCollectionChanged`.
- The collection raises `Reset` or lacks valid indices.

For these cases, consider:

- Reducing the frequency of updates.
- Batching changes and issuing fewer notifications.
- Moving sorting/filtering/grouping upstream (server-side or custom view).

## Sample: Streaming Updates

The DataGrid sample app includes a "Streaming Updates" tab that simulates a rolling window of adds/removes against a large `ObservableCollection`. Use it to validate update behavior and UI responsiveness.

## Related topics

- [ObservableRangeCollection and Performance](observable-range-collection.md)
- [Data Sources and Collection Views](data-sources-and-collection-views.md)
- [Scrolling and Virtualization](scrolling-virtualization.md)
