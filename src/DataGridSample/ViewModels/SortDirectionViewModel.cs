// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Collections;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class SortDirectionViewModel : ObservableObject
    {
        private bool _ownsSortDescriptions = true;
        private bool _multiSortEnabled = true;
        private SortCycleMode _sortCycleMode = SortCycleMode.AscendingDescendingNone;

        public SortDirectionViewModel()
        {
            Items = new ObservableCollection<Country>(Countries.All);
            ItemsView = new DataGridCollectionView(Items);

            SavedSorts = new ObservableCollection<SortState>();
            PresetSorts = new ObservableCollection<SortState>
            {
                new(nameof(Country.Region), ListSortDirection.Ascending),
                new(nameof(Country.Name), ListSortDirection.Ascending),
                new(nameof(Country.Population), ListSortDirection.Descending)
            };

            SaveCurrentSortsCommand = new RelayCommand(_ => SaveCurrentSorts());
            LoadPresetCommand = new RelayCommand(_ => ReplaceSavedSorts(PresetSorts));
            ClearSavedSortsCommand = new RelayCommand(_ => SavedSorts.Clear());
        }

        public ObservableCollection<Country> Items { get; }

        public DataGridCollectionView ItemsView { get; }

        public ObservableCollection<SortState> SavedSorts { get; }

        public ObservableCollection<SortState> PresetSorts { get; }

        public bool OwnsSortDescriptions
        {
            get => _ownsSortDescriptions;
            set => SetProperty(ref _ownsSortDescriptions, value);
        }

        public bool MultiSortEnabled
        {
            get => _multiSortEnabled;
            set => SetProperty(ref _multiSortEnabled, value);
        }

        public SortCycleMode SortCycleMode
        {
            get => _sortCycleMode;
            set => SetProperty(ref _sortCycleMode, value);
        }

        public RelayCommand SaveCurrentSortsCommand { get; }

        public RelayCommand LoadPresetCommand { get; }

        public RelayCommand ClearSavedSortsCommand { get; }

        public IReadOnlyList<SortState> CaptureCurrentSorts()
        {
            return ItemsView.SortDescriptions
                .Where(sd => sd.HasPropertyPath)
                .Select(sd => new SortState(sd.PropertyPath, sd.Direction))
                .ToList();
        }

        public void ReplaceSavedSorts(IEnumerable<SortState> sorts)
        {
            SavedSorts.Clear();
            foreach (var sort in sorts)
            {
                SavedSorts.Add(sort);
            }
        }

        private void SaveCurrentSorts()
        {
            ReplaceSavedSorts(CaptureCurrentSorts());
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public record SortState(string Property, ListSortDirection Direction);
}
