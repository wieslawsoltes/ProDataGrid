// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Avalonia.Controls;
using Xunit;

namespace Avalonia.Controls.DataGridTests.ColumnDefinitions;

public sealed class DataGridGeneratedMetricsBridgeTests
{
    [Fact]
    public void Subscription_forwards_typed_meter_measurements_and_owns_sink_lifetime()
    {
        string prefix = $"prodatagrid.tests.{Guid.NewGuid():N}";
        var sink = new RecordingSink(prefix);
        using var meter = new Meter(DataGridGeneratedMetricsBridge.MeterName, "tests");
        Counter<long> realized = meter.CreateCounter<long>($"{prefix}.rows.realized", "{row}");
        Histogram<double> refresh = meter.CreateHistogram<double>($"{prefix}.rows.display.update", "ms");

        IDisposable subscription = DataGridGeneratedMetricsBridge.Subscribe(
            "tests/row/v1",
            DataGridGeneratedPerformanceProfile.HighFrequencyStreaming,
            sink);

        realized.Add(3, new KeyValuePair<string, object?>("source", "new"));
        refresh.Record(1.25);

        Assert.Equal(2, sink.Count);
        Assert.Equal(1, sink.TagCounts[0]);
        Assert.Equal(0, sink.TagCounts[1]);
        Assert.Equal("tests/row/v1", sink.Last.SchemaId);
        Assert.Equal(DataGridGeneratedPerformanceProfile.HighFrequencyStreaming, sink.Last.PerformanceProfile);
        Assert.Equal($"{prefix}.rows.display.update", sink.Last.Name);
        Assert.Equal(DataGridGeneratedMetricKind.Histogram, sink.Last.Kind);
        Assert.Equal(1.25, sink.Last.Value);
        Assert.False(sink.Last.IsInteger);
        Assert.True(sink.Last.Timestamp > 0);

        subscription.Dispose();
        Assert.True(sink.IsDisposed);

        realized.Add(1);
        Assert.Equal(2, sink.Count);
    }

    [Fact]
    public void Subscription_classifies_all_supported_synchronous_instruments_and_ignores_other_meters()
    {
        string prefix = $"prodatagrid.tests.{Guid.NewGuid():N}";
        var sink = new RecordingSink(prefix);
        using var meter = new Meter(DataGridGeneratedMetricsBridge.MeterName, "tests");
        using var unrelatedMeter = new Meter($"{DataGridGeneratedMetricsBridge.MeterName}.unrelated", "tests");
        Counter<double> counter = meter.CreateCounter<double>($"{prefix}.counter");
        UpDownCounter<long> integerUpDown = meter.CreateUpDownCounter<long>($"{prefix}.updown.long");
        UpDownCounter<double> doubleUpDown = meter.CreateUpDownCounter<double>($"{prefix}.updown.double");
        Histogram<long> integerHistogram = meter.CreateHistogram<long>($"{prefix}.histogram.long");
        Counter<long> unrelatedCounter = unrelatedMeter.CreateCounter<long>($"{prefix}.unrelated");

        IDisposable subscription = DataGridGeneratedMetricsBridge.Subscribe(
            "tests/instruments/v1",
            DataGridGeneratedPerformanceProfile.Balanced,
            sink);

        counter.Add(1.5);
        integerUpDown.Add(-2);
        doubleUpDown.Add(2.5);
        integerHistogram.Record(7);
        unrelatedCounter.Add(100);

        Assert.Collection(
            sink.Measurements,
            measurement =>
            {
                Assert.Equal(DataGridGeneratedMetricKind.Counter, measurement.Kind);
                Assert.Equal(1.5, measurement.Value);
                Assert.False(measurement.IsInteger);
            },
            measurement =>
            {
                Assert.Equal(DataGridGeneratedMetricKind.UpDownCounter, measurement.Kind);
                Assert.Equal(-2, measurement.Value);
                Assert.True(measurement.IsInteger);
            },
            measurement =>
            {
                Assert.Equal(DataGridGeneratedMetricKind.UpDownCounter, measurement.Kind);
                Assert.Equal(2.5, measurement.Value);
                Assert.False(measurement.IsInteger);
            },
            measurement =>
            {
                Assert.Equal(DataGridGeneratedMetricKind.Histogram, measurement.Kind);
                Assert.Equal(7, measurement.Value);
                Assert.True(measurement.IsInteger);
            });

        subscription.Dispose();
        subscription.Dispose();
        Assert.True(sink.IsDisposed);
    }

    [Fact]
    public void Subscription_rejects_missing_context()
    {
        var sink = new RecordingSink("prodatagrid.tests.missing-context");

        Assert.Throws<ArgumentNullException>(() => DataGridGeneratedMetricsBridge.Subscribe(
            null!,
            DataGridGeneratedPerformanceProfile.Balanced,
            sink));
        Assert.Throws<ArgumentNullException>(() => DataGridGeneratedMetricsBridge.Subscribe(
            "schema",
            DataGridGeneratedPerformanceProfile.Balanced,
            null!));

        sink.Dispose();
    }

    private sealed class RecordingSink : IDataGridGeneratedMetricsSink
    {
        private readonly string _instrumentPrefix;

        public RecordingSink(string instrumentPrefix)
        {
            _instrumentPrefix = instrumentPrefix;
        }

        public int Count { get; private set; }

        public DataGridGeneratedMetricMeasurement Last { get; private set; }

        public List<DataGridGeneratedMetricMeasurement> Measurements { get; } = new();

        public List<int> TagCounts { get; } = new();

        public bool IsDisposed { get; private set; }

        public void Record(
            in DataGridGeneratedMetricMeasurement measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (!measurement.Name.StartsWith(_instrumentPrefix, StringComparison.Ordinal))
            {
                return;
            }

            Count++;
            Last = measurement;
            Measurements.Add(measurement);
            TagCounts.Add(tags.Length);
        }

        public void Dispose() => IsDisposed = true;
    }
}
