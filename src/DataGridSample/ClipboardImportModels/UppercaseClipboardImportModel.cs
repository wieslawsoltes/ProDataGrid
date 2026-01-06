using System.Collections.Generic;
using Avalonia.Controls.DataGridClipboard;

namespace DataGridSample.ClipboardImportModels
{
    public sealed class UppercaseClipboardImportModel : DataGridClipboardImportModel
    {
        protected override List<List<string>> ParseClipboardText(string text)
        {
            var rows = base.ParseClipboardText(text);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                for (var colIndex = 0; colIndex < row.Count; colIndex++)
                {
                    row[colIndex] = row[colIndex]?.ToUpperInvariant() ?? string.Empty;
                }
            }

            return rows;
        }
    }
}
