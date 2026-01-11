// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Input;
using Avalonia.Threading;

namespace Avalonia.Controls.DataGridSorting
{
    /// <summary>
    /// Bridges sorting gestures/model to the view's SortDescriptions.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridSortingAdapter : IDisposable
    {
        private readonly ISortingModel _model;
        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private readonly DataGridFastPathOptions _options;
        private readonly bool _useAccessorsOnly;
        private readonly bool _throwOnMissingAccessor;
        private IDataGridCollectionView _view;
        private bool _suppressViewSync;
        private bool _suppressModelSync;
        private Action _beforeViewRefresh;
        private Action _afterViewRefresh;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridSortingAdapter(
            ISortingModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            DataGridFastPathOptions options,
            Action beforeViewRefresh = null,
            Action afterViewRefresh = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));
            _options = options;
            _useAccessorsOnly = options?.UseAccessorsOnly ?? false;
            _throwOnMissingAccessor = options?.ThrowOnMissingAccessor ?? false;
            _beforeViewRefresh = beforeViewRefresh;
            _afterViewRefresh = afterViewRefresh;

            _model.SortingChanged += OnModelSortingChanged;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridSortingAdapter(
            ISortingModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            Action beforeViewRefresh = null,
            Action afterViewRefresh = null)
            : this(model, columnProvider, options: null, beforeViewRefresh, afterViewRefresh)
        {
        }

        internal void AttachLifecycle(Action beforeViewRefresh, Action afterViewRefresh)
        {
            _beforeViewRefresh = beforeViewRefresh;
            _afterViewRefresh = afterViewRefresh;
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IDataGridCollectionView View => _view;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void AttachView(IDataGridCollectionView view)
        {
            if (ReferenceEquals(_view, view))
            {
                return;
            }

            DetachView();
            _view = view;

            if (_view != null)
            {
                _view.SortDescriptions.CollectionChanged += OnViewSortDescriptionsChanged;

                if (_model.OwnsViewSorts)
                {
                    ApplyModelToView(_model.Descriptors);
                }
                else
                {
                    SyncModelFromView();
                }
            }
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void HandleHeaderClick(DataGridColumn column, KeyModifiers modifiers, ListSortDirection? forcedDirection = null)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            var descriptor = CreateDescriptor(column, forcedDirection ?? ListSortDirection.Ascending);
            if (descriptor == null)
            {
                return;
            }

            var sortingModifiers = SortingModifiers.None;
            if (modifiers.HasFlag(KeyModifiers.Shift))
            {
                sortingModifiers |= SortingModifiers.Multi;
            }
            if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
            {
                sortingModifiers |= SortingModifiers.Clear;
            }

            _model.Toggle(descriptor, sortingModifiers);
        }

        public void Dispose()
        {
            DetachView();
            _model.SortingChanged -= OnModelSortingChanged;
        }

        private void DetachView()
        {
            if (_view != null)
            {
                _view.SortDescriptions.CollectionChanged -= OnViewSortDescriptionsChanged;
                _view = null;
            }
        }

        public void RefreshOwnership()
        {
            if (_view == null)
            {
                return;
            }

            if (_model.OwnsViewSorts)
            {
                ApplyModelToView(_model.Descriptors);
            }
            else
            {
                SyncModelFromView();
            }
        }

        private void OnModelSortingChanged(object sender, SortingChangedEventArgs e)
        {
            if (_suppressModelSync)
            {
                return;
            }

            ApplyModelToView(e.NewDescriptors, e.OldDescriptors);
        }

        private void OnViewSortDescriptionsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressViewSync || _view == null)
            {
                return;
            }

