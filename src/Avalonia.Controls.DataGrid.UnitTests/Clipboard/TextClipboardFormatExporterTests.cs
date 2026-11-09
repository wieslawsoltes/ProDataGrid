// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class TextClipboardFormatExporterTests
{
    [AvaloniaTheory]
    [InlineData("BTC-USD", "BTC-USD\r\n")]
    [InlineData("a\tb", "\"a\tb\"\r\n")]
    [InlineData("a\nb", "\"a\nb\"\r\n")]
    [InlineData("a\rb", "\"a\rb\"\r\n")]
    [InlineData("a\"b", "\"a\"\"b\"\r\n")]
    [InlineData("", "\r\n")]
    [InlineData(null, "\r\n")]
    public void TextExporter_Quotes_Only_Delimited_Content(string? value, string expected)
    {
        var row = new DataGridRowClipboardEventArgs(null, false);
        row.ClipboardRowContent.Add(new DataGridClipboardCellContent(null, new DataGridTextColumn(), value));
        var context = new DataGridClipboardExportContext(new DataGrid(), new[] { row },
            DataGridClipboardCopyMode.ExcludeHeader, DataGridClipboardExportFormat.Text, DataGridSelectionUnit.Cell);
        var item = new DataTransferItem();
        Assert.True(new TextClipboardFormatExporter().TryExport(context, item));
        Assert.Equal(expected, item.TryGetRaw(DataFormat.Text));
        Assert.Equal(expected, item.TryGetRaw(TextClipboardFormatExporter.PlainTextFormat));
    }

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
        Assert.Equal("Name\tValue\r\nAlpha\t1\r\n", item.TryGetRaw(DataFormat.Text));
        Assert.Equal("Name\tValue\r\nAlpha\t1\r\n", item.TryGetRaw(TextClipboardFormatExporter.PlainTextFormat));
    }
}
