// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls.Utils;
using Avalonia.Data.Converters;

namespace Avalonia.Controls.DataGridPivoting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    abstract class PivotFieldBase : INotifyPropertyChanged
    {
        private object? _key;
        private string? _header;
        private string? _propertyPath;
        private DataGridBindingDefinition? _binding;
        private Func<object?, object?>? _valueSelector;
        private Func<object?, object?>? _groupSelector;
        private IValueConverter? _converter;
        private object? _converterParameter;
        private string? _stringFormat;
        private IFormatProvider? _formatProvider;
        private string? _nullLabel;
        private Type? _valueType;

        public event PropertyChangedEventHandler? PropertyChanged;

        public object? Key
        {
            get => _key;
            set => SetProperty(ref _key, value, nameof(Key));
        }

        public string? Header
        {
            get => _header;
            set => SetProperty(ref _header, value, nameof(Header));
        }

        public string? PropertyPath
        {
            get => _propertyPath;
            set => SetProperty(ref _propertyPath, value, nameof(PropertyPath));
        }

        public DataGridBindingDefinition? Binding
        {
            get => _binding;
            set
            {
                if (SetProperty(ref _binding, value, nameof(Binding)))
                {
                    if (ValueType == null && value?.ValueType != null)
                    {
                        ValueType = value.ValueType;
                    }
                }
            }
        }

        public Func<object?, object?>? ValueSelector
        {
            get => _valueSelector;
            set => SetProperty(ref _valueSelector, value, nameof(ValueSelector));
        }

        public Func<object?, object?>? GroupSelector
        {
            get => _groupSelector;
            set => SetProperty(ref _groupSelector, value, nameof(GroupSelector));
        }

        public IValueConverter? Converter
        {
            get => _converter;
            set => SetProperty(ref _converter, value, nameof(Converter));
        }

        public object? ConverterParameter
        {
            get => _converterParameter;
            set => SetProperty(ref _converterParameter, value, nameof(ConverterParameter));
        }

        public string? StringFormat
        {
            get => _stringFormat;
            set => SetProperty(ref _stringFormat, value, nameof(StringFormat));
        }

        public IFormatProvider? FormatProvider
        {
            get => _formatProvider;
            set => SetProperty(ref _formatProvider, value, nameof(FormatProvider));
        }

        public string? NullLabel
        {
            get => _nullLabel;
            set => SetProperty(ref _nullLabel, value, nameof(NullLabel));
        }

        public Type? ValueType
        {
            get => _valueType;
            set => SetProperty(ref _valueType, value, nameof(ValueType));
        }

        internal object? GetValue(object? item)
        {
            if (item == null)
            {
                return null;
            }

            if (ValueSelector != null)
            {
                return ValueSelector(item);
            }

            if (Binding?.ValueAccessor != null)
            {
                return Binding.ValueAccessor.GetValue(item);
            }

            if (!string.IsNullOrWhiteSpace(PropertyPath))
            {
                return ReflectionPropertyPath.GetValue(item, PropertyPath);
            }

            return null;
        }

        internal object? GetGroupValue(object? item)
        {
            var value = GetValue(item);
            if (GroupSelector != null)
            {
                return GroupSelector(value);
            }

            return value;
        }

        internal string FormatValue(object? value, CultureInfo culture, string? emptyValueLabel = null)
        {
            if (value == null)
            {
                if (!string.IsNullOrEmpty(NullLabel))
                {
                    return NullLabel;
                }

                return emptyValueLabel ?? string.Empty;
            }

            if (Converter != null)
            {
                var converterCulture = FormatProvider as CultureInfo ?? culture;
                var converted = Converter.Convert(value, typeof(string), ConverterParameter, converterCulture);
                return converted?.ToString() ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(StringFormat))
            {
                try
                {
                    if (StringFormat.Contains("{0"))
                    {
                        var provider = FormatProvider ?? culture;
                        return string.Format(provider, StringFormat, value);
                    }

                    if (value is IFormattable formattable)
                    {
                        return formattable.ToString(StringFormat, FormatProvider ?? culture);
                    }
                }
                catch
                {
                    // ignore formatting errors and fall back
                }
            }

            return value.ToString() ?? string.Empty;
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class PivotAxisField : PivotFieldBase
    {
        private IComparer<object?>? _comparer;
        private ListSortDirection? _sortDirection;
        private bool _showSubtotals = true;
        private PivotTotalPosition _subtotalPosition = PivotTotalPosition.End;
        private bool _showItemsWithNoData;
        private IEnumerable<object?>? _itemsSource;
        private bool _applyGroupSelectorToItemsSource;
        private PivotFieldFilter? _filter;
        private PivotValueFilter? _valueFilter;
        private PivotValueSort? _valueSort;

        public IComparer<object?>? Comparer
        {
            get => _comparer;
            set => SetProperty(ref _comparer, value, nameof(Comparer));
        }

        public ListSortDirection? SortDirection
        {
            get => _sortDirection;
            set => SetProperty(ref _sortDirection, value, nameof(SortDirection));
        }

        public bool ShowSubtotals
        {
            get => _showSubtotals;
            set => SetProperty(ref _showSubtotals, value, nameof(ShowSubtotals));
        }

        public PivotTotalPosition SubtotalPosition
        {
            get => _subtotalPosition;
            set => SetProperty(ref _subtotalPosition, value, nameof(SubtotalPosition));
        }

        public bool ShowItemsWithNoData
        {
            get => _showItemsWithNoData;
            set => SetProperty(ref _showItemsWithNoData, value, nameof(ShowItemsWithNoData));
        }

        public IEnumerable<object?>? ItemsSource
        {
            get => _itemsSource;
            set => SetProperty(ref _itemsSource, value, nameof(ItemsSource));
        }

        public bool ApplyGroupSelectorToItemsSource
        {
            get => _applyGroupSelectorToItemsSource;
            set => SetProperty(ref _applyGroupSelectorToItemsSource, value, nameof(ApplyGroupSelectorToItemsSource));
        }

        public PivotFieldFilter? Filter
        {
            get => _filter;
            set
            {
                if (ReferenceEquals(_filter, value))
                {
                    return;
                }

                if (_filter != null)
                {
                    _filter.Changed -= Filter_Changed;
                }

                _filter = value;

                if (_filter != null)
                {
                    _filter.Changed += Filter_Changed;
                }

                RaisePropertyChanged(nameof(Filter));
            }
        }

        public PivotValueFilter? ValueFilter
        {
            get => _valueFilter;
            set
            {
                if (ReferenceEquals(_valueFilter, value))
                {
                    return;
                }

                if (_valueFilter != null)
                {
                    _valueFilter.Changed -= ValueFilter_Changed;
                }

                _valueFilter = value;

                if (_valueFilter != null)
                {
                    _valueFilter.Changed += ValueFilter_Changed;
                }

                RaisePropertyChanged(nameof(ValueFilter));
            }
        }

        public PivotValueSort? ValueSort
        {
            get => _valueSort;
            set
            {
                if (ReferenceEquals(_valueSort, value))
                {
                    return;
                }

                if (_valueSort != null)
                {
                    _valueSort.Changed -= ValueSort_Changed;
                }

                _valueSort = value;

                if (_valueSort != null)
                {
                    _valueSort.Changed += ValueSort_Changed;
                }

                RaisePropertyChanged(nameof(ValueSort));
            }
        }

        private void Filter_Changed(object? sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(Filter));
        }

        private void ValueFilter_Changed(object? sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(ValueFilter));
        }

        private void ValueSort_Changed(object? sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(ValueSort));
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class PivotValueField : PivotFieldBase
    {
        private PivotAggregateType _aggregateType = PivotAggregateType.Sum;
        private IPivotAggregator? _customAggregator;
        private PivotValueDisplayMode _displayMode;
        private string? _formula;

        public PivotAggregateType AggregateType
        {
            get => _aggregateType;
            set => SetProperty(ref _aggregateType, value, nameof(AggregateType));
        }

        public IPivotAggregator? CustomAggregator
        {
            get => _customAggregator;
            set => SetProperty(ref _customAggregator, value, nameof(CustomAggregator));
        }

        public PivotValueDisplayMode DisplayMode
        {
            get => _displayMode;
            set => SetProperty(ref _displayMode, value, nameof(DisplayMode));
        }

        public string? Formula
        {
            get => _formula;
            set => SetProperty(ref _formula, value, nameof(Formula));
        }

        internal bool IsCalculated => !string.IsNullOrWhiteSpace(_formula);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class PivotFieldFilter
    {
        private readonly PivotFilterSet _included;
        private readonly PivotFilterSet _excluded;
        private Func<object?, bool>? _predicate;

        public PivotFieldFilter(IEnumerable<object?>? included = null, IEnumerable<object?>? excluded = null, IEqualityComparer<object?>? comparer = null)
        {
            _included = new PivotFilterSet(included, comparer, RaiseChanged);
            _excluded = new PivotFilterSet(excluded, comparer, RaiseChanged);
        }

        public event EventHandler? Changed;

        public Func<object?, bool>? Predicate
        {
            get => _predicate;
            set
            {
                if (Equals(_predicate, value))
                {
                    return;
                }

                _predicate = value;
                RaiseChanged();
            }
        }

        public ISet<object?> Included => _included;

        public ISet<object?> Excluded => _excluded;

        public bool IsMatch(object? value)
        {
            if (Predicate != null)
            {
                return Predicate(value);
            }

            if (_included.Count > 0)
            {
                return _included.Contains(value);
            }

            if (_excluded.Count > 0)
            {
                return !_excluded.Contains(value);
            }

            return true;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private sealed class PivotFilterSet : ISet<object?>
        {
            private readonly HashSet<object?> _set;
            private readonly Action _changed;

            public PivotFilterSet(IEnumerable<object?>? items, IEqualityComparer<object?>? comparer, Action changed)
            {
                _set = items != null ? new HashSet<object?>(items, comparer) : new HashSet<object?>(comparer);
                _changed = changed;
            }

            public int Count => _set.Count;

            public bool IsReadOnly => false;

            public bool Add(object? item)
            {
                var added = _set.Add(item);
                if (added)
                {
                    _changed();
                }

                return added;
            }

            void ICollection<object?>.Add(object? item)
            {
                Add(item);
            }

            public void Clear()
            {
                if (_set.Count == 0)
                {
                    return;
                }

                _set.Clear();
                _changed();
            }

            public bool Contains(object? item) => _set.Contains(item);

            public void CopyTo(object?[] array, int arrayIndex) => _set.CopyTo(array, arrayIndex);

            public void ExceptWith(IEnumerable<object?> other)
            {
                if (other == null)
                {
                    return;
                }

                var count = _set.Count;
                _set.ExceptWith(other);
                if (_set.Count != count)
                {
                    _changed();
                }
            }

            public IEnumerator<object?> GetEnumerator() => _set.GetEnumerator();

            public void IntersectWith(IEnumerable<object?> other)
            {
                if (other == null)
                {
                    return;
                }

                var count = _set.Count;
                _set.IntersectWith(other);
                if (_set.Count != count)
                {
                    _changed();
                }
            }

            public bool IsProperSubsetOf(IEnumerable<object?> other) => _set.IsProperSubsetOf(other);

            public bool IsProperSupersetOf(IEnumerable<object?> other) => _set.IsProperSupersetOf(other);

            public bool IsSubsetOf(IEnumerable<object?> other) => _set.IsSubsetOf(other);

            public bool IsSupersetOf(IEnumerable<object?> other) => _set.IsSupersetOf(other);

            public bool Overlaps(IEnumerable<object?> other) => _set.Overlaps(other);

            public bool SetEquals(IEnumerable<object?> other) => _set.SetEquals(other);

            public void SymmetricExceptWith(IEnumerable<object?> other)
            {
                if (other == null)
                {
                    return;
                }

                var count = _set.Count;
                _set.SymmetricExceptWith(other);
                if (_set.Count != count)
                {
                    _changed();
                }
            }

            public void UnionWith(IEnumerable<object?> other)
            {
                if (other == null)
                {
                    return;
                }

                var count = _set.Count;
                _set.UnionWith(other);
                if (_set.Count != count)
                {
                    _changed();
                }
            }

            public bool Remove(object? item)
            {
                var removed = _set.Remove(item);
                if (removed)
                {
                    _changed();
                }

                return removed;
            }

            IEnumerator IEnumerable.GetEnumerator() => _set.GetEnumerator();
        }
    }
}
