# Summaries and Aggregation

ProDataGrid supports column summaries for totals and aggregates. Summaries can appear in total rows, group headers/footers, and hierarchical views.

## Total and Group Summaries

Enable total and group summaries on the grid and attach summary descriptions to columns:

```xml
<DataGrid ShowTotalSummary="True"
          ShowGroupSummary="True"
          TotalSummaryPosition="Bottom"
          GroupSummaryPosition="Footer">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}">
      <DataGridTextColumn.Summaries>
        <DataGridAggregateSummaryDescription Aggregate="Sum"
                                             Scope="Both"
                                             Title="Sum: "
                                             StringFormat="N0" />
      </DataGridTextColumn.Summaries>
    </DataGridTextColumn>
  </DataGrid.Columns>
</DataGrid>
```

Built-in aggregates include `Sum`, `Average`, `Count`, `CountDistinct`, `Min`, `Max`, `First`, and `Last`.

`GroupSummaryPosition` supports `Header`, `Footer`, or `Both`. `TotalSummaryPosition` can be `Top` or `Bottom`.

## Summary Description Options

Each `DataGridSummaryDescription` supports:

- `Scope`: `Total`, `Group`, or `Both`.
- `Title` and `StringFormat` for labeling/formatting.
- `Converter`/`ConverterParameter` for custom formatting.
- `ContentTemplate` for fully custom visuals.

## Custom Calculators

Create custom summary calculators by implementing `IDataGridSummaryCalculator` and attach them with `DataGridCustomSummaryDescription`:

```csharp
column.Summaries.Add(new DataGridCustomSummaryDescription
{
    Calculator = new StandardDeviationCalculator(),
    Title = "StdDev: ",
    StringFormat = "N2"
});
```

## Summary Row Styling and Timing

- `SummaryRowTheme` lets you theme total and group summary rows.
- `SummaryRecalculationDelayMs` throttles recalculation during rapid updates.
- `SummaryRecalculated` fires after recalculation completes.

## Summary Cell Alignment and Themes

Summary cells use `DataGridSummaryCell` and are left-aligned by default. Numeric columns now align summary values to the right by default to match numeric cell content.

- `DataGrid.SummaryCellTheme` provides a grid-wide default theme for summary cells.
- `DataGridColumn.SummaryCellTheme` overrides the grid theme for a specific column.
- `DataGrid.SummaryCellHorizontalContentAlignment` / `DataGrid.SummaryCellVerticalContentAlignment` set grid-wide defaults.
- `DataGridColumn.SummaryCellHorizontalContentAlignment` / `DataGridColumn.SummaryCellVerticalContentAlignment` override per column.

### Alignment precedence

1. Column alignment (when explicitly set on the column).
2. Grid alignment.
3. Column default (e.g., numeric columns default to right alignment).
4. Control theme default (left alignment in the default theme).

```xml
<DataGrid ShowTotalSummary="True"
          SummaryCellHorizontalContentAlignment="Right">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Name"
                        Binding="{Binding Name}"
                        SummaryCellHorizontalContentAlignment="Left" />
    <DataGridNumericColumn Header="Amount"
                           Binding="{Binding Amount}"
                           FormatString="C2" />
  </DataGrid.Columns>
</DataGrid>
```

### Customize summary cells via styles

`DataGridSummaryCell` exposes pseudo-classes for common aggregates, so you can style cells by summary type:

- `:sum`, `:average`, `:count`, `:min`, `:max`, `:custom`, `:none`

```xml
<Style Selector="DataGridSummaryCell:sum">
  <Setter Property="Foreground" Value="#D97706" />
</Style>
<Style Selector="DataGridSummaryCell:average">
  <Setter Property="Foreground" Value="#2563EB" />
</Style>
```

If you want a different layout or templating, set a theme on the grid or column:

```xml
<DataGrid SummaryCellTheme="{StaticResource CompactSummaryCellTheme}" />
```

### Customize summary cells with column definitions

When you use `ColumnDefinitionsSource`, you can configure summary cell appearance on the definitions:

```xml
<DataGrid ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          ShowTotalSummary="True"
          SummaryCellHorizontalContentAlignment="Right" />
```

```csharp
ColumnDefinitions = new ObservableCollection<DataGridColumnDefinition>
{
    new DataGridNumericColumnDefinition
    {
        Header = "Amount",
        Binding = ColumnDefinitionBindingFactory.CreateBinding<Order, decimal>(
            nameof(Order.Amount),
            o => o.Amount,
            (o, v) => o.Amount = v),
        SummaryCellHorizontalContentAlignment = HorizontalAlignment.Right,
        SummaryCellThemeKey = "AmountSummaryCellTheme"
    }
};
```

You can also set summary alignment per column definition without touching the grid defaults.

## Notes

- Summary values update as the view filters or expands hierarchical nodes.
- Group summaries honor the same descriptors and scopes as total summaries.
