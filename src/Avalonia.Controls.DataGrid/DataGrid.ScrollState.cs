// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.Utilities;

namespace Avalonia.Controls
{
    internal interface IDataGridScrollStateManager
    {
        bool PendingRestore { get; }
        bool PreserveOnAttach { get; }

        void Capture(bool preserveOnAttach);
        bool ShouldPreserveScrollState();
        void Clear();
        void ClearPreserveOnAttachFlag();
        bool TryRestore();
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    partial class DataGrid
    {
        private sealed class ScrollStateManager : IDataGridScrollStateManager
        {
            private readonly DataGrid _owner;
            private bool _preserveOnAttach;
            private bool _pendingRestore;
            private IEnumerable _dataSource;
            private int? _dataSourceCount;
            private List<PreservedScrollStateSample> _samples;
            private int _firstScrollingSlot;
            private double _negVerticalOffset;
            private double _verticalOffset;
            private double _horizontalOffset;
            private RowHeightEstimatorState _rowHeightEstimatorState;
            private Func<object, object> _itemKeySelector;

            public ScrollStateManager(DataGrid owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public bool PendingRestore => _pendingRestore;

            public bool PreserveOnAttach => _preserveOnAttach;

            public void Capture(bool preserveOnAttach)
            {
                if (_owner.DisplayData.FirstScrollingSlot < 0 || _owner.DataConnection?.DataSource == null)
                {
                    if (_pendingRestore)
                    {
                        _preserveOnAttach = preserveOnAttach;
                        return;
                    }

                    Clear();
                    return;
                }

                _itemKeySelector = null;
                _preserveOnAttach = preserveOnAttach;
                _dataSource = _owner.DataConnection.DataSource;
                _dataSourceCount = TryGetDataSourceCount(_owner.DataConnection.DataSource);
                _samples = BuildSamples(_dataSourceCount);
                _firstScrollingSlot = _owner.DisplayData.FirstScrollingSlot;
                _negVerticalOffset = _owner.NegVerticalOffset;
                _verticalOffset = _owner._verticalOffset;
                _horizontalOffset = _owner.HorizontalOffset;
                _rowHeightEstimatorState = CaptureEstimatorState();
                _pendingRestore = true;
            }

            public bool ShouldPreserveScrollState()
            {
                return _preserveOnAttach && IsValid();
            }

            public void Clear()
            {
                _preserveOnAttach = false;
                _pendingRestore = false;
                _dataSource = null;
                _dataSourceCount = null;
                _samples = null;
                _firstScrollingSlot = -1;
                _negVerticalOffset = 0;
                _verticalOffset = 0;
                _horizontalOffset = 0;
                _rowHeightEstimatorState = null;
                _itemKeySelector = null;
            }

            public void ClearPreserveOnAttachFlag()
            {
                _preserveOnAttach = false;
            }

            public bool TryRestore()
            {
                if (!_pendingRestore)
                {
                    return false;
                }

                if (!_owner.IsAttachedToVisualTree || !_owner.IsVisible)
                {
                    return false;
                }

                if (!IsValid())
                {
                    if (!_owner._forceScrollStateRestore)
                    {
                        Clear();
                        return false;
                    }
                }

                if (!TryRestoreEstimatorState())
                {
                    Clear();
                    return false;
                }

                if (_owner.SlotCount == 0 || _owner.ColumnsItemsInternal.Count == 0 || MathUtilities.LessThanOrClose(_owner.CellsEstimatedHeight, 0))
                {
                    return false;
                }

                int targetSlot = Math.Min(_firstScrollingSlot, _owner.SlotCount - 1);
                if (targetSlot < 0)
                {
                    Clear();
                    return false;
                }

                if (_owner._collapsedSlotsTable.Contains(targetSlot))
                {
                    targetSlot = _owner.GetNextVisibleSlot(targetSlot);
                    if (targetSlot == -1)
                    {
                        Clear();
                        return false;
                    }
                }

                if (_owner.DisplayData.FirstScrollingSlot != -1)
                {
                    if (!_owner.CanRetainDisplayedRowsForScrollTarget(targetSlot))
                    {
                        _owner.ResetDisplayedRows();
                    }
                }

                _owner.NegVerticalOffset = Math.Max(0, _negVerticalOffset);
                _owner.UpdateDisplayedRows(targetSlot, _owner.CellsEstimatedHeight);

                double firstHeight = _owner.GetExactSlotElementHeight(_owner.DisplayData.FirstScrollingSlot);
                if (MathUtilities.GreaterThanOrClose(_owner.NegVerticalOffset, firstHeight))
                {
                    _owner.NegVerticalOffset = Math.Max(0, firstHeight - MathUtilities.DoubleEpsilon);
                }

                _owner._verticalOffset = Math.Max(0, _verticalOffset);
                _owner.SetVerticalOffset(_owner._verticalOffset);
                _owner.UpdateHorizontalOffset(Math.Max(0, _horizontalOffset));

                Clear();
                return true;
            }

            public DataGridScrollState CaptureState(DataGridStateOptions options)
            {
                if (_owner.DisplayData.FirstScrollingSlot < 0 || _owner.DataConnection?.DataSource == null)
                {
                    return null;
                }

                var dataSource = _owner.DataConnection.DataSource;
                var dataSourceCount = TryGetDataSourceCount(dataSource);

                return new DataGridScrollState
                {
                    DataSource = dataSource,
                    DataSourceCount = dataSourceCount,
                    Samples = BuildExternalSamples(dataSourceCount, options),
                    FirstScrollingSlot = _owner.DisplayData.FirstScrollingSlot,
                    NegVerticalOffset = _owner.NegVerticalOffset,
                    VerticalOffset = _owner._verticalOffset,
                    HorizontalOffset = _owner.HorizontalOffset,
                    RowHeightEstimatorState = CaptureEstimatorState()
                };
            }

            public bool TryRestore(DataGridScrollState state, DataGridStateOptions options)
            {
                if (state == null)
                {
                    return false;
                }

                IEnumerable dataSource = state.DataSource as IEnumerable;
                if (dataSource == null)
                {
                    if (options?.ItemKeySelector == null)
                    {
                        return false;
                    }

                    dataSource = _owner.DataConnection?.DataSource;
                    if (dataSource == null)
                    {
                        return false;
                    }
                }

                _preserveOnAttach = false;
                _pendingRestore = true;
                _dataSource = dataSource;
                _dataSourceCount = state.DataSourceCount;
                _samples = ConvertExternalSamples(state.Samples);
                _firstScrollingSlot = state.FirstScrollingSlot;
                _negVerticalOffset = state.NegVerticalOffset;
                _verticalOffset = state.VerticalOffset;
                _horizontalOffset = state.HorizontalOffset;
                _rowHeightEstimatorState = state.RowHeightEstimatorState;
                _itemKeySelector = options?.ItemKeySelector;

                return TryRestore();
            }

            private RowHeightEstimatorState CaptureEstimatorState()
            {
                if (_owner.RowHeightEstimator is IDataGridRowHeightEstimatorStateful stateful)
                {
                    return stateful.CaptureState();
                }

                return null;
            }

            private bool TryRestoreEstimatorState()
            {
                if (_rowHeightEstimatorState == null)
                {
                    return true;
                }

                if (_owner.RowHeightEstimator is not IDataGridRowHeightEstimatorStateful stateful)
                {
                    return false;
                }

                return stateful.TryRestoreState(_rowHeightEstimatorState);
            }

            private bool IsValid()
            {
                if (_owner.DataConnection?.DataSource == null)
                {
                    return false;
                }

                if (_dataSource == null)
                {
                    return false;
                }

                if (_itemKeySelector == null &&
                    !ReferenceEquals(_owner.DataConnection.DataSource, _dataSource))
                {
                    return false;
                }

                if (_rowHeightEstimatorState != null && _owner.RowHeightEstimator is not IDataGridRowHeightEstimatorStateful)
                {
                    return false;
                }

                var currentCount = TryGetDataSourceCount(_owner.DataConnection.DataSource);
                if (_dataSourceCount.HasValue)
                {
                    if (!currentCount.HasValue || currentCount.Value != _dataSourceCount.Value)
                    {
                        return false;
                    }
                }

                if (_samples != null)
                {
                    foreach (var sample in _samples)
                    {
                        if (currentCount.HasValue && sample.Index >= currentCount.Value)
                        {
                            return false;
                        }

                        object currentItem;
                        try
                        {
                            currentItem = _owner.DataConnection.GetDataItem(sample.Index);
                        }
                        catch
                        {
                            return false;
                        }

                        if (!ItemsMatch(sample.Item, currentItem))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private List<PreservedScrollStateSample> BuildSamples(int? count)
            {
                List<PreservedScrollStateSample> samples = null;

                AddSampleFromSlot(ref samples, _owner.DisplayData.FirstScrollingSlot);
                AddSampleFromSlot(ref samples, _owner.DisplayData.LastScrollingSlot);

                if (count.HasValue && count.Value > 0)
                {
                    AddSample(ref samples, 0);

                    if (count.Value > 2)
                    {
                        AddSample(ref samples, count.Value / 2);
                    }

                    if (count.Value > 1)
                    {
                        AddSample(ref samples, count.Value - 1);
                    }
                }

                return samples;
            }

            private void AddSampleFromSlot(ref List<PreservedScrollStateSample> samples, int slot)
            {
                if (slot < 0 || _owner.IsGroupSlot(slot))
                {
                    return;
                }

                AddSample(ref samples, _owner.RowIndexFromSlot(slot));
            }

            private void AddSample(ref List<PreservedScrollStateSample> samples, int index)
            {
                if (index < 0)
                {
                    return;
                }

                if (samples != null)
                {
                    foreach (var sample in samples)
                    {
                        if (sample.Index == index)
                        {
                            return;
                        }
                    }
                }

                var item = _owner.DataConnection.GetDataItem(index);
                samples ??= new List<PreservedScrollStateSample>(4);
                samples.Add(new PreservedScrollStateSample(index, item));
            }

            private static int? TryGetDataSourceCount(IEnumerable dataSource)
            {
                if (dataSource == null)
                {
                    return null;
                }

                if (dataSource is ICollection collection)
                {
                    return collection.Count;
                }

                var interfaces = dataSource.GetType().GetInterfaces();
                foreach (var candidate in interfaces)
                {
                    if (!candidate.IsGenericType)
                    {
                        continue;
                    }

                    var genericDefinition = candidate.GetGenericTypeDefinition();
                    if (genericDefinition != typeof(IReadOnlyCollection<>) &&
                        genericDefinition != typeof(ICollection<>))
                    {
                        continue;
                    }

                    var countProperty = candidate.GetProperty("Count");
                    if (countProperty?.PropertyType == typeof(int))
                    {
                        return (int?)countProperty.GetValue(dataSource);
                    }
                }

                return null;
            }

            private bool ItemsMatch(object left, object right)
            {
                if (_itemKeySelector != null)
                {
                    var rightKey = right != null ? _itemKeySelector(right) : null;
                    return Equals(left, rightKey);
                }

                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                if (left == null || right == null)
                {
                    return false;
                }

                if (left is string || right is string)
                {
                    return string.Equals(left as string, right as string, StringComparison.Ordinal);
                }

                if (left.GetType().IsValueType || right.GetType().IsValueType)
                {
                    return left.Equals(right);
                }

                return false;
            }

            private List<DataGridScrollSample> BuildExternalSamples(int? count, DataGridStateOptions options)
            {
                List<DataGridScrollSample> samples = null;

                AddExternalSampleFromSlot(ref samples, _owner.DisplayData.FirstScrollingSlot, options);
                AddExternalSampleFromSlot(ref samples, _owner.DisplayData.LastScrollingSlot, options);

                if (count.HasValue && count.Value > 0)
                {
                    AddExternalSample(ref samples, 0, options);

                    if (count.Value > 2)
                    {
                        AddExternalSample(ref samples, count.Value / 2, options);
                    }

                    if (count.Value > 1)
                    {
                        AddExternalSample(ref samples, count.Value - 1, options);
                    }
                }

                return samples;
            }

            private void AddExternalSampleFromSlot(ref List<DataGridScrollSample> samples, int slot, DataGridStateOptions options)
            {
                if (slot < 0 || _owner.IsGroupSlot(slot))
                {
                    return;
                }

                AddExternalSample(ref samples, _owner.RowIndexFromSlot(slot), options);
            }

            private void AddExternalSample(ref List<DataGridScrollSample> samples, int index, DataGridStateOptions options)
            {
                if (index < 0)
                {
                    return;
                }

                if (samples != null)
                {
                    foreach (var sample in samples)
                    {
                        if (sample.Index == index)
                        {
                            return;
                        }
                    }
                }

                var item = _owner.DataConnection.GetDataItem(index);
                if (item == null ||
                    ReferenceEquals(item, global::Avalonia.Collections.DataGridCollectionView.NewItemPlaceholder))
                {
                    return;
                }

                var key = options?.ItemKeySelector != null ? options.ItemKeySelector(item) : item;
                samples ??= new List<DataGridScrollSample>(4);
                samples.Add(new DataGridScrollSample(index, key));
            }

            private static List<PreservedScrollStateSample> ConvertExternalSamples(IReadOnlyList<DataGridScrollSample> samples)
            {
                if (samples == null || samples.Count == 0)
                {
                    return null;
                }

                var list = new List<PreservedScrollStateSample>(samples.Count);
                foreach (var sample in samples)
                {
                    list.Add(new PreservedScrollStateSample(sample.Index, sample.ItemKey));
                }
                return list;
            }

            private sealed class PreservedScrollStateSample
            {
                public PreservedScrollStateSample(int index, object item)
                {
                    Index = index;
                    Item = item;
                }

                public int Index { get; }
                public object Item { get; }
            }
        }
    }
}
