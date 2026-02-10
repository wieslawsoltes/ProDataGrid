# Filtering Model: End-to-End Usage

This guide shows a complete `FilteringModel` setup: model wiring, filter flyouts, descriptor updates, and troubleshooting. It is based on the filtering sample in `src/DataGridSample`.

## What this gives you

- One source of truth for filter state (`FilteringModel.Descriptors`).
- Header glyphs and filter flyouts that stay in sync with model state.
- Programmatic filtering (`SetOrUpdate`, `Apply`, `Remove`, `Clear`) without mutating `DataGridCollectionView.Filter` manually.
- A migration path to DynamicData/server-side filtering through a custom adapter.

## End-to-end flow

1. UI (flyout editors, buttons, commands) updates filter context objects.
2. Filter context commands write `FilteringDescriptor` entries to `FilteringModel`.
3. `DataGridFilteringAdapter` composes predicates from descriptors.
4. The adapter updates the view filter (`OwnsViewFilter=true`) or reconciles external filter ownership (`OwnsViewFilter=false`).
5. The grid updates rows and filter glyph state from the same descriptor list.

## 1. ViewModel wiring

Use stable column ids and include `propertyPath` in descriptors. For inline XAML columns, string `ColumnKey` ids are the most robust option.

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Controls.DataGridFiltering;

public sealed class OrdersViewModel
{
    private const string CustomerColumn = "customer";
    private const string StatusColumn = "status";
    private const string OrderedColumn = "ordered";
    private const string TotalColumn = "total";

    public DataGridCollectionView View { get; }
    public FilteringModel FilteringModel { get; } = new() { OwnsViewFilter = true };

    public IFilterTextContext CustomerFilter { get; }
    public IFilterEnumContext StatusFilter { get; }
    public IFilterDateContext OrderedFilter { get; }
    public IFilterNumberContext TotalFilter { get; }
    public ICommand ClearAllCommand { get; }

    public OrdersViewModel()
    {
        View = new DataGridCollectionView(new ObservableCollection<Order>(Seed()));

        CustomerFilter = new TextFilterContext(
            "Customer contains",
            apply: ApplyCustomer,
            clear: () => FilteringModel.Remove(CustomerColumn));

        StatusFilter = new EnumFilterContext(
            "Status (In)",
            new[] { "New", "Processing", "Shipped", "Delivered", "Canceled" },
            apply: ApplyStatus,
            clear: () => FilteringModel.Remove(StatusColumn));

        OrderedFilter = new DateFilterContext(
            "Ordered between",
            apply: ApplyOrderedRange,
            clear: () => FilteringModel.Remove(OrderedColumn));

        TotalFilter = new NumberFilterContext(
            "Total between",
            apply: ApplyTotalRange,
            clear: () => FilteringModel.Remove(TotalColumn));

        ClearAllCommand = new RelayCommand(_ => FilteringModel.Clear());
    }

    private void ApplyCustomer(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FilteringModel.Remove(CustomerColumn);
            return;
        }

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: CustomerColumn,
            @operator: FilteringOperator.Contains,
            propertyPath: nameof(Order.Customer),
            value: text,
            stringComparison: StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyStatus(IReadOnlyList<string> selected)
    {
        if (selected.Count == 0)
        {
            FilteringModel.Remove(StatusColumn);
            return;
        }

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: StatusColumn,
            @operator: FilteringOperator.In,
            propertyPath: nameof(Order.Status),
            values: selected.Cast<object>().ToArray()));
    }

    private void ApplyOrderedRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from == null && to == null)
        {
            FilteringModel.Remove(OrderedColumn);
            return;
        }

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: OrderedColumn,
            @operator: FilteringOperator.Between,
            propertyPath: nameof(Order.Ordered),
            values: new object[] { from ?? DateTimeOffset.MinValue, to ?? DateTimeOffset.MaxValue }));
    }

    private void ApplyTotalRange(double? min, double? max)
    {
        if (min == null && max == null)
        {
            FilteringModel.Remove(TotalColumn);
            return;
        }

        FilteringModel.SetOrUpdate(new FilteringDescriptor(
            columnId: TotalColumn,
            @operator: FilteringOperator.Between,
            propertyPath: nameof(Order.Total),
            values: new object[] { min ?? double.MinValue, max ?? double.MaxValue }));
    }

    private static IEnumerable<Order> Seed()
    {
        return new[]
        {
            new Order("Contoso", "New", DateTimeOffset.UtcNow.AddDays(-1), 450),
            new Order("Fabrikam", "Processing", DateTimeOffset.UtcNow.AddDays(-4), 1320),
            new Order("Tailspin", "Delivered", DateTimeOffset.UtcNow.AddDays(-8), 860)
        };
    }

    public sealed record Order(string Customer, string Status, DateTimeOffset Ordered, double Total);
}
```

The shared filter templates consume simple context interfaces:

- `IFilterTextContext`
- `IFilterNumberContext`
- `IFilterDateContext`
- `IFilterEnumContext`

You can copy the minimal context implementations from `src/DataGridSample/ViewModels/FilteringModelSampleViewModel.cs`.
The sample uses `RelayCommand`; any `ICommand` implementation is valid for the filter context apply/clear commands.

## 2. XAML wiring with built-in filter editors

Attach `FilteringModel` to the grid, assign `ColumnKey` on each filterable column, and reuse built-in filter templates.

```xml
<UserControl.Resources>
  <Flyout x:Key="CustomerFilterFlyout"
          Placement="Bottom"
          FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
          Content="{Binding CustomerFilter}"
          ContentTemplate="{StaticResource DataGridFilterTextEditorTemplate}" />

  <Flyout x:Key="StatusFilterFlyout"
          Placement="Bottom"
          FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
          Content="{Binding StatusFilter}"
          ContentTemplate="{StaticResource DataGridFilterEnumEditorTemplate}" />

  <Flyout x:Key="OrderedFilterFlyout"
          Placement="Bottom"
          FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
          Content="{Binding OrderedFilter}"
          ContentTemplate="{StaticResource DataGridFilterDateEditorTemplate}" />

  <Flyout x:Key="TotalFilterFlyout"
          Placement="Bottom"
          FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
          Content="{Binding TotalFilter}"
          ContentTemplate="{StaticResource DataGridFilterNumberEditorTemplate}" />
