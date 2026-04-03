// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartModelTests
    {
        [Fact]
        public void ChartModel_Uses_Incremental_Update_When_Available()
        {
            var source = new IncrementalTestSource();
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            ChartDataUpdate? lastUpdate = null;
            model.SnapshotUpdated += (_, e) => lastUpdate = e.Update;

            model.Refresh();

            Assert.Equal(1, source.TryBuildUpdateCalls);
            Assert.Equal(0, source.BuildSnapshotCalls);
            Assert.NotNull(lastUpdate);
            Assert.Equal(ChartDataDeltaKind.Insert, lastUpdate!.Delta.Kind);
            Assert.Equal(2, lastUpdate.Snapshot.Series[0].Values.Count);
        }

        [Fact]
        public void ChartModel_RequestChange_Triggers_Refresh()
        {
            var source = new CountingSource();
            var model = new ChartModel { DataSource = source };

            Assert.Equal(1, source.BuildSnapshotCalls);

            model.Request.MaxPoints = 250;

            Assert.Equal(2, source.BuildSnapshotCalls);
        }

        [Fact]
        public void ChartModel_AutoRefresh_Disabled_Does_Not_Refresh_Until_Manual()
        {
            var source = new CountingSource();
            var model = new ChartModel { AutoRefresh = false };

            model.DataSource = source;

            Assert.Equal(0, source.BuildSnapshotCalls);

            model.Refresh();

            Assert.Equal(1, source.BuildSnapshotCalls);
        }

        [Fact]
        public void ChartModel_ShowLatest_Follows_New_Data_When_Enabled()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();

            Assert.True(model.ShowLatest(5, followLatest: true));
            Assert.True(model.Interaction.FollowLatest);
            Assert.Equal(15, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);

            source.TotalCategories = 23;
            model.Refresh();

            Assert.Equal(18, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);
            Assert.Equal(5, model.Snapshot.Categories.Count);
            Assert.Equal("C19", model.Snapshot.Categories[0]);
            Assert.Equal("C23", model.Snapshot.Categories[4]);
        }

        [Fact]
        public void ChartModel_PanWindow_Disables_FollowLatest_And_Clamps()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();
            model.ShowLatest(5, followLatest: true);

            Assert.True(model.PanWindow(-3));
            Assert.False(model.Interaction.FollowLatest);
            Assert.Equal(12, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);

            Assert.True(model.PanWindow(-50));
            Assert.Equal(0, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);
        }

        [Fact]
        public void ChartModel_ZoomWindow_Adjusts_Window_Count()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();
            model.SetVisibleWindow(5, 10, false);

            Assert.True(model.ZoomWindow(2d, 0d, minWindowCount: 2));
            Assert.Equal(5, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);
        }

        [Fact]
        public void ChartModel_WindowHistory_Supports_Undo_And_Redo()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();

            Assert.False(model.CanUndoWindow);
            Assert.False(model.CanRedoWindow);

            Assert.True(model.SetVisibleWindow(5, 10, false));
            Assert.True(model.ZoomWindow(2d, 0d, minWindowCount: 2));

            Assert.True(model.CanUndoWindow);
            Assert.False(model.CanRedoWindow);
            Assert.Equal(5, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);

            Assert.True(model.UndoWindow());
            Assert.Equal(5, model.Request.WindowStart);
            Assert.Equal(10, model.Request.WindowCount);
            Assert.True(model.CanRedoWindow);

            Assert.True(model.RedoWindow());
            Assert.Equal(5, model.Request.WindowStart);
            Assert.Equal(5, model.Request.WindowCount);
        }

        [Fact]
        public void ChartModel_TryGetVisibleWindow_Returns_Clamped_Window_State()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();
            model.SetVisibleWindow(17, 10, false);

            Assert.True(model.TryGetVisibleWindow(out var total, out var start, out var count));
            Assert.Equal(20, total);
            Assert.Equal(10, count);
            Assert.Equal(10, start);
        }

        [Fact]
        public void ChartModel_ValueRangeHistory_Supports_Pan_Undo_And_Redo()
        {
            var source = new WindowedSource(20);
            var model = new ChartModel { AutoRefresh = false, DataSource = source };

            model.Refresh();

            Assert.True(model.SetValueRange(10d, 20d));
            Assert.Equal(10d, model.ValueAxis.Minimum);
            Assert.Equal(20d, model.ValueAxis.Maximum);

            Assert.True(model.PanValueRange(2.5d));
            Assert.Equal(12.5d, model.ValueAxis.Minimum);
            Assert.Equal(22.5d, model.ValueAxis.Maximum);

            Assert.True(model.CanUndoWindow);
            Assert.True(model.UndoWindow());
            Assert.Equal(10d, model.ValueAxis.Minimum);
            Assert.Equal(20d, model.ValueAxis.Maximum);

            Assert.True(model.CanRedoWindow);
            Assert.True(model.RedoWindow());
            Assert.Equal(12.5d, model.ValueAxis.Minimum);
            Assert.Equal(22.5d, model.ValueAxis.Maximum);

            Assert.True(model.ResetValueRange());
            Assert.Null(model.ValueAxis.Minimum);
            Assert.Null(model.ValueAxis.Maximum);
        }

        [Fact]
        public void ChartModel_TrackCrosshair_Updates_InteractionState()
        {
            var model = new ChartModel();

            model.TrackCrosshair(2, "10:15", 91.1d, 0.25d, 0.75d);

            Assert.True(model.Interaction.IsCrosshairVisible);
            Assert.Equal(2, model.Interaction.CrosshairCategoryIndex);
            Assert.Equal("10:15", model.Interaction.CrosshairCategoryLabel);
            Assert.Equal(91.1d, model.Interaction.CrosshairValue);
            Assert.Equal(0.25d, model.Interaction.CrosshairHorizontalRatio);
            Assert.Equal(0.75d, model.Interaction.CrosshairVerticalRatio);

            model.ClearCrosshair();

            Assert.False(model.Interaction.IsCrosshairVisible);
            Assert.Null(model.Interaction.CrosshairCategoryIndex);
            Assert.Null(model.Interaction.CrosshairCategoryLabel);
            Assert.Null(model.Interaction.CrosshairValue);
        }

        [Fact]
        public void ChartModel_Raises_ValueAxis_Change_When_Nested_ValueFormat_Updates()
        {
            var model = new ChartModel();
            var format = new ChartValueFormat
            {
                MaximumFractionDigits = 2
            };
            var propertyChanges = new List<string>();
            model.PropertyChanged += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.PropertyName))
                {
                    propertyChanges.Add(e.PropertyName!);
                }
            };

            model.ValueAxis.ValueFormat = format;
            propertyChanges.Clear();

            format.MaximumFractionDigits = 4;

            Assert.Contains(nameof(ChartModel.ValueAxis), propertyChanges);
        }

        private sealed class CountingSource : IChartDataSource
        {
            private readonly ChartDataSnapshot _snapshot;

            public CountingSource()
            {
                _snapshot = new ChartDataSnapshot(
                    new[] { "A" },
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Line, new double?[] { 1d }) },
                    1);
            }

            public int BuildSnapshotCalls { get; private set; }

            public event EventHandler? DataInvalidated;

            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
            {
                BuildSnapshotCalls++;
                return _snapshot;
            }

            public void Invalidate() => DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private sealed class IncrementalTestSource : IChartIncrementalDataSource
        {
            private readonly ChartDataUpdate _update;

            public IncrementalTestSource()
            {
                var snapshot = new ChartDataSnapshot(
                    new[] { "A", "B" },
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Line, new double?[] { 1d, 2d }) },
                    1);
                _update = new ChartDataUpdate(snapshot, new ChartDataDelta(ChartDataDeltaKind.Insert, 0, 0, 2));
            }

            public int BuildSnapshotCalls { get; private set; }

            public int TryBuildUpdateCalls { get; private set; }

            public event EventHandler? DataInvalidated;

            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
            {
                BuildSnapshotCalls++;
                return _update.Snapshot;
            }

            public bool TryBuildUpdate(ChartDataRequest request, ChartDataSnapshot previousSnapshot, out ChartDataUpdate update)
            {
                TryBuildUpdateCalls++;
                update = _update;
                return true;
            }
        }

        private sealed class WindowedSource : IChartDataSource, IChartWindowInfoProvider
        {
            public WindowedSource(int totalCategories)
            {
                TotalCategories = totalCategories;
            }

            public int TotalCategories { get; set; }

            public event EventHandler? DataInvalidated;

            public int? GetTotalCategoryCount() => TotalCategories;

            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
            {
                var total = Math.Max(0, TotalCategories);
                var count = request.WindowCount ?? total;
                if (count <= 0 || count > total)
                {
                    count = total;
                }

                var start = request.WindowStart ?? Math.Max(0, total - count);
                if (start < 0)
                {
                    start = 0;
                }

                if (start + count > total)
                {
                    start = Math.Max(0, total - count);
                }

                var categories = new string?[count];
                var values = new double?[count];
                for (var i = 0; i < count; i++)
                {
                    categories[i] = $"C{start + i + 1}";
                    values[i] = start + i + 1;
                }

                return new ChartDataSnapshot(
                    categories,
                    new[] { new ChartSeriesSnapshot("Series", ChartSeriesKind.Line, values) });
            }

            public void Invalidate() => DataInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
