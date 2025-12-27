# Selection and Navigation

Selection in ProDataGrid is built on Avalonia's `SelectionModel`, which provides stable selection across sorting, filtering, and paging. The grid still exposes the familiar `SelectedItem` and `SelectedItems` bindings while enabling richer scenarios.

## Using SelectionModel with DataGrid

Bind `Selection` to your view-model's `SelectionModel<T>`:

```xml
<DataGrid ItemsSource="{Binding Items}"
          Selection="{Binding MySelectionModel}"
          SelectionMode="Extended" />
```

```csharp
using Avalonia.Controls.Selection;

public SelectionModel<MyItem> MySelectionModel { get; } = new()
{
    SingleSelect = false
};
```

- Let the grid assign `Selection.Source` to its collection view; avoid pre-setting `Source` to prevent mismatches.
- You can share the same `SelectionModel<T>` with other controls (e.g., `ListBox Selection="{Binding MySelectionModel}"`) to keep selection in sync.
- `SelectedItems` binding still works; when `Selection` is set, it reflects the model's selection and updates it when the binding changes.

## Selection Model Integration

ProDataGrid routes row selection through Avalonia's `SelectionModel<object?>`, giving stable `SelectedItem/SelectedItems/SelectedIndex` bindings across sorting, filtering, paging, and collection mutations. Highlights:

- `SelectedItems` remains an `IList` (for bindings) but is backed by the selection model; adding/removing in the bound collection updates the grid, and vice versa.
- Selection survives collection changes (including sorted `DataGridCollectionView` inserts/moves) without losing currency; current row is preserved when possible.
- Multi-select gestures and `SelectionMode` map to the model (`SelectionMode=Single` maps to `SingleSelect=true`).
- A thin adapter keeps row index to slot mapping internal, so custom selection models can be injected later.

## SelectedItems and SelectedCells

You can still bind `SelectedItems` and `SelectedCells` directly:

```xml
<DataGrid ItemsSource="{Binding Items}"
          SelectedItems="{Binding SelectedItems, Mode=TwoWay}"
          SelectionMode="Extended"
          HeadersVisibility="All" />
```

```xml
<DataGrid ItemsSource="{Binding Items}"
          SelectedCells="{Binding SelectedCells, Mode=TwoWay}"
          SelectionUnit="Cell"
          SelectionMode="Extended"
          HeadersVisibility="All" />
```

When `SelectionUnit` includes cells, `SelectedCells` is an `IList<DataGridCellInfo>` that you can bind and inspect.

## Selection Stability and Paging

- Selection stays attached to items as rows are inserted, removed, shuffled, filtered, or sorted.
- With `DataGridCollectionView` paging, you can keep a single `SelectionModel` and preserve selections across page changes.
- In grouped views, selection targets data rows (group headers are ignored).

## Auto-Scroll to Selection

Turn on `AutoScrollToSelectedItem` to keep the current selection in view without handling `SelectionChanged` manually:

```xml
<DataGrid ItemsSource="{Binding Items}"
          AutoScrollToSelectedItem="True" />
```

## Selection Change Origin

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
        Debug.WriteLine($"Source={dg.Source}, user={dg.IsUserInitiated}, trigger={dg.TriggerEvent?.GetType().Name ?? \"none\"}");
    }
}
```

See the "Selection Origin" sample page in `DataGridSample` to observe the flags for pointer/keyboard, `SelectAll()`, `SelectionModel`, bindings, and ItemsSource changes.

## Cell and Row Selection

- `SelectionUnit=FullRow` or `Cell` controls selection granularity.
- `CurrentCell` and `CurrentItem` are kept in sync with keyboard navigation.

## CurrentCell Binding

Bind `CurrentCell` to track or drive the current cell, then react via `CurrentCellChanged`:

```xml
<DataGrid ItemsSource="{Binding Items}"
          SelectionUnit="Cell"
          CurrentCell="{Binding CurrentCell, Mode=TwoWay}"
          CurrentCellChanged="OnCurrentCellChanged" />
```

```csharp
using Avalonia.Controls;

public DataGridCellInfo CurrentCell { get; set; }
```

## Keyboard Gestures

Override built-in gestures via `KeyboardGestureOverrides`:

```csharp
KeyboardGestureOverrides = new DataGridKeyboardGestures
{
    MoveDown = new KeyGesture(Key.J),
    MoveUp = new KeyGesture(Key.K)
};
```

## Clipboard Copy

Enable clipboard copying via `ClipboardCopyMode`, then use standard copy gestures (`Ctrl/Cmd+C`) or the alternate (`Ctrl/Cmd+Insert`). See [Clipboard and Export](clipboard-and-export.md) for formats and customization.
