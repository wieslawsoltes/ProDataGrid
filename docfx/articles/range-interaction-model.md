# Range Interaction Model

The range interaction model controls drag selection thresholds, range anchors, fill handle targeting, and auto-scroll behavior. It centralizes the logic that makes selections feel Excel-like.

## Quick Start

Use the default model (already assigned) or provide your own implementation:

```csharp
dataGrid.RangeInteractionModel = new DataGridRangeInteractionModel();
```

## Custom model

Override the default behavior by subclassing `DataGridRangeInteractionModel`:

```csharp
using System;
using Avalonia.Controls;
using Avalonia.Controls.DataGridInteractions;

public sealed class TopLeftRangeInteractionModel : DataGridRangeInteractionModel
{
    protected override double DragSelectionThreshold => 8;

    public override DataGridCellRange BuildFillHandleRange(DataGridFillHandleRangeContext context)
    {
        var source = context.SourceRange;
        var target = context.TargetCell;

        var startRow = Math.Min(source.StartRow, target.RowIndex);
        var endRow = Math.Max(source.StartRow, target.RowIndex);
        var startColumn = Math.Min(source.StartColumn, target.ColumnIndex);
        var endColumn = Math.Max(source.StartColumn, target.ColumnIndex);

        return new DataGridCellRange(startRow, endRow, startColumn, endColumn);
    }
}
```

Assign the model:

```csharp
dataGrid.RangeInteractionModel = new TopLeftRangeInteractionModel();
```

## Factories and overrides

- Use `RangeInteractionModelFactory` to supply per-grid instances.
- Override `CreateRangeInteractionModel()` for subclassed grids.

## Samples

See the sample gallery for the range interaction model page.
