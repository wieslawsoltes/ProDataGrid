// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Controls.DataGridConditionalFormatting;

namespace Avalonia.Controls
{
    /// <summary>Describes a reflection-free generated grouping field.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedGroupField<TItem>
    {
        /// <summary>Gets the stable column key.</summary>
        string ColumnKey { get; }
        /// <summary>Gets group precedence.</summary>
        int Order { get; }
        /// <summary>Gets default group direction.</summary>
        ListSortDirection Direction { get; }
        /// <summary>Gets a boxed key for adapter boundaries.</summary>
        object GetKey(TItem item);
        /// <summary>Formats a group key.</summary>
        string FormatKey(TItem item, IFormatProvider formatProvider);
        /// <summary>Creates an Avalonia collection-view group description with a direct typed getter.</summary>
        DataGridGroupDescription CreateDescription();
        /// <summary>Creates a non-reflection group-order comparer for the collection view.</summary>
        IComparer CreateSortComparer();
    }

    /// <summary>Provides a typed generated grouping field and adapter.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The group-key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedGroupField<TItem, TValue> : IDataGridGeneratedGroupField<TItem>
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly Func<TValue, IFormatProvider, string> _formatter;
        private readonly IEqualityComparer<TValue> _comparer;
        private readonly IComparer<TValue> _orderComparer;

