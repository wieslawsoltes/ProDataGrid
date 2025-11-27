using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Collections;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class DataGridPageViewModel : ObservableObject
    {
        public DataGridPageViewModel()
        {
            RegionSortDescription = DataGridSortDescription.FromPath(
                nameof(Country.Region),
                ListSortDirection.Ascending,
                new ReversedStringComparer());

            CountriesView = new DataGridCollectionView(Countries.All);
            CountriesView.SortDescriptions.Add(RegionSortDescription);

            GroupedCountriesView = new DataGridCollectionView(Countries.All);
            GroupedCountriesView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(Country.Region)));

            EditablePeople = new ObservableCollection<Person>
            {
                new Person { FirstName = "John", LastName = "Doe" , Age = 30},
                new Person { FirstName = "Elizabeth", LastName = "Thomas", IsBanned = true , Age = 40 },
                new Person { FirstName = "Zack", LastName = "Ward" , Age = 50 }
            };

            AddPersonCommand = new RelayCommand(_ => EditablePeople.Add(new Person()));
        }

        public DataGridCollectionView CountriesView { get; }

        public DataGridCollectionView GroupedCountriesView { get; }

        public ObservableCollection<Person> EditablePeople { get; }

        public ICommand AddPersonCommand { get; }

        public DataGridSortDescription RegionSortDescription { get; }

        public void EnsureCustomSort(string propertyPath)
        {
            if (propertyPath == RegionSortDescription.PropertyPath &&
                !CountriesView.SortDescriptions.Contains(RegionSortDescription))
            {
                CountriesView.SortDescriptions.Add(RegionSortDescription);
            }
        }

        private sealed class ReversedStringComparer : IComparer<object?>, IComparer
        {
            public int Compare(object? x, object? y)
            {
                if (x is string left && y is string right)
                {
                    var reversedLeft = new string(left.Reverse().ToArray());
                    var reversedRight = new string(right.Reverse().ToArray());
                    return reversedLeft.CompareTo(reversedRight);
                }

                return Comparer.Default.Compare(x, y);
            }
        }
    }
}
