# Data Sources and Collection Views

ProDataGrid accepts any `IEnumerable` as an `ItemsSource` and wraps it in a view to provide sorting, filtering, grouping, paging, and currency.

## ItemsSource and View Creation

When you set `ItemsSource`, the grid chooses a view in this order:

1. If the source is an `IDataGridCollectionView`, it is used directly.
2. If the source implements `IDataGridCollectionViewFactory`, the grid calls `CreateView()` to get a custom view.
3. Otherwise, the grid wraps the source in `DataGridCollectionView`.

This means you can supply your own view for server-side or specialized data pipelines without subclassing the grid.

## DataGridCollectionView Basics

`DataGridCollectionView` lives in `Avalonia.Collections` and provides:

- Sorting (`SortDescriptions`)
- Filtering (`Filter`)
- Grouping (`GroupDescriptions`)
- Paging (`PageSize`, `PageIndex`, `MoveToPage`)
- Currency (`CurrentItem`, `CurrentChanged`)
- Batch updates (`DeferRefresh`)

Example:

```csharp
using Avalonia.Collections;
using System.ComponentModel;

var view = new DataGridCollectionView(items);
view.SortDescriptions.Add(DataGridSortDescription.FromPath("Name", ListSortDirection.Ascending));
view.Filter = item => ((Person)item).IsActive;
view.GroupDescriptions.Add(new DataGridPathGroupDescription("Department"));
view.PageSize = 50;

grid.ItemsSource = view;
```

## Editing, Add/Delete, and New-Row Placeholder

`DataGridCollectionView` implements `IDataGridEditableCollectionView`, so it supports:

- Add: `CanAddNew`, `AddNew`, `CommitNew`, `CancelNew`
- Edit: `EditItem`, `CommitEdit`, `CancelEdit`
- Remove: `CanRemove`, `Remove`, `RemoveAt`

When `CanUserAddRows="True"` and the view supports adding, the grid shows a new-row placeholder. You can customize it per column with `DataGridTemplateColumn.NewRowCellTemplate`.

## DataTable and TypeDescriptor Support

When binding to `DataTable.DefaultView`, the grid uses `TypeDescriptor` to resolve column metadata and cell bindings. This allows auto-generated columns without manual indexers.

## Trimming Notes

`DataGridCollectionView` uses reflection and `TypeDescriptor` only while reflection is enabled. In a trimmed or Native AOT application it reads values through column accessors and compiled bindings, and `AddNew` requires `NewItemFactory`. See [Trimming and Native AOT](trimming-and-native-aot.md).
