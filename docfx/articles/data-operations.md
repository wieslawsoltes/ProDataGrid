# Data Operations

ProDataGrid supports rich data operations through `DataGridCollectionView` and model-driven sorting, filtering, grouping, and search. These models keep UI state explicit and allow adapters for custom pipelines.

## Sorting Model Integration

Sorting is driven by a dedicated `ISortingModel` and adapter instead of directly mutating `SortDescriptions` from the header. This keeps sort state explicit, pluggable, and testable:

- Configure gesture policies on the model via `IsMultiSortEnabled`, `SortCycleMode` (2- or 3-state), and `OwnsSortDescriptions` (strict vs observe external changes).
- Per-column comparers/culture and `SortMemberPath` flow into `SortingDescriptor`s; duplicate-column guards and batch updates prevent drift.
- Plug in a custom model without subclassing the grid: set `SortingModel` or `SortingModelFactory` before use to inject alternate sort pipelines (e.g., server-side).
- Plug in a custom adapter without subclassing the grid: set `SortingAdapterFactory` to supply a specialized adapter (e.g., DynamicData/server-side) that can short-circuit local `SortDescriptions` churn via `TryApplyModelToView`.
- The adapter mirrors `Sorting` to `DataGridCollectionView.SortDescriptions`, logging/rolling back on failures and refreshing rows while preserving selection snapshots.
- Gestures are unchanged (click/shift/meta), but glyphs and programmatic updates now reflect the model, so tests and automation can drive sorting through the same surface.
- Centralized descriptors plus `SortingChanging/SortingChanged` events make sort state observable; batch updates and duplicate-column guards prevent drift.
- Use `SortingChanging` to cancel or amend descriptor updates (for example, to keep a primary sort pinned).
- Observe mode keeps the model in sync when `SortDescriptions` is mutated externally; strict mode keeps the model authoritative.
- Per-column `CustomSortComparer`, culture, and paths are preserved in `SortingDescriptor`s, so custom comparers are first-class (including natural/ordinal/culture-aware string sorts).
- Sample: see `Sorting Model Playground` in `src/DataGridSample` for presets, external-sort reconciliation, and event logging.

### Using SortingModel with DataGrid

Bind a custom sorting model or factory just like selection:

```xml
<DataGrid ItemsSource="{Binding Items}"
          SortingModel="{Binding MySortingModel}"
          IsMultiSortEnabled="True"
          SortCycleMode="AscendingDescendingNone"
          OwnsSortDescriptions="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name"
                        Binding="{Binding Name}"
                        SortMemberPath="Name" />
    <DataGridTextColumn Header="Status"
                        Binding="{Binding Status}"
                        SortMemberPath="Status"
                        CustomSortComparer="{Binding StatusComparer}" />
  </DataGrid.Columns>
</DataGrid>
```

```csharp
using Avalonia.Controls.DataGridSorting;
using System.Collections;
using System.ComponentModel;

public ISortingModel MySortingModel { get; } = new SortingModel
{
    MultiSort = true,
    CycleMode = SortCycleMode.AscendingDescendingNone,
    OwnsViewSorts = true
};

public IComparer StatusComparer { get; } = new StatusComparer();

public void ApplyPreset()
{
    MySortingModel.Apply(new[]
    {
        new SortingDescriptor("Status", ListSortDirection.Ascending, "Status", StatusComparer),
        new SortingDescriptor("Name", ListSortDirection.Ascending, "Name")
    });
}
```

Or swap the model creation globally:

```csharp
dataGrid.SortingModelFactory = new MyCustomSortingFactory();
```

### DynamicData Integration (Sorting)

To keep sorting upstream in a DynamicData pipeline (no local `SortDescriptions` churn), supply a custom adapter factory and feed a comparer subject into `Sort`:

```csharp
var source = new SourceList<Deployment>();
source.AddRange(Deployment.CreateSeed());

var adapterFactory = new DynamicDataSortingAdapterFactory(log => Debug.WriteLine(log));
var comparerSubject = new BehaviorSubject<IComparer<Deployment>>(adapterFactory.SortComparer);

source.Connect()
      .Sort(comparerSubject) // DynamicData performs the sort
      .Bind(out _view)
      .Subscribe();

SortingModel sortingModel = new SortingModel
{
    MultiSort = true,
    CycleMode = SortCycleMode.AscendingDescendingNone,
    OwnsViewSorts = true
};

sortingModel.SortingChanged += (_, e) =>
{
    adapterFactory.UpdateComparer(e.NewDescriptors);
    comparerSubject.OnNext(adapterFactory.SortComparer);
};

// In code-behind (since SortingAdapterFactory cannot be bound directly in XAML):
grid.SortingModel = sortingModel;
grid.SortingAdapterFactory = adapterFactory;
```

