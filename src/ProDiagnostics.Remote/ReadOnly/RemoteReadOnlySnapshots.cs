using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Source-location metadata attached to diagnostics records.
/// </summary>
public sealed record class RemoteSourceLocationSnapshot(
    string? Xaml,
    string? Code,
    string Status,
    RemoteSourceDocumentSnapshot? XamlLocation = null,
    RemoteSourceDocumentSnapshot? CodeLocation = null);

/// <summary>
/// Structured document location metadata.
/// </summary>
public sealed record class RemoteSourceDocumentSnapshot(
    string FilePath,
    int Line,
    int Column,
    string MethodName,
    string DisplayText);

/// <summary>
/// One changed preview region.
/// </summary>
public sealed record class RemotePreviewRectSnapshot(
    int X,
    int Y,
    int Width,
    int Height);

/// <summary>
/// Snapshot of preview domain capabilities.
/// </summary>
public sealed record class RemotePreviewCapabilitiesSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    string DefaultTransport,
    IReadOnlyList<string> SupportedTransports,
    bool SupportsInput,
    bool SupportsDiff,
    bool IsPaused,
    bool StreamEnabled,
    int TargetFps,
    int MaxWidth,
    int MaxHeight,
    double MaxScale);

/// <summary>
/// Snapshot of one captured preview frame.
/// </summary>
public sealed record class RemotePreviewSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    string Transport,
    string MimeType,
    int Width,
    int Height,
    double Scale,
    double RenderScaling,
    DateTimeOffset CapturedAtUtc,
    bool IsPaused,
    bool IsDelta,
    bool HasChanges,
    string FrameHash,
    string? PreviousFrameHash,
    string DiffKind,
    IReadOnlyList<RemotePreviewRectSnapshot> ChangedRegions,
    string? FrameData);

/// <summary>
/// Snapshot of visual bounds.
/// </summary>
public sealed record class RemoteRectSnapshot(
    double X,
    double Y,
    double Width,
    double Height);

/// <summary>
/// One node in a diagnostics tree snapshot.
/// </summary>
public sealed record class RemoteTreeNodeSnapshot(
    string NodeId,
    string NodePath,
    string? ParentNodePath,
    int Depth,
    string Type,
    string? ElementName,
    string Classes,
    string DisplayName,
    RemoteSourceLocationSnapshot Source,
    string RelationshipKind = "unknown",
    bool IsVisible = true,
    double Opacity = 1d,
    int ZIndex = 0,
    RemoteRectSnapshot? Bounds = null);

/// <summary>
/// Flattened snapshot for one tree scope.
/// </summary>
public sealed record class RemoteTreeSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    IReadOnlyList<RemoteTreeNodeSnapshot> Nodes);

/// <summary>
/// Snapshot of currently selected diagnostics target for one tree scope.
/// </summary>
public sealed record class RemoteSelectionSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string? NodeId,
    string? NodePath,
    string? Target,
    string? TargetType);

/// <summary>
/// One property row in diagnostics properties snapshot.
/// </summary>
public sealed record class RemotePropertySnapshot(
    string Name,
    string Group,
    string Type,
    string AssignedType,
    string PropertyType,
    string? DeclaringType,
    string Priority,
    bool? IsAttached,
    bool IsReadOnly,
    string? ValueText,
    string EditorKind = "text",
    IReadOnlyList<string>? EnumOptions = null,
    bool CanClearValue = false,
    bool CanSetNull = true,
    bool IsLocal = false,
    bool IsStyle = false,
    bool IsInherited = false,
    string CoercionStatus = "unknown",
    string ValidationStatus = "ok",
    RemoteSourceLocationSnapshot? Source = null);

/// <summary>
/// One setter row from value-frame/style diagnostics.
/// </summary>
public sealed record class RemoteSetterSnapshot(
    string Id,
    string Name,
    string? ValueText,
    bool IsActive,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source = null,
    string? FrameId = null);

/// <summary>
/// One value frame entry.
/// </summary>
public sealed record class RemoteValueFrameSnapshot(
    string Id,
    string Description,
    bool IsActive,
    string SourceLocation,
    IReadOnlyList<RemoteSetterSnapshot> Setters,
    RemoteSourceLocationSnapshot? Source = null,
    string? ParentId = null);

