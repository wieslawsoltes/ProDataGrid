// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridPivoting;
using Avalonia.Data.Core;
using Avalonia.Data.Converters;

namespace Avalonia.Controls.DataGridReporting
{
    internal sealed class OutlineReportBuilder
    {
        private static readonly IPropertyInfo CellValuesProperty = new ClrPropertyInfo(
            nameof(OutlineRow.CellValues),
            target => ((OutlineRow)((HierarchicalNode)target).Item!).CellValues,
            null,
            typeof(object?[]));

        private static readonly Func<HierarchicalNode, object?[]> CellValuesGetter =
            node => ((OutlineRow)node.Item!).CellValues;

        private readonly OutlineReportModel _model;
        private readonly PivotAggregatorRegistry _aggregators;
        private readonly CultureInfo _culture;

        public OutlineReportBuilder(OutlineReportModel model, PivotAggregatorRegistry aggregators, CultureInfo culture)
        {
            _model = model;
            _aggregators = aggregators;
            _culture = culture;
        }

        public OutlineBuildResult Build()
        {
            var itemsSource = _model.ItemsSource;
            if (itemsSource == null)
            {
                return OutlineBuildResult.Empty;
            }

            var groupFields = _model.GroupFields;
            var valueFields = _model.ValueFields;
            var layout = _model.Layout;
            var valueFieldCount = valueFields.Count;

            var root = new OutlineGroupNode(null, -1, null, null, Array.Empty<object?>(), Array.Empty<object?>(), valueFieldCount, valueFields, _aggregators);
            var groupFieldCount = groupFields.Count;
            var groupValues = new object?[groupFieldCount];
            var groupDisplayValues = new object?[groupFieldCount];
            var groupNodes = new OutlineGroupNode[groupFieldCount + 1];

            var valueFieldRequiresAggregation = new bool[valueFieldCount];
            for (var i = 0; i < valueFieldCount; i++)
            {
                var field = valueFields[i];
                valueFieldRequiresAggregation[i] = field.AggregateType != PivotAggregateType.None;
            }

            foreach (var item in itemsSource)
            {
                if (item == null)
                {
                    continue;
                }

                for (var i = 0; i < groupFieldCount; i++)
                {
                    var field = groupFields[i];
                    var value = field.GetGroupValue(item);
                    groupValues[i] = value;
                    groupDisplayValues[i] = field.FormatValue(value, _culture, layout.EmptyValueLabel);
                }

                groupNodes[0] = root;
                for (var i = 0; i < groupFieldCount; i++)
                {
                    groupNodes[i + 1] = groupNodes[i].GetOrCreateChild(groupFields[i], groupValues[i], groupDisplayValues[i], _culture, layout.EmptyValueLabel);
                }

                if (layout.ShowDetailRows)
                {
                    if (groupFieldCount == 0)
                    {
                        root.Items.Add(item);
                    }
                    else
                    {
                        groupNodes[groupFieldCount].Items.Add(item);
                    }
                }

                if (valueFieldCount > 0)
                {
                    for (var valueIndex = 0; valueIndex < valueFieldCount; valueIndex++)
                    {
                        if (!valueFieldRequiresAggregation[valueIndex])
                        {
                            continue;
                        }

                        var value = valueFields[valueIndex].GetValue(item);
                        for (var level = 0; level <= groupFieldCount; level++)
                        {
                            groupNodes[level].AddValue(valueIndex, value);
                        }
                    }
                }
            }

            SortTree(root, groupFields, _culture);

            var rows = new List<OutlineRow>();
            if (groupFieldCount == 0)
            {
                if (layout.ShowDetailRows)
                {
                    BuildDetailRows(root, valueFields, layout, _culture, rows);
                }
            }
            else
            {
                foreach (var child in root.Children)
                {
                    rows.Add(BuildGroupRow(child, groupFields, valueFields, layout, _culture));
                }
            }

            if (layout.ShowGrandTotal && valueFieldCount > 0)
            {
                var totalRow = new OutlineRow(
                    OutlineRowType.GrandTotal,
                    0,
                    Array.Empty<object?>(),
                    Array.Empty<object?>(),
                    layout.GrandTotalLabel,
                    0d,
                    valueFieldCount,
                    null)
                {
                    IsExpanded = false
                };

                for (var i = 0; i < valueFieldCount; i++)
                {
                    totalRow.SetCellValue(i, root.GetResult(i));
                }

                rows.Add(totalRow);
            }

            var columnDefinitions = BuildColumnDefinitions(valueFields, layout, _culture);
            return new OutlineBuildResult(rows, columnDefinitions);
        }

