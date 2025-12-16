// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls
{
    internal sealed class CsvClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        internal static readonly DataFormat<string> CsvFormat = DataFormat.CreateStringPlatformFormat("text/csv");
        internal static readonly DataFormat<string> CsvWindowsFormat = DataFormat.CreateStringPlatformFormat("Csv");

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            if (!context.Formats.HasFlag(DataGridClipboardExportFormat.Csv))
            {
                return false;
            }

            var csv = DataGridClipboardFormatting.BuildDelimitedText(context.Rows, ',', quoteAlways: false);
            if (string.IsNullOrEmpty(csv))
            {
                return false;
            }

            item.Set(CsvFormat, csv);
            item.Set(CsvWindowsFormat, csv);
            return true;
        }
    }
}