/// <summary>
/// One pseudo-class state row from diagnostics properties.
/// </summary>
public sealed record class RemotePseudoClassSnapshot(
    string Name,
    bool IsActive);

/// <summary>
/// One source document for inspected element.
/// </summary>
public sealed record class RemoteCodeDocumentSnapshot(
    string Kind,
    string FilePath,
    string DisplayName,
    string LocationText,
    int Line,
    int Column,
    string MethodName,
    bool Exists,
    string Text);

/// <summary>
/// Snapshot of source documents for selected node.
/// </summary>
public sealed record class RemoteCodeDocumentsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string Target,
    string TargetType,
    string? TargetNodeId,
    string? TargetNodePath,
    string Status,
    IReadOnlyList<RemoteCodeDocumentSnapshot> Documents);

/// <summary>
/// Snapshot of node resolution for code caret location.
/// </summary>
public sealed record class RemoteCodeResolveNodeSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string FilePath,
    int Line,
    int Column,
    bool Found,
    string? NodeId,
    string? NodePath,
    string? Target,
    string? TargetType,
    string MatchKind,
    string Status);

/// <summary>
/// Snapshot for selected target properties and value frames.
/// </summary>
public sealed record class RemotePropertiesSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string Target,
    string TargetType,
    string? TargetNodeId,
    string? TargetNodePath,
    IReadOnlyList<RemotePropertySnapshot> Properties,
    IReadOnlyList<RemotePseudoClassSnapshot> PseudoClasses,
    IReadOnlyList<RemoteValueFrameSnapshot> Frames,
    RemoteSourceLocationSnapshot Source);

/// <summary>
/// One node row from Elements 3D diagnostics snapshot.
/// </summary>
public sealed record class RemoteElements3DNodeSnapshot(
    string NodeId,
    string? NodePath,
    int Depth,
    string Node,
    int ZIndex,
    RemoteRectSnapshot Bounds,
    bool IsVisible,
    double Opacity,
    bool IsRendered,
    bool IsSelected);

/// <summary>
/// Snapshot for Elements 3D diagnostics.
/// </summary>
public sealed record class RemoteElements3DSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    string InspectedRoot,
    string? MainRootNodeId,
    string? CurrentRootNodeId,
    string? ScopedSelectionNodeId,
    string? SelectedNodeId,
    bool IsScopedToSelectionBranch,
    int NodeCount,
    int VisibleNodeCount,
    bool ShowInvisibleNodes,
    bool ShowExploded3DView,
    bool ShowAllLayersInGrid,
    double DepthSpacing,
    int Flat2DMaxLayersPerRow,
    double Tilt,
    double Zoom,
    double OrbitYaw,
    double OrbitPitch,
    double OrbitRoll,
    int AvailableMinDepth,
    int AvailableMaxDepth,
    int MinVisibleDepth,
    int MaxVisibleDepth,
    int MaxVisibleElements,
    string? SvgSnapshot,
    string? SvgViewBox,
    IReadOnlyList<RemoteElements3DNodeSnapshot> Nodes,
    IReadOnlyList<string> VisibleNodeIds);

/// <summary>
/// Snapshot for overlay and live-inspect options.
/// </summary>
public sealed record class RemoteOverlayOptionsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    bool VisualizeMarginPadding,
    bool ShowInfo,
    bool ShowRulers,
    bool ShowExtensionLines,
    bool HighlightElements,
    bool LiveHoverEnabled,
    bool ClipToTargetBounds);

/// <summary>
/// One view-model context row from view-model/bindings diagnostics.
/// </summary>
public sealed record class RemoteViewModelContextSnapshot(
    string Id,
    int Level,
    string Element,
    string Priority,
    string ViewModelType,
    string ValuePreview,
    bool IsCurrent,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source,
    string? NodeId,
    string? NodePath);

/// <summary>
/// One binding diagnostics row from view-model/bindings diagnostics.
/// </summary>
public sealed record class RemoteBindingDiagnosticSnapshot(
    string Id,
    string PropertyName,
    string OwnerType,
    string Priority,
    string Status,
    string BindingDescription,
    string ValuePreview,
    string ValueType,
    string Diagnostic,
    bool HasError,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source,
    string? NodeId,
    string? NodePath);

