// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Utilities;

namespace Avalonia.Controls.DataGridDragDrop
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridRowReorderHandler : IDataGridRowDropHandler
    {
        private bool ValidateCore(DataGridRowDropEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

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

            var editable = args.Grid.DataConnection?.EditableCollectionView;
            if (editable?.IsAddingNew == true || editable?.IsEditingItem == true)
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

            if (args.TargetItem != null && args.Items.Contains(args.TargetItem))
            {
                return false;
            }

            var view = args.Grid.DataConnection?.CollectionView;
            if (view != null)
            {
                if ((view.SortDescriptions?.Count ?? 0) > 0 ||
                    view.IsGrouping ||
                    view is DataGridCollectionView paged && paged.PageSize > 0)
                {
                    return false;
                }
            }

            if (!args.RequestedEffect.HasFlag(DragDropEffects.Move))
            {
                args.EffectiveEffect = DragDropEffects.None;
                return false;
            }

            args.EffectiveEffect = DragDropEffects.Move;
            return true;
        }

#if !DATAGRID_INTERNAL
        public bool Validate(DataGridRowDropEventArgs args) => ValidateCore(args);
#else
        bool IDataGridRowDropHandler.Validate(DataGridRowDropEventArgs args) => ValidateCore(args);
#endif

        private bool ExecuteCore(DataGridRowDropEventArgs args)
        {
            if (!ValidateCore(args))
            {
                return false;
            }

            var list = args.TargetList!;
            var ordered = args.SourceIndices
                .Zip(args.Items, (index, item) => (index, item))
                .Where(x => x.item != null && !ReferenceEquals(x.item, DataGridCollectionView.NewItemPlaceholder))
                .OrderBy(x => x.index)
                .ToList();

            if (ordered.Count == 0)
            {
                return false;
            }

            var items = ordered.Select(x => x.item).ToList();
            var sourceListIndices = new List<int>(items.Count);

            var indexLookup = new Dictionary<object, Queue<int>>(ReferenceEqualityComparer.Instance);

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                {
                    continue;
                }

                if (!indexLookup.TryGetValue(item, out var queue))
                {
                    queue = new Queue<int>();
                    indexLookup[item] = queue;
                }

                queue.Enqueue(i);
            }

            foreach (var item in items)
            {
                if (!indexLookup.TryGetValue(item, out var queue) || queue.Count == 0)
                {
                    return false;
                }

                sourceListIndices.Add(queue.Dequeue());
            }

            int insertIndex = args.InsertIndex;
            if (args.TargetItem != null)
            {
                var targetListIndex = list.IndexOf(args.TargetItem);
                if (targetListIndex < 0)
                {
                    return false;
                }

                insertIndex = args.Position == DataGridRowDropPosition.After
                    ? targetListIndex + 1
                    : targetListIndex;
            }
            else
            {
                insertIndex = Math.Min(insertIndex, list.Count);
            }

            var beforeCount = sourceListIndices.Count(i => i < insertIndex);
            insertIndex -= beforeCount;

            sourceListIndices.Sort();

            for (int i = sourceListIndices.Count - 1; i >= 0; i--)
            {
                list.RemoveAt(sourceListIndices[i]);
            }

            for (int i = 0; i < items.Count; i++)
            {
                list.Insert(insertIndex + i, items[i]);
            }

            return true;
        }

#if !DATAGRID_INTERNAL
        public bool Execute(DataGridRowDropEventArgs args) => ExecuteCore(args);
#else
        bool IDataGridRowDropHandler.Execute(DataGridRowDropEventArgs args) => ExecuteCore(args);
#endif
    }
}
