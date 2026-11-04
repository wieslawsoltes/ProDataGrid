# Visual layout modes

ProDataGrid provides three visual layout modes. The mode is selected per grid, so
an application can use the compatibility layout for feature-rich grids and a
lighter layout for large, fixed-height grids.

| Mode | Display representation | Recommended use |
| --- | --- | --- |
| `Nested` | Each realized row owns a cells presenter and retained cell controls. | Default choice for arbitrary templates, variable row heights, row details, group rows, and custom row features. |
| `Flat` | Realized rows and cells are retained controls arranged by one rows presenter. | Fixed-height grids that need retained cell controls with a smaller visual tree. |
| `Virtualized` | Compatible display cells are drawn by one virtual surface. Editors and compatibility controls are created when required. | Large, read-mostly grids with fixed row heights, explicit column widths, and typed value accessors. |

`Nested` remains the default. Existing grids are unchanged until they opt into a
different mode.

## Load the flat-layout resources

Load one Avalonia theme, the matching ProDataGrid v2 theme, and the flat resource
dictionary:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Flat.xaml" />
</Application.Styles>
```

For the Simple theme, replace `FluentTheme` and `Fluent.v2.xaml` with
`SimpleTheme` and `Simple.v2.xaml`.

`Flat.xaml` is additive. Loading it does not change existing grids. It provides
the keyed `DataGridFlatTheme`, which supplies the flat row template and selects
`Flat` by default.

## Use Flat mode

Apply `DataGridFlatTheme` to a grid with a finite row height and explicit pixel or
star column widths:

```xml
<DataGrid Theme="{StaticResource DataGridFlatTheme}"
          VisualLayoutMode="Flat"
          ItemsSource="{CompiledBinding Rows}"
          AutoGenerateColumns="False"
          HeadersVisibility="Column"
          RowHeight="32">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Id"
                        Width="80"
                        x:DataType="models:Person"
                        Binding="{CompiledBinding Id}" />
    <DataGridTextColumn Header="Name"
                        Width="*"
                        x:DataType="models:Person"
                        Binding="{CompiledBinding Name}" />
  </DataGrid.Columns>
</DataGrid>
```

The example assumes that the view declares the `models` XML namespace.

Flat mode keeps normal row and cell controls. Selection, editing, validation,
keyboard input, and automation continue to use the retained control model. The
main restriction is the flat row template: it is intended for column-only headers
and does not host row headers or row details.

## Use Virtualized mode

Use the same theme and set `VisualLayoutMode="Virtualized"`:

```xml
<DataGrid Theme="{StaticResource DataGridFlatTheme}"
          VisualLayoutMode="Virtualized"
          ItemsSource="{CompiledBinding Rows}"
          ColumnDefinitionsSource="{CompiledBinding Columns}"
          AutoGenerateColumns="False"
          HeadersVisibility="Column"
          RowHeight="32" />
```

Virtualized mode also requires compatible typed value accessors. Column
definitions created with `DataGridBindingDefinition` provide both the compiled
binding and the accessor metadata needed by the surface. See
[Virtualized cell surface](virtual-surface-architecture.md) for supported columns,
configuration, and fallback behavior.

## Choose a mode

Start with `Nested` when compatibility is the priority. Choose `Flat` when all of
the following are true:

- rows have a fixed, finite height;
- visible columns use pixel or star widths;
- row headers and row details are not required; and
- retained controls or templates are still needed for display.

Choose `Virtualized` when the Flat requirements are met and the visible columns
can use the supported surface renderers and typed accessors.

| Requirement or feature | `Nested` | `Flat` | `Virtualized` |
| --- | ---: | ---: | ---: |
| Fixed-height rows | Supported | Required | Required for the optimized rowless path |
| Pixel and star column widths | Supported | Supported | Supported |
| Auto or size-to-cells widths | Supported | Use with care | Retained fallback |
| Row headers and row details | Supported | Use `Nested` | Retained compatibility path; prefer `Nested` |
| Collection-view group rows | Supported | Use `Nested` | Retained compatibility path; prefer `Nested` |
| Arbitrary cell templates and themes | Supported | Supported | Retained fallback |
| Selection and current cell | Supported | Supported | Supported |
| Editing and validation | Supported | Supported | Supported through retained editors |
| UI Automation | Supported | Supported | Supported through retained compatibility rows |
| Hierarchical rows | Supported | Supported | Supported for compatible columns |

Fallback is intentional. If a visible column or grid feature cannot be represented
faithfully by the virtual surface, ProDataGrid keeps the requested mode but uses
retained flat cells for display.

## Hierarchical data

Visual layout does not replace the hierarchy model. Configure
`HierarchicalModel`, `HierarchicalRowsEnabled`, and a
`DataGridHierarchicalColumn` as usual, then choose the visual mode independently.
Expansion, collapse, and selection remain owned by the hierarchy and selection
models.

For the Virtualized path, the hierarchy column must use a compatible typed text
accessor and the grid must otherwise satisfy the surface requirements. A custom
hierarchy cell template uses retained fallback.

## Sample pages

The sample gallery provides matched pages for both flat and hierarchical data:

| Data shape | `Nested` | `Flat` | `Virtualized` |
| --- | --- | --- | --- |
| Flat rows | `NestedSurfaceFlatDataPage` | `FlatSurfaceFlatDataPage` | `VirtualSurfaceFlatDataPage` |
| Hierarchical rows | `NestedSurfaceHierarchyPage` | `FlatSurfaceHierarchyPage` | `VirtualSurfaceHierarchyPage` |

The Virtualized pages include a mode selector for supported display columns. Use
these pages to verify appearance and interaction before adapting the configuration
to an application.

## Troubleshooting

If Virtualized mode appears to use retained cells, check that:

1. `DataGridFlatTheme` is applied;
2. `RowHeight` is finite and greater than zero;
3. every visible column uses a pixel or star width;
4. every visible column is supported by the surface;
5. every surface-rendered column has a compatible typed value accessor; and
6. the grid does not use a custom cell theme, row details, group rows, cell
   lifecycle handlers, conditional formatting, or search highlighting.

Use `Nested` for a grid whose required behavior is outside the flat-layout
contract. Layout mode is a per-grid choice, not an application-wide setting.

## Related articles

- [Virtualized cell surface](virtual-surface-architecture.md)
- [Scrolling and virtualization](scrolling-virtualization.md)
- [Optimized retained and drawn cells](optimized-cell-paths.md)
- [Column definitions: AOT-friendly bindings](column-definitions-aot.md)
- [Benchmarking layout performance](layout-performance-benchmarking.md)
