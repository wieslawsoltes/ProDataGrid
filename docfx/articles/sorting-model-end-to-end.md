# Sorting Model: End-to-End Usage

This guide shows complete `SortingModel` wiring: descriptor ownership, header gestures, programmatic presets, and adapter handoff for upstream sorting pipelines.

## What this gives you

- A single sort state source (`SortingModel.Descriptors`).
- Header-click sorting and programmatic sorting through the same model.
- Optional guard rails via `SortingChanging` (cancel/normalize sort changes).
- Clear ownership control between model and `DataGridCollectionView.SortDescriptions`.

## End-to-end flow

1. Header clicks or commands create/update sort descriptors.
2. `SortingModel` raises `SortingChanging` and then `SortingChanged`.
3. The sorting adapter applies descriptors to the view sort pipeline.
4. Grid sort glyphs and `SortDescriptions` remain synchronized.

## 1. ViewModel wiring

Use stable `columnId` values (`ColumnKey` or definition instance) and include `propertyPath` when sorting by member path.

```csharp
using System;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls.DataGridSorting;

public sealed class DeploymentsViewModel
{
    public DataGridCollectionView View { get; }

    public ISortingModel SortingModel { get; } = new SortingModel
    {
        MultiSort = true,
        CycleMode = SortCycleMode.AscendingDescendingNone,
        OwnsViewSorts = true
    };

    public bool PinStatusPrimary { get; set; } = true;

    public DeploymentsViewModel()
    {
        View = new DataGridCollectionView(Deployment.CreateSeed());
        SortingModel.SortingChanging += OnSortingChanging;
    }

    public void ApplyStatusPreset()
    {
        SortingModel.Apply(new[]
        {
            new SortingDescriptor("status", ListSortDirection.Ascending, nameof(Deployment.Status)),
            new SortingDescriptor("ring", ListSortDirection.Ascending, nameof(Deployment.Ring)),
            new SortingDescriptor("started", ListSortDirection.Descending, nameof(Deployment.Started))
        });
    }

    public void AddServiceTiebreaker()
    {
        SortingModel.SetOrUpdate(new SortingDescriptor(
            columnId: "service",
            direction: ListSortDirection.Ascending,
            propertyPath: nameof(Deployment.Service)));
    }

    public void ClearSorts() => SortingModel.Clear();

    private void OnSortingChanging(object? sender, SortingChangingEventArgs e)
    {
        if (!PinStatusPrimary)
        {
            return;
        }

        if (e.NewDescriptors.Count == 0 ||
            !string.Equals(e.NewDescriptors[0].PropertyPath, nameof(Deployment.Status), StringComparison.Ordinal))
        {
            e.Cancel = true;
        }
    }
}
```

## 2. XAML wiring

Bind `SortingModel` and use matching `ColumnKey` values in columns:

```xml
<DataGrid ItemsSource="{Binding View}"
          SortingModel="{Binding SortingModel}"
          AutoGenerateColumns="False"
          CanUserSortColumns="True"
          IsMultiSortEnabled="True"
          SortCycleMode="AscendingDescendingNone">
  <DataGrid.Columns>
    <DataGridTextColumn Header="Service"
                        ColumnKey="service"
                        Binding="{Binding Service}"
                        SortMemberPath="Service" />
    <DataGridTextColumn Header="Status"
                        ColumnKey="status"
                        Binding="{Binding Status}"
                        SortMemberPath="Status" />
    <DataGridTextColumn Header="Ring"
                        ColumnKey="ring"
                        Binding="{Binding Ring}"
                        SortMemberPath="Ring" />
    <DataGridTextColumn Header="Started"
                        ColumnKey="started"
                        Binding="{Binding Started}"
                        SortMemberPath="Started" />
  </DataGrid.Columns>
</DataGrid>
```

## 3. Batching and ownership

Batch descriptor updates:

```csharp
using (SortingModel.DeferRefresh())
{
    SortingModel.SetOrUpdate(new SortingDescriptor("status", ListSortDirection.Ascending, nameof(Deployment.Status)));
    SortingModel.SetOrUpdate(new SortingDescriptor("started", ListSortDirection.Descending, nameof(Deployment.Started)));
}
```

If an external pipeline owns `SortDescriptions`, switch the model to observe mode:

```csharp
SortingModel.OwnsViewSorts = false;
```

In observe mode, model and view stay synchronized, but external mutations are not overwritten by model ownership.

## 4. Custom comparer sorting

Use comparer-backed descriptors for domain ordering:

```csharp
SortingModel.SetOrUpdate(new SortingDescriptor(
    columnId: "status",
    direction: ListSortDirection.Ascending,
    propertyPath: nameof(Deployment.Status),
    comparer: StringComparer.OrdinalIgnoreCase));
```

For header clicks to use a custom comparer, set `CustomSortComparer` on the corresponding `DataGridColumn`.

## 5. DynamicData/server-side sorting

Use a custom `SortingAdapterFactory` to translate model descriptors upstream:

```csharp
grid.SortingModel = viewModel.SortingModel;
grid.SortingAdapterFactory = viewModel.SortingAdapterFactory;
```

Reference implementations:

- `src/DataGridSample/Adapters/DynamicDataSortingAdapterFactory.cs`
- `src/DataGridSample/Adapters/DynamicDataStreamingSortingAdapterFactory.cs`

## Troubleshooting

- Sorting appears ignored:
  Make sure `SortMemberPath` (or descriptor `propertyPath`) resolves to a real member, or provide a comparer.
- Wrong column sorted:
  Ensure `SortingDescriptor.ColumnId` matches `DataGridColumn.ColumnKey` (or the materialized column/definition id).
- Duplicate/unstable sort order:
  Normalize descriptors in `SortingChanging`, or apply full presets with `Apply(...)` instead of ad-hoc updates.

## Full sample references

- `src/DataGridSample/Pages/SortingModelPlaygroundPage.axaml`
- `src/DataGridSample/ViewModels/SortingModelPlaygroundViewModel.cs`
- `src/DataGridSample/Pages/SortingModelPage.axaml`

## Related articles

- [Data Operations](data-operations.md)
- [Column Definitions (Model Integration)](column-definitions-models.md)
- [Filtering Model: End-to-End Usage](filtering-model-end-to-end.md)
- [Search Model: End-to-End Usage](search-model-end-to-end.md)
