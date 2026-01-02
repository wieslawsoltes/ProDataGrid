// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Selection;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalExpandCollapseItemsViewModel : ObservableObject
    {
        public sealed class TreeItem
        {
            public TreeItem(int id, string name, TreeItem? parent = null)
            {
                Id = id;
                Name = name;
                Parent = parent;
                Children = new ObservableCollection<TreeItem>();
            }

            public int Id { get; }

            public string Name { get; }

            public TreeItem? Parent { get; }

            public ObservableCollection<TreeItem> Children { get; }
        }

        private int _visibleCount;
        private int _selectedCount;

        public HierarchicalExpandCollapseItemsViewModel()
        {
            Roots = BuildSample();

            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                VirtualizeChildren = false
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoots(Roots);
            Model.FlattenedChanged += (_, __) => UpdateVisibleCount();

            SelectionModel = new SelectionModel<object> { SingleSelect = false };
            SelectionModel.SelectionChanged += (_, __) => UpdateSelectedCount();

            ExpandSelectedCommand = new RelayCommand(_ => ExpandSelected());
            CollapseSelectedCommand = new RelayCommand(_ => CollapseSelected());
            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
            ClearSelectionCommand = new RelayCommand(_ => SelectionModel.Clear());

            UpdateVisibleCount();
            UpdateSelectedCount();
        }

        public ObservableCollection<TreeItem> Roots { get; }

        public HierarchicalModel<TreeItem> Model { get; }

        public SelectionModel<object> SelectionModel { get; }

        public RelayCommand ExpandSelectedCommand { get; }

        public RelayCommand CollapseSelectedCommand { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand ClearSelectionCommand { get; }

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

        private void ExpandSelected()
        {
            var targets = GetSelectedItems();
            if (targets.Count == 0)
            {
                return;
            }

            Model.Expand(targets);
        }

        private void CollapseSelected()
        {
            var targets = GetSelectedItems();
            if (targets.Count == 0)
            {
                return;
            }

            Model.Collapse(targets);
        }

        private IReadOnlyList<TreeItem> GetSelectedItems()
        {
            if (SelectionModel.SelectedItems.Count == 0)
            {
                return Array.Empty<TreeItem>();
            }

            var items = new List<TreeItem>();
            var seen = new HashSet<TreeItem>();

            foreach (var entry in SelectionModel.SelectedItems)
            {
                if (entry is HierarchicalNode node && node.Item is TreeItem item && seen.Add(item))
                {
                    items.Add(item);
                }
                else if (entry is TreeItem itemValue && seen.Add(itemValue))
                {
                    items.Add(itemValue);
                }
            }

            return items;
        }

        private void UpdateVisibleCount()
        {
            VisibleCount = Model.Count;
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = SelectionModel.SelectedItems.Count;
        }

        private static ObservableCollection<TreeItem> BuildSample()
        {
            var roots = new ObservableCollection<TreeItem>();
            var id = 1;

            for (var group = 1; group <= 3; group++)
            {
                var root = new TreeItem(id++, $"Group {group}");
                for (var child = 1; child <= 3; child++)
                {
                    var childNode = new TreeItem(id++, $"Item {group}.{child}", root);
                    for (var leaf = 1; leaf <= 2; leaf++)
                    {
                        childNode.Children.Add(new TreeItem(id++, $"Item {group}.{child}.{leaf}", childNode));
                    }
                    root.Children.Add(childNode);
                }
                roots.Add(root);
            }

            return roots;
        }
    }
}
