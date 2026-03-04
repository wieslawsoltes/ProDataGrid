using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Headless.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.Diagnostics.ManagedDevTools.PerfTests;

public sealed class RemoteManagedPerfTests
{
    private readonly ITestOutputHelper _output;

    public RemoteManagedPerfTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    [Trait("Category", "Perf")]
    public async Task Perf_RemoteLoopback_TreeSnapshotP95_RemainsBounded()
    {
        var root = CreateDenseWindow(1200);
        await using var session = await DevToolsRemoteLoopbackSession.StartAsync(
            root,
            new DevToolsRemoteLoopbackOptions
            {
                UseDynamicPort = true,
                HostOptions = new DevToolsRemoteAttachHostOptions
                {
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
                ClientOptions = new RemoteDiagnosticsClientOptions
                {
                    ClientName = "managed-perf-tests",
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
            });

        _ = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
            IncludeSourceLocations = false,
            IncludeVisualDetails = false,
        });

        var samples = new double[20];
        for (var i = 0; i < samples.Length; i++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            });
            samples[i] = RemoteRuntimeMetrics.ElapsedMilliseconds(started);
        }

        var p95 = Percentile(samples, 0.95);
        var max = samples.Max();
        _output.WriteLine("Tree snapshot p95(ms): " + p95.ToString("F2"));
        _output.WriteLine("Tree snapshot max(ms): " + max.ToString("F2"));

        Assert.True(p95 < 2500, "Tree snapshot p95 exceeded expected perf guardrail.");
    }

    [AvaloniaFact]
    [Trait("Category", "Perf")]
    public async Task Perf_RemoteLoopback_PropertiesSnapshotP95_RemainsBounded()
    {
        var root = CreateDenseWindow(1200);
        await using var session = await DevToolsRemoteLoopbackSession.StartAsync(
            root,
            new DevToolsRemoteLoopbackOptions
            {
                UseDynamicPort = true,
            });

        var tree = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
            IncludeSourceLocations = false,
            IncludeVisualDetails = false,
        });
        var target = tree.Nodes.FirstOrDefault(x => x.Depth > 1) ?? tree.Nodes.Last();

        _ = await session.ReadOnly.GetPropertiesSnapshotAsync(new RemotePropertiesSnapshotRequest
        {
            Scope = "combined",
            NodeId = target.NodeId,
            NodePath = target.NodePath,
            IncludeClrProperties = true,
        });

        var samples = new double[20];
        for (var i = 0; i < samples.Length; i++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = await session.ReadOnly.GetPropertiesSnapshotAsync(new RemotePropertiesSnapshotRequest
            {
                Scope = "combined",
                NodeId = target.NodeId,
                NodePath = target.NodePath,
                IncludeClrProperties = true,
            });
            samples[i] = RemoteRuntimeMetrics.ElapsedMilliseconds(started);
        }

        var p95 = Percentile(samples, 0.95);
        var max = samples.Max();
        _output.WriteLine("Properties snapshot p95(ms): " + p95.ToString("F2"));
        _output.WriteLine("Properties snapshot max(ms): " + max.ToString("F2"));

        Assert.True(p95 < 2500, "Properties snapshot p95 exceeded expected perf guardrail.");
    }

    private static double Percentile(double[] samples, double percentile)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        var ordered = (double[])samples.Clone();
        Array.Sort(ordered);
        var index = (int)Math.Ceiling(ordered.Length * percentile) - 1;
        index = Math.Clamp(index, 0, ordered.Length - 1);
        return ordered[index];
    }

    private static Window CreateDenseWindow(int controlCount)
    {
        var stack = new StackPanel
        {
            Name = "PerfStack",
        };

        for (var index = 0; index < controlCount; index++)
        {
            stack.Children.Add(new Border
            {
                Name = "PerfBorder" + index,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Name = "PerfText" + index,
                            Text = "Item " + index,
                        },
                        new TextBox
                        {
                            Name = "PerfInput" + index,
                            Text = "Value " + index,
                        },
                    },
                },
            });
        }

        return new Window
        {
            Name = "PerfRootWindow",
            Width = 1200,
            Height = 900,
            Content = new ScrollViewer
            {
                Content = stack,
            },
        };
    }
}
