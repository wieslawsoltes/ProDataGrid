# Introduction

ProDataGrid is a high-performance DataGrid control for Avalonia. It builds on the classic `Avalonia.Controls.DataGrid` surface while adding richer models (selection, sorting, filtering), better scrolling behavior, and more control over virtualization and diagnostics.

## About

`ProDataGrid` is a hard fork of the original `Avalonia.Controls.DataGrid` control for [Avalonia](https://github.com/AvaloniaUI/Avalonia).

It displays repeating data in a customizable grid with enhanced features and improved performance, and is maintained as an independent NuGet package to evolve faster than the in-box control.

`ProDiagnostics` is a hard fork of the original `Avalonia.Diagnostics` for [Avalonia](https://github.com/AvaloniaUI/Avalonia).

## Features

| Area | Highlights |
| --- | --- |
| Virtualization & scrolling | ScrollViewer-based `ILogicalScrollable` presenter, smooth wheel/gesture handling, snap points, anchor support, predictive row prefetch, frozen columns. |
| Columns | Text, template, checkbox columns; auto/star/pixel sizing; reordering, resizing, visibility control, frozen sections. |
| Rows | Variable-height support with pluggable estimators; row details; grouping headers; selection modes; row headers. |
| Drag & drop | Opt-in row drag/drop with header/row handles, multi-row moves, routed events, pluggable handlers (flat + hierarchical before/after/inside), and built-in visuals/auto-scroll. |
| Editing & navigation | In-place editing, commit/cancel, keyboard navigation, clipboard copy modes, current cell tracking. |
| Data operations | Sorting, grouping, paging, currency management via `DataGridCollectionView` family; selection built on Avalonia `SelectionModel` for stable binding across sort/filter. |
| Styling & theming | Fluent/Simple v2 ScrollViewer templates, row/cell styling, template overrides, theme resources, focus/selection visuals. |
| Data binding | Auto-generates columns from `DataTable.DefaultView` and binds cells via TypeDescriptor (no manual indexers), `SelectedItems` two-way binding support, `DataGridCollectionView` for sorting/grouping/editing. |

## Supported Targets

- .NET 6.0 and 10.0; .NET Standard 2.0 for compatibility.
- Avalonia 11.3.x (see `Directory.Packages.props`).
- Windows, Linux, and macOS (via Avalonia's cross-platform stack).
