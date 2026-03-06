using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class DevToolsStandaloneWindowFactoryTests
{
    private sealed class MarkerWindow : Window, IDevToolsHostSurface
    {
    }

    [AvaloniaFact]
    public void CreateRemoteWindow_Binds_MainViewModel()
    {
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            new Uri("ws://127.0.0.1:29414/attach/"),
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        Assert.IsType<MainViewModel>(mainWindow.DataContext);

        window.Close();
    }

    [AvaloniaFact]
    public void CreateRemoteWindow_WithDiagnosticsRoot_Binds_MainViewModel()
    {
        var diagnosticsRoot = new Window();
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            new Uri("ws://127.0.0.1:29414/attach/"),
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
            },
            diagnosticsRoot);

        var mainWindow = Assert.IsType<MainWindow>(window);
        Assert.IsType<MainViewModel>(mainWindow.DataContext);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_Loads_RemoteTree_From_AttachHost()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            host.WebSocketEndpoint,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        TreePageViewModel? tree = null;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                return tree.Nodes.Length > 0;
            },
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotNull(tree);
        Assert.NotEmpty(tree!.Nodes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_Loads_RemoteTree_When_Target_Contains_Control_Managed_PseudoClasses()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new StackPanel
            {
                Name = "RemoteStack",
                Children =
                {
                    new TextBox
                    {
                        Name = "EmptyInput",
                        Text = string.Empty,
                    },
                },
            },
        };

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

        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            host.WebSocketEndpoint,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        TreePageViewModel? tree = null;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                return tree.Nodes.Length > 0 && tree.SelectedNode is not null;
            },
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotNull(tree);
        Assert.NotNull(tree!.SelectedNode);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_With_Preconnected_Session_Loads_RemoteTree_From_AttachHost()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var session = await DevToolsRemoteClientSession.ConnectAsync(host.WebSocketEndpoint);
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            session,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);

        TreePageViewModel? tree = null;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                return tree.Nodes.Length > 0;
            },
            timeout: TimeSpan.FromSeconds(5));

        Assert.NotNull(tree);
        Assert.NotEmpty(tree!.Nodes);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_With_Preconnected_Session_Renders_RemoteTree_Content()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var session = await DevToolsRemoteClientSession.ConnectAsync(host.WebSocketEndpoint);
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            session,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        Control? treeGrid = null;
        TreePageViewModel? tree = null;
        var lastTreeNodeCount = -1;
        var lastSelectionType = string.Empty;
        var lastGridFound = false;
        var lastActualWidth = -1d;
        var lastGridWidth = -1d;
        var lastGridHeight = -1d;
        var lastVisualTypes = string.Empty;
        var lastTexts = string.Empty;
        var lastTextWidth = -1d;
        var lastTextHeight = -1d;
        var lastTextForeground = string.Empty;
        var lastRightContentType = string.Empty;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                lastTreeNodeCount = tree.Nodes.Length;
                lastSelectionType = tree.SelectedNode?.Type ?? "(null)";
                lastRightContentType = vm.RightContent?.GetType().Name ?? "(null)";
                treeGrid = mainWindow.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(x => string.Equals(x.Name, "tree", StringComparison.Ordinal));
                lastGridFound = treeGrid is not null;
                if (treeGrid is not null)
                {
                    lastGridWidth = treeGrid.Bounds.Width;
                    lastGridHeight = treeGrid.Bounds.Height;
                    lastVisualTypes = string.Join(
                        ",",
                        treeGrid.GetVisualDescendants()
                            .Select(x => x.GetType().Name)
                            .Distinct(StringComparer.Ordinal)
                            .Take(12));
                    lastTexts = string.Join(
                        "|",
                        treeGrid.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Select(x => x.Text)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
                            .Take(12));
                    var firstTextBlock = treeGrid.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
                    if (firstTextBlock is not null)
                    {
                        lastTextWidth = firstTextBlock.Bounds.Width;
                        lastTextHeight = firstTextBlock.Bounds.Height;
                        lastTextForeground = DescribeBrush(firstTextBlock.Foreground);
                    }
                }
                return tree is not null &&
                    tree.SelectedNode is not null &&
                    string.Equals(lastRightContentType, nameof(ControlDetailsViewModel), StringComparison.Ordinal) &&
                    lastTexts.Contains("Window", StringComparison.Ordinal);
            },
            timeout: TimeSpan.FromSeconds(5),
            failureMessageFactory: () =>
                $"treeNodes={lastTreeNodeCount}, selected={lastSelectionType}, rightContent={lastRightContentType}, gridFound={lastGridFound}, gridBounds={lastGridWidth}x{lastGridHeight}, actualWidth={lastActualWidth}, textBounds={lastTextWidth}x{lastTextHeight}, textForeground={lastTextForeground}, visuals=[{lastVisualTypes}], texts=[{lastTexts}]");

        Assert.NotNull(tree);
        Assert.NotNull(treeGrid);
        Assert.NotNull(tree!.SelectedNode);
        Assert.IsType<ControlDetailsViewModel>(vm.RightContent);
        Assert.Contains("Window", lastTexts, StringComparison.Ordinal);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_With_Preloaded_Bootstrap_Snapshots_Seeds_Tree_And_Properties()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var session = await DevToolsRemoteClientSession.ConnectAsync(host.WebSocketEndpoint);
        var initialTree = await session.Domains.ReadOnly.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
            });
        var initialSelection = await session.Domains.ReadOnly.GetSelectionSnapshotAsync(
            new RemoteSelectionSnapshotRequest
            {
                Scope = "combined",
            });

        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            session,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            },
            initialCombinedTreeSnapshot: initialTree,
            initialCombinedSelectionSnapshot: initialSelection);

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        Control? treeGrid = null;
        TreePageViewModel? tree = null;
        var lastTexts = string.Empty;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                treeGrid = mainWindow.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(x => string.Equals(x.Name, "tree", StringComparison.Ordinal));
                lastTexts = treeGrid is null
                    ? string.Empty
                    : string.Join(
                        "|",
                        treeGrid.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Select(x => x.Text)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
                            .Take(12));

                return tree is not null &&
                    tree.SelectedNode is not null &&
                    vm.RightContent is ControlDetailsViewModel &&
                    lastTexts.Contains("Window", StringComparison.Ordinal);
            },
            timeout: TimeSpan.FromSeconds(5),
            failureMessageFactory: () =>
                $"texts=[{lastTexts}] rightContent={vm.RightContent?.GetType().Name ?? "(null)"} nodes={tree?.Nodes.Length ?? -1}");

        Assert.NotNull(tree);
        Assert.NotNull(treeGrid);
        Assert.NotEmpty(tree!.Nodes);
        Assert.NotNull(tree.SelectedNode);
        Assert.IsType<ControlDetailsViewModel>(vm.RightContent);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_With_Empty_Preloaded_TreeSnapshot_Refreshes_Live_RemoteTree()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var session = await DevToolsRemoteClientSession.ConnectAsync(host.WebSocketEndpoint);
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            session,
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
                ConnectOnStartup = true,
            },
            initialCombinedTreeSnapshot: new RemoteTreeSnapshot(
                SnapshotVersion: 1,
                Generation: 0,
                Scope: "combined",
                Nodes: Array.Empty<RemoteTreeNodeSnapshot>()),
            initialCombinedSelectionSnapshot: new RemoteSelectionSnapshot(
                SnapshotVersion: 1,
                Generation: 0,
                Scope: "combined",
                NodeId: null,
                NodePath: null,
                Target: null,
                TargetType: null));

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        TreePageViewModel? tree = null;
        var lastTexts = string.Empty;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                var treeGrid = mainWindow.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(x => string.Equals(x.Name, "tree", StringComparison.Ordinal));
                lastTexts = treeGrid is null
                    ? string.Empty
                    : string.Join(
                        "|",
                        treeGrid.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Select(x => x.Text)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
                            .Take(12));

                return tree is not null &&
                    tree.Nodes.Length > 0 &&
                    tree.SelectedNode is not null &&
                    vm.RightContent is ControlDetailsViewModel &&
                    lastTexts.Contains("Window", StringComparison.Ordinal);
            },
            timeout: TimeSpan.FromSeconds(5),
            failureMessageFactory: () => $"texts=[{lastTexts}] rightContent={vm.RightContent?.GetType().Name ?? "(null)"} nodes={tree?.Nodes.Length ?? -1}");

        window.Close();
    }

    [AvaloniaFact]
    public void DoesBelongToDevTool_ReturnsTrue_For_Marked_Standalone_Surface()
    {
        var window = new MarkerWindow
        {
            Content = new TextBox
            {
                Name = "ConnectionInput",
            },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var textBox = Assert.IsType<TextBox>(window.Content);
        Assert.True(textBox.DoesBelongToDevTool());

        window.Close();
    }

    [AvaloniaFact]
    public void CreateRemoteWindow_RemoteNoFallback_Disables_LocalInspectFallback()
    {
        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            new Uri("ws://127.0.0.1:29414/attach/"),
            new DevToolsOptions
            {
                UseRemoteRuntime = true,
                DisableLocalFallbackInRemoteRuntime = true,
            });

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        Assert.False(vm.AllowLocalInspectFallback);

        window.Close();
    }

    [AvaloniaFact]
    public async Task CreateRemoteWindow_DeferredConnect_Loads_RemoteTree_After_Reapplying_Options()
    {
        var root = new Window
        {
            Name = "RemoteRoot",
            Content = new Grid
            {
                Name = "RemoteGrid",
                Children =
                {
                    new TextBlock
                    {
                        Name = "RemoteText",
                        Text = "Hello remote devtools",
                    },
                },
            },
        };

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

        var displayOptions = new DevToolsOptions
        {
            UseRemoteRuntime = true,
            DisableLocalFallbackInRemoteRuntime = true,
            ConnectOnStartup = false,
        };

        var connectOptions = new DevToolsOptions
        {
            UseRemoteRuntime = true,
            DisableLocalFallbackInRemoteRuntime = true,
            ConnectOnStartup = true,
            RemoteRuntimeEndpoint = host.WebSocketEndpoint,
        };

        var window = DevToolsStandaloneWindowFactory.CreateRemoteWindow(
            host.WebSocketEndpoint,
            displayOptions);

        var mainWindow = Assert.IsType<MainWindow>(window);
        var vm = Assert.IsType<MainViewModel>(mainWindow.DataContext);
        window.Show();

        Dispatcher.UIThread.RunJobs();
        var initialTree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
        Assert.Empty(initialTree.Nodes);
        Assert.Null(vm.RightContent);

        mainWindow.SetOptions(connectOptions);

        TreePageViewModel? tree = null;
        var lastTexts = string.Empty;
        await WaitUntilAsync(
            () =>
            {
                Dispatcher.UIThread.RunJobs();
                tree = Assert.IsType<TreePageViewModel>(vm.TreeContent);
                var treeGrid = mainWindow.GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(x => string.Equals(x.Name, "tree", StringComparison.Ordinal));
                lastTexts = treeGrid is null
                    ? string.Empty
                    : string.Join(
                        "|",
                        treeGrid.GetVisualDescendants()
                            .OfType<TextBlock>()
                            .Select(x => x.Text)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.Ordinal)
                            .Take(12));

                return tree is not null &&
                    tree.SelectedNode is not null &&
                    vm.RightContent is ControlDetailsViewModel &&
                    lastTexts.Contains("Window", StringComparison.Ordinal);
            },
            timeout: TimeSpan.FromSeconds(5),
            failureMessageFactory: () => $"texts=[{lastTexts}] rightContent={vm.RightContent?.GetType().Name ?? "(null)"} nodes={tree?.Nodes.Length ?? -1}");

        window.Close();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, Func<string>? failureMessageFactory = null)
    {
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < timeout)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(predicate(), failureMessageFactory?.Invoke() ?? "Condition was not reached within timeout.");
    }

    private static int AllocateTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static bool TryGetFirstColumnActualWidth(Control grid, out double actualWidth)
    {
        actualWidth = 0;
        var columnsProperty = grid.GetType().GetProperty("Columns");
        if (columnsProperty?.GetValue(grid) is not IList columns || columns.Count == 0 || columns[0] is null)
        {
            return false;
        }

        var widthProperty = columns[0]!.GetType().GetProperty("ActualWidth");
        if (widthProperty?.GetValue(columns[0]) is not double width)
        {
            return false;
        }

        actualWidth = width;
        return true;
    }

    private static string DescribeBrush(IBrush? brush)
    {
        return brush switch
        {
            ISolidColorBrush solid => solid.Color.ToString(),
            null => "(null)",
            _ => brush.GetType().Name
        };
    }
}
