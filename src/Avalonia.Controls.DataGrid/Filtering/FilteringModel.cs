// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Avalonia.Controls.DataGridFiltering
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum FilteringOperator
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
    sealed class FilteringDescriptor : IEquatable<FilteringDescriptor>
    {
        public FilteringDescriptor(
            object columnId,
            FilteringOperator @operator,
            string propertyPath = null,
            object value = null,
            IReadOnlyList<object> values = null,
            Func<object, bool> predicate = null,
            CultureInfo culture = null,
            StringComparison? stringComparison = null)
        {
            ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
            Operator = @operator;
            PropertyPath = propertyPath;
            Value = value;
            Values = values;
            Predicate = predicate;
            Culture = culture;
            StringComparisonMode = stringComparison;

            if (@operator == FilteringOperator.Custom && predicate == null)
            {
                throw new ArgumentException("Custom filtering requires a predicate.", nameof(predicate));
            }

            if (predicate == null && string.IsNullOrEmpty(propertyPath) && columnId == null)
            {
                throw new ArgumentException(
                    "Filtering descriptors require either a property path, a custom predicate, or a column id.",
                    nameof(propertyPath));
            }
        }

        public object ColumnId { get; }

        public string PropertyPath { get; }

        public FilteringOperator Operator { get; }

        public object Value { get; }

        public IReadOnlyList<object> Values { get; }

        public Func<object, bool> Predicate { get; }

        public CultureInfo Culture { get; }

        public StringComparison? StringComparisonMode { get; }

        /// <summary>
        /// True = <see cref="DataGridColumnDefinition" />, False = <see cref="DataGridColumn" />, Null = unknown
        /// </summary>
        internal bool? ColumnIdIsColumnDefinition { get; set; }

        public bool HasPredicate => Predicate != null;

        public override bool Equals(object obj)
        {
            return Equals(obj as FilteringDescriptor);
        }

        public bool Equals(FilteringDescriptor other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
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

            if (!ValuesEqual(Values, other.Values))
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

            if (!Equals(Predicate, other.Predicate))
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
                hash = (hash * 23) + (ColumnId?.GetHashCode() ?? 0);
                hash = (hash * 23) + (PropertyPath?.GetHashCode() ?? 0);
                hash = (hash * 23) + Operator.GetHashCode();
                hash = (hash * 23) + (Value?.GetHashCode() ?? 0);
                hash = (hash * 23) + ValuesHash(Values);
                hash = (hash * 23) + (Culture?.GetHashCode() ?? 0);
                hash = (hash * 23) + (StringComparisonMode?.GetHashCode() ?? 0);
                hash = (hash * 23) + (Predicate?.GetHashCode() ?? 0);
                return hash;
            }
        }

        private static bool ValuesEqual(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
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

        private static int ValuesHash(IReadOnlyList<object> values)
        {
            if (values == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < values.Count; i++)
                {
                    hash = (hash * 23) + (values[i]?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class FilteringChangedEventArgs : EventArgs
    {
        public FilteringChangedEventArgs(IReadOnlyList<FilteringDescriptor> oldDescriptors, IReadOnlyList<FilteringDescriptor> newDescriptors)
        {
            OldDescriptors = oldDescriptors ?? Array.Empty<FilteringDescriptor>();
            NewDescriptors = newDescriptors ?? Array.Empty<FilteringDescriptor>();
        }

        public IReadOnlyList<FilteringDescriptor> OldDescriptors { get; }

        public IReadOnlyList<FilteringDescriptor> NewDescriptors { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class FilteringChangingEventArgs : CancelEventArgs
    {
        public FilteringChangingEventArgs(IReadOnlyList<FilteringDescriptor> oldDescriptors, IReadOnlyList<FilteringDescriptor> newDescriptors)
        {
            OldDescriptors = oldDescriptors ?? Array.Empty<FilteringDescriptor>();
            NewDescriptors = newDescriptors ?? Array.Empty<FilteringDescriptor>();
        }

        public IReadOnlyList<FilteringDescriptor> OldDescriptors { get; }

        public IReadOnlyList<FilteringDescriptor> NewDescriptors { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IFilteringModel : INotifyPropertyChanged
    {
        IReadOnlyList<FilteringDescriptor> Descriptors { get; }

        bool OwnsViewFilter { get; set; }

        event EventHandler<FilteringChangedEventArgs> FilteringChanged;

        event EventHandler<FilteringChangingEventArgs> FilteringChanging;

        void SetOrUpdate(FilteringDescriptor descriptor);

        void Apply(IEnumerable<FilteringDescriptor> descriptors);

        void Clear();

        bool Remove(object columnId);

        bool Move(object columnId, int newIndex);

        void BeginUpdate();

        void EndUpdate();

        IDisposable DeferRefresh();
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class FilteringModelInteractionEventArgs : EventArgs
    {
        public FilteringModelInteractionEventArgs(object columnId)
        {
            ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
        }

        public object ColumnId { get; }
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IFilteringModelInteraction
    {
        event EventHandler<FilteringModelInteractionEventArgs> ShowFilterFlyoutRequested;

        void RequestShowFilterFlyout(object columnId);
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    interface IDataGridFilteringModelFactory
    {
        IFilteringModel Create();
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    sealed class FilteringModel : IFilteringModel, IFilteringModelInteraction
    {
        private readonly List<FilteringDescriptor> _descriptors = new();
        private readonly IReadOnlyList<FilteringDescriptor> _readOnlyView;
        private int _updateNesting;
        private bool _hasPendingChange;
        private List<FilteringDescriptor> _pendingOldDescriptors;

        public FilteringModel()
        {
            OwnsViewFilter = true;
            _readOnlyView = _descriptors.AsReadOnly();
        }

        public IReadOnlyList<FilteringDescriptor> Descriptors => _readOnlyView;

        private bool _ownsViewFilter;

        public bool OwnsViewFilter
        {
            get => _ownsViewFilter;
            set
            {
                if (_ownsViewFilter != value)
                {
                    _ownsViewFilter = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OwnsViewFilter)));
                }
            }
        }

        public event EventHandler<FilteringChangedEventArgs> FilteringChanged;

        public event EventHandler<FilteringChangingEventArgs> FilteringChanging;

        public event EventHandler<FilteringModelInteractionEventArgs> ShowFilterFlyoutRequested;

        public event PropertyChangedEventHandler PropertyChanged;

        public void RequestShowFilterFlyout(object columnId)
        {
            if (columnId == null)
            {
                throw new ArgumentNullException(nameof(columnId));
            }

            ShowFilterFlyoutRequested?.Invoke(this, new FilteringModelInteractionEventArgs(columnId));
        }

        public void SetOrUpdate(FilteringDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var next = new List<FilteringDescriptor>(_descriptors);
            int index = IndexOf(descriptor.ColumnId);
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

        public void Apply(IEnumerable<FilteringDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            ApplyState(new List<FilteringDescriptor>(descriptors));
        }

        public void Clear()
        {
            if (_descriptors.Count == 0)
            {
                return;
            }

            ApplyState(new List<FilteringDescriptor>());
        }

        public bool Remove(object columnId)
        {
            if (columnId == null)
            {
                throw new ArgumentNullException(nameof(columnId));
            }

            int index = IndexOf(columnId);
            if (index < 0)
            {
                return false;
            }

            var next = new List<FilteringDescriptor>(_descriptors);
            next.RemoveAt(index);
            ApplyState(next);
            return true;
        }

        public bool Move(object columnId, int newIndex)
        {
            if (columnId == null)
            {
                throw new ArgumentNullException(nameof(columnId));
            }

            if (newIndex < 0 || newIndex >= _descriptors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(newIndex));
            }

            int oldIndex = IndexOf(columnId);
            if (oldIndex < 0 || oldIndex == newIndex)
            {
                return false;
            }

            var next = new List<FilteringDescriptor>(_descriptors);
            var descriptor = next[oldIndex];
            next.RemoveAt(oldIndex);
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
                throw new InvalidOperationException("EndUpdate called without a matching BeginUpdate.");
            }

            _updateNesting--;

            if (_updateNesting == 0 && _hasPendingChange)
            {
                var oldDescriptors = _pendingOldDescriptors ?? new List<FilteringDescriptor>();
                _pendingOldDescriptors = null;
                _hasPendingChange = false;
                RaiseFilteringChanged(oldDescriptors, new List<FilteringDescriptor>(_descriptors));
            }
        }

        public IDisposable DeferRefresh()
        {
            BeginUpdate();
            return new UpdateScope(this);
        }

        private void ApplyState(List<FilteringDescriptor> next)
        {
            for (int i = 0; i < next.Count; i++)
            {
                if (next[i] == null)
                {
                    throw new ArgumentException("Filtering descriptors cannot contain null entries.", nameof(next));
                }
            }

            EnsureUniqueColumns(next);

            if (SequenceEqual(_descriptors, next))
            {
                return;
            }

            var oldDescriptors = new List<FilteringDescriptor>(_descriptors);

            if (!RaiseFilteringChanging(oldDescriptors, new List<FilteringDescriptor>(next)))
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

            RaiseFilteringChanged(oldDescriptors, new List<FilteringDescriptor>(_descriptors));
        }

        private bool RaiseFilteringChanging(IReadOnlyList<FilteringDescriptor> oldDescriptors, IReadOnlyList<FilteringDescriptor> newDescriptors)
        {
            var handler = FilteringChanging;
            if (handler == null)
            {
                return true;
            }

            var args = new FilteringChangingEventArgs(
                new List<FilteringDescriptor>(oldDescriptors),
                new List<FilteringDescriptor>(newDescriptors));
            handler(this, args);
            return !args.Cancel;
        }

        private void RaiseFilteringChanged(IReadOnlyList<FilteringDescriptor> oldDescriptors, IReadOnlyList<FilteringDescriptor> newDescriptors)
        {
            FilteringChanged?.Invoke(this, new FilteringChangedEventArgs(oldDescriptors, newDescriptors));
        }

        private int IndexOf(object columnId)
        {
            for (int i = 0; i < _descriptors.Count; i++)
            {
                if (IsSameColumnId(_descriptors[i], columnId))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSameColumnId(FilteringDescriptor descriptor, object columnId)
        {
            if (Equals(descriptor.ColumnId, columnId))
            {
                return true;
            }

            if (string.IsNullOrEmpty(descriptor.PropertyPath))
            {
                return false;
            }

            if (columnId is string path &&
                string.Equals(descriptor.PropertyPath, path, StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static void EnsureUniqueColumns(List<FilteringDescriptor> descriptors)
        {
            var set = new HashSet<object>();
            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    continue;
                }

                var key = descriptor.PropertyPath ?? descriptor.ColumnId;
                if (!set.Add(key))
                {
                    throw new ArgumentException("Filtering descriptors must have unique column identifiers.", nameof(descriptors));
                }
            }
        }

        private static bool SequenceEqual(List<FilteringDescriptor> left, List<FilteringDescriptor> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
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
            private readonly FilteringModel _owner;
            private bool _disposed;

            public UpdateScope(FilteringModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner.EndUpdate();
                _disposed = true;
            }
        }
    }
}
