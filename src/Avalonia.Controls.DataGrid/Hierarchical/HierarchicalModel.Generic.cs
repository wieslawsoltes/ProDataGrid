// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.DataGridHierarchical
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IHierarchicalModel<T> : IHierarchicalModel
    {
        HierarchicalOptions<T> TypedOptions { get; }

        new HierarchicalNode<T>? Root { get; }

        new IReadOnlyList<HierarchicalNode<T>> Flattened { get; }

        new IReadOnlyList<HierarchicalNode<T>> ObservableFlattened { get; }

        void SetRoot(T rootItem);

        /// <summary>
        /// Sets multiple root items that will be displayed at the top level.
        /// Creates a virtual root container that holds all items.
        /// </summary>
        /// <param name="rootItems">Collection of items to display at root level.</param>
        void SetRoots(IEnumerable<T> rootItems);

        HierarchicalNode<T> GetTypedNode(int index);

        HierarchicalNode<T>? FindNode(T item);

        int IndexOf(T item);

        void Expand(HierarchicalNode<T> node);

        void Expand(IEnumerable<T> items);

        Task ExpandAsync(HierarchicalNode<T> node, CancellationToken cancellationToken = default);

        Task ExpandAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

        void Collapse(HierarchicalNode<T> node);

        void Collapse(IEnumerable<T> items);

        Task CollapseAsync(IEnumerable<T> items, CancellationToken cancellationToken = default);

        void Toggle(HierarchicalNode<T> node);

        Task ToggleAsync(HierarchicalNode<T> node, CancellationToken cancellationToken = default);

        void Refresh(HierarchicalNode<T>? node = null);

        Task RefreshAsync(HierarchicalNode<T>? node = null, CancellationToken cancellationToken = default);

        void ExpandAll(HierarchicalNode<T>? node = null, int? maxDepth = null);

        void CollapseAll(HierarchicalNode<T>? node = null, int? minDepth = null);

        void Sort(HierarchicalNode<T>? node = null, IComparer<T>? comparer = null, bool recursive = true);

        void ApplySiblingComparer(IComparer<T>? comparer, bool recursive = true);

        event EventHandler<HierarchicalNodeEventArgs<T>>? NodeExpandedTyped;

        event EventHandler<HierarchicalNodeEventArgs<T>>? NodeCollapsedTyped;

        event EventHandler<HierarchicalNodeEventArgs<T>>? NodeLoadingTyped;

        event EventHandler<HierarchicalNodeEventArgs<T>>? NodeLoadedTyped;

        event EventHandler<HierarchicalNodeLoadFailedEventArgs<T>>? NodeLoadFailedTyped;

        event EventHandler<HierarchicalNodeRetryEventArgs<T>>? NodeLoadRetryScheduledTyped;

        event EventHandler<HierarchyChangedEventArgs<T>>? HierarchyChangedTyped;

        event EventHandler<FlattenedChangedEventArgs<T>>? FlattenedChangedTyped;
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeEventArgs<T> : EventArgs
    {
        public HierarchicalNodeEventArgs(HierarchicalNode<T> node)
        {
            Node = node;
        }

        public HierarchicalNode<T> Node { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeLoadFailedEventArgs<T> : HierarchicalNodeEventArgs<T>
    {
        public HierarchicalNodeLoadFailedEventArgs(HierarchicalNode<T> node, Exception error)
            : base(node)
        {
            Error = error;
        }

        public Exception Error { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalNodeRetryEventArgs<T> : HierarchicalNodeEventArgs<T>
    {
        public HierarchicalNodeRetryEventArgs(HierarchicalNode<T> node, TimeSpan delay)
            : base(node)
        {
            Delay = delay;
        }

        public TimeSpan Delay { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchyChangedEventArgs<T> : HierarchicalNodeEventArgs<T>
    {
        public HierarchyChangedEventArgs(HierarchicalNode<T> node, NotifyCollectionChangedAction action)
            : base(node)
        {
            Action = action;
        }

        public NotifyCollectionChangedAction Action { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class FlattenedChangedEventArgs<T> : EventArgs
    {
        public FlattenedChangedEventArgs(FlattenedChangedEventArgs untyped, IReadOnlyList<HierarchicalNode<T>> flattened)
        {
            Untyped = untyped ?? throw new ArgumentNullException(nameof(untyped));
            Flattened = flattened ?? throw new ArgumentNullException(nameof(flattened));
        }

        public FlattenedChangedEventArgs Untyped { get; }

        public IReadOnlyList<FlattenedChange> Changes => Untyped.Changes;

        public int Version => Untyped.Version;

        public FlattenedIndexMap IndexMap => Untyped.IndexMap;

        public IReadOnlyList<HierarchicalNode<T>> Flattened { get; }
    }

    /// <summary>
    /// Strongly-typed options wrapper to avoid reflection when working with hierarchical data.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalOptions<T>
    {
        private Func<T, IEnumerable<T>?>? _childrenSelector;
        private Func<T, CancellationToken, Task<IEnumerable<T>?>>? _childrenSelectorAsync;
        private Func<T, IAsyncEnumerable<T>?>? _childrenSelectorAsyncEnumerable;
        private Func<T, IObservable<IEnumerable<T>?>>? _childrenSelectorObservable;
        private Func<T, IObservable<object?>?>? _childrenChangeSetObservable;
        private Func<T, IEnumerable<T>?>? _itemsSelector;
        private Func<T, bool>? _isLeafSelector;
        private IComparer<T>? _siblingComparer;
        private Func<T, IComparer<T>?>? _siblingComparerSelector;
        private string? _childrenPropertyPath;
        private bool _autoExpandRoot;
        private int? _maxAutoExpandDepth;
        private bool _virtualizeChildren = true;
        private int? _maxDepth;
        private bool _treatGroupsAsNodes;
        private ExpandedStateKeyMode _expandedStateKeyMode = ExpandedStateKeyMode.Item;
        private Func<T, object?>? _expandedStateKeySelector;
        private Func<T, bool?>? _isExpandedSelector;
        private Action<T, bool>? _isExpandedSetter;
        private string? _isExpandedPropertyPath;
        private Func<T, IReadOnlyList<int>?>? _itemPathSelector;
        private bool _allowExpandToItemSearch;

        private HierarchicalOptions? _untyped;

        public Func<T, IEnumerable<T>?>? ChildrenSelector
        {
            get => _childrenSelector;
            set
            {
                _childrenSelector = value;
                Push();
            }
        }

        public Func<T, CancellationToken, Task<IEnumerable<T>?>>? ChildrenSelectorAsync
        {
            get => _childrenSelectorAsync;
            set
            {
                _childrenSelectorAsync = value;
                Push();
            }
        }

        public Func<T, IAsyncEnumerable<T>?>? ChildrenSelectorAsyncEnumerable
        {
            get => _childrenSelectorAsyncEnumerable;
            set
            {
                _childrenSelectorAsyncEnumerable = value;
                Push();
            }
        }

        public Func<T, IObservable<IEnumerable<T>?>>? ChildrenSelectorObservable
        {
            get => _childrenSelectorObservable;
            set
            {
                _childrenSelectorObservable = value;
                Push();
            }
        }

        /// <summary>
        /// Optional DynamicData-style change set selector. When provided, the observable should emit change set objects
        /// (any payload); the model will recompute children on each notification. Use when you need incremental updates
        /// without reassigning the children enumerable.
        /// </summary>
        public Func<T, IObservable<object?>?>? ChildrenChangeSetObservable
        {
            get => _childrenChangeSetObservable;
            set
            {
                _childrenChangeSetObservable = value;
                Push();
            }
        }

        public Func<T, IEnumerable<T>?>? ItemsSelector
        {
            get => _itemsSelector;
            set
            {
                _itemsSelector = value;
                Push();
            }
        }

        public Func<T, bool>? IsLeafSelector
        {
            get => _isLeafSelector;
            set
            {
                _isLeafSelector = value;
                Push();
            }
        }

        public IComparer<T>? SiblingComparer
        {
            get => _siblingComparer;
            set
            {
                _siblingComparer = value;
                Push();
            }
        }

        public Func<T, IComparer<T>?>? SiblingComparerSelector
        {
            get => _siblingComparerSelector;
            set
            {
                _siblingComparerSelector = value;
                Push();
            }
        }

        public string? ChildrenPropertyPath
        {
            get => _childrenPropertyPath;
            set
            {
                _childrenPropertyPath = value;
                Push();
            }
        }

        public bool AutoExpandRoot
        {
            get => _autoExpandRoot;
            set
            {
                _autoExpandRoot = value;
                Push();
            }
        }

        public int? MaxAutoExpandDepth
        {
            get => _maxAutoExpandDepth;
            set
            {
                _maxAutoExpandDepth = value;
                Push();
            }
        }

        public bool VirtualizeChildren
        {
            get => _virtualizeChildren;
            set
            {
                _virtualizeChildren = value;
                Push();
            }
        }

        public int? MaxDepth
        {
            get => _maxDepth;
            set
            {
                _maxDepth = value;
                Push();
            }
        }

        public bool TreatGroupsAsNodes
        {
            get => _treatGroupsAsNodes;
            set
            {
                _treatGroupsAsNodes = value;
                Push();
            }
        }

        public ExpandedStateKeyMode ExpandedStateKeyMode
        {
            get => _expandedStateKeyMode;
            set
            {
                _expandedStateKeyMode = value;
                Push();
            }
        }

        public Func<T, object?>? ExpandedStateKeySelector
        {
            get => _expandedStateKeySelector;
            set
            {
                _expandedStateKeySelector = value;
                Push();
            }
        }

        public Func<T, bool?>? IsExpandedSelector
        {
            get => _isExpandedSelector;
            set
            {
                _isExpandedSelector = value;
                Push();
            }
        }

        public Action<T, bool>? IsExpandedSetter
        {
            get => _isExpandedSetter;
            set
            {
                _isExpandedSetter = value;
                Push();
            }
        }

        public string? IsExpandedPropertyPath
        {
            get => _isExpandedPropertyPath;
            set
            {
                _isExpandedPropertyPath = value;
                Push();
            }
        }

        public Func<T, IReadOnlyList<int>?>? ItemPathSelector
        {
            get => _itemPathSelector;
            set
            {
                _itemPathSelector = value;
                Push();
            }
        }

        public bool AllowExpandToItemSearch
        {
            get => _allowExpandToItemSearch;
            set
            {
                _allowExpandToItemSearch = value;
                Push();
            }
        }

        internal HierarchicalOptions ToUntyped()
        {
            return EnsureUntyped();
        }

        internal HierarchicalOptions EnsureUntyped(HierarchicalOptions? untyped = null)
        {
            _untyped = untyped ?? new HierarchicalOptions();
            ApplyToUntyped(_untyped);
            return _untyped;
        }

        private void Push()
        {
            if (_untyped != null)
            {
                ApplyToUntyped(_untyped);
            }
        }

        private void ApplyToUntyped(HierarchicalOptions target)
        {
            target.AutoExpandRoot = AutoExpandRoot;
            target.MaxAutoExpandDepth = MaxAutoExpandDepth;
            target.VirtualizeChildren = VirtualizeChildren;
            target.MaxDepth = MaxDepth;
            target.TreatGroupsAsNodes = TreatGroupsAsNodes;
            target.ExpandedStateKeyMode = ExpandedStateKeyMode;
            target.ExpandedStateKeySelector = ExpandedStateKeySelector != null
                ? o => o is T typed ? ExpandedStateKeySelector(typed) : null
                : null;
            target.IsExpandedSelector = IsExpandedSelector != null
                ? o => o is T typed ? IsExpandedSelector(typed) : null
                : null;
            target.IsExpandedSetter = IsExpandedSetter != null
                ? (o, value) =>
                {
                    if (o is T typed)
                    {
                        IsExpandedSetter(typed, value);
                    }
                }
                : null;
            target.IsExpandedPropertyPath = IsExpandedPropertyPath;
            target.ItemPathSelector = ItemPathSelector != null
                ? o => o is T typed ? ItemPathSelector(typed) : null
                : null;
            target.AllowExpandToItemSearch = AllowExpandToItemSearch;
            target.ChildrenPropertyPath = ChildrenPropertyPath;
            target.ChildrenSelector = ChildrenSelector != null
                ? o => o is T typed ? ChildrenSelector(typed) : null
                : null;
            target.ChildrenSelectorAsync = ResolveAsyncSelector();
            target.ItemsSelector = ItemsSelector != null
                ? o => o is T typed ? ItemsSelector(typed) : null
                : null;
            target.IsLeafSelector = IsLeafSelector != null
                ? o => o is T typed && IsLeafSelector(typed)
                : null;
            target.SiblingComparer = SiblingComparer != null
                ? Comparer<object>.Create((x, y) =>
                {
                    if (x is T a && y is T b)
                    {
                        return SiblingComparer.Compare(a, b);
                    }

                    return 0;
                })
                : null;
            target.SiblingComparerSelector = SiblingComparerSelector != null
                ? o =>
                {
                    if (o is T typed)
                    {
                        var comparer = SiblingComparerSelector(typed);
                        if (comparer != null)
                        {
                            return Comparer<object>.Create((x, y) =>
                            {
                                if (x is T a && y is T b)
                                {
                                    return comparer.Compare(a, b);
                                }

                                return 0;
                            });
                        }
                    }

                    return null;
                }
                : null;
        }

        private Func<object, CancellationToken, Task<IEnumerable?>>? ResolveAsyncSelector()
        {
            if (ChildrenSelectorAsync != null)
            {
                return (o, ct) =>
                {
                    if (o is T typed)
                    {
                        return ChildrenSelectorAsync(typed, ct).ContinueWith(t => (IEnumerable?)t.Result, ct);
                    }

                    return Task.FromResult<IEnumerable?>(null);
                };
            }

            if (ChildrenSelectorAsyncEnumerable != null)
            {
                return async (o, ct) =>
                {
                    if (o is T typed)
                    {
                        var asyncEnumerable = ChildrenSelectorAsyncEnumerable(typed);
                        if (asyncEnumerable == null)
                        {
                            return null;
                        }

                        var list = new List<T>();
                        await foreach (var item in asyncEnumerable.WithCancellation(ct))
                        {
                            list.Add(item);
                        }

                        return list;
                    }

                    return null;
                };
            }

            if (ChildrenSelectorObservable != null)
            {
                return async (o, ct) =>
                {
                    if (o is T typed)
                    {
                        var observable = ChildrenSelectorObservable(typed);
                        if (observable == null)
                        {
                            return null;
                        }

                        var tcs = new TaskCompletionSource<IEnumerable?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var subscription = observable.Subscribe(new Observer<IEnumerable?>(tcs));

                        using (ct.Register(() => tcs.TrySetCanceled(ct)))
                        {
                            try
                            {
                                var result = await tcs.Task.ConfigureAwait(false);
                                return result;
                            }
                            finally
                            {
                                subscription.Dispose();
                            }
                        }
                    }

                    return null;
                };
            }

            if (ChildrenChangeSetObservable != null)
            {
                return async (o, ct) =>
                {
                    if (o is T typed)
                    {
                        var observable = ChildrenChangeSetObservable(typed);
                        if (observable == null)
                        {
                            return null;
                        }

                        var collection = new ObservableCollection<T>();
                        var tcs = new TaskCompletionSource<IEnumerable?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var subscription = observable.Subscribe(new ChangeSetObserver<T>(typed, collection, this, tcs));

                        using (ct.Register(() => tcs.TrySetCanceled(ct)))
                        {
                            try
                            {
                                var result = await tcs.Task.ConfigureAwait(false);
                                return result;
                            }
                            finally
                            {
                                subscription.Dispose();
                            }
                        }
                    }

                    return null;
                };
            }

            return null;
        }

        private sealed class Observer<TValue> : IObserver<TValue>
        {
            private readonly TaskCompletionSource<IEnumerable?> _tcs;

            public Observer(TaskCompletionSource<IEnumerable?> tcs)
            {
                _tcs = tcs;
            }

            public void OnCompleted()
            {
                _tcs.TrySetResult(null);
            }

            public void OnError(Exception error)
            {
                _tcs.TrySetException(error);
            }

            public void OnNext(TValue value)
            {
                if (value is IEnumerable enumerable)
                {
                    _tcs.TrySetResult(enumerable);
                }
                else
                {
                    _tcs.TrySetResult(null);
                }
            }
        }

        private sealed class ChangeSetObserver<TValue> : IObserver<object?>
        {
            private readonly T _owner;
            private readonly ObservableCollection<TValue> _collection;
            private readonly HierarchicalOptions<T> _options;
            private readonly TaskCompletionSource<IEnumerable?> _tcs;
            private bool _hasValue;

            public ChangeSetObserver(T owner, ObservableCollection<TValue> collection, HierarchicalOptions<T> options, TaskCompletionSource<IEnumerable?> tcs)
            {
                _owner = owner;
                _collection = collection;
                _options = options;
                _tcs = tcs;
            }

            public void OnCompleted()
            {
                if (!_hasValue)
                {
                    _tcs.TrySetResult(Array.Empty<TValue>());
                }
            }

            public void OnError(Exception error)
            {
                _tcs.TrySetException(error);
            }

            public void OnNext(object? changeSet)
            {
                // We don't have a concrete IChangeSet dependency; treat each notification as "recompute children".
                // Callers should re-run their own projection and assign via ChildrenSelector/observable, but here we
                // allow them to push via ItemsSelector fallback.
                if (_options.ChildrenSelector != null)
                {
                    var result = _options.ChildrenSelector(_owner);
                    ReplaceWith(result);
                }

                _hasValue = true;
                _tcs.TrySetResult(_collection);
            }

            private void ReplaceWith(IEnumerable? items)
            {
                _collection.Clear();
                if (items == null)
                {
                    return;
                }

                foreach (var item in items)
                {
                    if (item is TValue typed)
                    {
                        _collection.Add(typed);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Strongly-typed hierarchical model wrapper to make MVVM/DynamicData pipelines easier.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class HierarchicalModel<T> : HierarchicalModel, IHierarchicalModel<T>
    {
        public HierarchicalModel(HierarchicalOptions<T>? options = null)
            : base(PrepareOptions(options ??= new HierarchicalOptions<T>()))
        {
            TypedOptions = options;
            ObservableFlattened = new ProjectedObservableNodes<T>(base.ObservableFlattened);
            WireTypedEvents();
        }

        private static HierarchicalOptions PrepareOptions(HierarchicalOptions<T> typed)
        {
            return typed.EnsureUntyped();
        }

        private void WireTypedEvents()
        {
            NodeExpanded += (_, e) => NodeExpandedTyped?.Invoke(this, new HierarchicalNodeEventArgs<T>(new HierarchicalNode<T>(e.Node)));
            NodeCollapsed += (_, e) => NodeCollapsedTyped?.Invoke(this, new HierarchicalNodeEventArgs<T>(new HierarchicalNode<T>(e.Node)));
            NodeLoading += (_, e) => NodeLoadingTyped?.Invoke(this, new HierarchicalNodeEventArgs<T>(new HierarchicalNode<T>(e.Node)));
            NodeLoaded += (_, e) => NodeLoadedTyped?.Invoke(this, new HierarchicalNodeEventArgs<T>(new HierarchicalNode<T>(e.Node)));
            NodeLoadFailed += (_, e) => NodeLoadFailedTyped?.Invoke(this, new HierarchicalNodeLoadFailedEventArgs<T>(new HierarchicalNode<T>(e.Node), e.Error));
            NodeLoadRetryScheduled += (_, e) => NodeLoadRetryScheduledTyped?.Invoke(this, new HierarchicalNodeRetryEventArgs<T>(new HierarchicalNode<T>(e.Node), e.Delay));
            HierarchyChanged += (_, e) => HierarchyChangedTyped?.Invoke(this, new HierarchyChangedEventArgs<T>(new HierarchicalNode<T>(e.Node), e.Action));
            FlattenedChanged += (_, e) => FlattenedChangedTyped?.Invoke(this, new FlattenedChangedEventArgs<T>(e, ObservableFlattened));
        }

        public HierarchicalOptions<T> TypedOptions { get; }

        public new int Count => base.Count;

        public new int FlattenedVersion => base.FlattenedVersion;

        public new IDisposable BeginVirtualizationGuard() => base.BeginVirtualizationGuard();

        public new HierarchicalNode<T>? Root => base.Root != null ? new HierarchicalNode<T>(base.Root) : null;

        public new IReadOnlyList<HierarchicalNode<T>> Flattened => new TypedNodeList(base.Flattened);

        public new IReadOnlyList<HierarchicalNode<T>> ObservableFlattened { get; }

        public event EventHandler<HierarchicalNodeEventArgs<T>>? NodeExpandedTyped;

        public event EventHandler<HierarchicalNodeEventArgs<T>>? NodeCollapsedTyped;

        public event EventHandler<HierarchicalNodeEventArgs<T>>? NodeLoadingTyped;

        public event EventHandler<HierarchicalNodeEventArgs<T>>? NodeLoadedTyped;

        public event EventHandler<HierarchicalNodeLoadFailedEventArgs<T>>? NodeLoadFailedTyped;

        public event EventHandler<HierarchicalNodeRetryEventArgs<T>>? NodeLoadRetryScheduledTyped;

        public event EventHandler<HierarchyChangedEventArgs<T>>? HierarchyChangedTyped;

        public event EventHandler<FlattenedChangedEventArgs<T>>? FlattenedChangedTyped;

        public void SetRoot(T rootItem) => base.SetRoot(rootItem!);

        public void SetRoots(IEnumerable<T> rootItems) => base.SetRoots(rootItems.Cast<object>());

        public HierarchicalNode<T> GetTypedNode(int index) => new HierarchicalNode<T>(base.GetNode(index));

        public HierarchicalNode<T>? FindNode(T item)
        {
            var node = base.FindNode(item!);
            return node != null ? new HierarchicalNode<T>(node) : null;
        }

        public int IndexOf(T item) => base.IndexOf(item!);

        public void Expand(HierarchicalNode<T> node) => base.Expand(node.Inner);

        public void Expand(IEnumerable<T> items) => base.Expand(items);

        public Task ExpandAsync(HierarchicalNode<T> node, CancellationToken cancellationToken = default) =>
            base.ExpandAsync(node.Inner, cancellationToken);

        public Task ExpandAsync(IEnumerable<T> items, CancellationToken cancellationToken = default) =>
            base.ExpandAsync(items, cancellationToken);

        public void Collapse(HierarchicalNode<T> node) => base.Collapse(node.Inner);

        public void Collapse(IEnumerable<T> items) => base.Collapse(items);

        public Task CollapseAsync(IEnumerable<T> items, CancellationToken cancellationToken = default) =>
            base.CollapseAsync(items, cancellationToken);

        public void Toggle(HierarchicalNode<T> node) => base.Toggle(node.Inner);

        public Task ToggleAsync(HierarchicalNode<T> node, CancellationToken cancellationToken = default) =>
            base.ToggleAsync(node.Inner, cancellationToken);

        public void Refresh(HierarchicalNode<T>? node = null) => base.Refresh(node?.Inner);

        public Task RefreshAsync(HierarchicalNode<T>? node = null, CancellationToken cancellationToken = default) =>
            base.RefreshAsync(node?.Inner, cancellationToken);

        public void ExpandAll(HierarchicalNode<T>? node = null, int? maxDepth = null) =>
            base.ExpandAll(node?.Inner, maxDepth);

        public void CollapseAll(HierarchicalNode<T>? node = null, int? minDepth = null) =>
            base.CollapseAll(node?.Inner, minDepth);

        public void Sort(HierarchicalNode<T>? node = null, IComparer<T>? comparer = null, bool recursive = true)
        {
            IComparer<object>? untyped = null;
            if (comparer != null)
            {
                untyped = Comparer<object>.Create((x, y) =>
                {
                    if (x is T a && y is T b)
                    {
                        return comparer.Compare(a, b);
                    }

                    return 0;
                });
            }

            base.Sort(node?.Inner, untyped, recursive);
        }

        public void ApplySiblingComparer(IComparer<T>? comparer, bool recursive = true)
        {
            TypedOptions.SiblingComparer = comparer;

            IComparer<object>? untyped = null;
            if (comparer != null)
            {
                untyped = Comparer<object>.Create((x, y) =>
                {
                    if (x is T a && y is T b)
                    {
                        return comparer.Compare(a, b);
                    }

                    return 0;
                });
            }

            base.ApplySiblingComparer(untyped, recursive);
        }

        private sealed class TypedNodeList : IReadOnlyList<HierarchicalNode<T>>
        {
            private readonly IReadOnlyList<HierarchicalNode> _inner;

            public TypedNodeList(IReadOnlyList<HierarchicalNode> inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public HierarchicalNode<T> this[int index] => new HierarchicalNode<T>(_inner[index]);

            public int Count => _inner.Count;

            public IEnumerator<HierarchicalNode<T>> GetEnumerator()
            {
                foreach (var node in _inner)
                {
                    yield return new HierarchicalNode<T>(node);
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    /// <summary>
    /// Typed view over an existing hierarchical node.
    /// </summary>
    /// <typeparam name="T">Item type.</typeparam>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    readonly struct HierarchicalNode<T> : IHierarchicalNodeItem
    {
        internal HierarchicalNode(HierarchicalNode inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal HierarchicalNode Inner { get; }

        public T Item => Inner.Item is T typed
            ? typed
            : throw new InvalidCastException($"Node item is not of type {typeof(T).FullName}.");

        object IHierarchicalNodeItem.Item => Inner.Item;

        public HierarchicalNode<T>? Parent => Inner.Parent != null ? new HierarchicalNode<T>(Inner.Parent) : null;

        public IReadOnlyList<HierarchicalNode<T>> Children => new ReadOnlyCollection<HierarchicalNode<T>>(
            Inner.Children.Select(child => new HierarchicalNode<T>(child)).ToList());

        public bool IsExpanded => Inner.IsExpanded;

        public bool IsLeaf => Inner.IsLeaf;

        public int Level => Inner.Level;

        public bool IsLoading => Inner.IsLoading;

        public int ExpandedCount => Inner.ExpandedCount;

        public Exception? LoadError => Inner.LoadError;

        public override bool Equals(object? obj)
        {
            return obj is HierarchicalNode<T> other && ReferenceEquals(Inner, other.Inner);
        }

        public override int GetHashCode() => Inner.GetHashCode();

        public static bool operator ==(HierarchicalNode<T> left, HierarchicalNode<T> right) => left.Equals(right);

        public static bool operator !=(HierarchicalNode<T> left, HierarchicalNode<T> right) => !left.Equals(right);
    }

    internal sealed class ProjectedObservableNodes<T> : IReadOnlyList<HierarchicalNode<T>>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private readonly ReadOnlyObservableCollection<HierarchicalNode> _source;

        public ProjectedObservableNodes(ReadOnlyObservableCollection<HierarchicalNode> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            ((INotifyCollectionChanged)_source).CollectionChanged += OnSourceCollectionChanged;
            ((INotifyPropertyChanged)_source).PropertyChanged += OnSourcePropertyChanged;
        }

        public HierarchicalNode<T> this[int index] => new HierarchicalNode<T>(_source[index]);

        public int Count => _source.Count;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public event PropertyChangedEventHandler? PropertyChanged;

        public IEnumerator<HierarchicalNode<T>> GetEnumerator()
        {
            foreach (var node in _source)
            {
                yield return new HierarchicalNode<T>(node);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyCollectionChangedEventArgs projected = e.Action switch
            {
                NotifyCollectionChangedAction.Add => new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    Project(e.NewItems),
                    e.NewStartingIndex),
                NotifyCollectionChangedAction.Remove => new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Remove,
                    Project(e.OldItems),
                    e.OldStartingIndex),
                NotifyCollectionChangedAction.Replace => new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Replace,
                    Project(e.NewItems),
                    Project(e.OldItems),
                    e.OldStartingIndex),
                NotifyCollectionChangedAction.Move => new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Move,
                    Project(e.NewItems),
                    e.NewStartingIndex,
                    e.OldStartingIndex),
                _ => new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
            };

            CollectionChanged?.Invoke(this, projected);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
        }

        private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);
        }

        private void OnPropertyChanged(string? propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static IList Project(IList? items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<HierarchicalNode<T>>();
            }

            var result = new List<HierarchicalNode<T>>(items.Count);
            foreach (var item in items)
            {
                if (item is HierarchicalNode node)
                {
                    result.Add(new HierarchicalNode<T>(node));
                }
            }

            return result;
        }
    }
}
