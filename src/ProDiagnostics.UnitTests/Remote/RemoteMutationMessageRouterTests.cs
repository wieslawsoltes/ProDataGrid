using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteMutationMessageRouterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    [AvaloniaFact]
    public async Task HandleAsync_Preview_Commands_Apply_State_Changes()
    {
        var (window, _) = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            root: window,
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteMutationDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteMutationMessageRouter(source);

        var pause = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PreviewPausedSet,
            new RemoteSetPreviewPausedRequest
            {
                IsPaused = true,
            });
        Assert.True(pause.Changed);
        Assert.True(streamSource.IsPreviewPaused);

        var settings = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PreviewSettingsSet,
            new RemoteSetPreviewSettingsRequest
            {
                Transport = "png",
                TargetFps = 12,
                MaxWidth = 1024,
                MaxHeight = 768,
                Scale = 1.25d,
                EnableDiff = true,
                IncludeFrameData = true,
            });
        Assert.True(settings.Changed);

        var resume = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PreviewPausedSet,
            new RemoteSetPreviewPausedRequest
            {
                IsPaused = false,
            });
        Assert.True(resume.Changed);
        Assert.False(streamSource.IsPreviewPaused);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PreviewInputInject_Accepts_Pointer_And_Key_Events()
    {
        var (window, _) = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            root: window,
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteMutationDiagnosticsSource(
            window,
            streamPauseController: streamSource);
        var router = new RemoteMutationMessageRouter(source);

        var pointer = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PreviewInputInject,
            new RemotePreviewInputRequest
            {
                EventType = "pointer_move",
                X = 20,
                Y = 30,
                FrameWidth = 800,
                FrameHeight = 600,
            });
        Assert.True(pointer.Changed);

        var key = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PreviewInputInject,
            new RemotePreviewInputRequest
            {
                EventType = "key_down",
                Key = "Enter",
            });
        Assert.True(key.Changed);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PropertiesSet_Updates_Target_Property()
    {
        var (window, button) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        Assert.True(button.IsEnabled);

        var result = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PropertiesSet,
            new RemoteSetPropertyRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PropertyName = "IsEnabled",
                PropertyKind = "avalonia",
                PropertyDeclaringType = typeof(InputElement).AssemblyQualifiedName,
                ValueText = "false",
            });

        Assert.True(result.Changed);
        Assert.False(button.IsEnabled);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PropertiesSet_Updates_Target_Clr_Property()
    {
        var (window, button) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        Assert.Equal("before", button.DiagnosticTag);

        var result = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PropertiesSet,
            new RemoteSetPropertyRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PropertyName = nameof(TestInspectableButton.DiagnosticTag),
                PropertyKind = "clr",
                PropertyDeclaringType = typeof(TestInspectableButton).AssemblyQualifiedName,
                ValueText = "after",
            });

        Assert.True(result.Changed);
        Assert.Equal("after", button.DiagnosticTag);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PseudoClassSet_Toggles_State()
    {
        var (window, button) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var enable = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PseudoClassSet,
            new RemoteSetPseudoClassRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PseudoClass = "remote-state",
                IsActive = true,
            });
        Assert.True(enable.Changed);
        Assert.Contains("remote-state", button.Classes);

        var disable = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.PseudoClassSet,
            new RemoteSetPseudoClassRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PseudoClass = "remote-state",
                IsActive = false,
            });
        Assert.True(disable.Changed);
        Assert.DoesNotContain("remote-state", button.Classes);
    }

    [AvaloniaFact]
    public async Task HandleAsync_PseudoClassSet_WithPseudoClass_Returns_ValidationError()
    {
        var (window, _) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var response = await SendRawRequestAsync(
            router,
            RemoteMutationMethods.PseudoClassSet,
            new RemoteSetPseudoClassRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PseudoClass = ":pointerover",
                IsActive = true,
            });

        Assert.False(response.IsSuccess);
        Assert.Equal("validation_error", response.ErrorCode);
    }

    [AvaloniaFact]
    public async Task HandleAsync_InspectHovered_WhenGestureIsInactive_Returns_NoChange()
    {
        var (window, _) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var result = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.InspectHovered,
            new RemoteInspectHoveredRequest
            {
                Scope = "combined",
            });

        Assert.Equal(RemoteMutationMethods.InspectHovered, result.Operation);
        Assert.False(result.Changed);
    }

    [AvaloniaFact]
    public async Task InspectHovered_WithLiveHoverEnabled_DoesNotChangeSelection_WithoutGesture()
    {
        var (window, button) = CreateTestWindow();
        window.Width = 240;
        window.Height = 120;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var point = button.TranslatePoint(
            new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d),
            window);
        Assert.True(point.HasValue);

        var selectionState = new InProcessRemoteSelectionState();
        var overlayState = new InProcessRemoteOverlayState();
        var source = new InProcessRemoteMutationDiagnosticsSource(
            window,
            overlayState: overlayState,
            selectionState: selectionState);

        var pointerMove = await source.InjectPreviewInputAsync(new RemotePreviewInputRequest
        {
            EventType = "pointer_move",
            X = point.Value.X,
            Y = point.Value.Y,
        });
        Assert.True(pointerMove.Changed);
        Assert.True(overlayState.IsLiveHoverEnabled);

        var result = await source.InspectHoveredAsync(new RemoteInspectHoveredRequest
        {
            Scope = "combined",
            RequireInspectGesture = true,
        });

        Assert.Equal(RemoteMutationMethods.InspectHovered, result.Operation);
        Assert.False(result.Changed);
        Assert.Null(selectionState.GetSnapshot("combined").NodePath);

        window.Close();
        source.Dispose();
    }

    [AvaloniaFact]
    public async Task LiveHoverOverlay_Remains_Inactive_Until_Remote_Session_Attaches()
    {
        var button = new Button
        {
            Name = "OverlayTarget",
            Content = "Target",
        };
        var window = new Window
        {
            Width = 320,
            Height = 180,
            Content = new VisualLayerManager
            {
                Child = new Grid
                {
                    Children =
                    {
                        button,
                    },
                },
            },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var point = button.TranslatePoint(
            new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d),
            window);
        Assert.True(point.HasValue);

        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        try
        {
            var pointerMoveBeforeSession = await source.InjectPreviewInputAsync(new RemotePreviewInputRequest
            {
                EventType = "pointer_move",
                X = point.Value.X,
                Y = point.Value.Y,
            });
            Assert.True(pointerMoveBeforeSession.Changed);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, AdornerLayer.GetAdornerLayer(button)?.Children.Count ?? 0);

            source.SetActiveRemoteSessionCount(1);
            Dispatcher.UIThread.RunJobs();

            var selectionAfterSession = await source.SetSelectionAsync(new RemoteSetSelectionRequest
            {
                Scope = "combined",
                ControlName = "OverlayTarget",
            });
            Assert.True(selectionAfterSession.Changed);
            Dispatcher.UIThread.RunJobs();
            Assert.True((AdornerLayer.GetAdornerLayer(button)?.Children.Count ?? 0) > 0);

            source.SetActiveRemoteSessionCount(0);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, AdornerLayer.GetAdornerLayer(button)?.Children.Count ?? 0);
        }
        finally
        {
            source.Dispose();
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task HandleAsync_Elements3D_Commands_Apply_StateChanges()
    {
        var (window, _) = CreateTestWindow();
        var elements3D = new Elements3DPageViewModel(window, selectedObjectAccessor: null);
        var source = new InProcessRemoteMutationDiagnosticsSource(
            window,
            elements3DPageViewModel: elements3D);
        var router = new RemoteMutationMessageRouter(source);

        var rootSet = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.Elements3DRootSet,
            new RemoteSetElements3DRootRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
            });
        Assert.True(rootSet.Changed);
        Assert.Contains("TestButton", elements3D.InspectedRoot, StringComparison.Ordinal);

        var filtersSet = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.Elements3DFiltersSet,
            new RemoteSetElements3DFiltersRequest
            {
                ShowInvisibleNodes = false,
                ShowExploded3DView = false,
                ShowAllLayersInGrid = true,
                MaxVisibleElements = 12,
            });
        Assert.True(filtersSet.Changed);
        Assert.False(elements3D.ShowInvisibleNodes);
        Assert.False(elements3D.ShowExploded3DView);
        Assert.True(elements3D.ShowAllLayersInGrid);
        Assert.Equal(12, elements3D.MaxVisibleElements);

        var rootReset = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.Elements3DRootReset,
            new RemoteEmptyMutationRequest());
        Assert.True(rootReset.Changed);
    }

    [AvaloniaFact]
    public async Task HandleAsync_Overlay_Commands_Apply_StateChanges()
    {
        var (window, _) = CreateTestWindow();
        var overlayState = new InProcessRemoteOverlayState();
        var source = new InProcessRemoteMutationDiagnosticsSource(
            window,
            overlayState: overlayState);
        var router = new RemoteMutationMessageRouter(source);

        var optionsSet = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.OverlayOptionsSet,
            new RemoteSetOverlayOptionsRequest
            {
                ShowInfo = true,
                ShowRulers = true,
                ShowExtensionLines = true,
                ClipToTargetBounds = true,
            });
        Assert.True(optionsSet.Changed);
        var snapshot = overlayState.GetSnapshot();
        Assert.True(snapshot.ShowInfo);
        Assert.True(snapshot.ShowRulers);
        Assert.True(snapshot.ShowExtensionLines);
        Assert.True(snapshot.ClipToTargetBounds);

        var liveHoverSet = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.OverlayLiveHoverSet,
            new RemoteSetOverlayLiveHoverRequest
            {
                IsEnabled = false,
            });
        Assert.True(liveHoverSet.Changed);
        Assert.False(overlayState.GetSnapshot().LiveHoverEnabled);
    }

    [AvaloniaFact]
    public async Task HandleAsync_Breakpoints_Commands_Apply_SideEffects()
    {
        var (window, _) = CreateTestWindow();
        var breakpoints = new BreakpointService();
        var source = new InProcessRemoteMutationDiagnosticsSource(window, breakpointService: breakpoints);
        var router = new RemoteMutationMessageRouter(source);

        var addProperty = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsPropertyAdd,
            new RemoteAddPropertyBreakpointRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PropertyName = "IsEnabled",
            });
        Assert.True(addProperty.Changed);
        Assert.Single(breakpoints.Entries);
        Assert.Equal(BreakpointKind.Property, breakpoints.Entries[0].Kind);

        var disableAll = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsEnabledSet,
            new RemoteSetBreakpointsEnabledRequest { IsEnabled = false });
        Assert.True(disableAll.Changed);
        Assert.All(breakpoints.Entries, entry => Assert.False(entry.IsEnabled));

        var addEvent = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsEventAdd,
            new RemoteAddEventBreakpointRequest
            {
                EventName = "Click",
                EventOwnerType = "Button",
                IsGlobal = true,
            });
        Assert.True(addEvent.Changed);
        Assert.Equal(2, breakpoints.Entries.Count);
        Assert.Contains(breakpoints.Entries, entry => entry.Kind == BreakpointKind.Event);

        var clear = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsClear,
            new RemoteEmptyMutationRequest());
        Assert.True(clear.Changed);
        Assert.Empty(breakpoints.Entries);
    }

    [AvaloniaFact]
    public async Task HandleAsync_BreakpointsToggleAndRemove_Apply_SideEffects()
    {
        var (window, _) = CreateTestWindow();
        var breakpoints = new BreakpointService();
        var source = new InProcessRemoteMutationDiagnosticsSource(window, breakpointService: breakpoints);
        var router = new RemoteMutationMessageRouter(source);

        await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsPropertyAdd,
            new RemoteAddPropertyBreakpointRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PropertyName = "IsEnabled",
            });

        var entry = Assert.Single(breakpoints.Entries);
        Assert.True(entry.IsEnabled);

        var toggle = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsToggle,
            new RemoteToggleBreakpointRequest
            {
                BreakpointId = entry.Id,
                IsEnabled = false,
            });
        Assert.True(toggle.Changed);
        Assert.False(entry.IsEnabled);

        var remove = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.BreakpointsRemove,
            new RemoteRemoveBreakpointRequest
            {
                BreakpointId = entry.Id,
            });
        Assert.True(remove.Changed);
        Assert.Empty(breakpoints.Entries);
    }

    [AvaloniaFact]
    public async Task HandleAsync_Events_And_Logs_Commands_Apply_SideEffects()
    {
        var (window, _) = CreateTestWindow();
        var breakpoints = new BreakpointService();
        using var logsPage = new LogsPageViewModel();
        using var eventsPage = new EventsPageViewModel(mainViewModel: null, breakpointService: breakpoints);

        var source = new InProcessRemoteMutationDiagnosticsSource(
            root: window,
            breakpointService: breakpoints,
            eventsPageViewModel: eventsPage,
            logsPageViewModel: logsPage);
        var router = new RemoteMutationMessageRouter(source);

        var disableAllEvents = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.EventsDisableAll,
            new RemoteEmptyMutationRequest());
        Assert.True(disableAllEvents.Changed);
        Assert.Equal(0, CountEnabledEventNodes(eventsPage.Nodes));

        var enableDefaultEvents = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.EventsDefaultsEnable,
            new RemoteEmptyMutationRequest());
        Assert.True(enableDefaultEvents.Changed);
        Assert.True(CountEnabledEventNodes(eventsPage.Nodes) > 0);

        var logsSettings = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.LogsLevelsSet,
            new RemoteSetLogLevelsRequest
            {
                ShowVerbose = true,
                ShowDebug = true,
                ShowInformation = false,
                MaxEntries = 250,
            });
        Assert.True(logsSettings.Changed);
        Assert.True(logsPage.ShowVerbose);
        Assert.True(logsPage.ShowDebug);
        Assert.False(logsPage.ShowInformation);
        Assert.Equal(250, logsPage.MaxEntries);
    }

    [AvaloniaFact]
    public async Task HandleAsync_EventsNodeEnabledSet_Updates_Event_Node()
    {
        var (window, _) = CreateTestWindow();
        var breakpoints = new BreakpointService();
        using var eventsPage = new EventsPageViewModel(mainViewModel: null, breakpointService: breakpoints);
        var source = new InProcessRemoteMutationDiagnosticsSource(
            root: window,
            breakpointService: breakpoints,
            eventsPageViewModel: eventsPage);
        var router = new RemoteMutationMessageRouter(source);

        var eventNode = FindEventNode(eventsPage.Nodes, "Button", "Click");
        Assert.NotNull(eventNode);
        Assert.True(eventNode!.IsEnabled);

        var disable = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.EventsNodeEnabledSet,
            new RemoteSetEventEnabledRequest
            {
                EventName = "Click",
                EventOwnerType = "Button",
                IsEnabled = false,
            });
        Assert.True(disable.Changed);
        Assert.False(eventNode.IsEnabled);
    }

    [AvaloniaFact]
    public async Task HandleAsync_MetricsAndProfilerPause_Commands_Apply_StreamPauseState()
    {
        var (window, _) = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
                ProfilerSampleInterval = TimeSpan.FromMilliseconds(200),
            });
        using var _ = streamSource.Subscribe(static _ => { });

        var source = new InProcessRemoteMutationDiagnosticsSource(
            root: window,
            streamPauseController: streamSource);
        var router = new RemoteMutationMessageRouter(source);

        var pauseMetrics = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.MetricsPausedSet,
            new RemoteSetPausedRequest { IsPaused = true });
        Assert.True(pauseMetrics.Changed);
        Assert.True(streamSource.IsMetricsPaused);

        var pauseMetricsNoOp = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.MetricsPausedSet,
            new RemoteSetPausedRequest { IsPaused = true });
        Assert.False(pauseMetricsNoOp.Changed);

        var pauseProfiler = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.ProfilerPausedSet,
            new RemoteSetPausedRequest { IsPaused = true });
        Assert.True(pauseProfiler.Changed);
        Assert.True(streamSource.IsProfilerPaused);

        var resumeProfiler = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.ProfilerPausedSet,
            new RemoteSetPausedRequest { IsPaused = false });
        Assert.True(resumeProfiler.Changed);
        Assert.False(streamSource.IsProfilerPaused);
    }

    [AvaloniaFact]
    public async Task HandleAsync_MetricsAndProfilerSettings_Commands_Apply_StreamSettings()
    {
        var (window, _) = CreateTestWindow();
        using var streamSource = new InProcessRemoteStreamSource(
            options: InProcessRemoteStreamSourceOptions.Default with
            {
                EnableUdpTelemetryFallback = false,
            });
        using var _ = streamSource.Subscribe(static _ => { });
        var source = new InProcessRemoteMutationDiagnosticsSource(
            root: window,
            streamPauseController: streamSource);
        var router = new RemoteMutationMessageRouter(source);

        var metrics = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.MetricsSettingsSet,
            new RemoteSetMetricsSettingsRequest
            {
                MaxRetainedMeasurements = 123,
                MaxSeries = 45,
                MaxSamplesPerSeries = 67,
            });
        Assert.True(metrics.Changed);
        Assert.Equal(3, metrics.AffectedCount);

        var metricsSnapshot = streamSource.GetMetricsSnapshot(
            new RemoteMetricsSnapshotRequest
            {
                IncludeMeasurements = false,
                IncludeSeries = false,
            });
        Assert.Equal(123, metricsSnapshot.MaxRetainedMeasurements);
        Assert.Equal(45, metricsSnapshot.MaxSeries);
        Assert.Equal(67, metricsSnapshot.MaxSamplesPerSeries);

        var profiler = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.ProfilerSettingsSet,
            new RemoteSetProfilerSettingsRequest
            {
                MaxRetainedSamples = 88,
                SampleIntervalMs = 333,
            });
        Assert.True(profiler.Changed);
        Assert.Equal(2, profiler.AffectedCount);

        var profilerSnapshot = streamSource.GetProfilerSnapshot(
            new RemoteProfilerSnapshotRequest
            {
                IncludeSamples = false,
            });
        Assert.Equal(88, profilerSnapshot.MaxRetainedSamples);
        Assert.Equal(333, profilerSnapshot.SampleIntervalMs);
    }

    [AvaloniaFact]
    public async Task HandleAsync_SelectionSet_Updates_Shared_Selection_State()
    {
        var (window, _) = CreateTestWindow();
        var nodeIds = new InProcessRemoteNodeIdentityProvider();
        var selectionState = new InProcessRemoteSelectionState();
        var readOnlySource = new InProcessRemoteReadOnlyDiagnosticsSource(
            window,
            nodeIdentityProvider: nodeIds,
            selectionState: selectionState);
        var source = new InProcessRemoteMutationDiagnosticsSource(
            root: window,
            nodeIdentityProvider: nodeIds,
            selectionState: selectionState);
        var router = new RemoteMutationMessageRouter(source);

        var tree = await readOnlySource.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest { Scope = "combined" },
            CancellationToken.None);
        var buttonNode = Assert.Single(tree.Nodes, node => node.ElementName == "TestButton");

        var setSelection = await SendRequestAsync<RemoteMutationResult>(
            router,
            RemoteMutationMethods.SelectionSet,
            new RemoteSetSelectionRequest
            {
                Scope = "combined",
                NodeId = buttonNode.NodeId,
            });
        Assert.True(setSelection.Changed);
        Assert.Equal(buttonNode.NodePath, setSelection.TargetNodePath);

        var selection = await readOnlySource.GetSelectionSnapshotAsync(
            new RemoteSelectionSnapshotRequest { Scope = "combined" },
            CancellationToken.None);
        Assert.Equal(buttonNode.NodeId, selection.NodeId);
        Assert.Equal(buttonNode.NodePath, selection.NodePath);
        Assert.Equal("combined", selection.Scope);
    }

    [AvaloniaFact]
    public async Task HandleAsync_InvalidPayload_Returns_ValidationError()
    {
        var (window, _) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var response = await SendRawRequestAsync(
            router,
            RemoteMutationMethods.PropertiesSet,
            new RemoteSetPropertyRequest
            {
                Scope = "combined",
                ControlName = "TestButton",
                PropertyName = string.Empty,
                ValueText = "false",
            });

        Assert.False(response.IsSuccess);
        Assert.Equal("validation_error", response.ErrorCode);
    }

    [AvaloniaFact]
    public async Task HandleAsync_CodeDocumentOpen_WithMissingPath_Returns_ValidationError()
    {
        var (window, _) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var response = await SendRawRequestAsync(
            router,
            RemoteMutationMethods.CodeDocumentOpen,
            new RemoteCodeDocumentOpenRequest
            {
                FilePath = string.Empty,
            });

        Assert.False(response.IsSuccess);
        Assert.Equal("validation_error", response.ErrorCode);
    }

    [AvaloniaFact]
    public async Task HandleAsync_UnavailableFeature_Returns_FeatureUnavailable()
    {
        var (window, _) = CreateTestWindow();
        var source = new InProcessRemoteMutationDiagnosticsSource(window);
        var router = new RemoteMutationMessageRouter(source);

        var response = await SendRawRequestAsync(
            router,
            RemoteMutationMethods.BreakpointsClear,
            new RemoteEmptyMutationRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal("feature_unavailable", response.ErrorCode);
    }

    private static async Task<TPayload> SendRequestAsync<TPayload>(
        RemoteMutationMessageRouter router,
        string method,
        object request)
    {
        var response = await SendRawRequestAsync(router, method, request);
        Assert.True(response.IsSuccess, response.ErrorCode + ": " + response.ErrorMessage);
        var payload = JsonSerializer.Deserialize<TPayload>(response.PayloadJson, JsonOptions);
        return Assert.IsType<TPayload>(payload);
    }

    private static async Task<RemoteResponseMessage> SendRawRequestAsync(
        RemoteMutationMessageRouter router,
        string method,
        object request)
    {
        var payloadJson = JsonSerializer.Serialize(request, JsonOptions);
        var response = await router.HandleAsync(
            new DummyAttachConnection(),
            new RemoteRequestMessage(
                SessionId: Guid.NewGuid(),
                RequestId: 17,
                Method: method,
                PayloadJson: payloadJson),
            CancellationToken.None);

        return Assert.IsType<RemoteResponseMessage>(response);
    }

    private static (Window window, TestInspectableButton button) CreateTestWindow()
    {
        var button = new TestInspectableButton
        {
            Name = "TestButton",
            Content = "Click me",
            DiagnosticTag = "before",
        };

        var rootPanel = new StackPanel
        {
            Name = "RootPanel",
            Children = { button },
        };

        var window = new Window
        {
            Name = "RootWindow",
            Content = rootPanel,
        };
        return (window, button);
    }

    private sealed class TestInspectableButton : Button
    {
        public string? DiagnosticTag { get; set; }
    }

    private static EventTreeNode? FindEventNode(IEnumerable<EventTreeNodeBase> nodes, string ownerTypeName, string eventName)
    {
        foreach (var node in nodes)
        {
            var found = FindEventNode(node, ownerTypeName, eventName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static EventTreeNode? FindEventNode(EventTreeNodeBase node, string ownerTypeName, string eventName)
    {
        if (node is EventTreeNode eventNode &&
            string.Equals(eventNode.Event.OwnerType.Name, ownerTypeName, StringComparison.Ordinal) &&
            string.Equals(eventNode.Event.Name, eventName, StringComparison.Ordinal))
        {
            return eventNode;
        }

        if (node.Children is null)
        {
            return null;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            var found = FindEventNode(node.Children[i], ownerTypeName, eventName);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static int CountEnabledEventNodes(IEnumerable<EventTreeNodeBase> nodes)
    {
        return nodes.Sum(CountEnabledEventNodes);
    }

    private static int CountEnabledEventNodes(EventTreeNodeBase node)
    {
        var enabled = node.IsEnabled == true ? 1 : 0;
        if (node.Children is null)
        {
            return enabled;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            enabled += CountEnabledEventNodes(node.Children[i]);
        }

        return enabled;
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