/// <summary>
/// Snapshot for view-model/bindings diagnostics.
/// </summary>
public sealed record class RemoteBindingsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string InspectedElement,
    string InspectedElementType,
    string? InspectedNodeId,
    string? InspectedNodePath,
    bool ShowOnlyBindingErrors,
    IReadOnlyList<RemoteViewModelContextSnapshot> ViewModels,
    IReadOnlyList<RemoteBindingDiagnosticSnapshot> Bindings);

/// <summary>
/// One tree entry row from styles diagnostics.
/// </summary>
public sealed record class RemoteStyleTreeEntrySnapshot(
    string Id,
    int Depth,
    string Element,
    string ElementType,
    string Classes,
    string PseudoClasses,
    int FrameCount,
    int ActiveFrameCount,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source,
    string? NodeId,
    string? NodePath,
    string? ParentId = null);

/// <summary>
/// One style-resolution trace row.
/// </summary>
public sealed record class RemoteStyleResolutionSnapshot(
    string Id,
    int Order,
    int HostLevel,
    string Host,
    string HostKind,
    string PropagationScope,
    int LogicalDistance,
    int VisualDistance,
    bool StylesInitialized,
    string Style,
    string StyleKind,
    string Selector,
    string Path,
    string SourceLocation,
    int AppliedCount,
    int ActiveCount,
    string Notes,
    RemoteSourceLocationSnapshot? Source = null);

/// <summary>
/// Snapshot for styles diagnostics.
/// </summary>
public sealed record class RemoteStylesSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string InspectedRoot,
    string InspectedRootType,
    string? InspectedRootNodeId,
    IReadOnlyList<RemoteStyleTreeEntrySnapshot> TreeEntries,
    IReadOnlyList<RemoteValueFrameSnapshot> Frames,
    IReadOnlyList<RemoteSetterSnapshot> Setters,
    IReadOnlyList<RemoteStyleResolutionSnapshot> Resolution);

/// <summary>
/// One node in resources tree.
/// </summary>
public sealed record class RemoteResourceNodeSnapshot(
    string Id,
    string? NodeId,
    string NodePath,
    string? ParentNodePath,
    int Depth,
    string Kind,
    string Name,
    string? SecondaryText,
    string? ValueType,
    string? ValuePreview,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source = null);

/// <summary>
/// One flattened resource entry row.
/// </summary>
public sealed record class RemoteResourceEntrySnapshot(
    string Id,
    string? NodeId,
    string NodePath,
    string KeyDisplay,
    string KeyType,
    string ValueType,
    string ValuePreview,
    bool IsDeferred,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source = null);

/// <summary>
/// Snapshot for resources diagnostics.
/// </summary>
public sealed record class RemoteResourcesSnapshot(
    int SnapshotVersion,
    long Generation,
    IReadOnlyList<RemoteResourceNodeSnapshot> Nodes,
    IReadOnlyList<RemoteResourceEntrySnapshot> Entries);

/// <summary>
/// One asset row in assets diagnostics snapshot.
/// </summary>
public sealed record class RemoteAssetSnapshot(
    string Id,
    string AssemblyName,
    string AssetPath,
    string Name,
    string Kind,
    string Uri,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source = null);

/// <summary>
/// Snapshot for assets diagnostics.
/// </summary>
public sealed record class RemoteAssetsSnapshot(
    int SnapshotVersion,
    long Generation,
    IReadOnlyList<RemoteAssetSnapshot> Assets);

/// <summary>
/// One events tree node from diagnostics events page.
/// </summary>
public sealed record class RemoteEventNodeSnapshot(
    string Id,
    string? ParentId,
    int Depth,
    string NodeKind,
    string Text,
    bool? IsEnabled,
    bool IsVisible,
    bool IsExpanded,
    string? OwnerType,
    string? EventName);

/// <summary>
/// One recorded routed-event entry.
/// </summary>
public sealed record class RemoteRecordedEventSnapshot(
    string Id,
    DateTimeOffset TriggerTime,
    string EventName,
    string Source,
    string Originator,
    string? HandledBy,
    string ObservedRoutes,
    bool IsHandled,
    string? SourceNodeId,
    string? SourceNodePath);