</UserControl.Resources>

<DataGrid ItemsSource="{Binding View}"
          FilteringModel="{Binding FilteringModel}"
          AutoGenerateColumns="False">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Customer"
                        ColumnKey="customer"
                        Binding="{Binding Customer}"
                        SortMemberPath="Customer"
                        FilterFlyout="{StaticResource CustomerFilterFlyout}" />

    <DataGridTextColumn Header="Status"
                        ColumnKey="status"
                        Binding="{Binding Status}"
                        SortMemberPath="Status"
                        FilterFlyout="{StaticResource StatusFilterFlyout}" />

    <DataGridTextColumn Header="Ordered"
                        ColumnKey="ordered"
                        Binding="{Binding Ordered, StringFormat='{}{0:MM-dd}'}"
                        SortMemberPath="Ordered"
                        FilterFlyout="{StaticResource OrderedFilterFlyout}" />

    <DataGridTextColumn Header="Total"
                        ColumnKey="total"
                        Binding="{Binding Total, StringFormat='{}{0:C2}'}"
                        SortMemberPath="Total"
                        FilterFlyout="{StaticResource TotalFilterFlyout}" />
  </DataGrid.Columns>
</DataGrid>
```

## 3. Programmatic filtering and batching

Use `DeferRefresh()` when you apply several descriptors at once.

```csharp
using (FilteringModel.DeferRefresh())
{
    FilteringModel.SetOrUpdate(new FilteringDescriptor(
        columnId: "status",
        @operator: FilteringOperator.In,
        propertyPath: nameof(Order.Status),
        values: new object[] { "Processing", "Shipped" }));

    FilteringModel.SetOrUpdate(new FilteringDescriptor(
        columnId: "total",
        @operator: FilteringOperator.GreaterThanOrEqual,
        propertyPath: nameof(Order.Total),
        value: 500d));
}
```

Use `Apply(...)` when you want to replace the full descriptor set in one operation:

```csharp
FilteringModel.Apply(new[]
{
    new FilteringDescriptor("customer", FilteringOperator.Contains, nameof(Order.Customer), value: "Contoso"),
    new FilteringDescriptor("status", FilteringOperator.Equals, nameof(Order.Status), value: "Delivered")
});
```

## 4. Column identity rules (important)

Choose one identity strategy and keep it consistent:

- Inline XAML columns: set `DataGridColumn.ColumnKey` and use the same key as `FilteringDescriptor.ColumnId`.
- Column definitions (`ColumnDefinitionsSource`): use the definition instance or its `ColumnKey`.
- Always include `propertyPath` unless the descriptor is `FilteringOperator.Custom` with a full predicate.

Common failure mode:

- Descriptors are created with `DataGridColumnDefinition` ids but the page uses inline `DataGridColumn`s. The ids do not match materialized columns, so predicates are not applied. Use shared `ColumnKey` values (or matching `propertyPath`) to fix this.

## 5. External filter ownership

If another layer owns `DataGridCollectionView.Filter`, set:

```csharp
FilteringModel.OwnsViewFilter = false;
```

In this mode the filtering adapter reconciles model descriptors with the external filter instead of replacing it as the single owner.

## 6. DynamicData or server-side filtering

Keep the same `FilteringModel` in the grid, but provide a custom `FilteringAdapterFactory` that translates descriptors to your upstream query/predicate pipeline.

```csharp
grid.FilteringModel = viewModel.FilteringModel;
grid.FilteringAdapterFactory = viewModel.FilteringAdapterFactory;
```

Reference implementations:

- `src/DataGridSample/Adapters/DynamicDataFilteringAdapterFactory.cs`
- `src/DataGridSample/Adapters/DynamicDataStreamingFilteringAdapterFactory.cs`

## Full sample references

- `src/DataGridSample/Pages/FilteringModelSamplePage.axaml`
- `src/DataGridSample/ViewModels/FilteringModelSampleViewModel.cs`
- `src/DataGridSample/Pages/ColumnDefinitionsFilteringModelPage.axaml`

## Related articles

- [Data Operations](data-operations.md)
- [Sorting Model: End-to-End Usage](sorting-model-end-to-end.md)
- [Search Model: End-to-End Usage](search-model-end-to-end.md)
- [Column Header Menus and Filters](column-header-menus-and-filters.md)
- [Column Definitions (Model Integration)](column-definitions-models.md)
- [DynamicData Streaming (SourceList)](dynamicdata-streaming-sourcelist.md)
