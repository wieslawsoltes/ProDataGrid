using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Remote;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.IntegrationTests;

[Collection("MetricsCapture")]
public class RemoteAttachPerfIntegrationTests
{
    [AvaloniaFact]
    [Trait("Category", "Perf")]
    public async Task Perf_RemoteLoopback_MixedSnapshotWorkload_CompletesWithoutTimeouts()
    {
        var root = CreateDenseWindow(1500);
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
                    ClientName = "integration-perf-client",
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    RequestTimeout = TimeSpan.FromSeconds(30),
                },
            });

        var tree = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
        {
            Scope = "combined",
            IncludeSourceLocations = false,
            IncludeVisualDetails = false,
        });
        var target = tree.Nodes.FirstOrDefault(node => node.Depth > 1) ?? tree.Nodes.Last();

        var samples = new double[30];
        for (var i = 0; i < samples.Length; i++)
        {
            var started = Stopwatch.GetTimestamp();
            _ = await session.ReadOnly.GetTreeSnapshotAsync(new RemoteTreeSnapshotRequest
            {
                Scope = "combined",
                IncludeSourceLocations = false,
                IncludeVisualDetails = false,
            });
            _ = await session.ReadOnly.GetPropertiesSnapshotAsync(new RemotePropertiesSnapshotRequest
            {
                Scope = "combined",
                NodeId = target.NodeId,
                NodePath = target.NodePath,
                IncludeClrProperties = true,
            });
            _ = await session.ReadOnly.GetStylesSnapshotAsync(new RemoteStylesSnapshotRequest
            {
                Scope = "combined",
                NodeId = target.NodeId,
                NodePath = target.NodePath,
            });
            _ = await session.ReadOnly.GetResourcesSnapshotAsync(new RemoteResourcesSnapshotRequest
            {
                IncludeEntries = true,
            });
            samples[i] = RemoteRuntimeMetrics.ElapsedMilliseconds(started);
        }

        var p95 = Percentile(samples, 0.95);
        Assert.True(p95 < 5000, "Mixed workload exceeded integration perf guardrail.");
    }

    private static double Percentile(double[] values, double percentile)
    {
        var copy = values.ToArray();
        Array.Sort(copy);
        var index = (int)Math.Ceiling(copy.Length * percentile) - 1;
        index = Math.Clamp(index, 0, copy.Length - 1);
        return copy[index];
    }

    private static Window CreateDenseWindow(int controlCount)
    {
        var stack = new StackPanel
        {
            Name = "PerfIntegrationStack",
        };

        for (var i = 0; i < controlCount; i++)
        {
            stack.Children.Add(new Border
            {
                Name = "PerfIntegrationBorder" + i,
                Child = new StackPanel
                {
                    Name = "PerfIntegrationInner" + i,
                    Children =
                    {
                        new TextBlock
                        {
                            Name = "PerfIntegrationText" + i,
                            Text = "Row " + i,
                        },
                        new TextBox
                        {
                            Name = "PerfIntegrationInput" + i,
                            Text = "Value " + i,
                        },
                    },
                },
            });
        }

        return new Window
        {
            Name = "PerfIntegrationRoot",
            Width = 1200,
            Height = 900,
            Content = new ScrollViewer
            {
                Content = stack,
            },
        };
    }
}
