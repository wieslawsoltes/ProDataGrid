using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Threading;
using DataGridSample.Adapters;
using DataGridSample.Collections;
using DataGridSample.Models;
using DataGridSample.Mvvm;
using DynamicData;

namespace DataGridSample.ViewModels
{
    public class DynamicDataHierarchicalSourceCacheViewModel : ObservableObject, IDisposable
    {
        private const string NamePropertyPath = "Item.Name";
        private const string PricePropertyPath = "Item.Price";

        private readonly SourceCache<HierarchicalStreamingItem, int> _source;
        private readonly ReadOnlyObservableCollection<HierarchicalStreamingItem> _roots;
        private readonly BehaviorSubject<IComparer<HierarchicalStreamingItem>> _sortSubject;
        private readonly BehaviorSubject<Func<HierarchicalStreamingItem, bool>> _treeFilterSubject;
        private readonly DynamicDataHierarchicalSortingAdapterFactory _sortingAdapterFactory;
        private readonly DynamicDataHierarchicalFilteringAdapterFactory _filteringAdapterFactory;
        private readonly DynamicDataHierarchicalSearchAdapterFactory _searchAdapterFactory;
        private readonly INotifyCollectionChanged _rootNotifications;
        private readonly Dictionary<HierarchicalStreamingItem, FilteredObservableRangeCollection<HierarchicalStreamingItem>> _filteredChildren = new();
        private Func<HierarchicalStreamingItem, bool> _treePredicate = static _ => true;
        private bool _treeFilterActive;
        private readonly DispatcherTimer _timer;
        private readonly Queue<int> _idQueue = new Queue<int>();
        private readonly Random _random = new Random();
        private int _targetRootCount = 2000;
        private int _childrenPerRoot = 3;
        private int _batchSize = 20;
        private int _intervalMs = 50;
        private bool _isRunning;
        private long _updates;
        private int _nextId;
        private int _rootCount;
        private int _visibleCount;
        private string? _nameFilter;
        private double? _minPrice;
        private double? _maxPrice;
        private string _query = string.Empty;
        private string _resultSummary = "No results";
        private ISortingModel? _sortingModel;
        private IFilteringModel? _filteringModel;
        private ISearchModel? _searchModel;
        private bool _multiSortEnabled = true;
        private SortCycleMode _sortCycleMode = SortCycleMode.AscendingDescendingNone;
        private readonly IDisposable _subscription;

        public DynamicDataHierarchicalSourceCacheViewModel()
        {
            _source = new SourceCache<HierarchicalStreamingItem, int>(item => item.Id);
            _sortingAdapterFactory = new DynamicDataHierarchicalSortingAdapterFactory(OnUpstreamSortsChanged);
            _filteringAdapterFactory = new DynamicDataHierarchicalFilteringAdapterFactory(OnUpstreamFiltersChanged);
            _searchAdapterFactory = new DynamicDataHierarchicalSearchAdapterFactory(OnUpstreamSearchChanged);

            _sortSubject = new BehaviorSubject<IComparer<HierarchicalStreamingItem>>(_sortingAdapterFactory.SortComparer);
            _treeFilterSubject = new BehaviorSubject<Func<HierarchicalStreamingItem, bool>>(_treePredicate);

            _subscription = _source.Connect()
                .Filter(_treeFilterSubject)
                .Sort(_sortSubject)
                .Bind(out _roots)
                .Subscribe();

            _rootNotifications = _roots;
            _rootNotifications.CollectionChanged += OnRootCollectionChanged;

            var options = new HierarchicalOptions<HierarchicalStreamingItem>
            {
                ChildrenSelector = FilterChildren,
                IsLeafSelector = IsFilteredLeaf,
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedSetter = (item, value) => item.IsExpanded = value
            };

            Model = new HierarchicalModel<HierarchicalStreamingItem>(options);
            Model.SetRoots(_roots);
            Model.FlattenedChanged += OnModelFlattenedChanged;

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

            SearchModel = new SearchModel
            {
                HighlightMode = SearchHighlightMode.TextAndCell,
                HighlightCurrent = true,
                WrapNavigation = true,
                UpdateSelectionOnNavigate = true
            };
            SearchModel.SearchChanged += SearchModelOnSearchChanged;
            SearchModel.ResultsChanged += SearchModelOnResultsChanged;
            SearchModel.CurrentChanged += SearchModelOnCurrentChanged;

            UpdateTreePredicate();

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
            ClearSearchCommand = new RelayCommand(_ => Query = string.Empty);
            NextCommand = new RelayCommand(_ => SearchModel.MoveNext(), _ => SearchModel.Results.Count > 0);
            PreviousCommand = new RelayCommand(_ => SearchModel.MovePrevious(), _ => SearchModel.Results.Count > 0);

            ResetItems();
        }

