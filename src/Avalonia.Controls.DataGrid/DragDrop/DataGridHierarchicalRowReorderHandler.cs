// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Input;
using Avalonia.Utilities;

namespace Avalonia.Controls.DataGridDragDrop
{
    /// <summary>
    /// Reorders hierarchical rows by moving items between sibling collections.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridHierarchicalRowReorderHandler : IDataGridRowDropHandler
    {
        private bool ValidateCore(DataGridRowDropEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!args.IsSameGrid || args.Grid == null || args.Items.Count == 0)
            {
                return false;
            }

            var grid = args.Grid;
            if (!grid.CanUserReorderRows || grid.IsReadOnly || !grid.IsEnabled)
            {
                return false;
            }

            if (!grid.HierarchicalRowsEnabled || grid.HierarchicalModel is not IHierarchicalModel model)
            {
                return false;
            }

            if (model.Root == null)
            {
                return false;
            }

            if (args.TargetItem == DataGridCollectionView.NewItemPlaceholder)
            {
                return false;
            }

            if (args.Position == DataGridRowDropPosition.Inside && args.TargetItem == null)
            {
                return false;
            }

            if (!TryCastNodes(args.Items, out var nodes))
            {
                return false;
            }

            if (args.TargetItem is HierarchicalNode targetNode && nodes.Contains(targetNode))
            {
                return false;
            }

            // Do not allow dragging the root container.
            if (nodes.Any(n => ReferenceEquals(n, model.Root)))
            {
                return false;
            }

            if (nodes.Any(n => !HasWritableChildren(n.Parent ?? model.Root)))
            {
                return false;
            }

            if (args.Position == DataGridRowDropPosition.Inside &&
                args.TargetItem is HierarchicalNode target &&
                !HasWritableChildren(target))
            {
                return false;
            }

            if (!TryResolveDropTarget(model, args, out var parent, out var insertIndex))
            {
                return false;
            }

            if (!HasWritableChildren(parent))
            {
                return false;
            }

            if (parent == null)
            {
                return false;
            }

            // Reject when the target is within the dragged subtree.
            if (nodes.Any(n => IsAncestorOf(n, parent)))
            {
                return false;
            }

            if (grid.DataConnection?.EditableCollectionView?.IsAddingNew == true ||
                grid.DataConnection?.EditableCollectionView?.IsEditingItem == true)
            {
                return false;
            }

            // Only move is currently supported.
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

            var grid = args.Grid!;
            var model = (IHierarchicalModel)grid.HierarchicalModel!;
            if (!TryCastNodes(args.Items, out var nodes) ||
                !TryResolveDropTarget(model, args, out var parent, out var insertIndex) ||
                parent == null)
            {
                return false;
            }

            var childrenList = GetChildrenList(parent);
            if (childrenList == null || childrenList.IsReadOnly || childrenList.IsFixedSize)
            {
                return false;
            }

            // Order dragged nodes by their current flattened index to keep visual order.
            var orderedNodes = args.SourceIndices
                .Zip(nodes, (index, node) => (index, node))
                .OrderBy(t => t.index)
                .Select(t => t.node)
                .ToList();

            // Remove from source parents starting from the end to keep indices valid.
            foreach (var group in orderedNodes.GroupBy(n => n.Parent ?? model.Root))
            {
                var parentNode = group.Key;
                if (parentNode == null)
                {
                    continue;
                }

                var sourceList = GetChildrenList(parentNode);
                if (sourceList == null || sourceList.IsReadOnly || sourceList.IsFixedSize)
                {
                    return false;
                }

                var indexes = group
                    .Select(n => sourceList.IndexOf(n.Item))
                    .Where(i => i >= 0)
                    .OrderByDescending(i => i)
                    .ToList();

                foreach (var index in indexes)
                {
                    if (ReferenceEquals(parentNode, parent) && index < insertIndex)
                    {
                        insertIndex--;
                    }

                    sourceList.RemoveAt(index);
                }
            }

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                childrenList.Insert(insertIndex + i, orderedNodes[i].Item);
            }

            return true;
        }

#if !DATAGRID_INTERNAL
        public bool Execute(DataGridRowDropEventArgs args) => ExecuteCore(args);
#else
        bool IDataGridRowDropHandler.Execute(DataGridRowDropEventArgs args) => ExecuteCore(args);
#endif

        private static bool TryCastNodes(IReadOnlyList<object> items, out List<HierarchicalNode> nodes)
        {
            nodes = new List<HierarchicalNode>(items.Count);

            foreach (var item in items)
            {
                if (item is not HierarchicalNode node)
                {
                    nodes.Clear();
                    return false;
                }

                nodes.Add(node);
            }

            return nodes.Count > 0;
        }

        private static bool TryResolveDropTarget(
            IHierarchicalModel model,
            DataGridRowDropEventArgs args,
            out HierarchicalNode? parent,
            out int insertIndex)
        {
            parent = model.Root;
            insertIndex = 0;

            if (model.Root == null)
            {
                return false;
            }

            var targetNode = args.TargetItem as HierarchicalNode;
            if (targetNode == null)
            {
                parent = model.Root;
                insertIndex = parent.MutableChildren.Count;
                return true;
            }

            if (args.Position == DataGridRowDropPosition.Inside)
            {
                parent = targetNode;
                insertIndex = parent.MutableChildren.Count;
                return true;
            }

            parent = targetNode.Parent ?? model.Root;
            if (parent == null)
            {
                return false;
            }

            insertIndex = parent.MutableChildren.IndexOf(targetNode);
            if (insertIndex < 0)
            {
                return false;
            }

            if (args.Position == DataGridRowDropPosition.After)
            {
                insertIndex++;
            }

            insertIndex = MathUtilities.Clamp(insertIndex, 0, parent.MutableChildren.Count);
            return true;
        }

        private static bool IsAncestorOf(HierarchicalNode ancestor, HierarchicalNode node)
        {
            var current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static IList? GetChildrenList(HierarchicalNode parent)
        {
            return parent.ChildrenSource as IList;
        }

        private static bool HasWritableChildren(HierarchicalNode node)
        {
            var list = GetChildrenList(node);
            return list != null && !list.IsReadOnly && !list.IsFixedSize;
        }
    }
}
