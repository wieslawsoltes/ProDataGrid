// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class TextClipboardFormatExporterTests
{
    [AvaloniaFact]
    public void TextExporter_Writes_Text()
    {
        var rows = ClipboardTestData.BuildRows();
        var item = new DataTransferItem();
        var exporter = new TextClipboardFormatExporter();

        var result = exporter.TryExport(
            new DataGridClipboardExportContext(
                new DataGrid(),
                rows,
                DataGridClipboardCopyMode.IncludeHeader,
                DataGridClipboardExportFormat.Text,
                DataGridSelectionUnit.FullRow),
            item);

        Assert.True(result);
        Assert.Equal("\"Name\"\t\"Value\"\r\n\"Alpha\"\t\"1\"\r\n", item.TryGetRaw(DataFormat.Text));
        Assert.Equal("\"Name\"\t\"Value\"\r\n\"Alpha\"\t\"1\"\r\n", item.TryGetRaw(TextClipboardFormatExporter.PlainTextFormat));
    }
}
