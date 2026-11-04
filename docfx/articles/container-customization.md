# Container customization

ProDataGrid owns and recycles its row and cell containers. Customize their appearance,
content, behavior, and lifecycle through the supported extension points below.

| Scenario | Supported extension point |
|---|---|
| Change row or cell appearance and visual states | `RowTheme`, `CellTheme`, column `CellTheme`, styles, and control themes |
| Supply arbitrary display or editor content | `DataGridTemplateColumn`, `CellTemplate`, and `CellEditingTemplate` |
| Reuse an expensive templated visual while rows recycle | `DataGridTemplateColumn.ReuseCellContent` or `IRecyclingDataTemplate` |
| Implement a new column and editor contract | Derive from `DataGridColumn` or `DataGridBoundColumn` |
| Attach and clear per-realization integration state | `CellPrepared`, `CellClearing`, `LoadingRow`, and `UnloadingRow` |
| Render a display value without a nested retained display control | supported drawn display mode or `DataGridCustomDrawingColumn` |
| Observe or veto grid state transitions | editing, committed-value, and transactional selection events |
| Replace the outer virtualized containers | `DataGrid.RealizationFactory` |

Prefer themes, templates, and columns when they can express the customization. A
realization factory is the lower-level migration hook for applications that require
`DataGridRow`, `DataGridCell`, or `DataGridColumnHeader` subclasses.

## Container ownership

`DataGridRealizationFactory` replaces only the outer containers. The grid still owns
their lifetime, preparation, layout, selection, editing, validation, automation, and
recycling. Columns still own display content, editors, values, sorting, filtering,
search, grouping, clipboard/export data, and persistence semantics. Never derive a
semantic value from a realized cell: virtualization means most cells do not exist.

The request contexts expose:

- the owning grid;
- the row data context and normalized application item;
- the `HierarchicalNode`, flattened row index, and slot when applicable;
- the owning row and column for cells; and
- left/right frozen-region state for column headers.

Call `CreateDefaultContainer()` from a context when a request does not need a custom
container. The grid performs the normal preparation after the factory returns it.

## Recycling keys

Each container kind has a data-side key overload and an element-side key overload.
Return equal, stable keys only when the existing element is compatible with the new
request. The grid keeps row pools partitioned by key, rebuilds incompatible cells on
a recycled row, and replaces an incompatible cached header. Return `null` on either
side to disable reuse for that request or element.

The shared `DataGridRealizationFactory.Default` keeps the legacy fast path. Assigning
a different factory clears old pools and regenerates realized rows, cells, and headers,
so containers from different factories cannot mix. Replacing a factory also ends an
active edit through the grid's normal commit-or-cancel path.

## Example

The following factory alternates row and cell subclasses by application item. The
subclasses reuse the built-in themes through `StyleKeyOverride`; an application can
instead provide dedicated control themes for its custom types.

```csharp
public sealed record OrderRow(int Id);

public sealed class EvenOrderRow : DataGridRow
{
    protected override Type StyleKeyOverride => typeof(DataGridRow);
}

public sealed class OddOrderRow : DataGridRow
{
    protected override Type StyleKeyOverride => typeof(DataGridRow);
}

public sealed class EvenOrderCell : DataGridCell
{
    protected override Type StyleKeyOverride => typeof(DataGridCell);
}

public sealed class OddOrderCell : DataGridCell
{
    protected override Type StyleKeyOverride => typeof(DataGridCell);
}

public sealed class OrderRealizationFactory : DataGridRealizationFactory
{
    public override DataGridRow CreateRow(DataGridRowRealizationContext context) =>
        IsEven(context.Item) ? new EvenOrderRow() : new OddOrderRow();

    public override object? GetRowRecyclingKey(DataGridRowRealizationContext context) =>
        IsEven(context.Item) ? typeof(EvenOrderRow) : typeof(OddOrderRow);

    public override object? GetRowRecyclingKey(DataGridRow row) => row.GetType();

    public override DataGridCell CreateCell(DataGridCellRealizationContext context) =>
        IsEven(context.Item) ? new EvenOrderCell() : new OddOrderCell();

    public override object? GetCellRecyclingKey(DataGridCellRealizationContext context) =>
        IsEven(context.Item) ? typeof(EvenOrderCell) : typeof(OddOrderCell);

    public override object? GetCellRecyclingKey(DataGridCell cell) => cell.GetType();

    private static bool IsEven(object? item) => item is OrderRow row && (row.Id & 1) == 0;
}
```

Register the factory as a resource rather than constructing it in view code-behind:

```xml
<Window.Resources>
  <local:OrderRealizationFactory x:Key="OrderRealizationFactory" />
</Window.Resources>

<DataGrid RealizationFactory="{StaticResource OrderRealizationFactory}" />
```

Items that are already `DataGridRow` containers keep the existing own-container
behavior and do not require a factory-created row.

## Compatibility

No existing customization API changes. `DataGridRealizationFactory.Default`, existing
themes and templates, and column-selected direct/drawn display paths remain the
compatibility default.
