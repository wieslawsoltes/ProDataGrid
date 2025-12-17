using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalRowDragDropViewModel : ObservableObject
    {
        private DataGridRowDragHandle _rowDragHandle;
        private bool _showHandle = true;

        public HierarchicalRowDragDropViewModel()
        {
            var root = CreateTree();
            Model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = x => x.Children,
                AutoExpandRoot = true
            });
            Model.SetRoot(root);

            Options = new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move
            };

            DropHandler = new DataGridHierarchicalRowReorderHandler();
            RowDragHandle = DataGridRowDragHandle.RowHeader;
            DragHandles = new[]
            {
                DataGridRowDragHandle.RowHeader,
                DataGridRowDragHandle.Row,
                DataGridRowDragHandle.RowHeaderAndRow
            };

            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
        }

        public HierarchicalModel<TreeItem> Model { get; }

        public DataGridRowDragDropOptions Options { get; }

        public IDataGridRowDropHandler DropHandler { get; }

        public IReadOnlyList<DataGridRowDragHandle> DragHandles { get; }

        public DataGridRowDragHandle RowDragHandle
        {
            get => _rowDragHandle;
            set => SetProperty(ref _rowDragHandle, value);
        }

        public bool ShowHandle
        {
            get => _showHandle;
            set => SetProperty(ref _showHandle, value);
        }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        private static TreeItem CreateTree()
        {
            return new TreeItem("Releases", new ObservableCollection<TreeItem>
            {
                new("v1.0", new ObservableCollection<TreeItem>
                {
                    new("Features", new ObservableCollection<TreeItem>
                    {
                        new("Drag & drop rows"),
                        new("Hierarchical preview")
                    }),
                    new("Bugfixes", new ObservableCollection<TreeItem>
                    {
                        new("Selection regression"),
                        new("Cell templates")
                    })
                }),
                new("v2.0", new ObservableCollection<TreeItem>
                {
                    new("Features", new ObservableCollection<TreeItem>
                    {
                        new("Virtualization revamp"),
                        new("Keyboard navigation")
                    }),
                    new("Bugfixes", new ObservableCollection<TreeItem>
                    {
                        new("Dark theme polish"),
                        new("Scrolling jitter")
                    })
                }),
                new("Backlog", new ObservableCollection<TreeItem>
                {
                    new("Performance"),
                    new("Docs & samples"),
                    new("Accessibility")
                })
            });
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public class TreeItem
        {
            public TreeItem(string name, ObservableCollection<TreeItem>? children = null)
            {
                Name = name;
                Children = children ?? new ObservableCollection<TreeItem>();
            }

            public string Name { get; }

            public ObservableCollection<TreeItem> Children { get; }
        }
    }
}
