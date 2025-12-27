# Styling and Themes

ProDataGrid ships updated templates and styling hooks so you can customize rows, cells, and headers while keeping logical scrolling and frozen columns aligned.

## v2 Templates

```xml
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
```

## Generic and Legacy Themes

ProDataGrid also ships a shared `Generic.xaml` with core control themes and legacy theme wrappers:

- `Themes/Generic.xaml` is the base theme for custom styling and non-Fluent/Simple environments.
- `Themes/Fluent.xaml` and `Themes/Simple.xaml` are the legacy (non-v2) templates if you need to stay on the classic ScrollBar layout.

```xml
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Generic.xaml" />
```

## Row and Cell Styling

Use styles that target `DataGridRow` and `DataGridCell` to control selection visuals, alternating rows, and focus cues. Useful pseudo-classes include `:selected`, `:current`, `:editing`, `:invalid`, `:searchmatch`, and `:searchcurrent`.

```xml
<Style Selector="DataGridRow:current">
  <Setter Property="Background" Value="#FFE3F2FD" />
  <Setter Property="BorderBrush" Value="#FF64B5F6" />
  <Setter Property="BorderThickness" Value="0,0,0,2" />
</Style>
```

## Column Themes

Column-specific styles let you highlight key columns or adjust alignment, formatting, and glyphs. Derived columns can reuse built-in cell themes through protected accessors and `ControlTheme` resources keyed in your app:

- `CellCheckBoxTheme`
- `CellComboBoxTheme` / `CellComboBoxDisplayTheme`
- `CellHyperlinkButtonTheme`
- `CellTextBoxTheme` / `CellTextBlockTheme`

The sample `Column Themes` page shows custom columns applying these themes to generated elements.

```xml
<ControlTheme x:Key="DataGridCellCheckBoxTheme"
              TargetType="CheckBox"
              BasedOn="{StaticResource {x:Type CheckBox}}" />
```

## Current Row and Current Cell

The current row and cell are exposed through `:current` so you can style focus and selection states independently.
