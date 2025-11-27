using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DataGridSample.Models;

namespace DataGridSample
{
    public partial class FrozenColumnsPage : UserControl, INotifyPropertyChanged
    {
        private int _frozenColumnCount = 2;

        public ObservableCollection<PixelItem> Items { get; } = new();

        public int FrozenColumnCount
        {
            get => _frozenColumnCount;
            set
            {
                if (_frozenColumnCount != value)
                {
                    _frozenColumnCount = value;
                    OnPropertyChanged(nameof(FrozenColumnCount));
                }
            }
        }

        public FrozenColumnsPage()
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
            var random = new Random(17);
            for (int i = 1; i <= 200; i++)
            {
                Items.Add(PixelItem.Create(i, random));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