        private static OutlineRow BuildGroupRow(
            OutlineGroupNode node,
            IList<OutlineGroupField> groupFields,
            IList<OutlineValueField> valueFields,
            OutlineLayoutOptions layout,
            CultureInfo culture)
        {
            var level = node.Level;
            var field = groupFields[level];
            var valueCount = valueFields.Count;
            var showTotals = layout.ShowSubtotals && field.ShowSubtotals;
            var labelText = node.DisplayValue ?? string.Empty;
            var label = showTotals ? FormatSubtotalLabel(labelText, layout, culture) : labelText;

            var rowType = showTotals ? OutlineRowType.Subtotal : OutlineRowType.Detail;
            var row = new OutlineRow(
                rowType,
                level,
                node.PathValues,
                node.PathDisplayValues,
                label,
                level * layout.Indent,
                valueCount,
                null)
            {
                IsExpanded = layout.AutoExpandGroups
            };

            if (showTotals)
            {
                for (var i = 0; i < valueCount; i++)
                {
                    row.SetCellValue(i, node.GetResult(i));
                }
            }

            if (node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    row.MutableChildren.Add(BuildGroupRow(child, groupFields, valueFields, layout, culture));
                }
            }

            if (layout.ShowDetailRows && level == groupFields.Count - 1 && node.Items.Count > 0)
            {
                BuildDetailRows(node, valueFields, layout, culture, row.MutableChildren);
            }

            row.IsExpandable = row.Children.Count > 0;
            return row;
        }

        private static void BuildDetailRows(
            OutlineGroupNode node,
            IList<OutlineValueField> valueFields,
            OutlineLayoutOptions layout,
            CultureInfo culture,
            List<OutlineRow> target)
        {
            foreach (var item in node.Items)
            {
                var label = GetDetailLabel(item, layout, culture);
                var row = new OutlineRow(
                    OutlineRowType.Detail,
                    node.Level + 1,
                    node.PathValues,
                    node.PathDisplayValues,
                    label,
                    (node.Level + 1) * layout.Indent,
                    valueFields.Count,
                    item)
                {
                    IsExpanded = false,
                    IsExpandable = false
                };

                for (var i = 0; i < valueFields.Count; i++)
                {
                    row.SetCellValue(i, valueFields[i].GetValue(item));
                }

                target.Add(row);
            }
        }

        private static string GetDetailLabel(object? item, OutlineLayoutOptions layout, CultureInfo culture)
        {
            if (item == null)
            {
                return layout.EmptyValueLabel ?? string.Empty;
            }

            if (layout.DetailLabelSelector != null)
            {
                return layout.DetailLabelSelector(item) ?? string.Empty;
            }

            return Convert.ToString(item, culture) ?? string.Empty;
        }

        private static string FormatSubtotalLabel(string value, OutlineLayoutOptions layout, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(layout.SubtotalLabelFormat))
            {
                return value;
            }

