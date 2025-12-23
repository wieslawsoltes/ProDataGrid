// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalSelectionViewModel : ObservableObject
    {
        public sealed class TreeItem
        {
            public TreeItem(int id, string name, TreeItem? parent = null, ObservableCollection<TreeItem>? children = null)
            {
                Id = id;
                Name = name;
                Parent = parent;
                Children = children ?? new ObservableCollection<TreeItem>();
            }

            public int Id { get; }

            public string Name { get; }

            public TreeItem? Parent { get; }

            public ObservableCollection<TreeItem> Children { get; }
        }

        private TreeItem? _selectedItem;
        private string _selectedLabel = "None";
        private int _visibleCount;
        private int _totalCount;
        private readonly TreeItem? _deepItem;

        public HierarchicalSelectionViewModel()
        {
            Roots = BuildSample();
            _deepItem = FindDeepItem();

            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 0,
                VirtualizeChildren = false,
                ItemPathSelector = BuildPath
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoots(Roots);
            Model.FlattenedChanged += (_, __) => UpdateCounts();

            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
            RefreshCommand = new RelayCommand(_ => Model.Refresh());
            RebuildCommand = new RelayCommand(_ => Model.SetRoots(Roots));
            SelectFirstVisibleCommand = new RelayCommand(_ => SelectVisible(isFirst: true));
            SelectLastVisibleCommand = new RelayCommand(_ => SelectVisible(isFirst: false));
            SelectDeepItemCommand = new RelayCommand(_ => SelectedItem = _deepItem);
            ClearSelectionCommand = new RelayCommand(_ => SelectedItem = null);

            UpdateCounts();
        }

        public ObservableCollection<TreeItem> Roots { get; }

        public HierarchicalModel<TreeItem> Model { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand RebuildCommand { get; }

        public RelayCommand SelectFirstVisibleCommand { get; }

        public RelayCommand SelectLastVisibleCommand { get; }

        public RelayCommand SelectDeepItemCommand { get; }

        public RelayCommand ClearSelectionCommand { get; }

        public TreeItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    SelectedLabel = value == null ? "None" : $"{value.Name} (#{value.Id})";
                }
            }
        }

        public string SelectedLabel
        {
            get => _selectedLabel;
            private set => SetProperty(ref _selectedLabel, value);
        }

        public int VisibleCount
        {
            get => _visibleCount;
            private set => SetProperty(ref _visibleCount, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            private set => SetProperty(ref _totalCount, value);
        }

        private void SelectVisible(bool isFirst)
        {
            if (Model.Count == 0)
            {
                SelectedItem = null;
                return;
            }

            var index = isFirst ? 0 : Model.Count - 1;
            SelectedItem = Model.Flattened[index].Item as TreeItem;
        }

        private TreeItem? FindDeepItem()
        {
            foreach (var root in Roots)
            {
                foreach (var child in root.Children)
                {
                    if (child.Children.Count > 0)
                    {
                        return child.Children[0];
                    }
                }
            }

            return null;
        }

        private IReadOnlyList<int>? BuildPath(TreeItem item)
        {
            var segments = new List<int>();
            var current = item;

            while (current != null)
            {
                if (current.Parent == null)
                {
                    var rootIndex = Roots.IndexOf(current);
                    if (rootIndex < 0)
                    {
                        return null;
                    }

                    segments.Add(rootIndex);
                    break;
                }

                var parent = current.Parent;
                var index = parent.Children.IndexOf(current);
                if (index < 0)
                {
                    return null;
                }

                segments.Add(index);
                current = parent;
            }

            segments.Reverse();
            return segments;
        }

        private void UpdateCounts()
        {
            VisibleCount = Model.Count;
            TotalCount = CountItems(Roots);
        }

        private static int CountItems(IEnumerable<TreeItem> items)
        {
            var count = 0;
            foreach (var item in items)
            {
                count++;
                if (item.Children.Count > 0)
                {
                    count += CountItems(item.Children);
                }
            }
            return count;
        }

        private static ObservableCollection<TreeItem> BuildSample()
        {
            var roots = new ObservableCollection<TreeItem>();
            var id = 1;

            for (int group = 1; group <= 3; group++)
            {
                var root = new TreeItem(id++, $"Group {group}");
                for (int child = 1; child <= 4; child++)
                {
                    var childNode = new TreeItem(id++, $"Item {group}.{child}", root);
                    if (child % 2 == 0)
                    {
                        for (int leaf = 1; leaf <= 3; leaf++)
                        {
                            childNode.Children.Add(new TreeItem(id++, $"Item {group}.{child}.{leaf}", childNode));
                        }
                    }
                    root.Children.Add(childNode);
                }
                roots.Add(root);
            }

            return roots;
        }
    }
}
