# State and Persistence

ProDataGrid includes helpers to capture and restore grid state. This is useful for persisting user preferences or restoring UI state after data refreshes.

## Capture and Restore

Use the `Capture*State` and `Restore*State` helpers or capture the full state in one call.

```csharp
using System.Linq;

var options = new DataGridStateOptions
{
    ItemKeySelector = item => (item as MyRow)?.Id,
    ItemKeyResolver = key => Items.FirstOrDefault(row => Equals(row.Id, key)),
    ColumnKeySelector = column => column.Header?.ToString(),
    ColumnKeyResolver = key => grid.Columns.FirstOrDefault(
        column => Equals(column.Header, key))
};

var state = grid.CaptureState(DataGridStateSections.All, options);

// Later
grid.RestoreState(state, DataGridStateSections.All, options);
```

## Section Helpers

Each section has dedicated capture/restore helpers:

- Columns: `CaptureColumnLayoutState`, `RestoreColumnLayoutState`
- Sorting: `CaptureSortingState`, `RestoreSortingState`
- Filtering: `CaptureFilteringState`, `RestoreFilteringState`
- Search: `CaptureSearchState`, `RestoreSearchState`
- Grouping: `CaptureGroupingState`, `RestoreGroupingState`
- Hierarchical: `CaptureHierarchicalState`, `RestoreHierarchicalState`
- Selection: `CaptureSelectionState`, `RestoreSelectionState`
- Scroll: `CaptureScrollState`, `TryRestoreScrollState`

Example:

```csharp
var selection = grid.CaptureSelectionState(options);
grid.RestoreSelectionState(selection, options);

var scroll = grid.CaptureScrollState(options);
grid.TryRestoreScrollState(scroll, options);
```

## Sections

You can capture specific slices instead of the full state:

- Selection
- Scroll
- Sorting
- Filtering
- Searching
- Columns
- Grouping
- Hierarchical
- Layout (Columns + Sorting + Filtering + Searching + Grouping)
- View (Sorting + Filtering + Searching + Grouping)
