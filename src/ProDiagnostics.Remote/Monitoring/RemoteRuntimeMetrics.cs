using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Threading;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Runtime metrics instrumentation for remote diagnostics protocol execution.
/// </summary>
public static class RemoteRuntimeMetrics
{
    private const string MeterName = "Avalonia.Diagnostics.Remote.Runtime";

    private static readonly Meter s_meter = new(MeterName);

    private static readonly Counter<long> s_requestCount =
        s_meter.CreateCounter<long>("remote.request.count");

    private static readonly Counter<long> s_streamPublishCount =
        s_meter.CreateCounter<long>("remote.stream.publish.count");

    private static readonly Counter<long> s_streamDroppedCount =
        s_meter.CreateCounter<long>("remote.stream.dropped.count");

    private static readonly Counter<long> s_transportFailureCount =
        s_meter.CreateCounter<long>("remote.transport.failure.count");

    private static readonly Counter<long> s_connectionAcceptedCount =
        s_meter.CreateCounter<long>("remote.connection.accepted.count");

    private static readonly Counter<long> s_connectionClosedCount =
        s_meter.CreateCounter<long>("remote.connection.closed.count");

    private static readonly Histogram<double> s_requestDuration =
        s_meter.CreateHistogram<double>("remote.request.duration", unit: "ms");

    private static readonly Histogram<double> s_snapshotDuration =
        s_meter.CreateHistogram<double>("remote.snapshot.duration", unit: "ms");

    private static readonly Histogram<double> s_uiThreadCaptureDuration =
        s_meter.CreateHistogram<double>("remote.ui_thread.capture.duration", unit: "ms");

    private static readonly Histogram<double> s_serializeDuration =
        s_meter.CreateHistogram<double>("remote.serialize.duration", unit: "ms");

    private static readonly Histogram<double> s_deserializeDuration =
        s_meter.CreateHistogram<double>("remote.deserialize.duration", unit: "ms");

    private static readonly Histogram<double> s_heartbeatDuration =
        s_meter.CreateHistogram<double>("remote.heartbeat.duration", unit: "ms");

    private static readonly Histogram<long> s_payloadInBytes =
        s_meter.CreateHistogram<long>("remote.payload.in.bytes", unit: "By");

    private static readonly Histogram<long> s_payloadOutBytes =
        s_meter.CreateHistogram<long>("remote.payload.out.bytes", unit: "By");

    private static readonly Histogram<long> s_snapshotPayloadBytes =
        s_meter.CreateHistogram<long>("remote.snapshot.payload.bytes", unit: "By");

    private static long s_activeConnectionsHttp;
    private static long s_activeConnectionsNamedPipe;
    private static long s_activeConnectionsInProc;
    private static long s_activeStreamSessions;
    private static long s_streamQueueDepthMax;
    private static long s_streamQueueDepthAverage;
    private static long s_snapshotCacheEntries;

    private static readonly KeyValuePair<string, object?>[] s_httpTransportTag =
        { new("transport", "http") };

    private static readonly KeyValuePair<string, object?>[] s_namedPipeTransportTag =
        { new("transport", "namedpipe") };

    private static readonly KeyValuePair<string, object?>[] s_inProcTransportTag =
        { new("transport", "inproc") };

