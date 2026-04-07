using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ProDiagnostics.Transport;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class ProfilerPageViewModelTests
{
    [AvaloniaFact]
    public void RefreshNow_Adds_Sample_And_Updates_Summary()
    {
        var sampler = new FakeProfilerSampler(
            new ProfilerSampleSnapshot(
                DateTimeOffset.UtcNow,
                CpuPercent: 14.5,
                WorkingSetMb: 256.0,
                PrivateMemoryMb: 300.0,
                ManagedHeapMb: 88.0,
                Gen0Collections: 1,
                Gen1Collections: 0,
                Gen2Collections: 0));

        using var viewModel = new ProfilerPageViewModel(sampler, startSampling: false);

        viewModel.RefreshNow();

        Assert.Equal(1, viewModel.SampleCount);
        Assert.Equal(14.5, viewModel.CurrentCpuPercent, 3);
        Assert.Equal(14.5, viewModel.PeakCpuPercent, 3);
        Assert.Equal(256.0, viewModel.CurrentWorkingSetMb, 3);
        Assert.Equal(256.0, viewModel.PeakWorkingSetMb, 3);
        Assert.Equal(88.0, viewModel.CurrentManagedHeapMb, 3);
    }

    [AvaloniaFact]
    public void MaxSamples_Trims_Oldest_Samples()
    {
        var sampler = new FakeProfilerSampler(
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 1, 100, 120, 30, 0, 0, 0),
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 2, 110, 130, 31, 0, 0, 0),
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 3, 120, 140, 32, 0, 0, 0));

        using var viewModel = new ProfilerPageViewModel(sampler, startSampling: false)
        {
            MaxSamples = 2
        };

        viewModel.RefreshNow();
        viewModel.RefreshNow();
        viewModel.RefreshNow();

        Assert.Equal(2, viewModel.SampleCount);
        Assert.Equal(3, viewModel.CurrentCpuPercent, 3);
    }

    [AvaloniaFact]
    public void Clear_Removes_Samples_And_Resets_Runtime_Values()
    {
        var sampler = new FakeProfilerSampler(
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 10, 120, 130, 50, 1, 0, 0));

        using var viewModel = new ProfilerPageViewModel(sampler, startSampling: false);
        viewModel.RefreshNow();

        viewModel.Clear();

        Assert.Equal(0, viewModel.SampleCount);
        Assert.Equal(0, viewModel.CurrentCpuPercent, 3);
        Assert.Equal(0, viewModel.PeakCpuPercent, 3);
        Assert.Equal(0, viewModel.CurrentWorkingSetMb, 3);
        Assert.Equal(0, viewModel.PeakWorkingSetMb, 3);
        Assert.Equal(0, viewModel.CurrentManagedHeapMb, 3);
    }

    [AvaloniaFact]
    public void PauseOrResumeSampling_Toggles_State_And_Button_Text()
    {
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, 0, 0)),
            startSampling: false);

        Assert.False(viewModel.IsSampling);
        Assert.Equal("Resume", viewModel.ToggleSamplingText);

        viewModel.PauseOrResumeSampling();

        Assert.True(viewModel.IsSampling);
        Assert.Equal("Pause", viewModel.ToggleSamplingText);
    }

    [AvaloniaFact]
    public void SetTabActive_AutoPauses_And_Resumes_Only_For_TabDriven_Pause()
    {
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, 0, 0)),
            startSampling: false);

        viewModel.PauseOrResumeSampling();
        Assert.True(viewModel.IsSampling);

        viewModel.SetTabActive(false);
        Assert.False(viewModel.IsSampling);

        viewModel.SetTabActive(true);
        Assert.True(viewModel.IsSampling);

        viewModel.PauseOrResumeSampling();
        Assert.False(viewModel.IsSampling);

        viewModel.SetTabActive(false);
        viewModel.SetTabActive(true);
        Assert.False(viewModel.IsSampling);
    }

    [AvaloniaFact]
    public void Search_Selection_And_Remove_Selected_Record_Work()
    {
        var sampler = new FakeProfilerSampler(
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 1, 100, 110, 10, 0, 0, 0),
            new ProfilerSampleSnapshot(DateTimeOffset.UtcNow.AddSeconds(1), 5, 140, 150, 20, 0, 0, 0));

        using var viewModel = new ProfilerPageViewModel(sampler, startSampling: false);
        viewModel.RefreshNow();
        viewModel.RefreshNow();

        Assert.True(viewModel.SelectNextMatch());
        Assert.NotNull(viewModel.SelectedSample);

        Assert.True(viewModel.RemoveSelectedRecord());
        Assert.Equal(1, viewModel.SampleCount);
        Assert.Null(viewModel.SelectedSample);
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Drops_Local_Process_Activities()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(now, 0, 0, 0, 0, 0, 0, 0)),
            startSampling: false,
            startRemoteListener: false,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 1234,
            ProcessName: "LocalApp",
            AppName: "LocalApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryActivity(
            sessionId,
            now.AddMilliseconds(1),
            SourceName: "remote.activity.source",
            Name: "Local Activity",
            StartTime: now,
            Duration: TimeSpan.FromMilliseconds(10),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, viewModel.SampleCount);
        Assert.Equal(0, viewModel.RemotePacketCount);
        Assert.Equal(1, viewModel.DroppedLocalPacketCount);
    }

    [AvaloniaFact]
    public void HandleTelemetryPacket_Accepts_Remote_Activities_And_Switches_To_Remote_Mode()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(now, 1, 2, 3, 4, 0, 0, 0)),
            startSampling: true,
            startRemoteListener: false,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: 1234);

        viewModel.RefreshNow();

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 5678,
            ProcessName: "RemoteApp",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryActivity(
            sessionId,
            now.AddMilliseconds(1),
            SourceName: "remote.activity.source",
            Name: "Remote Activity",
            StartTime: now,
            Duration: TimeSpan.FromMilliseconds(42),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        var sample = Assert.Single(viewModel.SamplesView.Cast<ProfilerSampleViewModel>());
        Assert.True(viewModel.IsRemoteMode);
        Assert.False(viewModel.IsSampling);
        Assert.Equal(1, viewModel.RemotePacketCount);
        Assert.Equal("remote.activity.source", sample.SourceName);
        Assert.Equal("Remote Activity", sample.ActivityName);
        Assert.Equal("RemoteApp(5678)", sample.Process);
        Assert.Equal(42, sample.DurationMs, 3);
    }

    [AvaloniaFact]
    public void RemoteListener_Accepts_Remote_Activities_EndToEnd_Over_Udp()
    {
        var port = GetAvailableUdpPort();
        var sessionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(timestamp, 0, 0, 0, 0, 0, 0, 0)),
            startSampling: false,
            startRemoteListener: true,
            remotePort: port,
            localProcessId: Environment.ProcessId);

        SendPacket(port, new TelemetryHello(
            sessionId,
            timestamp,
            ProcessId: Environment.ProcessId + 1000,
            ProcessName: "RemoteProcess",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        SendPacket(port, new TelemetryActivity(
            sessionId,
            timestamp.AddMilliseconds(1),
            SourceName: "remote.source",
            Name: "Remote activity",
            StartTime: timestamp,
            Duration: TimeSpan.FromMilliseconds(15),
            Tags: Array.Empty<TelemetryTag>()));

        WaitFor(() => viewModel.SampleCount == 1 && viewModel.RemotePacketCount == 1, TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsRemoteMode);
        Assert.Equal(1, viewModel.RemoteSessionCount);
        Assert.Equal(1, viewModel.RemotePacketCount);
        Assert.Equal(0, viewModel.DroppedLocalPacketCount);
    }

    [AvaloniaFact]
    public void SetTabActive_Inactive_RemoteMode_WithoutMutation_Drops_Incoming_Samples_Until_Reactivated()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        using var viewModel = new ProfilerPageViewModel(
            new FakeProfilerSampler(new ProfilerSampleSnapshot(now, 1, 2, 3, 4, 0, 0, 0)),
            startSampling: false,
            startRemoteListener: false,
            remotePort: TelemetryProtocol.DefaultPort,
            localProcessId: 1234);

        viewModel.HandleTelemetryPacket(new TelemetryHello(
            sessionId,
            now,
            ProcessId: 5678,
            ProcessName: "RemoteApp",
            AppName: "RemoteApp",
            MachineName: "test-machine",
            RuntimeVersion: Environment.Version.ToString()));

        viewModel.HandleTelemetryPacket(new TelemetryActivity(
            sessionId,
            now.AddMilliseconds(1),
            SourceName: "remote.activity.source",
            Name: "Before Inactive",
            StartTime: now,
            Duration: TimeSpan.FromMilliseconds(10),
            Tags: Array.Empty<TelemetryTag>()));

        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, viewModel.SampleCount);
        Assert.True(viewModel.IsRemoteMode);

        viewModel.SetTabActive(false);
        viewModel.HandleTelemetryPacket(new TelemetryActivity(
            sessionId,
            now.AddMilliseconds(2),
            SourceName: "remote.activity.source",
            Name: "While Inactive",
            StartTime: now,
            Duration: TimeSpan.FromMilliseconds(11),
            Tags: Array.Empty<TelemetryTag>()));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, viewModel.SampleCount);

        viewModel.SetTabActive(true);
        viewModel.HandleTelemetryPacket(new TelemetryActivity(
            sessionId,
            now.AddMilliseconds(3),
            SourceName: "remote.activity.source",
            Name: "After Reactivation",
            StartTime: now,
            Duration: TimeSpan.FromMilliseconds(12),
            Tags: Array.Empty<TelemetryTag>()));
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, viewModel.SampleCount);
    }

    private sealed class FakeProfilerSampler : IProfilerSampler
    {
        private readonly ProfilerSampleSnapshot[] _samples;
        private int _index;

        public FakeProfilerSampler(params ProfilerSampleSnapshot[] samples)
        {
            _samples = samples;
        }

        public ProfilerSampleSnapshot CaptureSample()
        {
            if (_samples.Length == 0)
            {
                return new ProfilerSampleSnapshot(DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, 0, 0);
            }

            if (_index >= _samples.Length)
            {
                return _samples[^1];
            }

            return _samples[_index++];
        }
    }

    private static int GetAvailableUdpPort()
    {
        using var udp = new UdpClient(0);
        var endpoint = Assert.IsType<IPEndPoint>(udp.Client.LocalEndPoint);
        return endpoint.Port;
    }

    private static void SendPacket(int port, TelemetryPacket packet)
    {
        var writer = new TelemetryPacketWriter();
        var payload = packet switch
        {
            TelemetryHello hello => writer.Write(hello).ToArray(),
            TelemetryMetric metric => writer.Write(metric, 0).ToArray(),
            TelemetryActivity activity => writer.Write(activity, 0).ToArray(),
            _ => throw new NotSupportedException("Unsupported telemetry packet type: " + packet.GetType().Name)
        };
        using var sender = new UdpClient();
        sender.Send(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, port));
    }

    private static void WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
    }
}
