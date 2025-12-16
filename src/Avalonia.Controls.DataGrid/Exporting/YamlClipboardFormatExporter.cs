// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls
{
    internal sealed class YamlClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        internal static readonly DataFormat<string> YamlFormat = DataFormat.CreateStringPlatformFormat("application/x-yaml");

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            if (context.Formats != DataGridClipboardExportFormat.Yaml)
            {
                return false;
            }

            var yaml = DataGridClipboardFormatting.BuildYaml(context.Rows);
            if (string.IsNullOrEmpty(yaml))
            {
                return false;
            }

            item.Set(DataFormat.Text, yaml);
            item.Set(YamlFormat, yaml);
            return true;
        }
    }
}
