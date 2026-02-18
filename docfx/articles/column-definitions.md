# Column Definitions

Column definitions let you define DataGrid columns as data in a view model. You bind `ColumnDefinitionsSource` to a list of `DataGridColumnDefinition` and the grid materializes the built-in column types for you. This keeps view models free of Avalonia control instances and keeps columns fully typed and in sync.

## When to use column definitions

- You want MVVM-friendly columns without constructing `DataGridColumn` controls in view models.
- You want to reuse column metadata across multiple views.
- You need string-keyed templates/themes and AOT-friendly compiled bindings.

## Basic setup

```xml
<DataGrid ItemsSource="{Binding Items}"
          ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          AutoGenerateColumns="False"
          HeadersVisibility="All">
  <DataGrid.Resources>
    <DataTemplate x:Key="StatusBadgeTemplate">
      <Border Padding="6,2" CornerRadius="8">
        <TextBlock Text="{Binding Status}" />
      </Border>
    </DataTemplate>
  </DataGrid.Resources>
</DataGrid>
```

```csharp
public DataGridColumnDefinitionList ColumnDefinitions { get; } = new()
{
    new DataGridTextColumnDefinition
    {
        Header = "First Name",
        Binding = DataGridBindingDefinition.Create<Person, string>(p => p.FirstName),
        Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
    },
    new DataGridTextColumnDefinition
    {
        Header = "Last Name",
        Binding = DataGridBindingDefinition.Create<Person, string>(p => p.LastName),
        Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
    },
    new DataGridTemplateColumnDefinition
    {
        Header = "Badge",
        CellTemplateKey = "StatusBadgeTemplate",
        IsReadOnly = true
    }
};
```

## Typed builder

If you prefer a strongly typed builder, use `DataGridColumnDefinitionBuilder`:

```csharp
var nameProperty = new ClrPropertyInfo(
    nameof(Person.Name),
    target => ((Person)target).Name,
    (target, value) => ((Person)target).Name = (string)value,
    typeof(string));

var builder = DataGridColumnDefinitionBuilder.For<Person>();

ColumnDefinitions = new DataGridColumnDefinitionList
{
    builder.Text("Name", nameProperty, p => p.Name, (p, v) => p.Name = v),
    builder.Template("Badge", "StatusBadgeTemplate")
};
```

## Definition types and property mapping

Each built-in column has a matching `*ColumnDefinition` type (for example, `DataGridTextColumnDefinition`). Definition properties mirror the column properties.

- `Header`, `Width`, `MinWidth`, `MaxWidth`, `DisplayIndex`, `IsVisible`, `CanUserSort`, `IsReadOnly`, `FilterFlyout`, and more.
- Template/theme keys: `HeaderTemplateKey`, `CellTemplateKey`, `CellEditingTemplateKey`, `HeaderThemeKey`, `CellThemeKey`, `FilterThemeKey`.
- Flyout resource keys: `FilterFlyoutKey`.
- Style hooks: `CellStyleClasses`, `HeaderStyleClasses`.
- Bindings: `Binding`, `SelectedItemBinding`, `SelectedValueBinding`, `TextBinding`, `ClipboardContentBinding`, `CellBackgroundBinding`, and `CellForegroundBinding`.

For the full catalog, see [Column Types Reference](column-types-reference.md).

## Template and theme keys

Template and theme properties use string keys and are resolved against grid resources first, then application resources:

- `HeaderTemplateKey`, `CellTemplateKey`, `CellEditingTemplateKey`, `NewRowCellTemplateKey`
- `HeaderThemeKey`, `CellThemeKey`, `FilterThemeKey`
- `FilterFlyoutKey`

Keep the templates in `DataGrid.Resources` or `Application.Resources` to decouple view models from visuals.

## Per-column filter flyouts

Use `FilterFlyout` to assign a flyout instance directly, or `FilterFlyoutKey` to resolve flyouts from resources (recommended for MVVM-friendly definitions):

```csharp
new DataGridTextColumnDefinition
{
    Header = "Customer",
    Binding = DataGridBindingDefinition.Create<Order, string>(o => o.Customer),
    FilterFlyoutKey = "CustomerFilterFlyout"
};
```

```xml
<DataGrid ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          FilteringModel="{Binding FilteringModel}">
  <DataGrid.Resources>
    <Flyout x:Key="CustomerFilterFlyout"
            Placement="Bottom"
            FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
            Content="{Binding CustomerFilter}"
            ContentTemplate="{StaticResource DataGridFilterTextEditorTemplate}" />
  </DataGrid.Resources>
</DataGrid>
```

## Updates, lifetime, and threading

- `ColumnDefinitionsSource` tracks `INotifyCollectionChanged` and applies add/remove/reset changes.
- Move/replace notifications are handled incrementally to avoid rebuilding the whole column set.
- Each definition is `INotifyPropertyChanged`; updates are pushed to the materialized column.
- Changes must happen on the UI thread and the same thread that created the binding.
- `ColumnDefinitionsSource` cannot be used together with bound `Columns` or inline column declarations.