Header clicks update the `SortingModel`; the custom adapter overrides `TryApplyModelToView`, so instead of touching `SortDescriptions` it rebuilds a `SortExpressionComparer` chain and pushes it to the DynamicData `Sort` operator. The grid still shows glyphs/state from the `SortingModel`, but the actual sort happens in the DynamicData pipeline.

## Filtering Model Integration

Filtering is also driven by a pluggable model/adapter pair so header filters stay in sync with UI state and selection remains stable when rows are filtered.

- Bind `FilteringModel` or set `FilteringModelFactory`; `OwnsViewFilter` switches between authoritative mode (adapter owns `Filter`) and observer mode (adapter reconciles to an external `Filter`), with `FilteringChanging/Changed` and `BeginUpdate/EndUpdate/DeferRefresh` batching a single refresh.
- Swap adapters via `FilteringAdapterFactory` (DynamicData/server-side) that override `TryApplyModelToView`; adapter lifecycle hooks mirror sorting (`AttachLifecycle`) so selection/currency snapshots are restored after filters apply.
- Per-column predicate factories avoid reflection: set `DataGridColumnFilter.PredicateFactory` to return a typed predicate/parser for that column; descriptors carry culture/string comparison.
- Adapter guarantees: descriptor to predicate for string/between/in/custom cases, duplicate guards, observer-mode reconciliation, and selection stability are covered by unit tests.
- The filter button glyphs and default editor templates (text/number/date/enum) live in `Themes/Generic.xaml` and can be reused across themes and samples.

```xml
<DataGrid ItemsSource="{Binding Items}"
          FilteringModel="{Binding MyFilteringModel}">
  <DataGrid.Columns>
    <DataGridTextColumn x:Name="NameColumn"
                        Header="Name"
                        Binding="{Binding Name}"
                        SortMemberPath="Name" />
    <DataGridTextColumn x:Name="ScoreColumn"
                        Header="Score"
                        Binding="{Binding Score}"
                        SortMemberPath="Score" />
  </DataGrid.Columns>
</DataGrid>
```

```csharp
using Avalonia.Controls.DataGridFiltering;

public IFilteringModel MyFilteringModel { get; } = new FilteringModel { OwnsViewFilter = true };

public void ApplyFilters()
{
    MyFilteringModel.Apply(new[]
    {
        new FilteringDescriptor("Score", FilteringOperator.GreaterThanOrEqual, nameof(Item.Score), value: 5),
        new FilteringDescriptor("Name", FilteringOperator.Contains, nameof(Item.Name), value: "al", stringComparison: StringComparison.OrdinalIgnoreCase)
    });
}

// Optional: typed predicate, no reflection
DataGridColumnFilter.SetPredicateFactory(ScoreColumn, descriptor =>
    o => ((Item)o).Score >= 5);
```

For DynamicData/server-side filtering, supply a custom `FilteringAdapterFactory` that overrides `TryApplyModelToView` and pushes a composed predicate (or query object) upstream; the grid still drives glyphs and descriptors locally while selection stays intact.

### Filtering UI and Column Flyouts

Columns expose `FilterFlyout` and `ShowFilterButton` so you can plug in custom editors while keeping the header glyphs consistent. The built-in templates live in `Themes/Generic.xaml`:

```xml
<UserControl.Resources>
  <Flyout x:Key="StatusFilterFlyout"
          Placement="Bottom"
          FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
          Content="{Binding StatusFilter}"
          ContentTemplate="{StaticResource DataGridFilterEnumEditorTemplate}" />
</UserControl.Resources>

<DataGridTextColumn Header="Status"
                    Binding="{Binding Status}"
                    FilterFlyout="{StaticResource StatusFilterFlyout}" />
```

Use `ShowFilterButton="True"` if you want a filter glyph without a flyout (for example, to open an external panel).

### DynamicData Integration (Filtering)

Keep filtering upstream in a DynamicData pipeline while the grid shows filter glyphs from `FilteringModel`:

