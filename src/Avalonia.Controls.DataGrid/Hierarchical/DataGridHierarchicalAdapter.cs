// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Avalonia.Controls.DataGridHierarchical
{
    /// <summary>
    /// Factory to create a hierarchical adapter without subclassing <see cref="DataGrid"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridHierarchicalAdapterFactory
    {
        DataGridHierarchicalAdapter Create(DataGrid grid, IHierarchicalModel model);
    }

    /// <summary>
    /// Factory to create a strongly-typed hierarchical adapter without subclassing <see cref="DataGrid"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridHierarchicalAdapterFactory<T> : IDataGridHierarchicalAdapterFactory
    {
        DataGridHierarchicalAdapter<T> Create(DataGrid grid, IHierarchicalModel<T> model);
    }

    /// <summary>
    /// Factory to create a strongly-typed hierarchical model without subclassing <see cref="DataGrid"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridHierarchicalModelFactory<T> : IDataGridHierarchicalModelFactory
    {
        new IHierarchicalModel<T> Create();
    }

    internal static class DataGridHierarchicalAdapterFactoryExtensions
    {
        public static DataGridHierarchicalAdapter CreateFromUntyped<T>(
            this IDataGridHierarchicalAdapterFactory<T> factory,
            DataGrid grid,
            IHierarchicalModel model)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (model is not IHierarchicalModel<T> typed)
            {
                throw new ArgumentException("Model is not compatible with the typed factory.", nameof(model));
            }

            return factory.Create(grid, typed).InnerAdapter;
        }
    }

    /// <summary>
    /// Bridges a hierarchical model to the DataGrid by exposing flattened accessors and gestures.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class DataGridHierarchicalAdapter : IDisposable
    {
        private readonly IHierarchicalModel _model;
        private readonly Action<FlattenedChangedEventArgs>? _flattenedChangedCallback;
        private IDisposable? _virtualizationGuard;
        private DispatcherTimer? _guardTimer;
        private static readonly TimeSpan GuardDebounce = TimeSpan.FromMilliseconds(50);

        public DataGridHierarchicalAdapter(
            IHierarchicalModel model,
            Action<FlattenedChangedEventArgs>? flattenedChangedCallback = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _flattenedChangedCallback = flattenedChangedCallback;

            _model.FlattenedChanged += OnModelFlattenedChanged;
        }

        public IHierarchicalModel Model => _model;

        public int Count => _model.Count;

        public object? ItemAt(int index) => _model.GetItem(index);

        public HierarchicalNode NodeAt(int index) => _model.GetNode(index);

        public int LevelAt(int index) => _model.GetNode(index).Level;

        public bool IsExpandable(int index)
        {
            var node = _model.GetNode(index);
            return !node.IsLeaf;
        }

        public bool IsExpanded(int index) => _model.GetNode(index).IsExpanded;

        public void Toggle(int index)
        {
            BeginDebouncedVirtualizationGuard();
            _model.Toggle(_model.GetNode(index));
        }

        public void Expand(int index)
        {
            BeginDebouncedVirtualizationGuard();
            _model.Expand(_model.GetNode(index));
        }

        public void Collapse(int index)
        {
            BeginDebouncedVirtualizationGuard();
            _model.Collapse(_model.GetNode(index));
        }

        public int IndexOfNode(HierarchicalNode node) => _model.IndexOf(node);

        public int IndexOfItem(object item) => _model.IndexOf(item);

        public int IndexOf(object item)
        {
            return IndexOfItem(item);
        }

        public event EventHandler<FlattenedChangedEventArgs>? FlattenedChanged;

        public void Dispose()
        {
            _model.FlattenedChanged -= OnModelFlattenedChanged;
            EndDebouncedVirtualizationGuard();

            if (_guardTimer != null)
            {
                _guardTimer.Tick -= GuardTimer_Tick;
                _guardTimer.Stop();
                _guardTimer = null;
            }
        }

        public void SetRoot(object root)
        {
            _model.SetRoot(root);
        }

        public void Sort(HierarchicalNode? node = null, IComparer<object>? comparer = null, bool recursive = true)
        {
            _model.Sort(node, comparer, recursive);
        }

        public void ApplySiblingComparer(IComparer<object>? comparer, bool recursive = true)
        {
            _model.ApplySiblingComparer(comparer, recursive);
        }

        public void ExpandAll(HierarchicalNode? node = null, int? maxDepth = null)
        {
            BeginDebouncedVirtualizationGuard();
            _model.ExpandAll(node, maxDepth);
        }

        public void CollapseAll(HierarchicalNode? node = null, int? minDepth = null)
        {
            BeginDebouncedVirtualizationGuard();
            _model.CollapseAll(node, minDepth);
        }

        private void OnModelFlattenedChanged(object? sender, FlattenedChangedEventArgs e)
        {
            FlattenedChanged?.Invoke(this, e);
            _flattenedChangedCallback?.Invoke(e);
        }

        private void BeginDebouncedVirtualizationGuard()
        {
            _virtualizationGuard ??= _model.BeginVirtualizationGuard();

            if (_guardTimer == null)
            {
                _guardTimer = new DispatcherTimer { Interval = GuardDebounce };
                _guardTimer.Tick += GuardTimer_Tick;
            }

            _guardTimer.Stop();
            _guardTimer.Start();
        }

        private void GuardTimer_Tick(object? sender, EventArgs e)
        {
            EndDebouncedVirtualizationGuard();
        }

        private void EndDebouncedVirtualizationGuard()
        {
            _guardTimer?.Stop();
            _virtualizationGuard?.Dispose();
            _virtualizationGuard = null;
        }
    }

    /// <summary>
    /// Typed adapter that wraps <see cref="DataGridHierarchicalAdapter"/> to remove casts for <typeparamref name="T"/>.
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class DataGridHierarchicalAdapter<T> : IDisposable
    {
        private readonly IHierarchicalModel<T> _model;
        private readonly DataGridHierarchicalAdapter _inner;

        public DataGridHierarchicalAdapter(
            IHierarchicalModel<T> model,
            Action<FlattenedChangedEventArgs<T>>? flattenedChangedCallback = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _inner = new DataGridHierarchicalAdapter(model, e =>
            {
                var typedArgs = new FlattenedChangedEventArgs<T>(e, _model.ObservableFlattened);
                FlattenedChanged?.Invoke(this, typedArgs);
                flattenedChangedCallback?.Invoke(typedArgs);
            });
        }

        public IHierarchicalModel<T> Model => _model;

        public int Count => _inner.Count;

        internal DataGridHierarchicalAdapter InnerAdapter => _inner;

        public T ItemAt(int index) => _model.GetTypedNode(index).Item;

        public HierarchicalNode<T> NodeAt(int index) => _model.GetTypedNode(index);

        public int LevelAt(int index) => NodeAt(index).Level;

        public bool IsExpandable(int index) => !NodeAt(index).IsLeaf;

        public bool IsExpanded(int index) => NodeAt(index).IsExpanded;

        public void Toggle(int index)
        {
            _inner.Toggle(index);
        }

        public Task ToggleAsync(int index, CancellationToken cancellationToken = default)
        {
            return _model.ToggleAsync(_model.GetTypedNode(index), cancellationToken);
        }

        public void Expand(int index)
        {
            _inner.Expand(index);
        }

        public Task ExpandAsync(int index, CancellationToken cancellationToken = default)
        {
            return _model.ExpandAsync(_model.GetTypedNode(index), cancellationToken);
        }

        public void Collapse(int index)
        {
            _inner.Collapse(index);
        }

        public int IndexOfNode(HierarchicalNode<T> node) => _model.IndexOf(node.Inner);

        public int IndexOfItem(T item) => _model.IndexOf(item);

        public int IndexOf(object item) => _model.IndexOf(item);

        public event EventHandler<FlattenedChangedEventArgs<T>>? FlattenedChanged;

        public void Dispose()
        {
            _inner.Dispose();
        }

        public void SetRoot(T root) => _model.SetRoot(root);

        public void Sort(HierarchicalNode<T>? node = null, IComparer<T>? comparer = null, bool recursive = true)
        {
            _model.Sort(node, comparer, recursive);
        }

        public Task RefreshAsync(HierarchicalNode<T>? node = null, CancellationToken cancellationToken = default)
        {
            return _model.RefreshAsync(node, cancellationToken);
        }

        public void ApplySiblingComparer(IComparer<T>? comparer, bool recursive = true)
        {
            _model.ApplySiblingComparer(comparer, recursive);
        }

        public void ExpandAll(HierarchicalNode<T>? node = null, int? maxDepth = null)
        {
            _inner.ExpandAll(node?.Inner, maxDepth);
        }

        public void CollapseAll(HierarchicalNode<T>? node = null, int? minDepth = null)
        {
            _inner.CollapseAll(node?.Inner, minDepth);
        }
    }
}
