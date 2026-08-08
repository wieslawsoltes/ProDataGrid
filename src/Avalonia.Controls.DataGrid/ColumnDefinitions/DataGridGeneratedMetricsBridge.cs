// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Avalonia.Controls
{
    /// <summary>Identifies the runtime instrument that produced a generated metric sample.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedMetricKind
    {
        /// <summary>The instrument kind is not recognized by the bridge.</summary>
        Unknown,
        /// <summary>A monotonic counter measurement.</summary>
        Counter,
        /// <summary>An up/down counter measurement.</summary>
        UpDownCounter,
        /// <summary>A histogram measurement.</summary>
        Histogram
    }

    /// <summary>Contains one allocation-free renderer metric measurement and generated schema context.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedMetricMeasurement
    {
        internal DataGridGeneratedMetricMeasurement(
            string schemaId,
            DataGridGeneratedPerformanceProfile performanceProfile,
            Instrument instrument,
            DataGridGeneratedMetricKind kind,
            double value,
            bool isInteger,
            long timestamp)
        {
            SchemaId = schemaId;
            PerformanceProfile = performanceProfile;
            Name = instrument.Name;
            Unit = instrument.Unit;
            Description = instrument.Description;
            Kind = kind;
            Value = value;
            IsInteger = isInteger;
            Timestamp = timestamp;
        }

        /// <summary>Gets the generated schema identifier associated with the subscribing view.</summary>
        public string SchemaId { get; }
        /// <summary>Gets the generated performance profile associated with the subscribing view.</summary>
        public DataGridGeneratedPerformanceProfile PerformanceProfile { get; }
        /// <summary>Gets the stable System.Diagnostics.Metrics instrument name.</summary>
        public string Name { get; }
        /// <summary>Gets the instrument unit.</summary>
        public string Unit { get; }
        /// <summary>Gets the instrument description.</summary>
        public string Description { get; }
        /// <summary>Gets the instrument kind.</summary>
        public DataGridGeneratedMetricKind Kind { get; }
        /// <summary>Gets the measurement converted to double.</summary>
        public double Value { get; }
        /// <summary>Gets whether the original measurement was an integer.</summary>
        public bool IsInteger { get; }
        /// <summary>Gets the Stopwatch timestamp captured by the bridge.</summary>
        public long Timestamp { get; }
    }

    /// <summary>Consumes typed ProDataGrid renderer metrics while a generated view is active.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedMetricsSink : IDisposable
    {
        /// <summary>Records one measurement and its allocation-free metric tags.</summary>
        void Record(
            in DataGridGeneratedMetricMeasurement measurement,
            ReadOnlySpan<KeyValuePair<string, object>> tags);
    }

    /// <summary>Bridges the existing ProDataGrid meter to a generated, replaceable typed sink.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    static class DataGridGeneratedMetricsBridge
    {
        /// <summary>Gets the meter name observed by generated metric subscriptions.</summary>
        public const string MeterName = "ProDataGrid.Diagnostic.Meter";

        /// <summary>Creates an activation-scoped subscription and transfers sink ownership to it.</summary>
        public static IDisposable Subscribe(
            string schemaId,
            DataGridGeneratedPerformanceProfile performanceProfile,
            IDataGridGeneratedMetricsSink sink)
        {
            ArgumentNullException.ThrowIfNull(schemaId);
            ArgumentNullException.ThrowIfNull(sink);
            return new Subscription(schemaId, performanceProfile, sink);
        }

        private sealed class Subscription : IDisposable
        {
            private readonly string _schemaId;
            private readonly DataGridGeneratedPerformanceProfile _performanceProfile;
            private MeterListener _listener;
            private IDataGridGeneratedMetricsSink _sink;

            public Subscription(
                string schemaId,
                DataGridGeneratedPerformanceProfile performanceProfile,
                IDataGridGeneratedMetricsSink sink)
            {
                _schemaId = schemaId;
                _performanceProfile = performanceProfile;
                _sink = sink;
                var listener = new MeterListener
                {
                    InstrumentPublished = static (instrument, candidate) =>
                    {
                        if (string.Equals(instrument.Meter.Name, MeterName, StringComparison.Ordinal))
                        {
                            candidate.EnableMeasurementEvents(instrument);
                        }
                    }
                };
                listener.SetMeasurementEventCallback<long>(OnLongMeasurement);
                listener.SetMeasurementEventCallback<double>(OnDoubleMeasurement);
                _listener = listener;
                try
                {
                    listener.Start();
                }
                catch
                {
                    _listener = null;
                    listener.Dispose();
                    _sink = null;
                    sink.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                MeterListener listener = Interlocked.Exchange(ref _listener, null);
                IDataGridGeneratedMetricsSink sink = Interlocked.Exchange(ref _sink, null);
                listener?.Dispose();
                sink?.Dispose();
            }

            private void OnLongMeasurement(
                Instrument instrument,
                long value,
                ReadOnlySpan<KeyValuePair<string, object>> tags,
                object state)
            {
                IDataGridGeneratedMetricsSink sink = Volatile.Read(ref _sink);
                if (sink == null)
                {
                    return;
                }

                var measurement = new DataGridGeneratedMetricMeasurement(
                    _schemaId,
                    _performanceProfile,
                    instrument,
                    GetKind(instrument),
                    value,
                    isInteger: true,
                    Stopwatch.GetTimestamp());
                sink.Record(in measurement, tags);
            }

            private void OnDoubleMeasurement(
                Instrument instrument,
                double value,
                ReadOnlySpan<KeyValuePair<string, object>> tags,
                object state)
            {
                IDataGridGeneratedMetricsSink sink = Volatile.Read(ref _sink);
                if (sink == null)
                {
                    return;
                }

                var measurement = new DataGridGeneratedMetricMeasurement(
                    _schemaId,
                    _performanceProfile,
                    instrument,
                    GetKind(instrument),
                    value,
                    isInteger: false,
                    Stopwatch.GetTimestamp());
                sink.Record(in measurement, tags);
            }

            private static DataGridGeneratedMetricKind GetKind(Instrument instrument)
            {
                if (instrument is Counter<long> || instrument is Counter<double>)
                {
                    return DataGridGeneratedMetricKind.Counter;
                }
                if (instrument is UpDownCounter<long> || instrument is UpDownCounter<double>)
                {
                    return DataGridGeneratedMetricKind.UpDownCounter;
                }
                if (instrument is Histogram<long> || instrument is Histogram<double>)
                {
                    return DataGridGeneratedMetricKind.Histogram;
                }
                return DataGridGeneratedMetricKind.Unknown;
            }
        }
    }
}
