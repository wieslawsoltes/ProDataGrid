// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace Avalonia.Controls
{
    /// <summary>
    /// Aggregates per-format exporters to build a clipboard payload.
    /// </summary>
    internal sealed class DataGridClipboardExporter : IDataGridClipboardExporter
    {
        private readonly IReadOnlyList<IDataGridClipboardFormatExporter> _exporters;

        public DataGridClipboardExporter(IReadOnlyList<IDataGridClipboardFormatExporter>? exporters = null)
        {
            _exporters = exporters ?? CreateDefaultFormatExporters();
        }

        public static IReadOnlyList<IDataGridClipboardFormatExporter> CreateDefaultFormatExporters()
        {
            return new IDataGridClipboardFormatExporter[]
            {
                new TextClipboardFormatExporter(),
                new CsvClipboardFormatExporter(),
                new HtmlClipboardFormatExporter(),
                new MarkdownClipboardFormatExporter(),
                new XmlClipboardFormatExporter(),
                new YamlClipboardFormatExporter()
            };
        }

        public IAsyncDataTransfer? BuildClipboardData(DataGridClipboardExportContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Rows.Count == 0)
            {
                return null;
            }

            var item = new DataTransferItem();
            var added = false;

            foreach (var exporter in _exporters)
            {
                if (exporter.TryExport(context, item))
                {
                    added = true;
                }
            }

            if (!added)
            {
                return null;
            }

            var transfer = new DataTransfer();
            transfer.Add(item);
            return transfer;
        }

        internal static DataFormat<string> HtmlFormat => HtmlClipboardFormatExporter.HtmlFormat;
        internal static DataFormat<string> HtmlWindowsFormat => HtmlClipboardFormatExporter.HtmlWindowsFormat;
        internal static DataFormat<string> CsvFormat => CsvClipboardFormatExporter.CsvFormat;
        internal static DataFormat<string> CsvWindowsFormat => CsvClipboardFormatExporter.CsvWindowsFormat;
        internal static DataFormat<string> MarkdownFormat => MarkdownClipboardFormatExporter.MarkdownFormat;
        internal static DataFormat<string> XmlFormat => XmlClipboardFormatExporter.XmlFormat;
        internal static DataFormat<string> YamlFormat => YamlClipboardFormatExporter.YamlFormat;
        internal static DataFormat<string> UnicodeTextFormat => TextClipboardFormatExporter.UnicodeTextFormat;
    }
}
