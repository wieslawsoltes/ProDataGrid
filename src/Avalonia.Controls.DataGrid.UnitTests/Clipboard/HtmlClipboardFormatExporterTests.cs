// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class HtmlClipboardFormatExporterTests
{
    [AvaloniaFact]
    public void HtmlExporter_Writes_Html()
    {
        var rows = ClipboardTestData.BuildRows();
        var item = new DataTransferItem();
        var exporter = new HtmlClipboardFormatExporter();

        var result = exporter.TryExport(
            new DataGridClipboardExportContext(
                new DataGrid(),
                rows,
                DataGridClipboardCopyMode.IncludeHeader,
                DataGridClipboardExportFormat.Html,
                DataGridSelectionUnit.FullRow),
            item);

        Assert.True(result);
        var cfHtml = item.TryGetRaw(HtmlClipboardFormatExporter.HtmlFormat) as string;
        Assert.NotNull(cfHtml);
        Assert.Contains("StartHTML:", cfHtml);
        Assert.Contains("<!--StartFragment-->", cfHtml);
        Assert.Contains("<table>", cfHtml);
        Assert.Contains("<th>Name</th>", cfHtml);
        Assert.Contains("<td>Alpha</td>", cfHtml);

        var plain = item.TryGetRaw(DataFormat.Text) as string;
        Assert.NotNull(plain);
        Assert.DoesNotContain("StartHTML:", plain);
        Assert.DoesNotContain("<!--StartFragment-->", plain);
        Assert.Contains("<html><body><table>", plain);
        Assert.Contains("<th>Name</th>", plain);
        Assert.Contains("<td>Alpha</td>", plain);
    }
}
