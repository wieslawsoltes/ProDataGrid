// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls
{
    internal sealed class CsvClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        internal static readonly DataFormat<string> CsvFormat = DataFormat.CreateStringPlatformFormat("text/csv");

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            if (context.Formats != DataGridClipboardExportFormat.Csv)
            {
                return false;
            }

            var csv = DataGridClipboardFormatting.BuildDelimitedText(context.Rows, ',', quoteAlways: false);
            if (string.IsNullOrEmpty(csv))
            {
                return false;
            }

            item.Set(DataFormat.Text, csv);
            item.Set(CsvFormat, csv);
            return true;
        }
    }
}
