# Drag and Drop

ProDataGrid provides opt-in row drag and drop with built-in visuals and auto-scroll. You can target flat or hierarchical lists with pluggable handlers.

## Row Drag and Drop Quick Start

- Turn on `CanUserReorderRows="True"` (default handle is the row header) and choose `RowDragHandle` (`RowHeader`, `Row`, or `RowHeaderAndRow`).
- Toggle `RowDragHandleVisible` to show or hide the drag handle glyph.
- Configure effects via `RowDragDropOptions` (`AllowedEffects`, `DragSelectedRows`, and drag thresholds) and plug in a drop handler when you need custom moves. Built-ins: `DataGridRowReorderHandler` (flat lists) and `DataGridHierarchicalRowReorderHandler` (hierarchical; supports before/after/inside like TreeDataGrid).
- `RowDragStarting`/`RowDragCompleted` are routed events for telemetry/customization; the grid auto-updates selection after a successful drop.
- Dropping in the top/bottom thirds of a hierarchical row inserts before/after; the middle inserts inside that node's children.

```xml
<DataGrid ItemsSource="{Binding Items}"
          CanUserReorderRows="True"
          RowDragHandle="RowHeaderAndRow"
          RowDragHandleVisible="{Binding ShowHandle}"
          RowDragDropOptions="{Binding Options}"
          RowDropHandler="{Binding DropHandler}" />
```

```csharp
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;

public DataGridRowDragDropOptions Options { get; } = new DataGridRowDragDropOptions
{
    AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy,
    DragSelectedRows = true
};

public IDataGridRowDropHandler DropHandler { get; } = new DataGridRowReorderHandler();
```

## Hierarchical Drag and Drop

Use `DataGridHierarchicalRowReorderHandler` to support before/after/inside drop targets in tree-like data sets.

```xml
<DataGrid ItemsSource="{Binding HierarchicalItems}"
          CanUserReorderRows="True"
          HierarchicalRowsEnabled="True"
          RowDragHandle="RowHeaderAndRow"
          RowDragDropOptions="{Binding Options}"
          RowDropHandler="{Binding DropHandler}" />
```

```csharp
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;

public DataGridRowDragDropOptions Options { get; } = new DataGridRowDragDropOptions
{
    AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy,
    DragSelectedRows = true
};

public IDataGridRowDropHandler DropHandler { get; } = new DataGridHierarchicalRowReorderHandler();
```
