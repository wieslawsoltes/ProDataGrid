# Column Header Menus and Filter Flyouts

This guide covers header context menus, programmatic filter flyout display, and filter status indicators for ProDataGrid.

## UX Reference (Common Grids)

In mature grid controls, column headers typically support:

- Right-click context menus on the header surface and its icons.
- Menu actions for sorting, clearing sorts, showing filters, and hiding columns.
- A visible filter indicator when a column is filtered, even if the filter button is hidden.

ProDataGrid now follows the same UX patterns using Avalonia concepts such as `ContextMenu`, `Flyout`, and templated parts.

## New API Surface

- `DataGrid.ColumnHeaderContextMenu`: default context menu for all column headers.
- `DataGridColumn.HeaderContextMenu`: per-column override.
- `DataGridColumn.TryShowFilterFlyout()`: programmatically show a column's filter flyout.
- `DataGridColumnHeader.TryShowFilterFlyout()`: programmatically show the header's filter flyout.
- `DataGrid.ClearFilter(DataGridColumn column)`: clear the active filter for a column.
- `DataGridColumn.ClearFilter()`: clear the active filter for the column instance.

## Context Menu with Filter Actions

Define a shared header context menu and use `DataGrid.ColumnHeaderContextMenuColumnId` as the command parameter. This works for right-clicks on the header surface and on header icons:

```xml
<ContextMenu x:Key="HeaderContextMenu"
             x:DataType="viewModels:HeaderContextMenuViewModel"
             DataContext="{Binding $parent[DataGrid].DataContext}">
  <MenuItem Header="Show filter"
            Command="{Binding ShowFilterFlyoutCommand}"
            CommandParameter="{Binding $parent[DataGrid].ColumnHeaderContextMenuColumnId}" />
  <MenuItem Header="Clear filter"
            Command="{Binding ClearColumnFilterCommand}"
            CommandParameter="{Binding $parent[DataGrid].ColumnHeaderContextMenuColumnId}" />
</ContextMenu>

<DataGrid ColumnHeaderContextMenu="{StaticResource HeaderContextMenu}">
  <!-- Columns here -->
</DataGrid>
```

## Programmatic Filter Flyout

You can open a filter flyout from code or a view model command:

```csharp
if (column.TryShowFilterFlyout())
{
    // The flyout is now open.
}
```

To clear a filter programmatically:

```csharp
column.ClearFilter();
```

## Filter Status Icon

When a column is filtered and the filter button is hidden, the header now shows a filter status icon. This is driven by the header template and the `:filtered` pseudo class, so it stays consistent across themes and re-templating.

## Sample

See the sample page in the gallery:

- **Header Context Menu** in the sample app (`MainWindow`) demonstrates:
  - Context menu actions on headers and icons
  - Programmatic filter flyout display
  - Column visibility toggles

Run the gallery:

```bash
dotnet run --project src/DataGridSample/DataGridSample.csproj
```
