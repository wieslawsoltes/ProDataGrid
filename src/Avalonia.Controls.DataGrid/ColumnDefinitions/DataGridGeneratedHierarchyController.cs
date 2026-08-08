// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Selects which related nodes remain visible for a hierarchical filter.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedHierarchyFilterMode
    {
        /// <summary>Include only nodes that match.</summary>
        MatchOnly,
        /// <summary>Include matching nodes and all their ancestors.</summary>
        AncestorsOfMatch,
        /// <summary>Include matching nodes and all their descendants.</summary>
        DescendantsOfMatch,
        /// <summary>Use the caller-supplied key-set builder.</summary>
        Custom
    }

    /// <summary>Selects where a generated hierarchy comparer is applied.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedHierarchySortMode
    {
        /// <summary>Sort children only within each parent.</summary>
        Siblings,
        /// <summary>Sort the flattened projection globally.</summary>
        Global
    }

    /// <summary>Provides a public typed hierarchical projection suitable for compiled bindings.</summary>
    /// <typeparam name="TItem">The source node type.</typeparam>
    /// <typeparam name="TKey">The stable node key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedNode<TItem, TKey>
    {
        internal DataGridGeneratedNode(TItem item, TKey key, int depth, TKey parentKey, bool hasParent, bool isExpanded)
        {
            Item = item;
            Key = key;
            Depth = depth;
            ParentKey = parentKey;
            HasParent = hasParent;
            IsExpanded = isExpanded;
        }

        /// <summary>Gets the typed source item.</summary>
        public TItem Item { get; }
        /// <summary>Gets the stable node key.</summary>
        public TKey Key { get; }
        /// <summary>Gets the zero-based hierarchy depth.</summary>
        public int Depth { get; }
        /// <summary>Gets the parent key when <see cref="HasParent"/> is true.</summary>
        public TKey ParentKey { get; }
        /// <summary>Gets whether this node has a parent.</summary>
        public bool HasParent { get; }
        /// <summary>Gets whether this node was expanded when projected.</summary>
        public bool IsExpanded { get; }
    }

    /// <summary>
    /// Executes reflection-free hierarchy traversal, expansion persistence, filtering, and typed projection.
    /// </summary>
    /// <typeparam name="TItem">The source node type.</typeparam>
    /// <typeparam name="TKey">The stable node key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedHierarchyController<TItem, TKey>
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly Func<TItem, IEnumerable<TItem>> _childrenSelector;
        private readonly Func<TItem, bool> _expandedGetter;
        private readonly Action<TItem, bool> _expandedSetter;
        private readonly Func<TItem, CancellationToken, ValueTask<IReadOnlyList<TItem>>> _childLoader;
        private readonly IEqualityComparer<TKey> _keyComparer;

        /// <summary>Initializes a generated hierarchy controller.</summary>
        public DataGridGeneratedHierarchyController(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            Func<TItem, IEnumerable<TItem>> childrenSelector,
            Func<TItem, bool> expandedGetter = null,
            Action<TItem, bool> expandedSetter = null,
            Func<TItem, CancellationToken, ValueTask<IReadOnlyList<TItem>>> childLoader = null,
            IEqualityComparer<TKey> keyComparer = null)
        {
            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _childrenSelector = childrenSelector ?? throw new ArgumentNullException(nameof(childrenSelector));
            _expandedGetter = expandedGetter ?? (_ => false);
            _expandedSetter = expandedSetter;
            _childLoader = childLoader;
            _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        }

        /// <summary>Gets whether an asynchronous child loader was generated.</summary>
        public bool CanLoadChildren => _childLoader != null;

        /// <summary>Loads children through the validated user hook.</summary>
        public ValueTask<IReadOnlyList<TItem>> LoadChildrenAsync(TItem item, CancellationToken cancellationToken = default) =>
            _childLoader == null
                ? ValueTask.FromResult<IReadOnlyList<TItem>>(CopyChildren(item))
                : _childLoader(item, cancellationToken);

        /// <summary>Validates unique keys and cycle-free traversal.</summary>
        public int Validate(IEnumerable<TItem> roots, int maxDepth = 4096)
        {
            var seen = new HashSet<TKey>(_keyComparer);
            var path = new HashSet<TKey>(_keyComparer);
            int count = 0;
            foreach (TItem root in roots ?? throw new ArgumentNullException(nameof(roots)))
            {
                ValidateCore(root, seen, path, 0, maxDepth, ref count);
            }
            return count;
        }

        /// <summary>Captures expanded node keys for restoration across resets.</summary>
        public HashSet<TKey> CaptureExpanded(IEnumerable<TItem> roots)
        {
            var expanded = new HashSet<TKey>(_keyComparer);
            Traverse(roots, item =>
            {
                if (_expandedGetter(item))
                {
                    expanded.Add(_keyAccessor.GetKey(item));
                }
            });
            return expanded;
        }

        /// <summary>Restores expansion state by stable key.</summary>
        public void RestoreExpanded(IEnumerable<TItem> roots, IReadOnlySet<TKey> expandedKeys)
        {
            if (expandedKeys == null)
            {
                throw new ArgumentNullException(nameof(expandedKeys));
            }
            EnsureWritableExpansion();
            Traverse(roots, item => _expandedSetter(item, expandedKeys.Contains(_keyAccessor.GetKey(item))));
        }

        /// <summary>Expands every traversed node.</summary>
        public void ExpandAll(IEnumerable<TItem> roots) => SetAllExpanded(roots, true);

        /// <summary>Collapses every traversed node.</summary>
        public void CollapseAll(IEnumerable<TItem> roots) => SetAllExpanded(roots, false);

        /// <summary>Expands every ancestor on the path to a stable key.</summary>
        public bool ExpandToKey(IEnumerable<TItem> roots, TKey key, int maxDepth = 4096)
        {
            EnsureWritableExpansion();
            var seen = new HashSet<TKey>(_keyComparer);
            var path = new List<TItem>();
            foreach (TItem root in roots ?? throw new ArgumentNullException(nameof(roots)))
            {
                if (FindPath(root, key, seen, path, 0, maxDepth))
                {
                    for (int index = 0; index + 1 < path.Count; index++)
                    {
                        _expandedSetter(path[index], true);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Builds the stable key set included by a hierarchy-aware filter.</summary>
        public HashSet<TKey> BuildFilterKeys(
            IEnumerable<TItem> roots,
            Predicate<TItem> predicate,
            DataGridGeneratedHierarchyFilterMode mode,
            Func<IEnumerable<TItem>, Predicate<TItem>, HashSet<TKey>> custom = null)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }
            if (mode == DataGridGeneratedHierarchyFilterMode.Custom)
            {
                return custom?.Invoke(roots, predicate) ??
                    throw new ArgumentNullException(nameof(custom));
            }

            var result = new HashSet<TKey>(_keyComparer);
            var seen = new HashSet<TKey>(_keyComparer);
            var path = new List<TItem>();
            foreach (TItem root in roots ?? throw new ArgumentNullException(nameof(roots)))
            {
                BuildFilterKeysCore(root, predicate, mode, result, seen, path);
            }
            return result;
        }

        /// <summary>Creates a typed flattened projection with optional sibling or global sorting.</summary>
        public IReadOnlyList<DataGridGeneratedNode<TItem, TKey>> Project(
            IEnumerable<TItem> roots,
            IComparer<TItem> comparer = null,
            DataGridGeneratedHierarchySortMode sortMode = DataGridGeneratedHierarchySortMode.Siblings,
            bool expandedOnly = false)
        {
            var result = new List<DataGridGeneratedNode<TItem, TKey>>();
            var seen = new HashSet<TKey>(_keyComparer);
            AppendProjection(roots, default, false, 0, comparer, expandedOnly, seen, result);
            if (comparer != null && sortMode == DataGridGeneratedHierarchySortMode.Global)
            {
                result.Sort((left, right) => comparer.Compare(left.Item, right.Item));
            }
            return result;
        }

        private void SetAllExpanded(IEnumerable<TItem> roots, bool value)
        {
            EnsureWritableExpansion();
            Traverse(roots, item => _expandedSetter(item, value));
        }

        private void Traverse(IEnumerable<TItem> roots, Action<TItem> action)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }
            var seen = new HashSet<TKey>(_keyComparer);
            var stack = new Stack<TItem>();
            PushReverse(stack, roots);
            while (stack.Count > 0)
            {
                TItem item = stack.Pop();
                TKey key = _keyAccessor.GetKey(item);
                if (!seen.Add(key))
                {
                    throw new InvalidOperationException("Generated hierarchy contains a cycle or duplicate stable key.");
                }
                action(item);
                PushReverse(stack, _childrenSelector(item));
            }
        }

        private void ValidateCore(TItem item, HashSet<TKey> seen, HashSet<TKey> path, int depth, int maxDepth, ref int count)
        {
            if (depth > maxDepth)
            {
                throw new InvalidOperationException("Generated hierarchy exceeded its configured maximum depth.");
            }
            TKey key = _keyAccessor.GetKey(item);
            if (!path.Add(key))
            {
                throw new InvalidOperationException("Generated hierarchy contains a cycle.");
            }
            if (!seen.Add(key))
            {
                throw new InvalidOperationException("Generated hierarchy contains duplicate stable keys.");
            }
            count++;
            foreach (TItem child in _childrenSelector(item) ?? Array.Empty<TItem>())
            {
                ValidateCore(child, seen, path, depth + 1, maxDepth, ref count);
            }
            path.Remove(key);
        }

        private bool FindPath(TItem item, TKey target, HashSet<TKey> seen, List<TItem> path, int depth, int maxDepth)
        {
            if (depth > maxDepth)
            {
                return false;
            }
            TKey key = _keyAccessor.GetKey(item);
            if (!seen.Add(key))
            {
                throw new InvalidOperationException("Generated hierarchy contains a cycle or duplicate stable key.");
            }
            path.Add(item);
            if (_keyComparer.Equals(key, target))
            {
                return true;
            }
            foreach (TItem child in _childrenSelector(item) ?? Array.Empty<TItem>())
            {
                if (FindPath(child, target, seen, path, depth + 1, maxDepth))
                {
                    return true;
                }
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }

        private void BuildFilterKeysCore(
            TItem item,
            Predicate<TItem> predicate,
            DataGridGeneratedHierarchyFilterMode mode,
            HashSet<TKey> result,
            HashSet<TKey> seen,
            List<TItem> path)
        {
            TKey key = _keyAccessor.GetKey(item);
            if (!seen.Add(key))
            {
                throw new InvalidOperationException("Generated hierarchy contains a cycle or duplicate stable key.");
            }
            path.Add(item);
            bool matches = predicate(item);
            if (matches)
            {
                result.Add(key);
                if (mode == DataGridGeneratedHierarchyFilterMode.AncestorsOfMatch)
                {
                    foreach (TItem ancestor in path)
                    {
                        result.Add(_keyAccessor.GetKey(ancestor));
                    }
                }
                else if (mode == DataGridGeneratedHierarchyFilterMode.DescendantsOfMatch)
                {
                    AddSubtreeKeys(item, result, new HashSet<TKey>(_keyComparer));
                }
            }
            foreach (TItem child in _childrenSelector(item) ?? Array.Empty<TItem>())
            {
                BuildFilterKeysCore(child, predicate, mode, result, seen, path);
            }
            path.RemoveAt(path.Count - 1);
        }

        private void AddSubtreeKeys(TItem item, HashSet<TKey> result, HashSet<TKey> visited)
        {
            TKey key = _keyAccessor.GetKey(item);
            if (!visited.Add(key))
            {
                throw new InvalidOperationException("Generated hierarchy contains a cycle.");
            }
            result.Add(key);
            foreach (TItem child in _childrenSelector(item) ?? Array.Empty<TItem>())
            {
                AddSubtreeKeys(child, result, visited);
            }
        }

        private void AppendProjection(
            IEnumerable<TItem> items,
            TKey parentKey,
            bool hasParent,
            int depth,
            IComparer<TItem> comparer,
            bool expandedOnly,
            HashSet<TKey> seen,
            List<DataGridGeneratedNode<TItem, TKey>> result)
        {
            var siblings = new List<TItem>(items ?? Array.Empty<TItem>());
            if (comparer != null)
            {
                siblings.Sort(comparer);
            }
            foreach (TItem item in siblings)
            {
                TKey key = _keyAccessor.GetKey(item);
                if (!seen.Add(key))
                {
                    throw new InvalidOperationException("Generated hierarchy contains a cycle or duplicate stable key.");
                }
                bool expanded = _expandedGetter(item);
                result.Add(new DataGridGeneratedNode<TItem, TKey>(item, key, depth, parentKey, hasParent, expanded));
                if (!expandedOnly || expanded)
                {
                    AppendProjection(_childrenSelector(item), key, true, depth + 1, comparer, expandedOnly, seen, result);
                }
            }
        }

        private IReadOnlyList<TItem> CopyChildren(TItem item) =>
            new List<TItem>(_childrenSelector(item) ?? Array.Empty<TItem>());

        private static void PushReverse(Stack<TItem> stack, IEnumerable<TItem> items)
        {
            if (items == null)
            {
                return;
            }
            var copy = new List<TItem>(items);
            for (int index = copy.Count - 1; index >= 0; index--)
            {
                stack.Push(copy[index]);
            }
        }

        private void EnsureWritableExpansion()
        {
            if (_expandedSetter == null)
            {
                throw new InvalidOperationException("Generated hierarchy does not define a writable [DataGridExpanded] member.");
            }
        }
    }
}
