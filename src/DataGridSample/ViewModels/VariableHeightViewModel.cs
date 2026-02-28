using System;
using System.Collections.Generic;
using System.Windows.Input;
using DataGridSample.Collections;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class VariableHeightViewModel : ObservableObject
    {
        private int _itemCount = 500;
        private int _seed = 42;
        private int _scrollToIndex;
        private string _selectedEstimator = "Advanced";
        private string _itemCountText = "Items: 0";
        private string _scrollInfoText = "Scroll Position: N/A";
        private string _visibleRangeText = "Visible Range: N/A";

        public VariableHeightViewModel()
        {
            Estimators = new[] { "Advanced", "Caching", "Default" };
            Items = new ObservableRangeCollection<VariableHeightItem>();
            RegenerateCommand = new RelayCommand(_ => GenerateItems());
        }

        public ObservableRangeCollection<VariableHeightItem> Items { get; }

        public IReadOnlyList<string> Estimators { get; }

        public ICommand RegenerateCommand { get; }

        public int ItemCount
        {
            get => _itemCount;
            set => SetProperty(ref _itemCount, value);
        }

        public int Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }

        public int ScrollToIndex
        {
            get => _scrollToIndex;
            set => SetProperty(ref _scrollToIndex, value);
        }

        public string SelectedEstimator
        {
            get => _selectedEstimator;
            set => SetProperty(ref _selectedEstimator, value);
        }

        public string ItemCountText
        {
            get => _itemCountText;
            set => SetProperty(ref _itemCountText, value);
        }

        public string ScrollInfoText
        {
            get => _scrollInfoText;
            set => SetProperty(ref _scrollInfoText, value);
        }

        public string VisibleRangeText
        {
            get => _visibleRangeText;
            set => SetProperty(ref _visibleRangeText, value);
        }

        public void GenerateItems()
        {
            Items.ResetWith(VariableHeightItem.GenerateItems(ItemCount, Seed));

            ItemCountText = $"Items: {Items.Count}";
            ItemsRegenerated?.Invoke();
        }

        public event Action? ItemsRegenerated;
    }
}
