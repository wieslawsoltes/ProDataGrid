using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DataGridSample.Models;

namespace DataGridSample
{
    public partial class AutoColumnsPage : UserControl, INotifyPropertyChanged
    {
        private int _itemCount = 50;

        public ObservableCollection<Person> Items { get; } = new();

        public int ItemCount
        {
            get => _itemCount;
            set
            {
                if (_itemCount != value)
                {
                    _itemCount = value;
                    OnPropertyChanged(nameof(ItemCount));
                }
            }
        }

        public AutoColumnsPage()
        {
            InitializeComponent();
            DataContext = this;
            Populate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Populate()
        {
            Items.Clear();
            var random = new Random(23);

            string[] firstNames = { "Alex", "Sam", "Jordan", "Taylor", "Morgan", "Jamie", "Casey", "Riley", "Avery", "Skyler" };
            string[] lastNames = { "Smith", "Johnson", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor", "Anderson", "Thomas" };

            for (int i = 0; i < ItemCount; i++)
            {
                var person = new Person
                {
                    FirstName = firstNames[random.Next(firstNames.Length)],
                    LastName = lastNames[random.Next(lastNames.Length)],
                    Age = random.Next(18, 75),
                    IsBanned = random.NextDouble() < 0.15
                };
                Items.Add(person);
            }
        }

        private void OnRegenerateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Populate();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
