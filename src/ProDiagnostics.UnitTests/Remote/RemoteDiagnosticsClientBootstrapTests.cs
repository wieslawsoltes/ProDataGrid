using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

[Collection("MetricsCapture")]
public class RemoteDiagnosticsClientBootstrapTests
{
    [AvaloniaFact]
    public async Task RemoteDiagnosticsClient_CanBootstrap_AllSnapshotDomains_From_AttachHost()
    {
        var port = AllocateTcpPort();
        var hostOptions = new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = port,
                Path = "/attach",
                BindingMode = HttpAttachBindingMode.Localhost,
                ReceiveTimeout = TimeSpan.FromSeconds(10),
                ServerOptions = RemoteProtocol.DefaultServerOptions with
                {
                    HeartbeatInterval = TimeSpan.FromSeconds(30),
                },
            },
            EnableMutationApi = true,
            EnableStreamingApi = true,
            RequestTimeout = TimeSpan.FromSeconds(10),
        };

        var root = new Window
        {
            Name = "RootWindow",
            Content = new Grid
            {
                Name = "RootGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "TitleText",
                        Text = "Remote bootstrap",
                    },
                },
            },
        };

        await using var host = new DevToolsRemoteAttachHost(root, hostOptions);
        await host.StartAsync();
        await using IRemoteDiagnosticsClient client = new RemoteDiagnosticsClient();
        await client.ConnectAsync(
            host.WebSocketEndpoint,
            new RemoteDiagnosticsClientOptions
            {
                ClientName = "bootstrap-tests",
                ConnectTimeout = TimeSpan.FromSeconds(8),
                RequestTimeout = TimeSpan.FromSeconds(10),
            });

        Assert.True(client.IsConnected);
        Assert.Contains("trees", client.EnabledFeatures, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("properties", client.EnabledFeatures, StringComparer.OrdinalIgnoreCase);

        var domains = new RemoteDiagnosticsDomainServices(client);

        var tree = await domains.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
            IncludeSourceLocations = true,
        });
        Assert.True(tree.SnapshotVersion > 0);
        Assert.NotEmpty(tree.Nodes);

        var firstNode = tree.Nodes[0];

        var selection = await domains.ReadOnly.GetSelectionSnapshotAsync(new RemoteSelectionSnapshotRequest
        {
            Scope = "combined",
        });
        Assert.True(selection.SnapshotVersion > 0);

        var setSelection = await domains.Mutation.SetSelectionAsync(new RemoteSetSelectionRequest
        {
            Scope = "combined",
            NodeId = firstNode.NodeId,
            NodePath = firstNode.NodePath,
        });
        Assert.Equal(RemoteMutationMethods.SelectionSet, setSelection.Operation);

        var properties = await domains.ReadOnly.GetPropertiesSnapshotAsync(new RemotePropertiesSnapshotRequest
        {
            Scope = "combined",
            NodeId = firstNode.NodeId,
            NodePath = firstNode.NodePath,
            IncludeClrProperties = true,
        });
        Assert.True(properties.SnapshotVersion > 0);

        var code = await domains.ReadOnly.GetCodeDocumentsSnapshotAsync(new RemoteCodeDocumentsRequest
        {
            Scope = "combined",
            NodeId = firstNode.NodeId,
            NodePath = firstNode.NodePath,
        });
        Assert.True(code.SnapshotVersion > 0);

        if (code.Documents.Count > 0)
        {
            var firstDocument = code.Documents[0];
            var resolve = await domains.ReadOnly.ResolveCodeNodeAsync(new RemoteCodeResolveNodeRequest
            {
                Scope = "combined",
                FilePath = firstDocument.FilePath,
                Line = Math.Max(1, firstDocument.Line),
                Column = Math.Max(0, firstDocument.Column),
            });
            Assert.True(resolve.SnapshotVersion > 0);
        }

        var bindings = await domains.ReadOnly.GetBindingsSnapshotAsync(new RemoteBindingsSnapshotRequest
        {
            Scope = "combined",
            NodeId = firstNode.NodeId,
            NodePath = firstNode.NodePath,
        });
        Assert.True(bindings.SnapshotVersion > 0);

        var styles = await domains.ReadOnly.GetStylesSnapshotAsync(new RemoteStylesSnapshotRequest
        {
            Scope = "combined",
            NodeId = firstNode.NodeId,
            NodePath = firstNode.NodePath,
        });
        Assert.True(styles.SnapshotVersion > 0);

        var resources = await domains.ReadOnly.GetResourcesSnapshotAsync(new RemoteResourcesSnapshotRequest
        {
            IncludeEntries = true,
        });
        Assert.True(resources.SnapshotVersion > 0);

        var assets = await domains.ReadOnly.GetAssetsSnapshotAsync();
        Assert.True(assets.SnapshotVersion > 0);

        var eventsSnapshot = await domains.ReadOnly.GetEventsSnapshotAsync(new RemoteEventsSnapshotRequest
        {
            Scope = "combined",
            IncludeRecordedEvents = true,
        });
        Assert.True(eventsSnapshot.SnapshotVersion > 0);

        var breakpoints = await domains.ReadOnly.GetBreakpointsSnapshotAsync(new RemoteBreakpointsSnapshotRequest
        {
            Scope = "combined",
        });
        Assert.True(breakpoints.SnapshotVersion > 0);

        var logs = await domains.ReadOnly.GetLogsSnapshotAsync(new RemoteLogsSnapshotRequest
        {
            IncludeEntries = true,
        });
        Assert.True(logs.SnapshotVersion > 0);

        var metrics = await domains.ReadOnly.GetMetricsSnapshotAsync(new RemoteMetricsSnapshotRequest
        {
            IncludeMeasurements = true,
            IncludeSeries = true,
        });
        Assert.True(metrics.SnapshotVersion > 0);

        var profiler = await domains.ReadOnly.GetProfilerSnapshotAsync(new RemoteProfilerSnapshotRequest
        {
            IncludeSamples = true,
        });
        Assert.True(profiler.SnapshotVersion > 0);

        var elements3D = await domains.ReadOnly.GetElements3DSnapshotAsync(new RemoteElements3DSnapshotRequest
        {
            IncludeNodes = true,
            IncludeVisibleNodeIds = true,
        });
        Assert.True(elements3D.SnapshotVersion > 0);

        var overlay = await domains.ReadOnly.GetOverlayOptionsSnapshotAsync();
        Assert.True(overlay.SnapshotVersion > 0);

        var toggleOverlay = await domains.Mutation.SetOverlayLiveHoverAsync(new RemoteSetOverlayLiveHoverRequest
        {
            IsEnabled = !overlay.LiveHoverEnabled,
        });
        Assert.Equal(RemoteMutationMethods.OverlayLiveHoverSet, toggleOverlay.Operation);
    }

    [AvaloniaFact]
    public async Task DevToolsRemoteLoopbackSession_Creates_Working_Local_Client()
    {
        var root = new Window
        {
            Name = "LoopbackRoot",
            Content = new Border
            {
                Name = "LoopbackBorder",
                Child = new TextBlock
                {
                    Text = "Loopback",
                },
            },
        };

        await using var session = await DevToolsRemoteLoopbackSession.StartAsync(
            root,
            new DevToolsRemoteLoopbackOptions
            {
                UseDynamicPort = true,
                HostOptions = new DevToolsRemoteAttachHostOptions
                {
                    EnableMutationApi = true,
                    EnableStreamingApi = true,
                    RequestTimeout = TimeSpan.FromSeconds(10),
                },
                ClientOptions = new RemoteDiagnosticsClientOptions
                {
                    ClientName = "loopback-bootstrap",
                    ConnectTimeout = TimeSpan.FromSeconds(8),
                    RequestTimeout = TimeSpan.FromSeconds(10),
                },
            });

        Assert.True(session.Client.IsConnected);
        var tree = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
        });
        Assert.NotEmpty(tree.Nodes);
    }

    [AvaloniaFact]
    public async Task RemoteDiagnosticsClient_Selection_And_Overlay_Render_On_Target_Window()
    {
        var target = new TextBlock
        {
            Name = "OverlayTarget",
            Text = "Overlay target",
        };
        var root = new Window
        {
            Name = "OverlayRoot",
            Width = 640,
            Height = 480,
            Content = new VisualLayerManager
            {
                Child = new Grid
                {
                    Children =
                    {
                        target,
                    },
                },
            },
        };
        root.Show();
        Dispatcher.UIThread.RunJobs();
        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return AdornerLayer.GetAdornerLayer(target) is not null;
        });

        var port = AllocateTcpPort();
        await using var host = new DevToolsRemoteAttachHost(
            root,
            new DevToolsRemoteAttachHostOptions
            {
                HttpOptions = HttpAttachServerOptions.Default with
                {
                    Port = port,
                    Path = "/attach",
                    BindingMode = HttpAttachBindingMode.Localhost,
                    ReceiveTimeout = TimeSpan.FromSeconds(10),
                    ServerOptions = RemoteProtocol.DefaultServerOptions with
                    {
                        HeartbeatInterval = TimeSpan.FromSeconds(30),
                    },
                },
                EnableMutationApi = true,
                EnableStreamingApi = true,
                RequestTimeout = TimeSpan.FromSeconds(10),
            });
        await host.StartAsync();
        root.Show();
        Dispatcher.UIThread.RunJobs();

        await using IRemoteDiagnosticsClient client = new RemoteDiagnosticsClient();
        await client.ConnectAsync(
            host.WebSocketEndpoint,
            new RemoteDiagnosticsClientOptions
            {
                ClientName = "overlay-tests",
                ConnectTimeout = TimeSpan.FromSeconds(8),
                RequestTimeout = TimeSpan.FromSeconds(10),
            });

        var domains = new RemoteDiagnosticsDomainServices(client);
        await domains.Mutation.SetSelectionAsync(
            new RemoteSetSelectionRequest
            {
                Scope = "combined",
                ControlName = "OverlayTarget",
            });
        await domains.Mutation.SetOverlayOptionsAsync(
            new RemoteSetOverlayOptionsRequest
            {
                HighlightElements = true,
                VisualizeMarginPadding = true,
            });

        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return AdornerLayer.GetAdornerLayer(target)?.Children.Count > 0;
        });

        await domains.Mutation.SetOverlayOptionsAsync(
            new RemoteSetOverlayOptionsRequest
            {
                HighlightElements = false,
            });

        await WaitUntilAsync(() =>
        {
            Dispatcher.UIThread.RunJobs();
            return AdornerLayer.GetAdornerLayer(target)?.Children.Count == 0;
        });

        root.Close();
    }

    [AvaloniaFact]
    public async Task RemoteDiagnosticsClient_Streams_Full_Event_Chain_Metadata()
    {
        var port = AllocateTcpPort();
        var button = new Button
        {
            Name = "RemoteButton",
            Content = "Click me",
        };

        var root = new Window
        {
            Name = "EventsRoot",
            Content = new StackPanel
            {
                Name = "EventsPanel",
                Children =
                {
                    button,
                },
            },
        };

        await using var host = new DevToolsRemoteAttachHost(
            root,
            new DevToolsRemoteAttachHostOptions
            {
                HttpOptions = HttpAttachServerOptions.Default with
                {
                    Port = port,
                    Path = "/attach",
                    BindingMode = HttpAttachBindingMode.Localhost,
                    ReceiveTimeout = TimeSpan.FromSeconds(10),
                    ServerOptions = RemoteProtocol.DefaultServerOptions with
                    {
                        HeartbeatInterval = TimeSpan.FromSeconds(30),
                    },
                },
                EnableMutationApi = true,
                EnableStreamingApi = true,
                RequestTimeout = TimeSpan.FromSeconds(10),
            });
        await host.StartAsync();

        await using IRemoteDiagnosticsClient client = new RemoteDiagnosticsClient();
        await client.ConnectAsync(
            host.WebSocketEndpoint,
            new RemoteDiagnosticsClientOptions
            {
                ClientName = "events-stream-tests",
                ConnectTimeout = TimeSpan.FromSeconds(8),
                RequestTimeout = TimeSpan.FromSeconds(10),
            });

        var domains = new RemoteDiagnosticsDomainServices(client);
        var receivedEvent = new TaskCompletionSource<RemoteEventStreamPayload>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = domains.Stream.Subscribe(
            RemoteStreamTopics.Events,
            RemoteJsonSerializerContext.Default.RemoteEventStreamPayload,
            payload =>
            {
                if (payload.IsParsed &&
                    payload.Payload is { } eventPayload &&
                    string.Equals(eventPayload.EventName, Button.ClickEvent.Name, StringComparison.Ordinal))
                {
                    receivedEvent.TrySetResult(eventPayload);
                }
            });

        await Dispatcher.UIThread.InvokeAsync(() => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)));

        var streamedEvent = await receivedEvent.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(streamedEvent.ChainLength >= 2);
        Assert.Equal(streamedEvent.ChainLength, streamedEvent.EventChain.Count);
        Assert.Contains(streamedEvent.EventChain, link => !string.IsNullOrWhiteSpace(link.NodePath));
        Assert.Equal(Button.ClickEvent.OwnerType.FullName ?? Button.ClickEvent.OwnerType.Name, streamedEvent.EventOwnerType);

        root.Close();
    }

    private static int AllocateTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(predicate(), "Condition was not satisfied before timeout.");
    }
}
