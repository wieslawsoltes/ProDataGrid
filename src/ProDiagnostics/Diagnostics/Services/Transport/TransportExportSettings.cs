using System;
using System.Collections.Generic;

namespace Avalonia.Diagnostics.Services;

internal sealed class TransportExportSettings
{
    public static TransportExportSettings Default { get; } = new()
    {
        Host = "127.0.0.1",
        Port = 54831,
        MaxTagsPerMessage = 32,
        HelloInterval = TimeSpan.FromSeconds(5),
        IncludeActivityTags = true,
        IncludeMetricTags = true,
        ActivitySourceNames = new[] { "*" },
        MeterNames = new[] { "*" }
    };

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 54831;

    public int MaxTagsPerMessage { get; init; } = 32;

    public TimeSpan HelloInterval { get; init; } = TimeSpan.FromSeconds(5);

    public bool IncludeActivityTags { get; init; } = true;

    public bool IncludeMetricTags { get; init; } = true;

    public IReadOnlyList<string> ActivitySourceNames { get; init; } = new[] { "*" };

    public IReadOnlyList<string> MeterNames { get; init; } = new[] { "*" };
}
