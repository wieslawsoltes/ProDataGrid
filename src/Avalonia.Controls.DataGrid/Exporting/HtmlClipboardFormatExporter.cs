// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls
{
    internal sealed class HtmlClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        internal static readonly DataFormat<string> HtmlFormat = DataFormat.CreateStringPlatformFormat("text/html");

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            if (context.Formats != DataGridClipboardExportFormat.Html)
            {
                return false;
            }

            if (!DataGridClipboardFormatting.TryBuildHtmlPayloads(
                    context.Rows,
                    out var html,
                    out var cfHtml))
            {
                return false;
            }

            item.Set(DataFormat.Text, html);
            item.Set(HtmlFormat, cfHtml);
            return true;
        }
    }
}
