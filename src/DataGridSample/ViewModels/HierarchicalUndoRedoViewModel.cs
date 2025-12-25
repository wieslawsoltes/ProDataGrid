// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls.DataGridHierarchical;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class HierarchicalUndoRedoViewModel : ObservableObject
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
        public sealed class TreeItem : ObservableObject
        {
            private TreeItem? _parent;

            public TreeItem(int id, string name, TreeItem? parent = null)
            {
                Id = id;
                Name = name;
                _parent = parent;
                Children = new ObservableCollection<TreeItem>();
            }

            public int Id { get; }

            public string Name { get; }

            public TreeItem? Parent
            {
                get => _parent;
                set
                {
                    if (SetProperty(ref _parent, value))
                    {
                        OnPropertyChanged(nameof(ParentName));
                    }
                }
            }

            public string ParentName => Parent?.Name ?? "Root";

            public ObservableCollection<TreeItem> Children { get; }
        }

        private sealed class ReparentAction
        {
            public ReparentAction(TreeItem item, TreeItem? oldParent, int oldIndex, TreeItem? newParent, int newIndex)
            {
                Item = item;
                OldParent = oldParent;
                OldIndex = oldIndex;
                NewParent = newParent;
                NewIndex = newIndex;
            }

            public TreeItem Item { get; }
            public TreeItem? OldParent { get; }
            public int OldIndex { get; }
            public TreeItem? NewParent { get; }
            public int NewIndex { get; }
        }

        private readonly Stack<ReparentAction> _undoStack = new();
        private readonly Stack<ReparentAction> _redoStack = new();
        private TreeItem? _selectedItem;
        private TreeItem? _destinationParent;
        private string _selectedLabel = "Selected: None";
        private string _status = "Select an item and move it between groups.";
        private int _undoCount;
        private int _redoCount;

        public HierarchicalUndoRedoViewModel()
        {
            Roots = BuildSample();

            var options = new HierarchicalOptions<TreeItem>
            {
                ChildrenSelector = item => item.Children,
                IsLeafSelector = item => item.Children.Count == 0,
                AutoExpandRoot = true,
                MaxAutoExpandDepth = 1
            };

            Model = new HierarchicalModel<TreeItem>(options);
            Model.SetRoots(Roots);

            MoveSelectedCommand = new RelayCommand(_ => MoveSelected(), _ => CanMoveSelected());
            UndoCommand = new RelayCommand(_ => Undo(), _ => _undoStack.Count > 0);
            RedoCommand = new RelayCommand(_ => Redo(), _ => _redoStack.Count > 0);
            ExpandAllCommand = new RelayCommand(_ => Model.ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => Model.CollapseAll());
            ResetCommand = new RelayCommand(_ => ResetSample());

            DestinationParent = Roots.FirstOrDefault();
        }

        public ObservableCollection<TreeItem> Roots { get; }

        public HierarchicalModel<TreeItem> Model { get; }

        public RelayCommand MoveSelectedCommand { get; }

        public RelayCommand UndoCommand { get; }

        public RelayCommand RedoCommand { get; }

        public RelayCommand ExpandAllCommand { get; }

        public RelayCommand CollapseAllCommand { get; }

        public RelayCommand ResetCommand { get; }

        public TreeItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    SelectedLabel = value == null ? "Selected: None" : $"Selected: {value.Name} (#{value.Id})";
                    UpdateCommandStates();
                }
            }
        }

        public TreeItem? DestinationParent
        {
            get => _destinationParent;
            set
            {
                if (SetProperty(ref _destinationParent, value))
                {
                    UpdateCommandStates();
                }
            }
        }

        public string SelectedLabel
        {
            get => _selectedLabel;
            private set => SetProperty(ref _selectedLabel, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public int UndoCount
        {
            get => _undoCount;
            private set => SetProperty(ref _undoCount, value);
        }

        public int RedoCount
        {
            get => _redoCount;
            private set => SetProperty(ref _redoCount, value);
        }

        private bool CanMoveSelected()
        {
            if (SelectedItem == null || DestinationParent == null)
            {
                return false;
            }

            if (SelectedItem.Parent == null)
            {
                return false;
            }

            if (ReferenceEquals(SelectedItem.Parent, DestinationParent))
            {
                return false;
            }

            return !IsDescendant(DestinationParent, SelectedItem);
        }

        private void MoveSelected()
        {
            var selectedItem = SelectedItem;
            var destinationParent = DestinationParent;
            if (selectedItem == null || destinationParent == null)
            {
                Status = "Select an item and destination parent.";
                return;
            }

            if (selectedItem.Parent == null)
            {
                Status = "Root items are fixed in this sample.";
                return;
            }

            if (ReferenceEquals(selectedItem.Parent, destinationParent))
            {
                Status = $"'{selectedItem.Name}' is already under '{destinationParent.Name}'.";
                return;
            }

            if (IsDescendant(destinationParent, selectedItem))
            {
                Status = "Cannot move an item into its own subtree.";
                return;
            }

            var sourceParent = selectedItem.Parent;
            var sourceCollection = GetCollection(sourceParent);
            var sourceIndex = sourceCollection.IndexOf(selectedItem);
            if (sourceIndex < 0)
            {
                Status = "Could not locate the selected item.";
                return;
            }

            var destinationCollection = GetCollection(destinationParent);
            var destinationIndex = destinationCollection.Count;

            var action = new ReparentAction(selectedItem, sourceParent, sourceIndex, destinationParent, destinationIndex);

            ApplyMove(action.Item, action.OldParent, action.OldIndex, action.NewParent, action.NewIndex);
            _undoStack.Push(action);
            _redoStack.Clear();

            UpdateHistoryCounts();
            Status = $"Moved '{selectedItem.Name}' to '{destinationParent.Name}'.";
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }

            var action = _undoStack.Pop();
            ApplyMove(action.Item, action.NewParent, action.NewIndex, action.OldParent, action.OldIndex);
            _redoStack.Push(action);

            SelectedItem = action.Item;
            UpdateHistoryCounts();
            Status = $"Undo: moved '{action.Item.Name}' back to '{action.OldParent?.Name ?? "Root"}'.";
        }

        private void Redo()
        {
            if (_redoStack.Count == 0)
            {
                return;
            }

            var action = _redoStack.Pop();
            ApplyMove(action.Item, action.OldParent, action.OldIndex, action.NewParent, action.NewIndex);
            _undoStack.Push(action);

            SelectedItem = action.Item;
            UpdateHistoryCounts();
            Status = $"Redo: moved '{action.Item.Name}' to '{action.NewParent?.Name ?? "Root"}'.";
        }

        private void ResetSample()
        {
            Roots.Clear();
            foreach (var root in BuildSample())
            {
                Roots.Add(root);
            }

            Model.SetRoots(Roots);
            DestinationParent = Roots.FirstOrDefault();
            SelectedItem = null;

            _undoStack.Clear();
            _redoStack.Clear();
            UpdateHistoryCounts();
            Status = "Sample data reset.";
        }

        private void ApplyMove(TreeItem item, TreeItem? fromParent, int fromIndex, TreeItem? toParent, int toIndex)
        {
            var sourceCollection = GetCollection(fromParent);
            if (fromIndex < 0 || fromIndex >= sourceCollection.Count || !ReferenceEquals(sourceCollection[fromIndex], item))
            {
                fromIndex = sourceCollection.IndexOf(item);
            }

            if (fromIndex >= 0)
            {
                sourceCollection.RemoveAt(fromIndex);
            }

            var destinationCollection = GetCollection(toParent);
            if (toIndex < 0 || toIndex > destinationCollection.Count)
            {
                toIndex = destinationCollection.Count;
            }

            destinationCollection.Insert(toIndex, item);
            item.Parent = toParent;
        }

        private ObservableCollection<TreeItem> GetCollection(TreeItem? parent)
        {
            return parent?.Children ?? Roots;
        }

        private void UpdateHistoryCounts()
        {
            UndoCount = _undoStack.Count;
            RedoCount = _redoStack.Count;
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            MoveSelectedCommand.RaiseCanExecuteChanged();
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        private static bool IsDescendant(TreeItem node, TreeItem potentialAncestor)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (ReferenceEquals(current, potentialAncestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static ObservableCollection<TreeItem> BuildSample()
        {
            var id = 1;

            var backlog = new TreeItem(id++, "Backlog");
            var inProgress = new TreeItem(id++, "In Progress");
            var done = new TreeItem(id++, "Done");

            var planning = AddChild(backlog, ref id, "Planning");
            AddChild(planning, ref id, "Specs");
            AddChild(planning, ref id, "Estimates");

            var feature = AddChild(backlog, ref id, "Feature X");
            AddChild(feature, ref id, "Design");
            AddChild(feature, ref id, "Implementation");

            AddChild(inProgress, ref id, "Bugfix Sprint");
            var qa = AddChild(inProgress, ref id, "QA");
            AddChild(qa, ref id, "Regression");

            AddChild(done, ref id, "Release Notes");
            AddChild(done, ref id, "Telemetry Review");

            return new ObservableCollection<TreeItem>
            {
                backlog,
                inProgress,
                done
            };
        }

        private static TreeItem AddChild(TreeItem parent, ref int id, string name)
        {
            var child = new TreeItem(id++, name, parent);
            parent.Children.Add(child);
            return child;
        }
    }
}
