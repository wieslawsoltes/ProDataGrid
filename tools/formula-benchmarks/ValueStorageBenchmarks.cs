// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ProDataGrid.FormulaEngine;

internal static class ValueStorageBenchmarks
{
    private static readonly FormulaReference Reference = new FormulaReference(new FormulaReferenceAddress(
        FormulaReferenceMode.A1, 7, 3, true, true, new FormulaSheetReference(null, "Sheet1")));
    private static readonly FormulaValue ReadValue = FormulaValue.FromReference(Reference);
    private static FormulaValue _sink;

    public static void Run(StringBuilder output)
    {
        output.AppendLine("# formula_value_bytes=" + Unsafe.SizeOf<FormulaValue>().ToString(CultureInfo.InvariantCulture));
        output.AppendLine("# formula_reference_bytes=" + Unsafe.SizeOf<FormulaReference>().ToString(CultureInfo.InvariantCulture));
        Measure(output, "reference_factory_escaped", 100000, CreateReference, 7);
        Measure(output, "reference_payload_read", 100000, ReadReference, 7);
        Measure(output, "allocate_10000_cell_array", 20, AllocateArray, 10000);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double CreateReference()
    {
        // Make the produced reference escape so boxing cannot be optimized away.
        _sink = FormulaValue.FromReference(Reference);
        return _sink.AsReference().Start.Row;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double ReadReference() => ReadValue.AsReference().Start.Row;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double AllocateArray()
    {
        var array = new FormulaArray(10000, 1);
        var count = array.RowCount;
        GC.KeepAlive(array);
        return count;
    }

    private static void Measure(StringBuilder output, string name, int iterations, Func<double> operation, double expected)
    {
        for (var warm = 0; warm < 2; warm++)
            for (var i = 0; i < iterations; i++)
                if (operation() != expected) throw new InvalidOperationException(name + " correctness failure.");
        var times = new double[7];
        var allocations = new double[7];
        for (var sample = 0; sample < times.Length; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            double checksum = 0;
            for (var i = 0; i < iterations; i++) checksum += operation();
            var elapsed = Stopwatch.GetTimestamp() - start;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (checksum != iterations * expected) throw new InvalidOperationException(name + " checksum failure.");
            times[sample] = elapsed * 1000000d / Stopwatch.Frequency / iterations;
            allocations[sample] = allocated / (double)iterations;
            Write(output, name, sample.ToString(CultureInfo.InvariantCulture), iterations, times[sample], allocations[sample]);
        }
        Array.Sort(times);
        Array.Sort(allocations);
        Write(output, name, "median", iterations, times[3], allocations[3]);
    }

    private static void Write(StringBuilder output, string name, string sample, int iterations, double time, double bytes)
    {
        output.Append(name).Append(',').Append(sample).Append(',').Append(iterations.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(time.ToString("R", CultureInfo.InvariantCulture)).Append(',').AppendLine(bytes.ToString("R", CultureInfo.InvariantCulture));
    }
}
