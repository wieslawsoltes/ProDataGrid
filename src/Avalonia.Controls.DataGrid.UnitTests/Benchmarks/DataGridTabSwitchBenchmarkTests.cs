using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public class DataGridTabSwitchBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public DataGridTabSwitchBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void TabSwitchBenchmark_Reports_AttachDetach_Latency()
    {
        const int rowCount = 400;
        const int iterations = 25;
        const double defaultMaxAverageMs = 1000;
        const double defaultMaxP95Ms = 2000;

        var maxAverageMs = ResolveLatencyBudget("DATAGRID_TAB_SWITCH_MAX_AVERAGE_MS", defaultMaxAverageMs);
        var maxP95Ms = ResolveLatencyBudget("DATAGRID_TAB_SWITCH_MAX_P95_MS", defaultMaxP95Ms);

        using var metrics = new MetricsCapture();

        var items = new ObservableCollection<BenchmarkItem>(
            Enumerable.Range(0, rowCount).Select(CreateItem));

        var grid = CreateGrid(items);
        var attachCount = 0;
        var detachCount = 0;
        grid.AttachedToVisualTree += (_, _) => attachCount++;
        grid.DetachedFromVisualTree += (_, _) => detachCount++;

        var placeholder = new Border
        {
            Width = 300,
            Height = 200
        };

        var window = new Window
        {
            Width = 900,
            Height = 650,
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);

        var toPlaceholderSamples = new double[iterations];
        var toGridSamples = new double[iterations];
        var combinedSamples = new double[iterations * 2];

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            // Warm-up.
            SwitchContent(window, placeholder);
            SwitchContent(window, grid);

            for (var i = 0; i < iterations; i++)
            {
                toPlaceholderSamples[i] = MeasureSwitch(window, placeholder);
                toGridSamples[i] = MeasureSwitch(window, grid);
                combinedSamples[(i * 2)] = toPlaceholderSamples[i];
                combinedSamples[(i * 2) + 1] = toGridSamples[i];
            }
        }
        finally
        {
            window.Close();
        }

        var switchAway = CalculateStats(toPlaceholderSamples);
        var switchBack = CalculateStats(toGridSamples);
        var overall = CalculateStats(combinedSamples);

        _output.WriteLine(FormattableString.Invariant(
            $"Tab switch benchmark: rows={rowCount}, iterations={iterations}, samples={combinedSamples.Length}; to-placeholder avg={switchAway.Average:F2} ms p95={switchAway.P95:F2} ms; to-grid avg={switchBack.Average:F2} ms p95={switchBack.P95:F2} ms; overall avg={overall.Average:F2} ms p95={overall.P95:F2} ms min={overall.Min:F2} ms max={overall.Max:F2} ms"));
        _output.WriteLine(FormattableString.Invariant(
            $"Tab switch lifecycle: attached={attachCount}, detached={detachCount}"));
        _output.WriteLine(metrics.CreateSummary());
        _output.WriteLine(FormattableString.Invariant(
            $"Tab switch budgets: max-average={maxAverageMs:F2} ms, max-p95={maxP95Ms:F2} ms"));

        var expectedAttachDetachCount = iterations + 2;
        Assert.Equal(expectedAttachDetachCount, attachCount);
        Assert.Equal(expectedAttachDetachCount, detachCount);
        Assert.InRange(overall.Average, 0.01, maxAverageMs);
        Assert.InRange(overall.P95, 0.01, maxP95Ms);
    }

    private static (double Average, double P95, double Min, double Max) CalculateStats(double[] samples)
    {
        Array.Sort(samples);
        var average = samples.Average();
        var min = samples[0];
        var max = samples[^1];
        var p95Index = (int)Math.Ceiling(samples.Length * 0.95) - 1;
        p95Index = Math.Max(0, Math.Min(samples.Length - 1, p95Index));
        var p95 = samples[p95Index];
        return (average, p95, min, max);
    }

    private static double ResolveLatencyBudget(string variableName, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static double MeasureSwitch(Window window, Control content)
    {
        var sw = Stopwatch.StartNew();
        SwitchContent(window, content);
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static void SwitchContent(Window window, Control content)
    {
        window.Content = content;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        content.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static DataGrid CreateGrid(ObservableCollection<BenchmarkItem> items)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Height = 520
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding(nameof(BenchmarkItem.Index)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C01", Binding = new Binding(nameof(BenchmarkItem.C01)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C02", Binding = new Binding(nameof(BenchmarkItem.C02)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C03", Binding = new Binding(nameof(BenchmarkItem.C03)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C04", Binding = new Binding(nameof(BenchmarkItem.C04)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C05", Binding = new Binding(nameof(BenchmarkItem.C05)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C06", Binding = new Binding(nameof(BenchmarkItem.C06)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C07", Binding = new Binding(nameof(BenchmarkItem.C07)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C08", Binding = new Binding(nameof(BenchmarkItem.C08)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C09", Binding = new Binding(nameof(BenchmarkItem.C09)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C10", Binding = new Binding(nameof(BenchmarkItem.C10)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C11", Binding = new Binding(nameof(BenchmarkItem.C11)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C12", Binding = new Binding(nameof(BenchmarkItem.C12)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C13", Binding = new Binding(nameof(BenchmarkItem.C13)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C14", Binding = new Binding(nameof(BenchmarkItem.C14)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C15", Binding = new Binding(nameof(BenchmarkItem.C15)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "C16", Binding = new Binding(nameof(BenchmarkItem.C16)) });

        return grid;
    }

    private static BenchmarkItem CreateItem(int index)
    {
        return new BenchmarkItem
        {
            Index = index,
            C01 = $"Name {index}",
            C02 = $"Bucket {index % 12}",
            C03 = $"Category {index % 4}",
            C04 = (index * 17 % 101).ToString(CultureInfo.InvariantCulture),
            C05 = (index * 19 % 211).ToString(CultureInfo.InvariantCulture),
            C06 = (index * 23 % 307).ToString(CultureInfo.InvariantCulture),
            C07 = (index * 29 % 401).ToString(CultureInfo.InvariantCulture),
            C08 = (index * 31 % 503).ToString(CultureInfo.InvariantCulture),
            C09 = $"{DateTime.Today.AddDays(index % 31):yyyy-MM-dd}",
            C10 = $"{DateTime.Today.AddMinutes(index % 120):HH:mm}",
            C11 = $"Flag {(index % 2 == 0 ? "A" : "B")}",
            C12 = $"State {(index % 3 == 0 ? "Open" : "Closed")}",
            C13 = (index * 7 % 97).ToString(CultureInfo.InvariantCulture),
            C14 = (index * 11 % 89).ToString(CultureInfo.InvariantCulture),
            C15 = (index * 13 % 79).ToString(CultureInfo.InvariantCulture),
            C16 = $"Item-{index:D4}"
        };
    }

    private sealed class BenchmarkItem
    {
        public int Index { get; set; }

        public string C01 { get; set; } = string.Empty;
        public string C02 { get; set; } = string.Empty;
        public string C03 { get; set; } = string.Empty;
        public string C04 { get; set; } = string.Empty;
        public string C05 { get; set; } = string.Empty;
        public string C06 { get; set; } = string.Empty;
        public string C07 { get; set; } = string.Empty;
        public string C08 { get; set; } = string.Empty;
        public string C09 { get; set; } = string.Empty;
        public string C10 { get; set; } = string.Empty;
        public string C11 { get; set; } = string.Empty;
        public string C12 { get; set; } = string.Empty;
        public string C13 { get; set; } = string.Empty;
        public string C14 { get; set; } = string.Empty;
        public string C15 { get; set; } = string.Empty;
        public string C16 { get; set; } = string.Empty;
    }

    private sealed class MetricsCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, (double Total, int Count)> _doubleStats = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _longTotals = new(StringComparer.Ordinal);

        public MetricsCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == DataGridDiagnostics.MeterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            {
                _doubleStats.TryGetValue(instrument.Name, out var stats);
                _doubleStats[instrument.Name] = (stats.Total + measurement, stats.Count + 1);
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                _longTotals.TryGetValue(instrument.Name, out var total);
                _longTotals[instrument.Name] = total + measurement;
            });

            _listener.Start();
        }

        public string CreateSummary()
        {
            var rowsRefresh = GetDoubleStats(DataGridDiagnostics.Meters.RowsRefreshTimeName);
            var rowsDisplay = GetDoubleStats(DataGridDiagnostics.Meters.RowsDisplayUpdateTimeName);
            var rowsDisplayScan = GetDoubleStats(DataGridDiagnostics.Meters.RowsDisplayScanTimeName);
            var rowsDisplayScanRealize = GetDoubleStats(DataGridDiagnostics.Meters.RowsDisplayScanRealizeTimeName);
            var rowsDisplayTrim = GetDoubleStats(DataGridDiagnostics.Meters.RowsDisplayTrimTimeName);
            var rowsMeasure = GetDoubleStats(DataGridDiagnostics.Meters.RowsMeasureTimeName);
            var rowsArrange = GetDoubleStats(DataGridDiagnostics.Meters.RowsArrangeTimeName);
            var rowGenerate = GetDoubleStats(DataGridDiagnostics.Meters.RowGenerateTimeName);
            var gridRefresh = GetDoubleStats(DataGridDiagnostics.Meters.DataGridRefreshTimeName);

            var rowsDisplayScanned = GetLongTotal(DataGridDiagnostics.Meters.RowsDisplayScannedCountName);
            var rowsDisplayRemoved = GetLongTotal(DataGridDiagnostics.Meters.RowsDisplayRemovedCountName);
            var rowsRealized = GetLongTotal(DataGridDiagnostics.Meters.RowsRealizedCountName);
            var rowsRecycled = GetLongTotal(DataGridDiagnostics.Meters.RowsRecycledCountName);
            var rowsPrepared = GetLongTotal(DataGridDiagnostics.Meters.RowsPreparedCountName);
            var rowsMeasured = GetLongTotal(DataGridDiagnostics.Meters.RowsMeasuredCountName);
            var rowsMeasureSkipped = GetLongTotal(DataGridDiagnostics.Meters.RowsMeasureSkippedCountName);
            var rowsArranged = GetLongTotal(DataGridDiagnostics.Meters.RowsArrangedCountName);
            var rowsArrangeSkipped = GetLongTotal(DataGridDiagnostics.Meters.RowsArrangeSkippedCountName);
            var rowsArrangeOffscreen = GetLongTotal(DataGridDiagnostics.Meters.RowsArrangeOffscreenCountName);
            var rowsDisplayReused = GetLongTotal(DataGridDiagnostics.Meters.RowsDisplayReusedCountName);
            var totalMeasureDecisions = rowsMeasured + rowsMeasureSkipped;
            var totalArrangeDecisions = rowsArranged + rowsArrangeSkipped;
            var measureWorkRate = totalMeasureDecisions > 0
                ? (double)rowsMeasured / totalMeasureDecisions * 100
                : 0;
            var arrangeWorkRate = totalArrangeDecisions > 0
                ? (double)rowsArranged / totalArrangeDecisions * 100
                : 0;
            var rowsDisplayScanTraverseTotal = Math.Max(0, rowsDisplayScan.Total - rowsDisplayScanRealize.Total);
            var rowsDisplayScanTraverseAverage = rowsDisplayScan.Count > 0
                ? rowsDisplayScanTraverseTotal / rowsDisplayScan.Count
                : 0;

            return FormattableString.Invariant(
                $"Tab switch diagnostics: grid-refresh total={gridRefresh.Total:F2} ms count={gridRefresh.Count} avg={gridRefresh.Average:F2} ms; rows-refresh total={rowsRefresh.Total:F2} ms count={rowsRefresh.Count} avg={rowsRefresh.Average:F2} ms; rows-display total={rowsDisplay.Total:F2} ms count={rowsDisplay.Count} avg={rowsDisplay.Average:F2} ms; rows-display-scan total={rowsDisplayScan.Total:F2} ms count={rowsDisplayScan.Count} avg={rowsDisplayScan.Average:F2} ms; rows-display-scan-realize total={rowsDisplayScanRealize.Total:F2} ms count={rowsDisplayScanRealize.Count} avg={rowsDisplayScanRealize.Average:F2} ms; rows-display-scan-traverse total={rowsDisplayScanTraverseTotal:F2} ms avg={rowsDisplayScanTraverseAverage:F2} ms; rows-display-trim total={rowsDisplayTrim.Total:F2} ms count={rowsDisplayTrim.Count} avg={rowsDisplayTrim.Average:F2} ms; rows-measure total={rowsMeasure.Total:F2} ms count={rowsMeasure.Count} avg={rowsMeasure.Average:F2} ms; rows-arrange total={rowsArrange.Total:F2} ms count={rowsArrange.Count} avg={rowsArrange.Average:F2} ms; row-generate total={rowGenerate.Total:F2} ms count={rowGenerate.Count} avg={rowGenerate.Average:F2} ms; rows-display-reused={rowsDisplayReused}; rows-display-scanned={rowsDisplayScanned}; rows-display-removed={rowsDisplayRemoved}; rows-measured={rowsMeasured}; rows-measure-skipped={rowsMeasureSkipped}; rows-arranged={rowsArranged}; rows-arrange-skipped={rowsArrangeSkipped}; rows-arrange-offscreen={rowsArrangeOffscreen}; rows-measure-work={measureWorkRate:F1}%; rows-arrange-work={arrangeWorkRate:F1}%; rows-realized={rowsRealized}; rows-recycled={rowsRecycled}; rows-prepared={rowsPrepared}");
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private (double Total, int Count, double Average) GetDoubleStats(string metricName)
        {
            return _doubleStats.TryGetValue(metricName, out var stats)
                ? (stats.Total, stats.Count, stats.Count > 0 ? stats.Total / stats.Count : 0)
                : (0, 0, 0);
        }

        private long GetLongTotal(string metricName)
        {
            return _longTotals.TryGetValue(metricName, out var total)
                ? total
                : 0;
        }
    }
}
