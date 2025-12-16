// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Input;

namespace Avalonia.Controls
{
    /// <summary>
    /// Example exporter that adds a JSON payload alongside the standard tab-delimited text.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#endif
    sealed class JsonClipboardExporter : IDataGridClipboardExporter
    {
        /// <summary>
        /// Gets the default JSON data format (<c>application/json</c>).
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#endif
        static DataFormat<string> DefaultJsonFormat { get; } = DataFormat.CreateStringPlatformFormat("application/json");

        /// <summary>
        /// Gets the data format used for the JSON payload.
        /// </summary>
#if !DATAGRID_INTERNAL
        public
#endif
        DataFormat<string> JsonFormat { get; }

        public JsonClipboardExporter(DataFormat<string>? jsonFormat = null)
        {
            JsonFormat = jsonFormat ?? DefaultJsonFormat;
        }

        public IAsyncDataTransfer? BuildClipboardData(DataGridClipboardExportContext context)
        {
            var baseExporter = new DataGridClipboardExporter(context.Grid.ClipboardFormatExporters);
            var baseData = baseExporter.BuildClipboardData(context);

            if (baseData is null)
            {
                return null;
            }

            if (baseData is not DataTransfer transfer)
            {
                return baseData;
            }

            var json = BuildDetailedJson(context);
            if (!string.IsNullOrEmpty(json))
            {
                var jsonItem = new DataTransferItem();
                jsonItem.Set(JsonFormat, json);
                transfer.Add(jsonItem);
            }

            return transfer;
        }

        internal static string BuildDetailedJson(DataGridClipboardExportContext context)
        {
            var rows = context.Rows;
            var columns = BuildColumns(rows);

            var builder = new StringBuilder();
            builder.Append("{\"meta\":{");
            builder.Append("\"copyMode\":\"").Append(context.CopyMode).Append("\",");
            builder.Append("\"selectionUnit\":\"").Append(context.SelectionUnit).Append("\",");
            builder.Append("\"formats\":\"").Append(context.Formats).Append("\"");
            builder.Append("},");

            builder.Append("\"columns\":[");
            for (var c = 0; c < columns.Count; c++)
            {
                builder.Append('"').Append(EscapeJson(columns[c])).Append('"');
                if (c < columns.Count - 1)
                {
                    builder.Append(',');
                }
            }
            builder.Append("],");

            builder.Append("\"rows\":[");

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                builder.Append("{\"type\":\"").Append(row.IsColumnHeadersRow ? "header" : "data").Append("\",");
                builder.Append("\"isHeader\":").Append(row.IsColumnHeadersRow.ToString().ToLowerInvariant()).Append(",\"cells\":[");

                for (var c = 0; c < row.ClipboardRowContent.Count; c++)
                {
                    var cell = row.ClipboardRowContent[c];
                    var value = cell.Content?.ToString() ?? string.Empty;
                    var columnName = columns.Count > c ? columns[c] : $"Column{c + 1}";
                    builder.Append("{\"column\":\"").Append(EscapeJson(columnName)).Append("\",");
                    builder.Append("\"value\":\"").Append(EscapeJson(value)).Append("\"}");

                    if (c < row.ClipboardRowContent.Count - 1)
                    {
                        builder.Append(',');
                    }
                }

                builder.Append("]}");

                if (i < rows.Count - 1)
                {
                    builder.Append(',');
                }
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static List<string> BuildColumns(IReadOnlyList<DataGridRowClipboardEventArgs> rows)
        {
            var columns = new List<string>();

            if (rows.Count > 0 && rows[0].IsColumnHeadersRow)
            {
                foreach (var cell in rows[0].ClipboardRowContent)
                {
                    columns.Add(cell.Content?.ToString() ?? string.Empty);
                }
            }

            return columns;
        }

        private static string EscapeJson(string value)
        {
            return DataGridClipboardFormatting.EscapeJson(value);
        }
    }
}
