using System;
using System.Collections.Generic;

namespace ProDiagnostics.Transport;

public sealed class DiagnosticsUdpOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = TelemetryProtocol.DefaultPort;
    public int MaxTagsPerMessage { get; set; } = TelemetryProtocol.DefaultMaxTags;
    public TimeSpan HelloInterval { get; set; } = TimeSpan.FromSeconds(5);
    public bool IncludeActivityTags { get; set; } = true;
    public bool IncludeMetricTags { get; set; } = true;

    public IReadOnlyList<string> ActivitySourceNames { get; set; } = new[] { "*" };
    public IReadOnlyList<string> MeterNames { get; set; } = new[] { "*" };
}
