using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// String-based key/value tag payload used in streamed diagnostics messages.
/// </summary>
public readonly record struct RemoteStreamTag(string Key, string Value);

/// <summary>
/// Stream payload for live preview updates.
/// </summary>
public readonly record struct RemotePreviewStreamPayload(
    DateTimeOffset TimestampUtc,
    long Generation,
    string Transport,
    string MimeType,
    int Width,
    int Height,
    double Scale,
    double RenderScaling,
    bool IsDelta,
    bool HasChanges,
    string FrameHash,
    string? PreviousFrameHash,
    string DiffKind,
    IReadOnlyList<RemotePreviewRectSnapshot> ChangedRegions,
    string? FrameData);

/// <summary>
/// Stream payload for diagnostics metrics updates.
/// </summary>
public readonly record struct RemoteMetricStreamPayload(
    DateTimeOffset TimestampUtc,
    string Source,
    Guid SessionId,
    string MeterName,
    string InstrumentName,
    string Description,
    string Unit,
    string InstrumentType,
    double Value,
    IReadOnlyList<RemoteStreamTag> Tags);

/// <summary>
/// Stream payload for profiler updates.
/// </summary>
public readonly record struct RemoteProfilerStreamPayload(
    DateTimeOffset TimestampUtc,
    string Source,
    Guid SessionId,
    string Process,
    double CpuPercent,
    double WorkingSetMb,
    double PrivateMemoryMb,
    double ManagedHeapMb,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    string ActivitySource,
    string ActivityName,
    double DurationMs,
    IReadOnlyList<RemoteStreamTag> Tags);

/// <summary>
/// Stream payload for logs updates.
/// </summary>
public readonly record struct RemoteLogStreamPayload(
    DateTimeOffset TimestampUtc,
    string Level,
    string Area,
    string Source,
    string Message);

/// <summary>
/// Stream payload for routed-events updates.
/// </summary>
public readonly record struct RemoteEventStreamPayload(
    DateTimeOffset TimestampUtc,
    string EventName,
    string? EventOwnerType,
    string Source,
    string Originator,
    string ObservedRoutes,
    bool IsHandled,
    string HandledBy,
    int ChainLength,
    string? SourceNodeId,
    string? SourceNodePath,
    IReadOnlyList<RemoteEventChainLinkSnapshot> EventChain);
