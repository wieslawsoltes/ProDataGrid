using System.Collections.ObjectModel;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedCustomRow), ProviderName = "GeneratedCustomFacadeSchema")]
public sealed partial class GeneratedColumnsCustomViewModel : ReactiveObject
{
    public GeneratedColumnsCustomViewModel()
    {
        Items = new ObservableCollection<GeneratedCustomRow>
        {
            new() { Id = 1, Label = "User-defined implementation", Priority = 5 },
            new() { Id = 2, Label = "Generated facade", Priority = 3 }
        };
    }

    public ObservableCollection<GeneratedCustomRow> Items { get; }
}
