using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteReadOnlyMessageRouterParityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [AvaloniaFact]
    public async Task HandleAsync_PreviewCapabilities_Returns_Supported_Transports()
    {
        var window = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            root: window,
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteReadOnlyMessageRouter(source);

        var snapshot = await SendRequestAsync<RemotePreviewCapabilitiesSnapshot>(
            router,
            RemoteReadOnlyMethods.PreviewCapabilitiesGet,
            new RemotePreviewCapabilitiesRequest());

        Assert.Equal("svg", snapshot.DefaultTransport);
        Assert.Contains("svg", snapshot.SupportedTransports);
        Assert.Contains("png", snapshot.SupportedTransports);
        Assert.True(snapshot.SupportsInput);
        Assert.True(snapshot.SupportsDiff);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PreviewSnapshot_Returns_Frame_Data()
    {
        var window = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            root: window,
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteReadOnlyMessageRouter(source);

        var snapshot = await SendRequestAsync<RemotePreviewSnapshot>(
            router,
            RemoteReadOnlyMethods.PreviewSnapshotGet,
            new RemotePreviewSnapshotRequest
            {
                Transport = "svg",
                IncludeFrameData = true,
                EnableDiff = false,
                MaxWidth = 1200,
                MaxHeight = 900,
                Scale = 1d,
            });

        Assert.Equal("svg", snapshot.Transport);
        Assert.True(snapshot.Width > 0);
        Assert.True(snapshot.Height > 0);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.FrameHash));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.FrameData));
        Assert.Equal("image/svg+xml", snapshot.MimeType);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PreviewSnapshot_SvgDiffWithoutFrameData_RemainsStable_WhenUnchanged()
    {
        var window = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            root: window,
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteReadOnlyMessageRouter(source);

        var first = await SendRequestAsync<RemotePreviewSnapshot>(
            router,
            RemoteReadOnlyMethods.PreviewSnapshotGet,
            new RemotePreviewSnapshotRequest
            {
                Transport = "svg",
                IncludeFrameData = false,
                EnableDiff = true,
                MaxWidth = 1200,
                MaxHeight = 900,
                Scale = 1d,
            });

        var second = await SendRequestAsync<RemotePreviewSnapshot>(
            router,
            RemoteReadOnlyMethods.PreviewSnapshotGet,
            new RemotePreviewSnapshotRequest
            {
                Transport = "svg",
                IncludeFrameData = false,
                EnableDiff = true,
                PreviousFrameHash = first.FrameHash,
                MaxWidth = 1200,
                MaxHeight = 900,
                Scale = 1d,
            });

        Assert.Equal(first.FrameHash, second.FrameHash);
        Assert.False(second.HasChanges);
        Assert.Null(second.FrameData);
        Assert.Equal("image/svg+xml", second.MimeType);
    }

    [AvaloniaFact]
    public async Task HandleAsync_TreeSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteTreeSnapshotRequest { Scope = "combined" };

        var expected = await source.GetTreeSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteTreeSnapshot>(router, RemoteReadOnlyMethods.TreeSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_SelectionSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteSelectionSnapshotRequest { Scope = "combined" };

        var expected = await source.GetSelectionSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteSelectionSnapshot>(router, RemoteReadOnlyMethods.SelectionGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PropertiesSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemotePropertiesSnapshotRequest
        {
            Scope = "combined",
            ControlName = "TestButton",
            IncludeClrProperties = true,
        };

        var expected = await source.GetPropertiesSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemotePropertiesSnapshot>(router, RemoteReadOnlyMethods.PropertiesSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_Elements3DSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var elements3D = new Elements3DPageViewModel(window, selectedObjectAccessor: null);
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window, elements3DPageViewModel: elements3D);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteElements3DSnapshotRequest
        {
            IncludeNodes = true,
            IncludeVisibleNodeIds = true,
        };

        var expected = await source.GetElements3DSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteElements3DSnapshot>(router, RemoteReadOnlyMethods.Elements3DSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_OverlayOptionsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var overlayState = new InProcessRemoteOverlayState();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window, overlayState: overlayState);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteOverlayOptionsSnapshotRequest();

        var expected = await source.GetOverlayOptionsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteOverlayOptionsSnapshot>(router, RemoteReadOnlyMethods.OverlayOptionsGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_CodeDocuments_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteCodeDocumentsRequest
        {
            Scope = "combined",
            ControlName = "TestButton",
        };

        var expected = await source.GetCodeDocumentsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteCodeDocumentsSnapshot>(router, RemoteReadOnlyMethods.CodeDocumentsGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_CodeResolveNode_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var codeDocuments = await source.GetCodeDocumentsSnapshotAsync(
            new RemoteCodeDocumentsRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
            },
            CancellationToken.None);
        var doc = codeDocuments.Documents.FirstOrDefault(document => !string.IsNullOrWhiteSpace(document.FilePath));
        var request = new RemoteCodeResolveNodeRequest
        {
            Scope = "combined",
            FilePath = doc?.FilePath ?? "unknown.file",
            Line = doc?.Line ?? 1,
            Column = doc?.Column ?? 0,
        };

        var expected = await source.ResolveCodeNodeAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteCodeResolveNodeSnapshot>(router, RemoteReadOnlyMethods.CodeResolveNode, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_StylesSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteStylesSnapshotRequest
        {
            Scope = "combined",
            ControlName = "TestButton",
        };

        var expected = await source.GetStylesSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteStylesSnapshot>(router, RemoteReadOnlyMethods.StylesSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_BindingsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteBindingsSnapshotRequest
        {
            Scope = "combined",
            ControlName = "TestButton",
        };

        var expected = await source.GetBindingsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteBindingsSnapshot>(router, RemoteReadOnlyMethods.BindingsSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_ResourcesSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteResourcesSnapshotRequest { IncludeEntries = true };

        var expected = await source.GetResourcesSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteResourcesSnapshot>(router, RemoteReadOnlyMethods.ResourcesSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_AssetsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteAssetsSnapshotRequest();

        var expected = await source.GetAssetsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteAssetsSnapshot>(router, RemoteReadOnlyMethods.AssetsSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_EventsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var breakpointService = new BreakpointService();
        using var eventsPage = new EventsPageViewModel(mainViewModel: null, breakpointService: breakpointService);
        using var logsPage = new LogsPageViewModel();
        var button = ((StackPanel)window.Content!).Children.OfType<Button>().First();
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            breakpointService: breakpointService,
            eventsPageViewModel: eventsPage,
            logsPageViewModel: logsPage);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteEventsSnapshotRequest { Scope = "combined", IncludeRecordedEvents = true };

        var expected = await source.GetEventsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteEventsSnapshot>(router, RemoteReadOnlyMethods.EventsSnapshotGet, request);

        var recorded = Assert.Single(expected.RecordedEvents);
        Assert.True(recorded.EventChain.Count >= 2, "Expected a full routed-event chain in the remote snapshot.");
        Assert.Contains(recorded.EventChain, link => !string.IsNullOrWhiteSpace(link.NodePath));
        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_BreakpointsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var breakpointService = new BreakpointService();
        using var eventsPage = new EventsPageViewModel(mainViewModel: null, breakpointService: breakpointService);
        using var logsPage = new LogsPageViewModel();
        var button = ((StackPanel)window.Content!).Children.OfType<Button>().First();
        breakpointService.AddPropertyBreakpoint(Button.IsEnabledProperty, button, "TestButton");
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            breakpointService: breakpointService,
            eventsPageViewModel: eventsPage,
            logsPageViewModel: logsPage);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteBreakpointsSnapshotRequest { Scope = "combined" };

        var expected = await source.GetBreakpointsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteBreakpointsSnapshot>(router, RemoteReadOnlyMethods.BreakpointsSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_LogsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        var breakpointService = new BreakpointService();
        using var eventsPage = new EventsPageViewModel(mainViewModel: null, breakpointService: breakpointService);
        using var logsPage = new LogsPageViewModel();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            breakpointService: breakpointService,
            eventsPageViewModel: eventsPage,
            logsPageViewModel: logsPage);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteLogsSnapshotRequest { IncludeEntries = true };

        var expected = await source.GetLogsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteLogsSnapshot>(router, RemoteReadOnlyMethods.LogsSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_MetricsSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableMetricsStream = false,
                EnableProfilerStream = false,
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteMetricsSnapshotRequest
        {
            IncludeSeries = true,
            IncludeMeasurements = true,
        };

        var expected = await source.GetMetricsSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteMetricsSnapshot>(router, RemoteReadOnlyMethods.MetricsSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_ProfilerSnapshot_Matches_Direct_Source_Output()
    {
        var window = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
                ProfilerSampleInterval = TimeSpan.FromMilliseconds(200),
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteReadOnlyMessageRouter(source);
        var request = new RemoteProfilerSnapshotRequest
        {
            IncludeSamples = true,
        };

        var expected = await source.GetProfilerSnapshotAsync(request, CancellationToken.None);
        var actual = await SendRequestAsync<RemoteProfilerSnapshot>(router, RemoteReadOnlyMethods.ProfilerSnapshotGet, request);

        AssertSnapshotsEqual(expected, actual);
    }

    [AvaloniaFact]
    public async Task HandleAsync_UnknownMethod_Returns_MethodNotFound_Error()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var router = new RemoteReadOnlyMessageRouter(source);

        var response = await router.HandleAsync(
            new DummyAttachConnection(),
            new RemoteRequestMessage(
                SessionId: Guid.NewGuid(),
                RequestId: 42,
                Method: "diagnostics.unknown",
                PayloadJson: "{}"),
            CancellationToken.None);

        var typed = Assert.IsType<RemoteResponseMessage>(response);
        Assert.False(typed.IsSuccess);
        Assert.Equal("method_not_found", typed.ErrorCode);
    }

    [AvaloniaFact]
    public async Task ReadOnlySource_CanBeCalled_FromBackgroundThread()
    {
        var window = CreateTestWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);
        var request = new RemoteTreeSnapshotRequest { Scope = "combined" };

        var snapshot = await Task.Run(async () =>
        {
            return await source.GetTreeSnapshotAsync(request, CancellationToken.None);
        });

        Assert.NotEmpty(snapshot.Nodes);
    }

    private static async Task<TSnapshot> SendRequestAsync<TSnapshot>(
        RemoteReadOnlyMessageRouter router,
        string method,
        object request)
    {
        var payload = JsonSerializer.Serialize(request, JsonOptions);
        var response = await router.HandleAsync(
            new DummyAttachConnection(),
            new RemoteRequestMessage(
                SessionId: Guid.NewGuid(),
                RequestId: 7,
                Method: method,
                PayloadJson: payload),
            CancellationToken.None);

        var typed = Assert.IsType<RemoteResponseMessage>(response);
        Assert.True(typed.IsSuccess, typed.ErrorCode + ": " + typed.ErrorMessage);
        var snapshot = JsonSerializer.Deserialize<TSnapshot>(typed.PayloadJson, JsonOptions);
        return Assert.IsType<TSnapshot>(snapshot);
    }

    private static void AssertSnapshotsEqual<TSnapshot>(TSnapshot expected, TSnapshot actual)
    {
        var expectedJson = NormalizeSnapshotJson(JsonSerializer.Serialize(expected, JsonOptions));
        var actualJson = NormalizeSnapshotJson(JsonSerializer.Serialize(actual, JsonOptions));
        Assert.Equal(expectedJson, actualJson);
    }

    private static string NormalizeSnapshotJson(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            obj.Remove("generation");
        }

        return node?.ToJsonString(JsonOptions) ?? json;
    }

    private static Window CreateTestWindow()
    {
        var button = new Button
        {
            Name = "TestButton",
            Content = "Click me",
        };

        var panel = new StackPanel
        {
            Name = "RootPanel",
            Children = { button },
        };
        panel.Resources["AccentBrush"] = Brushes.Orange;
        panel.Styles.Add(new Style(selector => selector.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.FontSizeProperty, 16d),
                new Setter(Button.ForegroundProperty, Brushes.DarkGreen),
            },
        });

        return new Window
        {
            Name = "RootWindow",
            Content = panel,
        };
    }

    private sealed class DummyAttachConnection : IAttachConnection
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();

        public string? RemoteEndpoint => "test";

        public bool IsOpen => true;

        public ValueTask SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<AttachReceiveResult?> ReceiveAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AttachReceiveResult?>(null);

        public ValueTask CloseAsync(string? reason = null, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
