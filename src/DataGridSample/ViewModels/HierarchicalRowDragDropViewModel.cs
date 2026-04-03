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
        private bool _useMultipleRoots = true;
        private bool _prioritizeDragGesture = true;

        public HierarchicalRowDragDropViewModel()
        {
            Model = new HierarchicalModel<TreeItem>(new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = x => x.Children,
                AutoExpandRoot = true,
                IsLeafSelector = x => x.Children.Count == 0
            });

            // Start with multiple roots to demonstrate the feature
            RootItems = CreateMultipleRoots();
            Model.SetRoots(RootItems);

            Options = new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move,
                SuppressSelectionDragFromDragHandle = true
            };

            DropHandler = new TreeItemDropHandler();
            RowDragHandle = DataGridRowDragHandle.RowHeader;
            DragHandles = new[]
            {
                DataGridRowDragHandle.RowHeader,
                DataGridRowDragHandle.Row,
                DataGridRowDragHandle.RowHeaderAndRow
            };

            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
            ToggleMultiRootCommand = new RelayCommand(_ => UseMultipleRoots = !UseMultipleRoots);
        }

        public HierarchicalModel<TreeItem> Model { get; }

        public ObservableCollection<TreeItem> RootItems { get; }

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

        public bool PrioritizeDragGesture
        {
            get => _prioritizeDragGesture;
            set
            {
                if (SetProperty(ref _prioritizeDragGesture, value))
                {
                    Options.SuppressSelectionDragFromDragHandle = value;
                }
            }
        }

        public bool UseMultipleRoots
        {
            get => _useMultipleRoots;
            set
            {
                if (SetProperty(ref _useMultipleRoots, value))
                {
                    if (value)
                    {
                        // Switch to multiple roots
                        RootItems.Clear();
                        foreach (var item in CreateMultipleRoots())
                        {
                            RootItems.Add(item);
                        }
                        Model.SetRoots(RootItems);
                    }
                    else
                    {
                        // Switch back to single root
                        Model.SetRoot(CreateTree());
                    }
                }
            }
        }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand ToggleMultiRootCommand { get; }

        private static TreeItem CreateTree()
        {
            return new TreeItem("Releases", true, new ObservableCollection<TreeItem>
            {
                new("v1.0", true, new ObservableCollection<TreeItem>
                {
                    new("Features", true, new ObservableCollection<TreeItem>
                    {
                        new("Drag & drop rows", acceptsChildren: false),
                        new("Hierarchical preview", acceptsChildren: false)
                    }),
                    new("Bugfixes", true, new ObservableCollection<TreeItem>
                    {
                        new("Selection regression", acceptsChildren: false),
                        new("Cell templates", acceptsChildren: false)
                    })
                }),
                new("v2.0", true, new ObservableCollection<TreeItem>
                {
                    new("Features", true, new ObservableCollection<TreeItem>
                    {
                        new("Virtualization revamp", acceptsChildren: false),
                        new("Keyboard navigation", acceptsChildren: false)
                    }),
                    new("Bugfixes", true, new ObservableCollection<TreeItem>
                    {
                        new("Dark theme polish", acceptsChildren: false),
                        new("Scrolling jitter", acceptsChildren: false)
                    })
                }),
                new("Backlog", true, new ObservableCollection<TreeItem>
                {
                    new("Performance", acceptsChildren: false),
                    new("Docs & samples", acceptsChildren: false),
                    new("Accessibility", acceptsChildren: false)
                })
            });
        }

        private static ObservableCollection<TreeItem> CreateMultipleRoots()
        {
            return new ObservableCollection<TreeItem>
            {
                new("Project Alpha", true, new ObservableCollection<TreeItem>
                {
                    new("Tasks", true, new ObservableCollection<TreeItem>
                    {
                        new("Setup", acceptsChildren: false),
                        new("Implementation", acceptsChildren: false)
                    }),
                    new("Issues", acceptsChildren: false)
                }),
                new("Project Beta", true, new ObservableCollection<TreeItem>
                {
                    new("Design", true, new ObservableCollection<TreeItem>
                    {
                        new("Wireframes", acceptsChildren: false),
                        new("Mockups", acceptsChildren: false)
                    })
                }),
                new("Project Gamma", true, new ObservableCollection<TreeItem>
                {
                    new("Research", acceptsChildren: false)
                })
            };
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public class TreeItem
        {
            public TreeItem(string name, bool acceptsChildren = true, ObservableCollection<TreeItem>? children = null)
            {
                Name = name;
                AcceptsChildren = acceptsChildren;
                Children = children ?? new ObservableCollection<TreeItem>();
            }

            public string Name { get; }

            public bool AcceptsChildren { get; }

            public ObservableCollection<TreeItem> Children { get; }
        }

        private sealed class TreeItemDropHandler : IDataGridRowDropHandler
        {
            private readonly DataGridHierarchicalRowReorderHandler _reorder = new();

            public bool Validate(DataGridRowDropEventArgs args)
            {
                var reorderValid = _reorder.Validate(args);
                var leafOnlyTarget =
                    args.Position == DataGridRowDropPosition.Inside &&
                    args.TargetItem is HierarchicalNode node &&
                    node.Item is TreeItem item &&
                    !item.AcceptsChildren;

                if (!reorderValid || leafOnlyTarget)
                {
                    args.EffectiveEffect = DragDropEffects.None;
                    SetFeedbackCaption(
                        args,
                        leafOnlyTarget
                            ? "This node only accepts before/after drops."
                            : "Drop here is not allowed.");
                    return false;
                }

                SetFeedbackCaption(args, BuildCaption(args));
                return true;
            }

            public bool Execute(DataGridRowDropEventArgs args)
            {
                return _reorder.Execute(args);
            }

            private static void SetFeedbackCaption(DataGridRowDropEventArgs args, string caption)
            {
                if (args.Session != null)
                {
                    args.Session.FeedbackCaption = caption;
                }
            }

            private static string BuildCaption(DataGridRowDropEventArgs args)
            {
                var target = args.TargetItem is HierarchicalNode node && node.Item is TreeItem item
                    ? $"{DescribePosition(args.Position)} {item.Name}"
                    : "at the end of this level";
                var itemCount = args.Items.Count == 1 ? "1 node" : $"{args.Items.Count} nodes";
                return $"Move {itemCount} {target}.";
            }

            private static string DescribePosition(DataGridRowDropPosition position)
            {
                return position switch
                {
                    DataGridRowDropPosition.After => "after",
                    DataGridRowDropPosition.Inside => "inside",
                    _ => "before"
                };
            }
        }
    }
}
