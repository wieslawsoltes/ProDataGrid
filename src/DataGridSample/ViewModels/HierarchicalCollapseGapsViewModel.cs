using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalCollapseGapsViewModel : ObservableObject
    {
        public sealed class TreeItem
        {
            public TreeItem(string name, double rowHeight)
            {
                Name = name;
                RowHeight = rowHeight;
                Children = new ObservableCollection<TreeItem>();
            }

            public string Name { get; }

            public double RowHeight { get; }

            public bool IsExpanded { get; set; }

            public ObservableCollection<TreeItem> Children { get; }
        }

        private readonly Random _random = new Random(20260115);
        private int _visibleCount;
        private int _selectedCount;

        public HierarchicalCollapseGapsViewModel()
        {
            Roots = new ObservableCollection<TreeItem>();

            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                IsExpandedSelector = item => item.IsExpanded,
                IsExpandedSetter = (item, value) => item.IsExpanded = value,
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 2,
                VirtualizeChildren = true
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoots(Roots);
            Model.FlattenedChanged += (_, __) => UpdateCounts();

            SelectionModel = new SelectionModel<HierarchicalNode> { SingleSelect = false };
            SelectionModel.SelectionChanged += (_, __) => UpdateCounts();

            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
            ResetCommand = new RelayCommand(_ => ResetItems());

            ResetItems();
        }

        public ObservableCollection<TreeItem> Roots { get; }

        public HierarchicalModel<TreeItem> Model { get; }

        public SelectionModel<HierarchicalNode> SelectionModel { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand ResetCommand { get; }

        public int VisibleCount
        {
            get => _visibleCount;
            private set => SetProperty(ref _visibleCount, value);
        }

        public int SelectedCount
        {
            get => _selectedCount;
            private set => SetProperty(ref _selectedCount, value);
        }

        private void ResetItems()
        {
            Roots.Clear();
            var roots = BuildTree(rootCount: 4, childCount: 70, grandchildCount: 4);
            foreach (var root in roots)
            {
                Roots.Add(root);
            }

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            VisibleCount = Model.Count;
            SelectedCount = SelectionModel.SelectedItems.Count;
        }

        private ObservableCollection<TreeItem> BuildTree(int rootCount, int childCount, int grandchildCount)
        {
            var roots = new ObservableCollection<TreeItem>();
            for (var r = 0; r < rootCount; r++)
            {
                var root = new TreeItem($"Root {r + 1}", NextHeight(24, 36))
                {
                    IsExpanded = true
                };
                for (var c = 0; c < childCount; c++)
                {
                    var child = new TreeItem($"Root {r + 1}.{c + 1}", NextHeight(24, 32))
                    {
                        IsExpanded = true
                    };
                    for (var g = 0; g < grandchildCount; g++)
                    {
                        child.Children.Add(new TreeItem($"Root {r + 1}.{c + 1}.{g + 1}", NextHeight(24, 30)));
                    }
                    root.Children.Add(child);
                }
                roots.Add(root);
            }
            return roots;
        }

        private double NextHeight(int min, int max)
        {
            return _random.Next(min, max + 1);
        }
    }
}
