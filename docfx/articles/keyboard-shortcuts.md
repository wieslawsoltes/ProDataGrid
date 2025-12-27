# Keyboard Shortcuts

## Default Gestures

| Action | Default | Notes |
| --- | --- | --- |
| Tab | `Tab` | Moves to the next/previous editable cell while editing; `Shift+Tab` reverses. When not editing, focus can leave the grid. |
| MoveUp | `Up` | Moves selection up. `Ctrl+Up` jumps to the first row. `Shift` extends selection in `SelectionMode=Extended`. |
| MoveDown | `Down` | Moves selection down. `Ctrl+Down` jumps to the last row. `Shift` extends selection in `SelectionMode=Extended`. |
| MoveLeft | `Left` | Moves to the previous column. `Ctrl+Left` jumps to the first column. Collapses row groups or hierarchical nodes; `Alt+Left` collapses the subtree. |
| MoveRight | `Right` | Moves to the next column. `Ctrl+Right` jumps to the last column. Expands row groups or hierarchical nodes; `Alt+Right` expands the subtree. |
| MovePageUp | `PageUp` | Moves up by a viewport page. `Shift` extends selection in `SelectionMode=Extended`. |
| MovePageDown | `PageDown` | Moves down by a viewport page. `Shift` extends selection in `SelectionMode=Extended`. |
| MoveHome | `Home` | Moves to the first column. `Ctrl+Home` jumps to the first row. |
| MoveEnd | `End` | Moves to the last column. `Ctrl+End` jumps to the last row. |
| Enter | `Enter` | Commits edits and moves down one row. `Ctrl+Enter` commits without moving. |
| CancelEdit | `Escape` | Cancels cell/row editing. |
| BeginEdit | `F2` | Begins editing the current cell (default also honors `Alt+F2`). |
| SelectAll | `Ctrl/Cmd+A` | Selects all rows/cells when `SelectionMode=Extended`. |
| Copy | `Ctrl/Cmd+C` | Copies selection to the clipboard (requires `ClipboardCopyMode` to be enabled). |
| CopyAlternate | `Ctrl/Cmd+Insert` | Alternate copy gesture. |
| Delete | `Delete` | Removes selected rows when `CanUserDeleteRows` and the data source allows deletion. |
| ExpandAll | `Multiply` | Expands all children under the current hierarchical node or group. |

## Override Defaults

Use `DataGrid.KeyboardGestureOverrides` to remap built-in actions. Any non-null gesture replaces the default mapping for that action; set `Key.None` to disable an action. Built-in handling always respects `e.Handled`, so custom `KeyDown` handlers can opt out of the defaults.

```xml
<DataGrid KeyboardGestureOverrides="{Binding KeyboardGestureOverrides}" />
```

```csharp
KeyboardGestureOverrides = new DataGridKeyboardGestures
{
    MoveDown = new KeyGesture(Key.J),
    MoveUp = new KeyGesture(Key.K),
    ExpandAll = new KeyGesture(Key.E)
};
```
