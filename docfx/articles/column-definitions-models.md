# Column Definitions: Model Integration and Fast Path

Column definitions are data objects that the grid maps to real columns. Model-driven features (sorting, filtering, searching, conditional formatting) can reference definition instances directly, keeping column identity stable and avoiding reflection-heavy property paths.

## Fast path rules of thumb

- Use `DataGridBindingDefinition` (or `ValueAccessor`) so models can read values without reflection.
- Use the `DataGridColumnDefinition` instance as the column id in model descriptors, or assign `ColumnKey` and use that key for stable ids.
- Avoid string property paths unless you need them for external tooling or persistence.

## Sorting model

`SortingDescriptor` accepts an object id. Use the definition instance for a stable id that survives column re-materialization.

```csharp
var firstNameColumn = new DataGridTextColumnDefinition
{
    Header = "First Name",
    Binding = DataGridBindingDefinition.Create<Person, string>(p => p.FirstName)
};

var lastNameColumn = new DataGridTextColumnDefinition
{
    Header = "Last Name",
    Binding = DataGridBindingDefinition.Create<Person, string>(p => p.LastName)
};

var sortingModel = new SortingModel();

sortingModel.Apply(new[]
{
    new SortingDescriptor(firstNameColumn, ListSortDirection.Ascending),
    new SortingDescriptor(lastNameColumn, ListSortDirection.Descending)
});
```

If you need ids that survive recreation, assign `ColumnKey` and pass the key instead of the definition instance:

```csharp
firstNameColumn.ColumnKey = "first-name";
sortingModel.Apply(new[]
{
    new SortingDescriptor("first-name", ListSortDirection.Ascending)
});
```

If you set `ValueAccessor` or use `DataGridBindingDefinition`, sorting uses the typed accessor instead of reflection. `SortMemberPath` is still supported as a fallback.

If the sort key should differ from the displayed binding, set `DataGridColumnDefinitionOptions.SortValueAccessor` on the definition.

## Filtering model

When a descriptor references a column definition, the adapter resolves it to the materialized column and uses its value accessor.

```csharp
var statusColumn = new DataGridTextColumnDefinition
{
    Header = "Status",
    Binding = DataGridBindingDefinition.Create<Person, PersonStatus>(p => p.Status)
};

var filteringModel = new FilteringModel { OwnsViewFilter = true };

filteringModel.SetOrUpdate(new FilteringDescriptor(
    columnId: statusColumn,
    @operator: FilteringOperator.Equals,
    value: PersonStatus.Active));
```

For fast path filtering, ensure the column definition provides a value accessor (via binding or `ValueAccessor`).

If the filtering value should differ from the displayed value, set `DataGridColumnDefinitionOptions.FilterValueAccessor` on the definition to supply a dedicated filter accessor.

### Accessor-only adapters

If you want to avoid the reflection fallback in filtering/searching, enable the built-in accessor-only adapters:

```xml
<DataGrid FilteringModel="{Binding FilteringModel}"
          SearchModel="{Binding SearchModel}">
  <DataGrid.FastPathOptions>
    <DataGridFastPathOptions UseAccessorsOnly="True"
                             ThrowOnMissingAccessor="True" />
  </DataGrid.FastPathOptions>
</DataGrid>
```

`FastPathOptions` is a CLR property. If you need to bind or reuse an instance from a view model, assign it in code-behind:

```csharp
public MyPage()
{
    InitializeComponent();
    if (DataContext is MyViewModel vm)
    {
        Grid.FastPathOptions = vm.FastPathOptions;
    }
}
```

You can also wire the factories explicitly (useful if you want custom adapters later):

```xml
<DataGrid FilteringModel="{Binding FilteringModel}"
          SearchModel="{Binding SearchModel}"
          FilteringAdapterFactory="{StaticResource AccessorFilteringAdapterFactory}"
          SearchAdapterFactory="{StaticResource AccessorSearchAdapterFactory}" />
```

Define the resources using `DataGridAccessorFilteringAdapterFactory` and `DataGridAccessorSearchAdapterFactory`.

## Search model

`SearchDescriptor` supports explicit column sets. Pass definition instances to keep the mapping stable.

```csharp
var searchModel = new SearchModel();

searchModel.Apply(new[]
{
    new SearchDescriptor(
        query: "Ada",
        scope: SearchScope.ExplicitColumns,
        columnIds: new object[] { firstNameColumn, lastNameColumn })
});
```

The search adapter uses the value accessor when present, avoiding reflection-based getters.

For template columns or custom text, set `DataGridColumnDefinitionOptions.SearchTextProvider` (or the attached `DataGridColumnSearch.TextProvider`) to supply search text without reflection.

You can also use `DataGridColumnDefinitionOptions.SearchMemberPath` and `SearchFormatProvider` to align search with a specific formatted value.

## Hierarchical models

For hierarchical data, bind to `HierarchicalNode` and use `DataGridHierarchicalColumnDefinition` for the expander column. Apply sorting/filtering/searching in the hierarchical model and keep the adapters accessor-based.

See [Column Definitions (Hierarchical Columns)](column-definitions-hierarchical.md) and [Column Definitions (Hot Path Integration)](column-definitions-hot-path.md).

## Conditional formatting model

Conditional formatting descriptors can target a column definition directly. The adapter resolves the column and reads values through the accessor.

```csharp
var formattingModel = new ConditionalFormattingModel();

formattingModel.Apply(new[]
{
    new ConditionalFormattingDescriptor(
        ruleId: "StatusActive",
        columnId: statusColumn,
        @operator: ConditionalFormattingOperator.Equals,
        value: PersonStatus.Active,
        themeKey: "StatusActiveCellTheme")
});
```

## Summaries

Summary calculators use the column value accessor when available. If you define summaries on materialized columns, the accessor from the definition is used to avoid reflection.

Column definitions can also configure summary cell appearance:

- `SummaryCellThemeKey` sets a theme for summary cells created from the definition.
- `SummaryCellHorizontalContentAlignment` / `SummaryCellVerticalContentAlignment` control summary content alignment.

## State persistence

`DataGridState` uses the column definition as the default column key when available. If you need stable keys across sessions, set `DataGridStateOptions.ColumnKeySelector` and `ColumnKeyResolver` and return your own ids.

## Clipboard and export

Use `ClipboardContentBinding` on definitions to control exported values. AOT-friendly bindings work the same way as display/edit bindings and preserve type information.

## Related articles

- [Column Definitions](column-definitions.md)
- [Column Definitions (AOT-Friendly Bindings)](column-definitions-aot.md)
- [Column Definitions (Hot Path Integration)](column-definitions-hot-path.md)
- [Column Definitions (Hierarchical Columns)](column-definitions-hierarchical.md)
- [Column Definitions: Fast Path Overview](column-definitions-fast-path-overview.md)
