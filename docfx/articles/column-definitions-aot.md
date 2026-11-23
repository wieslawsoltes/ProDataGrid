# Column Definitions: AOT-Friendly Bindings

`DataGridBindingDefinition` is the bridge between column definitions and compiled bindings. For AOT, avoid expression-based overloads and supply a prebuilt `CompiledBindingPath` or `IPropertyInfo` with typed delegates.

## Why this matters

Expression-based overloads require dynamic code generation. In AOT environments, use the overloads that accept `CompiledBindingPath` or `IPropertyInfo` so bindings are compiled ahead of time and no runtime expression compilation is needed.

## Pattern 1: CompiledBindingPath + delegates

```csharp
static string GetName(Person person) => person.Name;

static void SetName(Person person, string value) => person.Name = value;

var nameProperty = new ClrPropertyInfo(
    nameof(Person.Name),
    target => ((Person)target).Name,
    (target, value) => ((Person)target).Name = (string)value,
    typeof(string));

var namePath = new CompiledBindingPathBuilder()
    .Property(nameProperty, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
    .Build();

var nameBinding = DataGridBindingDefinition.Create<Person, string>(namePath, GetName, SetName);

var nameColumn = new DataGridTextColumnDefinition
{
    Header = "Name",
    Binding = nameBinding
};
```

Use `PropertyInfoAccessorFactory.CreateAvaloniaPropertyAccessor` when the source is an `AvaloniaProperty` instead of an INPC-backed CLR property.

## Pattern 2: IPropertyInfo + delegates

The `IPropertyInfo` overload builds the compiled binding path for you:

```csharp
var nameProperty = new ClrPropertyInfo(
    nameof(Person.Name),
    target => ((Person)target).Name,
    (target, value) => ((Person)target).Name = (string)value,
    typeof(string));

var nameBinding = DataGridBindingDefinition.Create<Person, string>(nameProperty, GetName, SetName);
```

If you reuse the same `IPropertyInfo` across many columns or grids, you can cache the compiled path:

```csharp
var namePath = DataGridCompiledBindingPathCache.GetOrCreate(nameProperty);
var nameBinding = DataGridBindingDefinition.Create<Person, string>(namePath, GetName, SetName);
```

There is also a convenience overload:

```csharp
var nameBinding = DataGridBindingDefinition.CreateCached<Person, string>(nameProperty, GetName, SetName);
```

You can pair this with `DataGridColumnDefinitionBuilder` to keep column construction typed and AOT-friendly:

```csharp
var builder = DataGridColumnDefinitionBuilder.For<Person>();
var nameColumn = builder.Text(\"Name\", nameProperty, GetName, SetName);
```

## Editing and binding modes

- Provide a setter when the column is editable.
- For read-only columns, omit the setter and set `IsReadOnly = true` or set `Binding.Mode = BindingMode.OneWay`.
- `DataGridBindingDefinition` supports `Mode`, `UpdateSourceTrigger`, `StringFormat`, `Converter`, and other binding options.

## Works across all column bindings

Any column definition that accepts `DataGridBindingDefinition` can use these AOT-friendly patterns:

- `DataGridTextColumnDefinition.Binding`
- `DataGridNumericColumnDefinition.Binding`
- `DataGridComboBoxColumnDefinition.SelectedItemBinding` / `SelectedValueBinding` / `TextBinding`
- `DataGridHyperlinkColumnDefinition.Binding` / `ContentBinding`
- `CellBackgroundBinding` / `CellForegroundBinding`

## Fast path accessors

`DataGridBindingDefinition` also provides a typed value accessor used by sorting, filtering, searching, and conditional formatting. For computed values or non-binding columns, set a custom accessor and value type:

```csharp
var totalColumn = new DataGridTextColumnDefinition
{
    Header = "Total",
    ValueAccessor = new DataGridColumnValueAccessor<Order, decimal>(o => o.Price * o.Quantity),
    ValueType = typeof(decimal),
    IsReadOnly = true
};
```

For model integration details, see [Column Definitions: Model Integration and Fast Path](column-definitions-models.md).
For an end-to-end fast path guide, see [Column Definitions: Fast Path Overview](column-definitions-fast-path-overview.md).

For the application-level reflection switch, supported targets, and compatibility limits, see [Trimming and NativeAOT](trimming-native-aot.md).
