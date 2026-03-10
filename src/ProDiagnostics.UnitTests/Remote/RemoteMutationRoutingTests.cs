using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Diagnostics.Remote;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests.Remote;

public class RemoteMutationRoutingTests
{
    [AvaloniaFact]
    public void LogsPage_RemoteMutation_IsUsed_For_Settings_And_Clear()
    {
        using var viewModel = new LogsPageViewModel();
        var mutation = new RecordingMutationService();
        viewModel.SetRemoteMutationSource(mutation);

        viewModel.ShowDebug = true;
        WaitFor(() => mutation.SetLogLevelsCalls > 0);

        viewModel.Clear();
        WaitFor(() => mutation.ClearLogsCalls > 0);

        Assert.True(mutation.SetLogLevelsCalls > 0);
        Assert.Equal(1, mutation.ClearLogsCalls);
    }

    [AvaloniaFact]
    public void MetricsPage_RemoteMutation_IsUsed_For_Pause_And_Settings()
    {
        using var viewModel = new MetricsPageViewModel(
            subscribeToLiveMetrics: false,
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: Environment.ProcessId,
            maxPendingMeasurements: MetricsPageViewModel.DefaultPendingMeasurementQueueCapacity,
            maxRemoteSessions: MetricsPageViewModel.DefaultRemoteSessionCapacity,
            startPaused: false);

        var mutation = new RecordingMutationService();
        viewModel.SetRemoteMutationSource(mutation);

        viewModel.PauseOrResumeUpdates();
        WaitFor(() => mutation.SetMetricsPausedCalls > 0);

        viewModel.MaxSeries = viewModel.MaxSeries + 1;
        WaitFor(() => mutation.SetMetricsSettingsCalls > 0);

        Assert.Equal(1, mutation.SetMetricsPausedCalls);
        Assert.True(mutation.LastMetricsPaused);
        Assert.True(mutation.SetMetricsSettingsCalls > 0);
    }

    [AvaloniaFact]
    public void ProfilerPage_RemoteMutation_IsUsed_For_Pause_And_Settings()
    {
        using var viewModel = new ProfilerPageViewModel(
            sampler: new NoOpProfilerSampler(),
            startSampling: false,
            startRemoteListener: false,
            remotePort: 54831,
            localProcessId: Environment.ProcessId);

        var mutation = new RecordingMutationService();
        viewModel.SetRemoteMutationSource(mutation);

        viewModel.PauseOrResumeSampling();
        WaitFor(() => mutation.SetProfilerPausedCalls > 0);

        viewModel.MaxSamples = viewModel.MaxSamples + 1;
        WaitFor(() => mutation.SetProfilerSettingsCalls > 0);

        Assert.Equal(1, mutation.SetProfilerPausedCalls);
        Assert.False(mutation.LastProfilerPaused);
        Assert.True(mutation.SetProfilerSettingsCalls > 0);
    }

    [AvaloniaFact]
    public void EventsPage_RemoteMutation_IsUsed_For_Default_And_Clear()
    {
        _ = TestEventsControl.BubbleEvent;
        var viewModel = new EventsPageViewModel(mainViewModel: null);
        var mutation = new RecordingMutationService();
        viewModel.SetRemoteMutationSource(mutation, _ => ("combined", null, null));

        viewModel.DisableAll();
        WaitFor(() => mutation.DisableAllEventsCalls > 0);

        viewModel.EnableDefault();
        WaitFor(() => mutation.EnableDefaultEventsCalls > 0);

        viewModel.Clear();
        WaitFor(() => mutation.ClearEventsCalls > 0);

        Assert.Equal(1, mutation.DisableAllEventsCalls);
        Assert.Equal(1, mutation.EnableDefaultEventsCalls);
        Assert.Equal(1, mutation.ClearEventsCalls);
    }

