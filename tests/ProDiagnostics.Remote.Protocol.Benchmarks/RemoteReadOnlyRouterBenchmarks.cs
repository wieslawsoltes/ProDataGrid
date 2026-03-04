using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Avalonia.Diagnostics.Remote;

namespace ProDiagnostics.Remote.Protocol.Benchmarks;

[MemoryDiagnoser]
public class RemoteReadOnlyRouterBenchmarks
{
    [Params(500, 2000)]
    public int NodeCount { get; set; }

    private BenchmarkReadOnlySource _source = null!;
    private RemoteReadOnlyMessageRouter _router = null!;
    private FakeAttachConnection _connection = null!;
    private Guid _sessionId;
    private string _nodeId = string.Empty;
    private string _nodePath = string.Empty;
    private string _treePayloadJson = string.Empty;
    private string _propertiesPayloadJson = string.Empty;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _source = new BenchmarkReadOnlySource(NodeCount);
        _router = new RemoteReadOnlyMessageRouter(_source);
        _connection = new FakeAttachConnection();
        _sessionId = Guid.NewGuid();
        _treePayloadJson = JsonSerializer.Serialize(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            },
            RemoteJsonSerializerContext.Default.RemoteTreeSnapshotRequest);

        var tree = await _source.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            });
        var target = tree.Nodes.FirstOrDefault(x => x.Depth > 1) ?? tree.Nodes.Last();
        _nodeId = target.NodeId;
        _nodePath = target.NodePath;
        _propertiesPayloadJson = JsonSerializer.Serialize(
            new RemotePropertiesSnapshotRequest
            {
                Scope = "combined",
                NodeId = _nodeId,
                NodePath = _nodePath,
                IncludeClrProperties = true,
            },
            RemoteJsonSerializerContext.Default.RemotePropertiesSnapshotRequest);
    }

    [Benchmark]
    public ValueTask<IRemoteMessage?> Handle_TreeSnapshot()
    {
        return _router.HandleAsync(
            _connection,
            new RemoteRequestMessage(
                SessionId: _sessionId,
                RequestId: 1,
                Method: RemoteReadOnlyMethods.TreeSnapshotGet,
                PayloadJson: _treePayloadJson),
            CancellationToken.None);
    }

    [Benchmark]
    public ValueTask<IRemoteMessage?> Handle_PropertiesSnapshot()
    {
        return _router.HandleAsync(
            _connection,
            new RemoteRequestMessage(
                SessionId: _sessionId,
                RequestId: 2,
                Method: RemoteReadOnlyMethods.PropertiesSnapshotGet,
                PayloadJson: _propertiesPayloadJson),
            CancellationToken.None);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _source.DisposeAsync();
    }

    private sealed class FakeAttachConnection : IAttachConnection
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();

        public string? RemoteEndpoint => "benchmark";

        public bool IsOpen => true;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
        {
            _ = message;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return ValueTask.FromResult<AttachReceiveResult?>(null);
        }

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default)
        {
            _ = reason;
            _ = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BenchmarkReadOnlySource : IRemoteReadOnlyDiagnosticsSource, IAsyncDisposable
    {
        private readonly RemoteTreeSnapshot _treeSnapshot;
        private readonly RemotePropertiesSnapshot _propertiesSnapshot;
        private static readonly RemoteSourceLocationSnapshot s_emptySource = new(
            Xaml: null,
            Code: null,
            Status: string.Empty);

        public BenchmarkReadOnlySource(int nodeCount)
        {
            var nodes = new List<RemoteTreeNodeSnapshot>(nodeCount);
            for (var index = 0; index < nodeCount; index++)
            {
                var depth = index == 0 ? 0 : 2;
                var nodePath = "/combined/" + index;
                nodes.Add(new RemoteTreeNodeSnapshot(
                    NodeId: "node-" + index,
                    NodePath: nodePath,
                    ParentNodePath: depth == 0 ? null : "/combined/0",
                    Depth: depth,
                    Type: "Avalonia.Controls.Border",
                    ElementName: "Node" + index,
                    Classes: string.Empty,
                    DisplayName: "Node " + index,
                    Source: s_emptySource));
            }

            _treeSnapshot = new RemoteTreeSnapshot(
                SnapshotVersion: 1,
                Generation: 1,
                Scope: "combined",
                Nodes: nodes);

            _propertiesSnapshot = new RemotePropertiesSnapshot(
                SnapshotVersion: 1,
                Generation: 1,
                Scope: "combined",
                Target: "Node 1",
                TargetType: "Avalonia.Controls.Border",
                TargetNodeId: "node-1",
                TargetNodePath: "/combined/1",
                Properties: new[]
                {
                    new RemotePropertySnapshot(
                        Name: "Width",
                        Group: "Properties",
                        Type: "Double",
                        AssignedType: "Double",
                        PropertyType: "Double",
                        DeclaringType: "Avalonia.Layout.Layoutable",
                        Priority: "LocalValue",
                        IsAttached: false,
                        IsReadOnly: false,
                        ValueText: "120"),
                },
                Frames: Array.Empty<RemoteValueFrameSnapshot>(),
                Source: s_emptySource);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<RemotePreviewCapabilitiesSnapshot> GetPreviewCapabilitiesSnapshotAsync(
            RemotePreviewCapabilitiesRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemotePreviewSnapshot> GetPreviewSnapshotAsync(
            RemotePreviewSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteTreeSnapshot> GetTreeSnapshotAsync(
            RemoteTreeSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_treeSnapshot);

        public ValueTask<RemoteSelectionSnapshot> GetSelectionSnapshotAsync(
            RemoteSelectionSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemotePropertiesSnapshot> GetPropertiesSnapshotAsync(
            RemotePropertiesSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_propertiesSnapshot);

        public ValueTask<RemoteElements3DSnapshot> GetElements3DSnapshotAsync(
            RemoteElements3DSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteOverlayOptionsSnapshot> GetOverlayOptionsSnapshotAsync(
            RemoteOverlayOptionsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteCodeDocumentsSnapshot> GetCodeDocumentsSnapshotAsync(
            RemoteCodeDocumentsRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteCodeResolveNodeSnapshot> ResolveCodeNodeAsync(
            RemoteCodeResolveNodeRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteBindingsSnapshot> GetBindingsSnapshotAsync(
            RemoteBindingsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteStylesSnapshot> GetStylesSnapshotAsync(
            RemoteStylesSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteResourcesSnapshot> GetResourcesSnapshotAsync(
            RemoteResourcesSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteAssetsSnapshot> GetAssetsSnapshotAsync(
            RemoteAssetsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteEventsSnapshot> GetEventsSnapshotAsync(
            RemoteEventsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteBreakpointsSnapshot> GetBreakpointsSnapshotAsync(
            RemoteBreakpointsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteLogsSnapshot> GetLogsSnapshotAsync(
            RemoteLogsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteMetricsSnapshot> GetMetricsSnapshotAsync(
            RemoteMetricsSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<RemoteProfilerSnapshot> GetProfilerSnapshotAsync(
            RemoteProfilerSnapshotRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
