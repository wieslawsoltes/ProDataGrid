// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls.DataGridFiltering;
using DataGridSample.Adapters;
using DataGridSample.Models;
using DataGridSample.Mvvm;
using DynamicData;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>
    /// Demonstrates wiring FilteringModel to a DynamicData pipeline via a custom adapter factory,
    /// with filter editors bound to FilteringModel instead of touching the view directly.
    /// </summary>
    public class DynamicDataFilteringViewModel : ObservableObject, IDisposable
    {
        private readonly ReadOnlyObservableCollection<Deployment> _view;
        private readonly SourceList<Deployment> _source;
        private readonly CompositeDisposable _cleanup = new();
        private readonly BehaviorSubject<Func<Deployment, bool>> _filterSubject;
        private readonly DynamicDataFilteringAdapterFactory _adapterFactory;
        private readonly Subject<string> _serviceFilterSubject = new();
        private IFilteringModel? _filteringModel;
        private string? _serviceFilter;
        private string? _selectedRegion;
        private double? _minErrorRate;
        private double? _maxErrorRate;
        private DateTimeOffset? _startedFrom;
        private DateTimeOffset? _startedTo;

        public DynamicDataFilteringViewModel()
        {
            _source = new SourceList<Deployment>();
            _source.AddRange(Deployment.CreateSeed());

            _adapterFactory = new DynamicDataFilteringAdapterFactory(OnUpstreamFiltersChanged);
            _filterSubject = new BehaviorSubject<Func<Deployment, bool>>(_adapterFactory.FilterPredicate);

            var subscription = _source.Connect()
                .Filter(_filterSubject)
                .Bind(out _view)
                .Subscribe();
            _cleanup.Add(subscription);

            FilteringModel = new FilteringModel
            {
                OwnsViewFilter = true
            };

            FilteringModel.FilteringChanged += FilteringModelOnFilteringChanged;
            UpdateDescriptorSummaries(FilteringModel.Descriptors);

            ApplyPresetCommand = new RelayCommand(_ => ApplyPreset());
            ClearFiltersCommand = new RelayCommand(_ => FilteringModel.Clear());
            RemoveChipCommand = new RelayCommand(columnId => RemoveDescriptor(columnId));
            ApplyServiceFilterCommand = new RelayCommand(_ => ApplyServiceFilter(ServiceFilter));
            ClearServiceFilterCommand = new RelayCommand(_ => ServiceFilter = string.Empty);
            ApplyStatusFilterCommand = new RelayCommand(_ => ApplyStatusFilter());
            ClearStatusFilterCommand = new RelayCommand(_ => ClearStatusFilter());
            ApplyRegionFilterCommand = new RelayCommand(_ => ApplyRegionFilter(SelectedRegion));
            ClearRegionFilterCommand = new RelayCommand(_ => SelectedRegion = null);
            ApplyErrorFilterCommand = new RelayCommand(_ => ApplyErrorRateFilter());
            ClearErrorFilterCommand = new RelayCommand(_ => ClearErrorFilter());
            ApplyStartedFilterCommand = new RelayCommand(_ => ApplyStartedFilter());
            ClearStartedFilterCommand = new RelayCommand(_ => ClearStartedFilter());

            foreach (var status in StatusOptions)
            {
                status.Changed += OnStatusChanged;
            }

            var serviceFilterSubscription = _serviceFilterSubject
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(ApplyServiceFilter);
            _cleanup.Add(serviceFilterSubscription);
        }

        public ReadOnlyObservableCollection<Deployment> View => _view;

        public IFilteringModel FilteringModel
        {
            get => _filteringModel!;
            private set => SetProperty(ref _filteringModel, value);
        }

        public DynamicDataFilteringAdapterFactory AdapterFactory => _adapterFactory;

        public ObservableCollection<FilterDescriptorSummary> DescriptorSummaries { get; } = new();

        public ObservableCollection<FilterChip> FilterChips { get; } = new();

        public ObservableCollection<string> UpstreamFilters { get; } = new();

        public ObservableCollection<SelectableFilterValue> StatusOptions { get; } = new(new[]
        {
            new SelectableFilterValue("Rolling Out"),
            new SelectableFilterValue("Investigating"),
            new SelectableFilterValue("Paused"),
            new SelectableFilterValue("Completed"),
            new SelectableFilterValue("Blocked")
        });

        public ObservableCollection<string> RegionOptions { get; } = new(new[]
        {
            "us-east", "us-west", "eu-west", "apac-south", "us-central", "eu-north"
        });

        public string? ServiceFilter
        {
            get => _serviceFilter;
            set
            {
                if (SetProperty(ref _serviceFilter, value))
                {
                    _serviceFilterSubject.OnNext(value ?? string.Empty);
                }
            }
        }

        public string? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (SetProperty(ref _selectedRegion, value))
                {
                    ApplyRegionFilter(value);
                }
            }
        }

        public double? MinErrorRate
        {
            get => _minErrorRate;
            set
            {
                if (SetProperty(ref _minErrorRate, value))
                {
                    ApplyErrorRateFilter();
                }
            }
        }

        public double? MaxErrorRate
        {
            get => _maxErrorRate;
            set
            {
                if (SetProperty(ref _maxErrorRate, value))
                {
                    ApplyErrorRateFilter();
                }
            }
        }

        public DateTimeOffset? StartedFrom
        {
            get => _startedFrom;
            set
            {
                if (SetProperty(ref _startedFrom, value))
                {
                    ApplyStartedFilter();
                }
            }
        }

        public DateTimeOffset? StartedTo
        {
            get => _startedTo;
            set
            {
                if (SetProperty(ref _startedTo, value))
                {
                    ApplyStartedFilter();
                }
            }
        }

        public RelayCommand ApplyPresetCommand { get; }

        public RelayCommand ClearFiltersCommand { get; }

        public RelayCommand RemoveChipCommand { get; }

        public RelayCommand ApplyServiceFilterCommand { get; }

        public RelayCommand ClearServiceFilterCommand { get; }

        public RelayCommand ApplyStatusFilterCommand { get; }

        public RelayCommand ClearStatusFilterCommand { get; }

        public RelayCommand ApplyRegionFilterCommand { get; }

        public RelayCommand ClearRegionFilterCommand { get; }

        public RelayCommand ApplyErrorFilterCommand { get; }

        public RelayCommand ClearErrorFilterCommand { get; }

        public RelayCommand ApplyStartedFilterCommand { get; }

        public RelayCommand ClearStartedFilterCommand { get; }

        private void ApplyPreset()
        {
            FilteringModel.Apply(new[]
            {
                new FilteringDescriptor("Status", FilteringOperator.Equals, "Status", "Rolling Out"),
                new FilteringDescriptor("Region", FilteringOperator.In, "Region", values: new object[] { "us-east", "us-west" }),
                new FilteringDescriptor("ErrorRate", FilteringOperator.LessThan, "ErrorRate", 0.02)
            });
        }

        private void ApplyServiceFilter(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                FilteringModel.Remove("Service");
                return;
            }

            var descriptor = new FilteringDescriptor("Service", FilteringOperator.Contains, "Service", text.Trim());
            FilteringModel.SetOrUpdate(descriptor);
        }

        private void ApplyStatusFilter()
        {
            var selected = StatusOptions.Where(s => s.IsSelected).Select(s => (object?)s.Value).ToArray();
            if (selected.Length == 0)
            {
                FilteringModel.Remove("Status");
                return;
            }

            var descriptor = new FilteringDescriptor("Status", FilteringOperator.In, "Status", values: selected);
            FilteringModel.SetOrUpdate(descriptor);
        }

        private void ApplyRegionFilter(string? region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                FilteringModel.Remove("Region");
                return;
            }

            var descriptor = new FilteringDescriptor("Region", FilteringOperator.Equals, "Region", region);
            FilteringModel.SetOrUpdate(descriptor);
        }

        private void ApplyErrorRateFilter()
        {
            if (_minErrorRate == null && _maxErrorRate == null)
            {
                FilteringModel.Remove("ErrorRate");
                return;
            }

            if (_minErrorRate != null && _maxErrorRate != null)
            {
                var descriptor = new FilteringDescriptor(
                    "ErrorRate",
                    FilteringOperator.Between,
                    "ErrorRate",
                    values: new object[] { _minErrorRate!.Value, _maxErrorRate!.Value });
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            if (_minErrorRate != null)
            {
                var descriptor = new FilteringDescriptor(
                    "ErrorRate",
                    FilteringOperator.GreaterThanOrEqual,
                    "ErrorRate",
                    _minErrorRate.Value);
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            var maxDescriptor = new FilteringDescriptor(
                "ErrorRate",
                FilteringOperator.LessThanOrEqual,
                "ErrorRate",
                _maxErrorRate);
            FilteringModel.SetOrUpdate(maxDescriptor);
        }

        private void ApplyStartedFilter()
        {
            if (_startedFrom == null && _startedTo == null)
            {
                FilteringModel.Remove("Started");
                return;
            }

            if (_startedFrom != null && _startedTo != null)
            {
                var descriptor = new FilteringDescriptor(
                    "Started",
                    FilteringOperator.Between,
                    "Started",
                    values: new object[] { _startedFrom.Value, _startedTo.Value });
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            if (_startedFrom != null)
            {
                var descriptor = new FilteringDescriptor(
                    "Started",
                    FilteringOperator.GreaterThanOrEqual,
                    "Started",
                    _startedFrom.Value);
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            var maxDescriptor = new FilteringDescriptor(
                "Started",
                FilteringOperator.LessThanOrEqual,
                "Started",
                _startedTo);
            FilteringModel.SetOrUpdate(maxDescriptor);
        }

        private void ClearStatusFilter()
        {
            foreach (var status in StatusOptions)
            {
                status.IsSelected = false;
            }
            FilteringModel.Remove("Status");
        }

        private void ClearErrorFilter()
        {
            MinErrorRate = null;
            MaxErrorRate = null;
            FilteringModel.Remove("ErrorRate");
        }

        private void ClearStartedFilter()
        {
            StartedFrom = null;
            StartedTo = null;
            FilteringModel.Remove("Started");
        }

        private void FilteringModelOnFilteringChanged(object? sender, FilteringChangedEventArgs e)
        {
            UpdateDescriptorSummaries(e.NewDescriptors);
            UpdateChips(e.NewDescriptors);
            _adapterFactory.UpdateFilter(e.NewDescriptors);
            _filterSubject.OnNext(_adapterFactory.FilterPredicate);
        }

        private void UpdateDescriptorSummaries(IReadOnlyList<FilteringDescriptor> descriptors)
        {
            DescriptorSummaries.Clear();
            if (descriptors == null)
            {
                return;
            }

            foreach (var descriptor in descriptors.Where(d => d != null))
            {
                DescriptorSummaries.Add(new FilterDescriptorSummary(
                    descriptor.PropertyPath ?? descriptor.ColumnId?.ToString() ?? "(unknown)",
                    descriptor.Operator.ToString(),
                    descriptor.Values != null ? string.Join(", ", descriptor.Values) : descriptor.Value?.ToString() ?? "(null)"));
            }
        }

        private void UpdateChips(IReadOnlyList<FilteringDescriptor> descriptors)
        {
            FilterChips.Clear();
            if (descriptors == null)
            {
                return;
            }

            foreach (var descriptor in descriptors.Where(d => d != null))
            {
                var label = descriptor.PropertyPath ?? descriptor.ColumnId?.ToString() ?? "(unknown)";
                var value = descriptor.Values != null ? string.Join(", ", descriptor.Values) : descriptor.Value?.ToString() ?? descriptor.Operator.ToString();
                FilterChips.Add(new FilterChip(descriptor.ColumnId!, label, $"{descriptor.Operator}: {value}"));
            }
        }

        private void OnUpstreamFiltersChanged(string description)
        {
            UpstreamFilters.Insert(0, $"{DateTime.Now:HH:mm:ss} {description}");
            while (UpstreamFilters.Count > 20)
            {
                UpstreamFilters.RemoveAt(UpstreamFilters.Count - 1);
            }
        }

        public void Dispose()
        {
            _filterSubject.Dispose();
            _cleanup.Dispose();
            foreach (var status in StatusOptions)
            {
                status.Changed -= OnStatusChanged;
            }
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public record FilterDescriptorSummary(string Column, string Operator, string Value);

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public record FilterChip(object ColumnId, string Column, string Value);

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public sealed class SelectableFilterValue : ObservableObject
        {
            private bool _isSelected;

            public SelectableFilterValue(string value)
            {
                Value = value;
            }

            public string Value { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (SetProperty(ref _isSelected, value))
                    {
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            public event EventHandler? Changed;
        }

        private void OnStatusChanged(object? sender, EventArgs e)
        {
            ApplyStatusFilter();
        }

        private void RemoveDescriptor(object? columnId)
        {
            if (columnId == null)
            {
                return;
            }

            FilteringModel.Remove(columnId);

            if (Equals(columnId, "Service"))
            {
                ServiceFilter = string.Empty;
            }
            else if (Equals(columnId, "Status"))
            {
                ClearStatusFilter();
            }
            else if (Equals(columnId, "Region"))
            {
                SelectedRegion = null;
            }
            else if (Equals(columnId, "ErrorRate"))
            {
                ClearErrorFilter();
            }
            else if (Equals(columnId, "Started"))
            {
                ClearStartedFilter();
            }
        }
    }
}