    [AvaloniaFact]
    public void BreakpointsPage_RemoteMutation_IsUsed_For_Enable_Disable_And_Clear()
    {
        var service = new BreakpointService();
        service.AddPropertyBreakpoint(Control.WidthProperty, new Border(), "Border.Width");

        using var viewModel = new BreakpointsPageViewModel(service);
        var mutation = new RecordingMutationService();
        viewModel.SetRemoteMutationSource(mutation);

        viewModel.DisableAll();
        WaitFor(() => mutation.SetBreakpointsEnabledCalls > 0);

        viewModel.EnableAll();
        WaitFor(() => mutation.SetBreakpointsEnabledCalls > 1);

        viewModel.ClearAll();
        WaitFor(() => mutation.ClearBreakpointsCalls > 0);

        Assert.Equal(2, mutation.SetBreakpointsEnabledCalls);
        Assert.Equal(1, mutation.ClearBreakpointsCalls);
    }

    private static void WaitFor(Func<bool> condition, int timeoutMs = 1500)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            Dispatcher.UIThread.RunJobs();
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before timeout.");
            }

            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private sealed class RecordingMutationService : IRemoteMutationDiagnosticsDomainService
    {
        public int ClearLogsCalls { get; private set; }
        public int SetLogLevelsCalls { get; private set; }
        public int SetMetricsPausedCalls { get; private set; }
        public bool LastMetricsPaused { get; private set; }
        public int SetMetricsSettingsCalls { get; private set; }
        public int SetProfilerPausedCalls { get; private set; }
        public bool LastProfilerPaused { get; private set; }
        public int SetProfilerSettingsCalls { get; private set; }
        public int DisableAllEventsCalls { get; private set; }
        public int EnableDefaultEventsCalls { get; private set; }
        public int ClearEventsCalls { get; private set; }
        public int SetBreakpointsEnabledCalls { get; private set; }
        public int ClearBreakpointsCalls { get; private set; }

        public ValueTask<RemoteMutationResult> InspectHoveredAsync(RemoteInspectHoveredRequest? request = null, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.InspectHovered);
        public ValueTask<RemoteMutationResult> SetSelectionAsync(RemoteSetSelectionRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.SelectionSet);
        public ValueTask<RemoteMutationResult> SetPreviewPausedAsync(RemoteSetPreviewPausedRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.PreviewPausedSet);
        public ValueTask<RemoteMutationResult> SetPreviewSettingsAsync(RemoteSetPreviewSettingsRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.PreviewSettingsSet);
        public ValueTask<RemoteMutationResult> InjectPreviewInputAsync(RemotePreviewInputRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.PreviewInputInject);
        public ValueTask<RemoteMutationResult> SetPropertyAsync(RemoteSetPropertyRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.PropertiesSet);
        public ValueTask<RemoteMutationResult> SetPseudoClassAsync(RemoteSetPseudoClassRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.PseudoClassSet);
        public ValueTask<RemoteMutationResult> SetElements3DRootAsync(RemoteSetElements3DRootRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.Elements3DRootSet);
        public ValueTask<RemoteMutationResult> ResetElements3DRootAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.Elements3DRootReset);
        public ValueTask<RemoteMutationResult> SetElements3DFiltersAsync(RemoteSetElements3DFiltersRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.Elements3DFiltersSet);
        public ValueTask<RemoteMutationResult> SetOverlayOptionsAsync(RemoteSetOverlayOptionsRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.OverlayOptionsSet);
        public ValueTask<RemoteMutationResult> SetOverlayLiveHoverAsync(RemoteSetOverlayLiveHoverRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.OverlayLiveHoverSet);
        public ValueTask<RemoteMutationResult> OpenCodeDocumentAsync(RemoteCodeDocumentOpenRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.CodeDocumentOpen);
        public ValueTask<RemoteMutationResult> AddPropertyBreakpointAsync(RemoteAddPropertyBreakpointRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.BreakpointsPropertyAdd);
        public ValueTask<RemoteMutationResult> AddEventBreakpointAsync(RemoteAddEventBreakpointRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.BreakpointsEventAdd);
        public ValueTask<RemoteMutationResult> RemoveBreakpointAsync(RemoteRemoveBreakpointRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.BreakpointsRemove);
        public ValueTask<RemoteMutationResult> ToggleBreakpointAsync(RemoteToggleBreakpointRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.BreakpointsToggle);

        public ValueTask<RemoteMutationResult> ClearBreakpointsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default)
        {
            ClearBreakpointsCalls++;
            return Success(RemoteMutationMethods.BreakpointsClear);
        }

        public ValueTask<RemoteMutationResult> SetBreakpointsEnabledAsync(RemoteSetBreakpointsEnabledRequest request, CancellationToken cancellationToken = default)
        {
            SetBreakpointsEnabledCalls++;
            return Success(RemoteMutationMethods.BreakpointsEnabledSet);
        }

        public ValueTask<RemoteMutationResult> ClearEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default)
        {
            ClearEventsCalls++;
            return Success(RemoteMutationMethods.EventsClear);
        }

        public ValueTask<RemoteMutationResult> SetEventEnabledAsync(RemoteSetEventEnabledRequest request, CancellationToken cancellationToken = default) => Success(RemoteMutationMethods.EventsNodeEnabledSet);

        public ValueTask<RemoteMutationResult> EnableDefaultEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default)
        {
            EnableDefaultEventsCalls++;
            return Success(RemoteMutationMethods.EventsDefaultsEnable);
        }

        public ValueTask<RemoteMutationResult> DisableAllEventsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default)
        {
            DisableAllEventsCalls++;
            return Success(RemoteMutationMethods.EventsDisableAll);
        }

        public ValueTask<RemoteMutationResult> ClearLogsAsync(RemoteEmptyMutationRequest? request = null, CancellationToken cancellationToken = default)
        {
            ClearLogsCalls++;
            return Success(RemoteMutationMethods.LogsClear);
        }

        public ValueTask<RemoteMutationResult> SetLogLevelsAsync(RemoteSetLogLevelsRequest request, CancellationToken cancellationToken = default)
        {
            SetLogLevelsCalls++;
            return Success(RemoteMutationMethods.LogsLevelsSet);
        }

        public ValueTask<RemoteMutationResult> SetMetricsPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default)
        {
            SetMetricsPausedCalls++;
            LastMetricsPaused = request.IsPaused;
            return Success(RemoteMutationMethods.MetricsPausedSet);
        }

        public ValueTask<RemoteMutationResult> SetMetricsSettingsAsync(RemoteSetMetricsSettingsRequest request, CancellationToken cancellationToken = default)
        {
            SetMetricsSettingsCalls++;
            return Success(RemoteMutationMethods.MetricsSettingsSet);
        }

        public ValueTask<RemoteMutationResult> SetProfilerPausedAsync(RemoteSetPausedRequest request, CancellationToken cancellationToken = default)
        {
            SetProfilerPausedCalls++;
            LastProfilerPaused = request.IsPaused;
            return Success(RemoteMutationMethods.ProfilerPausedSet);
        }

        public ValueTask<RemoteMutationResult> SetProfilerSettingsAsync(RemoteSetProfilerSettingsRequest request, CancellationToken cancellationToken = default)
        {
            SetProfilerSettingsCalls++;
            return Success(RemoteMutationMethods.ProfilerSettingsSet);
        }

        public ValueTask<RemoteMutationResult> SetStreamDemandAsync(RemoteSetStreamDemandRequest request, CancellationToken cancellationToken = default) =>
            Success(RemoteMutationMethods.StreamDemandSet);

        private static ValueTask<RemoteMutationResult> Success(string operation) =>
            ValueTask.FromResult(new RemoteMutationResult(operation, Changed: true, Message: "ok"));
    }

    private sealed class NoOpProfilerSampler : IProfilerSampler
    {
        public ProfilerSampleSnapshot CaptureSample() =>
            new(DateTimeOffset.UtcNow, CpuPercent: 0, WorkingSetMb: 0, PrivateMemoryMb: 0, ManagedHeapMb: 0, Gen0Collections: 0, Gen1Collections: 0, Gen2Collections: 0);
    }

    private sealed class TestEventsControl : Control
    {
        public static readonly RoutedEvent<RoutedEventArgs> BubbleEvent =
            RoutedEvent.Register<TestEventsControl, RoutedEventArgs>(nameof(BubbleEvent), RoutingStrategies.Bubble);
    }
}
