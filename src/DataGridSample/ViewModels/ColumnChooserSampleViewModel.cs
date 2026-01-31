using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Helpers;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ColumnChooserSampleViewModel : ObservableObject
    {
        public ColumnChooserSampleViewModel()
        {
            Items = CreatePeople();

            ColumnDefinitions = new ObservableCollection<DataGridColumnDefinition>
            {
                new DataGridTextColumnDefinition
                {
                    Header = "First Name",
                    Binding = ColumnDefinitionBindingFactory.CreateBinding<Person, string>(
                        nameof(Person.FirstName),
                        p => p.FirstName,
                        (p, v) => p.FirstName = v),
                    Width = new DataGridLength(1.2, DataGridLengthUnitType.Star),
                    CanUserHide = false
                },
                new DataGridTextColumnDefinition
                {
                    Header = "Last Name",
                    Binding = ColumnDefinitionBindingFactory.CreateBinding<Person, string>(
                        nameof(Person.LastName),
                        p => p.LastName,
                        (p, v) => p.LastName = v),
                    Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
                },
                new DataGridNumericColumnDefinition
                {
                    Header = "Age",
                    Binding = ColumnDefinitionBindingFactory.CreateBinding<Person, int>(
                        nameof(Person.Age),
                        p => p.Age,
                        (p, v) => p.Age = v),
                    Width = new DataGridLength(0.7, DataGridLengthUnitType.Star),
                    Minimum = 0,
                    Maximum = 120,
                    Increment = 1,
                    FormatString = "N0"
                },
                new DataGridComboBoxColumnDefinition
                {
                    Header = "Status",
                    ItemsSource = Enum.GetValues<PersonStatus>(),
                    SelectedItemBinding = ColumnDefinitionBindingFactory.CreateBinding<Person, PersonStatus>(
                        nameof(Person.Status),
                        p => p.Status,
                        (p, v) => p.Status = v),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                },
                new DataGridTextColumnDefinition
                {
                    Header = "Optional Status",
                    Binding = ColumnDefinitionBindingFactory.CreateBinding<Person, PersonStatus?>(
                        nameof(Person.OptionalStatus),
                        p => p.OptionalStatus,
                        (p, v) => p.OptionalStatus = v),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    IsVisible = false
                },
                new DataGridCheckBoxColumnDefinition
                {
                    Header = "Banned",
                    Binding = ColumnDefinitionBindingFactory.CreateBinding<Person, bool>(
                        nameof(Person.IsBanned),
                        p => p.IsBanned,
                        (p, v) => p.IsBanned = v),
                    Width = new DataGridLength(0.7, DataGridLengthUnitType.Star)
                }
            };
        }

        public ObservableCollection<Person> Items { get; }

        public ObservableCollection<DataGridColumnDefinition> ColumnDefinitions { get; }

        private static ObservableCollection<Person> CreatePeople()
        {
            return new ObservableCollection<Person>
            {
                new Person
                {
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    Age = 36,
                    Status = PersonStatus.Active,
                    OptionalStatus = PersonStatus.Active,
                    IsBanned = false,
                    ProfileLink = new Uri("https://example.com/ada")
                },
                new Person
                {
                    FirstName = "Alan",
                    LastName = "Turing",
                    Age = 41,
                    Status = PersonStatus.Suspended,
                    OptionalStatus = PersonStatus.Suspended,
                    IsBanned = false,
                    ProfileLink = new Uri("https://example.com/alan")
                },
                new Person
                {
                    FirstName = "Grace",
                    LastName = "Hopper",
                    Age = 85,
                    Status = PersonStatus.Active,
                    OptionalStatus = PersonStatus.Active,
                    IsBanned = true,
                    ProfileLink = new Uri("https://example.com/grace")
                },
                new Person
                {
                    FirstName = "Edsger",
                    LastName = "Dijkstra",
                    Age = 72,
                    Status = PersonStatus.Disabled,
                    OptionalStatus = PersonStatus.Disabled,
                    IsBanned = false,
                    ProfileLink = new Uri("https://example.com/edsger")
                }
            };
        }
    }
}
