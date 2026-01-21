// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    partial class DataGrid
    {
        private void SetColumnDefinitionsSource(IList<DataGridColumnDefinition> value)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            if (ReferenceEquals(value, _columnDefinitionsSource))
            {
                return;
            }

            SetAndRaise(ColumnDefinitionsSourceProperty, ref _columnDefinitionsSource, value);
        }

        private void OnColumnDefinitionsSourceChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_areHandlersSuspended)
            {
                return;
            }

            var oldValue = (IList<DataGridColumnDefinition>)e.OldValue;
            var newValue = (IList<DataGridColumnDefinition>)e.NewValue;

            if (ReferenceEquals(oldValue, newValue))
            {
                return;
            }

            DetachColumnDefinitions(oldValue);

            if (newValue == null)
            {
                _pendingColumnDefinitionsApply = false;
                ClearColumnsForBinding();
                return;
            }

            try
            {
                if (_boundColumns != null)
                {
                    throw new InvalidOperationException("Cannot set ColumnDefinitionsSource when Columns are bound. Clear Columns before binding definitions.");
                }

                if (HasInlineColumnsDefinedExcludingDefinitions())
                {
                    throw new InvalidOperationException("Cannot bind ColumnDefinitionsSource when inline columns are already defined. Clear existing columns before binding.");
                }

                AttachColumnDefinitions(newValue);

                if (_autoGeneratingColumnOperationCount > 0)
                {
                    _pendingColumnDefinitionsApply = true;
                    return;
                }

                ApplyColumnDefinitionsSnapshot();
            }
            catch
            {
                _columnDefinitionsSource = oldValue;
                AttachColumnDefinitions(oldValue);
                throw;
            }
        }

        private void AttachColumnDefinitions(IList<DataGridColumnDefinition> definitions)
        {
            _columnDefinitionsNotifications = definitions as INotifyCollectionChanged;
            if (_columnDefinitionsNotifications != null)
            {
                _columnDefinitionsNotifications.CollectionChanged += ColumnDefinitions_CollectionChanged;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ColumnDefinitionsSource does not implement INotifyCollectionChanged; applying snapshot without live updates.");
            }

            _columnDefinitionsThreadId = Environment.CurrentManagedThreadId;
        }

        private void DetachColumnDefinitions(IList<DataGridColumnDefinition> definitions)
        {
            DetachColumnDefinitionsNotifications();

            RemoveDefinitionColumns();

            foreach (var column in _columnDefinitionMap.Values)
            {
                DataGridColumnMetadata.ClearDefinition(column);
            }

            _columnDefinitionMap.Clear();
            _definitionColumns.Clear();
        }

        private void AttachColumnDefinitionsNotifications()
        {
            if (_columnDefinitionsSource == null)
            {
                return;
            }

            _columnDefinitionsNotifications = _columnDefinitionsSource as INotifyCollectionChanged;
            if (_columnDefinitionsNotifications != null)
            {
                _columnDefinitionsNotifications.CollectionChanged -= ColumnDefinitions_CollectionChanged;
                _columnDefinitionsNotifications.CollectionChanged += ColumnDefinitions_CollectionChanged;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ColumnDefinitionsSource does not implement INotifyCollectionChanged; applying snapshot without live updates.");
            }

            _columnDefinitionsThreadId = Environment.CurrentManagedThreadId;

            foreach (var definition in _columnDefinitionMap.Keys.ToList())
            {
                SubscribeDefinition(definition);
            }
        }

        private void DetachColumnDefinitionsNotifications()
        {
            if (_columnDefinitionsNotifications != null)
            {
                _columnDefinitionsNotifications.CollectionChanged -= ColumnDefinitions_CollectionChanged;
                _columnDefinitionsNotifications = null;
            }

            foreach (var definition in _columnDefinitionMap.Keys.ToList())
            {
                UnsubscribeDefinition(definition);
            }

            _columnDefinitionsThreadId = null;
        }

        private void ColumnDefinitions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                throw new InvalidOperationException("ColumnDefinitionsSource changes must occur on the UI thread.");
            }

            if (_columnDefinitionsThreadId.HasValue && _columnDefinitionsThreadId.Value != Environment.CurrentManagedThreadId)
            {
                throw new InvalidOperationException("ColumnDefinitionsSource changes must occur on the same thread that created the binding.");
            }

            if (_syncingColumnDefinitions)
            {
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset && ColumnsSourceResetBehavior == ColumnsSourceResetBehavior.Ignore)
            {
                return;
            }

            if (_autoGeneratingColumnOperationCount > 0)
            {
                _pendingColumnDefinitionsApply = true;
                return;
            }

            if (!TryApplyColumnDefinitionsChange(e))
            {
                ApplyColumnDefinitionsSnapshot();
            }
        }

        private void ColumnDefinition_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_syncingColumnDefinitions)
            {
                return;
            }

            if (sender is DataGridColumnDefinition definition && _columnDefinitionMap.TryGetValue(definition, out var column))
            {
                definition.ApplyPropertyChange(column, new DataGridColumnDefinitionContext(this), e.PropertyName);
            }
        }

        private void SubscribeDefinition(DataGridColumnDefinition definition)
        {
            if (definition is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged += ColumnDefinition_PropertyChanged;
            }
        }

        private void UnsubscribeDefinition(DataGridColumnDefinition definition)
        {
            if (definition is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged -= ColumnDefinition_PropertyChanged;
            }
        }

        private void ApplyColumnDefinitionsSnapshot()
        {
            if (_columnDefinitionsSource == null)
            {
                return;
            }

            List<DataGridColumnDefinition> snapshot;
            try
            {
                snapshot = _columnDefinitionsSource.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enumerate ColumnDefinitionsSource: {ex}");
                throw new InvalidOperationException("Failed to enumerate ColumnDefinitionsSource.", ex);
            }

            _areHandlersSuspended = true;
            _syncingColumnDefinitions = true;
            try
            {
                var context = new DataGridColumnDefinitionContext(this);
                var autoColumns = ColumnsInternal.Where(c => c.IsAutoGenerated).ToList();
                var newColumns = new List<DataGridColumn>(snapshot.Count);
                var newMap = new Dictionary<DataGridColumnDefinition, DataGridColumn>();
                var newColumnSet = new HashSet<DataGridColumn>();

                foreach (var definition in snapshot)
                {
                    if (definition == null)
                    {
                        throw new ArgumentNullException(nameof(definition));
                    }

                    if (!_columnDefinitionMap.TryGetValue(definition, out var column))
                    {
                        column = definition.CreateColumn(context);
                        SubscribeDefinition(definition);
                    }
                    else
                    {
                        definition.ApplyToColumn(column, context);
                    }

                    DataGridColumnMetadata.SetDefinition(column, definition);

                    newColumns.Add(column);
                    newMap[definition] = column;
                    newColumnSet.Add(column);
                }

                foreach (var pair in _columnDefinitionMap)
                {
                    if (!newMap.ContainsKey(pair.Key))
                    {
                        UnsubscribeDefinition(pair.Key);
                        DataGridColumnMetadata.ClearDefinition(pair.Value);
                    }
                }

                _columnDefinitionMap.Clear();
                foreach (var pair in newMap)
                {
                    _columnDefinitionMap.Add(pair.Key, pair.Value);
                }

                _definitionColumns.Clear();
                foreach (var column in newColumnSet)
                {
                    _definitionColumns.Add(column);
                }

                ClearColumnsForBinding();

                if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.BeforeSource)
                {
                    foreach (var auto in autoColumns)
                    {
                        ColumnsInternal.Add(auto);
                    }
                }

                foreach (var column in newColumns)
                {
                    ValidateBoundColumn(column);
                    ColumnsInternal.Add(column);
                }

                if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.AfterSource)
                {
                    foreach (var auto in autoColumns)
                    {
                        ColumnsInternal.Add(auto);
                    }
                }

                ApplyDefinitionDisplayIndexes(autoColumns, newColumns);
            }
            finally
            {
                _syncingColumnDefinitions = false;
                _areHandlersSuspended = false;
            }
        }

        private void ApplyDefinitionDisplayIndexes(IReadOnlyList<DataGridColumn> autoColumns, IReadOnlyList<DataGridColumn> definitionColumns)
        {
            _areHandlersSuspended = true;
            try
            {
                var displayIndex = 0;
                var orderedDefinitions = OrderDefinitionColumns(definitionColumns);

                if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.BeforeSource)
                {
                    foreach (var auto in autoColumns)
                    {
                        auto.DisplayIndex = displayIndex++;
                    }
                }

                foreach (var column in orderedDefinitions)
                {
                    if (column != null && ColumnsInternal.Contains(column))
                    {
                        column.DisplayIndex = displayIndex++;
                    }
                }

                if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.AfterSource)
                {
                    foreach (var auto in autoColumns)
                    {
                        auto.DisplayIndex = displayIndex++;
                    }
                }
            }
            finally
            {
                _areHandlersSuspended = false;
            }
        }

        private List<DataGridColumn> OrderDefinitionColumns(IReadOnlyList<DataGridColumn> definitionColumns)
        {
            if (definitionColumns == null || definitionColumns.Count == 0)
            {
                return new List<DataGridColumn>();
            }

            var ordered = new DataGridColumn[definitionColumns.Count];
            var remaining = new List<DataGridColumn>();
            var indexed = new List<(DataGridColumn Column, int DisplayIndex, int Order)>();

            for (int i = 0; i < definitionColumns.Count; i++)
            {
                var column = definitionColumns[i];
                if (column == null)
                {
                    continue;
                }

                var definition = DataGridColumnMetadata.GetDefinition(column);
                var displayIndex = definition?.DisplayIndex;
                if (displayIndex.HasValue && displayIndex.Value >= 0)
                {
                    indexed.Add((column, displayIndex.Value, i));
                }
                else
                {
                    remaining.Add(column);
                }
            }

            indexed.Sort((left, right) =>
            {
                var compare = left.DisplayIndex.CompareTo(right.DisplayIndex);
                return compare != 0 ? compare : left.Order.CompareTo(right.Order);
            });

            foreach (var entry in indexed)
            {
                var target = entry.DisplayIndex;
                if (target < 0)
                {
                    target = 0;
                }
                else if (target >= ordered.Length)
                {
                    target = ordered.Length - 1;
                }

                while (target < ordered.Length && ordered[target] != null)
                {
                    target++;
                }

                if (target >= ordered.Length)
                {
                    for (target = 0; target < ordered.Length; target++)
                    {
                        if (ordered[target] == null)
                        {
                            break;
                        }
                    }

                    if (target >= ordered.Length)
                    {
                        break;
                    }
                }

                ordered[target] = entry.Column;
            }

            var remainingIndex = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i] == null && remainingIndex < remaining.Count)
                {
                    ordered[i] = remaining[remainingIndex++];
                }
            }

            var result = new List<DataGridColumn>(ordered.Length);
            foreach (var column in ordered)
            {
                if (column != null)
                {
                    result.Add(column);
                }
            }

            for (; remainingIndex < remaining.Count; remainingIndex++)
            {
                result.Add(remaining[remainingIndex]);
            }

            return result;
        }

        private void ApplyPendingColumnDefinitions()
        {
            if (!_pendingColumnDefinitionsApply)
            {
                return;
            }

            _pendingColumnDefinitionsApply = false;
            ApplyColumnDefinitionsSnapshot();
        }

        private bool TryApplyColumnDefinitionsChange(NotifyCollectionChangedEventArgs e)
        {
            if (_columnDefinitionsSource == null)
            {
                return true;
            }

            if (!CanApplyColumnDefinitionsChange(e))
            {
                return false;
            }

            _areHandlersSuspended = true;
            _syncingColumnDefinitions = true;
            try
            {
                var context = new DataGridColumnDefinitionContext(this);

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        AddColumnDefinitions(e.NewItems, e.NewStartingIndex, context);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveColumnDefinitions(e.OldItems);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        RemoveColumnDefinitions(e.OldItems);
                        AddColumnDefinitions(e.NewItems, e.NewStartingIndex, context);
                        break;
                    case NotifyCollectionChangedAction.Move:
                        MoveColumnDefinitions(e.OldItems, e.NewStartingIndex);
                        break;
                }

                ApplyDefinitionDisplayIndexes(GetAutoGeneratedColumns(), GetDefinitionColumnsInOrder());
                return true;
            }
            finally
            {
                _syncingColumnDefinitions = false;
                _areHandlersSuspended = false;
            }
        }

        private bool CanApplyColumnDefinitionsChange(NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null || e.NewStartingIndex < 0)
                    {
                        return false;
                    }

                    foreach (var item in e.NewItems)
                    {
                        if (item is not DataGridColumnDefinition definition)
                        {
                            return false;
                        }

                        if (_columnDefinitionMap.ContainsKey(definition))
                        {
                            return false;
                        }
                    }
                    return true;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    foreach (var item in e.OldItems)
                    {
                        if (item is not DataGridColumnDefinition definition)
                        {
                            return false;
                        }

                        if (!_columnDefinitionMap.ContainsKey(definition))
                        {
                            return false;
                        }
                    }
                    return true;
                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems == null || e.OldItems == null || e.NewStartingIndex < 0)
                    {
                        return false;
                    }

                    foreach (var item in e.OldItems)
                    {
                        if (item is not DataGridColumnDefinition definition)
                        {
                            return false;
                        }

                        if (!_columnDefinitionMap.ContainsKey(definition))
                        {
                            return false;
                        }
                    }

                    foreach (var item in e.NewItems)
                    {
                        if (item is not DataGridColumnDefinition definition)
                        {
                            return false;
                        }

                        if (_columnDefinitionMap.ContainsKey(definition))
                        {
                            return false;
                        }
                    }
                    return true;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems == null || e.OldItems.Count != 1 || e.NewStartingIndex < 0)
                    {
                        return false;
                    }

                    if (e.OldItems[0] is not DataGridColumnDefinition movedDefinition)
                    {
                        return false;
                    }

                    if (!_columnDefinitionMap.TryGetValue(movedDefinition, out var column) || column == null)
                    {
                        return false;
                    }

                    return ColumnsInternal.Contains(column);
                case NotifyCollectionChangedAction.Reset:
                    return false;
                default:
                    return false;
            }
        }

        private void AddColumnDefinitions(IList newItems, int startingIndex, DataGridColumnDefinitionContext context)
        {
            var insertIndex = GetDefinitionInsertIndex(startingIndex);
            if (insertIndex < 0)
            {
                throw new InvalidOperationException("Cannot insert column definition at the specified index.");
            }

            foreach (var item in newItems)
            {
                if (item is not DataGridColumnDefinition definition)
                {
                    throw new InvalidOperationException("ColumnDefinitionsSource must contain DataGridColumnDefinition instances.");
                }

                if (_columnDefinitionMap.ContainsKey(definition))
                {
                    throw new InvalidOperationException("ColumnDefinitionsSource contains duplicate definitions.");
                }

                var column = definition.CreateColumn(context);
                ValidateBoundColumn(column);

                DataGridColumnMetadata.SetDefinition(column, definition);
                SubscribeDefinition(definition);

                _columnDefinitionMap.Add(definition, column);
                _definitionColumns.Add(column);

                ColumnsInternal.Insert(Math.Min(insertIndex, ColumnsInternal.Count), column);
                insertIndex++;
            }
        }

        private void RemoveColumnDefinitions(IList oldItems)
        {
            foreach (var item in oldItems)
            {
                if (item is not DataGridColumnDefinition definition)
                {
                    throw new InvalidOperationException("ColumnDefinitionsSource must contain DataGridColumnDefinition instances.");
                }

                if (!_columnDefinitionMap.TryGetValue(definition, out var column))
                {
                    throw new InvalidOperationException("Column definition was not found in the current grid.");
                }

                UnsubscribeDefinition(definition);
                _columnDefinitionMap.Remove(definition);

                if (column != null)
                {
                    _definitionColumns.Remove(column);
                    DataGridColumnMetadata.ClearDefinition(column);

                    if (ColumnsInternal.Contains(column))
                    {
                        ColumnsInternal.Remove(column);
                    }
                }
            }
        }

        private void MoveColumnDefinitions(IList movedItems, int newIndex)
        {
            if (movedItems.Count != 1)
            {
                return;
            }

            if (movedItems[0] is not DataGridColumnDefinition definition)
            {
                return;
            }

            if (!_columnDefinitionMap.TryGetValue(definition, out var column) || column == null)
            {
                return;
            }

            var oldIndex = ColumnsInternal.IndexOf(column);
            if (oldIndex < 0)
            {
                return;
            }

            var targetIndex = GetDefinitionInsertIndex(newIndex);
            if (targetIndex < 0)
            {
                return;
            }

            if (oldIndex == targetIndex)
            {
                return;
            }

            ColumnsInternal.RemoveAt(oldIndex);
            ColumnsInternal.Insert(Math.Min(targetIndex, ColumnsInternal.Count), column);
        }

        private int GetDefinitionInsertIndex(int definitionIndex)
        {
            if (definitionIndex < 0)
            {
                return -1;
            }

            var offset = ColumnsInternal.Contains(ColumnsInternal.RowGroupSpacerColumn) ? 1 : 0;

            if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.BeforeSource)
            {
                offset += GetAutoGeneratedColumnCount();
            }

            var maxIndex = ColumnsInternal.Count;
            if (AutoGeneratedColumnsPlacement == AutoGeneratedColumnsPlacement.AfterSource)
            {
                var autoStart = GetFirstAutoGeneratedColumnIndex();
                if (autoStart >= 0)
                {
                    maxIndex = autoStart;
                }
            }

            var insertIndex = offset + definitionIndex;
            if (insertIndex > maxIndex)
            {
                insertIndex = maxIndex;
            }

            return Math.Min(insertIndex, ColumnsInternal.Count);
        }

        private int GetAutoGeneratedColumnCount()
        {
            return ColumnsInternal.Count(c => c.IsAutoGenerated);
        }

        private int GetFirstAutoGeneratedColumnIndex()
        {
            for (int i = 0; i < ColumnsInternal.Count; i++)
            {
                if (ColumnsInternal[i].IsAutoGenerated)
                {
                    return i;
                }
            }

            return -1;
        }

        private List<DataGridColumn> GetAutoGeneratedColumns()
        {
            return ColumnsInternal.Where(c => c.IsAutoGenerated).ToList();
        }

        private List<DataGridColumn> GetDefinitionColumnsInOrder()
        {
            var list = new List<DataGridColumn>();
            foreach (var definition in _columnDefinitionsSource)
            {
                if (definition == null)
                {
                    continue;
                }

                if (_columnDefinitionMap.TryGetValue(definition, out var column))
                {
                    list.Add(column);
                }
            }

            return list;
        }

        private bool HasInlineColumnsDefinedExcludingDefinitions()
        {
            return ColumnsInternal.Any(c =>
                !c.IsAutoGenerated &&
                c is not DataGridFillerColumn &&
                !_definitionColumns.Contains(c) &&
                (_boundColumns == null || !_boundColumns.Contains(c)));
        }

        private bool HasColumnsSource()
        {
            return _boundColumns != null || _columnDefinitionsSource != null;
        }

        private void RemoveDefinitionColumns()
        {
            if (_definitionColumns.Count == 0)
            {
                return;
            }

            _syncingInternalColumns = true;
            _areHandlersSuspended = true;
            try
            {
                foreach (var column in _definitionColumns)
                {
                    if (column != null && ColumnsInternal.Contains(column))
                    {
                        ColumnsInternal.Remove(column);
                    }
                }
            }
            finally
            {
                _areHandlersSuspended = false;
                _syncingInternalColumns = false;
            }
        }
    }
}
