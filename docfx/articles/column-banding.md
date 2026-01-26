# Column Banding and Stacked Headers

Column banding lets you define multi-level headers for non-pivot tables. The `ColumnBandModel` builds column definitions with stacked header segments based on a band tree.

## Basic setup

```csharp
using Avalonia.Controls.DataGridBanding;

var model = new ColumnBandModel();

var salesColumn = new DataGridNumericColumnDefinition
{
    Header = "Sales",
    Binding = DataGridBindingDefinition.Create<Sale, double>(item => item.Sales)
};

model.Bands.Add(new ColumnBand
{
    Header = "Financials",
    Children =
    {
        new ColumnBand { Header = "Sales", ColumnDefinition = salesColumn }
    }
});
```

Bind the generated definitions to the grid:

```xml
<DataGrid ItemsSource="{Binding Items}"
          ColumnDefinitionsSource="{Binding Bands.ColumnDefinitions}"
          AutoGenerateColumns="False" />
```

## Notes

- `ColumnBand.Header` becomes a stacked header segment.
- Leaf nodes supply the `DataGridColumnDefinition` used by the grid.
- The model applies the `DataGridColumnBandHeaderTemplate` by default; override `HeaderTemplateKey` to customize.

## Sample

Run the sample app and open the "Column Banding" tab for a multi-level header layout.
