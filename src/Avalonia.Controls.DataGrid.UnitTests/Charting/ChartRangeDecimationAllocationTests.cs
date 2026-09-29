// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class ChartRangeDecimationAllocationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Sparse_Input_Does_Not_Allocate_The_Whole_Requested_Budget(bool oneFinitePoint)
        {
            double?[] low = new double?[100000], high = new double?[100000];
            if (oneFinitePoint) { low[50000] = 1; high[50000] = 2; }
            for (int i = 0; i < 20; i++) ChartRangeDecimator.SelectIndices(low, high, 50000);
            long before = GC.GetAllocatedBytesForCurrentThread();
            int[] indices = ChartRangeDecimator.SelectIndices(low, high, 50000);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.InRange(allocated, 0, 1024);
            Assert.Equal(oneFinitePoint ? new[] { 0, 50000, 50001 } : new[] { 0 }, indices);
        }
    }
}
