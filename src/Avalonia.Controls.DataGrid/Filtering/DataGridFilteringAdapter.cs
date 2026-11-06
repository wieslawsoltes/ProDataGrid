// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Utilities;

namespace Avalonia.Controls.DataGridFiltering
{
    /// <summary>
    /// Bridges filtering descriptors to the view's Filter predicate.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridFilteringAdapter : IDisposable
    {
        private static readonly object s_externalFilterColumnId = new();
        private static readonly object s_cultureComparerLock = new();
        private static readonly Dictionary<CultureInfo, IComparer<object>> s_cultureComparers = new();

        private readonly IFilteringModel _model;
        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private Action _beforeViewRefresh;
        private Action _afterViewRefresh;
        private readonly Dictionary<(Type type, string property), Func<object, object>> _getterCache = new();
        private Dictionary<PredicateCacheKey, Func<object, bool>> _predicateCache;
        private readonly object _modelChangeLock = new();
        private IDataGridCollectionView _view;
        private IReadOnlyList<FilteringDescriptor> _pendingNewDescriptors;
        private IReadOnlyList<FilteringDescriptor> _pendingOldDescriptors;
        private Action<Action> _postToViewThread;
        private int _pendingModelChangeVersion;
        private int _modelChangeVersion;
        private int _lastAppliedModelChangeVersion;
        private int _modelApplyQueued;
        private int _viewThreadId;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridFilteringAdapter(
            IFilteringModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            Action beforeViewRefresh = null,
            Action afterViewRefresh = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));
            _beforeViewRefresh = beforeViewRefresh;
            _afterViewRefresh = afterViewRefresh;
            _postToViewThread = static action => Dispatcher.UIThread.Post(action);

