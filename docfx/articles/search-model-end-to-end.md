# Search Model: End-to-End Usage

This guide shows complete `SearchModel` wiring: query descriptors, result navigation, highlight behavior, and adapter handoff.

## What this gives you

- One model for query state, results, and current match navigation.
- Consistent behavior between toolbar controls and keyboard navigation.
- Configurable matching (`Contains`, `Regex`, `Wildcard`, whole-word, case-sensitivity).
- Optional explicit-column search using stable column ids.

## End-to-end flow

1. Query/options update `SearchDescriptor` in `SearchModel`.
2. Search adapter evaluates rows/columns and publishes `Results`.
3. `ResultsChanged` and `CurrentChanged` update side panels and navigation state.
4. Grid highlights matches based on `HighlightMode` and current result index.

## 1. ViewModel wiring

```csharp
using System;
using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls.DataGridSearching;

public sealed class TicketsViewModel
{
    public DataGridCollectionView View { get; }
    public SearchModel SearchModel { get; } = new()
    {
        HighlightMode = SearchHighlightMode.TextAndCell,
        HighlightCurrent = true,
        WrapNavigation = true,
        UpdateSelectionOnNavigate = true
    };

    public string Query { get; set; } = string.Empty;
    public SearchMatchMode MatchMode { get; set; } = SearchMatchMode.Contains;
    public SearchTermCombineMode TermMode { get; set; } = SearchTermCombineMode.Any;
    public SearchScope Scope { get; set; } = SearchScope.AllColumns;

    public TicketsViewModel()
    {
        View = new DataGridCollectionView(CreateItems());
        SearchModel.ResultsChanged += (_, __) => RefreshResultSummary();
        SearchModel.CurrentChanged += (_, __) => RefreshResultSummary();
    }

    public void ApplySearch()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            SearchModel.Clear();
            return;
        }

        SearchModel.SetOrUpdate(new SearchDescriptor(
            query: Query.Trim(),
            matchMode: MatchMode,
            termMode: TermMode,
            scope: Scope,
            comparison: StringComparison.OrdinalIgnoreCase,
            wholeWord: false,
            normalizeWhitespace: true,
            ignoreDiacritics: true));
    }

    public bool Next() => SearchModel.MoveNext();
    public bool Previous() => SearchModel.MovePrevious();

    private void RefreshResultSummary()
    {
        // Update counters or current-match UI state here.
    }

    private static IReadOnlyList<TicketRow> CreateItems()
    {
        return new[]
        {
            new TicketRow(1, "Upgrade cache", "Alex", "Warm-up and telemetry checks"),
            new TicketRow(2, "Backfill metrics", "Jesse", "Include historical backfill validation")
        };
    }

    public sealed record TicketRow(int Id, string Title, string Owner, string Notes);
}
```

## 2. XAML wiring

```xml
<DataGrid ItemsSource="{Binding View}"
          SearchModel="{Binding SearchModel}"
          AutoGenerateColumns="False"
          CanUserSortColumns="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Id"
                        ColumnKey="id"
                        Binding="{Binding Id}"
                        SortMemberPath="Id" />
    <DataGridTextColumn Header="Title"
                        ColumnKey="title"
                        Binding="{Binding Title}"
                        SortMemberPath="Title" />
    <DataGridTextColumn Header="Owner"
                        ColumnKey="owner"
                        Binding="{Binding Owner}"
                        SortMemberPath="Owner" />
    <DataGridTextColumn Header="Notes"
                        ColumnKey="notes"
                        Binding="{Binding Notes}"
                        SortMemberPath="Notes" />
  </DataGrid.Columns>
</DataGrid>
```

Use buttons/shortcuts to call `MoveNext` and `MovePrevious` through commands.

## 3. Explicit column scope

Limit search to specific columns:

```csharp
SearchModel.SetOrUpdate(new SearchDescriptor(
    query: Query.Trim(),
    scope: SearchScope.ExplicitColumns,
    columnIds: new object[] { "title", "notes" },
    matchMode: SearchMatchMode.Contains,
    termMode: SearchTermCombineMode.Any,
    comparison: StringComparison.OrdinalIgnoreCase));
```

Use stable `ColumnKey` ids in columns when you rely on explicit column sets.

## 4. Highlights and navigation behavior

Search presentation flags are runtime-configurable:

```csharp
SearchModel.HighlightMode = SearchHighlightMode.TextAndCell; // None, Cell, TextAndCell
SearchModel.HighlightCurrent = true;
SearchModel.WrapNavigation = true;
SearchModel.UpdateSelectionOnNavigate = true;
```

Useful bindings:

- `SearchModel.Results.Count` for result counters.
- `SearchModel.CurrentIndex` to show `N of M`.
- `SearchModel.CurrentResult` for side-panel previews.

## 5. DynamicData/server-side search

If search should execute upstream, keep `SearchModel` in the grid and swap the adapter:

```csharp
grid.SearchModel = viewModel.SearchModel;
grid.SearchAdapterFactory = viewModel.SearchAdapterFactory;
```

Reference implementation:

- `src/DataGridSample/Adapters/DynamicDataSearchAdapterFactory.cs`

## Troubleshooting

- No highlights appear:
  Ensure `SearchModel` is bound and `HighlightMode` is not `None`.
- Empty query still matches rows:
  Call `SearchModel.Clear()` when query is empty, or use `allowEmpty: false` on descriptors.
- Explicit column scope returns zero results:
  Verify `columnIds` match real column ids (`ColumnKey`, member path, or column instance).

## Full sample references

- `src/DataGridSample/Pages/SearchModelPage.axaml`
- `src/DataGridSample/ViewModels/SearchModelViewModel.cs`
- `src/DataGridSample/Pages/ListBoxMimicSearchModelPage.axaml`

## Related articles

- [Data Operations](data-operations.md)
- [Column Definitions (Model Integration)](column-definitions-models.md)
- [Filtering Model: End-to-End Usage](filtering-model-end-to-end.md)
- [Sorting Model: End-to-End Usage](sorting-model-end-to-end.md)
