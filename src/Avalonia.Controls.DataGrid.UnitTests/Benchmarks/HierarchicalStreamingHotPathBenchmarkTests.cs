using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.Controls.DataGridTests.Benchmarks;

public class HierarchicalStreamingHotPathBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public HierarchicalStreamingHotPathBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public void HierarchicalStreamingBenchmark_Reports_Frame_And_HotPath_Timings()
    {
        const int targetRootCount = 2000;
        const int childrenPerRoot = 3;
        const int batchSize = 20;
        const int intervalMs = 50;
        const int iterations = 12;
        const double defaultMaxAverageFrameMs = 2000;
        const double defaultMaxP95FrameMs = 4000;

        var maxAverageFrameMs = ResolveLatencyBudget("DATAGRID_HIER_STREAM_MAX_AVERAGE_MS", defaultMaxAverageFrameMs);
        var maxP95FrameMs = ResolveLatencyBudget("DATAGRID_HIER_STREAM_MAX_P95_MS", defaultMaxP95FrameMs);

        using var meter = new MetricCapture();
        var random = new Random(7);
        var nextId = 0;
        var roots = new ObservableRangeCollection<NodeItem>();
        var model = new HierarchicalModel<NodeItem>(new HierarchicalOptions<NodeItem>
        {
            ChildrenSelector = item => item.Children,
            IsLeafSelector = item => item.Children.Count == 0,
            IsExpandedSelector = item => item.IsExpanded,
            IsExpandedSetter = (item, value) => item.IsExpanded = value
        });

        model.SetRoots(roots);

        var window = new Window
        {
            Width = 1200,
            Height = 800
        };

        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HierarchicalRowsEnabled = true,
            HierarchicalModel = model,
            ItemsSource = model.ObservableFlattened,
            UseLogicalScrollable = true,
            Width = 1150,
            Height = 740,
            RowHeight = 24,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            IsReadOnly = true
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Item.Name")
        });

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Price",
            Binding = new Binding("Item.PriceDisplay")
        });

        window.Content = grid;

        var frameSamples = new List<double>(iterations);

        try
        {
            window.Show();

            roots.ResetWith(CreateRoots(targetRootCount, childrenPerRoot, random, ref nextId));
            PumpLayout(window, grid);
            _output.WriteLine(FormattableString.Invariant(
                $"Initial counts: model={model.Count}, slots={grid.SlotCount}, displayed={grid.DisplayData?.NumDisplayedScrollingElements ?? -1}"));

            // Warm-up delta so JIT/initial template costs are excluded from measured samples.
            ApplyDelta(roots, batchSize, targetRootCount, childrenPerRoot, random, ref nextId);
            PumpLayout(window, grid);
            _output.WriteLine(FormattableString.Invariant(
                $"Post warm-up counts: model={model.Count}, slots={grid.SlotCount}, displayed={grid.DisplayData?.NumDisplayedScrollingElements ?? -1}"));

            for (var i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                ApplyDelta(roots, batchSize, targetRootCount, childrenPerRoot, random, ref nextId);
                PumpLayout(window, grid);
                sw.Stop();
                frameSamples.Add(sw.Elapsed.TotalMilliseconds);

                Thread.Sleep(intervalMs);
            }

            _output.WriteLine(FormattableString.Invariant(
                $"Post run counts: model={model.Count}, slots={grid.SlotCount}, displayed={grid.DisplayData?.NumDisplayedScrollingElements ?? -1}"));
        }
        finally
        {
            window.Close();
        }

        var frameStats = CalculateStats(frameSamples);
        var flattenedApplyStats = CalculateStats(meter.GetDoubleMeasurements(DataGridDiagnostics.Meters.HierarchicalFlattenedApplyTimeName));
        var pseudoClassStats = CalculateStats(meter.GetDoubleMeasurements(DataGridDiagnostics.Meters.CellPseudoClassUpdateTimeName));
        var refreshStats = CalculateStats(meter.GetDoubleMeasurements(DataGridDiagnostics.Meters.HierarchicalRefreshTimeName));
        var gateCounterValues = meter.GetLongMeasurements(DataGridDiagnostics.Meters.HierarchicalFlattenedApplyGateCountName);
        var gateCounterTags = meter.GetLongMeasurementTags(DataGridDiagnostics.Meters.HierarchicalFlattenedApplyGateCountName);
        var gateSummary = SummarizeGateDecisions(gateCounterValues, gateCounterTags);

        _output.WriteLine(FormattableString.Invariant(
            $"Hierarchical streaming scenario: roots={targetRootCount}, children={childrenPerRoot}, batch={batchSize}, interval={intervalMs}ms, iterations={iterations}"));
        _output.WriteLine(FormattableString.Invariant(
            $"Frame time: avg={frameStats.Average:F2} ms p95={frameStats.P95:F2} ms max={frameStats.Max:F2} ms samples={frameStats.Count}"));
        _output.WriteLine(FormattableString.Invariant(
            $"ApplyHierarchicalFlattenedChanges: avg={flattenedApplyStats.Average:F3} ms p95={flattenedApplyStats.P95:F3} ms max={flattenedApplyStats.Max:F3} ms samples={flattenedApplyStats.Count}"));
        _output.WriteLine(FormattableString.Invariant(
            $"DataGridCell.UpdatePseudoClasses: avg={pseudoClassStats.Average:F3} ms p95={pseudoClassStats.P95:F3} ms max={pseudoClassStats.Max:F3} ms samples={pseudoClassStats.Count}"));
        _output.WriteLine(FormattableString.Invariant(
            $"HierarchicalModel.RefreshAsync: avg={refreshStats.Average:F3} ms p95={refreshStats.P95:F3} ms max={refreshStats.Max:F3} ms samples={refreshStats.Count}"));
        _output.WriteLine(FormattableString.Invariant(
            $"Hierarchical flattened apply gate: total={gateSummary.Total} allowed={gateSummary.Allowed} blocked={gateSummary.Blocked} samples={gateCounterValues.Count}"));
        foreach (var reason in gateSummary.ReasonCounts.OrderByDescending(x => x.Value))
        {
            _output.WriteLine(FormattableString.Invariant($"  gate-reason[{reason.Key}]={reason.Value}"));
        }
        _output.WriteLine(FormattableString.Invariant(
            $"Frame budgets: max-average={maxAverageFrameMs:F2} ms, max-p95={maxP95FrameMs:F2} ms"));

        Assert.Equal(iterations, frameStats.Count);
        Assert.True(pseudoClassStats.Count > 0, "Expected cell pseudo-class update measurements.");
        if (flattenedApplyStats.Count == 0 && gateSummary.Total > 0)
        {
            Assert.True(gateSummary.Blocked > 0, "Expected blocked flattened-apply gate decisions when apply path has no samples.");
        }
        Assert.InRange(frameStats.Average, 0.01, maxAverageFrameMs);
        Assert.InRange(frameStats.P95, 0.01, maxP95FrameMs);
    }

    private static void ApplyDelta(
        ObservableRangeCollection<NodeItem> roots,
        int batchSize,
        int targetRootCount,
        int childrenPerRoot,
        Random random,
        ref int nextId)
    {
        var removeCount = Math.Min(batchSize, roots.Count);
        if (removeCount > 0)
        {
            roots.RemoveRange(0, removeCount);
        }

        roots.AddRange(CreateRoots(batchSize, childrenPerRoot, random, ref nextId));
        if (roots.Count > targetRootCount)
        {
            roots.RemoveRange(0, roots.Count - targetRootCount);
        }
    }

    private static void PumpLayout(Window window, DataGrid grid)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        grid.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        grid.UpdateLayout();
    }

    private static List<NodeItem> CreateRoots(int count, int childrenPerRoot, Random random, ref int nextId)
    {
        var roots = new List<NodeItem>(count);
        for (var i = 0; i < count; i++)
        {
            var rootId = ++nextId;
            var root = CreateNode(rootId, $"Root {rootId}", isExpanded: true, random);
            for (var child = 0; child < childrenPerRoot; child++)
            {
                var childId = ++nextId;
                root.Children.Add(CreateNode(childId, $"Item {rootId}-{child + 1}", isExpanded: false, random));
            }

            roots.Add(root);
        }

        return roots;
    }

    private static NodeItem CreateNode(int id, string name, bool isExpanded, Random random)
    {
        var price = Math.Round(random.NextDouble() * 1000, 2);
        return new NodeItem
        {
            Id = id,
            Name = name,
            PriceDisplay = price.ToString("F2", CultureInfo.InvariantCulture),
            IsExpanded = isExpanded
        };
    }

    private static (int Count, double Average, double P95, double Max) CalculateStats(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var ordered = samples.ToArray();
        Array.Sort(ordered);
        var average = ordered.Average();
        var p95Index = (int)Math.Ceiling(ordered.Length * 0.95) - 1;
        p95Index = Math.Max(0, Math.Min(ordered.Length - 1, p95Index));
        return (ordered.Length, average, ordered[p95Index], ordered[^1]);
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

    private static (long Total, long Allowed, long Blocked, IReadOnlyDictionary<string, long> ReasonCounts) SummarizeGateDecisions(
        IReadOnlyList<long> values,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> tagsByMeasurement)
    {
        long total = 0;
        long allowed = 0;
        long blocked = 0;
        var reasonCounts = new Dictionary<string, long>(StringComparer.Ordinal);

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value <= 0)
            {
                continue;
            }

            total += value;
            var tags = index < tagsByMeasurement.Count ? tagsByMeasurement[index] : null;
            if (TryGetTagBool(tags, DataGridDiagnostics.Tags.CanApply, out var canApply) && canApply)
            {
                allowed += value;
            }
            else
            {
                blocked += value;
            }

            var reason = TryGetTagString(tags, DataGridDiagnostics.Tags.Reason, out var taggedReason)
                ? taggedReason
                : "unknown";
            if (!reasonCounts.TryGetValue(reason, out var count))
            {
                reasonCounts[reason] = value;
            }
            else
            {
                reasonCounts[reason] = count + value;
            }
        }

        return (total, allowed, blocked, reasonCounts);
    }

    private static bool TryGetTagBool(IReadOnlyDictionary<string, object?>? tags, string key, out bool value)
    {
        value = false;
        if (tags == null || !tags.TryGetValue(key, out var raw))
        {
            return false;
        }

        if (raw is bool typed)
        {
            value = typed;
            return true;
        }

        if (raw is string text && bool.TryParse(text, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetTagString(IReadOnlyDictionary<string, object?>? tags, string key, out string value)
    {
        value = string.Empty;
        if (tags == null || !tags.TryGetValue(key, out var raw))
        {
            return false;
        }

        if (raw is string text && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        return false;
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly Dictionary<string, List<double>> _measurements = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> _longMeasurements = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<IReadOnlyDictionary<string, object?>>> _longMeasurementTags = new(StringComparer.Ordinal);

        public MetricCapture()
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
                if (!_measurements.TryGetValue(instrument.Name, out var list))
                {
                    list = new List<double>();
                    _measurements[instrument.Name] = list;
                }

                list.Add(measurement);
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (!_longMeasurements.TryGetValue(instrument.Name, out var values))
                {
                    values = new List<long>();
                    _longMeasurements[instrument.Name] = values;
                }

                values.Add(measurement);

                if (!_longMeasurementTags.TryGetValue(instrument.Name, out var tagEntries))
                {
                    tagEntries = new List<IReadOnlyDictionary<string, object?>>();
                    _longMeasurementTags[instrument.Name] = tagEntries;
                }

                var entry = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var tag in tags)
                {
                    entry[tag.Key] = tag.Value;
                }

                tagEntries.Add(entry);
            });

            _listener.Start();
        }

        public IReadOnlyList<double> GetDoubleMeasurements(string instrumentName)
            => _measurements.TryGetValue(instrumentName, out var values)
                ? values
                : Array.Empty<double>();

        public IReadOnlyList<long> GetLongMeasurements(string instrumentName)
            => _longMeasurements.TryGetValue(instrumentName, out var values)
                ? values
                : Array.Empty<long>();

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> GetLongMeasurementTags(string instrumentName)
            => _longMeasurementTags.TryGetValue(instrumentName, out var values)
                ? values
                : Array.Empty<IReadOnlyDictionary<string, object?>>();

        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    private sealed class NodeItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string PriceDisplay { get; set; } = string.Empty;

        public bool IsExpanded { get; set; }

        public ObservableRangeCollection<NodeItem> Children { get; } = new();
    }
}
