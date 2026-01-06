# Fill Handle and Auto Fill

The fill handle lets you extend a rectangular cell selection and auto-fill the new cells. It behaves similarly to Excel: dragging down or right repeats values, and numeric/date/time series are incremented when possible.

## Behavior

- The fill handle appears for **cell** selections (SelectionUnit `Cell` or `CellOrRowHeader`).
- Dragging expands the selection and fills the new cells on release.
- Auto-scroll kicks in when you drag near the grid edges.
- Hidden columns inside the selection suppress the fill handle.

## Fill model

Fill behavior is controlled by `IDataGridFillModel`. The default `DataGridFillModel` handles:

- Copy fill (repeat the source range).
- Series fill for numbers, dates, and times.

You can provide a custom model via the `FillModel` property, `FillModelFactory`, or by overriding `CreateFillModel()`.

## Custom fill model

Example: force copy-only behavior by repeating values instead of building a series.

```csharp
using Avalonia.Controls.DataGridFilling;

public sealed class CopyOnlyFillModel : IDataGridFillModel
{
    public void ApplyFill(DataGridFillContext context)
    {
        var source = context.SourceRange;
        var target = context.TargetRange;
        var rowCount = source.RowCount;
        var colCount = source.ColumnCount;

        if (rowCount <= 0 || colCount <= 0)
            return;

        for (var rowIndex = target.StartRow; rowIndex <= target.EndRow; rowIndex++)
        {
            using var scope = context.BeginRowEdit(rowIndex, out var item);
            if (item == null)
                continue;

            for (var columnIndex = target.StartColumn; columnIndex <= target.EndColumn; columnIndex++)
            {
                if (source.Contains(rowIndex, columnIndex))
                    continue;

                var sourceRow = source.StartRow + Mod(rowIndex - source.StartRow, rowCount);
                var sourceColumn = source.StartColumn + Mod(columnIndex - source.StartColumn, colCount);

                if (context.TryGetCellText(sourceRow, sourceColumn, out var text))
                {
                    context.TrySetCellText(item, columnIndex, text);
                }
            }
        }
    }

    private static int Mod(int value, int modulo)
    {
        if (modulo <= 0)
            return 0;

        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
```

```xml
<DataGrid FillModel="{Binding FillModel}"
          SelectionUnit="Cell"
          SelectionMode="Extended" />
```
