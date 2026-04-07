# Sample Gallery

The sample app (`src/DataGridSample`) is the fastest way to explore ProDataGrid features and real-world configurations.

Highlights include:

- Column types, editing, and validation.
- Bindable columns, auto-generation, and `DataTable.DefaultView` binding.
- Sorting/filtering/search models, grouping, and paging.
- Header context menus with filter flyouts and column visibility toggles.
- Selection stability, selection origin logging, and current cell tracking.
- Selection index resolution performance samples (interface, built-in cache, and resolver hook).
- Selection units and highlighting (row, column, and cell visuals).
- Clipboard export formats and export customization.
- Pivot tables with layout toggles, totals, and percent displays.
- Pivot table layouts for report filters, values in rows, percent of total, and show items with no data.
- Pivot value filters, value sorting, running totals, differences from previous, percent of parent totals, and index calculations.
- Pivot calculated measures, slicers, and pivot chart model data.
- ProCharts gallery with multiple chart types, axis options, legends, and export/clipboard actions.
- Outline reports with hierarchical grouping, subtotals, and detail rows.
- Range interaction model samples (drag thresholds, selection anchors, fill handle ranges).
- Clipboard import model samples (custom paste parsing and transforms).
- Editing interaction model samples (custom edit triggers and input handling).
- Fill handle auto-fill, including custom fill models.
- Conditional formatting model samples (cell and row themes, Power Fx and Excel-like scenarios).
- Hierarchical data, hierarchical drag/drop, tree-like mimics, and live row-drag session inspection.
- Row drag session samples with move/copy transitions, invalid-target feedback badges, and selection-drag coordination.
- Virtualization and scrolling diagnostics (large datasets, row height estimators, recycling).
- Variable-height scrolling with `DataGridCustomDrawingColumn`, composition custom-visual backend, Skia custom draw operations, shared text cache, and layout fast path.
- Live custom-drawing updates with composition backend + factory-driven invalidation (`DataGridCustomDrawingColumn.InvalidateCustomDrawingCells` and `IDataGridCellDrawOperationInvalidationSource`).
- Styling showcases and column theme usage.
- Column banding with stacked headers for non-pivot tables.

Run it locally:

```bash
dotnet run --project src/DataGridSample/DataGridSample.csproj
```
