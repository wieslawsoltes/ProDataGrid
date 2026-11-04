# Virtualized cell surface

`DataGridVisualLayoutMode.Virtualized` is the lightest display path for large,
fixed-height grids. Compatible display cells are drawn on one surface instead of
creating a `DataGridCell` control for every visible row and column.

The feature keeps the existing data, column, hierarchy, selection, and editing
models. When an operation needs control semantics, ProDataGrid creates the normal
retained controls for that operation. Unsupported display configurations use
retained flat cells automatically.

## When to use it

Virtualized mode is designed for grids that are:

- large and read-mostly;
- fixed-height;
- configured with explicit pixel or star column widths;
- built from supported ProDataGrid columns; and
- backed by typed value accessors.

Use `Nested` or `Flat` when display cells require arbitrary controls, templates,
dynamic resources, converters with custom binding behavior, variable row heights,
row details, or group rows.

## Configure the grid

First load the normal theme and the flat-layout resources:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Flat.xaml" />
</Application.Styles>
```

Then apply `DataGridFlatTheme` and select `Virtualized`:

```xml
<DataGrid Theme="{StaticResource DataGridFlatTheme}"
          VisualLayoutMode="Virtualized"
          ItemsSource="{CompiledBinding Rows}"
          ColumnDefinitionsSource="{CompiledBinding Columns}"
          AutoGenerateColumns="False"
          HeadersVisibility="Column"
          RowHeight="32"
          Selection="{CompiledBinding SelectionModel}"
          SelectedItem="{CompiledBinding SelectedItem, Mode=TwoWay}"
          SelectionMode="Single"
          SelectionUnit="FullRow"
          UseLogicalScrollable="True" />
```

The keyed theme selects `Flat` by default, so the explicit
`VisualLayoutMode="Virtualized"` setting is required.

## Provide typed accessors

The surface reads display values through `IDataGridColumnValueAccessor`. The
recommended approach is to create columns from `DataGridColumnDefinition` objects
and use `DataGridBindingDefinition` with typed getter and optional setter delegates.
This supplies compiled binding metadata without reflection.

```csharp
DataGridBindingDefinition nameBinding =
    DataGridBindingDefinition.Create<Person, string>(
        nameProperty,
        static person => person.Name,
        static (person, value) => person.Name = value);

DataGridTextColumnDefinition nameColumn = new()
{
    Header = "Name",
    Binding = nameBinding,
    Width = new DataGridLength(2, DataGridLengthUnitType.Star)
};
```

`nameProperty` is an `IPropertyInfo` for `Person.Name`. See
[Column definitions: AOT-friendly bindings](column-definitions-aot.md) for complete
`IPropertyInfo` and `CompiledBindingPath` examples.

Keep column construction and typed access in the ViewModel so the view remains a
passive compiled-binding surface.

## Supported display columns

The virtual surface supports the following built-in display configurations:

| Column | Surface behavior | Important requirements |
| --- | --- | --- |
| `DataGridTextColumn` | Draws formatted text. | Compatible typed text accessor. |
| `DataGridNumericColumn` | Draws formatted, aligned numeric text. | Direct binding and typed accessor. |
| `DataGridCheckBoxColumn` | Draws a two- or three-state indicator. | Direct binding and typed accessor. |
| `DataGridDatePickerColumn` | Draws short, long, or custom date text. | Direct binding and typed `DateTime` accessor. |
| `DataGridTimePickerColumn` | Draws formatted time text. | Direct binding and typed `TimeSpan` accessor. |
| `DataGridMaskedTextColumn` | Draws the raw bound text. | Direct text binding and typed text accessor. Mask prompts remain editor behavior. |
| `DataGridAutoCompleteColumn` | Draws the raw bound text. | Direct text binding and typed text accessor. Suggestions remain editor behavior. |
| `DataGridSliderColumn` | Draws formatted value text. | `ShowValueText`, direct binding, and typed text accessor. |
| `DataGridComboBoxColumn` | Draws editable text and a dropdown glyph. | Editable direct `TextBinding`, no selected-item/value binding, and typed text accessor. |
| `DataGridImageColumn` | Draws an image using the configured stretch behavior. | Fixed image dimensions, direct binding, and typed accessor. |
| `DataGridProgressBarColumn` | Draws the progress track and value. | Direct binding, typed accessor, fixed bar height, and no progress text. |
| `DataGridHierarchicalColumn` | Draws indentation, expander, and text. | No custom cell template and a compatible typed text accessor. |

All visible columns must be eligible. If one visible column is unsupported, the
grid uses retained fallback for the display surface.

## Selection, hierarchy, and input

Selection and current-cell state use the normal ProDataGrid models. Pointer and
keyboard input remain available, including hierarchy expander interaction. Bind a
ViewModel-owned selection model as shown above when application state needs to
observe or restore selection.

For hierarchical data, configure `HierarchicalModel`,
`HierarchicalRowsEnabled`, and a compatible `DataGridHierarchicalColumn` as on a
normal grid. Expansion changes the flattened item sequence; the visual mode only
changes how the visible rows are displayed.

## Editing and validation

Editing uses the existing column editors and binding pipeline. When editing
starts, ProDataGrid materializes the required retained row and cell, overlays the
normal editor, and uses the normal commit, cancel, and validation behavior. The
grid can return to the surface display after editing completes.

The display renderer is intentionally not a second editor implementation. Editor
features such as masks, autocomplete suggestions, ComboBox item templates, and
slider interaction remain available through their retained editors.

## Automatic retained fallback

Virtualized mode uses retained flat cells when the surface cannot preserve the
requested behavior. Common fallback causes include:

- a visible unsupported or derived column;
- a missing or incompatible typed accessor;
- `Auto` or `SizeToCells` width on a visible column;
- a grid-wide or column-specific cell theme;
- a template column or custom hierarchy cell template;
- row details, row headers, group rows, or item-owned row containers;
- conditional formatting or search descriptors;
- cell preparation or clearing handlers; and
- automation or another operation that requires retained row semantics.

Fallback preserves correctness. It is not an error, but it may change the
performance characteristics of the grid. When profiling, validate that the
workload still satisfies the surface requirements.

## Value updates

Visible items that implement `INotifyPropertyChanged` can invalidate their drawn
values without creating a binding expression for every display cell. Configure the
column's direct-value tracking option when using a column type that exposes one.
Disable tracking only for values that are immutable while visible.

Changing column visibility, width, formatting, or another eligibility setting may
switch the grid between the surface and retained fallback. ProDataGrid refreshes
the visible display when the configuration changes.

## Sample pages

The sample gallery includes two dedicated pages:

- `VirtualSurfaceFlatDataPage` demonstrates flat collections and can generate up
  to one million rows;
- `VirtualSurfaceHierarchyPage` demonstrates hierarchical expansion, collapse,
  selection, and scrolling.

Both pages provide a selector for text, checkbox, date, time, masked text,
autocomplete text, slider value text, editable ComboBox text, and a mixed set of
supported renderers.

## Related articles

- [Visual layout modes](flat-row-cell-layout.md)
- [Column definitions: AOT-friendly bindings](column-definitions-aot.md)
- [Selection model](selection-model-end-to-end.md)
- [Hierarchical model](hierarchical-model-end-to-end.md)
- [Benchmarking layout performance](layout-performance-benchmarking.md)
