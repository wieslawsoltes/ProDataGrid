using System;
using System.Collections.Generic;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Threading;
using DataGridSample.Collections;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalStreamingUpdatesViewModel : ObservableObject
    {
        public class TreeItem
        {
            public TreeItem(int id, string name, double price, DateTime updatedAt, bool isExpanded)
            {
                Id = id;
                Name = name;
                Price = price;
                UpdatedAt = updatedAt;
                PriceDisplay = price.ToString("F2");
                UpdatedAtDisplay = updatedAt.ToString("T");
                IsExpanded = isExpanded;
                Children = new ObservableRangeCollection<TreeItem>();
            }

            public int Id { get; }

            public string Name { get; }

            public double Price { get; }

            public DateTime UpdatedAt { get; }

            public string PriceDisplay { get; }

            public string UpdatedAtDisplay { get; }

            public bool IsExpanded { get; set; }

            public ObservableRangeCollection<TreeItem> Children { get; }
        }

        private readonly DispatcherTimer _timer;
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

        public HierarchicalStreamingUpdatesViewModel()
        {
            RootItems = new ObservableRangeCollection<TreeItem>();

            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedSetter = (item, value) => item.IsExpanded = value
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoots(RootItems);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_intervalMs)
            };
            _timer.Tick += (_, __) => ApplyUpdates();

            StartCommand = new RelayCommand(_ => Start(), _ => !IsRunning);
            StopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
            ResetCommand = new RelayCommand(_ => ResetItems());

            RootItems.CollectionChanged += (_, __) => UpdateCounts();
            Model.FlattenedChanged += (_, __) => UpdateCounts();

            ResetItems();
        }

        public ObservableRangeCollection<TreeItem> RootItems { get; }

        public HierarchicalModel<TreeItem> Model { get; }

        public RelayCommand StartCommand { get; }

        public RelayCommand StopCommand { get; }

        public RelayCommand ResetCommand { get; }

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
            Updates = 0;
            _nextId = 0;

            var items = new List<TreeItem>(_targetRootCount);
            for (var i = 0; i < _targetRootCount; i++)
            {
                items.Add(CreateRoot());
            }

            RootItems.ResetWith(items);
            UpdateCounts();
        }

        private void ApplyUpdates()
        {
            var batch = Math.Max(1, _batchSize);
            var newItems = new List<TreeItem>(batch);
            for (var i = 0; i < batch; i++)
            {
                newItems.Add(CreateRoot());
            }

            RootItems.AddRange(newItems);

            var removeCount = RootItems.Count - _targetRootCount;
            if (removeCount > 0)
            {
                RootItems.RemoveRange(0, removeCount);
            }

            Updates += batch;
            UpdateCounts();
        }

        private TreeItem CreateRoot()
        {
            var id = NextId();
            var root = CreateNode(id, $"Root {id}", isExpanded: true);

            if (_childrenPerRoot > 0)
            {
                var children = new List<TreeItem>(_childrenPerRoot);
                for (var i = 0; i < _childrenPerRoot; i++)
                {
                    children.Add(CreateChild(id, i + 1));
                }

                root.Children.AddRange(children);
            }

            return root;
        }

        private TreeItem CreateChild(int rootId, int index)
        {
            var id = NextId();
            return CreateNode(id, $"Item {rootId}-{index}", isExpanded: false);
        }

        private TreeItem CreateNode(int id, string name, bool isExpanded)
        {
            var price = Math.Round(_random.NextDouble() * 1000, 2);
            var updatedAt = DateTime.Now;
            return new TreeItem(id, name, price, updatedAt, isExpanded);
        }

        private int NextId()
        {
            return ++_nextId;
        }

        private void UpdateCounts()
        {
            RootCount = RootItems.Count;
            VisibleCount = Model.Count;
        }
    }
}
