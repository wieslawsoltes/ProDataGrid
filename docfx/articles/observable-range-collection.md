# ObservableRangeCollection and Performance

`ObservableRangeCollection<T>` is a helper collection used in the sample app (`DataGridSample.Collections`) to batch collection-change notifications.

For large grids, this is often the difference between smooth updates and UI stalls.

## Why use it

`ObservableCollection<T>` raises one notification per item (`Add`, `Remove`, etc.).
For large batches, that can trigger thousands of UI updates and expensive view work.

`ObservableRangeCollection<T>` adds range operations that raise a single collection-change event for the whole batch.

## API surface

- `AddRange(IEnumerable<T> items)`:
  Appends a batch and raises one `NotifyCollectionChangedAction.Add`.
- `InsertRange(int index, IEnumerable<T> items)`:
  Inserts a batch at an index and raises one `Add`.
- `RemoveRange(int index, int count)`:
  Removes a contiguous block and raises one `Remove`.
- `ResetWith(IEnumerable<T> items)`:
  Replaces all content and raises one `Reset`.

The implementation also raises `PropertyChanged` for `Count` and `Item[]` once per range operation.

## When to use each method

- Initial load or full regenerate:
  Use `ResetWith(...)`.
- Streaming append or chunked ingest:
  Use `AddRange(...)`.
- Sliding window (append + trim head):
  Use `AddRange(...)` and `RemoveRange(...)`.
- Ordered insertion into an existing list:
  Use `InsertRange(...)`.

## Performance guidance

### 1. Prefer range operations over item-by-item loops

Avoid:

```csharp
foreach (var item in batch)
{
    rows.Add(item);
}
```

Prefer:

```csharp
rows.AddRange(batch);
```

### 2. Build data off-thread, apply once on UI thread

Generate large batches in a background task, then apply with one range operation:

```csharp
var next = await Task.Run(() =>
{
    var list = new List<MyRow>(targetCount);
    for (var i = 0; i < targetCount; i++)
    {
        list.Add(CreateRow(i));
    }
    return list;
});

rows.ResetWith(next);
```

### 3. Use `ResetWith` only for full replacement

`Reset` is efficient for full rebuilds, but it invalidates the view broadly.
For live updates, prefer incremental `AddRange`/`RemoveRange` so the grid can process smaller deltas.

### 4. Keep batches reasonably sized

Very small batches (for example, 1-2 items at very high frequency) can still cause churn.
Coalesce updates into moderate chunks when possible.

### 5. Pass materialized lists when available

`InsertRange`/`AddRange` materialize non-list enumerables internally.
If you already have a `List<T>` (or other `IList<T>`), pass it directly to reduce extra allocations.

## DataGrid fast-path notes

For best DataGrid update performance:

- Keep notifications incremental with valid indices for add/remove/move patterns.
- Prefer range add/remove over repeated per-item notifications.
- Reserve `ResetWith` for full data replacement scenarios.

This aligns with the fast-path guidance in:

- [High-Frequency Updates](high-frequency-updates.md)
- [Hierarchical High-Frequency Updates](hierarchical-high-frequency-updates.md)

## Example patterns

### Full regenerate

```csharp
var rows = new ObservableRangeCollection<RowItem>();

async Task RegenerateAsync(int count)
{
    var next = await Task.Run(() =>
    {
        var list = new List<RowItem>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(CreateRow(i));
        }
        return list;
    });

    rows.ResetWith(next);
}
```

### Sliding window stream

```csharp
const int capacity = 100_000;

void ApplyBatch(IList<RowItem> batch)
{
    rows.AddRange(batch);

    var overflow = rows.Count - capacity;
    if (overflow > 0)
    {
        rows.RemoveRange(0, overflow);
    }
}
```

### Hierarchical child updates

```csharp
root.Children.AddRange(newChildren);
root.Children.RemoveRange(removeIndex, removeCount);
```

## Related topics

- [Data Sources and Collection Views](data-sources-and-collection-views.md)
- [High-Frequency Updates](high-frequency-updates.md)
- [Hierarchical High-Frequency Updates](hierarchical-high-frequency-updates.md)
- [Scrolling and Virtualization](scrolling-virtualization.md)
