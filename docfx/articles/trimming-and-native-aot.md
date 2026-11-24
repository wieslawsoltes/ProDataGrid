# Trimming and Native AOT

ProDataGrid publishes without trimming or AOT warnings.
Every code path that inspects items through reflection or `TypeDescriptor` runs only while the `ProDataGrid.IsReflectionEnabledByDefault` feature switch is on.

The switch is on by default.
`ProDataGrid.targets`, which the package imports into the consuming project, turns it off when the project sets `PublishTrimmed` or `PublishAot`.
The trimmer then removes the guarded code.
The switch is off in a regular build of such a project too, and a debug run behaves like the published application.

## Values without reflection

With the switch off, the grid reads item values from typed sources only:

| Feature | Source of values |
| --- | --- |
| Cells, editing, and validation | The compiled binding of the column. |
| Sorting, filtering, distinct-value filtering, searching, summaries, conditional formatting, formula references, and clipboard paste | The value accessor of the column. A column without one uses its compiled binding when the binding path equals the sort member path of the column. |
| Columns from `ColumnDefinitionsSource` | The accessors of the definitions. See [AOT-Friendly Bindings](column-definitions-aot.md) and [Source Generators](column-definitions-source-generators.md). |
| Combo box items | `DataGridComboBoxColumn.DisplayMemberBinding` and `SelectedValueMemberBinding`. |
| New rows | `DataGridCollectionView.NewItemFactory`. |
| Groups | `DataGridGeneratedGroupDescription<TItem, TValue>` or a custom `DataGridGroupDescription`. |
| Sorting from code | `DataGridSortDescription.FromAccessor` or `DataGridSortDescription.FromComparer`. |
| Hierarchical children | `HierarchicalOptions.ChildrenSelector` or `HierarchicalModel<T>`. |

A grid declared in XAML with compiled bindings needs no further setup:

```xml
<UserControl x:Name="Root" x:DataType="local:PeopleViewModel">
  <DataGrid ItemsSource="{Binding People}">
    <DataGrid.Columns>
      <DataGridTextColumn Header="Name" x:DataType="local:Person" Binding="{Binding Name}" />
      <DataGridTextColumn Header="Age" x:DataType="local:Person" Binding="{Binding Age}" />
      <DataGridComboBoxColumn Header="Department"
                              ItemsSource="{Binding #Root.((local:PeopleViewModel)DataContext).Departments}"
                              SelectedItemBinding="{Binding Department, x:DataType=local:Person}"
                              DisplayMemberBinding="{Binding Name, x:DataType=local:Department}" />
    </DataGrid.Columns>
  </DataGrid>
</UserControl>
```

A header click sorts these columns, and the filter, search, and summary features read the same properties the cells show.

Groups and new rows come from typed delegates:

```csharp
var people = new DataGridCollectionView(items)
{
    NewItemFactory = () => new Person()
};

people.GroupDescriptions.Add(
    new DataGridGeneratedGroupDescription<Person, string>("department", person => person.Department.Name));
```

The preceding code creates a `Person` for every new row and groups the rows by the name of the department.

## Features that require reflection

With the switch off, the following features are unavailable, and the grid logs a sort it skips:

- `AutoGenerateColumns`.
- `DataGridSortDescription.FromPath`, `DataGridPathGroupDescription`, and path group descriptions restored from saved state.
- A column whose `SortMemberPath` differs from the path of its binding, unless the column has a value accessor.
- `DataGridComboBoxColumn.DisplayMemberPath` and `SelectedValuePath`.
- `HierarchicalOptions.ChildrenPropertyPath`, which reports an `InvalidOperationException` as the load error of the node, and `IsExpandedPropertyPath`, which throws one.
- `DataTable` sources and other items described through `TypeDescriptor`.
- `AddNew` without `NewItemFactory`.
  `CanAddNew` returns `false`.

Reflection bindings, such as `new Binding("Name")` in code or a XAML binding with `x:CompileBindings="False"`, are an Avalonia limitation and report their own trimming warnings.

## Keeping reflection in a trimmed application

Set the `ProDataGridIsReflectionEnabledByDefault` MSBuild property to keep the reflection paths in a trimmed or Native AOT publication:

```xml
<PropertyGroup>
  <ProDataGridIsReflectionEnabledByDefault>true</ProDataGridIsReflectionEnabledByDefault>
</PropertyGroup>
```

The application then owns every trimming failure of the item types the grid inspects.

## Verification

`tests/Avalonia.Controls.DataGrid.AotSmoke` declares a grid in XAML with compiled bindings.
The CI publishes it with Native AOT and treats every trimming warning as an error.
The native binary runs sorting, filtering, searching, editing, grouping, and combo box scenarios.
`tests/ProDataGrid.SourceGeneration.AotSmoke` covers generated schemas.
