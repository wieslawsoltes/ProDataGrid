// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls
{
    internal sealed class JsonClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        internal static readonly DataFormat<string> JsonFormat = DataFormat.CreateStringPlatformFormat("application/json");

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            if (context.Formats != DataGridClipboardExportFormat.Json)
            {
                return false;
            }

            var json = DataGridClipboardFormatting.BuildJson(context.Rows);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            item.Set(DataFormat.Text, json);
            item.Set(JsonFormat, json);
            return true;
        }
    }
}
