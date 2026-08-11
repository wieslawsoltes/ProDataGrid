// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Avalonia.Controls.DataGridFiltering
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridAccessorFilteringAdapter : DataGridFilteringAdapter
    {
        private static readonly object s_cultureComparerLock = new();
        private static readonly Dictionary<CultureInfo, IComparer<object>> s_cultureComparers = new();

        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private readonly bool _throwOnMissingAccessor;
        private readonly DataGridFastPathOptions _options;
        private Dictionary<PredicateCacheKey, Func<object, bool>> _predicateCache;

        public DataGridAccessorFilteringAdapter(
            IFilteringModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            DataGridFastPathOptions options = null,
            Action beforeViewRefresh = null,
            Action afterViewRefresh = null)
            : base(model, columnProvider, beforeViewRefresh, afterViewRefresh)
        {
            _columnProvider = columnProvider;
            _throwOnMissingAccessor = options?.ThrowOnMissingAccessor ?? false;
            _options = options;
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<FilteringDescriptor> descriptors,
            IReadOnlyList<FilteringDescriptor> previousDescriptors,
            out bool changed)
        {
            var view = View;
            if (view == null)
            {
                changed = false;
                return true;
            }

            var predicate = ComposePredicate(descriptors);
            if (ReferenceEquals(view.Filter, predicate))
            {
                changed = false;
                return true;
            }

            using (view.DeferRefresh())
            {
                view.Filter = predicate;
            }

            changed = true;
            return true;
        }

        private protected Func<object, bool> ComposePredicate(IReadOnlyList<FilteringDescriptor> descriptors)
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

            var column = FindColumn(descriptor);
            if (column != null)
            {
                var factory = DataGridColumnFilter.GetPredicateFactory(column);
                if (factory != null)
                {
                    var key = new PredicateCacheKey(descriptor, factory, null);
                    return GetOrCreateCachedPredicate(
                        key,
                        previousCache,
                        nextCache,
                        () => AdaptPredicate(factory(descriptor)));
                }
            }

            if (descriptor.Predicate != null)
            {
                return AdaptPredicate(descriptor.Predicate);
            }

            if (column == null)
            {
                if (_throwOnMissingAccessor)
                {
                    _options.ReportMissingAccessor(
                        DataGridFastPathFeature.Filtering,
                        column: null,
                        descriptor.ColumnId,
                        "Filtering requires a column accessor but no column could be resolved.");
                    throw new InvalidOperationException("Filtering requires a column accessor but no column could be resolved.");
                }

                _options?.ReportMissingAccessor(
                    DataGridFastPathFeature.Filtering,
                    column: null,
                    descriptor.ColumnId,
                    "Filtering skipped because no column could be resolved.");
                return null;
            }

            var columnFilterAccessor = DataGridColumnFilter.GetValueAccessor(column);
            var accessor = columnFilterAccessor ?? DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor == null)
            {
                if (_throwOnMissingAccessor)
                {
                    _options.ReportMissingAccessor(
                        DataGridFastPathFeature.Filtering,
                        column,
                        descriptor.ColumnId,
                        $"Filtering requires a value accessor for column '{column.Header}'.");
                    throw new InvalidOperationException($"Filtering requires a value accessor for column '{column.Header}'.");
                }

                _options?.ReportMissingAccessor(
                    DataGridFastPathFeature.Filtering,
                    column,
                    descriptor.ColumnId,
                    $"Filtering skipped because no value accessor was found for column '{column.Header}'.");
                return null;
            }

            var accessorKey = new PredicateCacheKey(descriptor, accessor, null);
            return GetOrCreateCachedPredicate(accessorKey, previousCache, nextCache, () =>
            {
                if (accessor is IDataGridColumnFilterAccessor typedFilterAccessor)
                {
                    return item =>
                    {
                        object accessorItem = GetAccessorItem(accessor, item);
                        if (typedFilterAccessor.TryMatch(accessorItem, descriptor, out var match))
                        {
                            return match;
                        }

                        var value = accessor.GetValue(accessorItem);
                        return EvaluateDescriptor(value, descriptor);
                    };
                }

                return item => EvaluateDescriptor(
                    accessor.GetValue(GetAccessorItem(accessor, item)),
                    descriptor);
            });
        }

        private protected virtual Func<object, bool> AdaptPredicate(Func<object, bool> predicate)
        {
            return predicate;
        }

        private protected virtual object GetAccessorItem(
            IDataGridColumnValueAccessor accessor,
            object item)
        {
            return item;
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
    }
}