/// <summary>
/// Snapshot for events diagnostics.
/// </summary>
public sealed record class RemoteEventsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string Status,
    bool IncludeBubbleRoutes,
    bool IncludeTunnelRoutes,
    bool IncludeDirectRoutes,
    bool IncludeHandledEvents,
    bool IncludeUnhandledEvents,
    int MaxRecordedEvents,
    bool AutoScrollToLatest,
    int TotalRecordedEvents,
    int VisibleRecordedEvents,
    IReadOnlyList<RemoteEventNodeSnapshot> Nodes,
    IReadOnlyList<RemoteRecordedEventSnapshot> RecordedEvents);

/// <summary>
/// One breakpoint entry from diagnostics breakpoints page.
/// </summary>
public sealed record class RemoteBreakpointSnapshot(
    string Id,
    string Kind,
    string Name,
    string TargetDescription,
    bool IsEnabled,
    int HitCount,
    int TriggerAfterHits,
    bool SuspendExecution,
    bool LogMessage,
    bool RemoveOnceHit,
    DateTimeOffset? LastHitAt,
    string LastHitDetails,
    string? NodeId,
    string? NodePath,
    string SourceLocation,
    RemoteSourceLocationSnapshot? Source);

/// <summary>
/// Snapshot for breakpoints diagnostics.
/// </summary>
public sealed record class RemoteBreakpointsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Scope,
    string Status,
    int BreakpointCount,
    IReadOnlyList<RemoteBreakpointSnapshot> Breakpoints);

/// <summary>
/// One log row from diagnostics logs page.
/// </summary>
public sealed record class RemoteLogEntrySnapshot(
    string Id,
    DateTimeOffset Timestamp,
    string Level,
    string Area,
    string Source,
    string Message);

/// <summary>
/// Snapshot for logs diagnostics.
/// </summary>
public sealed record class RemoteLogsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    string CollectorName,
    bool ShowVerbose,
    bool ShowDebug,
    bool ShowInformation,
    bool ShowWarning,
    bool ShowError,
    bool ShowFatal,
    int MaxEntries,
    int EntryCount,
    int VisibleEntryCount,
    string FilterText,
    IReadOnlyList<RemoteLogEntrySnapshot> Entries);

/// <summary>
/// One raw metrics measurement row.
/// </summary>
public sealed record class RemoteMetricMeasurementSnapshot(
    string Id,
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
/// One aggregated metrics series row.
/// </summary>
public sealed record class RemoteMetricSeriesSnapshot(
    string Id,
    string MeterName,
    string InstrumentName,
    string Description,
    string Unit,
    string InstrumentType,
    double LastValue,
    double AverageValue,
    double MinValue,
    double MaxValue,
    int SampleCount,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RemoteStreamTag> Tags);

/// <summary>
/// Snapshot for metrics diagnostics.
/// </summary>
public sealed record class RemoteMetricsSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    bool IsPaused,
    int MaxRetainedMeasurements,
    int MaxSeries,
    int MaxSamplesPerSeries,
    long TotalMeasurements,
    long DroppedMeasurements,
    int MeasurementCount,
    int SeriesCount,
    IReadOnlyList<RemoteMetricSeriesSnapshot> Series,
    IReadOnlyList<RemoteMetricMeasurementSnapshot> Measurements);

/// <summary>
/// One profiler sample row.
/// </summary>
public sealed record class RemoteProfilerSampleSnapshot(
    string Id,
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
/// Snapshot for profiler diagnostics.
/// </summary>
public sealed record class RemoteProfilerSnapshot(
    int SnapshotVersion,
    long Generation,
    string Status,
    bool IsPaused,
    int MaxRetainedSamples,
    int SampleIntervalMs,
    long TotalSamples,
    long DroppedSamples,
    int SampleCount,
    double CurrentCpuPercent,
    double PeakCpuPercent,
    double CurrentWorkingSetMb,
    double PeakWorkingSetMb,
    double CurrentManagedHeapMb,
    double CurrentActivityDurationMs,
    double PeakActivityDurationMs,
    IReadOnlyList<RemoteProfilerSampleSnapshot> Samples);
