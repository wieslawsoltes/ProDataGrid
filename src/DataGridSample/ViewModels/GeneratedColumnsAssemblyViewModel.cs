using System.Collections.ObjectModel;
using DataGridSample.Models.SourceGenerationAssembly;
using ReactiveUI;

namespace DataGridSample.ViewModels;

public sealed partial class GeneratedColumnsAssemblyViewModel : ReactiveObject
{
    public GeneratedColumnsAssemblyViewModel()
    {
        Items = new ObservableCollection<GeneratedAssemblyRow>
        {
            new() { Sequence = 1, Namespace = "DataGridSample.Models.SourceGenerationAssembly", DiscoveryMode = "Assembly target", ReflectionFree = true },
            new() { Sequence = 2, Namespace = "DataGridSample.Generated", DiscoveryMode = "Custom provider namespace", ReflectionFree = true }
        };
    }

    public ObservableCollection<GeneratedAssemblyRow> Items { get; }
}
