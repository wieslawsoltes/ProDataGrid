// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using ProCharts;
using ProDataGrid.Charting;

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}; processors: {Environment.ProcessorCount}");
Console.WriteLine("Window snapshot benchmark: 128 visible points, value/X/size channels, 20 requests per measured operation, 2 warmups, median of 7 runs.");
Console.WriteLine("Input construction and initial source-cache construction are excluded; snapshot construction and result checks are included.");
Console.WriteLine("scenario,median_ms,median_allocated_bytes");
foreach (int count in new[] { 50000, 200000 })
{
    Row[] rows = new Row[count];
    for (int i = 0; i < rows.Length; i++) rows[i] = new Row(i);
    using DataGridChartModel model = new()
    {
        AutoRefresh = false,
        UseIncrementalUpdates = true,
        ItemsSource = rows,
        CategorySelector = static item => ((Row)item).Label,
        DownsampleMode = ChartDownsampleMode.None
    };
    model.Series.Add(new DataGridChartSeriesDefinition
    {
        Name = "Values", Kind = ChartSeriesKind.Bubble,
        ValueSelector = static item => ((Row)item).Index * 2d,
        XValueSelector = static item => ((Row)item).Index * 0.25,
        SizeSelector = static item => ((Row)item).Index % 7 + 1
    });
    ChartDataSnapshot original = model.BuildSnapshot(new ChartDataRequest { DownsampleMode = ChartDownsampleMode.None });
    ChartDataRequest[] requests = new ChartDataRequest[20];
    for (int i = 0; i < requests.Length; i++)
        requests[i] = new ChartDataRequest
        {
            WindowStart = i * 997 % (count - 128), WindowCount = 128,
            DownsampleMode = ChartDownsampleMode.None
        };
    Measure($"window_128_from_{count}_20_reads", () =>
    {
        foreach (ChartDataRequest request in requests)
        {
            ChartDataSnapshot snapshot = model.BuildSnapshot(request);
            int start = request.WindowStart!.Value;
            ChartSeriesSnapshot series = snapshot.Series[0];
            if (snapshot.Categories.Count != 128 || series.Values.Count != 128 ||
                series.Values[0] != start * 2d || series.Values[^1] != (start + 127) * 2d ||
                series.XValues![0] != start * 0.25 || series.SizeValues![0] != start % 7 + 1 ||
                snapshot.Categories[0] != rows[start].Label)
                throw new InvalidOperationException("Visible window channels lost their original alignment.");
        }
    });
    if (original.Categories.Count != count || original.Series[0].Values[^1] != (count - 1) * 2d)
        throw new InvalidOperationException("Window reads changed an existing full snapshot.");
}
Console.WriteLine("These measurements exercise the actual DataGridChartModel snapshot path. They do not measure rendering, native GPU submission, or UI frame rate.");

static void Measure(string name, Action operation)
{
    for (int i = 0; i < 2; i++) operation();
    double[] times = new double[7];
    long[] bytes = new long[7];
    for (int i = 0; i < times.Length; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        operation();
        times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes[i] = GC.GetAllocatedBytesForCurrentThread() - allocated;
    }
    Array.Sort(times); Array.Sort(bytes);
    Console.WriteLine($"{name},{times[3].ToString("F3", CultureInfo.InvariantCulture)},{bytes[3]}");
}

internal sealed class Row
{
    public Row(int index) { Index = index; Label = index.ToString(CultureInfo.InvariantCulture); }
    public int Index { get; }
    public string Label { get; }
}
