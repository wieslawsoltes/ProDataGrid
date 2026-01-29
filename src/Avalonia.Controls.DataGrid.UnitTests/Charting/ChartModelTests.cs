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
    }
}
