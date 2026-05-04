using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.DataGridSelection;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using DataGridSample.Adapters;
using DataGridSample.Models;
using DataGridSample.Mvvm;
using DynamicData;

namespace DataGridSample.ViewModels
{
    public class DynamicDataStreamingSourceCacheViewModel : ObservableObject, IDisposable
    {
        private readonly SourceCache<StreamingItem, int> _source;
        private readonly Queue<int> _idQueue = new();
        private readonly ReadOnlyObservableCollection<StreamingItem> _view;
        private readonly CompositeDisposable _cleanup = new();
        private readonly BehaviorSubject<IComparer<StreamingItem>> _sortSubject;
        private readonly BehaviorSubject<Func<StreamingItem, bool>> _filterSubject;
        private readonly DynamicDataStreamingSortingAdapterFactory _sortingAdapterFactory;
        private readonly DynamicDataStreamingFilteringAdapterFactory _filteringAdapterFactory;
        private readonly INotifyCollectionChanged _viewNotifications;
        private readonly DispatcherTimer _timer;
        private readonly Random _random = new Random();
        private int _targetCount = 10000;
        private int _batchSize = 50;
        private int _intervalMs = 33;
        private bool _isRunning;
        private long _updates;
        private int _nextId;
        private string? _symbolFilter;
        private double? _minPrice;
        private double? _maxPrice;
        private ISortingModel? _sortingModel;
        private IFilteringModel? _filteringModel;
        private bool _multiSortEnabled = true;
        private SortCycleMode _sortCycleMode = SortCycleMode.AscendingDescendingNone;
        private int? _expectedSelectedId;
        private string _selectionStatus = "No selection.";

        public DynamicDataStreamingSourceCacheViewModel()
        {
            _source = new SourceCache<StreamingItem, int>(item => item.Id);
            _sortingAdapterFactory = new DynamicDataStreamingSortingAdapterFactory(OnUpstreamSortsChanged);
            _filteringAdapterFactory = new DynamicDataStreamingFilteringAdapterFactory(OnUpstreamFiltersChanged);
            _sortSubject = new BehaviorSubject<IComparer<StreamingItem>>(_sortingAdapterFactory.SortComparer);
            _filterSubject = new BehaviorSubject<Func<StreamingItem, bool>>(_filteringAdapterFactory.FilterPredicate);

            var subscription = _source.Connect()
                .Filter(_filterSubject)
                .SortAndBind(out _view, _sortSubject, new()
                {
                    UseReplaceForUpdates = true
                })
                .Subscribe();
            _cleanup.Add(subscription);

            _viewNotifications = _view;
            _viewNotifications.CollectionChanged += ViewCollectionChanged;

            SelectionModel = new IdentitySelectionModel(item => item is StreamingItem stream ? stream.Id : item)
            {
                SingleSelect = true
            };
            SelectionModel.SelectionChanged += SelectionModelOnSelectionChanged;

            SortingModel = new SortingModel
            {
                MultiSort = true,
                CycleMode = SortCycleMode.AscendingDescendingNone,
                OwnsViewSorts = true
            };
            SortingModel.SortingChanged += SortingModelOnSortingChanged;

            FilteringModel = new FilteringModel
            {
                OwnsViewFilter = true
            };
            FilteringModel.FilteringChanged += FilteringModelOnFilteringChanged;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_intervalMs)
            };
            _timer.Tick += (_, __) => ApplyUpdates();

            StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
            StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
            ResetCommand = new RelayCommand(_ => ResetItems());
            ClearSortsCommand = new RelayCommand(_ => SortingModel.Clear());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            LoadSelectionReproCommand = new RelayCommand(_ => LoadSelectionRepro());
            SelectMiddleRowCommand = new RelayCommand(_ => SelectMiddleRow());
            ReplaceSelectedAndMoveCommand = new RelayCommand(_ => ReplaceSelectedAndMove());
            RunSelectionDriftReproCommand = new RelayCommand(_ => RunSelectionDriftRepro());

