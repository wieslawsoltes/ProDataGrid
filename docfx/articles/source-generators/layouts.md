# Generated layouts

`GenerateDataGridView` and `GenerateDataGridViewsForNamespace` can configure any built-in layout or bind a custom `IDataGridLayoutModel` property. Generated code is reflection-free and automatically enables `UseLogicalScrollable` when a layout is present.

## Built-in layout

```csharp
[GenerateDataGridViewModel(typeof(OrderRow))]
[GenerateDataGridView(
    typeof(OrderRow),
    ViewName = "OrderTilesPage",
    Layout = DataGridGeneratedLayout.UniformGrid,
    LayoutMinItemWidth = 260,
    LayoutMinItemHeight = 76,
    LayoutHorizontalSpacing = 8,
    LayoutVerticalSpacing = 8,
    LayoutMaximumRowsOrColumns = 4,
    LayoutItemsJustification = DataGridUniformGridItemsJustification.SpaceBetween,
    LayoutItemsStretch = DataGridUniformGridItemsStretch.Fill)]
public sealed partial class OrdersViewModel : ReactiveObject
{
    public IReadOnlyList<OrderRow> Items { get; } = LoadOrders();
}
```

The generator emits a concrete model initializer directly on the generated DataGrid. No service lookup, reflection, or runtime enum conversion is used.

## Layout enum

`DataGridGeneratedLayout` has these values:

| Value | Emitted model |
| --- | --- |
| `None` | No assignment; classic layout unless a custom binding is configured. |
| `Stack` | `DataGridStackLayoutModel` |
| `NonVirtualizingStack` | `DataGridNonVirtualizingStackLayoutModel` |
| `UniformGrid` | `DataGridUniformGridLayoutModel` |
| `Wrap` | `DataGridWrapLayoutModel` |

`DataGridGeneratedLayoutOrientation` is `Default`, `Horizontal`, or `Vertical`. `Default` leaves the concrete model default unchanged.

## Options

| Attribute option | Applies to | Default |
| --- | --- | --- |
| `Layout` | Built-ins | `None` |
| `LayoutOrientation` | All built-ins | `Default` |
| `LayoutSpacing` | Stack models | `0` |
| `LayoutDisableVirtualization` | Virtualizing stack | `false` |
| `LayoutHorizontalSpacing` | Uniform/wrap | `0` |
| `LayoutVerticalSpacing` | Uniform/wrap | `0` |
| `LayoutMinItemWidth` | Uniform grid | `NaN` |
| `LayoutMinItemHeight` | Uniform grid | `NaN` |
| `LayoutMaximumRowsOrColumns` | Uniform grid | `int.MaxValue` |
| `LayoutItemsJustification` | Uniform grid | `Start` |
| `LayoutItemsStretch` | Uniform grid | `None` |
| `LayoutMaximumCachedLines` | Wrap | `256` |

For uniform grid, horizontal spacing maps to `MinColumnSpacing` and vertical spacing maps to `MinRowSpacing`.

## Stack examples

```csharp
[GenerateDataGridView(
    typeof(LogRow),
    ViewName = "HorizontalLogPage",
    Layout = DataGridGeneratedLayout.Stack,
    LayoutOrientation = DataGridGeneratedLayoutOrientation.Horizontal,
    LayoutSpacing = 6)]
```

```csharp
[GenerateDataGridView(
    typeof(PrintRow),
    ViewName = "PrintRowsPage",
    Layout = DataGridGeneratedLayout.NonVirtualizingStack,
    LayoutSpacing = 2)]
```

## Wrap example

```csharp
[GenerateDataGridView(
    typeof(CardRow),
    ViewName = "CardsPage",
    Layout = DataGridGeneratedLayout.Wrap,
    LayoutHorizontalSpacing = 10,
    LayoutVerticalSpacing = 8,
    LayoutMaximumCachedLines = 128)]
```

## Bind a custom or runtime-switchable model

Use `LayoutModelPropertyName` instead of `Layout`:

```csharp
[GenerateDataGridViewModel(typeof(OrderRow))]
[GenerateDataGridView(
    typeof(OrderRow),
    ViewName = "OrdersPage",
    LayoutModelPropertyName = nameof(LayoutModel))]
public sealed partial class OrdersViewModel : ReactiveObject
{
    public IReadOnlyList<OrderRow> Items { get; } = LoadOrders();

    public IDataGridLayoutModel LayoutModel { get; } =
        new MyApplicationLayoutModel();
}
```

The generator validates that the named member is an accessible readable `IDataGridLayoutModel`. It emits an `IPropertyInfo` accessor and a compiled one-way binding to `DataGrid.LayoutModelProperty`.

`Layout` and `LayoutModelPropertyName` are mutually exclusive. Conflicting built-in/custom configuration is reported at compile time. An inaccessible, missing, write-only, or wrong-typed custom property is also rejected.

## Namespace policy

The same options exist on `GenerateDataGridViewsForNamespaceAttribute`, so an assembly can select a default layout for a model namespace. Type-level `GenerateDataGridView` declarations remain the place for view-specific overrides and distinct generated view names.

See [Model-based layouts](../model-based-layouts.md) for runtime behavior and [Custom layouts](../custom-layouts.md) for extension contracts.
