using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "GeneratedCustomFacadeSchema",
    ImplementationType = typeof(GeneratedCustomImplementationSchema))]
public sealed class GeneratedCustomRow
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public int Priority { get; set; }

    public static void ConfigureLabel(DataGridTextColumnDefinition column)
    {
        column.Watermark = "Customized by user code";
        column.Width = new DataGridLength(2, DataGridLengthUnitType.Star);
    }
}
