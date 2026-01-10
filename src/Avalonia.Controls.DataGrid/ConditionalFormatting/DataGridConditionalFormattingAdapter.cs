// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Utils;

namespace Avalonia.Controls.DataGridConditionalFormatting
{
    /// <summary>
    /// Evaluates conditional formatting descriptors against the view and current items.
    /// </summary>
    [RequiresUnreferencedCode("DataGridConditionalFormattingAdapter uses reflection to access item properties and is not compatible with trimming.")]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridConditionalFormattingAdapter : IDisposable
    {
        private readonly IConditionalFormattingModel _model;
        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private readonly Dictionary<(Type type, string property), Func<object, object>> _getterCache = new();
        private readonly Dictionary<INotifyPropertyChanged, int> _itemSubscriptionCounts = new();
        private List<ConditionalFormattingDescriptor> _cellDescriptors = new();
        private List<ConditionalFormattingDescriptor> _rowDescriptors = new();
        private IDataGridCollectionView _view;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridConditionalFormattingAdapter(
            IConditionalFormattingModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));

            _model.FormattingChanged += OnModelFormattingChanged;
            BuildDescriptorCache();
        }

        public event EventHandler FormattingChanged;

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
                UpdateItemSubscriptionsFromView();
                RaiseFormattingChanged();
                return;
            }

            DetachView();
            _view = view;

            if (_view is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += View_CollectionChanged;
            }

            UpdateItemSubscriptionsFromView();
            RaiseFormattingChanged();
        }

        public void Dispose()
        {
            DetachView();
            _model.FormattingChanged -= OnModelFormattingChanged;
        }

        public ConditionalFormattingDescriptor MatchCell(object item, int rowIndex, DataGridColumn column)
        {
            if (item == null || column == null || _cellDescriptors.Count == 0)
            {
                return null;
            }

            if (item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return null;
            }

            object cellValue = null;
            bool cellValueComputed = false;
            ConditionalFormattingDescriptor matched = null;

            foreach (var descriptor in _cellDescriptors)
            {
                if (!MatchesColumn(descriptor, column))
                {
                    continue;
                }

                if (!TryEvaluateDescriptor(descriptor, item, rowIndex, column, ref cellValue, ref cellValueComputed, out var isMatch))
                {
                    continue;
                }

                if (isMatch)
                {
                    matched = descriptor;
                    if (descriptor.StopIfTrue)
                    {
                        break;
                    }
                }
            }

            return matched;
        }

        public ConditionalFormattingDescriptor MatchRow(object item, int rowIndex)
        {
            if (item == null || _rowDescriptors.Count == 0)
            {
                return null;
            }

            if (item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return null;
            }

            ConditionalFormattingDescriptor matched = null;
            object cellValue = null;
            bool cellValueComputed = false;

            foreach (var descriptor in _rowDescriptors)
            {
                if (!TryEvaluateDescriptor(descriptor, item, rowIndex, column: null, ref cellValue, ref cellValueComputed, out var isMatch))
                {
                    continue;
                }

                if (isMatch)
                {
                    matched = descriptor;
                    if (descriptor.StopIfTrue)
                    {
                        break;
                    }
                }
            }

            return matched;
        }

        private void OnModelFormattingChanged(object sender, ConditionalFormattingChangedEventArgs e)
        {
            BuildDescriptorCache();
            RaiseFormattingChanged();
        }

        private void View_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                RaiseFormattingChanged();
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                UpdateItemSubscriptionsFromView();
            }
            else
            {
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        RemoveItemSubscription(item);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        AddItemSubscription(item);
                    }
                }
            }
            RaiseFormattingChanged();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_model.Descriptors.Count == 0)
            {
                return;
            }

            RaiseFormattingChanged();
        }

        private void RaiseFormattingChanged()
        {
            FormattingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BuildDescriptorCache()
        {
            if (_model.Descriptors == null || _model.Descriptors.Count == 0)
            {
                _cellDescriptors = new List<ConditionalFormattingDescriptor>();
                _rowDescriptors = new List<ConditionalFormattingDescriptor>();
                return;
            }

            var ordered = _model.Descriptors
                .Select((descriptor, index) => new { descriptor, index })
                .OrderBy(entry => entry.descriptor.Priority)
                .ThenBy(entry => entry.index)
                .Select(entry => entry.descriptor)
                .ToList();

            _cellDescriptors = ordered
                .Where(d => d.Target == ConditionalFormattingTarget.Cell)
                .ToList();
            _rowDescriptors = ordered
                .Where(d => d.Target == ConditionalFormattingTarget.Row)
                .ToList();
        }

        private bool TryEvaluateDescriptor(
            ConditionalFormattingDescriptor descriptor,
            object item,
            int rowIndex,
            DataGridColumn column,
            ref object cellValue,
            ref bool cellValueComputed,
            out bool isMatch)
        {
            isMatch = false;
            if (descriptor == null)
            {
                return false;
            }

            object value = null;
            object source = item;

            if (descriptor.ValueSource == ConditionalFormattingValueSource.Cell)
            {
                if (!cellValueComputed)
                {
                    cellValue = GetCellValue(item, column);
                    cellValueComputed = true;
                }

                source = cellValue;
            }

            if (source != null && !string.IsNullOrEmpty(descriptor.PropertyPath))
            {
                value = GetNestedPropertyValue(source, descriptor.PropertyPath);
            }
            else
            {
                value = source;
            }

            var context = new ConditionalFormattingContext(
                item,
                rowIndex,
                column,
                cellValue,
                value,
                descriptor.PropertyPath,
                descriptor.ValueSource);

            if (descriptor.Predicate != null)
            {
                isMatch = descriptor.Predicate(context);
                return true;
            }

            if (descriptor.Operator == ConditionalFormattingOperator.Custom)
            {
                return true;
            }

            isMatch = EvaluateOperator(descriptor, value);
            return true;
        }

        private bool EvaluateOperator(ConditionalFormattingDescriptor descriptor, object value)
        {
            switch (descriptor.Operator)
            {
                case ConditionalFormattingOperator.Equals:
                    return Equals(value, descriptor.Value);
                case ConditionalFormattingOperator.NotEquals:
                    return !Equals(value, descriptor.Value);
                case ConditionalFormattingOperator.Contains:
                    return Contains(value, descriptor.Value, descriptor.StringComparisonMode);
                case ConditionalFormattingOperator.StartsWith:
                    return StartsWith(value, descriptor.Value, descriptor.StringComparisonMode);
                case ConditionalFormattingOperator.EndsWith:
                    return EndsWith(value, descriptor.Value, descriptor.StringComparisonMode);
                case ConditionalFormattingOperator.GreaterThan:
                    return TryCompare(value, descriptor.Value, ResolveCulture(descriptor), out var greaterThan) && greaterThan > 0;
                case ConditionalFormattingOperator.GreaterThanOrEqual:
                    return TryCompare(value, descriptor.Value, ResolveCulture(descriptor), out var greaterThanOrEqual) && greaterThanOrEqual >= 0;
                case ConditionalFormattingOperator.LessThan:
                    return TryCompare(value, descriptor.Value, ResolveCulture(descriptor), out var lessThan) && lessThan < 0;
                case ConditionalFormattingOperator.LessThanOrEqual:
                    return TryCompare(value, descriptor.Value, ResolveCulture(descriptor), out var lessThanOrEqual) && lessThanOrEqual <= 0;
                case ConditionalFormattingOperator.Between:
                    return Between(value, descriptor.Values, ResolveCulture(descriptor));
                case ConditionalFormattingOperator.In:
                    return In(value, descriptor.Values);
                default:
                    return false;
            }
        }

        private CultureInfo ResolveCulture(ConditionalFormattingDescriptor descriptor)
        {
            if (descriptor?.Culture != null)
            {
                return descriptor.Culture;
            }

            return _view?.Culture ?? CultureInfo.CurrentCulture;
        }

        private static bool MatchesColumn(ConditionalFormattingDescriptor descriptor, DataGridColumn column)
        {
            if (descriptor.ColumnId == null)
            {
                return true;
            }

            if (ReferenceEquals(descriptor.ColumnId, column))
            {
                return true;
            }

            var definition = DataGridColumnMetadata.GetDefinition(column);
            if (definition != null)
            {
                if (ReferenceEquals(descriptor.ColumnId, definition))
                {
                    return true;
                }

                if (definition.ColumnKey != null && Equals(definition.ColumnKey, descriptor.ColumnId))
                {
                    return true;
                }
            }

            if (descriptor.ColumnId is string path)
            {
                var propertyPath = column?.GetSortPropertyName();
                return !string.IsNullOrEmpty(propertyPath)
                    && string.Equals(propertyPath, path, StringComparison.Ordinal);
            }

            return false;
        }

        private object GetCellValue(object item, DataGridColumn column)
        {
            if (item == null || column == null)
            {
                return null;
            }

            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null)
            {
                return accessor.GetValue(item);
            }

            var propertyPath = column.GetSortPropertyName();
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var getter = GetGetter(propertyPath);
            return getter?.Invoke(item);
        }

        private object GetNestedPropertyValue(object source, string propertyPath)
        {
            if (source == null || string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var getter = GetGetter(propertyPath);
            return getter?.Invoke(source);
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

            return item =>
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

                var compiled = new Func<object, object>(o => TypeHelper.GetNestedPropertyValue(o, propertyPath));
                _getterCache[(type, propertyPath)] = compiled;
                return compiled(item);
            };
        }

        private void DetachView()
        {
            if (_view is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged -= View_CollectionChanged;
            }

            _view = null;
            ClearItemSubscriptions();
        }

        private void UpdateItemSubscriptionsFromView()
        {
            ClearItemSubscriptions();

            if (_view == null)
            {
                return;
            }

            foreach (var item in _view)
            {
                AddItemSubscription(item);
            }
        }

        private void AddItemSubscription(object item)
        {
            if (item == null || item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return;
            }

            if (item is INotifyPropertyChanged inpc)
            {
                if (_itemSubscriptionCounts.TryGetValue(inpc, out var count))
                {
                    _itemSubscriptionCounts[inpc] = count + 1;
                    return;
                }

                _itemSubscriptionCounts[inpc] = 1;
                inpc.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void RemoveItemSubscription(object item)
        {
            if (item == null)
            {
                return;
            }

            if (item is INotifyPropertyChanged inpc &&
                _itemSubscriptionCounts.TryGetValue(inpc, out var count))
            {
                if (count <= 1)
                {
                    _itemSubscriptionCounts.Remove(inpc);
                    inpc.PropertyChanged -= Item_PropertyChanged;
                }
                else
                {
                    _itemSubscriptionCounts[inpc] = count - 1;
                }
            }
        }

        private void ClearItemSubscriptions()
        {
            if (_itemSubscriptionCounts.Count == 0)
            {
                return;
            }

            foreach (var item in _itemSubscriptionCounts.Keys)
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            _itemSubscriptionCounts.Clear();
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

            try
            {
                if (left is string leftString && right is string rightString)
                {
                    result = string.Compare(leftString, rightString, culture, CompareOptions.None);
                    return true;
                }

                if (left is IComparable comparable)
                {
                    object rightValue = right;
                    if (!left.GetType().IsAssignableFrom(right.GetType()))
                    {
                        rightValue = Convert.ChangeType(right, left.GetType(), culture);
                    }

                    result = comparable.CompareTo(rightValue);
                    return true;
                }

                var comparer = culture != null
                    ? Comparer<object>.Create((x, y) =>
                        string.Compare(Convert.ToString(x, culture), Convert.ToString(y, culture), StringComparison.Ordinal))
                    : Comparer<object>.Default;

                result = comparer.Compare(left, right);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }

        private static bool Between(object value, IReadOnlyList<object> values, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                return false;
            }

            return TryCompare(value, values[0], culture, out var lower) && lower >= 0
                && TryCompare(value, values[1], culture, out var upper) && upper <= 0;
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

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridConditionalFormattingAdapterFactory
    {
        DataGridConditionalFormattingAdapter Create(DataGrid grid, IConditionalFormattingModel model);
    }
}
