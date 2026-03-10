namespace Avalonia.Diagnostics.Remote;

/// <summary>
/// Well-known stream topics used by remote diagnostics push channels.
/// </summary>
public static class RemoteStreamTopics
{
    public const string Selection = "diagnostics.stream.selection";
    public const string Preview = "diagnostics.stream.preview";
    public const string Metrics = "diagnostics.stream.metrics";
    public const string Profiler = "diagnostics.stream.profiler";
    public const string Logs = "diagnostics.stream.logs";
    public const string Events = "diagnostics.stream.events";
}
