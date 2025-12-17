// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Collections;
using Avalonia.Controls.DataGridSorting;
using DataGridSample.Adapters;
using DataGridSample.Models;
using DataGridSample.Mvvm;
using DynamicData;

namespace DataGridSample.ViewModels
{
    /// <summary>
    /// Demonstrates wiring SortingModel to a DynamicData pipeline via a custom adapter factory.
    /// </summary>
    public class DynamicDataSortingViewModel : ObservableObject, IDisposable
    {
        private readonly ReadOnlyObservableCollection<Deployment> _view;
        private readonly SourceList<Deployment> _source;
        private readonly CompositeDisposable _cleanup = new();
        private readonly BehaviorSubject<IComparer<Deployment>> _sortSubject;
        private readonly DynamicDataSortingAdapterFactory _adapterFactory;
        private ISortingModel? _sortingModel;
        private bool _multiSortEnabled = true;
        private SortCycleMode _sortCycleMode = SortCycleMode.AscendingDescendingNone;

        public DynamicDataSortingViewModel()
        {
            _source = new SourceList<Deployment>();
            _source.AddRange(Deployment.CreateSeed());

            _adapterFactory = new DynamicDataSortingAdapterFactory(OnUpstreamSortsChanged);
            _sortSubject = new BehaviorSubject<IComparer<Deployment>>(_adapterFactory.SortComparer);

            var subscription = _source.Connect()
                .Sort(_sortSubject)
                .Bind(out _view)
                .Subscribe();
            _cleanup.Add(subscription);

            SortingModel = new SortingModel
            {
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescendingNone,
                OwnsViewSorts = true
            };

            SortingModel.SortingChanged += SortingModelOnSortingChanged;
            UpdateDescriptorSummaries(SortingModel.Descriptors);

            ApplyPresetCommand = new RelayCommand(_ => ApplyPreset());
            ClearSortsCommand = new RelayCommand(_ => SortingModel.Clear());
        }

        public ReadOnlyObservableCollection<Deployment> View => _view;

        public ISortingModel SortingModel
        {
            get => _sortingModel!;
            private set => SetProperty(ref _sortingModel, value);
        }

        public DynamicDataSortingAdapterFactory AdapterFactory => _adapterFactory;

        public ObservableCollection<SortDescriptorSummary> DescriptorSummaries { get; } = new();

        public ObservableCollection<string> UpstreamSorts { get; } = new();

        public bool MultiSortEnabled
        {
            get => _multiSortEnabled;
            set
            {
                if (SetProperty(ref _multiSortEnabled, value) && SortingModel != null)
                {
                    SortingModel.MultiSort = value;
                }
            }
        }

        public SortCycleMode SortCycleMode
        {
            get => _sortCycleMode;
            set
            {
                if (SetProperty(ref _sortCycleMode, value) && SortingModel != null)
                {
                    SortingModel.CycleMode = value;
                }
            }
        }

        public RelayCommand ApplyPresetCommand { get; }

        public RelayCommand ClearSortsCommand { get; }

        private void ApplyPreset()
        {
            SortingModel.Apply(new[]
            {
                new SortingDescriptor("Status", ListSortDirection.Ascending, "Status"),
                new SortingDescriptor("Service", ListSortDirection.Ascending, "Service"),
                new SortingDescriptor("Started", ListSortDirection.Descending, "Started")
            });
        }

        private void SortingModelOnSortingChanged(object? sender, SortingChangedEventArgs e)
        {
            UpdateDescriptorSummaries(e.NewDescriptors);
            _adapterFactory.UpdateComparer(e.NewDescriptors);
            _sortSubject.OnNext(_adapterFactory.SortComparer);
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
                DescriptorSummaries.Add(new SortDescriptorSummary(
                    descriptor.PropertyPath ?? descriptor.ColumnId?.ToString() ?? "(unknown)",
                    descriptor.HasComparer ? "Comparer" : "Path",
                    descriptor.Direction.ToString()));
            }
        }

        private void OnUpstreamSortsChanged(string description)
        {
            UpstreamSorts.Insert(0, $"{DateTime.Now:HH:mm:ss} {description}");
            while (UpstreamSorts.Count > 20)
            {
                UpstreamSorts.RemoveAt(UpstreamSorts.Count - 1);
            }
        }

        public void Dispose()
        {
            _sortSubject.Dispose();
            _cleanup.Dispose();
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public record SortDescriptorSummary(string Column, string Kind, string Direction);
    }
}
