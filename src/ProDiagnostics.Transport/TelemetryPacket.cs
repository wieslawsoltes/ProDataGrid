using System;
using System.Collections.Generic;

namespace ProDiagnostics.Transport;

public abstract record TelemetryPacket(TelemetryMessageType MessageType, Guid SessionId, DateTimeOffset Timestamp);

public sealed record TelemetryHello(
    Guid SessionId,
    DateTimeOffset Timestamp,
    int ProcessId,
    string ProcessName,
    string AppName,
    string MachineName,
    string RuntimeVersion)
    : TelemetryPacket(TelemetryMessageType.Hello, SessionId, Timestamp);

public sealed record TelemetryActivity(
    Guid SessionId,
    DateTimeOffset Timestamp,
    string SourceName,
    string Name,
    DateTimeOffset StartTime,
    TimeSpan Duration,
    IReadOnlyList<TelemetryTag> Tags)
    : TelemetryPacket(TelemetryMessageType.Activity, SessionId, Timestamp);

public sealed record TelemetryMetric(
    Guid SessionId,
    DateTimeOffset Timestamp,
    string MeterName,
    string InstrumentName,
    string Description,
    string Unit,
    string InstrumentType,
    TelemetryMetricValue Value,
    IReadOnlyList<TelemetryTag> Tags)
    : TelemetryPacket(TelemetryMessageType.Metric, SessionId, Timestamp);