    private static readonly HashSet<string> s_knownMethods = new(StringComparer.Ordinal)
    {
        "hello",
        "helloAck",
        "helloReject",
        "keepAlive",
        "disconnect",
        "request",
        "response",
        "stream",
        "error",
        RemoteReadOnlyMethods.PreviewCapabilitiesGet,
        RemoteReadOnlyMethods.PreviewSnapshotGet,
        RemoteReadOnlyMethods.TreeSnapshotGet,
        RemoteReadOnlyMethods.SelectionGet,
        RemoteReadOnlyMethods.PropertiesSnapshotGet,
        RemoteReadOnlyMethods.Elements3DSnapshotGet,
        RemoteReadOnlyMethods.OverlayOptionsGet,
        RemoteReadOnlyMethods.CodeDocumentsGet,
        RemoteReadOnlyMethods.CodeResolveNode,
        RemoteReadOnlyMethods.BindingsSnapshotGet,
        RemoteReadOnlyMethods.StylesSnapshotGet,
        RemoteReadOnlyMethods.ResourcesSnapshotGet,
        RemoteReadOnlyMethods.AssetsSnapshotGet,
        RemoteReadOnlyMethods.EventsSnapshotGet,
        RemoteReadOnlyMethods.BreakpointsSnapshotGet,
        RemoteReadOnlyMethods.LogsSnapshotGet,
        RemoteReadOnlyMethods.MetricsSnapshotGet,
        RemoteReadOnlyMethods.ProfilerSnapshotGet,
        RemoteMutationMethods.PreviewPausedSet,
        RemoteMutationMethods.PreviewSettingsSet,
        RemoteMutationMethods.PreviewInputInject,
        RemoteMutationMethods.InspectHovered,
        RemoteMutationMethods.SelectionSet,
        RemoteMutationMethods.PropertiesSet,
        RemoteMutationMethods.PseudoClassSet,
        RemoteMutationMethods.Elements3DRootSet,
        RemoteMutationMethods.Elements3DRootReset,
        RemoteMutationMethods.Elements3DFiltersSet,
        RemoteMutationMethods.OverlayOptionsSet,
        RemoteMutationMethods.OverlayLiveHoverSet,
        RemoteMutationMethods.CodeDocumentOpen,
        RemoteMutationMethods.BreakpointsPropertyAdd,
        RemoteMutationMethods.BreakpointsEventAdd,
        RemoteMutationMethods.BreakpointsRemove,
        RemoteMutationMethods.BreakpointsToggle,
        RemoteMutationMethods.BreakpointsClear,
        RemoteMutationMethods.BreakpointsEnabledSet,
        RemoteMutationMethods.EventsClear,
        RemoteMutationMethods.EventsNodeEnabledSet,
        RemoteMutationMethods.EventsDefaultsEnable,
        RemoteMutationMethods.EventsDisableAll,
        RemoteMutationMethods.LogsClear,
        RemoteMutationMethods.LogsLevelsSet,
        RemoteMutationMethods.StreamDemandSet,
        RemoteMutationMethods.MetricsPausedSet,
        RemoteMutationMethods.MetricsSettingsSet,
        RemoteMutationMethods.ProfilerPausedSet,
        RemoteMutationMethods.ProfilerSettingsSet,
    };

    static RemoteRuntimeMetrics()
    {
        _ = s_meter.CreateObservableGauge<long>(
            "remote.connection.active",
            ObserveConnectionActive);
        _ = s_meter.CreateObservableGauge<long>(
            "remote.stream.session.active",
            ObserveStreamSessionActive);
        _ = s_meter.CreateObservableGauge<long>(
            "remote.stream.queue.depth.max",
            ObserveStreamQueueDepthMax);
        _ = s_meter.CreateObservableGauge<long>(
            "remote.stream.queue.depth.avg",
            ObserveStreamQueueDepthAverage);
        _ = s_meter.CreateObservableGauge<long>(
            "remote.snapshot.cache.entries",
            ObserveSnapshotCacheEntries);
    }

    public static string RuntimeMeterName => MeterName;

    public static bool IsSnapshotPayloadMetricsEnabled => s_snapshotPayloadBytes.Enabled;