        public HierarchicalModel<HierarchicalStreamingItem> Model { get; }

        public DynamicDataHierarchicalSortingAdapterFactory SortingAdapterFactory => _sortingAdapterFactory;

        public DynamicDataHierarchicalFilteringAdapterFactory FilteringAdapterFactory => _filteringAdapterFactory;

        public DynamicDataHierarchicalSearchAdapterFactory SearchAdapterFactory => _searchAdapterFactory;

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

        public ISearchModel SearchModel
        {
            get => _searchModel!;
            private set => SetProperty(ref _searchModel, value);
        }

        public ObservableCollection<SortDescriptorSummary> SortSummaries { get; } = new();

        public ObservableCollection<FilterDescriptorSummary> FilterSummaries { get; } = new();

        public ObservableCollection<string> UpstreamSorts { get; } = new();

        public ObservableCollection<string> UpstreamFilters { get; } = new();

        public ObservableCollection<string> UpstreamSearches { get; } = new();

        public RelayCommand StartCommand { get; }

        public RelayCommand StopCommand { get; }

        public RelayCommand ResetCommand { get; }

        public RelayCommand ClearSortsCommand { get; }

        public RelayCommand ClearFiltersCommand { get; }

        public RelayCommand ClearSearchCommand { get; }

        public RelayCommand NextCommand { get; }

        public RelayCommand PreviousCommand { get; }

        public int TargetRootCount
        {
            get => _targetRootCount;
            set
            {
                var next = Math.Max(0, value);
                if (SetProperty(ref _targetRootCount, next) && !IsRunning)
                {
                    ResetItems();
                }
            }
        }

