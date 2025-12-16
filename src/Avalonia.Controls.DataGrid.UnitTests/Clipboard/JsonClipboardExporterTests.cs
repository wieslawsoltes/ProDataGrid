// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class JsonClipboardExporterTests
{
    [AvaloniaFact]
    public async Task JsonExporter_Writes_Text_And_Custom_Format()
    {
        var rows = ClipboardTestData.BuildRows();
        var customFormat = DataFormat.CreateStringPlatformFormat("application/vnd.avalonia.datagrid.sample+json");
        var exporter = new JsonClipboardExporter(customFormat);

        var data = exporter.BuildClipboardData(
            new DataGridClipboardExportContext(
                new DataGrid(),
                rows,
                DataGridClipboardCopyMode.IncludeHeader,
                DataGridClipboardExportFormat.Text,
                DataGridSelectionUnit.FullRow));

        Assert.NotNull(data);
        Assert.Contains(DataFormat.Text, data!.Formats);
        Assert.Contains(TextClipboardFormatExporter.PlainTextFormat, data.Formats);
        Assert.Contains(customFormat, data.Formats);

        var text = await data.TryGetValueAsync(DataFormat.Text);
        Assert.Equal("\"Name\"\t\"Value\"\r\n\"Alpha\"\t\"1\"\r\n", text);

        var json = await data.TryGetValueAsync(customFormat) as string;
        Assert.NotNull(json);
        Assert.Contains("\"meta\":{\"copyMode\":\"IncludeHeader\"", json);
        Assert.Contains("\"columns\":[\"Name\",\"Value\"]", json);
        Assert.Contains("\"type\":\"data\"", json);
        Assert.Contains("\"column\":\"Name\",\"value\":\"Alpha\"", json);
    }
}
