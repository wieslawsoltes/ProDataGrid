using System;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class ProfilerSampleViewModel
{
    public ProfilerSampleViewModel(
        DateTimeOffset timestamp,
        double cpuPercent,
        double workingSetMb,
        double privateMemoryMb,
        double managedHeapMb,
        int gen0Collections,
        int gen1Collections,
        int gen2Collections,
        string sourceName = "",
        string activityName = "",
        double durationMs = 0,
        string process = "")
    {
        Timestamp = timestamp;
        CpuPercent = cpuPercent;
        WorkingSetMb = workingSetMb;
        PrivateMemoryMb = privateMemoryMb;
        ManagedHeapMb = managedHeapMb;
        Gen0Collections = gen0Collections;
        Gen1Collections = gen1Collections;
        Gen2Collections = gen2Collections;
        SourceName = sourceName;
        ActivityName = activityName;
        DurationMs = durationMs;
        Process = process;
    }

    public DateTimeOffset Timestamp { get; }

    public double CpuPercent { get; }

    public double WorkingSetMb { get; }

    public double PrivateMemoryMb { get; }

    public double ManagedHeapMb { get; }

    public int Gen0Collections { get; }

    public int Gen1Collections { get; }

    public int Gen2Collections { get; }

    public string SourceName { get; }

    public string ActivityName { get; }

    public double DurationMs { get; }

    public string Process { get; }
}
