# Clipboard and Export

ProDataGrid can copy selected rows or cells to the clipboard in multiple formats, with hooks for custom formatting and export pipelines.

## Enable Clipboard Copy

Set `ClipboardCopyMode` and choose a selection unit. Plain text is always included; `ClipboardExportFormat` adds one extra format.

```xml
<DataGrid ClipboardCopyMode="IncludeHeader"
          ClipboardExportFormat="Html"
          SelectionMode="Extended"
          SelectionUnit="CellOrRowHeader" />
```

`ClipboardCopyMode` options:

- `None`: disable copy handling.
- `ExcludeHeader`: copy content only.
- `IncludeHeader`: prepend headers to the clipboard payload.

## Export Formats

`ClipboardExportFormat` controls the optional extra format added alongside plain text:

- `Text` (tab-separated; values are quoted only when they contain tabs, line breaks, or double quotes)
- `Csv`
- `Html`
- `Markdown`
- `Xml`
- `Yaml`
- `Json`

Plain text is always included for compatibility with basic clipboard targets.

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

Override `ClipboardExporter` or supply `ClipboardFormatExporters` to add or replace formats. Built-in format exporters include Text, CSV, HTML, Markdown, XML, YAML, and JSON.

```csharp
using Avalonia.Controls;

ItemsGrid.ClipboardExporter = new JsonClipboardExporter();
```
