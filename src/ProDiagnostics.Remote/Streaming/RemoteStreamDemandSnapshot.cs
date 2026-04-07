using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Aggregated stream demand flags per diagnostics domain/topic.
/// </summary>
public readonly record struct RemoteStreamDemandSnapshot(
    bool Selection,
    bool Preview,
    bool Metrics,
    bool Profiler,
    bool Logs,
    bool Events)
{
    public static RemoteStreamDemandSnapshot None => new(
        Selection: false,
        Preview: false,
        Metrics: false,
        Profiler: false,
        Logs: false,
        Events: false);

    public static RemoteStreamDemandSnapshot All => new(
        Selection: true,
        Preview: true,
        Metrics: true,
        Profiler: true,
        Logs: true,
        Events: true);

    public bool IsDemanded(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        return topic switch
        {
            RemoteStreamTopics.Selection => Selection,
            RemoteStreamTopics.Preview => Preview,
            RemoteStreamTopics.Metrics => Metrics,
            RemoteStreamTopics.Profiler => Profiler,
            RemoteStreamTopics.Logs => Logs,
            RemoteStreamTopics.Events => Events,
            _ => false,
        };
    }

    public static RemoteStreamDemandSnapshot FromTopics(IEnumerable<string>? topics)
    {
        if (topics is null)
        {
            return None;
        }

        var demand = None;
        foreach (var topic in topics)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                continue;
            }

            demand = topic switch
            {
                RemoteStreamTopics.Selection => demand with { Selection = true },
                RemoteStreamTopics.Preview => demand with { Preview = true },
                RemoteStreamTopics.Metrics => demand with { Metrics = true },
                RemoteStreamTopics.Profiler => demand with { Profiler = true },
                RemoteStreamTopics.Logs => demand with { Logs = true },
                RemoteStreamTopics.Events => demand with { Events = true },
                _ => demand,
            };
        }

        return demand;
    }
}
