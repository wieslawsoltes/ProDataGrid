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
using Avalonia.Collections;
using Avalonia.Controls;

namespace Avalonia.Controls.DataGridFiltering
{
    /// <summary>
    /// Bridges filtering descriptors to the view's Filter predicate.
    /// </summary>
    [RequiresUnreferencedCode("DataGridFilteringAdapter uses reflection to access item properties and is not compatible with trimming.")]
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
        private IDataGridCollectionView _view;

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

            _model.FilteringChanged += OnModelFilteringChanged;
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

            if (_view != null && _model.OwnsViewFilter)
            {
                ApplyModelToView(_model.Descriptors);
            }
            else if (_view is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += View_PropertyChanged;
                ReconcileExternalFilter();
            }
        }

        public void Dispose()
        {
            DetachView();
            _model.FilteringChanged -= OnModelFilteringChanged;
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
            ApplyModelToView(e.NewDescriptors, e.OldDescriptors);
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

            if (TryApplyModelToView(descriptors, previousDescriptors, out var handled))
            {
                if (handled)
                {
                    _afterViewRefresh?.Invoke();
                }
                return handled;
            }

            var predicate = ComposePredicate(descriptors);
            if (ReferenceEquals(_view.Filter, predicate))
            {
                return false;
            }

            EnsureBeforeViewRefresh();

            using (_view.DeferRefresh())
            {
                _view.Filter = predicate;
            }

            _afterViewRefresh?.Invoke();
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
            if (getter == null)
            {
                return null;
            }

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
                    return Compare(value, descriptor.Value, descriptor.Culture) > 0;
                case FilteringOperator.GreaterThanOrEqual:
                    return Compare(value, descriptor.Value, descriptor.Culture) >= 0;
                case FilteringOperator.LessThan:
                    return Compare(value, descriptor.Value, descriptor.Culture) < 0;
                case FilteringOperator.LessThanOrEqual:
                    return Compare(value, descriptor.Value, descriptor.Culture) <= 0;
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
            var columns = _columnProvider?.Invoke();
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

        private static int Compare(object left, object right, CultureInfo culture)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left is IComparable comparable)
            {
                return comparable.CompareTo(right);
            }

            return GetComparer(culture).Compare(left, right);
        }

        private static bool Between(object value, IReadOnlyList<object> bounds, CultureInfo culture)
        {
            if (bounds == null || bounds.Count < 2)
            {
                return false;
            }

            var lower = Compare(value, bounds[0], culture) >= 0;
            var upper = Compare(value, bounds[1], culture) <= 0;
            return lower && upper;
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
                inpc.PropertyChanged -= View_PropertyChanged;
            }

            _view = null;
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
