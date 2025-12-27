# Clipboard and Export

ProDataGrid can copy selected rows or cells to the clipboard in multiple formats, with hooks for custom formatting.

## Enable Clipboard Copy

Set `ClipboardCopyMode` and choose a selection unit. Plain text is always included; `ClipboardExportFormat` adds one extra format.

```xml
<DataGrid ClipboardCopyMode="IncludeHeader"
          ClipboardExportFormat="Html"
          SelectionMode="Extended"
          SelectionUnit="CellOrRowHeader" />
```

## Copy On Demand

Call `CopySelectionToClipboard` to use the current format or override it.

```csharp
using Avalonia.Controls;

ItemsGrid.CopySelectionToClipboard();
ItemsGrid.CopySelectionToClipboard(DataGridClipboardExportFormat.Csv);
```

## Customize Clipboard Content

Use `ClipboardContentBinding` to override what a column contributes, or handle `CopyingRowClipboardContent` to rewrite cells before export.

```csharp
using System.Globalization;
using Avalonia.Controls;

ItemsGrid.CopyingRowClipboardContent += (_, e) =>
{
    if (e.IsColumnHeadersRow)
    {
        return;
    }

    for (var i = 0; i < e.ClipboardRowContent.Count; i++)
    {
        var cell = e.ClipboardRowContent[i];
        if (cell.Column?.Header?.ToString() == "Price")
        {
            var formatted = string.Format(CultureInfo.InvariantCulture, "{0:C}", cell.Content);
            e.ClipboardRowContent[i] = new DataGridClipboardCellContent(cell.Item, cell.Column, formatted);
        }
    }
};
```

## Custom Exporters

Override `ClipboardExporter` or supply `ClipboardFormatExporters` to add or replace formats.

```csharp
using Avalonia.Controls;

ItemsGrid.ClipboardExporter = new JsonClipboardExporter();
```