            try
            {
                return string.Format(culture, layout.SubtotalLabelFormat, value);
            }
            catch
            {
                return value;
            }
        }

        private static List<DataGridColumnDefinition> BuildColumnDefinitions(
            IList<OutlineValueField> valueFields,
            OutlineLayoutOptions layout,
            CultureInfo culture)
        {
            var definitions = new List<DataGridColumnDefinition>();

            var rowHeaderColumn = new DataGridHierarchicalColumnDefinition
            {
                Header = layout.RowHeaderLabel,
                Binding = CreateNodeBinding<OutlineRow>("Item", row => row),
                CellTemplateKey = "DataGridOutlineRowHeaderTemplate",
                Indent = layout.Indent,
                IsReadOnly = true,
                CanUserSort = false,
                CanUserReorder = false,
                CanUserResize = true
            };

            definitions.Add(rowHeaderColumn);

            for (var i = 0; i < valueFields.Count; i++)
            {
                var valueField = valueFields[i];
                var stringFormat = valueField.StringFormat;
                var converterCulture = valueField.FormatProvider as CultureInfo ?? culture;
                var isNumericColumn = IsNumericValueField(valueField);
                var targetNullValue = isNumericColumn ? null : GetTargetNullValue(valueField, layout.EmptyValueLabel);
                var bindingStringFormat = isNumericColumn ? null : stringFormat;

                var binding = CreateArrayBinding(
                    CellValuesProperty,
                    CellValuesGetter,
                    i,
                    valueField.Converter,
                    valueField.ConverterParameter,
                    bindingStringFormat,
                    targetNullValue,
                    converterCulture);

                DataGridColumnDefinition columnDefinition;
                if (isNumericColumn)
                {
                    var numericColumn = new DataGridNumericColumnDefinition
                    {
                        Binding = binding,
                        IsReadOnly = true,
                        CanUserSort = false,
                        CanUserReorder = false,
                        CanUserResize = true
                    };

                    if (!string.IsNullOrEmpty(stringFormat))
                    {
                        numericColumn.FormatString = stringFormat;
                    }

                    if (valueField.FormatProvider is NumberFormatInfo numberFormat)
                    {
                        numericColumn.NumberFormat = numberFormat;
                    }

                    columnDefinition = numericColumn;
                }
                else
                {
                    columnDefinition = new DataGridTextColumnDefinition
                    {
                        Binding = binding,
                        IsReadOnly = true,
                        CanUserSort = false,
                        CanUserReorder = false,
                        CanUserResize = true
                    };
                }

                columnDefinition.Header = valueField.Header ?? $"Value {i + 1}";
                definitions.Add(columnDefinition);
            }

            return definitions;
        }

        private static DataGridBindingDefinition CreateNodeBinding<TValue>(string name, Func<OutlineRow, TValue> getter)
        {
            var propertyInfo = new ClrPropertyInfo(
                name,
                target => getter((OutlineRow)((HierarchicalNode)target).Item!),
                null,
                typeof(TValue));

            return DataGridBindingDefinition.Create<HierarchicalNode, TValue>(propertyInfo, node => getter((OutlineRow)node.Item!));
        }

        private static DataGridBindingDefinition CreateArrayBinding(
            IPropertyInfo property,
            Func<HierarchicalNode, object?[]> getter,
            int index,
            IValueConverter? converter,
            object? converterParameter,
            string? stringFormat,
            object? targetNullValue,
            CultureInfo? converterCulture)
        {
            var binding = DataGridBindingDefinition.CreateCached(property, getter);
            binding.Converter = new OutlineArrayIndexConverter(index, converter, converterParameter);
            binding.ConverterParameter = converterParameter;
            if (!string.IsNullOrEmpty(stringFormat))
            {
                binding.StringFormat = stringFormat;
            }

            if (targetNullValue != null)
            {
                binding.TargetNullValue = targetNullValue;
            }

            if (converterCulture != null)
            {
                binding.ConverterCulture = converterCulture;
            }

            return binding;
        }

        private static object? GetTargetNullValue(OutlineValueField? field, string? emptyValueLabel)
        {
            if (field != null && !string.IsNullOrEmpty(field.NullLabel))
            {
                return field.NullLabel;
            }

            if (!string.IsNullOrEmpty(emptyValueLabel))
            {
                return emptyValueLabel;
            }

            return null;
        }

        private static bool IsNumericValueField(OutlineValueField? field)
        {
            if (field == null)
            {
                return false;
            }

            if (field.ValueType != null)
            {
                var type = Nullable.GetUnderlyingType(field.ValueType) ?? field.ValueType;
                return type == typeof(byte) ||
                    type == typeof(sbyte) ||
                    type == typeof(short) ||
                    type == typeof(ushort) ||
                    type == typeof(int) ||
                    type == typeof(uint) ||
                    type == typeof(long) ||
                    type == typeof(ulong) ||
                    type == typeof(float) ||
                    type == typeof(double) ||
                    type == typeof(decimal);
            }

            return field.AggregateType != PivotAggregateType.None &&
                   field.AggregateType != PivotAggregateType.First &&
                   field.AggregateType != PivotAggregateType.Last;
        }

        private static void SortTree(OutlineGroupNode root, IList<OutlineGroupField> fields, CultureInfo culture)
        {
            if (fields.Count == 0)
            {
                return;
            }

            var comparer = new OutlineValueComparer(culture);
            SortNode(root, fields, comparer);
        }

        private static void SortNode(OutlineGroupNode node, IList<OutlineGroupField> fields, OutlineValueComparer defaultComparer)
        {
            if (node.Children.Count == 0)
            {
                return;
            }

            var fieldIndex = node.Level + 1;
            if (fieldIndex < 0 || fieldIndex >= fields.Count)
            {
                return;
            }

            var field = fields[fieldIndex];
            var comparer = field.Comparer ?? defaultComparer;
            var direction = field.SortDirection ?? ListSortDirection.Ascending;

            node.Children.Sort((left, right) =>
            {
                var result = comparer.Compare(left.Key, right.Key);
                return direction == ListSortDirection.Descending ? -result : result;
            });

            foreach (var child in node.Children)
            {
                SortNode(child, fields, defaultComparer);
            }
        }

        private sealed class OutlineGroupNode
        {
            private static readonly object NullKey = new();
            private readonly Dictionary<object, OutlineGroupNode> _childrenByKey;
            private readonly IPivotAggregationState[] _states;

            private readonly IList<OutlineValueField> _valueFields;
            private readonly PivotAggregatorRegistry _registry;

            public OutlineGroupNode(
                OutlineGroupNode? parent,
                int level,
                object? key,
                string? displayValue,
                object?[] pathValues,
                object?[] pathDisplayValues,
                int valueFieldCount,
                IList<OutlineValueField> valueFields,
                PivotAggregatorRegistry registry)
            {
                Parent = parent;
                Level = level;
                Key = key;
                DisplayValue = displayValue;
                PathValues = pathValues;
                PathDisplayValues = pathDisplayValues;
                Items = new List<object?>();
                Children = new List<OutlineGroupNode>();
                _childrenByKey = new Dictionary<object, OutlineGroupNode>();
                _valueFields = valueFields;
                _registry = registry;
                _states = CreateStates(valueFieldCount, valueFields, registry);
            }

            public OutlineGroupNode? Parent { get; }

            public int Level { get; }

            public object? Key { get; }

            public string? DisplayValue { get; }

            public object?[] PathValues { get; }

            public object?[] PathDisplayValues { get; }

            public List<object?> Items { get; }

            public List<OutlineGroupNode> Children { get; }

            public OutlineGroupNode GetOrCreateChild(
                OutlineGroupField field,
                object? key,
                object? displayValue,
                CultureInfo culture,
                string? emptyValueLabel)
            {
                var lookupKey = key ?? NullKey;
                if (_childrenByKey.TryGetValue(lookupKey, out var existing))
                {
                    return existing;
                }

                var childLevel = Level + 1;
                var pathValues = new object?[childLevel + 1];
                var pathDisplayValues = new object?[childLevel + 1];
                if (PathValues.Length > 0)
                {
                    Array.Copy(PathValues, pathValues, PathValues.Length);
                }

                if (PathDisplayValues.Length > 0)
                {
                    Array.Copy(PathDisplayValues, pathDisplayValues, PathDisplayValues.Length);
                }

                pathValues[childLevel] = key;
                pathDisplayValues[childLevel] = displayValue ?? field.FormatValue(key, culture, emptyValueLabel);

                var node = new OutlineGroupNode(
                    this,
                    childLevel,
                    key,
                    pathDisplayValues[childLevel]?.ToString(),
                    pathValues,
                    pathDisplayValues,
                    _states.Length,
                    _valueFields,
                    _registry);

                _childrenByKey[lookupKey] = node;
                Children.Add(node);
                return node;
            }

            public void AddValue(int index, object? value)
            {
                if (index < 0 || index >= _states.Length)
                {
                    return;
                }

                _states[index].Add(value);
            }

            public object? GetResult(int index)
            {
                if (index < 0 || index >= _states.Length)
                {
                    return null;
                }

                return _states[index].GetResult();
            }

            private static IPivotAggregationState[] CreateStates(
                int valueFieldCount,
                IList<OutlineValueField> valueFields,
                PivotAggregatorRegistry registry)
            {
                var states = new IPivotAggregationState[valueFieldCount];
                for (var i = 0; i < valueFieldCount; i++)
                {
                    var field = i < valueFields.Count ? valueFields[i] : null;
                    if (field == null || field.AggregateType == PivotAggregateType.None)
                    {
                        states[i] = new NullPivotAggregationState();
                        continue;
                    }

                    var aggregator = field.AggregateType == PivotAggregateType.Custom
                        ? field.CustomAggregator
                        : registry.Get(field.AggregateType);

                    states[i] = aggregator?.CreateState() ?? new NullPivotAggregationState();
                }

                return states;
            }

            private sealed class NullPivotAggregationState : IPivotAggregationState
            {
                public void Add(object? value)
                {
                }

                public void Merge(IPivotAggregationState other)
                {
                }

                public object? GetResult() => null;
            }
        }

        private sealed class OutlineValueComparer : IComparer<object?>
        {
            private readonly CompareInfo _compareInfo;

            public OutlineValueComparer(CultureInfo culture)
            {
                _compareInfo = (culture ?? CultureInfo.CurrentCulture).CompareInfo;
            }

            public int Compare(object? x, object? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return 1;
                }

                if (y == null)
                {
                    return -1;
                }

                if (x is string xs && y is string ys)
                {
                    return _compareInfo.Compare(xs, ys, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                }

                if (x is IComparable comparable)
                {
                    return comparable.CompareTo(y);
                }

                return _compareInfo.Compare(x.ToString(), y.ToString(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            }
        }

        private sealed class OutlineArrayIndexConverter : IValueConverter
        {
            private readonly int _index;
            private readonly IValueConverter? _valueConverter;
            private readonly object? _converterParameter;

            public OutlineArrayIndexConverter(int index, IValueConverter? valueConverter, object? converterParameter)
            {
                _index = index;
                _valueConverter = valueConverter;
                _converterParameter = converterParameter;
            }

            public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                if (value is not object?[] values || _index < 0 || _index >= values.Length)
                {
                    return null;
                }

                var cellValue = values[_index];
                if (_valueConverter != null)
                {
                    return _valueConverter.Convert(cellValue, targetType, _converterParameter, culture);
                }

                return cellValue;
            }

            public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }

        internal sealed class OutlineBuildResult
        {
            public static readonly OutlineBuildResult Empty = new OutlineBuildResult(
                new List<OutlineRow>(),
                new List<DataGridColumnDefinition>());

            public OutlineBuildResult(List<OutlineRow> rows, List<DataGridColumnDefinition> columnDefinitions)
            {
                Rows = rows;
                ColumnDefinitions = columnDefinitions;
            }

            public List<OutlineRow> Rows { get; }

            public List<DataGridColumnDefinition> ColumnDefinitions { get; }
        }
    }
}