```csharp
var source = new SourceList<Deployment>();
source.AddRange(Deployment.CreateSeed());

var adapterFactory = new DynamicDataFilteringAdapterFactory(log => Debug.WriteLine(log));
var filterSubject = new BehaviorSubject<Func<Deployment, bool>>(adapterFactory.FilterPredicate);

source.Connect()
      .Filter(filterSubject) // DynamicData performs the filtering
      .Bind(out _view)
      .Subscribe();

var filteringModel = new FilteringModel { OwnsViewFilter = true };

filteringModel.FilteringChanged += (_, e) =>
{
    adapterFactory.UpdateFilter(e.NewDescriptors);
    filterSubject.OnNext(adapterFactory.FilterPredicate);
};

grid.FilteringModel = filteringModel;
grid.FilteringAdapterFactory = adapterFactory; // bypasses DataGridCollectionView.Filter churn
```

If an external consumer owns `DataGridCollectionView.Filter`, set `OwnsViewFilter=false` and the adapter reconciles descriptors to that external filter (observer mode) while keeping glyphs in sync.

## Search Model and Column Search

Search integrates with the model layer and can be scoped to columns for lightweight in-grid find operations.

- Bind `SearchModel` directly or set `SearchModelFactory` to supply a custom model.
- Replace the default adapter with `SearchAdapterFactory` when you want a custom search pipeline (the built-in adapter uses reflection against column bindings).
- Descriptors control match rules, term combining, scope, and parsing behavior.
- Scopes include `AllColumns`, `VisibleColumns`, and `ExplicitColumns` (provide a column id list).
- Column ids can be `DataGridColumn` instances or search member path strings.
- Use `DataGridColumnSearch` attached properties to opt out or override the search member path/text.
- Highlight styles are exposed as `:searchmatch` and `:searchcurrent` pseudo-classes on rows and cells.

```csharp
using System;
using Avalonia.Controls.DataGridSearching;

public SearchModel SearchModel { get; } = new SearchModel
{
    HighlightMode = SearchHighlightMode.TextAndCell,
    HighlightCurrent = true,
    WrapNavigation = true,
    UpdateSelectionOnNavigate = true
};

public void ApplySearch(string query)
{
    SearchModel.SetOrUpdate(new SearchDescriptor(
        query.Trim(),
        matchMode: SearchMatchMode.Contains,
        termMode: SearchTermCombineMode.Any,
        scope: SearchScope.VisibleColumns,
        comparison: StringComparison.OrdinalIgnoreCase,
        wholeWord: false,
        normalizeWhitespace: true,
        ignoreDiacritics: true));
}
```

Explicit column search:

```csharp
SearchModel.SetOrUpdate(new SearchDescriptor(
    query.Trim(),
    scope: SearchScope.ExplicitColumns,
    columnIds: new object[] { "FirstName", "LastName" }));
```

Customize column search behavior without a prefix in XAML by using the attached properties in code:

```csharp
DataGridColumnSearch.SetIsSearchable(StatusColumn, false);
DataGridColumnSearch.SetSearchMemberPath(NameColumn, "Name");
DataGridColumnSearch.SetTextProvider(NotesColumn, item => ((Person)item).Notes);
```

### DynamicData Integration (Search)

If you need search descriptors to drive an upstream query, set a custom `SearchAdapterFactory` in code-behind and keep the grid highlighting enabled:

```csharp
grid.SearchModel = searchModel;
grid.SearchAdapterFactory = new DynamicDataSearchAdapterFactory(log => Debug.WriteLine(log));
```

## Grouping and Paging

Use `GroupDescriptions` to build group headers and control expand/collapse. Grouping can be modified at runtime.

```csharp
using Avalonia.Collections;

var view = new DataGridCollectionView(items);
view.GroupDescriptions.Add(new DataGridPathGroupDescription("Region"));
grid.ItemsSource = view;
grid.AreRowGroupHeadersFrozen = true;
```

You can expand or collapse all groups with `ExpandAllGroups()` and `CollapseAllGroups()`.

### Paging and Currency

- Page via `PageSize`, `PageIndex`, and `MoveToNextPage/MoveToPreviousPage/MoveToPage`.
- Currency is exposed through `CurrentItem`, `CurrentPosition`, `CurrentChanged`, and `CurrentChanging`.

```csharp
var view = new DataGridCollectionView(items);
view.PageSize = 50;
view.MoveToFirstPage();
```
