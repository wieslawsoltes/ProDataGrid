# Drag (Slide) Selection

ProDataGrid supports "slide" selection like WPF: press the left mouse button and drag across rows or cells to extend selection.

## Behavior

- **FullRow**: drag up or down to extend the row selection from the anchor row.
- **Cell**: drag to select a rectangular range of cells.
- **CellOrRowHeader**: drag a row header for row selection; drag inside cells for cell selection.
- **SelectionMode**: works in `Single` (selection follows the pointer) and `Extended` (range selection).

The selection anchor is set by the initial click, so dragging behaves like a Shift-extend gesture without requiring the Shift key.

## Auto-Scroll

Dragging outside the visible rows or cells starts auto-scrolling, keeping the selection range expanding as the grid scrolls.

## Notes

- Drag selection uses the mouse pointer; touch and pen are left to scroll and gesture interactions.
- `SelectionChanged` includes `Source=Pointer` and carries the triggering pointer event for diagnostics.
- Holding Ctrl/Cmd preserves existing selections while the drag range updates.
- The hover row (`:pointerover`) refreshes when drag selection ends to match the row under the pointer.

## Example

```xml
<DataGrid ItemsSource="{Binding Items}"
          SelectionMode="Extended"
          SelectionUnit="Cell" />
```
