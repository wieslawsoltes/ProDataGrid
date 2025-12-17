// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Collections;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Comparers;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class SortingModelPlaygroundViewModel : ObservableObject
    {
        private const int MaxLogEntries = 60;

        private bool _ownsSortDescriptions = true;
        private bool _multiSortEnabled = true;
        private SortCycleMode _sortCycleMode = SortCycleMode.AscendingDescendingNone;
        private bool _pinStatusPrimary = true;
        private ISortingModel? _sortingModel;

        public SortingModelPlaygroundViewModel()
        {
            Items = new ObservableCollection<Deployment>(Deployment.CreateSeed());
            ItemsView = new DataGridCollectionView(Items)
            {
                Culture = CultureInfo.InvariantCulture
            };

            DescriptorSummaries = new ObservableCollection<SortDescriptorSummary>();
            SortLog = new ObservableCollection<string>();

            ApplyStatusPresetCommand = new RelayCommand(_ => ApplyStatusPreset());
            ApplyPerformancePresetCommand = new RelayCommand(_ => ApplyPerformancePreset());
            ApplyRegionPresetCommand = new RelayCommand(_ => ApplyRegionPreset());
            AddServiceTiebreakerCommand = new RelayCommand(_ => AddServiceTiebreaker());
            PushExternalSortsCommand = new RelayCommand(_ => PushExternalSorts());
            ClearSortsCommand = new RelayCommand(_ => ClearSorts());
        }

        public ObservableCollection<Deployment> Items { get; }

        public DataGridCollectionView ItemsView { get; }

        public ObservableCollection<SortDescriptorSummary> DescriptorSummaries { get; }

        public ObservableCollection<string> SortLog { get; }

        public IComparer RingSorter => Comparers.RingComparer.Instance;

        public IComparer StatusSorter => Comparers.StatusComparer.Instance;

        public IComparer ServiceSorter => Comparers.ServiceNaturalComparer.Instance;

        public bool OwnsSortDescriptions
        {
            get => _ownsSortDescriptions;
            set
            {
                if (SetProperty(ref _ownsSortDescriptions, value) && _sortingModel != null)
                {
                    _sortingModel.OwnsViewSorts = value;
                }
            }
        }

        public bool MultiSortEnabled
        {
            get => _multiSortEnabled;
            set
            {
                if (SetProperty(ref _multiSortEnabled, value) && _sortingModel != null)
                {
                    _sortingModel.MultiSort = value;
                }
            }
        }

        public SortCycleMode SortCycleMode
        {
            get => _sortCycleMode;
            set
            {
                if (SetProperty(ref _sortCycleMode, value) && _sortingModel != null)
                {
                    _sortingModel.CycleMode = value;
                }
            }
        }

        public bool PinStatusPrimary
        {
            get => _pinStatusPrimary;
            set => SetProperty(ref _pinStatusPrimary, value);
        }

        public RelayCommand ApplyStatusPresetCommand { get; }

        public RelayCommand ApplyPerformancePresetCommand { get; }

        public RelayCommand ApplyRegionPresetCommand { get; }

        public RelayCommand AddServiceTiebreakerCommand { get; }

        public RelayCommand PushExternalSortsCommand { get; }

        public RelayCommand ClearSortsCommand { get; }

        public void AttachSortingModel(ISortingModel model)
        {
            if (ReferenceEquals(_sortingModel, model))
            {
                return;
            }

            if (_sortingModel != null)
            {
                _sortingModel.SortingChanging -= SortingModelOnSortingChanging;
                _sortingModel.SortingChanged -= SortingModelOnSortingChanged;
            }

            _sortingModel = model;
            _sortingModel.MultiSort = _multiSortEnabled;
            _sortingModel.CycleMode = _sortCycleMode;
            _sortingModel.OwnsViewSorts = _ownsSortDescriptions;

            _sortingModel.SortingChanging += SortingModelOnSortingChanging;
            _sortingModel.SortingChanged += SortingModelOnSortingChanged;

            UpdateDescriptorSummaries(_sortingModel.Descriptors);
        }

        private void SortingModelOnSortingChanging(object? sender, SortingChangingEventArgs e)
        {
            if (_pinStatusPrimary && !IsStatusFirst(e.NewDescriptors))
            {
                AppendLog("Change blocked: Status must remain the primary sort while pinning is enabled.");
                e.Cancel = true;
                return;
            }

            AppendLog($"SortingChanging: {Describe(e.OldDescriptors)} -> {Describe(e.NewDescriptors)}");
        }

        private void SortingModelOnSortingChanged(object? sender, SortingChangedEventArgs e)
        {
            UpdateDescriptorSummaries(e.NewDescriptors);
            AppendLog($"SortingChanged: {Describe(e.NewDescriptors)}");
        }

        private void ApplyStatusPreset()
        {
            ApplyProfile(new[]
            {
                CreateDescriptor(nameof(Deployment.Status), ListSortDirection.Ascending, StatusSorter),
                CreateDescriptor(nameof(Deployment.Ring), ListSortDirection.Ascending, RingSorter),
                CreateDescriptor(nameof(Deployment.Started), ListSortDirection.Descending)
            });

            AppendLog("Requested: Status (custom order) -> Ring (custom order) -> Newest first.");
        }

        private void ApplyPerformancePreset()
        {
            ApplyProfile(new[]
            {
                CreateDescriptor(nameof(Deployment.ErrorRate), ListSortDirection.Descending),
                CreateDescriptor(nameof(Deployment.Incidents), ListSortDirection.Descending),
                CreateDescriptor(nameof(Deployment.Service), ListSortDirection.Ascending, ServiceSorter)
            });

            AppendLog("Requested: Error rate descending then incidents descending with natural service tiebreaker.");
        }

        private void ApplyRegionPreset()
        {
            ApplyProfile(new[]
            {
                CreateDescriptor(nameof(Deployment.Region), ListSortDirection.Ascending),
                CreateDescriptor(nameof(Deployment.Service), ListSortDirection.Ascending, ServiceSorter),
                CreateDescriptor(nameof(Deployment.Started), ListSortDirection.Descending)
            });

            AppendLog("Requested: Region ascending then service (natural) ascending then newest first.");
        }

        private void AddServiceTiebreaker()
        {
            var descriptor = CreateDescriptor(nameof(Deployment.Service), ListSortDirection.Ascending, ServiceSorter);

            if (_sortingModel != null)
            {
                _sortingModel.SetOrUpdate(descriptor);
                AppendLog("Requested service natural sort as the last tiebreaker.");
            }
            else
            {
                AddSortDescription(descriptor);
                AppendLog("Requested service natural sort as fallback (view-level).");
            }
        }

        private void PushExternalSorts()
        {
            using (ItemsView.DeferRefresh())
            {
                ItemsView.SortDescriptions.Clear();
                ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Deployment.Region), ListSortDirection.Ascending, ItemsView.Culture));
                ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath(nameof(Deployment.ErrorRate), ListSortDirection.Descending, ItemsView.Culture));
            }

            AppendLog("Injected external SortDescriptions: Region ascending, Error rate descending.");
        }

        private void ClearSorts()
        {
            if (_sortingModel != null)
            {
                int before = _sortingModel.Descriptors.Count;
                _sortingModel.Clear();
                AppendLog(before == _sortingModel.Descriptors.Count
                    ? "Clear requested but current descriptors were preserved (likely by a guard)."
                    : "Cleared all sort descriptors.");
                return;
            }

            ItemsView.SortDescriptions.Clear();
            UpdateDescriptorSummaries(Array.Empty<SortingDescriptor>());
            AppendLog("Cleared all sort descriptors.");
        }

        private void ApplyProfile(IEnumerable<SortingDescriptor> descriptors)
        {
            var descriptorList = descriptors?.ToList() ?? new List<SortingDescriptor>();

            if (_sortingModel != null)
            {
                _sortingModel.Apply(descriptorList);
                return;
            }

            using (ItemsView.DeferRefresh())
            {
                ItemsView.SortDescriptions.Clear();
                foreach (var descriptor in descriptorList)
                {
                    AddSortDescription(descriptor);
                }
            }

            UpdateDescriptorSummaries(descriptorList);
        }

        private void AddSortDescription(SortingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return;
            }

            if (descriptor.HasComparer)
            {
                ItemsView.SortDescriptions.Add(DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction));
            }
            else if (descriptor.HasPropertyPath)
            {
                ItemsView.SortDescriptions.Add(DataGridSortDescription.FromPath(descriptor.PropertyPath, descriptor.Direction, descriptor.Culture ?? ItemsView.Culture));
            }
        }

        private SortingDescriptor CreateDescriptor(string propertyPath, ListSortDirection direction, IComparer? comparer = null)
        {
            return new SortingDescriptor(propertyPath, direction, propertyPath, comparer, ItemsView.Culture);
        }

        private bool IsStatusFirst(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return false;
            }

            return IsStatusDescriptor(descriptors[0]);
        }

        private static bool IsStatusDescriptor(SortingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return false;
            }

            if (descriptor.Comparer is StatusComparer)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(descriptor.PropertyPath) &&
                string.Equals(descriptor.PropertyPath, nameof(Deployment.Status), StringComparison.Ordinal))
            {
                return true;
            }

            if (descriptor.ColumnId is string columnId &&
                string.Equals(columnId, nameof(Deployment.Status), StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private void UpdateDescriptorSummaries(IReadOnlyList<SortingDescriptor> descriptors)
        {
            DescriptorSummaries.Clear();

            if (descriptors == null)
            {
                return;
            }

            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    continue;
                }

                DescriptorSummaries.Add(new SortDescriptorSummary(
                    DescribeColumn(descriptor),
                    descriptor.HasComparer ? "Comparer" : "Path",
                    descriptor.Direction.ToString()));
            }
        }

        private static string Describe(IEnumerable<SortingDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return "(none)";
            }

            var parts = descriptors
                .Where(d => d != null)
                .Select(d => $"{DescribeColumn(d)} {ShortDirection(d.Direction)}")
                .ToArray();

            return parts.Length == 0 ? "(none)" : string.Join(", ", parts);
        }

        private static string DescribeColumn(SortingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return "(null)";
            }

            if (!string.IsNullOrEmpty(descriptor.PropertyPath))
            {
                return descriptor.PropertyPath;
            }

            if (descriptor.Comparer != null)
            {
                return descriptor.Comparer.GetType().Name;
            }

            return descriptor.ColumnId?.ToString() ?? "(unknown)";
        }

        private static string ShortDirection(ListSortDirection direction)
        {
            return direction == ListSortDirection.Ascending ? "asc" : "desc";
        }

        private void AppendLog(string message)
        {
            var entry = $"{DateTime.Now:HH:mm:ss} {message}";
            SortLog.Insert(0, entry);

            while (SortLog.Count > MaxLogEntries)
            {
                SortLog.RemoveAt(SortLog.Count - 1);
            }
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public record SortDescriptorSummary(string Column, string Kind, string Direction);
    }
}
