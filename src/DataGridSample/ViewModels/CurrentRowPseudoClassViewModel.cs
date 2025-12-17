using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class CurrentRowPseudoClassViewModel : ObservableObject
    {
        private CurrentPerson? _selectedPerson;

        public CurrentRowPseudoClassViewModel()
        {
            People = new ObservableCollection<CurrentPerson>
            {
                new("Ada Lovelace", "Engineering", "Analyst"),
                new("Grace Hopper", "Architecture", "Compiler Lead"),
                new("Alan Turing", "Research", "Cryptography"),
                new("Katherine Johnson", "Aerospace", "Navigation"),
                new("Radia Perlman", "Networking", "Protocol Design"),
                new("Edsger Dijkstra", "Research", "Algorithms"),
                new("Barbara Liskov", "Platform", "Reliability"),
                new("Ken Thompson", "Systems", "Unix"),
                new("Margaret Hamilton", "Aerospace", "Software"),
                new("Linus Torvalds", "Systems", "Kernel")
            };

            SelectedPerson = People.FirstOrDefault();
        }

        public ObservableCollection<CurrentPerson> People { get; }

        public CurrentPerson? SelectedPerson
        {
            get => _selectedPerson;
            set => SetProperty(ref _selectedPerson, value);
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class CurrentPerson
    {
        public CurrentPerson(string name, string team, string role)
        {
            Name = name;
            Team = team;
            Role = role;
        }

        public string Name { get; }

        public string Team { get; }

        public string Role { get; }
    }
}
