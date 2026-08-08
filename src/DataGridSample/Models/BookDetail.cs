using System.Collections.ObjectModel;
using ProDataGrid.SourceGeneration;

namespace DataGridSample.Models;

[GenerateDataGridColumns(
    ProviderName = "RowDetailsBookSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class BookDetail
{
    [DataGridColumn(DataGridColumnKind.Text, Header = "Title", Order = 0, Width = "2*")]
    public string Title { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "In Stock", Order = 1, Width = "120")]
    public int InStock { get; set; }

    public string Summary { get; set; } = string.Empty;

    public ObservableCollection<AuthorDetail> Authors { get; } = new();
}

[GenerateDataGridColumns(
    ProviderName = "RowDetailsAuthorSchema",
    Discovery = DataGridColumnDiscovery.AttributedOnly)]
public sealed class AuthorDetail
{
    [DataGridColumn(DataGridColumnKind.Text, Header = "Author", Order = 0, Width = "2*")]
    public string Name { get; set; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Text, Header = "Contribution", Order = 1, Width = "*")]
    public string Contribution { get; set; } = string.Empty;
}
