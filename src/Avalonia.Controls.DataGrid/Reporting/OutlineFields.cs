// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls.Utils;
using Avalonia.Data.Converters;
using Avalonia.Controls.DataGridPivoting;

namespace Avalonia.Controls.DataGridReporting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    abstract class OutlineFieldBase : INotifyPropertyChanged
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
                return TypeHelper.GetNestedPropertyValue(item, PropertyPath);
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
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineGroupField : OutlineFieldBase
    {
        private IComparer<object?>? _comparer;
        private ListSortDirection? _sortDirection;
        private bool _showSubtotals = true;

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
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class OutlineValueField : OutlineFieldBase
    {
        private PivotAggregateType _aggregateType = PivotAggregateType.Sum;
        private IPivotAggregator? _customAggregator;

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
    }
}
