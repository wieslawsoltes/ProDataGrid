# Clipboard Import Model

The clipboard import model controls how pasted text is parsed and applied to grid cells. It is used whenever the grid handles paste operations.

## Quick Start

Assign the default model (already configured by the grid) or replace it:

```csharp
dataGrid.ClipboardImportModel = new DataGridClipboardImportModel();
```

## Custom model

Override parsing or paste logic to transform incoming data:

```csharp
using System.Collections.Generic;
using Avalonia.Controls.DataGridClipboard;

public sealed class UppercaseClipboardImportModel : DataGridClipboardImportModel
{
    protected override List<List<string>> ParseClipboardText(string text)
    {
        var rows = base.ParseClipboardText(text);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (var colIndex = 0; colIndex < row.Count; colIndex++)
            {
                row[colIndex] = row[colIndex]?.ToUpperInvariant() ?? string.Empty;
            }
        }

        return rows;
    }
}
```

Assign the model:

```csharp
dataGrid.ClipboardImportModel = new UppercaseClipboardImportModel();
```

## Factories and overrides

- Use `ClipboardImportModelFactory` to create instances per grid.
- Override `CreateClipboardImportModel()` for custom grid subclasses.

## Samples

See the sample gallery for the clipboard import model page.
