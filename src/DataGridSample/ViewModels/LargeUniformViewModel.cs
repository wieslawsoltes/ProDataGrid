using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DataGridSample.Collections;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class LargeUniformViewModel : ObservableObject
    {
        private int _itemCount = 200_000;
        private string _summary = "Items: 0";
        private string _selectedEstimator = "Advanced";
        private bool _isRegenerating;

        public LargeUniformViewModel()
        {
            Items = new ObservableRangeCollection<PixelItem>();
            Estimators = new[] { "Advanced", "Caching", "Default" };
            RegenerateCommand = new RelayCommand(
                _ => _ = PopulateAsync(),
                _ => !IsRegenerating);
            _ = PopulateAsync();
        }

        public ObservableRangeCollection<PixelItem> Items { get; }

        public RelayCommand RegenerateCommand { get; }

        public IReadOnlyList<string> Estimators { get; }

        public int ItemCount
        {
            get => _itemCount;
            set => SetProperty(ref _itemCount, value);
        }

        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public string SelectedEstimator
        {
            get => _selectedEstimator;
            set => SetProperty(ref _selectedEstimator, value);
        }

        public bool IsRegenerating
        {
            get => _isRegenerating;
            private set
            {
                if (SetProperty(ref _isRegenerating, value))
                {
                    RegenerateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private async Task PopulateAsync()
        {
            if (IsRegenerating)
            {
                return;
            }

            IsRegenerating = true;
            var targetCount = ItemCount;
            Summary = $"Items: {targetCount:n0} | Regenerating...";

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var items = await Task.Run(() =>
                {
                    var list = new List<PixelItem>(targetCount);
                    var random = new Random(17);
                    for (int i = 1; i <= targetCount; i++)
                    {
                        list.Add(PixelItem.Create(i, random));
                    }

                    return list;
                }).ConfigureAwait(true);

                Items.ResetWith(items);

                stopwatch.Stop();
                Summary = $"Items: {Items.Count:n0} | Regenerated in {stopwatch.Elapsed.TotalSeconds:N2}s";
            }
            catch (Exception ex)
            {
                Summary = $"Regenerate failed: {ex.Message}";
            }
            finally
            {
                IsRegenerating = false;
            }
        }
    }
}