        /// <summary>Initializes a generated grouping field.</summary>
        public DataGridGeneratedGroupField(
            string columnKey,
            int order,
            ListSortDirection direction,
            Func<TItem, TValue> getter,
            Func<TValue, IFormatProvider, string> formatter = null,
            IEqualityComparer<TValue> comparer = null,
            IComparer<TValue> orderComparer = null)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            Order = order;
            Direction = direction;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _formatter = formatter;
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
            _orderComparer = orderComparer ?? Comparer<TValue>.Default;
        }

        /// <inheritdoc />
        public string ColumnKey { get; }
        /// <inheritdoc />
        public int Order { get; }
        /// <inheritdoc />
        public ListSortDirection Direction { get; }
        /// <summary>Gets a typed key.</summary>
        public TValue GetTypedKey(TItem item) => _getter(item);
        /// <inheritdoc />
        public object GetKey(TItem item) => _getter(item);
        /// <inheritdoc />
        public string FormatKey(TItem item, IFormatProvider formatProvider)
        {
            TValue value = _getter(item);
            return _formatter != null
                ? _formatter(value, formatProvider)
                : value is null ? string.Empty : value.ToString();
        }
        /// <inheritdoc />
        public DataGridGroupDescription CreateDescription() =>
            new DataGridGeneratedGroupDescription<TItem, TValue>(ColumnKey, _getter, _comparer);

        /// <inheritdoc />
        public IComparer CreateSortComparer() => new GroupItemComparer(_getter, _orderComparer);

        private sealed class GroupItemComparer : IComparer
        {
            private readonly Func<TItem, TValue> _getter;
            private readonly IComparer<TValue> _comparer;

            public GroupItemComparer(Func<TItem, TValue> getter, IComparer<TValue> comparer)
            {
                _getter = getter;
                _comparer = comparer;
            }

            public int Compare(object left, object right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left is null) return -1;
                if (right is null) return 1;
                return _comparer.Compare(_getter((TItem)left), _getter((TItem)right));
            }
        }
    }

    /// <summary>Adapts a typed generated grouping getter to <see cref="DataGridGroupDescription"/>.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The group-key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedGroupDescription<TItem, TValue> : DataGridGroupDescription
    {
        private readonly string _columnKey;
        private readonly Func<TItem, TValue> _getter;
        private readonly IEqualityComparer<TValue> _comparer;

        /// <summary>Initializes the group-description adapter.</summary>
        public DataGridGeneratedGroupDescription(string columnKey, Func<TItem, TValue> getter, IEqualityComparer<TValue> comparer = null)
        {
            _columnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
        }

        /// <inheritdoc />
        public override string PropertyName => _columnKey;
        /// <inheritdoc />
        public override object GroupKeyFromItem(object item, int level, CultureInfo culture) => _getter((TItem)item);
        /// <inheritdoc />
        public override bool KeysMatch(object groupKey, object itemKey) =>
            groupKey is TValue left && itemKey is TValue right && _comparer.Equals(left, right);
    }

    /// <summary>Provides an incrementally maintained generated summary.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedSummary<TItem>
    {
        /// <summary>Gets the stable column key.</summary>
        string ColumnKey { get; }
        /// <summary>Gets the aggregate kind.</summary>
        DataGridAggregateType Aggregate { get; }
        /// <summary>Gets summary scope.</summary>
        DataGridSummaryScope Scope { get; }
        /// <summary>Gets the current result.</summary>
        object Value { get; }
        /// <summary>Adds an item incrementally.</summary>
        void Add(TItem item);
        /// <summary>Removes an item incrementally.</summary>
        void Remove(TItem item);
        /// <summary>Replaces an item incrementally.</summary>
        void Replace(TItem oldItem, TItem newItem);
        /// <summary>Resets from a source.</summary>
        void Reset(IEnumerable<TItem> items);
    }

    /// <summary>Maintains count, distinct, sum, average, min, max, first, or last through a typed accessor.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The summary value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedSummary<TItem, TValue> : IDataGridGeneratedSummary<TItem>
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly Func<TValue, TValue, TValue> _add;
        private readonly Func<TValue, TValue, TValue> _subtract;
        private readonly Func<TValue, int, object> _average;
        private readonly IEqualityComparer<TValue> _equalityComparer;
        private readonly IComparer<TValue> _orderComparer;
        private readonly Dictionary<TValue, int> _counts;
        private readonly List<TValue> _ordered = new();
        private TValue _sum;
        private int _nullCount;

        /// <summary>Initializes an incremental generated summary.</summary>
        public DataGridGeneratedSummary(
            string columnKey,
            DataGridAggregateType aggregate,
            DataGridSummaryScope scope,
            Func<TItem, TValue> getter,
            TValue zero = default,
            Func<TValue, TValue, TValue> add = null,
            Func<TValue, TValue, TValue> subtract = null,
            Func<TValue, int, object> average = null,
            IEqualityComparer<TValue> equalityComparer = null,
            IComparer<TValue> orderComparer = null)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            Aggregate = aggregate;
            Scope = scope;
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _sum = zero;
            Zero = zero;
            _add = add;
            _subtract = subtract;
            _average = average;
            _equalityComparer = equalityComparer ?? EqualityComparer<TValue>.Default;
            _orderComparer = orderComparer ?? Comparer<TValue>.Default;
            _counts = new Dictionary<TValue, int>(_equalityComparer);
        }

        /// <inheritdoc />
        public string ColumnKey { get; }
        /// <inheritdoc />
        public DataGridAggregateType Aggregate { get; }
        /// <inheritdoc />
        public DataGridSummaryScope Scope { get; }
        /// <summary>Gets the configured additive identity.</summary>
        public TValue Zero { get; }
        /// <summary>Gets the number of accumulated rows.</summary>
        public int Count => _ordered.Count;
        /// <inheritdoc />
        public object Value
        {
            get
            {
                if (Aggregate == DataGridAggregateType.Count) return Count;
                if (Aggregate == DataGridAggregateType.CountDistinct) return _counts.Count + (_nullCount == 0 ? 0 : 1);
                if (Count == 0) return null;
                if (Aggregate == DataGridAggregateType.Sum) return _sum;
                if (Aggregate == DataGridAggregateType.Average) return _average == null ? null : _average(_sum, Count);
                if (Aggregate == DataGridAggregateType.First) return _ordered[0];
                if (Aggregate == DataGridAggregateType.Last) return _ordered[Count - 1];
                if (Aggregate == DataGridAggregateType.Min || Aggregate == DataGridAggregateType.Max)
                {
                    TValue result = _ordered[0];
                    foreach (TValue value in _counts.Keys)
                    {
                        int comparison = _orderComparer.Compare(value, result);
                        if (Aggregate == DataGridAggregateType.Min ? comparison < 0 : comparison > 0) result = value;
                    }
                    if (_nullCount != 0)
                    {
                        TValue nullValue = default;
                        int comparison = _orderComparer.Compare(nullValue, result);
                        if (Aggregate == DataGridAggregateType.Min ? comparison < 0 : comparison > 0) result = nullValue;
                    }
                    return result;
                }
                return null;
            }
        }

        /// <inheritdoc />
        public void Add(TItem item)
        {
            TValue value = _getter(item);
            _ordered.Add(value);
            if (value is null)
            {
                _nullCount++;
            }
            else
            {
                _counts.TryGetValue(value, out int count);
                _counts[value] = count + 1;
            }
            if (_add != null) _sum = _add(_sum, value);
        }

        /// <inheritdoc />
        public void Remove(TItem item)
        {
            TValue value = _getter(item);
            int orderedIndex = _ordered.FindIndex(candidate => _equalityComparer.Equals(candidate, value));
            if (orderedIndex < 0) return;
            _ordered.RemoveAt(orderedIndex);
            if (value is null)
            {
                _nullCount--;
            }
            else if (_counts.TryGetValue(value, out int count))
            {
                if (count <= 1) _counts.Remove(value); else _counts[value] = count - 1;
            }
            if (_subtract != null) _sum = _subtract(_sum, value);
        }

        /// <inheritdoc />
        public void Replace(TItem oldItem, TItem newItem) { Remove(oldItem); Add(newItem); }

        /// <inheritdoc />
        public void Reset(IEnumerable<TItem> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _ordered.Clear();
            _counts.Clear();
            _nullCount = 0;
            _sum = Zero;
            foreach (TItem item in items) Add(item);
        }
    }

    /// <summary>Provides a non-generic reflection-free conditional-format rule contract.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedConditionalRule
    {
        /// <summary>Gets the stable rule ID.</summary>
        string RuleId { get; }
        /// <summary>Gets the stable column key.</summary>
        string ColumnKey { get; }
        /// <summary>Gets the resource theme key.</summary>
        string ThemeKey { get; }
        /// <summary>Gets whether the rule targets a cell or its row.</summary>
        ConditionalFormattingTarget Target { get; }
        /// <summary>Gets rule precedence.</summary>
        int Priority { get; }
        /// <summary>Gets whether evaluation stops after a match.</summary>
        bool StopIfTrue { get; }
        /// <summary>Evaluates the rule for an untyped item without reflection.</summary>
        bool IsMatch(object item);
        /// <summary>Creates the runtime conditional-formatting descriptor.</summary>
        ConditionalFormattingDescriptor CreateDescriptor();
    }

    /// <summary>Represents a generated conditional-format predicate and style metadata.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The tested value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedConditionalRule<TItem, TValue> : IDataGridGeneratedConditionalRule
    {
        private readonly Func<ConditionalFormattingContext, bool> _contextPredicate;

        /// <summary>Initializes a typed generated rule.</summary>
        public DataGridGeneratedConditionalRule(
            string ruleId,
            string columnKey,
            Func<TItem, TValue> getter,
            Func<TItem, TValue, bool> predicate,
            string themeKey,
            int priority = 0,
            bool stopIfTrue = true,
            ConditionalFormattingTarget target = ConditionalFormattingTarget.Cell)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            Getter = getter ?? throw new ArgumentNullException(nameof(getter));
            Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            ThemeKey = themeKey;
            Priority = priority;
            StopIfTrue = stopIfTrue;
            Target = target;
            _contextPredicate = context => context.Item is TItem item && IsMatch(item);
        }

        /// <summary>Gets the stable rule ID.</summary>
        public string RuleId { get; }
        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets the direct typed accessor.</summary>
        public Func<TItem, TValue> Getter { get; }
        /// <summary>Gets the allocation-free predicate.</summary>
        public Func<TItem, TValue, bool> Predicate { get; }
        /// <summary>Gets the resource theme key.</summary>
        public string ThemeKey { get; }
        /// <summary>Gets whether the rule targets a cell or its row.</summary>
        public ConditionalFormattingTarget Target { get; }
        /// <summary>Gets rule precedence.</summary>
        public int Priority { get; }
        /// <summary>Gets whether evaluation stops after a match.</summary>
        public bool StopIfTrue { get; }
        /// <summary>Evaluates the rule for an item.</summary>
        public bool IsMatch(TItem item) => Predicate(item, Getter(item));
        /// <inheritdoc />
        bool IDataGridGeneratedConditionalRule.IsMatch(object item) => item is TItem typed && IsMatch(typed);
        /// <inheritdoc />
        public ConditionalFormattingDescriptor CreateDescriptor() =>
            new ConditionalFormattingDescriptor(
                RuleId,
                ConditionalFormattingOperator.Custom,
                columnId: ColumnKey,
                predicate: _contextPredicate,
                themeKey: ThemeKey,
                target: Target,
                valueSource: ConditionalFormattingValueSource.Item,
                stopIfTrue: StopIfTrue,
                priority: Priority);
    }

    /// <summary>Creates runtime conditional-formatting models from generated typed rules.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridGeneratedConditionalFormatting
    {
        /// <summary>Creates and populates a mutable runtime model without property reflection.</summary>
        public static IConditionalFormattingModel CreateModel(
            IReadOnlyList<IDataGridGeneratedConditionalRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var descriptors = new ConditionalFormattingDescriptor[rules.Count];
            for (int index = 0; index < rules.Count; index++)
            {
                IDataGridGeneratedConditionalRule rule = rules[index] ??
                    throw new ArgumentException("Generated conditional-formatting rules cannot contain null entries.", nameof(rules));
                descriptors[index] = rule.CreateDescriptor();
            }

            var model = new ConditionalFormattingModel();
            model.Apply(descriptors);
            return model;
        }
    }

    /// <summary>Describes one generated leaf in a stable column-band path.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedBandField
    {
        /// <summary>Initializes band metadata.</summary>
        public DataGridGeneratedBandField(string columnKey, IReadOnlyList<string> path, int order)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Order = order;
        }
        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets root-to-leaf band headers.</summary>
        public IReadOnlyList<string> Path { get; }
        /// <summary>Gets ordering within the leaf band.</summary>
        public int Order { get; }
    }
}
