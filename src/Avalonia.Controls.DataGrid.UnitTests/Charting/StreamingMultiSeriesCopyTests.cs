// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class StreamingMultiSeriesCopyTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(18)]
        [InlineData(33)]
        [InlineData(70)]
        public void Contiguous_And_Wrapped_Unreduced_Windows_Preserve_Every_Cell_Label_Bit_And_Identity(int rows)
        {
            const int capacity = 17;
            StreamingMultiSeriesChartDataSource source = new(capacity, new[]
                { new StreamingChartSeries("A"), new StreamingChartSeries("B"), new StreamingChartSeries("C") });
            for (int row = 0; row < rows; row++)
                source.Append(row * 1.25 - 3, new double?[] { Value(row, 0), Value(row, 1), Value(row, 2) }, Label(row));
            int retained = Math.Min(rows, capacity), first = rows - retained;
            for (int requestedStart = -1; requestedStart <= retained + 1; requestedStart++)
            foreach (int? requestedCount in new int?[] { null, -1, 0, 1, 4, 17, 99 })
            {
                ChartDataRequest request = new()
                    { WindowStart = requestedStart, WindowCount = requestedCount, DownsampleMode = ChartDownsampleMode.None };
                StreamingMultiSeriesChartView view = source.BuildView(request);
                int start = Math.Clamp(requestedStart, 0, retained);
                int count = Math.Clamp(requestedCount ?? retained - start, 0, retained - start);
                Assert.Equal(start, view.WindowStart); Assert.Equal(count, view.WindowCount);
                Assert.Equal(count, view.SourceSampleIndices.Count); Assert.Equal(rows, view.TotalSamples);
                Assert.Equal(first, view.FirstRetainedSampleIndex); Assert.Equal(retained, view.RetainedCount);
                for (int i = 0; i < count; i++)
                {
                    int original = first + start + i;
                    Assert.Equal(original, view.SourceSampleIndices[i]);
                    Assert.Equal(original * 1.25 - 3, view.Snapshot.Series[0].XValues![i]);
                    Assert.Equal(Label(original), view.Snapshot.Categories[i]);
                    for (int s = 0; s < 3; s++)
                    {
                        double? expected = Value(original, s), actual = view.Snapshot.Series[s].Values[i];
                        Assert.Equal(expected, actual);
                        if (expected.HasValue)
                            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual!.Value));
                        Assert.Same(view.Snapshot.Series[0].XValues, view.Snapshot.Series[s].XValues);
                    }
                }
                request.DownsampleMode = ChartDownsampleMode.MinMax; request.MaxPoints = int.MaxValue;
                Assert.Same(view, source.BuildView(request));
            }
        }

        private static string? Label(int row) => row % 5 == 0 ? null : $"R{row}";
        private static double? Value(int row, int series) => series switch
        {
            0 => row * 2.0,
            1 => row % 3 == 0 ? null : row * 0.125,
            _ => -(double)row
        };
    }
}
