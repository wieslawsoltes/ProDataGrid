// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.ObjectModel;
using ProCharts;
using ProDataGrid.Charting;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class DataGridChartModelTests
    {
        [Fact]
        public void BuildSnapshot_Uses_Category_And_Value_Paths()
        {
            var items = new[]
            {
                new SampleRow { Category = "A", Revenue = 10d },
                new SampleRow { Category = "B", Revenue = 25d }
            };

            var model = new DataGridChartModel
            {
                ItemsSource = items,
                CategoryPath = nameof(SampleRow.Category)
            };
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Revenue",
                ValuePath = nameof(SampleRow.Revenue),
                Kind = ChartSeriesKind.Column
            });

            var snapshot = model.BuildSnapshot(new ChartDataRequest());

            Assert.Equal(2, snapshot.Categories.Count);
            Assert.Equal("A", snapshot.Categories[0]);
            Assert.Equal("B", snapshot.Categories[1]);
            Assert.Equal(10d, snapshot.Series[0].Values[0]);
            Assert.Equal(25d, snapshot.Series[0].Values[1]);
        }

        [Fact]
        public void BuildSnapshot_Evaluates_Formula_Series()
        {
            var items = new[]
            {
                new SampleRow { Category = "A", Revenue = 10d, Cost = 3d },
                new SampleRow { Category = "B", Revenue = 20d, Cost = 8d }
            };

            var model = new DataGridChartModel
            {
                ItemsSource = items,
                CategoryPath = nameof(SampleRow.Category)
            };
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Profit",
                Formula = "Revenue-Cost",
                Kind = ChartSeriesKind.Line
            });

            var snapshot = model.BuildSnapshot(new ChartDataRequest());

            Assert.Equal(7d, snapshot.Series[0].Values[0]);
            Assert.Equal(12d, snapshot.Series[0].Values[1]);
        }

        [Fact]
        public void BuildSnapshot_Evaluates_Structured_Reference_Formulas()
        {
            var items = new[]
            {
                new SampleRow { Category = "A", Revenue = 2d },
                new SampleRow { Category = "B", Revenue = 4d }
            };

            var model = new DataGridChartModel
            {
                ItemsSource = items,
                CategoryPath = nameof(SampleRow.Category)
            };
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Double",
                Formula = "[@Revenue]*2",
                Kind = ChartSeriesKind.Line
            });

            var snapshot = model.BuildSnapshot(new ChartDataRequest());

            Assert.Equal(4d, snapshot.Series[0].Values[0]);
            Assert.Equal(8d, snapshot.Series[0].Values[1]);
        }

        [Fact]
        public void TryBuildUpdate_Tracks_Insert_Changes()
        {
            var items = new ObservableCollection<SampleRow>
            {
                new SampleRow { Category = "A", Revenue = 1d },
                new SampleRow { Category = "B", Revenue = 2d }
            };

            var model = new DataGridChartModel
            {
                ItemsSource = items,
                CategoryPath = nameof(SampleRow.Category)
            };
            model.Series.Add(new DataGridChartSeriesDefinition
            {
                Name = "Revenue",
                ValuePath = nameof(SampleRow.Revenue),
                Kind = ChartSeriesKind.Column
            });

            var request = new ChartDataRequest();
            var snapshot = model.BuildSnapshot(request);

            items.Add(new SampleRow { Category = "C", Revenue = 3d });

            var updated = model.TryBuildUpdate(request, snapshot, out var update);

            Assert.True(updated);
            Assert.Equal(ChartDataDeltaKind.Insert, update.Delta.Kind);
            Assert.Equal(2, update.Delta.Index);
            Assert.Equal(3, update.Snapshot.Categories.Count);
            Assert.Equal(3d, update.Snapshot.Series[0].Values[2]);
        }

        private sealed class SampleRow
        {
            public string Category { get; set; } = string.Empty;

            public double Revenue { get; set; }

            public double Cost { get; set; }
        }
    }
}