            WeakEventHandlerManager.Subscribe<IFilteringModel, FilteringChangedEventArgs, DataGridFilteringAdapter>(
                _model,
                nameof(IFilteringModel.FilteringChanged),
                OnModelFilteringChanged);
        }

        internal void AttachLifecycle(Action beforeViewRefresh, Action afterViewRefresh)
        {
            _beforeViewRefresh = beforeViewRefresh;
            _afterViewRefresh = afterViewRefresh;
        }

        internal void SetViewThreadPostForTesting(Action<Action> post)
        {
            _postToViewThread = post ?? throw new ArgumentNullException(nameof(post));
        }

        /// <summary>
        /// Posts work across the adapter's view-affinity boundary. Derived adapters use the
        /// same boundary so tests and headless hosts cannot accidentally execute producer
        /// callbacks inline merely because the dispatcher reports access.
        /// </summary>
        protected void PostToViewThread(Action action)
        {
            _postToViewThread(action);
        }

        /// <summary>
        /// Invokes the owning grid's pre-refresh lifecycle callback, when configured.
        /// Derived adapters should pair this with <see cref="InvokeAfterViewRefresh"/>
        /// around refreshes initiated outside descriptor application.
        /// </summary>
        protected void InvokeBeforeViewRefresh()
        {
            _beforeViewRefresh?.Invoke();
        }

        /// <summary>
        /// Invokes the owning grid's post-refresh lifecycle callback, when configured.
        /// </summary>
        protected void InvokeAfterViewRefresh()
        {
            _afterViewRefresh?.Invoke();
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
            Volatile.Write(ref _viewThreadId, Environment.CurrentManagedThreadId);

            if (_view != null && _model.OwnsViewFilter)
            {
                ApplyModelToView(SnapshotDescriptors(_model.Descriptors));
            }
            else if (_view is INotifyPropertyChanged inpc)
            {
                WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, DataGridFilteringAdapter>(
                    inpc,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    View_PropertyChanged);
                ReconcileExternalFilter();
            }
        }

        public virtual void Dispose()
        {
            DetachView();
            WeakEventHandlerManager.Unsubscribe<FilteringChangedEventArgs, DataGridFilteringAdapter>(
                _model,
                nameof(IFilteringModel.FilteringChanged),
                OnModelFilteringChanged);
        }

        protected virtual bool TryApplyModelToView(
            IReadOnlyList<FilteringDescriptor> descriptors,
            IReadOnlyList<FilteringDescriptor> previousDescriptors,
            out bool changed)
        {
            changed = false;
            return false;
        }

        private void OnModelFilteringChanged(object sender, FilteringChangedEventArgs e)
        {
            int version = Interlocked.Increment(ref _modelChangeVersion);
            IReadOnlyList<FilteringDescriptor> newDescriptors =
                SnapshotDescriptors(e.NewDescriptors);
            IReadOnlyList<FilteringDescriptor> oldDescriptors =
                SnapshotDescriptors(e.OldDescriptors);
            // The headless dispatcher intentionally allows CheckAccess from worker threads.
            // Remember the thread that owns the attached view as the authoritative affinity
            // boundary so descriptor changes never refresh an Avalonia collection view inline
            // on a producer thread.
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref _viewThreadId))
            {
                ApplyModelToView(newDescriptors, oldDescriptors);
                Volatile.Write(ref _lastAppliedModelChangeVersion, version);
                return;
            }

            lock (_modelChangeLock)
            {
                _pendingNewDescriptors = newDescriptors;
                _pendingOldDescriptors = oldDescriptors;
                _pendingModelChangeVersion = version;
            }

            if (Interlocked.CompareExchange(ref _modelApplyQueued, 1, 0) != 0)
            {
                return;
            }

            PostToViewThread(ApplyLatestModelChangeOnUiThread);
        }

        private void ApplyLatestModelChangeOnUiThread()
        {
            Interlocked.Exchange(ref _modelApplyQueued, 0);
            int version;
            IReadOnlyList<FilteringDescriptor> newDescriptors;
            IReadOnlyList<FilteringDescriptor> oldDescriptors;
            lock (_modelChangeLock)
            {
                version = _pendingModelChangeVersion;
                newDescriptors = _pendingNewDescriptors;
                oldDescriptors = _pendingOldDescriptors;
            }

            if (version <= Volatile.Read(ref _lastAppliedModelChangeVersion))
            {
                return;
            }

            ApplyModelToView(newDescriptors, oldDescriptors);
            Volatile.Write(ref _lastAppliedModelChangeVersion, version);
        }

        private static IReadOnlyList<FilteringDescriptor> SnapshotDescriptors(
            IReadOnlyList<FilteringDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return Array.Empty<FilteringDescriptor>();
            }

            var snapshot = new FilteringDescriptor[descriptors.Count];
            for (int i = 0; i < descriptors.Count; i++)
            {
                snapshot[i] = descriptors[i];
            }

            return snapshot;
        }

        private bool ApplyModelToView(
            IReadOnlyList<FilteringDescriptor> descriptors,
            IReadOnlyList<FilteringDescriptor> previousDescriptors = null)
        {
            if (_view == null)
            {
                return false;
            }

            if (!_model.OwnsViewFilter)
            {
                return false;
            }

            InvokeBeforeViewRefresh();
            if (TryApplyModelToView(descriptors, previousDescriptors, out var changed))
            {
                if (changed)
                {
                    InvokeAfterViewRefresh();
                }
                return changed;
            }

            var predicate = ComposePredicate(descriptors);
            if (ReferenceEquals(_view.Filter, predicate))
            {
                return false;
            }

            try
            {
                using (_view.DeferRefresh())
                {
                    _view.Filter = predicate;
                }
            }
            finally
            {
                InvokeAfterViewRefresh();
            }
            return true;
        }

        private Func<object, bool> ComposePredicate(IReadOnlyList<FilteringDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                _predicateCache = null;
                return null;
            }

            var previousCache = _predicateCache;
            var nextCache = previousCache != null
                ? new Dictionary<PredicateCacheKey, Func<object, bool>>(previousCache.Count)
                : new Dictionary<PredicateCacheKey, Func<object, bool>>();

            var compiled = new List<Func<object, bool>>();
            foreach (var descriptor in descriptors)
            {
                var predicate = Compile(descriptor, previousCache, nextCache);
                if (predicate != null)
                {
                    compiled.Add(predicate);
                }
            }

            _predicateCache = nextCache;

            if (compiled.Count == 0)
            {
                return null;
            }

            if (compiled.Count == 1)
            {
                return compiled[0];
            }

            return item =>
            {
                for (int i = 0; i < compiled.Count; i++)
                {
                    if (!compiled[i](item))
                    {
                        return false;
                    }
                }

                return true;
            };
        }

        private Func<object, bool> Compile(
            FilteringDescriptor descriptor,
            Dictionary<PredicateCacheKey, Func<object, bool>> previousCache,
            Dictionary<PredicateCacheKey, Func<object, bool>> nextCache)
        {
            if (descriptor == null)
            {
                return null;
            }

            // Column-specific predicate factory takes precedence over inline predicate.
            var column = FindColumn(descriptor);
            if (column != null)
            {
                var factory = DataGridColumnFilter.GetPredicateFactory(column);
                if (factory != null)
                {
                    var key = new PredicateCacheKey(descriptor, factory, null);
                    return GetOrCreateCachedPredicate(key, previousCache, nextCache, () => factory(descriptor));
                }
            }

            if (descriptor.Predicate != null)
            {
                return descriptor.Predicate;
            }

            if (column != null)
            {
                var filterAccessor = DataGridColumnFilter.GetValueAccessor(column);
                var accessor = filterAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
                if (accessor != null)
                {
                    var key = new PredicateCacheKey(descriptor, accessor, null);
                    return GetOrCreateCachedPredicate(key, previousCache, nextCache, () =>
                        item => EvaluateDescriptor(accessor.GetValue(item), descriptor));
                }
            }

            if (string.IsNullOrEmpty(descriptor.PropertyPath))
            {
                return null;
            }

            var getter = GetGetter(descriptor.PropertyPath);

            var propertyKey = new PredicateCacheKey(descriptor, null, null);
            return GetOrCreateCachedPredicate(propertyKey, previousCache, nextCache, () =>
                item => EvaluateDescriptor(getter(item), descriptor));
        }

        private readonly struct PredicateCacheKey : IEquatable<PredicateCacheKey>
        {
            public PredicateCacheKey(FilteringDescriptor descriptor, object primary, object secondary)
            {
                Descriptor = descriptor;
                Primary = primary;
                Secondary = secondary;
            }

            public FilteringDescriptor Descriptor { get; }

            public object Primary { get; }

            public object Secondary { get; }

            public bool Equals(PredicateCacheKey other)
            {
                return Equals(Descriptor, other.Descriptor)
                    && ReferenceEquals(Primary, other.Primary)
                    && ReferenceEquals(Secondary, other.Secondary);
            }

            public override bool Equals(object obj)
            {
                return obj is PredicateCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Descriptor?.GetHashCode() ?? 0;
                    if (Primary != null)
                    {
                        hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(Primary);
                    }

                    if (Secondary != null)
                    {
                        hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(Secondary);
                    }

                    return hash;
                }
            }
        }

        private static Func<object, bool> GetOrCreateCachedPredicate(
            PredicateCacheKey key,
            Dictionary<PredicateCacheKey, Func<object, bool>> previousCache,
            Dictionary<PredicateCacheKey, Func<object, bool>> nextCache,
            Func<Func<object, bool>> factory)
        {
            if (previousCache != null && previousCache.TryGetValue(key, out var cached))
            {
                nextCache[key] = cached;
                return cached;
            }

            var created = factory();
            if (created != null)
            {
                nextCache[key] = created;
            }

            return created;
        }

        private static bool EvaluateDescriptor(object value, FilteringDescriptor descriptor)
        {
            switch (descriptor.Operator)
            {
                case FilteringOperator.Equals:
                    return Equals(value, descriptor.Value);
                case FilteringOperator.NotEquals:
                    return !Equals(value, descriptor.Value);
                case FilteringOperator.Contains:
                    return Contains(value, descriptor.Value, descriptor.StringComparisonMode);
                case FilteringOperator.StartsWith:
                    return StartsWith(value, descriptor.Value, descriptor.StringComparisonMode);
                case FilteringOperator.EndsWith:
                    return EndsWith(value, descriptor.Value, descriptor.StringComparisonMode);
                case FilteringOperator.GreaterThan:
                    return TryCompare(value, descriptor.Value, descriptor.Culture, out var greaterThan) && greaterThan > 0;
                case FilteringOperator.GreaterThanOrEqual:
                    return TryCompare(value, descriptor.Value, descriptor.Culture, out var greaterThanOrEqual) && greaterThanOrEqual >= 0;
                case FilteringOperator.LessThan:
                    return TryCompare(value, descriptor.Value, descriptor.Culture, out var lessThan) && lessThan < 0;
                case FilteringOperator.LessThanOrEqual:
                    return TryCompare(value, descriptor.Value, descriptor.Culture, out var lessThanOrEqual) && lessThanOrEqual <= 0;
                case FilteringOperator.Between:
                    return Between(value, descriptor.Values, descriptor.Culture);
                case FilteringOperator.In:
                    return In(value, descriptor.Values);
                default:
                    return true;
            }
        }

        private DataGridColumn FindColumn(FilteringDescriptor descriptor)
        {
            var columns = _columnProvider.Invoke();
            if (columns == null)
            {
                return null;
            }

            foreach (var column in columns)
            {
                if (DataGridColumnMetadata.MatchesColumnId(column, descriptor.ColumnId))
                {
                    return column;
                }

                var propertyName = column.GetSortPropertyName();
                if (!string.IsNullOrEmpty(propertyName) &&
                    string.Equals(propertyName, descriptor.PropertyPath, StringComparison.Ordinal))
                {
                    return column;
                }
            }

            return null;
        }

        private Func<object, object> GetGetter(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            Func<object, object> TryGetCached(Type type)
            {
                if (_getterCache.TryGetValue((type, propertyPath), out var cached))
                {
                    return cached;
                }

                return null;
            }

            Func<object, object> getter = null;

            getter = (item) =>
            {
                if (item == null)
                {
                    return null;
                }

                var type = item.GetType();
                var cached = TryGetCached(type);
                if (cached != null)
                {
                    return cached(item);
                }

                if (!DataGridReflectionOptions.IsEnabled)
                {
                    throw DataGridReflectionOptions.CreateException("Property-path filtering");
                }

                var prop = type.GetProperty(propertyPath);
                if (prop == null)
                {
                    return null;
                }

                var compiled = new Func<object, object>(o => prop.GetValue(o));
                _getterCache[(type, propertyPath)] = compiled;
                return compiled(item);
            };

            return getter;
        }

        private static bool Contains(object source, object target, StringComparison? comparison)
        {
            if (source == null || target == null)
            {
                return false;
            }

            if (source is string s && target is string t)
            {
                return s.IndexOf(t, comparison ?? StringComparison.Ordinal) >= 0;
            }

            if (source is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (Equals(item, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool StartsWith(object source, object target, StringComparison? comparison)
        {
            if (source is string s && target is string t)
            {
                return s.StartsWith(t, comparison ?? StringComparison.Ordinal);
            }

            return false;
        }

        private static bool EndsWith(object source, object target, StringComparison? comparison)
        {
            if (source is string s && target is string t)
            {
                return s.EndsWith(t, comparison ?? StringComparison.Ordinal);
            }

            return false;
        }

        private static IComparer<object> GetComparer(CultureInfo culture)
        {
            if (culture == null)
            {
                return Comparer<object>.Default;
            }

            lock (s_cultureComparerLock)
            {
                if (!s_cultureComparers.TryGetValue(culture, out var comparer))
                {
                    comparer = Comparer<object>.Create((x, y) =>
                        string.Compare(Convert.ToString(x, culture), Convert.ToString(y, culture), StringComparison.Ordinal));
                    s_cultureComparers[culture] = comparer;
                }

                return comparer;
            }
        }

        private static bool TryCompare(object left, object right, CultureInfo culture, out int result)
        {
            result = 0;

            if (left == null && right == null)
            {
                return true;
            }

            if (left == null)
            {
                result = -1;
                return true;
            }

            if (right == null)
            {
                result = 1;
                return true;
            }

            if (culture != null && left is string leftString && right is string rightString)
            {
                result = string.Compare(leftString, rightString, culture, CompareOptions.None);
                return true;
            }

            if (left is IComparable comparable)
            {
                if (left.GetType().IsAssignableFrom(right.GetType()))
                {
                    result = comparable.CompareTo(right);
                    return true;
                }

                try
                {
                    result = comparable.CompareTo(right);
                    return true;
                }
                catch (ArgumentException)
                {
                    // Fall back to conversion for mismatched runtime types.
                }
                catch (InvalidCastException)
                {
                    // Fall back to conversion for mismatched runtime types.
                }

                object rightValue;
                try
                {
                    rightValue = Convert.ChangeType(right, left.GetType(), culture);
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (InvalidCastException)
                {
                    return false;
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (OverflowException)
                {
                    return false;
                }

                try
                {
                    result = comparable.CompareTo(rightValue);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (InvalidCastException)
                {
                    return false;
                }
            }

            try
            {
                result = GetComparer(culture).Compare(left, right);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool Between(object value, IReadOnlyList<object> bounds, CultureInfo culture)
        {
            if (bounds == null || bounds.Count < 2)
            {
                return false;
            }

            return TryCompare(value, bounds[0], culture, out var lower) && lower >= 0
                && TryCompare(value, bounds[1], culture, out var upper) && upper <= 0;
        }

        private static bool In(object value, IReadOnlyList<object> values)
        {
            if (values == null || values.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (Equals(value, values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void DetachView()
        {
            if (_view is INotifyPropertyChanged inpc)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridFilteringAdapter>(
                    inpc,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    View_PropertyChanged);
            }

            _view = null;
            Volatile.Write(ref _viewThreadId, 0);
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IDataGridCollectionView.Filter))
            {
                ReconcileExternalFilter();
            }
        }

        private void ReconcileExternalFilter()
        {
            if (_view == null || _model.OwnsViewFilter)
            {
                return;
            }

            var external = _view.Filter;

            if (external == null)
            {
                if (_model.Descriptors.Count > 0)
                {
                    _model.Clear();
                }

                return;
            }

            if (_model.Descriptors.Count == 1 &&
                _model.Descriptors[0].Operator == FilteringOperator.Custom &&
                ReferenceEquals(_model.Descriptors[0].Predicate, external))
            {
                return;
            }

            _model.BeginUpdate();
            try
            {
                _model.Apply(new[]
                {
                    new FilteringDescriptor(
                        columnId: s_externalFilterColumnId,
                        @operator: FilteringOperator.Custom,
                        predicate: external)
                });
            }
            finally
            {
                _model.EndUpdate();
            }
        }
    }
}