            ResetItems();
        }

        public ReadOnlyObservableCollection<StreamingItem> View => _view;

        public DynamicDataStreamingSortingAdapterFactory SortingAdapterFactory => _sortingAdapterFactory;

        public DynamicDataStreamingFilteringAdapterFactory FilteringAdapterFactory => _filteringAdapterFactory;

        public ISelectionModel SelectionModel { get; }

        public ISortingModel SortingModel
        {
            get => _sortingModel!;
            private set => SetProperty(ref _sortingModel, value);
        }

        public IFilteringModel FilteringModel
        {
            get => _filteringModel!;
            private set => SetProperty(ref _filteringModel, value);
        }

        public ObservableCollection<SortDescriptorSummary> SortSummaries { get; } = new();

        public ObservableCollection<FilterDescriptorSummary> FilterSummaries { get; } = new();

        public ObservableCollection<string> UpstreamSorts { get; } = new();

        public ObservableCollection<string> UpstreamFilters { get; } = new();

        public RelayCommand StartCommand { get; }

        public RelayCommand StopCommand { get; }

        public RelayCommand ResetCommand { get; }

        public RelayCommand ClearSortsCommand { get; }

        public RelayCommand ClearFiltersCommand { get; }

        public RelayCommand LoadSelectionReproCommand { get; }

        public RelayCommand SelectMiddleRowCommand { get; }

        public RelayCommand ReplaceSelectedAndMoveCommand { get; }

        public RelayCommand RunSelectionDriftReproCommand { get; }

        public int TargetCount
        {
            get => _targetCount;
            set
            {
                var next = Math.Max(0, value);
                if (SetProperty(ref _targetCount, next) && !IsRunning)
                {
                    ResetItems();
                }
            }
        }

        public int BatchSize
        {
            get => _batchSize;
            set => SetProperty(ref _batchSize, Math.Max(1, value));
        }

        public int IntervalMs
        {
            get => _intervalMs;
            set
            {
                var next = Math.Max(1, value);
                if (SetProperty(ref _intervalMs, next) && _timer.IsEnabled)
                {
                    _timer.Interval = TimeSpan.FromMilliseconds(_intervalMs);
                }
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    StartCommand.RaiseCanExecuteChanged();
                    StopCommand.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(RunState));
                }
            }
        }

        public long Updates
        {
            get => _updates;
            private set => SetProperty(ref _updates, value);
        }

        public int ItemsCount => _view.Count;

        public string RunState => IsRunning ? "Running" : "Stopped";

        public int? ExpectedSelectedId
        {
            get => _expectedSelectedId;
            private set => SetProperty(ref _expectedSelectedId, value);
        }

        public int? SelectedId => SelectionModel.SelectedItem is StreamingItem item ? item.Id : null;

        public string SelectionStatus
        {
            get => _selectionStatus;
            private set => SetProperty(ref _selectionStatus, value);
        }

        public string? SymbolFilter
        {
            get => _symbolFilter;
            set
            {
                if (SetProperty(ref _symbolFilter, value))
                {
                    ApplySymbolFilter(value);
                }
            }
        }

        public double? MinPrice
        {
            get => _minPrice;
            set
            {
                if (SetProperty(ref _minPrice, value))
                {
                    ApplyPriceFilter();
                }
            }
        }

        public double? MaxPrice
        {
            get => _maxPrice;
            set
            {
                if (SetProperty(ref _maxPrice, value))
                {
                    ApplyPriceFilter();
                }
            }
        }

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

        private void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _timer.Interval = TimeSpan.FromMilliseconds(_intervalMs);
            _timer.Start();
            IsRunning = true;
        }

        private void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _timer.Stop();
            IsRunning = false;
        }

        private void ResetItems()
        {
            _source.Edit(cache =>
            {
                cache.Clear();
                _idQueue.Clear();
                _nextId = 0;
                for (var i = 0; i < _targetCount; i++)
                {
                    var item = CreateItem();
                    cache.AddOrUpdate(item);
                    _idQueue.Enqueue(item.Id);
                }
            });

            Updates = 0;
            ExpectedSelectedId = null;
            UpdateSelectionStatus();
        }

        private void ApplyUpdates()
        {
            var batch = Math.Max(1, _batchSize);
            var count = _source.Count;

            _source.Edit(cache =>
            {
                for (var i = 0; i < batch; i++)
                {
                    var item = CreateItem();
                    cache.AddOrUpdate(item);
                    _idQueue.Enqueue(item.Id);
                    count++;
                }

                while (count > _targetCount && _idQueue.Count > 0)
                {
                    var removeId = _idQueue.Dequeue();
                    cache.RemoveKey(removeId);
                    count--;
                }
            });

            Updates += batch;
        }

        private StreamingItem CreateItem()
        {
            var id = ++_nextId;
            var price = Math.Round(_random.NextDouble() * 1000, 2);
            var updatedAt = DateTime.Now;
            return new StreamingItem
            {
                Id = id,
                Symbol = $"SYM{id % 1000:D3}",
                Price = price,
                UpdatedAt = updatedAt,
                PriceDisplay = price.ToString("F2"),
                UpdatedAtDisplay = updatedAt.ToString("T")
            };
        }

        private void ApplySymbolFilter(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                FilteringModel.Remove(nameof(StreamingItem.Symbol));
                return;
            }

            var descriptor = new FilteringDescriptor(
                nameof(StreamingItem.Symbol),
                FilteringOperator.Contains,
                nameof(StreamingItem.Symbol),
                text.Trim());
            FilteringModel.SetOrUpdate(descriptor);
        }

        private void ApplyPriceFilter()
        {
            var minPrice = _minPrice;
            var maxPrice = _maxPrice;

            if (minPrice == null && maxPrice == null)
            {
                FilteringModel.Remove(nameof(StreamingItem.Price));
                return;
            }

            if (minPrice != null && maxPrice != null)
            {
                var descriptor = new FilteringDescriptor(
                    nameof(StreamingItem.Price),
                    FilteringOperator.Between,
                    nameof(StreamingItem.Price),
                    values: new object[] { minPrice.Value, maxPrice.Value });
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            if (minPrice != null)
            {
                var descriptor = new FilteringDescriptor(
                    nameof(StreamingItem.Price),
                    FilteringOperator.GreaterThanOrEqual,
                    nameof(StreamingItem.Price),
                    minPrice.Value);
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            if (maxPrice == null)
            {
                FilteringModel.Remove(nameof(StreamingItem.Price));
                return;
            }

            var maxDescriptor = new FilteringDescriptor(
                nameof(StreamingItem.Price),
                FilteringOperator.LessThanOrEqual,
                nameof(StreamingItem.Price),
                maxPrice.Value);
            FilteringModel.SetOrUpdate(maxDescriptor);
        }

        private void ClearFilters()
        {
            SymbolFilter = string.Empty;
            MinPrice = null;
            MaxPrice = null;
            FilteringModel.Clear();
        }

        private void LoadSelectionRepro()
        {
            Stop();
            SymbolFilter = string.Empty;
            MinPrice = null;
            MaxPrice = null;
            FilteringModel.Clear();
            SortingModel.Apply(new[]
            {
                new SortingDescriptor(nameof(StreamingItem.Price), ListSortDirection.Ascending, nameof(StreamingItem.Price))
            });

            _source.Edit(cache =>
            {
                cache.Clear();
                cache.AddOrUpdate(new StreamingItem { Id = 1, Symbol = "AAA", Price = 10, PriceDisplay = "10.00", UpdatedAt = DateTime.Today, UpdatedAtDisplay = "10:00:00" });
                cache.AddOrUpdate(new StreamingItem { Id = 2, Symbol = "BBB", Price = 20, PriceDisplay = "20.00", UpdatedAt = DateTime.Today, UpdatedAtDisplay = "10:00:01" });
                cache.AddOrUpdate(new StreamingItem { Id = 3, Symbol = "CCC", Price = 30, PriceDisplay = "30.00", UpdatedAt = DateTime.Today, UpdatedAtDisplay = "10:00:02" });
            });

            SelectionModel.Clear();
            ExpectedSelectedId = null;
            Updates = 0;
            UpdateSelectionStatus();
        }

        private void SelectMiddleRow()
        {
            if (_view.Count < 2)
            {
                return;
            }

            SelectionModel.Select(1);
            ExpectedSelectedId = SelectedId;
            UpdateSelectionStatus();
        }

        private void ReplaceSelectedAndMove()
        {
            if (SelectionModel.SelectedItem is not StreamingItem selected)
            {
                return;
            }

            ExpectedSelectedId = selected.Id;

            var replacement = new StreamingItem
            {
                Id = selected.Id,
                Symbol = selected.Symbol,
                Price = 999,
                PriceDisplay = "999.00",
                UpdatedAt = DateTime.Today.AddSeconds(30),
                UpdatedAtDisplay = "10:00:30"
            };

            _source.AddOrUpdate(replacement);
            UpdateSelectionStatus();
        }

        private void RunSelectionDriftRepro()
        {
            LoadSelectionRepro();
            SelectMiddleRow();
            ReplaceSelectedAndMove();
        }

        private void SortingModelOnSortingChanged(object? sender, SortingChangedEventArgs e)
        {
            UpdateSortSummaries(e.NewDescriptors);
            _sortingAdapterFactory.UpdateComparer(e.NewDescriptors);
            _sortSubject.OnNext(_sortingAdapterFactory.SortComparer);
        }

        private void FilteringModelOnFilteringChanged(object? sender, FilteringChangedEventArgs e)
        {
            UpdateFilterSummaries(e.NewDescriptors);
            _filteringAdapterFactory.UpdateFilter(e.NewDescriptors);
            _filterSubject.OnNext(_filteringAdapterFactory.FilterPredicate);
        }

        private void UpdateSortSummaries(IReadOnlyList<SortingDescriptor> descriptors)
        {
            SortSummaries.Clear();
            if (descriptors == null)
            {
                return;
            }

            foreach (var descriptor in descriptors)
            {
                SortSummaries.Add(new SortDescriptorSummary(
                    descriptor.PropertyPath ?? descriptor.ColumnId?.ToString() ?? "(unknown)",
                    descriptor.Direction.ToString()));
            }
        }

        private void UpdateFilterSummaries(IReadOnlyList<FilteringDescriptor> descriptors)
        {
            FilterSummaries.Clear();
            if (descriptors == null)
            {
                return;
            }

            foreach (var descriptor in descriptors.Where(d => d != null))
            {
                FilterSummaries.Add(new FilterDescriptorSummary(
                    descriptor.PropertyPath ?? descriptor.ColumnId?.ToString() ?? "(unknown)",
                    descriptor.Operator.ToString(),
                    descriptor.Values != null ? string.Join(", ", descriptor.Values) : descriptor.Value?.ToString() ?? "(null)"));
            }
        }

        private void OnUpstreamSortsChanged(string description)
        {
            UpstreamSorts.Insert(0, $"{DateTime.Now:HH:mm:ss} {description}");
            while (UpstreamSorts.Count > 12)
            {
                UpstreamSorts.RemoveAt(UpstreamSorts.Count - 1);
            }
        }

        private void OnUpstreamFiltersChanged(string description)
        {
            UpstreamFilters.Insert(0, $"{DateTime.Now:HH:mm:ss} {description}");
            while (UpstreamFilters.Count > 12)
            {
                UpstreamFilters.RemoveAt(UpstreamFilters.Count - 1);
            }
        }

        public void Dispose()
        {
            Stop();
            SortingModel.SortingChanged -= SortingModelOnSortingChanged;
            FilteringModel.FilteringChanged -= FilteringModelOnFilteringChanged;
            SelectionModel.SelectionChanged -= SelectionModelOnSelectionChanged;
            _viewNotifications.CollectionChanged -= ViewCollectionChanged;
            _sortSubject.Dispose();
            _filterSubject.Dispose();
            _cleanup.Dispose();
        }

        public record SortDescriptorSummary(string Column, string Direction);

        public record FilterDescriptorSummary(string Column, string Operator, string Value);

        private void ViewCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ItemsCount));
            OnPropertyChanged(nameof(SelectedId));
            UpdateSelectionStatus();
        }

        private void SelectionModelOnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SelectedId));
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            var selectedId = SelectedId;
            var expectedId = ExpectedSelectedId;

            if (expectedId == null)
            {
                SelectionStatus = selectedId == null
                    ? "No selection."
                    : $"Selected Id {selectedId}.";
                return;
            }

            SelectionStatus = selectedId == expectedId
                ? $"Selection stayed on Id {selectedId}."
                : $"Selection drifted. Expected Id {expectedId}, actual Id {(selectedId?.ToString() ?? "none")}.";
        }
    }
}
