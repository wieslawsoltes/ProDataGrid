# Introduction

ProDataGrid is a high-performance DataGrid control for Avalonia. It builds on the classic `Avalonia.Controls.DataGrid` surface while adding model-driven data operations, improved scrolling behavior, and stronger customization hooks for virtualization, styling, and diagnostics.

## About

`ProDataGrid` is a hard fork of the original `Avalonia.Controls.DataGrid` control for [Avalonia](https://github.com/AvaloniaUI/Avalonia). It stays API compatible where it matters while moving faster on performance, data operations, and advanced scenarios.

`ProDiagnostics` is a hard fork of the original `Avalonia.Diagnostics` with the same goal: ship developer tools independently from the main Avalonia package.

## Key Capabilities

| Area | Highlights |
| --- | --- |
| Virtualization & scrolling | ScrollViewer-based `ILogicalScrollable` presenter, snap points, scroll anchoring, row height estimators, frozen columns. |
| Columns | Wide range of built-in column types, flexible sizing, reordering/resizing, auto-generation, bindable columns. |
| Rows | Variable-height support, row details, group headers/footers, selection modes, row headers. |
| Drag & drop | Opt-in row drag/drop with handle control, multi-row moves, routed events, pluggable handlers (flat + hierarchical). |
| Editing & navigation | In-place editing, commit/cancel flow, keyboard navigation, clipboard copy and export. |
| Data operations | Sorting, filtering, grouping, paging, and search via `DataGridCollectionView` and model adapters. |
| Styling & theming | Fluent/Simple v2 templates, theme resources, control themes, pseudo-classes for states. |
| State & diagnostics | Capture/restore state, selection stability, row lifecycle hooks, and ProDiagnostics (managed + remote attach/web diagnostics tooling). |

## Architecture Snapshot

ProDataGrid exposes explicit models for key operations so you can integrate with external pipelines or server-side APIs:

- `SelectionModel` (stable selection)
- `SortingModel`, `FilteringModel`, and `SearchModel` (descriptors + adapters)
- `HierarchicalModel` (flattened view of tree data)
- `DataGridCollectionView` (sorting/filtering/grouping/paging + currency on any `IEnumerable`)

## Supported Targets

- .NET 6.0, .NET 8.0, and .NET 10.0; .NET Standard 2.0 for compatibility.
- Avalonia 11.3.x (see `Directory.Packages.props`).
- Windows, Linux, and macOS (via Avalonia's cross-platform stack).
