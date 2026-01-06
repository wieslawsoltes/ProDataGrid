// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Avalonia.Controls.DataGridConditionalFormatting
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum ConditionalFormattingOperator
    {
        Equals,
        NotEquals,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Between,
        In,
        Custom
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum ConditionalFormattingTarget
    {
        Cell,
        Row
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum ConditionalFormattingValueSource
    {
        Cell,
        Item
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct ConditionalFormattingContext
    {
        public ConditionalFormattingContext(
            object item,
            int rowIndex,
            DataGridColumn column,
            object cellValue,
            object value,
            string propertyPath,
            ConditionalFormattingValueSource valueSource)
        {
            Item = item;
            RowIndex = rowIndex;
            Column = column;
            CellValue = cellValue;
            Value = value;
            PropertyPath = propertyPath;
            ValueSource = valueSource;
        }

        public object Item { get; }

        public int RowIndex { get; }

        public DataGridColumn Column { get; }

        public int ColumnIndex => Column?.Index ?? -1;

        public object CellValue { get; }

        public object Value { get; }

        public string PropertyPath { get; }

        public ConditionalFormattingValueSource ValueSource { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ConditionalFormattingDescriptor : IEquatable<ConditionalFormattingDescriptor>
    {
        public ConditionalFormattingDescriptor(
            object ruleId,
            ConditionalFormattingOperator @operator,
            object columnId = null,
            string propertyPath = null,
            object value = null,
            IReadOnlyList<object> values = null,
            Func<ConditionalFormattingContext, bool> predicate = null,
            ControlTheme theme = null,
            object themeKey = null,
            ConditionalFormattingTarget target = ConditionalFormattingTarget.Cell,
            ConditionalFormattingValueSource valueSource = ConditionalFormattingValueSource.Cell,
            bool stopIfTrue = true,
            int priority = 0,
            CultureInfo culture = null,
            StringComparison? stringComparison = null)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            ColumnId = columnId;
            PropertyPath = propertyPath;
            Operator = @operator;
            Value = value;
            Values = values;
            Predicate = predicate;
            Theme = theme;
            ThemeKey = themeKey;
            Target = target;
            ValueSource = target == ConditionalFormattingTarget.Row
                ? ConditionalFormattingValueSource.Item
                : valueSource;
            StopIfTrue = stopIfTrue;
            Priority = priority;
            Culture = culture;
            StringComparisonMode = stringComparison;

            if (@operator == ConditionalFormattingOperator.Custom && predicate == null)
            {
                throw new ArgumentException("Custom conditional formatting requires a predicate.", nameof(predicate));
            }
        }

        public object RuleId { get; }

        public object ColumnId { get; }

        public string PropertyPath { get; }

        public ConditionalFormattingOperator Operator { get; }

        public object Value { get; }

        public IReadOnlyList<object> Values { get; }

        public Func<ConditionalFormattingContext, bool> Predicate { get; }

        public ControlTheme Theme { get; }

        public object ThemeKey { get; }

        public ConditionalFormattingTarget Target { get; }

        public ConditionalFormattingValueSource ValueSource { get; }

        public bool StopIfTrue { get; }

        public int Priority { get; }

        public CultureInfo Culture { get; }

        public StringComparison? StringComparisonMode { get; }

        public bool HasPredicate => Predicate != null;

        public bool HasValues => Values != null && Values.Count > 0;

        public override bool Equals(object obj)
        {
            return Equals(obj as ConditionalFormattingDescriptor);
        }

        public bool Equals(ConditionalFormattingDescriptor other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            if (!Equals(RuleId, other.RuleId))
            {
                return false;
            }

            if (!Equals(ColumnId, other.ColumnId))
            {
                return false;
            }

            if (!string.Equals(PropertyPath, other.PropertyPath, StringComparison.Ordinal))
            {
                return false;
            }

            if (Operator != other.Operator)
            {
                return false;
            }

            if (!Equals(Value, other.Value))
            {
                return false;
            }

            if (!SequenceEqual(Values, other.Values))
            {
                return false;
            }

            if (!Equals(Predicate, other.Predicate))
            {
                return false;
            }

            if (!Equals(Theme, other.Theme))
            {
                return false;
            }

            if (!Equals(ThemeKey, other.ThemeKey))
            {
                return false;
            }

            if (Target != other.Target)
            {
                return false;
            }

            if (ValueSource != other.ValueSource)
            {
                return false;
            }

            if (StopIfTrue != other.StopIfTrue)
            {
                return false;
            }

            if (Priority != other.Priority)
            {
                return false;
            }

            if (!Equals(Culture, other.Culture))
            {
                return false;
            }

            if (StringComparisonMode != other.StringComparisonMode)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + (RuleId?.GetHashCode() ?? 0);
                hash = (hash * 23) + (ColumnId?.GetHashCode() ?? 0);
                hash = (hash * 23) + (PropertyPath?.GetHashCode() ?? 0);
                hash = (hash * 23) + Operator.GetHashCode();
                hash = (hash * 23) + (Value?.GetHashCode() ?? 0);
                hash = (hash * 23) + (Predicate?.GetHashCode() ?? 0);
                hash = (hash * 23) + (Theme?.GetHashCode() ?? 0);
                hash = (hash * 23) + (ThemeKey?.GetHashCode() ?? 0);
                hash = (hash * 23) + Target.GetHashCode();
                hash = (hash * 23) + ValueSource.GetHashCode();
                hash = (hash * 23) + StopIfTrue.GetHashCode();
                hash = (hash * 23) + Priority.GetHashCode();
                hash = (hash * 23) + (Culture?.GetHashCode() ?? 0);
                hash = (hash * 23) + (StringComparisonMode?.GetHashCode() ?? 0);
                if (Values != null)
                {
                    for (int i = 0; i < Values.Count; i++)
                    {
                        hash = (hash * 23) + (Values[i]?.GetHashCode() ?? 0);
                    }
                }
                return hash;
            }
        }

        private static bool SequenceEqual(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class ConditionalFormattingChangedEventArgs : EventArgs
    {
        public ConditionalFormattingChangedEventArgs(
            IReadOnlyList<ConditionalFormattingDescriptor> oldDescriptors,
            IReadOnlyList<ConditionalFormattingDescriptor> newDescriptors)
        {
            OldDescriptors = oldDescriptors ?? Array.Empty<ConditionalFormattingDescriptor>();
            NewDescriptors = newDescriptors ?? Array.Empty<ConditionalFormattingDescriptor>();
        }

        public IReadOnlyList<ConditionalFormattingDescriptor> OldDescriptors { get; }

        public IReadOnlyList<ConditionalFormattingDescriptor> NewDescriptors { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class ConditionalFormattingChangingEventArgs : EventArgs
    {
        public ConditionalFormattingChangingEventArgs(
            IReadOnlyList<ConditionalFormattingDescriptor> oldDescriptors,
            IReadOnlyList<ConditionalFormattingDescriptor> newDescriptors)
        {
            OldDescriptors = oldDescriptors ?? Array.Empty<ConditionalFormattingDescriptor>();
            NewDescriptors = newDescriptors ?? Array.Empty<ConditionalFormattingDescriptor>();
        }

        public IReadOnlyList<ConditionalFormattingDescriptor> OldDescriptors { get; }

        public IReadOnlyList<ConditionalFormattingDescriptor> NewDescriptors { get; }

        public bool Cancel { get; set; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IConditionalFormattingModel : INotifyPropertyChanged
    {
        IReadOnlyList<ConditionalFormattingDescriptor> Descriptors { get; }

        event EventHandler<ConditionalFormattingChangedEventArgs> FormattingChanged;

        event EventHandler<ConditionalFormattingChangingEventArgs> FormattingChanging;

        void SetOrUpdate(ConditionalFormattingDescriptor descriptor);

        void Apply(IEnumerable<ConditionalFormattingDescriptor> descriptors);

        void Clear();

        bool Remove(object ruleId);

        bool Move(object ruleId, int newIndex);

        void BeginUpdate();

        void EndUpdate();

        IDisposable DeferRefresh();
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridConditionalFormattingModelFactory
    {
        IConditionalFormattingModel Create();
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class ConditionalFormattingModel : IConditionalFormattingModel
    {
        private readonly List<ConditionalFormattingDescriptor> _descriptors = new();
        private readonly IReadOnlyList<ConditionalFormattingDescriptor> _readOnlyView;
        private int _updateNesting;
        private bool _hasPendingChange;
        private List<ConditionalFormattingDescriptor> _pendingOldDescriptors;

        public ConditionalFormattingModel()
        {
            _readOnlyView = _descriptors.AsReadOnly();
        }

        public IReadOnlyList<ConditionalFormattingDescriptor> Descriptors => _readOnlyView;

        public event EventHandler<ConditionalFormattingChangedEventArgs> FormattingChanged;

        public event EventHandler<ConditionalFormattingChangingEventArgs> FormattingChanging;

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetOrUpdate(ConditionalFormattingDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var next = new List<ConditionalFormattingDescriptor>(_descriptors);
            int index = IndexOf(descriptor.RuleId);
            if (index >= 0)
            {
                next[index] = descriptor;
            }
            else
            {
                next.Add(descriptor);
            }

            ApplyState(next);
        }

        public void Apply(IEnumerable<ConditionalFormattingDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            ApplyState(new List<ConditionalFormattingDescriptor>(descriptors));
        }

        public void Clear()
        {
            if (_descriptors.Count == 0)
            {
                return;
            }

            ApplyState(new List<ConditionalFormattingDescriptor>());
        }

        public bool Remove(object ruleId)
        {
            if (ruleId == null)
            {
                throw new ArgumentNullException(nameof(ruleId));
            }

            int index = IndexOf(ruleId);
            if (index < 0)
            {
                return false;
            }

            var next = new List<ConditionalFormattingDescriptor>(_descriptors);
            next.RemoveAt(index);
            ApplyState(next);
            return true;
        }

        public bool Move(object ruleId, int newIndex)
        {
            if (ruleId == null)
            {
                throw new ArgumentNullException(nameof(ruleId));
            }

            if (newIndex < 0 || newIndex >= _descriptors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            int index = IndexOf(ruleId);
            if (index < 0 || index == newIndex)
            {
                return false;
            }

            var next = new List<ConditionalFormattingDescriptor>(_descriptors);
            var descriptor = next[index];
            next.RemoveAt(index);
            next.Insert(newIndex, descriptor);
            ApplyState(next);
            return true;
        }

        public void BeginUpdate()
        {
            _updateNesting++;
        }

        public void EndUpdate()
        {
            if (_updateNesting == 0)
            {
                return;
            }

            _updateNesting--;

            if (_updateNesting != 0 || !_hasPendingChange)
            {
                return;
            }

            _hasPendingChange = false;
            var oldDescriptors = _pendingOldDescriptors ?? new List<ConditionalFormattingDescriptor>();
            _pendingOldDescriptors = null;
            RaiseFormattingChanged(oldDescriptors, new List<ConditionalFormattingDescriptor>(_descriptors));
        }

        public IDisposable DeferRefresh()
        {
            BeginUpdate();
            return new UpdateScope(this);
        }

        private void ApplyState(List<ConditionalFormattingDescriptor> next)
        {
            for (int i = 0; i < next.Count; i++)
            {
                if (next[i] == null)
                {
                    throw new ArgumentException("Conditional formatting descriptors cannot contain null entries.", nameof(next));
                }
            }

            EnsureUniqueRuleIds(next);

            if (SequenceEqual(_descriptors, next))
            {
                return;
            }

            var oldDescriptors = new List<ConditionalFormattingDescriptor>(_descriptors);

            if (!RaiseFormattingChanging(oldDescriptors, new List<ConditionalFormattingDescriptor>(next)))
            {
                return;
            }

            _descriptors.Clear();
            _descriptors.AddRange(next);

            if (_updateNesting > 0)
            {
                if (!_hasPendingChange)
                {
                    _pendingOldDescriptors = oldDescriptors;
                }

                _hasPendingChange = true;
                return;
            }

            RaiseFormattingChanged(oldDescriptors, new List<ConditionalFormattingDescriptor>(_descriptors));
        }

        private bool RaiseFormattingChanging(
            IReadOnlyList<ConditionalFormattingDescriptor> oldDescriptors,
            IReadOnlyList<ConditionalFormattingDescriptor> newDescriptors)
        {
            var handler = FormattingChanging;
            if (handler == null)
            {
                return true;
            }

            var args = new ConditionalFormattingChangingEventArgs(
                new List<ConditionalFormattingDescriptor>(oldDescriptors),
                new List<ConditionalFormattingDescriptor>(newDescriptors));
            handler(this, args);
            return !args.Cancel;
        }

        private void RaiseFormattingChanged(
            IReadOnlyList<ConditionalFormattingDescriptor> oldDescriptors,
            IReadOnlyList<ConditionalFormattingDescriptor> newDescriptors)
        {
            FormattingChanged?.Invoke(this, new ConditionalFormattingChangedEventArgs(oldDescriptors, newDescriptors));
        }

        private int IndexOf(object ruleId)
        {
            for (int i = 0; i < _descriptors.Count; i++)
            {
                if (Equals(_descriptors[i].RuleId, ruleId))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void EnsureUniqueRuleIds(List<ConditionalFormattingDescriptor> descriptors)
        {
            var set = new HashSet<object>();
            for (int i = 0; i < descriptors.Count; i++)
            {
                var ruleId = descriptors[i].RuleId;
                if (!set.Add(ruleId))
                {
                    throw new ArgumentException($"Duplicate conditional formatting rule id '{ruleId}'.", nameof(descriptors));
                }
            }
        }

        private static bool SequenceEqual(
            IReadOnlyList<ConditionalFormattingDescriptor> left,
            IReadOnlyList<ConditionalFormattingDescriptor> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class UpdateScope : IDisposable
        {
            private readonly ConditionalFormattingModel _owner;
            private bool _disposed;

            public UpdateScope(ConditionalFormattingModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.EndUpdate();
            }
        }
    }
}
