# Events and Routing

ProDataGrid exposes a large set of routed events so you can observe grid behavior without subclassing. Most of them use bubbling routing, so you can attach handlers on the grid or a parent container.

## Core Routed Events

Common categories you can hook:

- Row lifecycle: `LoadingRow`, `UnloadingRow`, `LoadingRowGroup`, `UnloadingRowGroup`.
- Column changes: `AutoGeneratingColumn`, `ColumnReordering`, `ColumnReordered`, `ColumnDisplayIndexChanged`, `Sorting`.
- Editing: `BeginningEdit`, `PreparingCellForEdit`, `CellEditEnding`, `CellEditEnded`, `RowEditEnding`, `RowEditEnded`.
- Navigation: `CurrentCellChanged`, `SelectionChanged`, `CellPointerPressed`.
- Row details: `LoadingRowDetails`, `UnloadingRowDetails`, `RowDetailsVisibilityChanged`.
- Clipboard: `CopyingRowClipboardContent`.
- Scrolling: `HorizontalScroll`, `VerticalScroll`.
- Drag and drop: `RowDragStarting`, `RowDragCompleted`.

## Hooking Events in XAML

```xml
<DataGrid LoadingRow="OnLoadingRow"
          AutoGeneratingColumn="OnAutoGeneratingColumn"
          Sorting="OnSorting"
          CurrentCellChanged="OnCurrentCellChanged"
          CopyingRowClipboardContent="OnCopyingRowClipboardContent"
          RowEditEnded="OnRowEditEnded" />
```

## Hooking Routed Events in Code

Use `AddHandler` for lower-level routed events and for hierarchical toggles.

```csharp
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Interactivity;

AddHandler(DataGridColumnHeader.LeftClickEvent, OnHeaderLeftClick, RoutingStrategies.Bubble);
AddHandler(DataGridHierarchicalPresenter.ToggleRequestedEvent, OnToggleRequested, RoutingStrategies.Bubble);
```

The sample `RoutedEventsPage` in `src/DataGridSample` logs these events so you can see the event flow in real time.