        public int ChildrenPerRoot
        {
            get => _childrenPerRoot;
            set
            {
                var next = Math.Max(0, value);
                if (SetProperty(ref _childrenPerRoot, next) && !IsRunning)
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

        public int RootCount
        {
            get => _rootCount;
            private set => SetProperty(ref _rootCount, value);
        }

        public int VisibleCount
        {
            get => _visibleCount;
            private set => SetProperty(ref _visibleCount, value);
        }

        public string RunState => IsRunning ? "Running" : "Stopped";

        public string? NameFilter
        {
            get => _nameFilter;
            set
            {
                if (SetProperty(ref _nameFilter, value))
                {
                    ApplyNameFilter(value);
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

        public string Query
        {
            get => _query;
            set
            {
                if (SetProperty(ref _query, value))
                {
                    ApplySearch();
                }
            }
        }

        public string ResultSummary
        {
            get => _resultSummary;
            private set => SetProperty(ref _resultSummary, value);
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

                for (var i = 0; i < _targetRootCount; i++)
                {
                    var item = CreateRoot();
                    cache.AddOrUpdate(item);
                    _idQueue.Enqueue(item.Id);
                }
            });

            Updates = 0;
            UpdateCounts();
        }

        private void ApplyUpdates()
        {
            var batch = Math.Max(1, _batchSize);
            var count = _source.Count;

            _source.Edit(cache =>
            {
                for (var i = 0; i < batch; i++)
                {
                    var item = CreateRoot();
                    cache.AddOrUpdate(item);
                    _idQueue.Enqueue(item.Id);
                    count++;
                }

                while (count > _targetRootCount && _idQueue.Count > 0)
                {
                    var removeId = _idQueue.Dequeue();
                    cache.RemoveKey(removeId);
                    count--;
                }
            });

            Updates += batch;
            UpdateCounts();
        }

        private HierarchicalStreamingItem CreateRoot()
        {
            var id = NextId();
            var root = CreateNode(id, $"Root {id}", isExpanded: true);

            if (_childrenPerRoot > 0)
            {
                var children = new List<HierarchicalStreamingItem>(_childrenPerRoot);
                for (var i = 0; i < _childrenPerRoot; i++)
                {
                    children.Add(CreateChild(id, i + 1));
                }

                root.Children.AddRange(children);
            }

            return root;
        }

        private HierarchicalStreamingItem CreateChild(int rootId, int index)
        {
            var id = NextId();
            return CreateNode(id, $"Item {rootId}-{index}", isExpanded: false);
        }

        private HierarchicalStreamingItem CreateNode(int id, string name, bool isExpanded)
        {
            var price = Math.Round(_random.NextDouble() * 1000, 2);
            var updatedAt = DateTime.Now;
            return new HierarchicalStreamingItem(id, name, price, updatedAt, isExpanded);
        }

        private int NextId()
        {
            return ++_nextId;
        }

        private void ApplyNameFilter(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                FilteringModel.Remove(NamePropertyPath);
                return;
            }

            FilteringModel.SetOrUpdate(new FilteringDescriptor(
                columnId: NamePropertyPath,
                @operator: FilteringOperator.Contains,
                propertyPath: NamePropertyPath,
                value: text.Trim(),
                stringComparison: StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyPriceFilter()
        {
            var minPrice = _minPrice;
            var maxPrice = _maxPrice;

            if (minPrice == null && maxPrice == null)
            {
                FilteringModel.Remove(PricePropertyPath);
                return;
            }

            if (minPrice != null && maxPrice != null)
            {
                var descriptor = new FilteringDescriptor(
                    PricePropertyPath,
                    FilteringOperator.Between,
                    PricePropertyPath,
                    values: new object[] { minPrice.Value, maxPrice.Value });
                FilteringModel.SetOrUpdate(descriptor);
                return;
            }

            if (minPrice != null)
            {
                var minDescriptor = new FilteringDescriptor(
                    PricePropertyPath,
                    FilteringOperator.GreaterThanOrEqual,
                    PricePropertyPath,
                    minPrice.Value);
                FilteringModel.SetOrUpdate(minDescriptor);
                return;
            }

            if (maxPrice == null)
            {
                FilteringModel.Remove(PricePropertyPath);
                return;
            }

            var maxDescriptor = new FilteringDescriptor(
                PricePropertyPath,
                FilteringOperator.LessThanOrEqual,
                PricePropertyPath,
                maxPrice.Value);
            FilteringModel.SetOrUpdate(maxDescriptor);
        }

        private IEnumerable<HierarchicalStreamingItem> FilterChildren(HierarchicalStreamingItem item)
        {
            if (!_treeFilterActive || item.Children.Count == 0)
            {
                return item.Children;
            }

            if (!_filteredChildren.TryGetValue(item, out var filtered))
            {
                filtered = new FilteredObservableRangeCollection<HierarchicalStreamingItem>(item.Children, _treePredicate);
                _filteredChildren[item] = filtered;
            }

            return filtered;
        }

        private bool IsFilteredLeaf(HierarchicalStreamingItem item)
        {
            if (!_treeFilterActive)
            {
                return item.Children.Count == 0;
            }

            if (item.Children.Count == 0)
            {
                return true;
            }

            foreach (var child in item.Children)
            {
                if (_treePredicate(child))
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateFilteredChildrenPredicate()
        {
            foreach (var filtered in _filteredChildren.Values)
            {
                filtered.UpdatePredicate(_treePredicate);
            }
        }

        private void RemoveFilteredChildrenCache(HierarchicalStreamingItem item)
        {
            if (_filteredChildren.TryGetValue(item, out var filtered))
            {
                filtered.Dispose();
                _filteredChildren.Remove(item);
            }

            for (int i = 0; i < item.Children.Count; i++)
            {
                RemoveFilteredChildrenCache(item.Children[i]);
            }
        }

        private void ClearFilteredChildrenCache()
        {
            foreach (var filtered in _filteredChildren.Values)
            {
                filtered.Dispose();
            }

            _filteredChildren.Clear();
        }

        private static bool MatchesAny(HierarchicalStreamingItem root, Func<HierarchicalStreamingItem, bool> predicate)
        {
            if (predicate(root))
            {
                return true;
            }

            if (root.Children.Count == 0)
            {
                return false;
            }

            var stack = new Stack<HierarchicalStreamingItem>(root.Children);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (predicate(current))
                {
                    return true;
                }

                if (current.Children.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < current.Children.Count; i++)
                {
                    stack.Push(current.Children[i]);
                }
            }

            return false;
        }

        private void ClearFilters()
        {
            NameFilter = string.Empty;
            MinPrice = null;
            MaxPrice = null;
            FilteringModel.Clear();
        }

        private void ApplySearch(bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(_query))
            {
                SearchModel.Clear();
                UpdateResultSummary();
                return;
            }

            var descriptor = new SearchDescriptor(
                _query.Trim(),
                matchMode: SearchMatchMode.Contains,
                termMode: SearchTermCombineMode.Any,
                scope: SearchScope.VisibleColumns,
                comparison: StringComparison.OrdinalIgnoreCase);

            if (forceRefresh)
            {
                SearchModel.Clear();
            }

            SearchModel.SetOrUpdate(descriptor);
        }

        private void UpdateCounts()
        {
            RootCount = _roots.Count;
            VisibleCount = Model.Count;
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
            UpdateTreePredicate();
            Model.Refresh();
        }

        private void SearchModelOnSearchChanged(object? sender, SearchChangedEventArgs e)
        {
            _searchAdapterFactory.UpdatePredicate(e.NewDescriptors);
            UpdateTreePredicate();
            Model.Refresh();
        }

        private void SearchModelOnResultsChanged(object? sender, SearchResultsChangedEventArgs e)
        {
            UpdateResultSummary();
        }

        private void SearchModelOnCurrentChanged(object? sender, SearchCurrentChangedEventArgs e)
        {
            UpdateResultSummary();
        }

        private void UpdateTreePredicate()
        {
            _treeFilterActive = (FilteringModel?.Descriptors?.Count ?? 0) > 0 ||
                                (SearchModel?.Descriptors?.Count ?? 0) > 0;

            if (!_treeFilterActive)
            {
                _treePredicate = static _ => true;
                _treeFilterSubject.OnNext(_treePredicate);
                ClearFilteredChildrenCache();
                return;
            }

            var filterPredicate = _filteringAdapterFactory.FilterItemPredicate;
            var searchPredicate = _searchAdapterFactory.SearchItemPredicate;
            Func<HierarchicalStreamingItem, bool> nodePredicate = item => filterPredicate(item) && searchPredicate(item);
            _treePredicate = item => MatchesAny(item, nodePredicate);
            _treeFilterSubject.OnNext(_treePredicate);
            UpdateFilteredChildrenPredicate();
        }

        private void UpdateResultSummary()
        {
            var count = SearchModel.Results.Count;
            var current = SearchModel.CurrentIndex >= 0 ? SearchModel.CurrentIndex + 1 : 0;

            if (count == 0)
            {
                ResultSummary = "No results";
            }
            else if (current == 0)
            {
                ResultSummary = $"{count:n0} results";
            }
            else
            {
                ResultSummary = $"{current:n0} of {count:n0}";
            }

            NextCommand.RaiseCanExecuteChanged();
            PreviousCommand.RaiseCanExecuteChanged();
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

        private void OnUpstreamSearchChanged(string description)
        {
            UpstreamSearches.Insert(0, $"{DateTime.Now:HH:mm:ss} {description}");
            while (UpstreamSearches.Count > 12)
            {
                UpstreamSearches.RemoveAt(UpstreamSearches.Count - 1);
            }
        }

        public void Dispose()
        {
            Stop();
            Model.FlattenedChanged -= OnModelFlattenedChanged;
            _rootNotifications.CollectionChanged -= OnRootCollectionChanged;
            SortingModel.SortingChanged -= SortingModelOnSortingChanged;
            FilteringModel.FilteringChanged -= FilteringModelOnFilteringChanged;
            SearchModel.SearchChanged -= SearchModelOnSearchChanged;
            SearchModel.ResultsChanged -= SearchModelOnResultsChanged;
            SearchModel.CurrentChanged -= SearchModelOnCurrentChanged;
            _sortSubject.Dispose();
            _treeFilterSubject.Dispose();
            _subscription.Dispose();
            ClearFilteredChildrenCache();
        }

        private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ClearFilteredChildrenCache();
            }
            else if (e.OldItems != null)
            {
                foreach (var removed in e.OldItems.OfType<HierarchicalStreamingItem>())
                {
                    RemoveFilteredChildrenCache(removed);
                }
            }

            UpdateCounts();
        }

        private void OnModelFlattenedChanged(object? sender, FlattenedChangedEventArgs e)
            => UpdateCounts();

        public record SortDescriptorSummary(string Column, string Direction);

        public record FilterDescriptorSummary(string Column, string Operator, string Value);
    }
}