    public static string ResolveDomainFromMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return "none";
        }

        if (method.Contains(".tree.", StringComparison.Ordinal))
        {
            return "tree";
        }

        if (method.Contains(".properties.", StringComparison.Ordinal))
        {
            return "properties";
        }

        if (method.Contains(".elements3d.", StringComparison.Ordinal))
        {
            return "elements3d";
        }

        if (method.Contains(".styles.", StringComparison.Ordinal))
        {
            return "styles";
        }

        if (method.Contains(".resources.", StringComparison.Ordinal))
        {
            return "resources";
        }

        if (method.Contains(".assets.", StringComparison.Ordinal))
        {
            return "assets";
        }

        if (method.Contains(".events.", StringComparison.Ordinal))
        {
            return "events";
        }

        if (method.Contains(".breakpoints.", StringComparison.Ordinal))
        {
            return "breakpoints";
        }

        if (method.Contains(".logs.", StringComparison.Ordinal))
        {
            return "logs";
        }

        if (method.Contains(".stream.", StringComparison.Ordinal))
        {
            return "streaming";
        }

        if (method.Contains(".metrics.", StringComparison.Ordinal))
        {
            return "metrics";
        }

        if (method.Contains(".profiler.", StringComparison.Ordinal))
        {
            return "profiler";
        }

        if (method.Contains(".preview.", StringComparison.Ordinal))
        {
            return "preview";
        }

        if (method.Contains(".code.", StringComparison.Ordinal))
        {
            return "code";
        }

        if (method.Contains(".bindings.", StringComparison.Ordinal))
        {
            return "bindings";
        }

        if (method.Contains(".selection.", StringComparison.Ordinal))
        {
            return "tree";
        }

        if (method.Contains(".overlay.", StringComparison.Ordinal))
        {
            return "overlay";
        }

        return "none";
    }

    public static string MapMessageKind(RemoteMessageKind kind)
    {
        return kind switch
        {
            RemoteMessageKind.Hello => "hello",
            RemoteMessageKind.HelloAck => "helloAck",
            RemoteMessageKind.HelloReject => "helloReject",
            RemoteMessageKind.KeepAlive => "keepAlive",
            RemoteMessageKind.Disconnect => "disconnect",
            RemoteMessageKind.Request => "request",
            RemoteMessageKind.Response => "response",
            RemoteMessageKind.Stream => "stream",
            RemoteMessageKind.Error => "error",
            _ => "other",
        };
    }

    public static void RecordRequest(
        string transport,
        string method,
        string domain,
        string scope,
        string status,
        double durationMs,
        long payloadInBytes,
        long payloadOutBytes)
    {
        var tags = BuildCommonTags(transport, method, domain, scope, status, source: "none", cache: "bypass");
        s_requestCount.Add(1, tags);
        s_requestDuration.Record(durationMs, tags);
        if (payloadInBytes >= 0)
        {
            s_payloadInBytes.Record(payloadInBytes, tags);
        }

        if (payloadOutBytes >= 0)
        {
            s_payloadOutBytes.Record(payloadOutBytes, tags);
        }
    }

    public static void RecordSnapshotDuration(
        string domain,
        string scope,
        string status,
        double durationMs,
        string cache)
    {
        var tags = BuildCommonTags(
            transport: "inproc",
            method: "none",
            domain: domain,
            scope: scope,
            status: status,
            source: "none",
            cache: cache);
        s_snapshotDuration.Record(durationMs, tags);
    }

    public static void RecordUiThreadCaptureDuration(
        string domain,
        string scope,
        string status,
        double durationMs,
        string cache)
    {
        var tags = BuildCommonTags(
            transport: "inproc",
            method: "none",
            domain: domain,
            scope: scope,
            status: status,
            source: "none",
            cache: cache);
        s_uiThreadCaptureDuration.Record(durationMs, tags);
    }

    public static void RecordSnapshotPayloadBytes(
        string domain,
        string scope,
        long bytes,
        string cache)
    {
        if (!s_snapshotPayloadBytes.Enabled || bytes < 0)
        {
            return;
        }

        var tags = BuildCommonTags(
            transport: "inproc",
            method: "none",
            domain: domain,
            scope: scope,
            status: "ok",
            source: "none",
            cache: cache);
        s_snapshotPayloadBytes.Record(bytes, tags);
    }

    public static void RecordSerializeDuration(
        string transport,
        string method,
        string status,
        double durationMs)
    {
        var tags = BuildCommonTags(
            transport,
            method,
            domain: "none",
            scope: "none",
            status,
            source: "none",
            cache: "bypass");
        s_serializeDuration.Record(durationMs, tags);
    }

    public static void RecordDeserializeDuration(
        string transport,
        string method,
        string status,
        double durationMs)
    {
        var tags = BuildCommonTags(
            transport,
            method,
            domain: "none",
            scope: "none",
            status,
            source: "none",
            cache: "bypass");
        s_deserializeDuration.Record(durationMs, tags);
    }

    public static void RecordHeartbeatDuration(string transport, string status, double durationMs)
    {
        var tags = BuildCommonTags(
            transport,
            method: "keepAlive",
            domain: "none",
            scope: "none",
            status,
            source: "none",
            cache: "bypass");
        s_heartbeatDuration.Record(durationMs, tags);
    }

    public static void RecordPayloadInBytes(
        string transport,
        string method,
        string domain,
        string scope,
        string status,
        long bytes)
    {
        if (bytes < 0)
        {
            return;
        }

        var tags = BuildCommonTags(transport, method, domain, scope, status, source: "none", cache: "bypass");
        s_payloadInBytes.Record(bytes, tags);
    }

    public static void RecordPayloadOutBytes(
        string transport,
        string method,
        string domain,
        string scope,
        string status,
        long bytes)
    {
        if (bytes < 0)
        {
            return;
        }

        var tags = BuildCommonTags(transport, method, domain, scope, status, source: "none", cache: "bypass");
        s_payloadOutBytes.Record(bytes, tags);
    }

    public static void RecordStreamPublish(string domain, string source)
    {
        var tags = BuildCommonTags(
            transport: "inproc",
            method: "stream",
            domain,
            scope: "none",
            status: "ok",
            source,
            cache: "bypass");
        s_streamPublishCount.Add(1, tags);
    }

    public static void RecordStreamDropped(string domain, long droppedCount, string source)
    {
        if (droppedCount <= 0)
        {
            return;
        }

        var tags = BuildCommonTags(
            transport: "inproc",
            method: "stream",
            domain,
            scope: "none",
            status: "dropped",
            source,
            cache: "bypass");
        s_streamDroppedCount.Add(droppedCount, tags);
    }

    public static void RecordTransportFailure(string transport, string method, string domain, string status)
    {
        var tags = BuildCommonTags(
            transport,
            method,
            domain,
            scope: "none",
            status,
            source: "none",
            cache: "bypass");
        s_transportFailureCount.Add(1, tags);
    }

    public static void RecordConnectionAccepted(string transport)
    {
        var tags = BuildConnectionTags(transport);
        s_connectionAcceptedCount.Add(1, tags);
    }

    public static void RecordConnectionClosed(string transport)
    {
        var tags = BuildConnectionTags(transport);
        s_connectionClosedCount.Add(1, tags);
    }

    public static void SetActiveConnections(string transport, int count)
    {
        var normalized = NormalizeTransport(transport);
        var value = count < 0 ? 0 : count;
        switch (normalized)
        {
            case "http":
                Volatile.Write(ref s_activeConnectionsHttp, value);
                break;
            case "namedpipe":
                Volatile.Write(ref s_activeConnectionsNamedPipe, value);
                break;
            case "inproc":
                Volatile.Write(ref s_activeConnectionsInProc, value);
                break;
            default:
                Volatile.Write(ref s_activeConnectionsInProc, value);
                break;
        }
    }

    public static void SetActiveStreamSessions(int count)
    {
        Volatile.Write(ref s_activeStreamSessions, count < 0 ? 0 : count);
    }

    public static void SetStreamQueueDepth(long maxDepth, long averageDepth)
    {
        Volatile.Write(ref s_streamQueueDepthMax, maxDepth < 0 ? 0 : maxDepth);
        Volatile.Write(ref s_streamQueueDepthAverage, averageDepth < 0 ? 0 : averageDepth);
    }

    public static void SetSnapshotCacheEntries(int count)
    {
        Volatile.Write(ref s_snapshotCacheEntries, count < 0 ? 0 : count);
    }

    public static double ElapsedMilliseconds(long startTimestamp)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
        return elapsedTicks <= 0 ? 0 : elapsedTicks * 1000d / Stopwatch.Frequency;
    }

    public static int GetUtf8ByteCount(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? 0
            : Encoding.UTF8.GetByteCount(value);
    }

    private static TagList BuildConnectionTags(string transport)
    {
        TagList tags = default;
        tags.Add("transport", NormalizeTransport(transport));
        return tags;
    }

    private static TagList BuildCommonTags(
        string transport,
        string method,
        string domain,
        string scope,
        string status,
        string source,
        string cache)
    {
        TagList tags = default;
        tags.Add("transport", NormalizeTransport(transport));
        tags.Add("method", NormalizeMethod(method));
        tags.Add("domain", NormalizeDomain(domain));
        tags.Add("scope", NormalizeScope(scope));
        tags.Add("status", NormalizeStatus(status));
        tags.Add("source", NormalizeSource(source));
        tags.Add("cache", NormalizeCache(cache));
        return tags;
    }

    private static string NormalizeTransport(string? transport)
    {
        if (string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase))
        {
            return "http";
        }

        if (string.Equals(transport, "namedpipe", StringComparison.OrdinalIgnoreCase))
        {
            return "namedpipe";
        }

        if (string.Equals(transport, "inproc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(transport, "stream", StringComparison.OrdinalIgnoreCase))
        {
            return "inproc";
        }

        return "other";
    }

    private static string NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return "none";
        }

        if (s_knownMethods.Contains(method))
        {
            return method;
        }

        if (method.StartsWith("diagnostics.", StringComparison.Ordinal))
        {
            return "diagnostics.other";
        }

        return "other";
    }

    private static string NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return "none";
        }

        return domain switch
        {
            "tree" => "tree",
            "properties" => "properties",
            "elements3d" => "elements3d",
            "styles" => "styles",
            "resources" => "resources",
            "assets" => "assets",
            "events" => "events",
            "breakpoints" => "breakpoints",
            "logs" => "logs",
            "metrics" => "metrics",
            "profiler" => "profiler",
            "preview" => "preview",
            "code" => "code",
            "bindings" => "bindings",
            "overlay" => "overlay",
            "none" => "none",
            _ => "other",
        };
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.Equals(scope, "combined", StringComparison.OrdinalIgnoreCase))
        {
            return "combined";
        }

        if (string.Equals(scope, "visual", StringComparison.OrdinalIgnoreCase))
        {
            return "visual";
        }

        if (string.Equals(scope, "logical", StringComparison.OrdinalIgnoreCase))
        {
            return "logical";
        }

        return "none";
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "ok";
        }

        return status switch
        {
            "ok" => "ok",
            "error" => "error",
            "timeout" => "timeout",
            "cancelled" => "cancelled",
            "dropped" => "dropped",
            "hit" => "hit",
            "miss" => "miss",
            "bypass" => "bypass",
            _ => "other",
        };
    }

    private static string NormalizeSource(string? source)
    {
        if (string.Equals(source, "local", StringComparison.OrdinalIgnoreCase))
        {
            return "local";
        }

        if (string.Equals(source, "udp", StringComparison.OrdinalIgnoreCase))
        {
            return "udp";
        }

        if (string.Equals(source, "remote", StringComparison.OrdinalIgnoreCase))
        {
            return "remote";
        }

        return "none";
    }

    private static string NormalizeCache(string? cache)
    {
        if (string.Equals(cache, "hit", StringComparison.OrdinalIgnoreCase))
        {
            return "hit";
        }

        if (string.Equals(cache, "miss", StringComparison.OrdinalIgnoreCase))
        {
            return "miss";
        }

        return "bypass";
    }

    private static IEnumerable<Measurement<long>> ObserveConnectionActive()
    {
        yield return new Measurement<long>(
            Volatile.Read(ref s_activeConnectionsHttp),
            s_httpTransportTag);
        yield return new Measurement<long>(
            Volatile.Read(ref s_activeConnectionsNamedPipe),
            s_namedPipeTransportTag);
        yield return new Measurement<long>(
            Volatile.Read(ref s_activeConnectionsInProc),
            s_inProcTransportTag);
    }

    private static Measurement<long> ObserveStreamSessionActive()
    {
        return new Measurement<long>(Volatile.Read(ref s_activeStreamSessions));
    }

    private static Measurement<long> ObserveStreamQueueDepthMax()
    {
        return new Measurement<long>(Volatile.Read(ref s_streamQueueDepthMax));
    }

    private static Measurement<long> ObserveStreamQueueDepthAverage()
    {
        return new Measurement<long>(Volatile.Read(ref s_streamQueueDepthAverage));
    }

    private static Measurement<long> ObserveSnapshotCacheEntries()
    {
        return new Measurement<long>(Volatile.Read(ref s_snapshotCacheEntries));
    }
}
