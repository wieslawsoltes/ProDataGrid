# ProDataGrid

[![Build](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml)

[![Release](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml)
[![GitHub Release](https://img.shields.io/github/v/release/wieslawsoltes/Avalonia.Controls.DataGrid.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/releases)

[![NuGet](https://img.shields.io/nuget/v/ProDataGrid.svg)](https://www.nuget.org/packages/ProDataGrid/)

## About

`ProDataGrid` is a hard fork of the original `Avalonia.Controls.DataGrid` control for [Avalonia](https://github.com/AvaloniaUI/Avalonia).

It displays repeating data in a customizable grid with enhanced features and improved performance, and is maintained as an independent NuGet package to evolve faster than the in-box control.

## Features

| Area | Highlights |
| --- | --- |
| Virtualization & scrolling | ScrollViewer-based `ILogicalScrollable` presenter, smooth wheel/gesture handling, snap points, anchor support, predictive row prefetch, frozen columns. |
| Columns | Text, template, checkbox columns; auto/star/pixel sizing; reordering, resizing, visibility control, frozen sections. |
| Rows | Variable-height support with pluggable estimators; row details; grouping headers; selection modes; row headers. |
| Drag & drop | Opt-in row drag/drop with header/row handles, multi-row moves, routed events, pluggable handlers (flat + hierarchical before/after/inside), and built-in visuals/auto-scroll. |
| Editing & navigation | In-place editing, commit/cancel, keyboard navigation, clipboard copy modes, current cell tracking. |
| Data operations | Sorting, grouping, paging, currency management via `DataGridCollectionView` family; selection built on Avalonia `SelectionModel` for stable binding across sort/filter. |
| Styling & theming | Fluent/Simple v2 ScrollViewer templates, row/cell styling, template overrides, theme resources, focus/selection visuals. |
| Data binding | Auto-generates columns from `DataTable.DefaultView` and binds cells via TypeDescriptor (no manual indexers), `SelectedItems` two-way binding support, `DataGridCollectionView` for sorting/grouping/editing. |

## Supported targets

- .NET 6.0 and 10.0; .NET Standard 2.0 for compatibility.
- Avalonia 11.3.x (see `Directory.Packages.props`).
- Windows, Linux, and macOS (via Avalonia’s cross-platform stack).

## Installation

Install from NuGet:

```sh
dotnet add package ProDataGrid
```

Or add a package reference:

```xml
<PackageReference Include="ProDataGrid" Version="..." />
```

## Usage

Basic setup with common column types and width modes (pixel, auto, star):

```xml
<DataGrid Items="{Binding People}"
          AutoGenerateColumns="False"
          CanUserResizeColumns="True"
          UseLogicalScrollable="True"
          GridLinesVisibility="Horizontal">
  <DataGrid.Columns>
    <!-- Pixel width -->
    <DataGridTextColumn Header="ID"
                        Binding="{Binding Id}"
                        Width="60" />

    <!-- Auto sizes to content -->
    <DataGridTextColumn Header="Name"
                        Binding="{Binding Name}"
                        Width="Auto" />

    <!-- Fixed pixel width checkbox column -->
    <DataGridCheckBoxColumn Header="Active"
                            Binding="{Binding IsActive}"
                            Width="80" />

    <!-- Star sizing shares remaining space -->
    <DataGridTextColumn Header="Department"
                        Binding="{Binding Department}"
                        Width="*" />

    <!-- Template column with custom content and weighted star width -->
    <DataGridTemplateColumn Header="Notes"
                            Width="2*">
      <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
          <TextBlock Text="{Binding Notes}"
                     TextWrapping="Wrap" />
        </DataTemplate>
      </DataGridTemplateColumn.CellTemplate>
    </DataGridTemplateColumn>
  </DataGrid.Columns>
</DataGrid>
```

Widths accept pixel values (`"80"`), `Auto` (content-based), `*` or weighted stars (e.g., `2*`) that share remaining space.

### Row drag & drop quick start

- Turn on `CanUserReorderRows="True"` (default handle is the row header) and choose `RowDragHandle` (`RowHeader`, `Row`, or `RowHeaderAndRow`).
- Configure effects via `RowDragDropOptions` (e.g., allow copy) and plug in a drop handler when you need custom moves. Built-ins: `DataGridRowReorderHandler` (flat lists) and `DataGridHierarchicalRowReorderHandler` (hierarchical; supports before/after/inside like TreeDataGrid).
- `RowDragStarting`/`RowDragCompleted` are routed events for telemetry/customization; the grid auto-updates selection after a successful drop.
- Dropping in the top/bottom thirds of a hierarchical row inserts before/after; the middle inserts inside that node’s children.

```xml
<!-- xmlns:controls="clr-namespace:Avalonia.Controls;assembly=ProDataGrid" -->
<DataGrid Items="{Binding HierarchicalItems}"
          CanUserReorderRows="True"
          HierarchicalRowsEnabled="True"
          RowDragHandle="RowHeaderAndRow">
  <DataGrid.RowDropHandler>
    <controls:DataGridHierarchicalRowReorderHandler />
  </DataGrid.RowDropHandler>
  <DataGrid.RowDragDropOptions>
    <controls:DataGridRowDragDropOptions AllowedEffects="Move,Copy"
                                         DragSelectedRows="True" />
  </DataGrid.RowDragDropOptions>
</DataGrid>
```

## Using SelectionModel with DataGrid

DataGrid exposes a `Selection` property (`ISelectionModel`) so you can plug in your own selection model or share one across controls.

- Bind `Selection` to your view-model’s `SelectionModel<T>`:

  ```xml
  <DataGrid ItemsSource="{Binding Items}"
            Selection="{Binding MySelectionModel}"
            SelectionMode="Extended" />
  ```

  ```csharp
  public SelectionModel<MyItem> MySelectionModel { get; } = new()
  {
      SingleSelect = false
  };
  ```

- Let the grid assign `Selection.Source` to its collection view; avoid pre-setting `Source` to prevent mismatches.
- You can share the same `SelectionModel<T>` with other controls (e.g., `ListBox Selection="{Binding MySelectionModel}"`) to keep selection in sync.
- `SelectedItems` binding still works; when `Selection` is set, it reflects the model’s selection and updates it when the binding changes.

### Auto-scroll to selection

Turn on `AutoScrollToSelectedItem` to keep the current selection in view without handling `SelectionChanged` manually:

```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoScrollToSelectedItem="True" />
```

## Package Rename

This package has been renamed from `Avalonia.Controls.DataGrid` to `ProDataGrid`.

The new name gives the fork its own NuGet identity (so it can ship independently of Avalonia), avoids collisions with the built-in control, and signals the performance/features added in this branch.

The fork is maintained at https://github.com/wieslawsoltes/ProDataGrid.

### Migration

To migrate from the original package, update your NuGet reference:

```xml
<!-- Old -->
<PackageReference Include="Avalonia.Controls.DataGrid" Version="..." />

<!-- New -->
<PackageReference Include="ProDataGrid" Version="..." />
```

## ScrollViewer-based implementation (v2)

ProDataGrid now ships a ScrollViewer-based template that implements `ILogicalScrollable` on `DataGridRowsPresenter`. This removes the custom `PART_VerticalScrollbar`/`PART_HorizontalScrollbar` pair and lets Avalonia handle scroll bars, scroll chaining, and inertia.

Add the v2 theme to opt into the new template (enables `UseLogicalScrollable` by default):

```xml
<!-- Fluent -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />

<!-- or Simple -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
```

If you use a custom control template, wrap `DataGridRowsPresenter` in a `ScrollViewer` named `PART_ScrollViewer` and set `UseLogicalScrollable="True"`. Keep the column headers in a separate row so they stay fixed while rows scroll.

### Recycle pool controls

Row recycling after resize/shrink is governed by three styled properties:

- `TrimRecycledContainers` (default `False`): trims the recycled row/group-header pool to a small buffer when the viewport contracts (e.g., maximize then restore). Set to `True` to bound the recycle pool; leave `False` for a larger cache.
- `KeepRecycledContainersInVisualTree` (default `True`): when `True`, recycled rows stay as hidden children; set to `False` to remove recycled containers from the presenter.
- `RecycledContainerHidingMode` (default `MoveOffscreen`): choose how recycled containers are hidden when `KeepRecycledContainersInVisualTree=True`—either move far offscreen or just set `IsVisible=False` (TreeDataGrid-style).

Typical usage:

```xml
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="False"
          KeepRecycledContainersInVisualTree="True"
          RecycledContainerHidingMode="MoveOffscreen" />

<!-- Bounded recycle pool, removed from tree -->
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="True"
          KeepRecycledContainersInVisualTree="False" />

<!-- TreeDataGrid-style visibility hiding -->
<DataGrid UseLogicalScrollable="True"
          TrimRecycledContainers="False"
          KeepRecycledContainersInVisualTree="True"
          RecycledContainerHidingMode="SetIsVisibleOnly" />
```

A sample toggle UI is available on the “Resize recycling diagnostics” page in the sample app.

## Row height estimators

Scrolling with variable row heights is now driven by pluggable estimators via `RowHeightEstimator`:

- `AdvancedRowHeightEstimator` (default): regional averages + Fenwick tree for accurate offsets.
- `CachingRowHeightEstimator`: caches per-row heights for predictable datasets.
- `DefaultRowHeightEstimator`: average-based for uniform rows.

Override the estimator per grid:

```xml
<!-- declare xmlns:controls="clr-namespace:Avalonia.Controls;assembly=ProDataGrid" -->
<DataGrid UseLogicalScrollable="True">
  <DataGrid.RowHeightEstimator>
    <controls:CachingRowHeightEstimator />
  </DataGrid.RowHeightEstimator>
</DataGrid>
```

## Migrating existing usage

- Prefer the v2 theme or update your template to use the ScrollViewer pattern; legacy scroll bars remain available when `UseLogicalScrollable="False"`.
- Replace direct access to template scroll bars with `ScrollViewer` APIs (`ScrollChanged`, `Offset`, `Extent`, `Viewport`).
- When handling wheel/gesture input, rely on the built-in logic (it routes through `UpdateScroll` when `UseLogicalScrollable` is true).
- For theme v2, ensure frozen columns and header separators are kept in sync with horizontal offset (the supplied templates already do this).
- If you depend on stable scroll positioning with dynamic row heights, choose the estimator that matches your data set and reset it after data source changes if needed.

## Selection model integration

ProDataGrid now routes row selection through Avalonia’s `SelectionModel<object?>`, giving stable `SelectedItem/SelectedItems/SelectedIndex` bindings across sorting, filtering, paging, and collection mutations. Highlights:

- `SelectedItems` remains an `IList` (for bindings) but is backed by the selection model; adding/removing in the bound collection updates the grid, and vice versa.
- Selection survives collection changes (including sorted `DataGridCollectionView` inserts/moves) without losing currency; current row is preserved when possible.
- Multi-select gestures and `SelectionMode` map to the model (`SelectionMode=Single` ↔ `SingleSelect=true`).
- A thin adapter keeps row index ↔ slot mapping internal, so custom selection models can be injected later.

## Selection change origin

`SelectionChanged` now raises `DataGridSelectionChangedEventArgs`, which carries:

- `Source` flag (`Pointer`, `Keyboard`, `Command`, `Programmatic`, `ItemsSourceChange`, `SelectionModelSync`).
- `IsUserInitiated` helper (true when pointer/keyboard/command initiated the change).
- `TriggerEvent` when an input event caused the change.

Example handler:

```csharp
private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (e is DataGridSelectionChangedEventArgs dg)
    {
        Debug.WriteLine($"Source={dg.Source}, user={dg.IsUserInitiated}, trigger={dg.TriggerEvent?.GetType().Name ?? "none"}");
    }
}
```

See the “Selection Origin” sample page in `DataGridSample` to observe the flags for pointer/keyboard, `SelectAll()`, `SelectionModel`, bindings, and ItemsSource changes.

## Sorting model integration

Sorting is now driven by a dedicated `ISortingModel` and adapter instead of directly mutating `SortDescriptions` from the header. This keeps sort state explicit, pluggable, and testable:

- Configure gesture policies on the model via `IsMultiSortEnabled`, `SortCycleMode` (2- or 3-state), and `OwnsSortDescriptions` (strict vs observe external changes).
- Per-column comparers/culture and `SortMemberPath` flow into `SortingDescriptor`s; duplicate-column guards and batch updates prevent drift.
- Plug in a custom model without subclassing the grid: set `SortingModel` or `SortingModelFactory` before use to inject alternate sort pipelines (e.g., server-side).
- Plug in a custom adapter without subclassing the grid: set `SortingAdapterFactory` to supply a specialized adapter (e.g., DynamicData/server-side) that can short-circuit local `SortDescriptions` churn via `TryApplyModelToView`.
- The adapter mirrors `Sorting` ↔ `DataGridCollectionView.SortDescriptions`, logging/rolling back on failures and refreshing rows while preserving selection snapshots.
- Gestures are unchanged (click/shift/meta), but glyphs and programmatic updates now reflect the model, so tests and automation can drive sorting through the same surface.
- Centralized descriptors plus `SortingChanging/SortingChanged` events make sort state observable; batch updates and duplicate-column guards prevent drift.
- Observe mode keeps the model in sync when `SortDescriptions` is mutated externally; strict mode keeps the model authoritative.
- Per-column `CustomSortComparer`/culture/paths are preserved in `SortingDescriptor`s, so custom comparers are first-class (including natural/ordinal/culture-aware string sorts).
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

### DynamicData integration

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

## Filtering model integration

Filtering is also driven by a pluggable model/adapter pair so header filters stay in sync with UI state and selection remains stable when rows are filtered.

- Bind `FilteringModel` or set `FilteringModelFactory`; `OwnsViewFilter` switches between authoritative mode (adapter owns `Filter`) and observer mode (adapter reconciles to an external `Filter`), with `FilteringChanging/Changed` and `BeginUpdate/EndUpdate/DeferRefresh` batching a single refresh.
- Swap adapters via `FilteringAdapterFactory` (DynamicData/server-side) that override `TryApplyModelToView`; adapter lifecycle hooks mirror sorting (`AttachLifecycle`) so selection/currency snapshots are restored after filters apply.
- Per-column predicate factories avoid reflection: set `DataGridColumnFilter.PredicateFactory` to return a typed predicate/parser for that column; descriptors carry culture/string comparison.
- Adapter guarantees: descriptor → predicate for string/between/in/custom cases, duplicate guards, observer-mode reconciliation, and selection stability are covered by unit tests.
- Shared header filter resources (`DataGridFilterResources.xaml`) provide a reusable filter button glyph/pseudo-class and default editors (text/number/date/enum) that can be consumed across themes and samples.

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

### DynamicData integration (filtering)

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

## Hierarchical model integration

Hierarchical rows are driven by `IHierarchicalModel` (flattened view of visible nodes) plus a thin adapter and a built-in `DataGridHierarchicalColumn` that renders indentation and the expander glyph.

- Plug in a model or factory: bind `HierarchicalModel`/`HierarchicalModelFactory` and set `HierarchicalRowsEnabled="True"`.
- When hierarchical rows are enabled and no `ItemsSource` is provided, the grid auto-binds to the model's flattened view so callers don't have to manage or refresh a separate flattened collection; `ObservableFlattened` is available when you need `INotifyCollectionChanged`/reactive pipelines.
- Provide children/leaves via `HierarchicalOptions` (`ChildrenSelector`, optional `IsLeafSelector`, `AutoExpandRoot/MaxAutoExpandDepth`, `SiblingComparer`/`SiblingComparerSelector`, `VirtualizeChildren`). Use the typed flavor (`HierarchicalOptions<T>`/`HierarchicalModel<T>`) when you want strongly-typed selectors and observable flattened nodes.
- The adapter exposes `Count/ItemAt/Toggle/Expand/Collapse` and raises `FlattenedChanged`; selection mapping uses the flattened indices.
- Use `DataGridHierarchicalColumn` for the tree column; per-level indent is configurable via `Indent`.

```xml
<DataGrid ItemsSource="{Binding Rows}"
          HierarchicalModel="{Binding Model}"
          HierarchicalRowsEnabled="True"
          AutoGenerateColumns="False">
  <DataGrid.Columns>
    <DataGridHierarchicalColumn Header="Name"
                                Width="2*"
                                Binding="{Binding Item.Name}" />
    <DataGridTextColumn Header="Kind" Binding="{Binding Item.Kind}" />
  </DataGrid.Columns>
</DataGrid>
```

```csharp
var options = new HierarchicalOptions
{
    ChildrenSelector = item => ((TreeNode)item).Children,
    IsLeafSelector = item => !((TreeNode)item).IsDirectory,
    AutoExpandRoot = true,
    MaxAutoExpandDepth = 0,
    VirtualizeChildren = true
};

var model = new HierarchicalModel(options);
model.SetRoot(rootNode);
```

Sample: see `Hierarchical Model` page in `src/DataGridSample` for a file-system tree with Name/Kind/Size/Modified columns.

## Samples

- The sample app (`src/DataGridSample`) includes pages for pixel-perfect columns, frozen columns, large datasets, and variable-height scenarios (`Pages/*Page.axaml`).
- Run it locally with `dotnet run --project src/DataGridSample/DataGridSample.csproj` to see templates, estimators, `SelectedItems` binding, and the `DataTable.DefaultView` page that demonstrates the TypeDescriptor-based column binding.

## License

ProDataGrid is licensed under the [MIT License](licence.md).

The original Avalonia.Controls.DataGrid license is preserved in [licence-avalonia.md](licence-avalonia.md).
