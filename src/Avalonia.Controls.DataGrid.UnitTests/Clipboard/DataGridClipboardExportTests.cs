// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Clipboard;

public class DataGridClipboardExportTests
{
    [AvaloniaFact]
    public async Task Copy_Uses_Text_Format_By_Default()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);
            grid.SelectedItems.Add(items[1]);

            Assert.True(InvokeCopy(grid));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            var text = await data!.TryGetTextAsync();
            Assert.Equal("\"Name\"\t\"Value\"\r\n\"Alpha\"\t\"1\"\r\n\"Beta\"\t\"2\"\r\n", text);
            Assert.Contains(DataFormat.Text, data.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task Copy_Includes_Html_When_Requested()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.ClipboardExportFormat = DataGridClipboardExportFormat.Html;
            grid.SelectedItems.Add(items[0]);
            grid.SelectedItems.Add(items[1]);

            Assert.True(InvokeCopy(grid));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            var htmlFormat = DataGridClipboardExporter.HtmlFormat;

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(htmlFormat, data!.Formats);

            var html = await data.TryGetValueAsync(htmlFormat);
            Assert.NotNull(html);
            Assert.Contains("StartHTML:", html);
            Assert.Contains("<!--StartFragment-->", html);
            Assert.Contains("<table>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<th>Name</th>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<td>Alpha</td>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<td>1</td>", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Uses_Format_Argument()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.ClipboardExportFormat = DataGridClipboardExportFormat.Html;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Csv));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.CsvFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);

            var csv = await data.TryGetValueAsync(DataGridClipboardExporter.CsvFormat);
            Assert.Equal("Name,Value\r\nAlpha,1\r\n", csv);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Text_Only()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Text));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.MarkdownFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.XmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.YamlFormat, data.Formats);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Includes_Html_Format()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Html));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.HtmlFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);

            var html = await data.TryGetValueAsync(DataGridClipboardExporter.HtmlFormat);
            Assert.NotNull(html);
            Assert.Contains("<table>", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<td>Alpha</td>", html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Markdown_Only()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Markdown));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.MarkdownFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.XmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.YamlFormat, data.Formats);

            var markdown = await data.TryGetValueAsync(DataGridClipboardExporter.MarkdownFormat);
            Assert.Equal("|Name|Value|\n|---|---|\n|Alpha|1|\n", markdown?.Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Xml_Only()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Xml));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.XmlFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.MarkdownFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.YamlFormat, data.Formats);

            var xml = await data.TryGetValueAsync(DataGridClipboardExporter.XmlFormat);
            Assert.Equal("<rows><header><cell>Name</cell><cell>Value</cell></header><row><cell>Alpha</cell><cell>1</cell></row></rows>", xml);
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Yaml_Only()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Yaml));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.YamlFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.MarkdownFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.XmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.JsonFormat, data.Formats);

            var yaml = await data.TryGetValueAsync(DataGridClipboardExporter.YamlFormat);
            Assert.Equal("rows:\n- cells:\n  - Name\n  - Value\n- cells:\n  - Alpha\n  - 1\n", yaml?.Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task CopySelectionToClipboard_Json_Only()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.SelectedItems.Add(items[0]);

            Assert.True(grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Json));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.Contains(DataFormat.Text, data!.Formats);
            Assert.Contains(DataGridClipboardExporter.PlainTextFormat, data.Formats);
            Assert.Contains(DataGridClipboardExporter.JsonFormat, data!.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.CsvFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.HtmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.MarkdownFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.XmlFormat, data.Formats);
            Assert.DoesNotContain(DataGridClipboardExporter.YamlFormat, data.Formats);

            var json = await data.TryGetValueAsync(DataGridClipboardExporter.JsonFormat);
            Assert.NotNull(json);
            Assert.Equal("[{\"Name\":\"Alpha\",\"Value\":\"1\"}]", json?.Replace("\r\n", "\n", StringComparison.Ordinal));
        }
        finally
        {
            root.Close();
        }
    }

    [AvaloniaFact]
    public async Task Copy_Uses_Custom_Exporter()
    {
        var (grid, root, items) = CreateGrid();
        try
        {
            var exporter = new RecordingExporter();
            grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
            grid.ClipboardExportFormat = DataGridClipboardExportFormat.None;
            grid.ClipboardExporter = exporter;
            grid.SelectedItems.Add(items[0]);

            Assert.True(InvokeCopy(grid));

            var data = await WaitForClipboardAsync(root);
            Assert.NotNull(data);

            Assert.NotNull(exporter.Context);
            Assert.Equal(2, exporter.Context!.Rows.Count); // header + one row

            var markdownFormat = RecordingExporter.MarkdownFormat;
            Assert.Contains(markdownFormat, data!.Formats);
            var markdown = await data.TryGetValueAsync(markdownFormat);
            Assert.Equal("|Name|Value|\n|---|---|\n|Alpha|1|", markdown);
        }
        finally
        {
            root.Close();
        }
    }

    private static (DataGrid Grid, Window Root, List<Item> Items) CreateGrid()
    {
        var items = new List<Item>
        {
            new("Alpha", 1),
            new("Beta", 2)
        };

        var root = new Window
        {
            Width = 400,
            Height = 300,
            Styles =
            {
                new StyleInclude((Uri?)null)
                {
                    Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var grid = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.All,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(Item.Name)) });
        grid.ColumnsInternal.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(Item.Value)) });

        root.Content = grid;
        root.Show();
        grid.Focus();
        grid.UpdateLayout();

        return (grid, root, items);
    }

    private static bool InvokeCopy(DataGrid grid)
    {
        var method = typeof(DataGrid).GetMethod("ProcessCopyKey", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            return false;
        }

        return method.Invoke(grid, new object[] { KeyModifiers.Control }) is true;
    }

    private static async Task<IAsyncDataTransfer?> WaitForClipboardAsync(TopLevel root)
    {
        var clipboard = root.Clipboard;
        if (clipboard == null)
        {
            return null;
        }

        IAsyncDataTransfer? data = null;
        for (var i = 0; i < 10 && data == null; i++)
        {
            data = await clipboard.TryGetDataAsync();
            if (data == null)
            {
                await Task.Delay(10);
            }
        }

        return data ?? await clipboard.TryGetDataAsync();
    }

    private sealed record Item(string Name, int Value);

    private sealed class RecordingExporter : IDataGridClipboardExporter
    {
        public static readonly DataFormat<string> MarkdownFormat = DataFormat.CreateStringPlatformFormat("text/markdown");

        public DataGridClipboardExportContext? Context { get; private set; }

        public IAsyncDataTransfer? BuildClipboardData(DataGridClipboardExportContext context)
        {
            Context = context;

            var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(MarkdownFormat, "|Name|Value|\n|---|---|\n|Alpha|1|");
            transfer.Add(item);
            return transfer;
        }
    }
}
