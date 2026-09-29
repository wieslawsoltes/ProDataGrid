// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    internal static class FormulaAllocationMeasurements
    {
        // Measure only the supplied synchronous operation on a dedicated thread. No xUnit
        // assertions, task scheduling, result formatting or sample storage occur between
        // the allocation counter reads. Every retained sample is asserted by the caller;
        // this helper neither chooses a minimum nor retries a failed sample.
        public static Task<Sample[]> RunAsync(Func<double> operation, int iterations)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (iterations < 1) throw new ArgumentOutOfRangeException(nameof(iterations));
            var completion = new TaskCompletionSource<Sample[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = new Thread(() =>
            {
                try
                {
                    var samples = new Sample[7];
                    for (var warm = 0; warm < 3; warm++) Measure(operation, iterations);
                    for (var index = 0; index < samples.Length; index++) samples[index] = Measure(operation, iterations);
                    completion.SetResult(samples);
                }
                catch (Exception error)
                {
                    completion.SetException(error);
                }
            }) { IsBackground = true, Name = "Formula allocation measurement" };
            worker.Start();
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Sample Measure(Func<double> operation, int iterations)
        {
            double checksum = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++) checksum += operation();
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            return new Sample(bytes, checksum);
        }

        internal readonly struct Sample
        {
            public Sample(long bytes, double checksum) { Bytes = bytes; Checksum = checksum; }
            public long Bytes { get; }
            public double Checksum { get; }
        }
    }

    public sealed class FormulaAllocationMeasurementTests
    {
        [Fact]
        public async Task Isolated_Measurement_Still_Detects_Genuine_Per_Call_Allocations()
        {
            foreach (var sample in await FormulaAllocationMeasurements.RunAsync(static () => 1, 500))
            {
                Assert.Equal(500, sample.Checksum);
                Assert.True(sample.Bytes < 4096, $"Empty operation allocated {sample.Bytes} bytes.");
            }
            foreach (var sample in await FormulaAllocationMeasurements.RunAsync(Allocate, 500))
            {
                Assert.Equal(500, sample.Checksum);
                Assert.True(sample.Bytes >= 64000, $"Allocation positive control measured only {sample.Bytes} bytes.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double Allocate()
        {
            var data = new byte[128];
            GC.KeepAlive(data);
            return 1;
        }
    }
}
