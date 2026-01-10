// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Data.Converters;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridColumnValueAccessor
    {
        Type ItemType { get; }

        Type ValueType { get; }

        bool CanWrite { get; }

        object GetValue(object item);

        void SetValue(object item, object value);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridColumnValueAccessor<TItem, TValue> : IDataGridColumnValueAccessor
    {
        TValue GetValue(TItem item);

        void SetValue(TItem item, TValue value);
    }

    internal interface IDataGridColumnTextAccessor
    {
        bool TryGetText(
            object item,
            IValueConverter converter,
            object converterParameter,
            string stringFormat,
            CultureInfo culture,
            IFormatProvider formatProvider,
            out string text);
    }

    internal interface IDataGridColumnFilterAccessor
    {
        bool TryMatch(object item, FilteringDescriptor descriptor, out bool match);
    }

    internal interface IDataGridColumnValueAccessorComparerFactory
    {
        IComparer CreateComparer(CultureInfo culture = null);
    }

    internal interface IDataGridColumnValueAccessorComparer
    {
        IDataGridColumnValueAccessor Accessor { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnValueAccessor<TItem, TValue> : IDataGridColumnValueAccessor<TItem, TValue>, IDataGridColumnValueAccessorComparerFactory, IDataGridColumnTextAccessor, IDataGridColumnFilterAccessor
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly Action<TItem, TValue> _setter;

        public DataGridColumnValueAccessor(Func<TItem, TValue> getter, Action<TItem, TValue> setter = null)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter;
        }

        public Type ItemType => typeof(TItem);

        public Type ValueType => typeof(TValue);

        public bool CanWrite => _setter != null;

        public object GetValue(object item)
        {
            if (item is null)
            {
                return null;
            }

            if (item is TItem typed)
            {
                return GetValue(typed);
            }

            return null;
        }

        public TValue GetValue(TItem item)
        {
            if (item is null)
            {
                return default;
            }

            return _getter(item);
        }

        public void SetValue(object item, object value)
        {
            if (_setter == null)
            {
                throw new InvalidOperationException("Setter is not available for this accessor.");
            }

            if (item is not TItem typed)
            {
                throw new InvalidOperationException($"Expected item of type '{typeof(TItem)}' but received '{item?.GetType()}'.");
            }

            SetValue(typed, value is TValue typedValue ? typedValue : (TValue)value);
        }

        public void SetValue(TItem item, TValue value)
        {
            if (_setter == null)
            {
                throw new InvalidOperationException("Setter is not available for this accessor.");
            }

            if (item is null)
            {
                throw new InvalidOperationException("Cannot set a value on a null item.");
            }

            _setter(item, value);
        }

        IComparer IDataGridColumnValueAccessorComparerFactory.CreateComparer(CultureInfo culture)
        {
            return new DataGridColumnValueAccessorComparer<TItem, TValue>(this, culture);
        }

        bool IDataGridColumnTextAccessor.TryGetText(
            object item,
            IValueConverter converter,
            object converterParameter,
            string stringFormat,
            CultureInfo culture,
            IFormatProvider formatProvider,
            out string text)
        {
            text = null;
            if (item is not TItem typed)
            {
                return false;
            }

            var value = _getter(typed);
            text = FormatValue(value, converter, converterParameter, stringFormat, culture, formatProvider);
            return true;
        }

        bool IDataGridColumnFilterAccessor.TryMatch(object item, FilteringDescriptor descriptor, out bool match)
        {
            match = false;
            if (descriptor == null)
            {
                return false;
            }

            if (item is not TItem typed)
            {
                return false;
            }

            var value = _getter(typed);
            return TryEvaluateTyped(value, descriptor, out match);
        }

        private static string FormatValue(
            TValue value,
            IValueConverter converter,
            object converterParameter,
            string stringFormat,
            CultureInfo culture,
            IFormatProvider formatProvider)
        {
            if (value == null)
            {
                return null;
            }

            var provider = formatProvider ?? culture;
            if (converter != null)
            {
                var converted = converter.Convert(value, typeof(string), converterParameter, culture);
                return Convert.ToString(converted, provider);
            }

            if (!string.IsNullOrEmpty(stringFormat))
            {
                return string.Format(provider, stringFormat, value);
            }

            if (provider != null && value is IFormattable formattable)
            {
                return formattable.ToString(null, provider);
            }

            return value.ToString();
        }

        private static bool TryEvaluateTyped(TValue value, FilteringDescriptor descriptor, out bool match)
        {
            match = false;

            switch (descriptor.Operator)
            {
                case FilteringOperator.Equals:
                    return TryEquals(value, descriptor.Value, out match);
                case FilteringOperator.NotEquals:
                    if (TryEquals(value, descriptor.Value, out match))
                    {
                        match = !match;
                        return true;
                    }
                    return false;
                case FilteringOperator.Contains:
                    return TryContains(value, descriptor.Value, descriptor.StringComparisonMode, out match);
                case FilteringOperator.StartsWith:
                    return TryStartsWith(value, descriptor.Value, descriptor.StringComparisonMode, out match);
                case FilteringOperator.EndsWith:
                    return TryEndsWith(value, descriptor.Value, descriptor.StringComparisonMode, out match);
                case FilteringOperator.GreaterThan:
                    if (TryCompare(value, descriptor.Value, out var compareResult))
                    {
                        match = compareResult > 0;
                        return true;
                    }
                    return false;
                case FilteringOperator.GreaterThanOrEqual:
                    if (TryCompare(value, descriptor.Value, out compareResult))
                    {
                        match = compareResult >= 0;
                        return true;
                    }
                    return false;
                case FilteringOperator.LessThan:
                    if (TryCompare(value, descriptor.Value, out compareResult))
                    {
                        match = compareResult < 0;
                        return true;
                    }
                    return false;
                case FilteringOperator.LessThanOrEqual:
                    if (TryCompare(value, descriptor.Value, out compareResult))
                    {
                        match = compareResult <= 0;
                        return true;
                    }
                    return false;
                case FilteringOperator.Between:
                    return TryBetween(value, descriptor.Values, out match);
                case FilteringOperator.In:
                    return TryIn(value, descriptor.Values, out match);
                default:
                    match = true;
                    return true;
            }
        }

        private static bool TryEquals(TValue value, object target, out bool match)
        {
            if (target is TValue typed)
            {
                match = EqualityComparer<TValue>.Default.Equals(value, typed);
                return true;
            }

            if (target == null)
            {
                match = value == null;
                return true;
            }

            match = false;
            return false;
        }

        private static bool TryCompare(TValue value, object target, out int result)
        {
            if (target is TValue typed)
            {
                result = Comparer<TValue>.Default.Compare(value, typed);
                return true;
            }

            result = 0;
            return false;
        }

        private static bool TryBetween(TValue value, IReadOnlyList<object> bounds, out bool match)
        {
            match = false;
            if (bounds == null || bounds.Count < 2)
            {
                return true;
            }

            if (bounds[0] is TValue lower && bounds[1] is TValue upper)
            {
                var comparer = Comparer<TValue>.Default;
                match = comparer.Compare(value, lower) >= 0 && comparer.Compare(value, upper) <= 0;
                return true;
            }

            return false;
        }

        private static bool TryIn(TValue value, IReadOnlyList<object> values, out bool match)
        {
            match = false;
            if (values == null || values.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is not TValue)
                {
                    return false;
                }
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (EqualityComparer<TValue>.Default.Equals(value, (TValue)values[i]))
                {
                    match = true;
                    return true;
                }
            }

            match = false;
            return true;
        }

        private static bool TryContains(TValue value, object target, StringComparison? comparison, out bool match)
        {
            if (typeof(TValue) != typeof(string) || target is not string term)
            {
                match = false;
                return false;
            }

            var source = (string)(object)value;
            match = source != null && source.Contains(term, comparison ?? StringComparison.Ordinal);
            return true;
        }

        private static bool TryStartsWith(TValue value, object target, StringComparison? comparison, out bool match)
        {
            if (typeof(TValue) != typeof(string) || target is not string term)
            {
                match = false;
                return false;
            }

            var source = (string)(object)value;
            match = source != null && source.StartsWith(term, comparison ?? StringComparison.Ordinal);
            return true;
        }

        private static bool TryEndsWith(TValue value, object target, StringComparison? comparison, out bool match)
        {
            if (typeof(TValue) != typeof(string) || target is not string term)
            {
                match = false;
                return false;
            }

            var source = (string)(object)value;
            match = source != null && source.EndsWith(term, comparison ?? StringComparison.Ordinal);
            return true;
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnValueAccessorComparer : IComparer, IDataGridColumnValueAccessorComparer
    {
        private readonly IDataGridColumnValueAccessor _accessor;
        private readonly CultureInfo _culture;
        private readonly IComparer _valueComparer;

        public DataGridColumnValueAccessorComparer(IDataGridColumnValueAccessor accessor, CultureInfo culture = null)
            : this(accessor, valueComparer: null, culture: culture)
        {
        }

        public DataGridColumnValueAccessorComparer(
            IDataGridColumnValueAccessor accessor,
            IComparer valueComparer,
            CultureInfo culture = null)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _culture = culture ?? CultureInfo.CurrentCulture;
            _valueComparer = valueComparer;
        }

        public static IComparer Create(IDataGridColumnValueAccessor accessor, CultureInfo culture = null, IComparer valueComparer = null)
        {
            if (accessor == null)
            {
                throw new ArgumentNullException(nameof(accessor));
            }

            if (valueComparer != null)
            {
                return new DataGridColumnValueAccessorComparer(accessor, valueComparer, culture);
            }

            if (accessor is IDataGridColumnValueAccessorComparerFactory factory)
            {
                return factory.CreateComparer(culture);
            }

            return new DataGridColumnValueAccessorComparer(accessor, culture);
        }

        public IDataGridColumnValueAccessor Accessor => _accessor;

        public int Compare(object x, object y)
        {
            var left = _accessor.GetValue(x);
            var right = _accessor.GetValue(y);

            if (ReferenceEquals(left, right))
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

            if (_valueComparer != null)
            {
                return _valueComparer.Compare(left, right);
            }

            if (left is string leftString && right is string rightString)
            {
                return _culture.CompareInfo.Compare(leftString, rightString);
            }

            if (left is IComparable comparable)
            {
                return comparable.CompareTo(right);
            }

            return Comparer.Default.Compare(left, right);
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnValueAccessorComparer<TItem, TValue> : IComparer, IDataGridColumnValueAccessorComparer
    {
        private readonly IDataGridColumnValueAccessor<TItem, TValue> _accessor;
        private readonly IComparer<TValue> _comparer;

        public DataGridColumnValueAccessorComparer(
            IDataGridColumnValueAccessor<TItem, TValue> accessor,
            CultureInfo culture = null)
            : this(accessor, valueComparer: null, culture: culture)
        {
        }

        public DataGridColumnValueAccessorComparer(
            IDataGridColumnValueAccessor<TItem, TValue> accessor,
            IComparer<TValue> valueComparer,
            CultureInfo culture = null)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _comparer = valueComparer ?? CreateComparer(culture ?? CultureInfo.CurrentCulture);
        }

        public IDataGridColumnValueAccessor Accessor => _accessor;

        public int Compare(object x, object y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            var leftHas = TryGetValue(x, out var left);
            var rightHas = TryGetValue(y, out var right);

            if (!leftHas && !rightHas)
            {
                return 0;
            }

            if (!leftHas)
            {
                return -1;
            }

            if (!rightHas)
            {
                return 1;
            }

            var leftNull = left is null;
            var rightNull = right is null;

            if (leftNull && rightNull)
            {
                return 0;
            }

            if (leftNull)
            {
                return -1;
            }

            if (rightNull)
            {
                return 1;
            }

            return _comparer.Compare(left, right);
        }

        private bool TryGetValue(object item, out TValue value)
        {
            if (item is TItem typed)
            {
                value = _accessor.GetValue(typed);
                return true;
            }

            value = default;
            return false;
        }

        private static IComparer<TValue> CreateComparer(CultureInfo culture)
        {
            if (typeof(TValue) == typeof(string))
            {
                var comparer = Comparer<string>.Create((x, y) => culture.CompareInfo.Compare(x, y));
                return (IComparer<TValue>)(object)comparer;
            }

            return Comparer<TValue>.Default;
        }
    }
}
