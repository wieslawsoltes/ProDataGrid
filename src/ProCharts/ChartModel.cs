// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.ComponentModel;
using System.Collections.Generic;

namespace ProCharts
{
    public sealed class ChartModel : INotifyPropertyChanged, IDisposable
    {
        private IChartDataSource? _dataSource;
        private EventHandler? _dataInvalidatedHandler;
        private ChartDataSnapshot _snapshot = ChartDataSnapshot.Empty;
        private bool _autoRefresh = true;
        private int _updateNesting;
        private bool _pendingRefresh;
        private ChartTheme? _theme;
        private IReadOnlyList<ChartSeriesStyle>? _seriesStyles;
        private bool _suppressRequestRefresh;
        private readonly List<ChartViewportState> _viewportHistory = new();
        private int _viewportHistoryIndex = -1;
        private bool _suppressViewportHistory;

        public ChartModel()
        {
            Request = new ChartDataRequest();
            Request.PropertyChanged += OnRequestChanged;
            Interaction = new ChartInteractionState();
            Interaction.PropertyChanged += OnInteractionChanged;
            CategoryAxis = new ChartAxisDefinition(ChartAxisKind.Category);
            CategoryAxis.PropertyChanged += OnAxisChanged;
            SecondaryCategoryAxis = new ChartAxisDefinition(ChartAxisKind.Category) { IsVisible = false };
            SecondaryCategoryAxis.PropertyChanged += OnAxisChanged;
            ValueAxis = new ChartAxisDefinition(ChartAxisKind.Value);
            ValueAxis.PropertyChanged += OnAxisChanged;
            SecondaryValueAxis = new ChartAxisDefinition(ChartAxisKind.Value) { IsVisible = false };
            SecondaryValueAxis.PropertyChanged += OnAxisChanged;
            Legend = new ChartLegendDefinition();
            Legend.PropertyChanged += OnLegendChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? SnapshotChanged;

        public event EventHandler<ChartDataUpdateEventArgs>? SnapshotUpdated;

        public ChartDataRequest Request { get; }

        /// <summary>
        /// Gets the mutable interaction state for viewport-follow and crosshair behavior.
        /// </summary>
        public ChartInteractionState Interaction { get; }

        public ChartAxisDefinition CategoryAxis { get; }

        public ChartAxisDefinition SecondaryCategoryAxis { get; }

        public ChartAxisDefinition ValueAxis { get; }

        public ChartAxisDefinition SecondaryValueAxis { get; }

        public ChartLegendDefinition Legend { get; }

        public ChartTheme? Theme
        {
            get => _theme;
            set
            {
                if (ReferenceEquals(_theme, value))
                {
                    return;
                }

                _theme = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
            }
        }

        public IReadOnlyList<ChartSeriesStyle>? SeriesStyles
        {
            get => _seriesStyles;
            set
            {
                if (ReferenceEquals(_seriesStyles, value))
                {
                    return;
                }

                _seriesStyles = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeriesStyles)));
            }
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh == value)
                {
                    return;
                }

                _autoRefresh = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoRefresh)));
                RequestRefresh();
            }
        }

        public IChartDataSource? DataSource
        {
            get => _dataSource;
            set
            {
                if (ReferenceEquals(_dataSource, value))
                {
                    return;
                }

                DetachDataSource();
                _dataSource = value;
                AttachDataSource();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataSource)));
                RequestRefresh();
            }
        }

        public ChartDataSnapshot Snapshot
        {
            get => _snapshot;
            private set
            {
                if (ReferenceEquals(_snapshot, value))
                {
                    return;
                }

                _snapshot = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Snapshot)));
                SnapshotChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ChartDataUpdate? LastUpdate { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an older viewport state can be restored.
        /// </summary>
        public bool CanUndoWindow => _viewportHistoryIndex > 0;

        /// <summary>
        /// Gets a value indicating whether a newer viewport state can be restored.
        /// </summary>
        public bool CanRedoWindow => _viewportHistoryIndex >= 0 && _viewportHistoryIndex < _viewportHistory.Count - 1;

        public void Refresh()
        {
            var dataSource = _dataSource;
            if (dataSource == null)
            {
                ApplyUpdate(new ChartDataUpdate(ChartDataSnapshot.Empty, ChartDataDelta.Full));
                return;
            }

            AlignWindowToLatestIfNeeded();

            if (dataSource is IChartIncrementalDataSource incremental)
            {
                if (incremental.TryBuildUpdate(Request, _snapshot, out var update))
                {
                    ApplyUpdate(update);
                    return;
                }
            }

            ApplyUpdate(new ChartDataUpdate(dataSource.BuildSnapshot(Request), ChartDataDelta.Full));
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
            if (_updateNesting == 0 && _pendingRefresh)
            {
                _pendingRefresh = false;
                if (_autoRefresh)
                {
                    Refresh();
                }
            }
        }

        public IDisposable DeferRefresh()
        {
            BeginUpdate();
            return new DeferScope(this);
        }

        public void Dispose()
        {
            DetachDataSource();
            Request.PropertyChanged -= OnRequestChanged;
            Interaction.PropertyChanged -= OnInteractionChanged;
            CategoryAxis.PropertyChanged -= OnAxisChanged;
            SecondaryCategoryAxis.PropertyChanged -= OnAxisChanged;
            ValueAxis.PropertyChanged -= OnAxisChanged;
            SecondaryValueAxis.PropertyChanged -= OnAxisChanged;
            Legend.PropertyChanged -= OnLegendChanged;
        }

        private void AttachDataSource()
        {
            if (_dataSource == null)
            {
                return;
            }

            _dataInvalidatedHandler ??= CreateDataInvalidatedHandler();
            _dataSource.DataInvalidated += _dataInvalidatedHandler;
        }

        private void DetachDataSource()
        {
            if (_dataSource == null || _dataInvalidatedHandler == null)
            {
                return;
            }

            _dataSource.DataInvalidated -= _dataInvalidatedHandler;
        }

        private EventHandler CreateDataInvalidatedHandler()
        {
            var weakSelf = new WeakReference<ChartModel>(this);
            EventHandler? handler = null;
            handler = (sender, args) =>
            {
                if (!weakSelf.TryGetTarget(out var model))
                {
                    if (sender is IChartDataSource source)
                    {
                        source.DataInvalidated -= handler;
                    }

                    return;
                }

                model.RequestRefresh();
            };
            return handler;
        }

        /// <summary>
        /// Pans the visible category window by the specified number of categories.
        /// Positive values move toward newer categories; negative values move toward older categories.
        /// </summary>
        public bool PanWindow(int deltaCategories)
        {
            if (!TryGetWindowState(out var total, out var start, out var count) || count >= total)
            {
                return false;
            }

            return ApplyWindow(total, start + deltaCategories, count, followLatest: false);
        }

        /// <summary>
        /// Zooms the visible category window around the supplied anchor ratio.
        /// Scales greater than 1 zoom in; scales smaller than 1 zoom out.
        /// </summary>
        public bool ZoomWindow(double scale, double anchorRatio, int minWindowCount = 10)
        {
            if (!TryGetWindowState(out var total, out var start, out var count))
            {
                return false;
            }

            if (scale <= 0d || total <= 0)
            {
                return false;
            }

            var clampedAnchor = ClampRatio(anchorRatio);
            var newCount = (int)Math.Round(count / scale);
            var minCount = Math.Max(1, Math.Min(minWindowCount, total));
            newCount = Math.Max(minCount, Math.Min(newCount, total));

            var anchorIndex = start + (int)Math.Round(clampedAnchor * Math.Max(0, count - 1));
            var newStart = anchorIndex - (int)Math.Round(clampedAnchor * Math.Max(0, newCount - 1));

            return ApplyWindow(total, newStart, newCount, followLatest: false);
        }

        /// <summary>
        /// Shows the latest portion of the dataset and optionally enables follow-latest mode.
        /// </summary>
        public bool ShowLatest(int preferredWindowCount, bool followLatest = true)
        {
            var total = GetTotalCategoryCount();
            if (total <= 0)
            {
                return false;
            }

            var count = preferredWindowCount <= 0 ? total : Math.Min(preferredWindowCount, total);
            return ApplyWindow(total, total - count, count, followLatest);
        }

        /// <summary>
        /// Applies an explicit visible category window.
        /// </summary>
        public bool SetVisibleWindow(int start, int count, bool followLatest = false)
        {
            var total = GetTotalCategoryCount();
            if (total <= 0)
            {
                return false;
            }

            return ApplyWindow(total, start, count, followLatest);
        }

        /// <summary>
        /// Resets the chart to show the full available category range.
        /// </summary>
        public bool ResetWindow(bool followLatest = false)
        {
            var total = GetTotalCategoryCount();
            if (total <= 0)
            {
                return false;
            }

            var followLatestChanged = Interaction.FollowLatest != followLatest;
            var windowAlreadyReset = Request.WindowStart == null && Request.WindowCount == null;

            if (windowAlreadyReset && !followLatestChanged)
            {
                if (!_autoRefresh)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Request)));
                }

                return false;
            }

            EnsureViewportHistorySeeded();
            using (SuppressRequestRefresh())
            {
                Interaction.FollowLatest = followLatest;
                Request.WindowStart = null;
                Request.WindowCount = null;
            }

            RecordViewportState();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Request)));
            RequestRefresh();
            return true;
        }

        /// <summary>
        /// Applies an explicit visible value range to the primary value axis.
        /// </summary>
        /// <param name="minimum">The minimum visible value.</param>
        /// <param name="maximum">The maximum visible value.</param>
        /// <returns><see langword="true"/> when the value range changed; otherwise, <see langword="false"/>.</returns>
        public bool SetValueRange(double minimum, double maximum)
        {
            if (double.IsNaN(minimum) || double.IsInfinity(minimum) ||
                double.IsNaN(maximum) || double.IsInfinity(maximum) ||
                maximum <= minimum)
            {
                return false;
            }

            return SetValueRangeCore(minimum, maximum);
        }

        /// <summary>
        /// Pans the visible primary value-axis range by the specified amount.
        /// Positive values shift the visible range upward; negative values shift it downward.
        /// </summary>
        /// <param name="delta">The value delta applied to both minimum and maximum extents.</param>
        /// <returns><see langword="true"/> when the value range changed; otherwise, <see langword="false"/>.</returns>
        public bool PanValueRange(double delta)
        {
            var minimum = ValueAxis.Minimum;
            var maximum = ValueAxis.Maximum;
            if (!minimum.HasValue || !maximum.HasValue || double.IsNaN(delta) || double.IsInfinity(delta))
            {
                return false;
            }

            return SetValueRangeCore(minimum.Value + delta, maximum.Value + delta);
        }

        /// <summary>
        /// Clears any explicit primary value-axis range and returns to auto-fitting.
        /// </summary>
        /// <returns><see langword="true"/> when the value range changed; otherwise, <see langword="false"/>.</returns>
        public bool ResetValueRange()
        {
            return SetValueRangeCore(null, null);
        }

        /// <summary>
        /// Gets the current visible category window after clamping it to the available category count.
        /// </summary>
        /// <param name="totalCategoryCount">Receives the total number of categories available from the current data source.</param>
        /// <param name="windowStart">Receives the zero-based start index of the visible category window.</param>
        /// <param name="windowCount">Receives the number of visible categories in the current window.</param>
        /// <returns><see langword="true"/> when a visible window can be resolved; otherwise, <see langword="false"/>.</returns>
        public bool TryGetVisibleWindow(out int totalCategoryCount, out int windowStart, out int windowCount)
        {
            return TryGetWindowState(out totalCategoryCount, out windowStart, out windowCount);
        }

        /// <summary>
        /// Restores the previous visible category window when available.
        /// </summary>
        public bool UndoWindow()
        {
            if (!CanUndoWindow)
            {
                return false;
            }

            _viewportHistoryIndex--;
            RaiseViewportHistoryProperties();
            return RestoreViewportState(_viewportHistory[_viewportHistoryIndex]);
        }

        /// <summary>
        /// Reapplies a viewport state that was previously undone.
        /// </summary>
        public bool RedoWindow()
        {
            if (!CanRedoWindow)
            {
                return false;
            }

            _viewportHistoryIndex++;
            RaiseViewportHistoryProperties();
            return RestoreViewportState(_viewportHistory[_viewportHistoryIndex]);
        }

        /// <summary>
        /// Updates the active crosshair state using visible-window-local coordinates.
        /// </summary>
        public void TrackCrosshair(
            int? categoryIndex,
            string? categoryLabel,
            double? value,
            double horizontalRatio,
            double verticalRatio)
        {
            Interaction.SetCrosshair(categoryIndex, categoryLabel, value, horizontalRatio, verticalRatio);
        }

        /// <summary>
        /// Clears the active crosshair state.
        /// </summary>
        public void ClearCrosshair()
        {
            Interaction.ClearCrosshair();
        }

        private void OnRequestChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Request)));
            if (_suppressRequestRefresh)
            {
                return;
            }

            RequestRefresh();
        }

        private void OnInteractionChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Interaction)));
            if (_suppressRequestRefresh)
            {
                return;
            }

            if (e.PropertyName == nameof(ChartInteractionState.FollowLatest))
            {
                RequestRefresh();
            }
        }

        private void OnAxisChanged(object? sender, PropertyChangedEventArgs e)
        {
            var propertyName = ReferenceEquals(sender, CategoryAxis)
                ? nameof(CategoryAxis)
                : ReferenceEquals(sender, SecondaryCategoryAxis)
                    ? nameof(SecondaryCategoryAxis)
                    : ReferenceEquals(sender, SecondaryValueAxis)
                        ? nameof(SecondaryValueAxis)
                        : nameof(ValueAxis);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnLegendChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Legend)));
        }

        private void RequestRefresh()
        {
            if (_updateNesting > 0)
            {
                _pendingRefresh = true;
                return;
            }

            if (_autoRefresh)
            {
                Refresh();
            }
        }

        private void ApplyUpdate(ChartDataUpdate update)
        {
            LastUpdate = update;
            Snapshot = update.Snapshot;
            SnapshotUpdated?.Invoke(this, new ChartDataUpdateEventArgs(update));
        }

        private int GetTotalCategoryCount()
        {
            if (_dataSource is IChartWindowInfoProvider provider)
            {
                var count = provider.GetTotalCategoryCount();
                if (count.HasValue)
                {
                    return Math.Max(0, count.Value);
                }
            }

            return _snapshot.Categories.Count;
        }

        private bool TryGetWindowState(out int total, out int start, out int count)
        {
            total = GetTotalCategoryCount();
            start = 0;
            count = 0;

            if (total <= 0)
            {
                return false;
            }

            start = Request.WindowStart ?? 0;
            count = Request.WindowCount ?? total;
            if (count <= 0 || count > total)
            {
                count = total;
            }

            if (start < 0)
            {
                start = 0;
            }

            if (start + count > total)
            {
                start = Math.Max(0, total - count);
            }

            return true;
        }

        private bool ApplyWindow(int total, int start, int count, bool followLatest)
        {
            if (total <= 0)
            {
                return false;
            }

            var newCount = Math.Max(1, Math.Min(count, total));
            var newStart = Math.Max(0, Math.Min(start, Math.Max(0, total - newCount)));
            var resetWindow = newStart == 0 && newCount >= total;
            int? targetStart = resetWindow ? null : newStart;
            int? targetCount = resetWindow ? null : newCount;
            if (Interaction.FollowLatest == followLatest &&
                Request.WindowStart == targetStart &&
                Request.WindowCount == targetCount)
            {
                return false;
            }

            EnsureViewportHistorySeeded();
            using (SuppressRequestRefresh())
            {
                if (Interaction.FollowLatest != followLatest)
                {
                    Interaction.FollowLatest = followLatest;
                }

                Request.WindowStart = targetStart;
                Request.WindowCount = targetCount;
            }

            RecordViewportState();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Request)));
            RequestRefresh();
            return true;
        }

        private void AlignWindowToLatestIfNeeded()
        {
            if (!Interaction.FollowLatest)
            {
                return;
            }

            var total = GetTotalCategoryCount();
            if (total <= 0)
            {
                return;
            }

            if (!Request.WindowCount.HasValue || Request.WindowCount.Value >= total)
            {
                using (SuppressRequestRefresh())
                {
                    Request.WindowStart = null;
                    Request.WindowCount = null;
                }

                return;
            }

            var count = Math.Max(1, Math.Min(Request.WindowCount.Value, total));
            var start = Math.Max(0, total - count);
            using (SuppressRequestRefresh())
            {
                Request.WindowCount = count;
                Request.WindowStart = start;
            }
        }

        private IDisposable SuppressRequestRefresh()
        {
            _suppressRequestRefresh = true;
            return new RequestRefreshScope(this);
        }

        private bool RestoreViewportState(ChartViewportState state)
        {
            using (SuppressViewportHistory())
            {
                using (SuppressRequestRefresh())
                {
                    Interaction.FollowLatest = state.FollowLatest;
                    Request.WindowStart = state.WindowStart;
                    Request.WindowCount = state.WindowCount;
                    ValueAxis.Minimum = state.ValueAxisMinimum;
                    ValueAxis.Maximum = state.ValueAxisMaximum;
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Request)));
            RequestRefresh();
            return true;
        }

        private void RecordViewportState()
        {
            if (_suppressViewportHistory)
            {
                return;
            }

            var state = new ChartViewportState(
                Request.WindowStart,
                Request.WindowCount,
                Interaction.FollowLatest,
                ValueAxis.Minimum,
                ValueAxis.Maximum);
            if (_viewportHistoryIndex < 0)
            {
                _viewportHistory.Add(state);
                _viewportHistoryIndex = 0;
                RaiseViewportHistoryProperties();
                return;
            }

            var currentState = _viewportHistory[_viewportHistoryIndex];
            if (currentState.Equals(state))
            {
                return;
            }

            if (_viewportHistoryIndex < _viewportHistory.Count - 1)
            {
                _viewportHistory.RemoveRange(_viewportHistoryIndex + 1, _viewportHistory.Count - _viewportHistoryIndex - 1);
            }

            _viewportHistory.Add(state);
            _viewportHistoryIndex = _viewportHistory.Count - 1;
            RaiseViewportHistoryProperties();
        }

        private void EnsureViewportHistorySeeded()
        {
            if (_suppressViewportHistory || _viewportHistoryIndex >= 0)
            {
                return;
            }

            _viewportHistory.Add(new ChartViewportState(
                Request.WindowStart,
                Request.WindowCount,
                Interaction.FollowLatest,
                ValueAxis.Minimum,
                ValueAxis.Maximum));
            _viewportHistoryIndex = 0;
            RaiseViewportHistoryProperties();
        }

        private bool SetValueRangeCore(double? minimum, double? maximum)
        {
            if (ValueAxis.Minimum == minimum && ValueAxis.Maximum == maximum)
            {
                return false;
            }

            EnsureViewportHistorySeeded();
            ValueAxis.Minimum = minimum;
            ValueAxis.Maximum = maximum;
            RecordViewportState();
            return true;
        }

        private IDisposable SuppressViewportHistory()
        {
            _suppressViewportHistory = true;
            return new ViewportHistoryScope(this);
        }

        private void RaiseViewportHistoryProperties()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanUndoWindow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanRedoWindow)));
        }

        private static double ClampRatio(double value)
        {
            if (value < 0d)
            {
                return 0d;
            }

            if (value > 1d)
            {
                return 1d;
            }

            return value;
        }

        private sealed class DeferScope : IDisposable
        {
            private ChartModel? _model;

            public DeferScope(ChartModel model)
            {
                _model = model;
            }

            public void Dispose()
            {
                var model = _model;
                if (model == null)
                {
                    return;
                }

                _model = null;
                model.EndUpdate();
            }
        }

        private sealed class RequestRefreshScope : IDisposable
        {
            private ChartModel? _model;

            public RequestRefreshScope(ChartModel model)
            {
                _model = model;
            }

            public void Dispose()
            {
                var model = _model;
                if (model == null)
                {
                    return;
                }

                _model = null;
                model._suppressRequestRefresh = false;
            }
        }

        private sealed class ViewportHistoryScope : IDisposable
        {
            private ChartModel? _model;

            public ViewportHistoryScope(ChartModel model)
            {
                _model = model;
            }

            public void Dispose()
            {
                var model = _model;
                if (model == null)
                {
                    return;
                }

                _model = null;
                model._suppressViewportHistory = false;
            }
        }

        private readonly struct ChartViewportState : IEquatable<ChartViewportState>
        {
            public ChartViewportState(
                int? windowStart,
                int? windowCount,
                bool followLatest,
                double? valueAxisMinimum,
                double? valueAxisMaximum)
            {
                WindowStart = windowStart;
                WindowCount = windowCount;
                FollowLatest = followLatest;
                ValueAxisMinimum = valueAxisMinimum;
                ValueAxisMaximum = valueAxisMaximum;
            }

            public int? WindowStart { get; }

            public int? WindowCount { get; }

            public bool FollowLatest { get; }

            public double? ValueAxisMinimum { get; }

            public double? ValueAxisMaximum { get; }

            public bool Equals(ChartViewportState other)
            {
                return WindowStart == other.WindowStart &&
                    WindowCount == other.WindowCount &&
                    FollowLatest == other.FollowLatest &&
                    ValueAxisMinimum == other.ValueAxisMinimum &&
                    ValueAxisMaximum == other.ValueAxisMaximum;
            }

            public override bool Equals(object? obj)
            {
                return obj is ChartViewportState other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(WindowStart, WindowCount, FollowLatest, ValueAxisMinimum, ValueAxisMaximum);
            }
        }
    }
}