            SyncModelFromView();
            if (_afterViewRefresh != null)
            {
                Dispatcher.UIThread.Post(() => _afterViewRefresh());
            }
        }

        private bool ApplyModelToView(IReadOnlyList<SortingDescriptor> descriptors, IReadOnlyList<SortingDescriptor> previousDescriptors = null)
        {
            if (_view == null)
            {
                return false;
            }

            var beforeInvoked = false;
            void EnsureBeforeViewRefresh()
            {
                if (!beforeInvoked)
                {
                    _beforeViewRefresh?.Invoke();
                    beforeInvoked = true;
                }
            }

            EnsureBeforeViewRefresh();

            var effectiveDescriptors = PrepareDescriptorsForFastPath(descriptors);
            var effectivePreviousDescriptors = PrepareDescriptorsForFastPath(previousDescriptors);

            if (TryApplyModelToView(effectiveDescriptors, effectivePreviousDescriptors, out var handledChanged))
            {
                if (handledChanged)
                {
                    _afterViewRefresh?.Invoke();
                }

                return handledChanged;
            }

            _suppressViewSync = true;
            bool changed = false;
            try
            {
                var targetSorts = (IReadOnlyList<DataGridSortDescription>)(BuildSortDescriptions(effectiveDescriptors) ?? new List<DataGridSortDescription>());
                if (SortsEqual(_view.SortDescriptions, targetSorts))
                {
                    return false;
                }

                EnsureBeforeViewRefresh();

                var rollback = BuildSortDescriptions(effectivePreviousDescriptors) ?? _view.SortDescriptions.ToList();

                try
                {
                    using (_view.DeferRefresh())
                    {
                        _view.SortDescriptions.Clear();

                        foreach (var sortDescription in targetSorts)
                        {
                            _view.SortDescriptions.Add(sortDescription);
                        }

                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    LogInvalidSort($"Applying sort descriptors failed; reverting. {ex}");
                    RestoreSortDescriptions(rollback);

                    if (previousDescriptors != null)
                    {
                        _suppressModelSync = true;
                        try
                        {
                            _model.Apply(previousDescriptors);
                        }
                        finally
                        {
                            _suppressModelSync = false;
                        }
                    }
                }
            }
            finally
            {
                _suppressViewSync = false;
                if (changed)
                {
                    _afterViewRefresh?.Invoke();
                }
            }

            return changed;
        }

        /// <summary>
        /// Override to short-circuit default sort application (e.g., push descriptors upstream).
        /// Return true to indicate the call was handled; set <paramref name="changed"/> to true
        /// if the view was refreshed.
        /// </summary>
        /// <remarks>
        /// This is intended for advanced scenarios (DynamicData/server-side sorting) where
        /// the adapter should not rewrite <see cref="IDataGridCollectionView.SortDescriptions"/>.
        /// </remarks>
        protected virtual bool TryApplyModelToView(
            IReadOnlyList<SortingDescriptor> descriptors,
            IReadOnlyList<SortingDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = false;
            return false;
        }

        private void SyncModelFromView()
        {
            if (_view == null)
            {
                return;
            }

            var descriptors = new List<SortingDescriptor>();
            var seen = new HashSet<object>();
            foreach (var sort in _view.SortDescriptions)
            {
                var descriptor = ToSortingDescriptor(sort);
                if (descriptor != null)
                {
                    if (seen.Add(descriptor.ColumnId))
                    {
                        descriptors.Add(descriptor);
                    }
                    else
                    {
                        LogInvalidSort("Ignoring duplicate sort descriptor for the same column.");
                    }
                }
            }

            descriptors = PrepareDescriptorsForFastPath(descriptors)?.ToList() ?? new List<SortingDescriptor>();

            _suppressModelSync = true;
            try
            {
                _model.Apply(descriptors);
            }
            finally
            {
                _suppressModelSync = false;
            }
        }

        private SortingDescriptor CreateDescriptor(DataGridColumn column, ListSortDirection direction)
        {
            var columnId = DataGridColumnMetadata.GetColumnId(column);
            var culture = _view?.Culture ?? CultureInfo.InvariantCulture;
            if (column.CustomSortComparer != null)
            {
                return new SortingDescriptor(columnId, direction, comparer: column.CustomSortComparer, culture: culture);
            }

            var directionalComparer = GetDirectionalComparer(column, direction);
            if (directionalComparer != null)
            {
                return new SortingDescriptor(columnId, direction, comparer: directionalComparer, culture: culture);
            }

            var sortValueComparer = DataGridColumnSort.GetValueComparer(column);
            var sortAccessor = DataGridColumnSort.GetValueAccessor(column);
            if (sortAccessor != null || sortValueComparer != null)
            {
                var resolvedAccessor = sortAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
                if (resolvedAccessor != null)
                {
                    return new SortingDescriptor(
                        columnId,
                        direction,
                        propertyPath: column.GetSortPropertyName(),
                        comparer: DataGridColumnValueAccessorComparer.Create(resolvedAccessor, culture, sortValueComparer),
                        culture: culture);
                }
            }

            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null)
            {
                return new SortingDescriptor(
                    columnId,
                    direction,
                    propertyPath: column.GetSortPropertyName(),
                    comparer: DataGridColumnValueAccessorComparer.Create(accessor, culture),
                    culture: culture);
            }

            if (_useAccessorsOnly)
            {
                HandleMissingAccessor(
                    column,
                    columnId,
                    $"Sorting requires a value accessor for column '{column.Header}'.");
                return null;
            }

            var propertyPath = column.GetSortPropertyName();
            if (string.IsNullOrEmpty(propertyPath))
            {
                LogInvalidSort($"Cannot sort column '{column.Header}' because no sort path was found.");
                return null;
            }

            return new SortingDescriptor(columnId, direction, propertyPath, culture: culture);
        }

        private DataGridSortDescription ToSortDescription(SortingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            var column = FindColumnById(descriptor.ColumnId);

            if (descriptor.HasComparer)
            {
                if (descriptor.Comparer is IDataGridColumnValueAccessorComparer)
                {
                    var propertyPath = descriptor.PropertyPath;
                    if (string.IsNullOrEmpty(propertyPath) && column != null)
                    {
                        propertyPath = column.GetSortPropertyName();
                    }

                    return !string.IsNullOrEmpty(propertyPath)
                        ? DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction, propertyPath)
                        : DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction);
                }

                return DataGridSortDescription.FromComparer(descriptor.Comparer, descriptor.Direction);
            }

            if (column != null)
            {
                if (column.CustomSortComparer != null)
                {
                    return DataGridSortDescription.FromComparer(column.CustomSortComparer, descriptor.Direction);
                }

                var directionalComparer = GetDirectionalComparer(column, descriptor.Direction);
                if (directionalComparer != null)
                {
                    return DataGridSortDescription.FromComparer(directionalComparer, descriptor.Direction);
                }

                var sortValueComparer = DataGridColumnSort.GetValueComparer(column);
                var sortAccessor = DataGridColumnSort.GetValueAccessor(column);
                if (sortAccessor != null || sortValueComparer != null)
                {
                    var resolvedAccessor = sortAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
                    if (resolvedAccessor != null)
                    {
                        var culture = descriptor.Culture ?? _view?.Culture ?? CultureInfo.InvariantCulture;
                        var comparer = DataGridColumnValueAccessorComparer.Create(resolvedAccessor, culture, sortValueComparer);
                        var propertyPath = descriptor.PropertyPath;
                        if (string.IsNullOrEmpty(propertyPath))
                        {
                            propertyPath = column.GetSortPropertyName();
                        }

                        return !string.IsNullOrEmpty(propertyPath)
                            ? DataGridSortDescription.FromComparer(comparer, descriptor.Direction, propertyPath)
                            : DataGridSortDescription.FromComparer(comparer, descriptor.Direction);
                    }
                }

                var accessor = DataGridColumnMetadata.GetValueAccessor(column);
                if (accessor != null)
                {
                    var culture = descriptor.Culture ?? _view?.Culture ?? CultureInfo.InvariantCulture;
                    var comparer = DataGridColumnValueAccessorComparer.Create(accessor, culture);
                    var propertyPath = descriptor.PropertyPath;
                    if (string.IsNullOrEmpty(propertyPath))
                    {
                        propertyPath = column.GetSortPropertyName();
                    }

                    return !string.IsNullOrEmpty(propertyPath)
                        ? DataGridSortDescription.FromComparer(comparer, descriptor.Direction, propertyPath)
                        : DataGridSortDescription.FromComparer(comparer, descriptor.Direction);
                }
            }

            if (descriptor.HasPropertyPath)
            {
                if (_useAccessorsOnly)
                {
                    HandleMissingAccessor(
                        column,
                        descriptor.ColumnId,
                        "Sorting requires a value accessor but no column could be resolved.");
                    return null;
                }

                return DataGridSortDescription.FromPath(descriptor.PropertyPath, descriptor.Direction, descriptor.Culture);
            }

            return null;
        }

        private SortingDescriptor ToSortingDescriptor(DataGridSortDescription sort)
        {
            if (sort == null)
            {
                return null;
            }

            var column = FindColumnForSort(sort);
            if (sort is DataGridComparerSortDescription comparerSort)
            {
                var definition = column != null ? DataGridColumnMetadata.GetDefinition(column) : null;
                var definitionKey = DataGridColumnMetadata.GetDefinitionKey(definition);
                var id = (object)definitionKey ?? (object)column ?? (object)comparerSort.SourceComparer ?? (object)sort;
                return new SortingDescriptor(id, comparerSort.Direction, sort.PropertyPath, comparerSort.SourceComparer, _view?.Culture);
            }

            var propertyPath = sort.PropertyPath;
            var columnDefinition = column != null ? DataGridColumnMetadata.GetDefinition(column) : null;
            var columnDefinitionKey = DataGridColumnMetadata.GetDefinitionKey(columnDefinition);
            var columnId = (object)columnDefinitionKey ?? (object)column ?? (!string.IsNullOrEmpty(propertyPath) ? (object)propertyPath : sort);
            return new SortingDescriptor(columnId, sort.Direction, propertyPath, culture: _view?.Culture);
        }

        private IReadOnlyList<SortingDescriptor> PrepareDescriptorsForFastPath(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (!_useAccessorsOnly || descriptors == null || descriptors.Count == 0)
            {
                return descriptors;
            }

            var prepared = new List<SortingDescriptor>(descriptors.Count);
            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    continue;
                }

                if (descriptor.HasComparer)
                {
                    prepared.Add(descriptor);
                    continue;
                }

                var column = FindColumnForDescriptor(descriptor);
                if (column != null && TryBuildComparer(column, descriptor.Direction, descriptor.Culture, out var comparer))
                {
                    var propertyPath = !string.IsNullOrEmpty(descriptor.PropertyPath)
                        ? descriptor.PropertyPath
                        : column.GetSortPropertyName();
                    prepared.Add(new SortingDescriptor(descriptor.ColumnId, descriptor.Direction, propertyPath, comparer, descriptor.Culture));
                    continue;
                }

                HandleMissingAccessor(
                    column,
                    descriptor.ColumnId,
                    "Sorting requires a value accessor but no column could be resolved.");
            }

            return prepared;
        }

        private DataGridColumn FindColumnForDescriptor(SortingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            foreach (var column in EnumerateColumns())
            {
                if (DataGridColumnMetadata.MatchesColumnId(column, descriptor.ColumnId))
                {
                    return column;
                }

                if (!string.IsNullOrEmpty(descriptor.PropertyPath))
                {
                    var propertyName = column.GetSortPropertyName();
                    if (!string.IsNullOrEmpty(propertyName) &&
                        string.Equals(propertyName, descriptor.PropertyPath, StringComparison.Ordinal))
                    {
                        return column;
                    }
                }
            }

            return null;
        }

        private IComparer GetDirectionalComparer(DataGridColumn column, ListSortDirection direction)
        {
            if (column == null)
            {
                return null;
            }

            if (direction == ListSortDirection.Ascending)
            {
                return DataGridColumnSort.GetAscendingComparer(column);
            }

            if (direction == ListSortDirection.Descending)
            {
                var comparer = DataGridColumnSort.GetDescendingComparer(column);
                if (comparer != null)
                {
                    return new DataGridInvertedComparer(comparer);
                }
            }

            return null;
        }

        private bool TryBuildComparer(DataGridColumn column, ListSortDirection direction, CultureInfo culture, out IComparer comparer)
        {
            comparer = null;
            if (column == null)
            {
                return false;
            }

            if (column.CustomSortComparer != null)
            {
                comparer = column.CustomSortComparer;
                return true;
            }

            var directionalComparer = GetDirectionalComparer(column, direction);
            if (directionalComparer != null)
            {
                comparer = directionalComparer;
                return true;
            }

            var sortValueComparer = DataGridColumnSort.GetValueComparer(column);
            var sortAccessor = DataGridColumnSort.GetValueAccessor(column);
            if (sortAccessor != null || sortValueComparer != null)
            {
                var resolvedAccessor = sortAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
                if (resolvedAccessor != null)
                {
                    comparer = DataGridColumnValueAccessorComparer.Create(resolvedAccessor, culture, sortValueComparer);
                    return true;
                }
            }

            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null)
            {
                comparer = DataGridColumnValueAccessorComparer.Create(accessor, culture);
                return true;
            }

            return false;
        }

        private void HandleMissingAccessor(DataGridColumn column, object columnId, string message)
        {
            if (!_useAccessorsOnly)
            {
                return;
            }

            _options?.ReportMissingAccessor(DataGridFastPathFeature.Sorting, column, columnId, message);
            if (_throwOnMissingAccessor)
            {
                throw new InvalidOperationException(message);
            }

            LogInvalidSort(message);
        }

        private DataGridColumn FindColumnForSort(DataGridSortDescription sort)
        {
            foreach (var column in EnumerateColumns())
            {
                if (column == null)
                {
                    continue;
                }

                if (sort is DataGridComparerSortDescription comparerSort)
                {
                    var sourceComparer = comparerSort.SourceComparer;
                    if (column.CustomSortComparer != null && Equals(column.CustomSortComparer, sourceComparer))
                    {
                        return column;
                    }

                    var unwrappedComparer = DataGridSortComparerHelpers.Unwrap(sourceComparer);
                    var ascendingComparer = DataGridColumnSort.GetAscendingComparer(column);
                    if (ascendingComparer != null && ReferenceEquals(ascendingComparer, unwrappedComparer))
                    {
                        return column;
                    }

                    var descendingComparer = DataGridColumnSort.GetDescendingComparer(column);
                    if (descendingComparer != null && ReferenceEquals(descendingComparer, unwrappedComparer))
                    {
                        return column;
                    }

                    if (sourceComparer is IDataGridColumnValueAccessorComparer accessorComparer)
                    {
                        var sortAccessor = DataGridColumnSort.GetValueAccessor(column);
                        var accessor = sortAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
                        if (ReferenceEquals(accessor, accessorComparer.Accessor))
                        {
                            return column;
                        }
                    }
                }

                var propertyPath = column.GetSortPropertyName();
                if (!string.IsNullOrEmpty(propertyPath) && string.Equals(propertyPath, sort.PropertyPath, StringComparison.Ordinal))
                {
                    return column;
                }
            }

            return null;
        }

        private DataGridColumn FindColumnById(object columnId)
        {
            if (columnId == null)
            {
                return null;
            }

            if (columnId is DataGridColumn column)
            {
                return column;
            }

            if (columnId is DataGridColumnDefinition definition)
            {
                return EnumerateColumns().FirstOrDefault(c =>
                    ReferenceEquals(DataGridColumnMetadata.GetDefinition(c), definition));
            }

            return EnumerateColumns().FirstOrDefault(c =>
            {
                var definition = DataGridColumnMetadata.GetDefinition(c);
                return definition?.ColumnKey != null && Equals(definition.ColumnKey, columnId);
            });
        }

        private IEnumerable<DataGridColumn> EnumerateColumns()
        {
            return _columnProvider()?.Where(c => c != null) ?? Array.Empty<DataGridColumn>();
        }

        private List<DataGridSortDescription> BuildSortDescriptions(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                return null;
            }

            var list = new List<DataGridSortDescription>();
            foreach (var descriptor in descriptors)
            {
                var sort = ToSortDescription(descriptor);
                if (sort != null)
                {
                    list.Add(sort);
                }
                else
                {
                    LogInvalidSort("Skipping invalid sort descriptor (missing path/comparer).");
                }
            }
            return list;
        }

        private static bool SortsEqual(IReadOnlyList<DataGridSortDescription> existing, IReadOnlyList<DataGridSortDescription> target)
        {
            if (existing == null || target == null || existing.Count != target.Count)
            {
                return false;
            }

            for (int i = 0; i < existing.Count; i++)
            {
                var left = existing[i];
                var right = target[i];

                if (left.Direction != right.Direction)
                {
                    return false;
                }

                if (left is DataGridComparerSortDescription leftComparer &&
                    right is DataGridComparerSortDescription rightComparer)
                {
                    if (!Equals(leftComparer.SourceComparer, rightComparer.SourceComparer))
                    {
                        return false;
                    }
                }
                else if (left is DataGridComparerSortDescription || right is DataGridComparerSortDescription)
                {
                    return false;
                }
                else if (!string.Equals(left.PropertyPath, right.PropertyPath, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void RestoreSortDescriptions(IReadOnlyList<DataGridSortDescription> descriptions)
        {
            if (_view == null || descriptions == null)
            {
                return;
            }

            using (_view.DeferRefresh())
            {
                _view.SortDescriptions.Clear();
                foreach (var sort in descriptions)
                {
                    _view.SortDescriptions.Add(sort);
                }
            }
        }

        private void LogInvalidSort(string message)
        {
            Debug.WriteLine($"[DataGridSortingAdapter] {message}");
        }
    }
}
