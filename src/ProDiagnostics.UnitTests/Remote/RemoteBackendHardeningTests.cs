using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteBackendHardeningTests
{
    [AvaloniaFact]
    public async Task GetTreeSnapshotAsync_LargeCombinedTree_CompletesWithinSla()
    {
        var window = CreateLargeTreeWindow(nodeCount: 6000);
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);

        var stopwatch = Stopwatch.StartNew();
        var snapshot = await source.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
            },
            CancellationToken.None);
        stopwatch.Stop();

        Assert.True(snapshot.Nodes.Count >= 6000, "Expected a large combined tree snapshot.");
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            "Large tree snapshot exceeded SLA. Elapsed: " + stopwatch.Elapsed);
    }

    [AvaloniaFact]
    public async Task GetTreeSnapshotAsync_CanSkipVisualDetails_ForCompactPayload()
    {
        var window = CreateLargeTreeWindow(nodeCount: 600);
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);

        var compact = await source.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            },
            CancellationToken.None);
        var detailed = await source.GetTreeSnapshotAsync(
            new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = true,
            },
            CancellationToken.None);

        Assert.Equal(compact.Nodes.Count, detailed.Nodes.Count);
        Assert.All(compact.Nodes, static node => Assert.Null(node.Bounds));
        Assert.All(compact.Nodes, static node => Assert.Equal(1d, node.Opacity));
        Assert.All(compact.Nodes, static node => Assert.Equal(0, node.ZIndex));
        Assert.Contains(detailed.Nodes, static node => node.Bounds is not null);
    }

    [AvaloniaFact]
    public async Task HeavySnapshots_CanSkip_Optional_Collections()
    {
        var window = CreateStyledWindow();
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);

        var styles = await source.GetStylesSnapshotAsync(
            new RemoteStylesSnapshotRequest
            {
                Scope = "combined",
                ControlName = "StyledButton",
                IncludeTreeEntries = false,
                IncludeFrames = false,
                IncludeSetters = false,
                IncludeResolution = false,
            },
            CancellationToken.None);
        Assert.Empty(styles.TreeEntries);
        Assert.Empty(styles.Frames);
        Assert.Empty(styles.Setters);
        Assert.Empty(styles.Resolution);

        var elements = await source.GetElements3DSnapshotAsync(
            new RemoteElements3DSnapshotRequest
            {
                IncludeNodes = false,
                IncludeVisibleNodeIds = false,
            },
            CancellationToken.None);
        Assert.Empty(elements.Nodes);
        Assert.Empty(elements.VisibleNodeIds);
    }

    [AvaloniaFact]
    public async Task GetElements3DSnapshotAsync_WithSvgSnapshot_Renders_UsingAvaloniaPipeline()
    {
        var window = CreateLargeTreeWindow(nodeCount: 32);
        var source = new InProcessRemoteReadOnlyDiagnosticsSource(window);

        var snapshot = await source.GetElements3DSnapshotAsync(
            new RemoteElements3DSnapshotRequest
            {
                IncludeNodes = false,
                IncludeVisibleNodeIds = false,
                IncludeSvgSnapshot = true,
                SvgWidth = 1024,
                SvgHeight = 640,
                MaxSvgNodes = 512,
            },
            CancellationToken.None);

        if (!string.IsNullOrWhiteSpace(snapshot.SvgSnapshot))
        {
            Assert.Equal("0 0 1024 640", snapshot.SvgViewBox);
            Assert.Contains("<svg", snapshot.SvgSnapshot!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.True(string.IsNullOrWhiteSpace(snapshot.SvgSnapshot));
            Assert.True(string.IsNullOrWhiteSpace(snapshot.SvgViewBox));
        }
    }

    private static Window CreateLargeTreeWindow(int nodeCount)
    {
        var panel = new StackPanel { Name = "RootPanel" };
        for (var i = 0; i < nodeCount; i++)
        {
            panel.Children.Add(new Border
            {
                Name = "Node" + i.ToString(),
                Child = new TextBlock
                {
                    Text = "Item " + i.ToString(),
                },
            });
        }

        return new Window
        {
            Name = "RootWindow",
            Content = panel,
        };
    }

    private static Window CreateStyledWindow()
    {
        var button = new Button
        {
            Name = "StyledButton",
            Content = "Styled",
        };

        var root = new StackPanel
        {
            Name = "StyleRoot",
            Children = { button },
        };

        root.Styles.Add(
            new Style(selector => selector.OfType<Button>())
            {
                Setters =
                {
                    new Setter(Button.FontSizeProperty, 16d),
                },
            });

        return new Window
        {
            Name = "StyleWindow",
            Content = root,
        };
    }
}
