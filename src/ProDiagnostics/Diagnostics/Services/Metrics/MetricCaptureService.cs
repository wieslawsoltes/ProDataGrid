using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Avalonia.Diagnostics.Services;

internal static class MetricCaptureService
{
    // Avoid feedback loops by excluding internal remote diagnostics runtime meters.
    private const string RemoteDiagnosticsMeterPrefix = "Avalonia.Diagnostics.Remote.";
    private static readonly object s_gate = new();
    private static readonly List<Action<MetricMeasurementEvent>> s_subscribers = new();
    private static readonly AsyncLocal<int> s_suppressCaptureDepth = new();
    [ThreadStatic]
    private static int t_publishDepth;
    private static MeterListener? s_meterListener;
    private static Timer? s_observablePollingTimer;

    public static IDisposable Subscribe(Action<MetricMeasurementEvent> onMeasurement)
    {
        if (onMeasurement is null)
        {
            throw new ArgumentNullException(nameof(onMeasurement));
        }

        lock (s_gate)
        {
            EnsureListenerStarted();
            s_subscribers.Add(onMeasurement);
        }

        return new Subscription(onMeasurement);
    }

    public static void RequestObservableSnapshot()
    {
        MeterListener? listener;
        lock (s_gate)
        {
            listener = s_meterListener;
        }

        if (listener is null)
        {
            return;
        }

        TryRecordObservableInstruments(listener);
    }

    internal static IDisposable SuppressCapture()
    {
        s_suppressCaptureDepth.Value = s_suppressCaptureDepth.Value + 1;
        return new CaptureSuppressionScope();
    }

    private static void EnsureListenerStarted()
    {
        if (s_meterListener is not null)
        {
            return;
        }

        var listener = new MeterListener
        {
            InstrumentPublished = OnInstrumentPublished
        };
        listener.SetMeasurementEventCallback<byte>(OnMeasurement);
        listener.SetMeasurementEventCallback<short>(OnMeasurement);
        listener.SetMeasurementEventCallback<int>(OnMeasurement);
        listener.SetMeasurementEventCallback<long>(OnMeasurement);
        listener.SetMeasurementEventCallback<float>(OnMeasurement);
        listener.SetMeasurementEventCallback<double>(OnMeasurement);
        listener.SetMeasurementEventCallback<decimal>(OnMeasurement);
        listener.Start();

        s_meterListener = listener;
        s_observablePollingTimer = new Timer(
            static _ => PollObservableInstruments(),
            null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1));
    }

    private static void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (instrument.Meter.Name.StartsWith(RemoteDiagnosticsMeterPrefix, StringComparison.Ordinal))
        {
            return;
        }

        listener.EnableMeasurementEvents(instrument);
    }

    private static void PollObservableInstruments()
    {
        MeterListener? listener;
        lock (s_gate)
        {
            listener = s_meterListener;
        }

        if (listener is null)
        {
            return;
        }

        TryRecordObservableInstruments(listener);
    }

    private static void TryRecordObservableInstruments(MeterListener listener)
    {
        try
        {
            listener.RecordObservableInstruments();
        }
        catch (ObjectDisposedException)
        {
            // Can happen during teardown while the polling timer is still unwinding.
        }
    }

    private static void OnMeasurement(Instrument instrument, byte measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, short measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, int measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, float measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, measurement, tags);

    private static void OnMeasurement(Instrument instrument, decimal measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => PublishMeasurement(instrument, (double)measurement, tags);

    private static void PublishMeasurement(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (s_suppressCaptureDepth.Value > 0 || t_publishDepth > 0)
        {
            return;
        }

        var measurement = new MetricMeasurementEvent(
            DateTimeOffset.UtcNow,
            instrument.Meter.Name,
            instrument.Name,
            instrument.Description ?? string.Empty,
            instrument.Unit ?? string.Empty,
            instrument.GetType().Name,
            value,
            CaptureTags(tags));
        Publish(measurement);
    }

    private static IReadOnlyList<MetricTag> CaptureTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
        {
            return Array.Empty<MetricTag>();
        }

        var copied = new MetricTag[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            copied[i] = new MetricTag(tags[i].Key, tags[i].Value);
        }

        return copied;
    }

    private static void Publish(MetricMeasurementEvent measurement)
    {
        t_publishDepth++;
        try
        {
            Action<MetricMeasurementEvent>[] subscribers;
            lock (s_gate)
            {
                subscribers = s_subscribers.ToArray();
            }

            for (var i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    subscribers[i](measurement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("MetricCaptureService subscriber failed: " + ex);
                }
            }
        }
        finally
        {
            t_publishDepth--;
        }
    }

    private static void Unsubscribe(Action<MetricMeasurementEvent> callback)
    {
        lock (s_gate)
        {
            s_subscribers.Remove(callback);
            if (s_subscribers.Count != 0)
            {
                return;
            }

            s_observablePollingTimer?.Dispose();
            s_observablePollingTimer = null;

            s_meterListener?.Dispose();
            s_meterListener = null;
        }
    }

    internal readonly record struct MetricTag(string Key, object? Value);

    internal readonly record struct MetricMeasurementEvent(
        DateTimeOffset Timestamp,
        string MeterName,
        string InstrumentName,
        string Description,
        string Unit,
        string InstrumentType,
        double Value,
        IReadOnlyList<MetricTag> Tags);

    private sealed class Subscription : IDisposable
    {
        private Action<MetricMeasurementEvent>? _callback;

        public Subscription(Action<MetricMeasurementEvent> callback)
        {
            _callback = callback;
        }

        public void Dispose()
        {
            var callback = _callback;
            if (callback is null)
            {
                return;
            }

            _callback = null;
            Unsubscribe(callback);
        }
    }

    private sealed class CaptureSuppressionScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var depth = s_suppressCaptureDepth.Value;
            s_suppressCaptureDepth.Value = depth > 0 ? depth - 1 : 0;
        }
    }
}
