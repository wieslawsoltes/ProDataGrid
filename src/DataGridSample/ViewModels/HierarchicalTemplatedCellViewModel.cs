// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using Avalonia.Controls.DataGridHierarchical;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalTemplatedCellViewModel : ObservableObject
    {
        public class TreeItem
        {
            public TreeItem(string name, string iconKey, bool isIconVisible)
            {
                Name = name;
                IconKey = iconKey;
                IsIconVisible = isIconVisible;
                Children = new ObservableCollection<TreeItem>();
            }

            public string Name { get; }

            public string IconKey { get; }

            public bool IsIconVisible { get; }

            public ObservableCollection<TreeItem> Children { get; }
        }

        public HierarchicalTemplatedCellViewModel()
        {
            var root = CreateTree();
            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 0,
                VirtualizeChildren = true
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoot(root);
            if (Model.Root is { } rootNode)
            {
                Model.Expand(rootNode);
            }

            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
        }

        public HierarchicalModel<TreeItem> Model { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        private static TreeItem CreateTree()
        {
            var root = new TreeItem("Workspace", "Folder", isIconVisible: true);

            for (var groupIndex = 1; groupIndex <= 6; groupIndex++)
            {
                var group = new TreeItem($"Category {groupIndex}", "Folder", isIconVisible: true);

                for (var itemIndex = 1; itemIndex <= 12; itemIndex++)
                {
                    var iconKey = itemIndex % 3 == 0 ? "Code" : "Document";
                    var isVisible = itemIndex % 7 != 0;
                    group.Children.Add(new TreeItem($"Item {groupIndex}.{itemIndex}", iconKey, isVisible));
                }

                root.Children.Add(group);
            }

            return root;
        }
    }
}