`DataGridColumnDefinitionList` adds `AddRange` and `SuspendNotifications()` helpers for batching definition changes without triggering per-item updates.

## Auto-generated columns

You can combine `ColumnDefinitionsSource` with `AutoGenerateColumns`. Use `AutoGeneratedColumnsPlacement` to place auto-generated columns before or after the definition list.

## Bound columns and value accessors

`DataGridBindingDefinition` creates a compiled binding and also provides a typed value accessor used by sorting, filtering, searching, and conditional formatting. For computed values or non-binding scenarios, set `ValueAccessor` (and optionally `ValueType`) on the definition.

```csharp
new DataGridTextColumnDefinition
{
    Header = "Total",
    ValueAccessor = new DataGridColumnValueAccessor<Order, decimal>(o => o.Price * o.Quantity),
    ValueType = typeof(decimal),
    IsReadOnly = true
};
```

## Column options for search, filtering, and sorting

`DataGridColumnDefinitionOptions` lets you attach search/filter/sort metadata to a definition. These settings are applied to the materialized column via the same attached properties used by manual column configuration.

Key option properties:
- Search: `IsSearchable`, `SearchMemberPath`, `SearchTextProvider`, `SearchFormatProvider`
- Filtering: `FilterPredicateFactory`, `FilterValueAccessor`
- Sorting: `SortValueAccessor`, `SortValueComparer` (and typed comparisons via `DataGridColumnDefinitionOptions<TItem>`)

```csharp
var nameOptions = new DataGridColumnDefinitionOptions
{
    IsSearchable = true,
    SearchTextProvider = item => ((Person)item).Name,
    SearchFormatProvider = CultureInfo.InvariantCulture,
    FilterPredicateFactory = descriptor => item => ((Person)item).Age >= 18,
    FilterValueAccessor = new DataGridColumnValueAccessor<Person, int>(p => p.Age),
    SortValueAccessor = new DataGridColumnValueAccessor<Person, int>(p => p.Age)
};

var nameColumn = new DataGridTextColumnDefinition
{
    Header = "Name",
    Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name),
    Options = nameOptions
};
```

Use `SortValueAccessor` when the sort key should differ from the displayed value or binding. `FilterValueAccessor` can similarly target a different value for filtering operations.
Set `SortValueComparer` if you need custom ordering for the sort key values.

For model-level comparisons (no reflection, no boxing for the comparer itself), use the typed options:

```csharp
var sortOptions = new DataGridColumnDefinitionOptions<Person>
{
    CompareAscending = (left, right) => string.Compare(left.LastName, right.LastName, StringComparison.Ordinal),
    CompareDescending = (left, right) => string.Compare(right.LastName, left.LastName, StringComparison.Ordinal)
};
```

## Column keys

If you need stable identifiers across column re-materialization, assign `ColumnKey` on the definition. Model descriptors and state persistence can use that key instead of the definition instance:

```csharp
new DataGridTextColumnDefinition
{
    Header = "Name",
    ColumnKey = "name",
    Binding = DataGridBindingDefinition.Create<Person, string>(p => p.Name)
};
```

## Incremental updates and diffing

`ColumnDefinitionsSource` supports incremental add/remove/move/replace updates and now applies definition property changes incrementally as well. For batches, use `BeginUpdate`/`EndUpdate` or a list that can suspend notifications:

```csharp
var definitions = new DataGridColumnDefinitionList();

using (definitions.SuspendNotifications())
{
    definitions.Add(CreateFirstNameColumn());
    definitions.Add(CreateLastNameColumn());
}
```

For single definition updates:

```csharp
definition.BeginUpdate();
definition.Header = "Display Name";
definition.IsReadOnly = true;
definition.EndUpdate();
```

## AOT and fast path

Expression-based binding creation requires dynamic code generation. For AOT-friendly bindings and zero-reflection paths, use the overloads that accept a prebuilt `CompiledBindingPath` or `IPropertyInfo`. See:

- [Column Definitions: AOT-Friendly Bindings](column-definitions-aot.md)
- [Column Definitions: Model Integration and Fast Path](column-definitions-models.md)
- [Column Definitions: Hot Path Integration](column-definitions-hot-path.md)
- [Column Definitions: Hierarchical Columns](column-definitions-hierarchical.md)
- [Column Definitions: Fast Path Overview](column-definitions-fast-path-overview.md)

## Fast path options and diagnostics

Use `DataGridFastPathOptions` to enforce accessor-only adapters and surface missing accessor diagnostics:

```csharp
grid.FastPathOptions = new DataGridFastPathOptions
{
    UseAccessorsOnly = true,
    ThrowOnMissingAccessor = true
};

grid.FastPathOptions.MissingAccessor += (_, args) =>
    Debug.WriteLine($"{args.Feature}: {args.Message}");
```

If you need to bind or reuse a `DataGridFastPathOptions` instance from a view model, assign it in code-behind because it is a CLR property (not an AvaloniaProperty).
