// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.Controls
{
    /// <summary>
    /// Advanced implementation of <see cref="IDataGridRowHeightEstimator"/> that provides
    /// optimized handling for variable row heights. Features include:
    /// <list type="bullet">
    /// <item><description>Height cache for measured rows</description></item>
    /// <item><description>Regional averages for localized estimation</description></item>
    /// <item><description>Smooth scroll correction</description></item>
    /// <item><description>Fenwick tree for efficient cumulative height queries</description></item>
    /// <item><description>Bidirectional estimation updates</description></item>
    /// </list>
    /// </summary>
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class AdvancedRowHeightEstimator : IDataGridRowHeightEstimator, IDataGridRowHeightEstimatorStateful
    {
        private const double DefaultHeight = 22.0;
        private const int MaxRowGroupLevels = 10;
        private const int RegionSize = 100; // Number of rows per region for regional averages
        private const int MaxCacheSize = 50000; // Maximum cached heights
        private const double SmoothCorrectionFactor = 0.1; // Apply 10% of correction per update

        private double _defaultRowHeight = DefaultHeight;
        private double _globalRowHeightEstimate = DefaultHeight;
        private double _rowDetailsHeightEstimate;
        private double[] _rowGroupHeaderHeightsByLevel;
        private int _totalItemCount;

        // Height cache for measured rows
        private readonly Dictionary<int, double> _measuredHeights = new();
        private readonly Dictionary<int, double> _detailsHeights = new();

        // Regional statistics for localized height averages
        private readonly Dictionary<int, RegionStatistics> _regionStats = new();

        // Fenwick tree for O(log n) prefix sum queries
        private double[] _fenwickSumTree = Array.Empty<double>();
        private int[] _fenwickCountTree = Array.Empty<int>();
        private int _fenwickSize;

        // Smooth scroll correction state
        private double _pendingCorrection;
        private double _accumulatedError;

        // Bidirectional measurement range tracking
        private int _minMeasuredSlot = int.MaxValue;
        private int _maxMeasuredSlot = -1;

        // Statistics
        private double _sumMeasuredHeights;
        private int _measuredCount;
        private double _minMeasuredHeight = double.MaxValue;
        private double _maxMeasuredHeight = double.MinValue;

        // Collapsed slot tracking from DataGrid
        private int _lastCollapsedSlotCount;
        private int _lastDetailsCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdvancedRowHeightEstimator"/> class.
        /// </summary>
        public AdvancedRowHeightEstimator()
        {
            _rowGroupHeaderHeightsByLevel = new double[MaxRowGroupLevels];
            for (int i = 0; i < MaxRowGroupLevels; i++)
            {
                _rowGroupHeaderHeightsByLevel[i] = DefaultHeight;
            }
            
            InitializeFenwickTree(1000); // Start with capacity for 1000 rows
        }

        #region IDataGridRowHeightEstimator Properties

        /// <inheritdoc/>
        public double DefaultRowHeight
        {
            get => _defaultRowHeight;
            set
            {
                _defaultRowHeight = value;
                if (_measuredCount == 0)
                {
                    _globalRowHeightEstimate = value;
                }
            }
        }

        /// <inheritdoc/>
        public double RowHeightEstimate => _globalRowHeightEstimate;

        /// <inheritdoc/>
        public double RowDetailsHeightEstimate => _rowDetailsHeightEstimate;

        #endregion

        #region IDataGridRowHeightEstimator Methods

        /// <inheritdoc/>
        public double GetRowGroupHeaderHeightEstimate(int level)
        {
            if (level < 0 || level >= MaxRowGroupLevels)
                return DefaultHeight;
            return _rowGroupHeaderHeightsByLevel[level];
        }

        /// <inheritdoc/>
        public void RecordMeasuredHeight(int slot, double measuredHeight, bool hasDetails = false, double detailsHeight = 0)
        {
            if (slot < 0)
                return;

            // Manage cache size with smart eviction
            if (_measuredHeights.Count >= MaxCacheSize && !_measuredHeights.ContainsKey(slot))
            {
                EvictCacheEntries();
            }

            bool isNew = !_measuredHeights.ContainsKey(slot);
            double oldHeight = isNew ? _globalRowHeightEstimate : _measuredHeights[slot];

            // Store the measured height
            _measuredHeights[slot] = measuredHeight;

            // Update statistics
            if (isNew)
            {
                _sumMeasuredHeights += measuredHeight;
                _measuredCount++;
            }
            else
            {
                _sumMeasuredHeights = _sumMeasuredHeights - oldHeight + measuredHeight;
            }

            _minMeasuredHeight = Math.Min(_minMeasuredHeight, measuredHeight);
            _maxMeasuredHeight = Math.Max(_maxMeasuredHeight, measuredHeight);

            // Track measurement range
            _minMeasuredSlot = Math.Min(_minMeasuredSlot, slot);
            _maxMeasuredSlot = Math.Max(_maxMeasuredSlot, slot);

            // Update global running estimate
            if (_measuredCount > 0)
            {
                _globalRowHeightEstimate = _sumMeasuredHeights / _measuredCount;
            }

            UpdateRegionStatistics(slot, measuredHeight, oldHeight, isNew);
            UpdateFenwickTree(slot, measuredHeight, oldHeight, isNew);

            // Track error for smooth correction
            if (!isNew)
            {
                double error = measuredHeight - oldHeight;
                _accumulatedError += error;
            }

            // Handle details
            if (hasDetails && detailsHeight > 0)
            {
                _detailsHeights[slot] = detailsHeight;
                _rowDetailsHeightEstimate = detailsHeight;
            }
            else if (_detailsHeights.ContainsKey(slot))
            {
                _detailsHeights.Remove(slot);
            }
        }

        /// <inheritdoc/>
        public void RecordRowGroupHeaderHeight(int slot, int level, double measuredHeight)
        {
            if (level >= 0 && level < MaxRowGroupLevels)
            {
                _rowGroupHeaderHeightsByLevel[level] = measuredHeight;
            }
        }

        /// <inheritdoc/>
        public double GetEstimatedHeight(int slot, bool isRowGroupHeader = false, int rowGroupLevel = 0, bool hasDetails = false)
        {
            if (isRowGroupHeader)
            {
                return GetRowGroupHeaderHeightEstimate(rowGroupLevel);
            }

            double height;

            // Priority 1: Return cached height if available
            if (_measuredHeights.TryGetValue(slot, out double cachedHeight))
            {
                height = cachedHeight;
            }
            // Priority 2: Use regional estimate if available
            else
            {
                int region = slot / RegionSize;
                if (_regionStats.TryGetValue(region, out var stats) && stats.Count > 0)
                {
                    height = stats.Average;
                }
                // Priority 3: Try nearby regions
                else
                {
                    height = GetNearestRegionEstimate(region);
                }
            }

            // Add details height
            if (hasDetails)
            {
                if (_detailsHeights.TryGetValue(slot, out double detailsHeight))
                {
                    height += detailsHeight;
                }
                else
                {
                    height += _rowDetailsHeightEstimate;
                }
            }

            return height;
        }

        /// <inheritdoc/>
        public double CalculateTotalHeight(int totalSlotCount, int collapsedSlotCount, int[] rowGroupHeaderCounts, int detailsVisibleCount)
        {
            if (totalSlotCount <= 0)
                return 0;

            int visibleSlotCount = totalSlotCount - collapsedSlotCount;

            // Calculate row group header heights
            double headerHeight = 0;
            int totalHeaderCount = 0;
            if (rowGroupHeaderCounts != null)
            {
                for (int i = 0; i < rowGroupHeaderCounts.Length && i < MaxRowGroupLevels; i++)
                {
                    headerHeight += rowGroupHeaderCounts[i] * _rowGroupHeaderHeightsByLevel[i];
                    totalHeaderCount += rowGroupHeaderCounts[i];
                }
            }

            // Calculate regular row heights using regional estimates
            int regularRowCount = visibleSlotCount - totalHeaderCount;
            double rowHeight = CalculateRowHeightsWithRegions(regularRowCount, totalSlotCount);

            // Add details heights
            double detailsHeight = CalculateDetailsHeight(detailsVisibleCount);

            double correction = ApplySmoothCorrection();

            return headerHeight + rowHeight + detailsHeight + correction;
        }

        /// <inheritdoc/>
        public int EstimateSlotAtOffset(double verticalOffset, int totalSlotCount)
        {
            if (verticalOffset <= 0 || totalSlotCount <= 0)
                return 0;

            // Use Fenwick tree if we have enough measured data
            if (_fenwickSize > 0 && _measuredCount > 0)
            {
                // We have enough measured data to use the Fenwick tree
                return FindSlotByOffsetFenwick(verticalOffset, totalSlotCount);
            }

            // Fallback: Use regional estimates for more accurate estimation
            return FindSlotByOffsetRegional(verticalOffset, totalSlotCount);
        }

        /// <inheritdoc/>
        public double EstimateOffsetToSlot(int slot)
        {
            if (slot <= 0)
                return 0;

            // Use Fenwick tree for efficient prefix sum query
            if (_fenwickSize >= slot && _measuredCount > 0)
            {
                var (measuredSum, measuredCount) = QueryFenwickPrefix(slot);
                int estimatedUnmeasured = Math.Max(0, slot - measuredCount);
                return measuredSum + estimatedUnmeasured * _globalRowHeightEstimate;
            }

            // Fallback: Use regional estimates
            return EstimateOffsetWithRegions(slot);
        }

        /// <inheritdoc/>
        public void UpdateFromDisplayedRows(int firstDisplayedSlot, int lastDisplayedSlot, double[] displayedHeights, double verticalOffset, double negVerticalOffset, int collapsedSlotCount, int detailsCount)
        {
            if (displayedHeights == null || displayedHeights.Length == 0)
                return;

            // Note: Individual height recording is done before this call via RecordMeasuredHeight
            // for each displayed element. The displayedHeights array here is only used for
            // reference (e.g., scroll correction) and not for re-recording heights,
            // since the slots may not be contiguous (collapsed slots are skipped).

            _lastCollapsedSlotCount = collapsedSlotCount;
            _lastDetailsCount = detailsCount;

            ProcessScrollCorrection(verticalOffset, negVerticalOffset, firstDisplayedSlot);
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _measuredHeights.Clear();
            _detailsHeights.Clear();
            _regionStats.Clear();
            
            _sumMeasuredHeights = 0;
            _measuredCount = 0;
            _minMeasuredHeight = double.MaxValue;
            _maxMeasuredHeight = double.MinValue;
            _minMeasuredSlot = int.MaxValue;
            _maxMeasuredSlot = -1;
            
            _globalRowHeightEstimate = _defaultRowHeight;
            _rowDetailsHeightEstimate = 0;
            
            _pendingCorrection = 0;
            _accumulatedError = 0;

            for (int i = 0; i < MaxRowGroupLevels; i++)
            {
                _rowGroupHeaderHeightsByLevel[i] = DefaultHeight;
            }

            // Reset Fenwick tree
            InitializeFenwickTree(_fenwickSize > 0 ? _fenwickSize : 1000);
        }

        /// <inheritdoc/>
        public void OnDataSourceChanged(int newItemCount)
        {
            _totalItemCount = newItemCount;
            Reset();
            
            // Resize Fenwick tree if needed
            if (newItemCount > _fenwickSize)
            {
                InitializeFenwickTree(Math.Max(newItemCount, _fenwickSize * 2));
            }
        }

        /// <inheritdoc/>
        public void OnItemsInserted(int startIndex, int count)
        {
            _totalItemCount += count;

            // Shift cached heights
            ShiftCachedHeights(startIndex, count, isInsert: true);

            // Shift regional statistics
            ShiftRegionalStatistics(startIndex, count, isInsert: true);

            // Rebuild Fenwick tree (could be optimized with a more sophisticated data structure)
            RebuildFenwickTree();
        }

        /// <inheritdoc/>
        public void OnItemsRemoved(int startIndex, int count)
        {
            _totalItemCount = Math.Max(0, _totalItemCount - count);

            // Remove and shift cached heights
            ShiftCachedHeights(startIndex, count, isInsert: false);

            // Update regional statistics
            ShiftRegionalStatistics(startIndex, count, isInsert: false);

            // Rebuild Fenwick tree
            RebuildFenwickTree();

            // Recalculate min/max
            RecalculateStatistics();
        }

        /// <inheritdoc/>
        public RowHeightEstimatorState CaptureState()
        {
            var regionStats = new Dictionary<int, RegionStatisticsState>(_regionStats.Count);
            foreach (var entry in _regionStats)
            {
                var stats = entry.Value;
                regionStats[entry.Key] = new RegionStatisticsState(
                    stats.Sum,
                    stats.Count,
                    stats.Min,
                    stats.Max,
                    new HashSet<int>(stats.MeasuredSlots));
            }

            return new AdvancedState(
                _defaultRowHeight,
                _globalRowHeightEstimate,
                _rowDetailsHeightEstimate,
                (double[])_rowGroupHeaderHeightsByLevel.Clone(),
                _totalItemCount,
                new Dictionary<int, double>(_measuredHeights),
                new Dictionary<int, double>(_detailsHeights),
                regionStats,
                (double[])_fenwickSumTree.Clone(),
                (int[])_fenwickCountTree.Clone(),
                _fenwickSize,
                _pendingCorrection,
                _accumulatedError,
                _minMeasuredSlot,
                _maxMeasuredSlot,
                _sumMeasuredHeights,
                _measuredCount,
                _minMeasuredHeight,
                _maxMeasuredHeight,
                _lastCollapsedSlotCount,
                _lastDetailsCount);
        }

        /// <inheritdoc/>
        public bool TryRestoreState(RowHeightEstimatorState state)
        {
            if (state is not AdvancedState snapshot)
            {
                return false;
            }

            _defaultRowHeight = snapshot.DefaultRowHeight;
            _globalRowHeightEstimate = snapshot.GlobalRowHeightEstimate;
            _rowDetailsHeightEstimate = snapshot.RowDetailsHeightEstimate;
            _totalItemCount = snapshot.TotalItemCount;
            _pendingCorrection = snapshot.PendingCorrection;
            _accumulatedError = snapshot.AccumulatedError;
            _minMeasuredSlot = snapshot.MinMeasuredSlot;
            _maxMeasuredSlot = snapshot.MaxMeasuredSlot;
            _sumMeasuredHeights = snapshot.SumMeasuredHeights;
            _measuredCount = snapshot.MeasuredCount;
            _minMeasuredHeight = snapshot.MinMeasuredHeight;
            _maxMeasuredHeight = snapshot.MaxMeasuredHeight;
            _lastCollapsedSlotCount = snapshot.LastCollapsedSlotCount;
            _lastDetailsCount = snapshot.LastDetailsCount;

            _measuredHeights.Clear();
            foreach (var entry in snapshot.MeasuredHeights)
            {
                _measuredHeights[entry.Key] = entry.Value;
            }

            _detailsHeights.Clear();
            foreach (var entry in snapshot.DetailsHeights)
            {
                _detailsHeights[entry.Key] = entry.Value;
            }

            _regionStats.Clear();
            foreach (var entry in snapshot.RegionStats)
            {
                var stats = new RegionStatistics
                {
                    Sum = entry.Value.Sum,
                    Count = entry.Value.Count,
                    Min = entry.Value.Min,
                    Max = entry.Value.Max
                };
                foreach (var slot in entry.Value.MeasuredSlots)
                {
                    stats.MeasuredSlots.Add(slot);
                }
                _regionStats[entry.Key] = stats;
            }

            _fenwickSumTree = snapshot.FenwickSumTree != null ? (double[])snapshot.FenwickSumTree.Clone() : Array.Empty<double>();
            _fenwickCountTree = snapshot.FenwickCountTree != null ? (int[])snapshot.FenwickCountTree.Clone() : Array.Empty<int>();
            _fenwickSize = snapshot.FenwickSize;

            if (snapshot.RowGroupHeaderHeightsByLevel != null)
            {
                var length = Math.Min(snapshot.RowGroupHeaderHeightsByLevel.Length, MaxRowGroupLevels);
                for (int i = 0; i < MaxRowGroupLevels; i++)
                {
                    _rowGroupHeaderHeightsByLevel[i] = i < length
                        ? snapshot.RowGroupHeaderHeightsByLevel[i]
                        : DefaultHeight;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public RowHeightEstimatorDiagnostics GetDiagnostics()
        {
            var regionInfo = string.Join(", ", _regionStats.Take(5).Select(r => $"R{r.Key}:{r.Value.Average:F1}({r.Value.Count})"));
            
            return new RowHeightEstimatorDiagnostics
            {
                AlgorithmName = "Advanced (Regional + Fenwick + Smooth Correction)",
                CurrentRowHeightEstimate = _globalRowHeightEstimate,
                CachedHeightCount = _measuredHeights.Count,
                TotalRowCount = _totalItemCount,
                EstimatedTotalHeight = CalculateTotalHeight(_totalItemCount, 0, Array.Empty<int>(), _detailsHeights.Count),
                MinMeasuredHeight = _minMeasuredHeight == double.MaxValue ? _globalRowHeightEstimate : _minMeasuredHeight,
                MaxMeasuredHeight = _maxMeasuredHeight == double.MinValue ? _globalRowHeightEstimate : _maxMeasuredHeight,
                AverageMeasuredHeight = _measuredCount > 0 ? _sumMeasuredHeights / _measuredCount : _globalRowHeightEstimate,
                AdditionalInfo = $"Regions: {_regionStats.Count}, Range: [{_minMeasuredSlot}-{_maxMeasuredSlot}], PendingCorr: {_pendingCorrection:F1}, Regions: {regionInfo}"
            };
        }

        private sealed class AdvancedState : RowHeightEstimatorState
        {
            public AdvancedState(
                double defaultRowHeight,
                double globalRowHeightEstimate,
                double rowDetailsHeightEstimate,
                double[] rowGroupHeaderHeightsByLevel,
                int totalItemCount,
                Dictionary<int, double> measuredHeights,
                Dictionary<int, double> detailsHeights,
                Dictionary<int, RegionStatisticsState> regionStats,
                double[] fenwickSumTree,
                int[] fenwickCountTree,
                int fenwickSize,
                double pendingCorrection,
                double accumulatedError,
                int minMeasuredSlot,
                int maxMeasuredSlot,
                double sumMeasuredHeights,
                int measuredCount,
                double minMeasuredHeight,
                double maxMeasuredHeight,
                int lastCollapsedSlotCount,
                int lastDetailsCount)
                : base(nameof(AdvancedRowHeightEstimator))
            {
                DefaultRowHeight = defaultRowHeight;
                GlobalRowHeightEstimate = globalRowHeightEstimate;
                RowDetailsHeightEstimate = rowDetailsHeightEstimate;
                RowGroupHeaderHeightsByLevel = rowGroupHeaderHeightsByLevel;
                TotalItemCount = totalItemCount;
                MeasuredHeights = measuredHeights;
                DetailsHeights = detailsHeights;
                RegionStats = regionStats;
                FenwickSumTree = fenwickSumTree;
                FenwickCountTree = fenwickCountTree;
                FenwickSize = fenwickSize;
                PendingCorrection = pendingCorrection;
                AccumulatedError = accumulatedError;
                MinMeasuredSlot = minMeasuredSlot;
                MaxMeasuredSlot = maxMeasuredSlot;
                SumMeasuredHeights = sumMeasuredHeights;
                MeasuredCount = measuredCount;
                MinMeasuredHeight = minMeasuredHeight;
                MaxMeasuredHeight = maxMeasuredHeight;
                LastCollapsedSlotCount = lastCollapsedSlotCount;
                LastDetailsCount = lastDetailsCount;
            }

            public double DefaultRowHeight { get; }
            public double GlobalRowHeightEstimate { get; }
            public double RowDetailsHeightEstimate { get; }
            public double[] RowGroupHeaderHeightsByLevel { get; }
            public int TotalItemCount { get; }
            public Dictionary<int, double> MeasuredHeights { get; }
            public Dictionary<int, double> DetailsHeights { get; }
            public Dictionary<int, RegionStatisticsState> RegionStats { get; }
            public double[] FenwickSumTree { get; }
            public int[] FenwickCountTree { get; }
            public int FenwickSize { get; }
            public double PendingCorrection { get; }
            public double AccumulatedError { get; }
            public int MinMeasuredSlot { get; }
            public int MaxMeasuredSlot { get; }
            public double SumMeasuredHeights { get; }
            public int MeasuredCount { get; }
            public double MinMeasuredHeight { get; }
            public double MaxMeasuredHeight { get; }
            public int LastCollapsedSlotCount { get; }
            public int LastDetailsCount { get; }
        }

        private sealed class RegionStatisticsState
        {
            public RegionStatisticsState(double sum, int count, double min, double max, HashSet<int> measuredSlots)
            {
                Sum = sum;
                Count = count;
                Min = min;
                Max = max;
                MeasuredSlots = measuredSlots;
            }

            public double Sum { get; }
            public int Count { get; }
            public double Min { get; }
            public double Max { get; }
            public HashSet<int> MeasuredSlots { get; }
        }

        #endregion

        #region Regional Statistics

        private class RegionStatistics
        {
            public double Sum { get; set; }
            public int Count { get; set; }
            public double Min { get; set; } = double.MaxValue;
            public double Max { get; set; } = double.MinValue;
            public double Average => Count > 0 ? Sum / Count : 0;
            
            // Track which slots in this region have been measured (for O(1) lookup)
            public HashSet<int> MeasuredSlots { get; } = new HashSet<int>();
        }

        private void UpdateRegionStatistics(int slot, double newHeight, double oldHeight, bool isNew)
        {
            int region = slot / RegionSize;

            if (!_regionStats.TryGetValue(region, out var stats))
            {
                stats = new RegionStatistics();
                _regionStats[region] = stats;
            }

            if (isNew)
            {
                stats.Sum += newHeight;
                stats.Count++;
                stats.MeasuredSlots.Add(slot);
            }
            else
            {
                stats.Sum = stats.Sum - oldHeight + newHeight;
            }

            stats.Min = Math.Min(stats.Min, newHeight);
            stats.Max = Math.Max(stats.Max, newHeight);
        }

        private double GetNearestRegionEstimate(int targetRegion)
        {
            if (_regionStats.Count == 0)
                return _globalRowHeightEstimate;

            // Find the nearest region with data
            int bestRegion = -1;
            int bestDistance = int.MaxValue;

            foreach (var region in _regionStats.Keys)
            {
                int distance = Math.Abs(region - targetRegion);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRegion = region;
                }
            }

            if (bestRegion >= 0 && _regionStats.TryGetValue(bestRegion, out var stats) && stats.Count > 0)
            {
                // Blend with global estimate based on distance
                double weight = 1.0 / (1.0 + bestDistance * 0.1);
                return stats.Average * weight + _globalRowHeightEstimate * (1 - weight);
            }

            return _globalRowHeightEstimate;
        }

        private double CalculateRowHeightsWithRegions(int regularRowCount, int totalSlotCount)
        {
            double totalHeight = 0;
            int accountedRows = 0;

            // Sum up cached heights
            foreach (var kvp in _measuredHeights)
            {
                if (kvp.Key < totalSlotCount)
                {
                    totalHeight += kvp.Value;
                    accountedRows++;
                }
            }

            // Estimate remaining rows using regional averages
            int remainingRows = regularRowCount - accountedRows;
            if (remainingRows > 0)
            {
                // Calculate weighted average from regions
                double regionWeightedSum = 0;
                int regionWeightedCount = 0;

                foreach (var kvp in _regionStats)
                {
                    if (kvp.Value.Count > 0)
                    {
                        // Use the tracked measured slots count instead of O(n) loop
                        int regionStart = kvp.Key * RegionSize;
                        int regionEnd = Math.Min((kvp.Key + 1) * RegionSize, totalSlotCount);
                        int regionMeasuredCount = kvp.Value.MeasuredSlots.Count;

                        int regionUnmeasured = (regionEnd - regionStart) - regionMeasuredCount;
                        if (regionUnmeasured > 0)
                        {
                            regionWeightedSum += kvp.Value.Average * regionUnmeasured;
                            regionWeightedCount += regionUnmeasured;
                        }
                    }
                }

                // Use regional weighted average if available, otherwise global
                double estimateForRemaining = regionWeightedCount > 0
                    ? regionWeightedSum / regionWeightedCount
                    : _globalRowHeightEstimate;

                // Account for rows in regions we haven't seen
                int unaccountedRegionRows = remainingRows - regionWeightedCount;
                if (unaccountedRegionRows > 0)
                {
                    totalHeight += unaccountedRegionRows * _globalRowHeightEstimate;
                }

                totalHeight += regionWeightedSum;
            }

            return totalHeight;
        }

        private double EstimateOffsetWithRegions(int slot)
        {
            double offset = 0;
            int currentSlot = 0;

            // Process each region
            while (currentSlot < slot)
            {
                int region = currentSlot / RegionSize;
                int regionStart = region * RegionSize;
                int regionEnd = Math.Min((region + 1) * RegionSize, slot);
                int slotsInRegion = regionEnd - currentSlot;

                // Count cached heights in this portion
                double cachedSum = 0;
                int cachedCount = 0;

                for (int i = currentSlot; i < regionEnd; i++)
                {
                    if (_measuredHeights.TryGetValue(i, out double height))
                    {
                        cachedSum += height;
                        cachedCount++;
                    }
                }

                // Add cached heights
                offset += cachedSum;

                // Estimate remaining slots in this portion
                int uncachedCount = slotsInRegion - cachedCount;
                if (uncachedCount > 0)
                {
                    if (_regionStats.TryGetValue(region, out var stats) && stats.Count > 0)
                    {
                        offset += uncachedCount * stats.Average;
                    }
                    else
                    {
                        offset += uncachedCount * _globalRowHeightEstimate;
                    }
                }

                currentSlot = regionEnd;
            }

            return offset;
        }

        private int FindSlotByOffsetRegional(double targetOffset, int totalSlotCount)
        {
            double accumulatedOffset = 0;
            int slot = 0;

            while (slot < totalSlotCount && accumulatedOffset < targetOffset)
            {
                double slotHeight = GetEstimatedHeight(slot);
                if (accumulatedOffset + slotHeight > targetOffset)
                {
                    break;
                }
                accumulatedOffset += slotHeight;
                slot++;
            }

            return Math.Min(slot, totalSlotCount - 1);
        }

        private void ShiftRegionalStatistics(int startIndex, int count, bool isInsert)
        {
            // For simplicity, rebuild affected regions
            // A more sophisticated implementation could do incremental updates
            var affectedRegions = new HashSet<int>();
            
            int startRegion = startIndex / RegionSize;
            int endRegion = (_totalItemCount + count) / RegionSize;

            for (int r = startRegion; r <= endRegion; r++)
            {
                affectedRegions.Add(r);
            }

            // Remove affected regions (they'll be rebuilt as heights are re-recorded)
            foreach (var region in affectedRegions)
            {
                _regionStats.Remove(region);
            }
        }

        #endregion

        #region Smooth Scroll Correction

        private double ApplySmoothCorrection()
        {
            // Apply a portion of the pending correction
            double correction = _pendingCorrection * SmoothCorrectionFactor;
            _pendingCorrection -= correction;

            // Decay accumulated error over time
            _accumulatedError *= 0.95;

            return correction;
        }

        private void ProcessScrollCorrection(double verticalOffset, double negVerticalOffset, int firstDisplayedSlot)
        {
            // EstimateOffsetToSlot counts the rows of collapsed groups, which take no space in the viewport.
            if (_lastCollapsedSlotCount > 0)
            {
                return;
            }

            // Calculate expected offset based on our estimates
            double expectedOffset = EstimateOffsetToSlot(firstDisplayedSlot) + negVerticalOffset;
            double actualOffset = verticalOffset;

            // Track the difference as pending correction
            double error = actualOffset - expectedOffset;
            
            // Only apply correction if error is significant
            if (Math.Abs(error) > 1.0)
            {
                _pendingCorrection += error * 0.5; // Add half the error as pending correction
            }
        }

        #endregion

        #region Fenwick Tree

        private void InitializeFenwickTree(int size)
        {
            _fenwickSize = size;
            _fenwickSumTree = new double[size + 1];
            _fenwickCountTree = new int[size + 1];
        }

        private void UpdateFenwickTree(int slot, double newHeight, double oldHeight, bool isNew)
        {
            if (slot >= _fenwickSize)
            {
                // Resize if needed
                int newSize = Math.Max(_fenwickSize * 2, slot + 1);
                ResizeFenwickTree(newSize);
            }

            double deltaSum = isNew ? newHeight : newHeight - oldHeight;
            int deltaCount = isNew ? 1 : 0;

            int i = slot + 1;
            while (i <= _fenwickSize)
            {
                _fenwickSumTree[i] += deltaSum;
                if (deltaCount != 0)
                {
                    _fenwickCountTree[i] += deltaCount;
                }
                i += i & (-i);
            }
        }

        private (double Sum, int Count) QueryFenwickPrefix(int count)
        {
            double measuredSum = 0;
            int measuredCount = 0;

            int i = Math.Min(count, _fenwickSize);
            while (i > 0)
            {
                measuredSum += _fenwickSumTree[i];
                measuredCount += _fenwickCountTree[i];
                i -= i & (-i);
            }

            return (measuredSum, measuredCount);
        }

        private int FindSlotByOffsetFenwick(double targetOffset, int totalSlotCount)
        {
            // Binary search using cumulative heights with measured data + estimates
            int low = 0;
            int high = Math.Min(_fenwickSize, totalSlotCount);
            
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                var (sum, measuredCount) = QueryFenwickPrefix(mid);
                double midOffset = sum + Math.Max(0, mid - measuredCount) * _globalRowHeightEstimate;
                
                if (midOffset <= targetOffset)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Min(low, totalSlotCount - 1);
        }

        private void ResizeFenwickTree(int newSize)
        {
            var oldSumTree = _fenwickSumTree;
            var oldCountTree = _fenwickCountTree;

            _fenwickSize = newSize;
            _fenwickSumTree = new double[newSize + 1];
            _fenwickCountTree = new int[newSize + 1];

            // Copy old data
            Array.Copy(oldSumTree, _fenwickSumTree, Math.Min(oldSumTree.Length, _fenwickSumTree.Length));
            Array.Copy(oldCountTree, _fenwickCountTree, Math.Min(oldCountTree.Length, _fenwickCountTree.Length));
        }

        private void RebuildFenwickTree()
        {
            // Reset tree
            Array.Clear(_fenwickSumTree, 0, _fenwickSumTree.Length);
            Array.Clear(_fenwickCountTree, 0, _fenwickCountTree.Length);

            // Rebuild from cached heights
            foreach (var kvp in _measuredHeights)
            {
                if (kvp.Key < _fenwickSize)
                {
                    UpdateFenwickTree(kvp.Key, kvp.Value, 0, isNew: true);
                }
            }
        }

        #endregion

        #region Cache Management

        private void EvictCacheEntries()
        {
            // Smart eviction: remove entries far from the current view
            // Prefer keeping entries near the middle of the measured range
            int midpoint = (_minMeasuredSlot + _maxMeasuredSlot) / 2;
            int removeCount = MaxCacheSize / 4;

            var sortedByDistance = _measuredHeights.Keys
                .OrderByDescending(k => Math.Abs(k - midpoint))
                .Take(removeCount)
                .ToList();

            foreach (var key in sortedByDistance)
            {
                var height = _measuredHeights[key];
                _measuredHeights.Remove(key);
                _sumMeasuredHeights -= height;
                _measuredCount--;

                // Update regional stats
                int region = key / RegionSize;
                if (_regionStats.TryGetValue(region, out var stats))
                {
                    stats.Sum -= height;
                    stats.Count--;
                    if (stats.Count == 0)
                    {
                        _regionStats.Remove(region);
                    }
                }
            }

            RecalculateStatistics();
            RebuildFenwickTree();
        }

        private void ShiftCachedHeights(int startIndex, int count, bool isInsert)
        {
            if (isInsert)
            {
                // Shift heights at or after startIndex up by count
                var slotsToUpdate = _measuredHeights.Keys.Where(k => k >= startIndex).OrderByDescending(k => k).ToList();
                foreach (var slot in slotsToUpdate)
                {
                    var height = _measuredHeights[slot];
                    _measuredHeights.Remove(slot);
                    _measuredHeights[slot + count] = height;
                }

                // Same for details
                var detailsToUpdate = _detailsHeights.Keys.Where(k => k >= startIndex).OrderByDescending(k => k).ToList();
                foreach (var slot in detailsToUpdate)
                {
                    var height = _detailsHeights[slot];
                    _detailsHeights.Remove(slot);
                    _detailsHeights[slot + count] = height;
                }

                // Update range tracking
                if (_minMeasuredSlot >= startIndex)
                    _minMeasuredSlot += count;
                if (_maxMeasuredSlot >= startIndex)
                    _maxMeasuredSlot += count;
            }
            else
            {
                // Remove heights in the deleted range
                var slotsToRemove = _measuredHeights.Keys.Where(k => k >= startIndex && k < startIndex + count).ToList();
                foreach (var slot in slotsToRemove)
                {
                    var height = _measuredHeights[slot];
                    _measuredHeights.Remove(slot);
                    _sumMeasuredHeights -= height;
                    _measuredCount--;
                }

                // Shift remaining heights down
                var slotsToShift = _measuredHeights.Keys.Where(k => k >= startIndex + count).OrderBy(k => k).ToList();
                foreach (var slot in slotsToShift)
                {
                    var height = _measuredHeights[slot];
                    _measuredHeights.Remove(slot);
                    _measuredHeights[slot - count] = height;
                }

                // Same for details
                var detailsToRemove = _detailsHeights.Keys.Where(k => k >= startIndex && k < startIndex + count).ToList();
                foreach (var slot in detailsToRemove)
                {
                    _detailsHeights.Remove(slot);
                }

                var detailsToShift = _detailsHeights.Keys.Where(k => k >= startIndex + count).OrderBy(k => k).ToList();
                foreach (var slot in detailsToShift)
                {
                    var height = _detailsHeights[slot];
                    _detailsHeights.Remove(slot);
                    _detailsHeights[slot - count] = height;
                }

                // Update range tracking
                if (_minMeasuredSlot >= startIndex + count)
                    _minMeasuredSlot -= count;
                else if (_minMeasuredSlot >= startIndex)
                    _minMeasuredSlot = startIndex;

                if (_maxMeasuredSlot >= startIndex + count)
                    _maxMeasuredSlot -= count;
                else if (_maxMeasuredSlot >= startIndex)
                    _maxMeasuredSlot = Math.Max(startIndex - 1, -1);
            }
        }

        private void RecalculateStatistics()
        {
            if (_measuredHeights.Count == 0)
            {
                _minMeasuredHeight = double.MaxValue;
                _maxMeasuredHeight = double.MinValue;
                _minMeasuredSlot = int.MaxValue;
                _maxMeasuredSlot = -1;
                _sumMeasuredHeights = 0;
                _measuredCount = 0;
            }
            else
            {
                _minMeasuredHeight = _measuredHeights.Values.Min();
                _maxMeasuredHeight = _measuredHeights.Values.Max();
                _minMeasuredSlot = _measuredHeights.Keys.Min();
                _maxMeasuredSlot = _measuredHeights.Keys.Max();
                _sumMeasuredHeights = _measuredHeights.Values.Sum();
                _measuredCount = _measuredHeights.Count;
            }

            _globalRowHeightEstimate = _measuredCount > 0
                ? _sumMeasuredHeights / _measuredCount
                : _defaultRowHeight;
        }

        private double CalculateDetailsHeight(int detailsVisibleCount)
        {
            double detailsHeight = 0;

            // Use cached details heights
            foreach (var kvp in _detailsHeights)
            {
                detailsHeight += kvp.Value;
                detailsVisibleCount--;
            }

            // Estimate remaining
            detailsHeight += Math.Max(0, detailsVisibleCount) * _rowDetailsHeightEstimate;

            return detailsHeight;
        }

        #endregion
    }
}
