using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels
{
    public class RowDragDropViewModel : ObservableObject
    {
        private readonly ObservableCollection<ChangeItem> _items;
        private readonly DataGridRowDragDropOptions _options;
        private readonly IDataGridRowDropHandler _dropHandler;
        private DataGridRowDragHandle _rowDragHandle;
        private bool _allowCopy;
        private bool _showHandle = true;
        private bool _prioritizeDragGesture = true;
        private int _nextId;

        public RowDragDropViewModel()
        {
            _items = new ObservableCollection<ChangeItem>(CreateItems());
            _nextId = _items.Any() ? _items.Max(x => x.Id) : 0;
            _options = new DataGridRowDragDropOptions
            {
                AllowedEffects = DragDropEffects.Move,
                SuppressSelectionDragFromDragHandle = true
            };
            _dropHandler = new ChangeItemDropHandler(this);
            _rowDragHandle = DataGridRowDragHandle.RowHeaderAndRow;
            DragHandles = new[]
            {
                DataGridRowDragHandle.RowHeader,
                DataGridRowDragHandle.Row,
                DataGridRowDragHandle.RowHeaderAndRow
            };
        }

        public ObservableCollection<ChangeItem> Items => _items;

        public DataGridRowDragDropOptions Options => _options;

        public IDataGridRowDropHandler DropHandler => _dropHandler;

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
                    _options.SuppressSelectionDragFromDragHandle = value;
                }
            }
        }

        public bool AllowCopy
        {
            get => _allowCopy;
            set
            {
                if (SetProperty(ref _allowCopy, value))
                {
                    _options.AllowedEffects = value
                        ? DragDropEffects.Move | DragDropEffects.Copy
                        : DragDropEffects.Move;
                }
            }
        }

        internal int NextId()
        {
            _nextId++;
            return _nextId;
        }

        private static IEnumerable<ChangeItem> CreateItems()
        {
            var topics = new[]
            {
                "Design review",
                "API surface",
                "Performance sweep",
                "Docs polish",
                "Accessibility",
                "Test debt",
                "Theme tweaks",
                "Regression triage",
                "Toolkit sync",
                "Release checklist",
                "Localization",
                "UX polish",
                "Animations",
                "Instrumentation",
                "Crash triage",
                "Memory sweep",
                "Networking",
                "Caching",
                "Shadows",
                "Typography",
                "Forms overhaul",
                "Grid layout",
                "Data sync",
                "Theme docs",
                "Samples refresh",
                "Benchmark run",
                "QA signoff",
                "Release notes"
            };

            var value = 10;
            foreach (var topic in topics)
            {
                yield return new ChangeItem
                {
                    Id = value,
                    Name = topic,
                    Lane = value <= 30 ? "Pinned" : "Backlog",
                    Value = value
                };

                value += 10;
            }
        }

        private sealed class ChangeItemDropHandler : IDataGridRowDropHandler
        {
            private readonly RowDragDropViewModel _owner;
            private readonly DataGridRowReorderHandler _reorder = new();

            public ChangeItemDropHandler(RowDragDropViewModel owner)
            {
                _owner = owner;
            }

            public bool Validate(DataGridRowDropEventArgs args)
            {
                var reorderValid = args.RequestedEffect == DragDropEffects.Copy
                    ? CanCopy(args)
                    : _reorder.Validate(args);
                var target = args.TargetItem as ChangeItem;
                var protectedLane = target?.Lane == "Pinned" && args.Position == DataGridRowDropPosition.Before;

                if (!reorderValid || protectedLane)
                {
                    args.EffectiveEffect = DragDropEffects.None;
                    SetFeedbackCaption(
                        args,
                        protectedLane
                            ? "Pinned rows stay at the top of the backlog."
                            : "Drop here is not allowed.");
                    return false;
                }

                args.EffectiveEffect = args.RequestedEffect == DragDropEffects.Copy
                    ? DragDropEffects.Copy
                    : DragDropEffects.Move;
                SetFeedbackCaption(args, BuildCaption(args));
                return true;
            }

            public bool Execute(DataGridRowDropEventArgs args)
            {
                if (args.EffectiveEffect == DragDropEffects.Copy && args.TargetList is IList list)
                {
                    var insertIndex = args.InsertIndex;
                    foreach (var item in args.Items.OfType<ChangeItem>())
                    {
                        var copy = new ChangeItem
                        {
                            Id = _owner.NextId(),
                            Name = $"{item.Name} (copy)",
                            Lane = item.Lane,
                            Value = item.Value
                        };

                        list.Insert(insertIndex++, copy);
                    }

                    return true;
                }

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
                var operation = args.EffectiveEffect == DragDropEffects.Copy ? "Copy" : "Move";
                var itemCount = args.Items.Count == 1 ? "1 row" : $"{args.Items.Count} rows";
                var target = args.TargetItem is ChangeItem changeItem
                    ? $"{DescribePosition(args.Position)} {changeItem.Name}"
                    : $"at row {args.InsertIndex + 1}";
                return $"{operation} {itemCount} {target}.";
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

            private static bool CanCopy(DataGridRowDropEventArgs args)
            {
                if (!args.IsSameGrid ||
                    args.Grid == null ||
                    args.TargetList == null ||
                    args.Items.Count == 0)
                {
                    return false;
                }

                if (!args.Grid.CanUserReorderRows ||
                    args.Grid.IsReadOnly ||
                    !args.Grid.IsEnabled)
                {
                    return false;
                }

                var view = args.Grid.CollectionView;
                if (view is DataGridCollectionView editableView &&
                    (editableView.IsAddingNew || editableView.IsEditingItem))
                {
                    return false;
                }

                if (args.TargetList.IsReadOnly || args.TargetList.IsFixedSize)
                {
                    return false;
                }

                if (args.TargetItem == DataGridCollectionView.NewItemPlaceholder)
                {
                    return false;
                }

                if (view != null)
                {
                    if ((view.SortDescriptions?.Count ?? 0) > 0 ||
                        view.IsGrouping ||
                        view is DataGridCollectionView paged && paged.PageSize > 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
