using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls.Selection;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class ItemsSourceSwapViewModel : ObservableObject
    {
        private IEnumerable? _itemsSource;
        private string _status = string.Empty;

        public ItemsSourceSwapViewModel()
        {
            ListA = CreateItems("Alpha", 6);
            ListB = CreateItems("Beta", 5);
            ViewB = new DataGridCollectionView(ListB);
            SelectionModel = new SelectionModel<SwapItem> { SingleSelect = false };

            SelectionModel.SelectionChanged += (_, _) => UpdateStatus();
            SelectionModel.PropertyChanged += SelectionModelOnPropertyChanged;

            UseListACommand = new RelayCommand(_ => SetItemsSource(ListA));
            UseListBCommand = new RelayCommand(_ => SetItemsSource(ListB));
            UseViewCommand = new RelayCommand(_ => SetItemsSource(ViewB));
            ClearCommand = new RelayCommand(_ => SetItemsSource(null));
            ResetCommand = new RelayCommand(_ => ResetItems());

            SetItemsSource(ListA);
        }

        public ObservableCollection<SwapItem> ListA { get; }
        public ObservableCollection<SwapItem> ListB { get; }
        public DataGridCollectionView ViewB { get; }
        public SelectionModel<SwapItem> SelectionModel { get; }

        public IEnumerable? ItemsSource
        {
            get => _itemsSource;
            private set => SetProperty(ref _itemsSource, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public RelayCommand UseListACommand { get; }
        public RelayCommand UseListBCommand { get; }
        public RelayCommand UseViewCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand ResetCommand { get; }

        private void SetItemsSource(IEnumerable? source)
        {
            ItemsSource = source;
            UpdateStatus();
        }

        private void ResetItems()
        {
            ListA.Clear();
            ListB.Clear();

            foreach (var item in CreateItems("Alpha", 6))
            {
                ListA.Add(item);
            }

            foreach (var item in CreateItems("Beta", 5))
            {
                ListB.Add(item);
            }

            SetItemsSource(ListA);
        }

        private void SelectionModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectionModel<SwapItem>.Source) ||
                e.PropertyName == nameof(SelectionModel<SwapItem>.SelectedIndex) ||
                e.PropertyName == nameof(SelectionModel<SwapItem>.SelectedItem))
            {
                UpdateStatus();
            }
        }

        private void UpdateStatus()
        {
            Status = $"ItemsSource: {DescribeSource(ItemsSource)} | SelectionModel.Source: {DescribeSource(SelectionModel.Source)} | Selected: {SelectionModel.SelectedItems.Count}";
        }

        private static string DescribeSource(IEnumerable? source)
        {
            if (source == null)
            {
                return "null";
            }

            var count = source is ICollection collection ? collection.Count : -1;
            var typeName = source.GetType().Name;
            return count >= 0 ? $"{typeName} ({count})" : typeName;
        }

        private static ObservableCollection<SwapItem> CreateItems(string prefix, int count)
        {
            var items = new ObservableCollection<SwapItem>();
            for (var i = 0; i < count; i++)
            {
                items.Add(new SwapItem
                {
                    Name = $"{prefix} {i + 1}",
                    Value = (i + 1) * 10
                });
            }

            return items;
        }

        public class SwapItem
        {
            public string Name { get; set; } = string.Empty;
            public int Value { get; set; }
        }
    }
}
