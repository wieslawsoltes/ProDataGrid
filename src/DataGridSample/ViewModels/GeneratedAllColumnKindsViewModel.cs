using System.Collections.ObjectModel;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedAllColumnKindsRow), ProviderName = "GeneratedAllColumnKindsSchema")]
public sealed partial class GeneratedAllColumnKindsViewModel : ReactiveObject
{
    public GeneratedAllColumnKindsViewModel()
    {
        Items = new ObservableCollection<GeneratedAllColumnKindsRow>
        {
            new(),
            new() { Text = "Second row", Numeric = 87, ProgressBar = 25, Slider = 80, CheckBox = false }
        };
    }

    public ObservableCollection<GeneratedAllColumnKindsRow> Items { get; }
}
