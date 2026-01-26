# Outline Reports

Outline reports provide hierarchical group rows with subtotals, grand totals, and expand/collapse behavior. Use `OutlineReportModel` to build a grouped report and bind it to a `DataGrid` with hierarchical rows enabled.

## Basic setup

```csharp
using Avalonia.Controls.DataGridReporting;
using Avalonia.Controls.DataGridPivoting;

var report = new OutlineReportModel
{
    ItemsSource = sales
};

report.GroupFields.Add(new OutlineGroupField
{
    Header = "Region",
    ValueSelector = item => ((Sale)item!).Region
});

report.GroupFields.Add(new OutlineGroupField
{
    Header = "Category",
    ValueSelector = item => ((Sale)item!).Category
});

report.ValueFields.Add(new OutlineValueField
{
    Header = "Sales",
    ValueSelector = item => ((Sale)item!).Sales,
    AggregateType = PivotAggregateType.Sum
});

report.Layout.ShowDetailRows = true;
report.Layout.ShowSubtotals = true;
report.Layout.ShowGrandTotal = true;
report.Layout.DetailLabelSelector = item => ((Sale)item!).Product;
```

Bind it to a grid:

```xml
<DataGrid ColumnDefinitionsSource="{Binding Report.ColumnDefinitions}"
          HierarchicalModel="{Binding Report.HierarchicalModel}"
          HierarchicalRowsEnabled="True"
          AutoGenerateColumns="False" />
```

When hierarchical rows are enabled and `ItemsSource` is omitted, the grid binds to the model's flattened view.

## Fields and layout

- `OutlineGroupField` controls grouping, sorting, and subtotal visibility per level.
- `OutlineValueField` defines aggregates (Sum, Count, Min, Max, custom) and formatting.
- `OutlineLayoutOptions` controls subtotal/grand total labels, indentation, auto-expand, and detail row labels.

## Expand/collapse

`OutlineReportModel.HierarchicalModel` exposes `ExpandAll` and `CollapseAll` to control group expansion from the view model.

## Sample

Run the sample app and open the "Outline Report" tab for a live report with subtotals and detail rows.
