using System.Collections.ObjectModel;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ColumnDragIndicatorViewModel : ObservableObject
    {
        public ColumnDragIndicatorViewModel()
        {
            Items = new ObservableCollection<Person>
            {
                new() { FirstName = "Ada", LastName = "Lovelace", Age = 36, Status = PersonStatus.Active },
                new() { FirstName = "Alan", LastName = "Turing", Age = 41, Status = PersonStatus.New },
                new() { FirstName = "Grace", LastName = "Hopper", Age = 85, Status = PersonStatus.Suspended },
                new() { FirstName = "Jean", LastName = "Bartik", Age = 86, Status = PersonStatus.Active },
                new() { FirstName = "Claude", LastName = "Shannon", Age = 84, Status = PersonStatus.Disabled }
            };

            TemplateHeader = new ColumnHeaderInfo("Status", "templated header");
        }

        public ObservableCollection<Person> Items { get; }

        public ColumnHeaderInfo TemplateHeader { get; }

        public record ColumnHeaderInfo(string Title, string Detail);
    }
}
