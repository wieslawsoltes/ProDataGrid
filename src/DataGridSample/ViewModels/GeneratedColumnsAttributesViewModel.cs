using System.Collections.ObjectModel;
using DataGridSample.Models;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using DataGridSample.Pages;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedEmployee), ProviderName = "GeneratedEmployeeDataGridSchema")]
[GenerateDataGridView(
    typeof(GeneratedEmployee),
    ViewName = "GeneratedColumnsCodeView",
    ViewNamespace = "DataGridSample.Pages",
    BaseType = typeof(GeneratedGridViewBase),
    Title = "Generated C# view with custom base",
    Recipe = DataGridViewRecipe.Explorer,
    AutomationId = "generated-columns-code-grid")]
public sealed partial class GeneratedColumnsAttributesViewModel : ReactiveObject
{
    public GeneratedColumnsAttributesViewModel()
    {
        Items = new ObservableCollection<GeneratedEmployee>
        {
            new() { Id = 1, Name = "Ada Lovelace", Team = "Compiler", Score = 98, IsActive = true, Joined = new(1843, 1, 1, 0, 0, 0, System.TimeSpan.Zero) },
            new() { Id = 2, Name = "Grace Hopper", Team = "Runtime", Score = 96, IsActive = true, Joined = new(1944, 6, 1, 0, 0, 0, System.TimeSpan.Zero) },
            new() { Id = 3, Name = "Edsger Dijkstra", Team = "Algorithms", Score = 94, IsActive = false, Joined = new(1952, 9, 1, 0, 0, 0, System.TimeSpan.Zero) }
        };
    }

    public ObservableCollection<GeneratedEmployee> Items { get; }
}
