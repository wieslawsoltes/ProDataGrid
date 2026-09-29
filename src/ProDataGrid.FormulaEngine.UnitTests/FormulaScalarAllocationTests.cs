// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace ProDataGrid.FormulaEngine.Tests
{
    [CollectionDefinition("Formula allocation measurements", DisableParallelization = true)]
    public sealed class FormulaAllocationCollection { }

    // Runtime allocation counters include all managed work on the calling thread. Keep the
    // measured thread out of the runner's parallel test dispatch and synchronization context.
    [Collection("Formula allocation measurements")]
    public sealed class FormulaScalarAllocationTests
    {
        [Fact]
        public void Primitive_Scalar_Calls_Are_Allocation_Free_After_Warmup()
        {
            var measured = new long[7];
            var checksums = new double[7];
            long allocatingControl = 0;
            Exception? failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    var evaluation = ExcelScientificFunctionTests.Context();
                    if (!evaluation.FunctionRegistry.TryGetFunction("SIN", out var function) || function is ILazyFormulaFunction)
                        throw new InvalidOperationException("SIN must expose the eager scalar path.");
                    var arguments = new[] { FormulaValue.FromNumber(0.5) };
                    var context = new FormulaFunctionContext(evaluation);
                    for (var i = 0; i < 3; i++) Measure(function, context, arguments, out _);
                    for (var sample = 0; sample < measured.Length; sample++)
                        measured[sample] = Measure(function, context, arguments, out checksums[sample]);

                    // Calibrate the probe against a real escaped managed allocation per call.
                    // A probe that accidentally elides the calls must not satisfy this test.
                    var allocating = new AllocatingControl(function);
                    Measure(allocating, context, arguments, out _);
                    allocatingControl = Measure(allocating, context, arguments, out var controlSum);
                    if (Math.Abs(controlSum - 10000 * Math.Sin(0.5)) > 1e-8)
                        throw new InvalidOperationException("Allocation control returned an invalid checksum.");
                    GC.KeepAlive(allocating);
                }
                catch (Exception error) { failure = error; }
            }) { IsBackground = true };
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(30)), "Allocation measurement worker did not complete.");
            Assert.Null(failure);
            for (var sample = 0; sample < measured.Length; sample++)
            {
                ExcelScientificFunctionTests.Close(10000 * Math.Sin(0.5), checksums[sample]);
                Assert.True(measured[sample] < 4096,
                    $"Primitive SIN allocation samples (10,000 calls each): {string.Join(", ", measured)} bytes");
            }
            Assert.True(allocatingControl >= 160000,
                $"Positive control failed to detect escaped per-call allocations: {allocatingControl} bytes");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long Measure(IFormulaFunction function, FormulaFunctionContext context,
            FormulaValue[] arguments, out double sum)
        {
            var start = GC.GetAllocatedBytesForCurrentThread();
            sum = 0;
            for (var i = 0; i < 10000; i++) sum += function.Invoke(context, arguments).AsNumber();
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }

        private sealed class AllocatingControl : IFormulaFunction
        {
            private readonly IFormulaFunction _inner;
            private volatile byte[]? _last;
            public AllocatingControl(IFormulaFunction inner) { _inner = inner; }
            public string Name => "ALLOCATING_CONTROL";
            public FormulaFunctionInfo Info => _inner.Info;
            public FormulaValue Invoke(FormulaFunctionContext context, IReadOnlyList<FormulaValue> args)
            {
                _last = new byte[1];
                return _inner.Invoke(context, args);
            }
        }
    }
}
