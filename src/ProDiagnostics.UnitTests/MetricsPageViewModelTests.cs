using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

[Collection("MetricsCapture")]
public class MetricsPageViewModelTests
{
    [AvaloniaFact]
    public void AddMeasurement_Aggregates_Series_And_Updates_Statistics()
    {
        using var viewModel = new MetricsPageViewModel(subscribeToLiveMetrics: false);

        viewModel.AddMeasurement(CreateMeasurement("requests.total", 2));
        viewModel.AddMeasurement(CreateMeasurement("requests.total", 6));
        Dispatcher.UIThread.RunJobs();

        var metric = Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());
        Assert.Equal("requests.total", metric.InstrumentName);
        Assert.Equal(6, metric.LastValue);
        Assert.Equal(2, metric.MinValue);
        Assert.Equal(6, metric.MaxValue);
        Assert.Equal(4, metric.AverageValue);
        Assert.Equal(2, metric.SampleCount);
        Assert.Equal("Up", metric.Trend);
        Assert.Equal(2, viewModel.TotalMeasurements);
    }

    [AvaloniaFact]
    public void LiveCapture_WhenEnabled_Collects_InProcess_Metrics()
    {
        var meterName = "ProDiagnostics.UnitTests.LiveCapture." + Guid.NewGuid().ToString("N");
        var instrumentName = "counter." + Guid.NewGuid().ToString("N");
        using var viewModel = new MetricsPageViewModel(
            subscribeToLiveMetrics: true,
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: Environment.ProcessId,
            maxPendingMeasurements: MetricsPageViewModel.DefaultPendingMeasurementQueueCapacity,
            maxRemoteSessions: MetricsPageViewModel.DefaultRemoteSessionCapacity);

        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>(instrumentName);
        counter.Add(3);

        WaitFor(
            () => viewModel.SeriesView.Cast<MetricSeriesViewModel>().Any(series => series.InstrumentName == instrumentName),
            TimeSpan.FromSeconds(2));

        var metric = viewModel.SeriesView.Cast<MetricSeriesViewModel>().First(series => series.InstrumentName == instrumentName);
        Assert.Equal(instrumentName, metric.InstrumentName);
        Assert.True(metric.SampleCount >= 1);
        Assert.Equal(3, metric.LastValue, 6);
    }

    [AvaloniaFact]
    public void LiveCapture_WhenDisabled_DoesNotCollect_InProcess_Metrics()
    {
        var meterName = "ProDiagnostics.UnitTests.LiveCaptureDisabled." + Guid.NewGuid().ToString("N");
        var instrumentName = "counter." + Guid.NewGuid().ToString("N");
        using var viewModel = new MetricsPageViewModel(
            subscribeToLiveMetrics: false,
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: Environment.ProcessId,
            maxPendingMeasurements: MetricsPageViewModel.DefaultPendingMeasurementQueueCapacity,
            maxRemoteSessions: MetricsPageViewModel.DefaultRemoteSessionCapacity);

        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>(instrumentName);
        counter.Add(5);

        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(50);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(
            viewModel.SeriesView.Cast<MetricSeriesViewModel>(),
            series => series.InstrumentName == instrumentName);
    }

    [AvaloniaFact]
    public void Category_Filter_Hides_Histograms_When_Disabled()
    {
        using var viewModel = new MetricsPageViewModel(subscribeToLiveMetrics: false);

        viewModel.AddMeasurement(CreateMeasurement("requests.total", 1, instrumentType: "Counter`1"));
        viewModel.AddMeasurement(CreateMeasurement("request.duration", 10, instrumentType: "Histogram`1"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(2, viewModel.SeriesCount);

        viewModel.ShowHistograms = false;

        var visible = viewModel.SeriesView.Cast<MetricSeriesViewModel>().ToArray();
        Assert.Single(visible);
        Assert.Equal("requests.total", visible[0].InstrumentName);
    }

    [AvaloniaFact]
    public void MaxSeries_Trims_Oldest_Series()
    {
        using var viewModel = new MetricsPageViewModel(subscribeToLiveMetrics: false)
        {
            MaxSeries = 2
        };

        viewModel.AddMeasurement(CreateMeasurement("series.a", 1));
        viewModel.AddMeasurement(CreateMeasurement("series.b", 2));
        viewModel.AddMeasurement(CreateMeasurement("series.c", 3));
        Dispatcher.UIThread.RunJobs();

        var metrics = viewModel.SeriesView.Cast<MetricSeriesViewModel>().ToArray();
        Assert.Equal(2, metrics.Length);
        Assert.DoesNotContain(metrics, x => x.InstrumentName == "series.a");
        Assert.Contains(metrics, x => x.InstrumentName == "series.b");
        Assert.Contains(metrics, x => x.InstrumentName == "series.c");
    }

    [AvaloniaFact]
    public void MetricCaptureService_Subscriber_Exception_Does_Not_Block_Other_Subscribers()
    {
        var received = false;
        using var failing = MetricCaptureService.Subscribe(_ => throw new InvalidOperationException("boom"));
        using var succeeding = MetricCaptureService.Subscribe(_ => received = true);
        using var meter = new Meter("ProDiagnostics.UnitTests.MetricsIsolation." + Guid.NewGuid().ToString("N"));
        var counter = meter.CreateCounter<int>("isolation.counter");

        var ex = Record.Exception(() => counter.Add(1));
        Assert.Null(ex);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!received && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.True(received);
    }

    [AvaloniaFact]
    public void Search_Selection_Remove_And_ClearSelectionOrFilter_Work()
    {
        using var viewModel = new MetricsPageViewModel(subscribeToLiveMetrics: false);
        viewModel.AddMeasurement(CreateMeasurement("series.alpha", 1));
        viewModel.AddMeasurement(CreateMeasurement("series.beta", 2));
        Dispatcher.UIThread.RunJobs();
        viewModel.MetricsFilter.FilterString = "series.";

        Assert.True(viewModel.SelectNextMatch());
        Assert.NotNull(viewModel.SelectedSeries);
        Assert.True(viewModel.RemoveSelectedRecord());
        Assert.Null(viewModel.SelectedSeries);
        Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());

        Assert.True(viewModel.ClearSelectionOrFilter());
        Assert.Equal(string.Empty, viewModel.MetricsFilter.FilterString);
    }

    [Fact]
    public void MetricCaptureService_SuppressCapture_Blocks_Reentrant_Publish()
    {
        var meterName = "ProDiagnostics.UnitTests.Suppression." + Guid.NewGuid().ToString("N");
        var observed = 0;
        using var subscription = MetricCaptureService.Subscribe(m =>
        {
            if (string.Equals(m.MeterName, meterName, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref observed);
            }
        });
        using var meter = new Meter(meterName);
        var histogram = meter.CreateHistogram<double>("suppression.histogram");

        using (MetricCaptureService.SuppressCapture())
        {
            histogram.Record(1);
        }

        histogram.Record(2);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (Volatile.Read(ref observed) < 1 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.Equal(1, Volatile.Read(ref observed));
    }

    [Fact]
    public void MetricCaptureService_Drops_Reentrant_Measurements_From_Subscriber()
    {
        var meterName = "ProDiagnostics.UnitTests.Reentrant." + Guid.NewGuid().ToString("N");
        var outerObserved = 0;
        var innerObserved = 0;

        using var meter = new Meter(meterName);
        var outer = meter.CreateHistogram<double>("outer");
        var inner = meter.CreateHistogram<double>("inner");

        using var subscription = MetricCaptureService.Subscribe(m =>
        {
            if (!string.Equals(m.MeterName, meterName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(m.InstrumentName, "outer", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref outerObserved);
                inner.Record(42);
                return;
            }

            if (string.Equals(m.InstrumentName, "inner", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref innerObserved);
            }
        });

        outer.Record(1);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (Volatile.Read(ref outerObserved) < 1 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Thread.Sleep(50);

        Assert.Equal(1, Volatile.Read(ref outerObserved));
        Assert.Equal(0, Volatile.Read(ref innerObserved));
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Drops_Local_Process_Session_Metrics()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new MetricsPageViewModel(startRemoteListener: false, remotePort: 54831, localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 1234,
            ProcessName: "DataGridSample",
            AppName: "DataGridSample",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryMetric(
            sessionId,
            now.AddMilliseconds(1),
            MeterName: "test.meter",
            InstrumentName: "requests.total",
            Description: string.Empty,
            Unit: string.Empty,
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(1),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, viewModel.SeriesCount);
        Assert.Equal(0, viewModel.RemotePacketCount);
        Assert.Equal(1, viewModel.DroppedLocalPacketCount);
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Drops_Metrics_For_Unknown_Session()
    {
        var now = DateTimeOffset.UtcNow;
        using var viewModel = new MetricsPageViewModel(startRemoteListener: false, remotePort: 54831, localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryMetric(
            Guid.NewGuid(),
            now,
            MeterName: "test.meter",
            InstrumentName: "requests.total",
            Description: string.Empty,
            Unit: string.Empty,
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(5),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, viewModel.SeriesCount);
        Assert.Equal(0, viewModel.RemotePacketCount);
        Assert.Equal(1, viewModel.DroppedUnknownSessionMetricCount);
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Drops_New_Sessions_When_Cap_Is_Reached()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        using var viewModel = new MetricsPageViewModel(
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: 1234,
            maxPendingMeasurements: 128,
            maxRemoteSessions: 1);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionA,
            now,
            ProcessId: 5001,
            ProcessName: "RemoteA",
            AppName: "RemoteA",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionB,
            now.AddMilliseconds(1),
            ProcessId: 5002,
            ProcessName: "RemoteB",
            AppName: "RemoteB",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryMetric(
            sessionB,
            now.AddMilliseconds(2),
            MeterName: "test.meter",
            InstrumentName: "requests.total",
            Description: string.Empty,
            Unit: string.Empty,
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(1),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, viewModel.RemoteSessionCount);
        Assert.Equal(1, viewModel.DroppedSessionCount);
        Assert.Equal(1, viewModel.DroppedUnknownSessionMetricCount);
        Assert.Equal(0, viewModel.SeriesCount);
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Accepts_Remote_Process_Session_Metrics()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new MetricsPageViewModel(startRemoteListener: false, remotePort: 54831, localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 5678,
            ProcessName: "RemoteApp",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryMetric(
            sessionId,
            now.AddMilliseconds(1),
            MeterName: "test.meter",
            InstrumentName: "requests.total",
            Description: string.Empty,
            Unit: string.Empty,
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(5),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        var metric = Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());
        Assert.Equal("requests.total", metric.InstrumentName);
        Assert.Equal(1, viewModel.SeriesCount);
        Assert.Equal(1, viewModel.RemotePacketCount);
        Assert.Equal(0, viewModel.DroppedLocalPacketCount);
        Assert.Contains("process=RemoteApp(5678)", metric.TagsSummary);
    }

    [AvaloniaFact]
    public void RemoteListener_Accepts_Remote_Metrics_EndToEnd_Over_Udp()
    {
        var port = GetAvailableUdpPort();
        var sessionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        using var viewModel = new MetricsPageViewModel(
            startRemoteListener: true,
            remotePort: port,
            localProcessId: Environment.ProcessId);

        SendPacket(port, new TelemetryHello(
            sessionId,
            timestamp,
            ProcessId: Environment.ProcessId + 1000,
            ProcessName: "RemoteProcess",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        SendPacket(port, new TelemetryMetric(
            sessionId,
            timestamp.AddMilliseconds(1),
            MeterName: "remote.meter",
            InstrumentName: "remote.counter",
            Description: "remote metric",
            Unit: "count",
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromLong(7),
            Tags: Array.Empty<TelemetryTag>()));

        WaitFor(() => viewModel.SeriesCount == 1 && viewModel.RemotePacketCount == 1, TimeSpan.FromSeconds(2));

        var metric = Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());
        Assert.Equal("remote.counter", metric.InstrumentName);
        Assert.Equal(1, viewModel.RemoteSessionCount);
        Assert.Equal(1, viewModel.RemotePacketCount);
        Assert.Equal(0, viewModel.DroppedLocalPacketCount);
    }

    [AvaloniaFact]
    public void RemoteListener_Drops_Local_Process_Metrics_EndToEnd_Over_Udp()
    {
        var port = GetAvailableUdpPort();
        var sessionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        using var viewModel = new MetricsPageViewModel(
            startRemoteListener: true,
            remotePort: port,
            localProcessId: Environment.ProcessId);

        SendPacket(port, new TelemetryHello(
            sessionId,
            timestamp,
            ProcessId: Environment.ProcessId,
            ProcessName: "LocalProcess",
            AppName: "LocalApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        SendPacket(port, new TelemetryMetric(
            sessionId,
            timestamp.AddMilliseconds(1),
            MeterName: "local.meter",
            InstrumentName: "local.counter",
            Description: "local metric",
            Unit: "count",
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromLong(3),
            Tags: Array.Empty<TelemetryTag>()));

        WaitFor(() => viewModel.DroppedLocalPacketCount == 1, TimeSpan.FromSeconds(2));

        Assert.Equal(0, viewModel.SeriesCount);
        Assert.Equal(0, viewModel.RemotePacketCount);
        Assert.Equal(1, viewModel.DroppedLocalPacketCount);
    }

    [AvaloniaFact]
    public void AddMeasurement_Drops_Oldest_When_Pending_Queue_Capacity_Is_Exceeded()
    {
        using var viewModel = new MetricsPageViewModel(
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: 1234,
            maxPendingMeasurements: 4,
            maxRemoteSessions: 8);

        for (var i = 0; i < 7; i++)
        {
            viewModel.AddMeasurement(CreateMeasurement("queue.metric", i));
        }

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        var metric = Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());
        Assert.Equal(4, metric.SampleCount);
        Assert.Equal(6, metric.LastValue, 3);
        Assert.Equal(4, viewModel.TotalMeasurements);
        Assert.Equal(3, viewModel.DroppedQueueMeasurementCount);
    }

    [AvaloniaFact]
    public void PauseOrResumeUpdates_Pauses_And_Resumes_Flush()
    {
        using var viewModel = new MetricsPageViewModel(subscribeToLiveMetrics: false);

        viewModel.PauseOrResumeUpdates();
        Assert.True(viewModel.IsUpdatesPaused);
        Assert.Equal("Resume", viewModel.ToggleUpdatesText);

        viewModel.AddMeasurement(CreateMeasurement("paused.metric", 1));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, viewModel.SeriesCount);
        Assert.Equal(0, viewModel.TotalMeasurements);

        viewModel.PauseOrResumeUpdates();
        Assert.False(viewModel.IsUpdatesPaused);
        Assert.Equal("Pause", viewModel.ToggleUpdatesText);
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        var metric = Assert.Single(viewModel.SeriesView.Cast<MetricSeriesViewModel>());
        Assert.Equal("paused.metric", metric.InstrumentName);
        Assert.Equal(1, viewModel.TotalMeasurements);
    }

    [AvaloniaFact]
    public void Clear_Resets_Remote_Session_Tracking_And_Counters()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new MetricsPageViewModel(startRemoteListener: false, remotePort: 54831, localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 5678,
            ProcessName: "RemoteApp",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryMetric(
            sessionId,
            now.AddMilliseconds(1),
            MeterName: "test.meter",
            InstrumentName: "requests.total",
            Description: string.Empty,
            Unit: string.Empty,
            InstrumentType: "Counter`1",
            Value: TelemetryMetricValue.FromDouble(5),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, viewModel.RemoteSessionCount);
        Assert.Equal(1, viewModel.RemotePacketCount);
        Assert.Equal(1, viewModel.SeriesCount);

        viewModel.Clear();

        Assert.Equal(0, viewModel.RemoteSessionCount);
        Assert.Equal(0, viewModel.RemotePacketCount);
        Assert.Equal(0, viewModel.DroppedLocalPacketCount);
        Assert.Equal(0, viewModel.DroppedUnknownSessionMetricCount);
        Assert.Equal(0, viewModel.DroppedQueueMeasurementCount);
        Assert.Equal(0, viewModel.DroppedSessionCount);
        Assert.Equal(0, viewModel.SeriesCount);
    }

    private static MetricCaptureService.MetricMeasurementEvent CreateMeasurement(
        string instrumentName,
        double value,
        string instrumentType = "Counter`1")
    {
        return new MetricCaptureService.MetricMeasurementEvent(
            DateTimeOffset.UtcNow,
            "unit.test.meter",
            instrumentName,
            Description: string.Empty,
            Unit: string.Empty,
            instrumentType,
            value,
            Array.Empty<MetricCaptureService.MetricTag>());
    }

    private static int GetAvailableUdpPort()
    {
        using var udp = new UdpClient(0);
        var endpoint = Assert.IsType<IPEndPoint>(udp.Client.LocalEndPoint);
        return endpoint.Port;
    }

    private static void SendPacket(int port, TelemetryPacket packet)
    {
        var writer = new TelemetryPacketWriter();
        var payload = packet switch
        {
            TelemetryHello hello => writer.Write(hello).ToArray(),
            TelemetryMetric metric => writer.Write(metric, 0).ToArray(),
            TelemetryActivity activity => writer.Write(activity, 0).ToArray(),
            _ => throw new NotSupportedException("Unsupported telemetry packet type: " + packet.GetType().Name)
        };
        using var sender = new UdpClient();
        sender.Send(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));
    }

    private static void WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
    }
}
