using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GridHierarchyBenchmarks.Shared;
#if PRO
using Avalonia.Controls.DataGridHierarchical;
#else
using TreeDataGridControl = Avalonia.Controls.TreeDataGrid;
#endif

namespace GridHierarchyBenchmarks.Native;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        NativeBenchmarkOptions.Initialize(args);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(
            Array.Empty<string>(),
            ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<NativeBenchmarkApplication>()
            .UsePlatformDetect();
        NativePlatformInfo.RenderingSubsystem = builder.RenderingSubsystemName ?? "unknown";
        NativePlatformInfo.RuntimePlatformServices = builder.RuntimePlatformServicesName ?? "unknown";
        return builder;
    }
}

internal static class NativePlatformInfo
{
    public static string RenderingSubsystem { get; set; } = "unknown";

    public static string RuntimePlatformServices { get; set; } = "unknown";
}

internal sealed class NativeBenchmarkApplication : Application
{
    private NativeGridHandle? _inspectionHandle;

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
#if PRO
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml"),
        });
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/Themes/"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Flat.xaml"),
        });
#else
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.TreeDataGrid/Themes/"))
        {
            Source = new Uri("avares://Avalonia.Controls.TreeDataGrid/Themes/Fluent.axaml"),
        });
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var host = NativeGridAdapter.CreateHostWindow();
            desktop.MainWindow = host;

            if (NativeBenchmarkOptions.Inspect)
            {
                _inspectionHandle = NativeGridAdapter.Create(expanded: true);
                host.Content = _inspectionHandle.Grid;
                host.Closed += (_, _) =>
                {
                    _inspectionHandle?.Dispose();
                    _inspectionHandle = null;
                    desktop.Shutdown();
                };
                host.Opened += (_, _) => Dispatcher.UIThread.Post(
                    () => RunInspectionAsync(host),
                    DispatcherPriority.Background);
            }
            else
            {
                host.Opened += (_, _) => Dispatcher.UIThread.Post(
                    () => RunBenchmarkAsync(host, desktop),
                    DispatcherPriority.Background);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void RunInspectionAsync(Window host)
    {
        try
        {
            await NativeBenchmarkRunner.WaitForRenderedFrameAsync(host);
            host.UpdateLayout();
            var validation = NativeGridAdapter.Validate(_inspectionHandle!);
            Console.WriteLine(
                $"NATIVE_INSPECT_READY implementation={NativeBenchmarkOptions.ImplementationName} " +
                $"pid={Environment.ProcessId} rows={validation.RealizedRows} cells={validation.RealizedCells} " +
                $"extent={validation.ExtentWidth:F0}x{validation.ExtentHeight:F0} scaling={host.RenderScaling:F2}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            host.Close();
        }
    }

    private static async void RunBenchmarkAsync(
        Window host,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await NativeBenchmarkRunner.WaitForRenderedFrameAsync(host);
            var result = await new NativeBenchmarkRunner(host).RunAsync();
            await NativeBenchmarkResultWriter.WriteAsync(result, NativeBenchmarkOptions.OutputPath);
            Console.WriteLine(JsonSerializer.Serialize(result, NativeBenchmarkResultWriter.JsonOptions));
            host.Close();
            desktop.Shutdown(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            host.Close();
            desktop.Shutdown(1);
        }
    }
}

internal enum NativeScrollPattern
{
    Discontinuous,
    Line,
    Fractional,
}

internal static class NativeBenchmarkOptions
{
    public static bool Inspect { get; private set; }

    public static bool DrawnCells { get; private set; }

    public static int WarmupIterations { get; private set; } = 2;

    public static int Iterations { get; private set; } = 5;

    public static int ScrollJumps { get; private set; } = 32;

    public static NativeScrollPattern ScrollPattern { get; private set; }

    public static bool FirstRenderOnly { get; private set; }

    public static bool CollapseOnly { get; private set; }

    public static bool ScrollOnly { get; private set; }

    public static bool AvaloniaDiagnostics { get; private set; }

    public static bool ProDataGridDiagnostics { get; private set; }

    public static int AlignmentCallbacks { get; private set; } = 3;

    public static string OutputPath { get; private set; } = Path.GetFullPath("native-result.json");

#if PRO && PRODATAGRID_PR335
    public static string ProMode { get; } =
        (Environment.GetEnvironmentVariable("GRID_BENCH_PRO_MODE") ?? "standard")
        .Trim()
        .ToLowerInvariant();

    public static bool AllowVirtualFallback { get; } =
        string.Equals(
            Environment.GetEnvironmentVariable("GRID_BENCH_ALLOW_VIRTUAL_FALLBACK"),
            "1",
            StringComparison.Ordinal);
#endif

    public static string ImplementationName =>
#if PRO
#if PRODATAGRID_PR335
        ProMode switch
        {
            "standard" => "ProDataGrid (legacy default retained)",
            "optimized" => "ProDataGrid (optimized retained)",
            "direct" => "ProDataGrid (direct-content retained)",
            "direct-cell" => "ProDataGrid (direct-cell retained)",
            "drawn" => "ProDataGrid (drawn ordinary cells)",
            "flat-direct-cell" => "ProDataGrid (flat direct-cell retained)",
            "flat-drawn" => "ProDataGrid (flat drawn ordinary cells)",
            "virtual" => "ProDataGrid (virtual cell surface)",
            "virtual-checkbox" => "ProDataGrid (virtual cell surface with checkbox)",
            "virtual-date" => "ProDataGrid (virtual cell surface with date)",
            "virtual-time" => "ProDataGrid (virtual cell surface with time)",
            "virtual-masked" => "ProDataGrid (virtual cell surface with masked text)",
            "virtual-autocomplete" => "ProDataGrid (virtual cell surface with autocomplete text)",
            "virtual-slider-text" => "ProDataGrid (virtual cell surface with slider text)",
            "virtual-combobox-text" => "ProDataGrid (virtual cell surface with combo-box text)",
            _ => $"ProDataGrid ({ProMode})",
        };
#else
        "ProDataGrid";
#endif
#elif ACCELERATE
        DrawnCells ? "Accelerate TreeDataGrid (drawn cells)" : "Accelerate TreeDataGrid (retained cells)";
#else
        "Wieslaw's TreeDataGrid";
#endif

#if PRO && PRODATAGRID_PR335
    public static string PerformanceMode => ProMode;
#endif

    public static void Initialize(string[] args)
    {
        for (var i = 0; i < args.Length; ++i)
        {
            switch (args[i])
            {
                case "--inspect":
                    Inspect = true;
                    break;
                case "--drawn":
                    DrawnCells = bool.Parse(RequireValue(args, ref i));
                    break;
                case "--output":
                    OutputPath = Path.GetFullPath(RequireValue(args, ref i));
                    break;
                case "--warmup":
                    WarmupIterations = ParsePositive(RequireValue(args, ref i), "warmup iterations", allowZero: true);
                    break;
                case "--iterations":
                    Iterations = ParsePositive(RequireValue(args, ref i), "iterations", allowZero: false);
                    break;
                case "--scroll-jumps":
                    ScrollJumps = ParsePositive(RequireValue(args, ref i), "scroll jumps", allowZero: false);
                    break;
                case "--scroll-pattern":
                    ScrollPattern = ParseScrollPattern(RequireValue(args, ref i));
                    break;
                case "--first-render-only":
                    FirstRenderOnly = true;
                    break;
                case "--collapse-only":
                    CollapseOnly = true;
                    break;
                case "--scroll-only":
                    ScrollOnly = true;
                    break;
                case "--avalonia-diagnostics":
                    AvaloniaDiagnostics = true;
                    break;
                case "--prodatagrid-diagnostics":
                    ProDataGridDiagnostics = true;
                    break;
                case "--alignment-callbacks":
                    AlignmentCallbacks = ParsePositive(
                        RequireValue(args, ref i),
                        "alignment callbacks",
                        allowZero: false);
                    break;
            }
        }

        if (AvaloniaDiagnostics)
        {
            AppContext.SetSwitch("Avalonia.Diagnostics.Diagnostic.IsEnabled", true);
            NativeAvaloniaDiagnostics.Initialize();
        }

        if (ProDataGridDiagnostics)
        {
#if PRO
            AppContext.SetSwitch("ProDataGrid.Diagnostics.IsEnabled", true);
            NativeProDataGridDiagnostics.Initialize();
#else
            throw new ArgumentException("--prodatagrid-diagnostics is supported only by ProDataGrid.");
#endif
        }

#if !ACCELERATE
        if (DrawnCells)
            throw new ArgumentException("--drawn true is supported only by Accelerate TreeDataGrid.");
#endif
#if PRO && PRODATAGRID_PR335
        if (ProMode is not ("standard" or "optimized" or "direct" or "direct-cell" or "drawn" or
            "flat-direct-cell" or "flat-drawn" or "virtual" or "virtual-checkbox" or "virtual-date" or
            "virtual-time" or "virtual-masked" or "virtual-autocomplete" or "virtual-slider-text" or
            "virtual-combobox-text"))
            throw new ArgumentException($"Unsupported GRID_BENCH_PRO_MODE: {ProMode}.");
#endif
        if (ScrollOnly && (FirstRenderOnly || CollapseOnly))
            throw new ArgumentException("--scroll-only cannot be combined with --first-render-only or --collapse-only.");
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"Missing value for {args[index - 1]}.");
        return args[index];
    }

    private static int ParsePositive(string value, string name, bool allowZero)
    {
        if (!int.TryParse(value, out var result) || result < (allowZero ? 0 : 1))
            throw new ArgumentException($"Invalid {name}: {value}.");
        return result;
    }

    private static NativeScrollPattern ParseScrollPattern(string value)
    {
        if (!Enum.TryParse(value, ignoreCase: true, out NativeScrollPattern result) ||
            !Enum.IsDefined(result))
        {
            throw new ArgumentException(
                $"Invalid scroll pattern '{value}'. Expected discontinuous, line, or fractional.");
        }

        return result;
    }
}

internal sealed class NativeBenchmarkRunner
{
    private readonly Window _host;

    public NativeBenchmarkRunner(Window host)
    {
        _host = host;
    }

    public async Task<NativeBenchmarkResult> RunAsync()
    {
        var firstRender = NativeBenchmarkOptions.CollapseOnly || NativeBenchmarkOptions.ScrollOnly
            ? null
            : await MeasureFirstRenderAsync();
        var expandAndRender = NativeBenchmarkOptions.FirstRenderOnly || NativeBenchmarkOptions.CollapseOnly ||
            NativeBenchmarkOptions.ScrollOnly
            ? null
            : await MeasureExpandAndRenderAsync();
        var collapseAndRender = NativeBenchmarkOptions.FirstRenderOnly || NativeBenchmarkOptions.ScrollOnly
            ? null
            : await MeasureCollapseAndRenderAsync();
        var scrollAndRender = NativeBenchmarkOptions.FirstRenderOnly
            || NativeBenchmarkOptions.CollapseOnly
            ? null
            : await MeasureScrollAndRenderAsync();

        return new NativeBenchmarkResult
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Implementation = NativeBenchmarkOptions.ImplementationName,
#if PRO && PRODATAGRID_PR335
            Mode = NativeBenchmarkOptions.PerformanceMode,
#else
            Mode = NativeBenchmarkOptions.DrawnCells ? "drawn" : "retained",
#endif
            AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString() ?? "unknown",
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown",
            RenderingSubsystem = NativePlatformInfo.RenderingSubsystem,
            RuntimePlatformServices = NativePlatformInfo.RuntimePlatformServices,
            LogicalProcessorCount = Environment.ProcessorCount,
            ClientWidth = _host.ClientSize.Width,
            ClientHeight = _host.ClientSize.Height,
            RenderScaling = _host.RenderScaling,
            WarmupIterations = NativeBenchmarkOptions.WarmupIterations,
            MeasuredIterations = NativeBenchmarkOptions.Iterations,
            ScrollJumpsPerIteration = NativeBenchmarkOptions.ScrollJumps,
            ScrollPattern = NativeBenchmarkOptions.ScrollPattern.ToString(),
            AlignmentCallbacks = NativeBenchmarkOptions.AlignmentCallbacks,
            FirstExpandedRender = firstRender,
            ExpandAllAndRender = expandAndRender,
            CollapseAllAndRender = collapseAndRender,
            ScrollAndRender = scrollAndRender,
        };
    }

    private async Task<NativeWorkloadResult> MeasureFirstRenderAsync()
    {
        for (var i = 0; i < NativeBenchmarkOptions.WarmupIterations; ++i)
            await FirstRenderIterationAsync(measure: false);

        var times = new List<double>();
        var allocations = new List<double>();
        NativeValidation? validation = null;

        for (var i = 0; i < NativeBenchmarkOptions.Iterations; ++i)
        {
            var measurement = await FirstRenderIterationAsync(measure: true);
            times.Add(measurement.ElapsedMilliseconds);
            allocations.Add(measurement.AllocatedBytes);
            validation = measurement.Validation;
        }

        return NativeWorkloadResult.Create("FirstExpandedRender", times, allocations, validation!);
    }

    private async Task<NativeMeasurement> FirstRenderIterationAsync(bool measure)
    {
        using var handle = NativeGridAdapter.Create(expanded: true);
        if (measure)
            await PrepareMeasurementAsync(_host);

        var allocatedBefore = measure ? GC.GetTotalAllocatedBytes(precise: false) : 0;
        var renderedFrame = WaitForRenderedFrameAsync(_host);
        var stopwatch = Stopwatch.StartNew();
        _host.Content = handle.Grid;
        _host.UpdateLayout();
        await renderedFrame;
        stopwatch.Stop();
        var allocated = measure ? GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore : 0;
        var validation = NativeGridAdapter.Validate(handle);
        await DetachAsync();

        return new NativeMeasurement(stopwatch.Elapsed.TotalMilliseconds, allocated, validation);
    }

    private async Task<NativeWorkloadResult> MeasureExpandAndRenderAsync()
    {
        for (var i = 0; i < NativeBenchmarkOptions.WarmupIterations; ++i)
            await ExpandAndRenderIterationAsync(measure: false);

        var times = new List<double>();
        var allocations = new List<double>();
        NativeValidation? validation = null;

        for (var i = 0; i < NativeBenchmarkOptions.Iterations; ++i)
        {
            var measurement = await ExpandAndRenderIterationAsync(measure: true);
            times.Add(measurement.ElapsedMilliseconds);
            allocations.Add(measurement.AllocatedBytes);
            validation = measurement.Validation;
        }

        return NativeWorkloadResult.Create("ExpandAllAndRender", times, allocations, validation!);
    }

    private async Task<NativeMeasurement> ExpandAndRenderIterationAsync(bool measure)
    {
        using var handle = NativeGridAdapter.Create(expanded: false);
        _host.Content = handle.Grid;
        _host.UpdateLayout();
        await WaitForRenderedFrameAsync(_host);

        if (measure)
            await PrepareMeasurementAsync(_host);

        var allocatedBefore = measure ? GC.GetTotalAllocatedBytes(precise: false) : 0;
        var renderedFrame = WaitForRenderedFrameAsync(_host);
        var stopwatch = Stopwatch.StartNew();
        handle.ExpandAll();
        _host.UpdateLayout();
        await renderedFrame;
        stopwatch.Stop();
        var allocated = measure ? GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore : 0;
        var validation = NativeGridAdapter.Validate(handle);
        await DetachAsync();

        return new NativeMeasurement(stopwatch.Elapsed.TotalMilliseconds, allocated, validation);
    }

    private async Task<NativeWorkloadResult> MeasureCollapseAndRenderAsync()
    {
        for (var i = 0; i < NativeBenchmarkOptions.WarmupIterations; ++i)
            await CollapseAndRenderIterationAsync(measure: false);

        var times = new List<double>();
        var allocations = new List<double>();
        var mutationTimes = new List<double>();
        var layoutTimes = new List<double>();
        var frameTimes = new List<double>();
        var uiRenderTimes = new List<double>();
        var compositorUpdateTimes = new List<double>();
        var compositorRenderTimes = new List<double>();
        var firstFrameCallbackTimes = new List<double>();
        var secondFrameCallbackTimes = new List<double>();
        var animationTickIntervals = new List<double>();
        var layoutUpdatedCounts = new List<int>();
        NativeValidation? validation = null;

        for (var i = 0; i < NativeBenchmarkOptions.Iterations; ++i)
        {
            var measurement = await CollapseAndRenderIterationAsync(measure: true);
            times.Add(measurement.ElapsedMilliseconds);
            allocations.Add(measurement.AllocatedBytes);
            mutationTimes.Add(measurement.MutationMilliseconds);
            layoutTimes.Add(measurement.LayoutMilliseconds);
            frameTimes.Add(measurement.FrameMilliseconds);
            firstFrameCallbackTimes.Add(measurement.FirstFrameCallbackMilliseconds);
            secondFrameCallbackTimes.Add(measurement.SecondFrameCallbackMilliseconds);
            animationTickIntervals.Add(measurement.AnimationTickIntervalMilliseconds);
            layoutUpdatedCounts.Add(measurement.LayoutUpdatedCount);
            uiRenderTimes.Add(measurement.UiRenderMilliseconds);
            compositorUpdateTimes.Add(measurement.CompositorUpdateMilliseconds);
            compositorRenderTimes.Add(measurement.CompositorRenderMilliseconds);
            validation = measurement.Validation;
        }

        return NativeWorkloadResult.Create(
            "CollapseAllAndRender",
            times,
            allocations,
            validation!,
            mutationTimes,
            layoutTimes,
            frameTimes,
            firstFrameCallbackTimes,
            secondFrameCallbackTimes,
            animationTickIntervals,
            layoutUpdatedCounts,
            uiRenderTimes,
            compositorUpdateTimes,
            compositorRenderTimes);
    }

    private async Task<NativeMeasurement> CollapseAndRenderIterationAsync(bool measure)
    {
        const TreeShape shape = TreeShape.OptimizedSample149792Depth5;
        using var handle = NativeGridAdapter.Create(
            expanded: true,
            shape: shape,
            retainCollapsedChildren: true);
        _host.Content = handle.Grid;
        _host.UpdateLayout();
        await WaitForRenderedFrameAsync(_host);
        NativeGridAdapter.Validate(handle, TreeDataFactory.ExpectedCount(shape));

        if (measure)
            await PrepareMeasurementAsync(_host);

        var layoutUpdatedCount = 0;
        void OnLayoutUpdated(object? sender, EventArgs e) => layoutUpdatedCount++;
        handle.Grid.LayoutUpdated += OnLayoutUpdated;

        var allocatedBefore = measure ? GC.GetTotalAllocatedBytes(precise: false) : 0;
        NativeAvaloniaDiagnostics.Reset();
        var renderedFrame = RequestRenderedFrame(_host);
        var stopwatch = Stopwatch.StartNew();
        handle.CollapseAll();
        var mutationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        _host.UpdateLayout();
        var mutationAndLayoutMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var layoutCompletedTimestamp = Stopwatch.GetTimestamp();
        await renderedFrame.Completion;
        stopwatch.Stop();
        handle.Grid.LayoutUpdated -= OnLayoutUpdated;
        var allocated = measure ? GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore : 0;
        var renderingDiagnostics = await NativeAvaloniaDiagnostics.SnapshotAsync();
        var validation = NativeGridAdapter.Validate(handle, expectedCount: 32);
        await DetachAsync();

        return new NativeMeasurement(
            stopwatch.Elapsed.TotalMilliseconds,
            allocated,
            validation,
            mutationMilliseconds,
            mutationAndLayoutMilliseconds - mutationMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds - mutationAndLayoutMilliseconds,
            Stopwatch.GetElapsedTime(layoutCompletedTimestamp, renderedFrame.FirstCallbackTimestamp).TotalMilliseconds,
            Stopwatch.GetElapsedTime(renderedFrame.FirstCallbackTimestamp, renderedFrame.SecondCallbackTimestamp).TotalMilliseconds,
            (renderedFrame.SecondAnimationTimestamp - renderedFrame.FirstAnimationTimestamp).TotalMilliseconds,
            layoutUpdatedCount,
            renderingDiagnostics.UiRenderMilliseconds,
            renderingDiagnostics.CompositorUpdateMilliseconds,
            renderingDiagnostics.CompositorRenderMilliseconds);
    }

    private async Task<NativeWorkloadResult> MeasureScrollAndRenderAsync()
    {
        using var handle = NativeGridAdapter.Create(expanded: true);
        _host.Content = handle.Grid;
        _host.UpdateLayout();
        await WaitForRenderedFrameAsync(_host);
        NativeGridAdapter.Validate(handle);
        var viewer = NativeGridAdapter.GetScrollViewer(handle);

        for (var batch = 0; batch < NativeBenchmarkOptions.WarmupIterations; ++batch)
        {
            await PrepareScrollBatchAsync(handle, viewer, batch);
            await RunScrollBatchAsync(handle, viewer, batch, measure: false);
        }

        var times = new List<double>();
        var allocationPerJump = new List<double>();
        var mutationTimes = new List<double>();
        var layoutTimes = new List<double>();
        var frameTimes = new List<double>();
        var firstFrameCallbackTimes = new List<double>();
        var secondFrameCallbackTimes = new List<double>();
        var animationTickIntervals = new List<double>();
        var uiRenderTimes = new List<double>();
        var compositorUpdateTimes = new List<double>();
        var compositorRenderTimes = new List<double>();
        var proDataGridDiagnostics = new List<IReadOnlyDictionary<string, double>>();

        for (var batch = 0; batch < NativeBenchmarkOptions.Iterations; ++batch)
        {
            await PrepareScrollBatchAsync(handle, viewer, batch);
            await PrepareMeasurementAsync(_host);
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            var batchMeasurements = await RunScrollBatchAsync(handle, viewer, batch, measure: true);
            var allocated = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore;
            foreach (NativeScrollMeasurement measurement in batchMeasurements)
            {
                times.Add(measurement.ElapsedMilliseconds);
                mutationTimes.Add(measurement.MutationMilliseconds);
                layoutTimes.Add(measurement.LayoutMilliseconds);
                frameTimes.Add(measurement.FrameMilliseconds);
                firstFrameCallbackTimes.Add(measurement.FirstFrameCallbackMilliseconds);
                secondFrameCallbackTimes.Add(measurement.SecondFrameCallbackMilliseconds);
                animationTickIntervals.Add(measurement.AnimationTickIntervalMilliseconds);
                uiRenderTimes.Add(measurement.UiRenderMilliseconds);
                compositorUpdateTimes.Add(measurement.CompositorUpdateMilliseconds);
                compositorRenderTimes.Add(measurement.CompositorRenderMilliseconds);
                if (measurement.ProDataGridDiagnostics is not null)
                {
                    proDataGridDiagnostics.Add(measurement.ProDataGridDiagnostics);
                }
            }
            allocationPerJump.Add(allocated / (double)NativeBenchmarkOptions.ScrollJumps);
        }

        var validation = NativeGridAdapter.Validate(handle);
        await DetachAsync();
        return NativeWorkloadResult.Create(
            "ScrollAndRender",
            times,
            allocationPerJump,
            validation,
            mutationTimes,
            layoutTimes,
            frameTimes,
            firstFrameCallbackTimes,
            secondFrameCallbackTimes,
            animationTickIntervals,
            uiRenderTimes: uiRenderTimes,
            compositorUpdateTimes: compositorUpdateTimes,
            compositorRenderTimes: compositorRenderTimes,
            proDataGridDiagnostics: proDataGridDiagnostics);
    }

    private async Task PrepareScrollBatchAsync(NativeGridHandle handle, ScrollViewer viewer, int batch)
    {
        if (NativeBenchmarkOptions.ScrollPattern == NativeScrollPattern.Discontinuous)
        {
            return;
        }

        NativeGridAdapter.SetScrollRowOffset(viewer, GetSmoothBatchStartRow(batch));
        handle.Grid.UpdateLayout();
        await WaitForRenderedFrameAsync(_host);
    }

    private async Task<IReadOnlyList<NativeScrollMeasurement>> RunScrollBatchAsync(
        NativeGridHandle handle,
        ScrollViewer viewer,
        int batch,
        bool measure)
    {
        var measurements = measure ? new List<NativeScrollMeasurement>(NativeBenchmarkOptions.ScrollJumps) : null;
        for (var i = 0; i < NativeBenchmarkOptions.ScrollJumps; ++i)
        {
            var operation = (batch * NativeBenchmarkOptions.ScrollJumps) + i;
            double rowOffset = GetScrollRowOffset(batch, i, operation);
            if (NativeBenchmarkOptions.AvaloniaDiagnostics)
            {
                NativeAvaloniaDiagnostics.Reset();
            }
            if (NativeBenchmarkOptions.ProDataGridDiagnostics)
            {
                NativeProDataGridDiagnostics.Reset();
            }
            NativeFrameBarrier renderedFrame = RequestRenderedFrame(_host);
            var stopwatch = Stopwatch.StartNew();
            NativeGridAdapter.SetScrollRowOffset(viewer, rowOffset);
            double mutationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            handle.Grid.UpdateLayout();
            double mutationAndLayoutMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            long layoutCompletedTimestamp = Stopwatch.GetTimestamp();
            await renderedFrame.Completion;
            stopwatch.Stop();
            NativeRenderingDiagnostics renderingDiagnostics = measure && NativeBenchmarkOptions.AvaloniaDiagnostics
                ? await NativeAvaloniaDiagnostics.SnapshotAsync()
                : default;
            IReadOnlyDictionary<string, double>? proDataGridDiagnostics =
                measure && NativeBenchmarkOptions.ProDataGridDiagnostics
                    ? NativeProDataGridDiagnostics.Snapshot()
                    : null;
            measurements?.Add(new NativeScrollMeasurement(
                stopwatch.Elapsed.TotalMilliseconds,
                mutationMilliseconds,
                mutationAndLayoutMilliseconds - mutationMilliseconds,
                stopwatch.Elapsed.TotalMilliseconds - mutationAndLayoutMilliseconds,
                Stopwatch.GetElapsedTime(
                    layoutCompletedTimestamp,
                    renderedFrame.FirstCallbackTimestamp).TotalMilliseconds,
                Stopwatch.GetElapsedTime(
                    renderedFrame.FirstCallbackTimestamp,
                    renderedFrame.SecondCallbackTimestamp).TotalMilliseconds,
                (renderedFrame.SecondAnimationTimestamp -
                    renderedFrame.FirstAnimationTimestamp).TotalMilliseconds,
                renderingDiagnostics.UiRenderMilliseconds,
                renderingDiagnostics.CompositorUpdateMilliseconds,
                renderingDiagnostics.CompositorRenderMilliseconds,
                proDataGridDiagnostics));
        }

        return measurements is null ? Array.Empty<NativeScrollMeasurement>() : measurements;
    }

    private static double GetScrollRowOffset(int batch, int index, int operation)
    {
        return NativeBenchmarkOptions.ScrollPattern switch
        {
            NativeScrollPattern.Discontinuous => ((operation * 509) % 2_000) + 1,
            NativeScrollPattern.Line => GetSmoothBatchStartRow(batch) + index + 1,
            NativeScrollPattern.Fractional =>
                GetSmoothBatchStartRow(batch) + ((index & 1) == 0 ? 0.375 : 0.625),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static int GetSmoothBatchStartRow(int batch) => 500 + ((batch * 131) % 1_000);

    private async Task DetachAsync()
    {
        var renderedFrame = WaitForRenderedFrameAsync(_host);
        _host.Content = null;
        _host.UpdateLayout();
        await renderedFrame;
    }

    private static void CollectForMeasurement()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static async Task PrepareMeasurementAsync(TopLevel topLevel)
    {
        CollectForMeasurement();
        // Full collection duration depends on each implementation's retained graph and can
        // otherwise shift the timed operation to opposite sides of a Windows vsync boundary.
        // Start every sample after the same configurable animation-pulse quiescence barrier.
        await WaitForAnimationCallbacksAsync(topLevel, NativeBenchmarkOptions.AlignmentCallbacks);
    }

    private static Task WaitForAnimationCallbacksAsync(TopLevel topLevel, int callbackCount)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void RequestNext(int remaining)
        {
            topLevel.RequestAnimationFrame(_ =>
            {
                if (remaining == 1)
                    completion.TrySetResult();
                else
                    RequestNext(remaining - 1);
            });
        }

        RequestNext(callbackCount);
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    public static async Task WaitForRenderedFrameAsync(TopLevel topLevel)
    {
        await RequestRenderedFrame(topLevel).Completion;
    }

    private static NativeFrameBarrier RequestRenderedFrame(TopLevel topLevel)
    {
        var barrier = new NativeFrameBarrier();
        topLevel.RequestAnimationFrame(firstTimestamp =>
        {
            barrier.SetFirstCallback(firstTimestamp);
            topLevel.RequestAnimationFrame(secondTimestamp =>
                barrier.SetSecondCallback(secondTimestamp));
        });
        return barrier;
    }
}

internal sealed class NativeFrameBarrier
{
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completion => _completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

    public long FirstCallbackTimestamp { get; private set; }

    public long SecondCallbackTimestamp { get; private set; }

    public TimeSpan FirstAnimationTimestamp { get; private set; }

    public TimeSpan SecondAnimationTimestamp { get; private set; }

    public void SetFirstCallback(TimeSpan animationTimestamp)
    {
        FirstCallbackTimestamp = Stopwatch.GetTimestamp();
        FirstAnimationTimestamp = animationTimestamp;
    }

    public void SetSecondCallback(TimeSpan animationTimestamp)
    {
        SecondCallbackTimestamp = Stopwatch.GetTimestamp();
        SecondAnimationTimestamp = animationTimestamp;
        _completion.TrySetResult();
    }
}

internal static class NativeAvaloniaDiagnostics
{
    private const string MeterName = "Avalonia.Diagnostic.Meter";
    private const string UiRenderName = "avalonia.ui.render.time";
    private const string CompositorUpdateName = "avalonia.comp.update.time";
    private const string CompositorRenderName = "avalonia.comp.render.time";
    private static readonly ConcurrentQueue<NativeDiagnosticMeasurement> s_measurements = new();
    private static MeterListener? s_listener;

    public static void Initialize()
    {
        if (s_listener != null)
        {
            return;
        }

        s_listener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName &&
                    instrument.Name is UiRenderName or CompositorUpdateName or CompositorRenderName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        s_listener.SetMeasurementEventCallback<double>(static (instrument, value, _, _) =>
            s_measurements.Enqueue(new NativeDiagnosticMeasurement(instrument.Name, value)));
        s_listener.Start();
    }

    public static void Reset()
    {
        while (s_measurements.TryDequeue(out _))
        {
        }
    }

    public static async Task<NativeRenderingDiagnostics> SnapshotAsync()
    {
        if (!NativeBenchmarkOptions.AvaloniaDiagnostics)
        {
            return default;
        }

        // CompositionBatch.Processed releases the second animation callback before the
        // compositor has necessarily finished its update/render pass. This unmeasured delay
        // lets the diagnostic histograms finish without changing the timed result.
        await Task.Delay(50);

        var uiRender = 0.0;
        var compositorUpdate = 0.0;
        var compositorRender = 0.0;
        while (s_measurements.TryDequeue(out var measurement))
        {
            switch (measurement.Name)
            {
                case UiRenderName:
                    uiRender = Math.Max(uiRender, measurement.Value);
                    break;
                case CompositorUpdateName:
                    compositorUpdate = Math.Max(compositorUpdate, measurement.Value);
                    break;
                case CompositorRenderName:
                    compositorRender = Math.Max(compositorRender, measurement.Value);
                    break;
            }
        }

        return new NativeRenderingDiagnostics(uiRender, compositorUpdate, compositorRender);
    }

    private readonly record struct NativeDiagnosticMeasurement(string Name, double Value);
}

internal readonly record struct NativeRenderingDiagnostics(
    double UiRenderMilliseconds,
    double CompositorUpdateMilliseconds,
    double CompositorRenderMilliseconds);

internal static class NativeProDataGridDiagnostics
{
    private const string MeterName = "ProDataGrid.Diagnostic.Meter";
    private static readonly ConcurrentQueue<NativeDiagnosticMeasurement> s_measurements = new();
    private static MeterListener? s_listener;

    public static void Initialize()
    {
        if (s_listener != null)
        {
            return;
        }

        s_listener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        s_listener.SetMeasurementEventCallback<double>(static (instrument, value, _, _) =>
            s_measurements.Enqueue(new NativeDiagnosticMeasurement(instrument.Name, value)));
        s_listener.SetMeasurementEventCallback<long>(static (instrument, value, _, _) =>
            s_measurements.Enqueue(new NativeDiagnosticMeasurement(instrument.Name, value)));
        s_listener.Start();
    }

    public static void Reset()
    {
        while (s_measurements.TryDequeue(out _))
        {
        }
    }

    public static IReadOnlyDictionary<string, double> Snapshot()
    {
        var sums = new Dictionary<string, double>(StringComparer.Ordinal);
        while (s_measurements.TryDequeue(out NativeDiagnosticMeasurement measurement))
        {
            sums.TryGetValue(measurement.Name, out double previous);
            sums[measurement.Name] = previous + measurement.Value;
        }

        return sums;
    }

    private readonly record struct NativeDiagnosticMeasurement(string Name, double Value);
}

internal static class NativeGridAdapter
{
    public static Window CreateHostWindow()
    {
        var window = new Window
        {
            Name = "BenchmarkWindow",
            Title = $"Native hierarchy benchmark - {NativeBenchmarkOptions.ImplementationName}",
            Width = 800,
            Height = 500,
            CanResize = false,
            Background = Brushes.White,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
#if !PRO
        window.Styles.Add(new Style(x => x.OfType<TreeDataGridRow>())
        {
            Setters =
            {
                new Setter(TreeDataGridRow.HeightProperty, 24.0),
            },
        });
#endif
        return window;
    }

    public static NativeGridHandle Create(
        bool expanded,
        TreeShape shape = TreeShape.Deep4094Depth11,
        bool retainCollapsedChildren = false)
    {
        var roots = TreeDataFactory.Create(shape);
#if PRO
#if PRODATAGRID_PR335
        var optimized = NativeBenchmarkOptions.ProMode != "standard";
        var directHierarchy = NativeBenchmarkOptions.ProMode is "direct" or "direct-cell" or "drawn" or
            "flat-direct-cell" or "flat-drawn";
        var directContent = NativeBenchmarkOptions.ProMode == "direct";
        var directCell = NativeBenchmarkOptions.ProMode is "direct-cell" or "flat-direct-cell";
        var drawn = NativeBenchmarkOptions.ProMode is "drawn" or "flat-drawn";
        var flatLayout = NativeBenchmarkOptions.ProMode is "flat-direct-cell" or "flat-drawn";
        var virtualSurface = NativeBenchmarkOptions.ProMode is "virtual" or "virtual-checkbox" or "virtual-date" or
            "virtual-time" or "virtual-masked" or "virtual-autocomplete" or "virtual-slider-text" or
            "virtual-combobox-text";
        var virtualCheckBox = NativeBenchmarkOptions.ProMode == "virtual-checkbox";
        var virtualDate = NativeBenchmarkOptions.ProMode == "virtual-date";
        var virtualTime = NativeBenchmarkOptions.ProMode == "virtual-time";
        var virtualMasked = NativeBenchmarkOptions.ProMode == "virtual-masked";
        var virtualAutoComplete = NativeBenchmarkOptions.ProMode == "virtual-autocomplete";
        var virtualSliderText = NativeBenchmarkOptions.ProMode == "virtual-slider-text";
        var virtualComboBoxText = NativeBenchmarkOptions.ProMode == "virtual-combobox-text";
        var options = new HierarchicalOptions<Node>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children.Count == 0,
            VirtualizeChildren = !retainCollapsedChildren,
        };
        var model = new HierarchicalModel<Node>(options);
        model.SetRoots(roots);
        if (expanded)
            model.ExpandAll();

        var grid = new DataGrid
        {
            Name = "BenchmarkGrid",
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            IsReadOnly = true,
            RowHeight = 24,
            UseLogicalScrollable = true,
            CanUserSortColumns = false,
            CanUserResizeColumns = false,
            CanUserReorderColumns = false,
            UseLightweightFiller = optimized,
            VisualLayoutMode = virtualSurface
                ? DataGridVisualLayoutMode.Virtualized
                : flatLayout
                    ? DataGridVisualLayoutMode.Flat
                    : DataGridVisualLayoutMode.Nested,
        };
        var hierarchyColumn = new DataGridHierarchicalColumn
        {
            Header = "Name",
            Width = new DataGridLength(300),
            Binding = new Binding("Item.Name"),
            UseDirectCell = directHierarchy,
            UseDirectTextContent = directHierarchy,
            TrackDirectTextValueChanges = false,
        };
        DataGridColumnMetadata.SetValueAccessor(
            hierarchyColumn,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(node => ((Node)node.Item).Name));
        grid.Columns.Add(hierarchyColumn);
        AddProTextColumn(grid, "Id", 90, "Item.Id", node => ((Node)node.Item).Id, directContent, directCell, drawn);
        AddProTextColumn(grid, "Depth", 90, "Item.Depth", node => ((Node)node.Item).Depth, directContent, directCell, drawn);
        AddProTextColumn(grid, "Children", 100, "Item.ChildCount", node => ((Node)node.Item).ChildCount, directContent, directCell, drawn);
        if (virtualCheckBox)
        {
            AddProCheckBoxColumn(grid, "Has children", 180, "Item.HasChildren", node => ((Node)node.Item).HasChildren);
        }
        else if (virtualDate)
        {
            AddProDateColumn(grid, "Date", 180, "Item.Date", node => ((Node)node.Item).Date);
        }
        else if (virtualTime)
        {
            AddProTimeColumn(grid, "Time", 180, "Item.Time", node => ((Node)node.Item).Time);
        }
        else if (virtualMasked)
        {
            AddProMaskedTextColumn(grid, "Phone", 180, "Item.Phone", node => ((Node)node.Item).Phone);
        }
        else if (virtualAutoComplete)
        {
            AddProAutoCompleteColumn(grid, "Category", 180, "Item.Category", node => ((Node)node.Item).Category);
        }
        else if (virtualSliderText)
        {
            AddProSliderTextColumn(grid, "Value", 180, "Item.SliderValue", node => ((Node)node.Item).SliderValue);
        }
        else if (virtualComboBoxText)
        {
            AddProComboBoxTextColumn(grid, "Category", 180, "Item.Category", node => ((Node)node.Item).Category);
        }
        else
        {
            AddProTextColumn(grid, "Payload", 180, "Item.Payload", node => ((Node)node.Item).Payload, directContent, directCell, drawn);
        }
        if (optimized)
            ApplyProOptimizedThemes(grid, featurePreserving: NativeBenchmarkOptions.ProMode == "optimized");
        if (virtualSurface)
        {
            grid.CellTheme = null;
            ApplyProFlatThemes(grid);
        }
        else if (flatLayout)
        {
            ApplyProFlatThemes(grid);
        }
        return new NativeGridHandle(model, grid);
#else
        var options = new HierarchicalOptions<Node>
        {
            ChildrenSelector = node => node.Children,
            IsLeafSelector = node => node.Children.Count == 0,
            VirtualizeChildren = !retainCollapsedChildren,
        };
        var model = new HierarchicalModel<Node>(options);
        model.SetRoots(roots);
        if (expanded)
            model.ExpandAll();

        var grid = new DataGrid
        {
            Name = "BenchmarkGrid",
            AutoGenerateColumns = false,
            HierarchicalModel = model,
            HierarchicalRowsEnabled = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            IsReadOnly = true,
            RowHeight = 24,
            UseLogicalScrollable = true,
            CanUserSortColumns = false,
            CanUserResizeColumns = false,
            CanUserReorderColumns = false,
        };
        grid.Columns.Add(new DataGridHierarchicalColumn
        {
            Header = "Name",
            Width = new DataGridLength(300),
            Binding = new Binding("Item.Name"),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Id",
            Width = new DataGridLength(90),
            Binding = new Binding("Item.Id"),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Depth",
            Width = new DataGridLength(90),
            Binding = new Binding("Item.Depth"),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Children",
            Width = new DataGridLength(100),
            Binding = new Binding("Item.ChildCount"),
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Payload",
            Width = new DataGridLength(180),
            Binding = new Binding("Item.Payload"),
        });
        return new NativeGridHandle(model, grid);
#endif
#else
        var source = new HierarchicalTreeDataGridSource<Node>(roots);
#if ACCELERATE
        source.WithHierarchicalExpanderTextColumn(
            "Name",
            x => x.Name,
            x => x.Children,
            hasChildren: x => x.HasChildren,
            options: options =>
            {
                options.Width = new GridLength(300);
                options.UseDrawnCells = NativeBenchmarkOptions.DrawnCells;
            });
#else
        source.WithHierarchicalExpanderTextColumn(
            "Name",
            x => x.Name,
            x => x.Children,
            options => options.Width = new GridLength(300));
#endif
        source.WithTextColumn("Id", x => x.Id, options =>
        {
            options.Width = new GridLength(90);
#if ACCELERATE
            options.UseDrawnCells = NativeBenchmarkOptions.DrawnCells;
#endif
        });
        source.WithTextColumn("Depth", x => x.Depth, options =>
        {
            options.Width = new GridLength(90);
#if ACCELERATE
            options.UseDrawnCells = NativeBenchmarkOptions.DrawnCells;
#endif
        });
        source.WithTextColumn("Children", x => x.ChildCount, options =>
        {
            options.Width = new GridLength(100);
#if ACCELERATE
            options.UseDrawnCells = NativeBenchmarkOptions.DrawnCells;
#endif
        });
        source.WithTextColumn("Payload", x => x.Payload, options =>
        {
            options.Width = new GridLength(180);
#if ACCELERATE
            options.UseDrawnCells = NativeBenchmarkOptions.DrawnCells;
#endif
        });
        if (expanded)
            source.ExpandAll();

        var grid = new TreeDataGridControl
        {
            Name = "BenchmarkGrid",
            Source = source,
        };
        return new NativeGridHandle(source, grid);
#endif
    }

#if PRO && PRODATAGRID_PR335
    private static void AddProTextColumn<TValue>(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, TValue> getter,
        bool directContent,
        bool directCell,
        bool drawn)
    {
        var column = new DataGridTextColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            UseDirectTextContent = directContent,
            UseDirectTextCell = directCell,
            TrackDirectTextValueChanges = false,
            DisplayMode = drawn ? DataGridColumnDisplayMode.Drawn : DataGridColumnDisplayMode.Retained,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, TValue>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProCheckBoxColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, bool> getter)
    {
        var column = new DataGridCheckBoxColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, bool>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProDateColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, DateTime> getter)
    {
        var column = new DataGridDatePickerColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            SelectedDateFormat = CalendarDatePickerFormat.Custom,
            CustomDateFormatString = "yyyy-MM-dd",
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, DateTime>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProTimeColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, TimeSpan> getter)
    {
        var column = new DataGridTimePickerColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            ClockIdentifier = "24HourClock",
            UseSeconds = true,
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, TimeSpan>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProMaskedTextColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, string> getter)
    {
        var column = new DataGridMaskedTextColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            Mask = "(000) 000-0000",
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProAutoCompleteColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, string> getter)
    {
        var column = new DataGridAutoCompleteColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            ItemsSource = new[] { "Category-000", "Category-001", "Category-002" },
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProSliderTextColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, double> getter)
    {
        var column = new DataGridSliderColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            Binding = new Binding(bindingPath),
            Minimum = 0,
            Maximum = 100,
            ShowValueText = true,
            ValueTextFormat = "{0:0.0}",
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, double>(getter));
        grid.Columns.Add(column);
    }

    private static void AddProComboBoxTextColumn(
        DataGrid grid,
        string header,
        double width,
        string bindingPath,
        Func<HierarchicalNode, string> getter)
    {
        var column = new DataGridComboBoxColumn
        {
            Header = header,
            Width = new DataGridLength(width),
            IsEditable = true,
            ItemsSource = new[] { "Category-000", "Category-001", "Category-002" },
            TextBinding = new Binding(bindingPath),
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(getter));
        grid.Columns.Add(column);
    }

    private static void ApplyProOptimizedThemes(DataGrid grid, bool featurePreserving)
    {
        grid.RowTheme = FindProTheme(
            grid,
            featurePreserving ? "DataGridOptimizedFeatureUnfrozenRowTheme" : "DataGridOptimizedUnfrozenRowTheme");
        grid.CellTheme = FindProTheme(grid, "DataGridOptimizedCellTheme");
        grid.ColumnHeaderTheme = FindProTheme(
            grid,
            featurePreserving ? "DataGridOptimizedFeatureColumnHeaderTheme" : "DataGridOptimizedColumnHeaderTheme");
    }

    private static void ApplyProFlatThemes(DataGrid grid)
    {
        grid.Theme = FindProTheme(grid, "DataGridFlatTheme");
        grid.RowTheme = FindProTheme(grid, "DataGridFlatRowTheme");
    }

    private static ControlTheme FindProTheme(DataGrid grid, string key)
    {
        if ((grid.TryFindResource(key, out object? value) ||
             Application.Current?.TryFindResource(key, out value) == true) &&
            value is ControlTheme theme)
        {
            return theme;
        }

        throw new InvalidOperationException($"Unable to resolve optimized theme resource '{key}'.");
    }
#endif

    public static NativeValidation Validate(NativeGridHandle handle, int expectedCount = 4_094)
    {
        if (handle.Count != expectedCount)
            throw new InvalidOperationException($"Expected {expectedCount:N0} rows, got {handle.Count:N0}.");

#if PRO
        var realizedRows = handle.Grid.GetVisualDescendants().OfType<DataGridRow>().Count();
        var realizedCells = handle.Grid.GetVisualDescendants().OfType<DataGridCell>().Count();
#else
        var realizedRows = handle.Grid.GetVisualDescendants().OfType<TreeDataGridRow>().Count();
        var realizedCells = handle.Grid.GetVisualDescendants().OfType<TreeDataGridCell>().Count();
#endif
#if PRO && PRODATAGRID_PR335
        var expectsLightweightRows =
            NativeBenchmarkOptions.ProMode.StartsWith("virtual", StringComparison.Ordinal) &&
            !NativeBenchmarkOptions.AllowVirtualFallback;
        if (expectsLightweightRows)
        {
            if (realizedRows != 0)
                throw new InvalidOperationException($"Virtual cell surface realized {realizedRows} retained rows.");
            if (realizedCells != 0)
                throw new InvalidOperationException($"Virtual cell surface realized {realizedCells} retained cells.");
        }
        else if (realizedRows <= 0 || realizedRows >= 200)
        {
            throw new InvalidOperationException($"Virtualization validation failed: {realizedRows} realized rows.");
        }
#else
        if (realizedRows <= 0 || realizedRows >= 200)
            throw new InvalidOperationException($"Virtualization validation failed: {realizedRows} realized rows.");
#endif

        var viewer = GetScrollViewer(handle);
        return new NativeValidation(
            handle.Count,
            realizedRows,
            realizedCells,
            handle.Grid.GetVisualDescendants().Count(),
            handle.Grid.GetVisualDescendants().OfType<Control>().Count(),
            viewer.Extent.Width,
            viewer.Extent.Height,
            viewer.Viewport.Width,
            viewer.Viewport.Height);
    }

    public static ScrollViewer GetScrollViewer(NativeGridHandle handle) =>
        handle.Grid.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .First(x => x.Name == "PART_ScrollViewer");

    public static void SetScrollRowOffset(ScrollViewer viewer, double rowOffset)
    {
        viewer.Offset = new Vector(0, rowOffset * 24.0);
    }
}

#if PRO
internal sealed class NativeGridHandle : IDisposable
{
    public NativeGridHandle(HierarchicalModel<Node> model, DataGrid grid)
    {
        Model = model;
        Grid = grid;
    }

    public HierarchicalModel<Node> Model { get; }

    public DataGrid Grid { get; }

    public int Count => Model.Count;

    public void ExpandAll() => Model.ExpandAll();

    public void CollapseAll() => Model.CollapseAll();

    public void Dispose()
    {
    }
}
#else
internal sealed class NativeGridHandle : IDisposable
{
    public NativeGridHandle(HierarchicalTreeDataGridSource<Node> source, TreeDataGridControl grid)
    {
        Source = source;
        Grid = grid;
    }

    public HierarchicalTreeDataGridSource<Node> Source { get; }

    public TreeDataGridControl Grid { get; }

    public int Count => Source.Rows.Count;

    public void ExpandAll() => Source.ExpandAll();

    public void CollapseAll() => Source.CollapseAll();

    public void Dispose() => Source.Dispose();
}
#endif

internal sealed record NativeMeasurement(
    double ElapsedMilliseconds,
    long AllocatedBytes,
    NativeValidation Validation,
    double MutationMilliseconds = 0,
    double LayoutMilliseconds = 0,
    double FrameMilliseconds = 0,
    double FirstFrameCallbackMilliseconds = 0,
    double SecondFrameCallbackMilliseconds = 0,
    double AnimationTickIntervalMilliseconds = 0,
    int LayoutUpdatedCount = 0,
    double UiRenderMilliseconds = 0,
    double CompositorUpdateMilliseconds = 0,
    double CompositorRenderMilliseconds = 0);

internal readonly record struct NativeScrollMeasurement(
    double ElapsedMilliseconds,
    double MutationMilliseconds,
    double LayoutMilliseconds,
    double FrameMilliseconds,
    double FirstFrameCallbackMilliseconds,
    double SecondFrameCallbackMilliseconds,
    double AnimationTickIntervalMilliseconds,
    double UiRenderMilliseconds,
    double CompositorUpdateMilliseconds,
    double CompositorRenderMilliseconds,
    IReadOnlyDictionary<string, double>? ProDataGridDiagnostics);

internal sealed record NativeValidation(
    int RowCount,
    int RealizedRows,
    int RealizedCells,
    int RealizedVisuals,
    int RealizedControls,
    double ExtentWidth,
    double ExtentHeight,
    double ViewportWidth,
    double ViewportHeight);

internal sealed class NativeWorkloadResult
{
    public required string Name { get; init; }

    public int Samples { get; init; }

    public double MeanMilliseconds { get; init; }

    public double MedianMilliseconds { get; init; }

    public double P95Milliseconds { get; init; }

    public double StandardDeviationMilliseconds { get; init; }

    public double MeanAllocatedBytes { get; init; }

    public double MeanMutationMilliseconds { get; init; }

    public double MeanLayoutMilliseconds { get; init; }

    public double MeanFrameMilliseconds { get; init; }

    public double MeanFirstFrameCallbackMilliseconds { get; init; }

    public double MeanSecondFrameCallbackMilliseconds { get; init; }

    public double MeanAnimationTickIntervalMilliseconds { get; init; }

    public double MeanLayoutUpdatedCount { get; init; }

    public double MeanUiRenderMilliseconds { get; init; }

    public double MeanCompositorUpdateMilliseconds { get; init; }

    public double MeanCompositorRenderMilliseconds { get; init; }

    public IReadOnlyDictionary<string, double>? MeanProDataGridDiagnostics { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<double>>? ProDataGridDiagnosticSamples { get; init; }

    public required IReadOnlyList<double> MillisecondSamples { get; init; }

    public IReadOnlyList<double>? MutationMillisecondSamples { get; init; }

    public IReadOnlyList<double>? LayoutMillisecondSamples { get; init; }

    public IReadOnlyList<double>? FrameMillisecondSamples { get; init; }

    public IReadOnlyList<double>? FirstFrameCallbackMillisecondSamples { get; init; }

    public IReadOnlyList<double>? SecondFrameCallbackMillisecondSamples { get; init; }

    public IReadOnlyList<double>? AnimationTickIntervalMillisecondSamples { get; init; }

    public IReadOnlyList<int>? LayoutUpdatedCountSamples { get; init; }

    public required NativeValidation Validation { get; init; }

    public static NativeWorkloadResult Create(
        string name,
        IReadOnlyList<double> times,
        IReadOnlyList<double> allocations,
        NativeValidation validation,
        IReadOnlyList<double>? mutationTimes = null,
        IReadOnlyList<double>? layoutTimes = null,
        IReadOnlyList<double>? frameTimes = null,
        IReadOnlyList<double>? firstFrameCallbackTimes = null,
        IReadOnlyList<double>? secondFrameCallbackTimes = null,
        IReadOnlyList<double>? animationTickIntervals = null,
        IReadOnlyList<int>? layoutUpdatedCounts = null,
        IReadOnlyList<double>? uiRenderTimes = null,
        IReadOnlyList<double>? compositorUpdateTimes = null,
        IReadOnlyList<double>? compositorRenderTimes = null,
        IReadOnlyList<IReadOnlyDictionary<string, double>>? proDataGridDiagnostics = null)
    {
        var ordered = times.OrderBy(x => x).ToArray();
        var mean = times.Average();
        var variance = times.Sum(x => Math.Pow(x - mean, 2)) / times.Count;
        IReadOnlyDictionary<string, IReadOnlyList<double>>? diagnosticSamples =
            CreateDiagnosticSamples(proDataGridDiagnostics);
        return new NativeWorkloadResult
        {
            Name = name,
            Samples = times.Count,
            MeanMilliseconds = mean,
            MedianMilliseconds = Percentile(ordered, 0.50),
            P95Milliseconds = Percentile(ordered, 0.95),
            StandardDeviationMilliseconds = Math.Sqrt(variance),
            MeanAllocatedBytes = allocations.Average(),
            MeanMutationMilliseconds = mutationTimes?.Average() ?? 0,
            MeanLayoutMilliseconds = layoutTimes?.Average() ?? 0,
            MeanFrameMilliseconds = frameTimes?.Average() ?? 0,
            MeanFirstFrameCallbackMilliseconds = firstFrameCallbackTimes?.Average() ?? 0,
            MeanSecondFrameCallbackMilliseconds = secondFrameCallbackTimes?.Average() ?? 0,
            MeanAnimationTickIntervalMilliseconds = animationTickIntervals?.Average() ?? 0,
            MeanLayoutUpdatedCount = layoutUpdatedCounts?.Average() ?? 0,
            MeanUiRenderMilliseconds = uiRenderTimes?.Average() ?? 0,
            MeanCompositorUpdateMilliseconds = compositorUpdateTimes?.Average() ?? 0,
            MeanCompositorRenderMilliseconds = compositorRenderTimes?.Average() ?? 0,
            MeanProDataGridDiagnostics = diagnosticSamples?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Average(),
                StringComparer.Ordinal),
            ProDataGridDiagnosticSamples = diagnosticSamples,
            MillisecondSamples = times.ToArray(),
            MutationMillisecondSamples = mutationTimes?.ToArray(),
            LayoutMillisecondSamples = layoutTimes?.ToArray(),
            FrameMillisecondSamples = frameTimes?.ToArray(),
            FirstFrameCallbackMillisecondSamples = firstFrameCallbackTimes?.ToArray(),
            SecondFrameCallbackMillisecondSamples = secondFrameCallbackTimes?.ToArray(),
            AnimationTickIntervalMillisecondSamples = animationTickIntervals?.ToArray(),
            LayoutUpdatedCountSamples = layoutUpdatedCounts?.ToArray(),
            Validation = validation,
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<double>>? CreateDiagnosticSamples(
        IReadOnlyList<IReadOnlyDictionary<string, double>>? measurements)
    {
        if (measurements is not { Count: > 0 })
        {
            return null;
        }

        string[] names = measurements
            .SelectMany(measurement => measurement.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var samples = new Dictionary<string, IReadOnlyList<double>>(names.Length, StringComparer.Ordinal);
        foreach (string name in names)
        {
            var values = new double[measurements.Count];
            for (int index = 0; index < measurements.Count; index++)
            {
                measurements[index].TryGetValue(name, out values[index]);
            }
            samples.Add(name, values);
        }
        return samples;
    }

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Count) - 1, 0, ordered.Count - 1);
        return ordered[index];
    }
}

internal sealed class NativeBenchmarkResult
{
    public DateTimeOffset TimestampUtc { get; init; }

    public required string Implementation { get; init; }

    public required string Mode { get; init; }

    public required string AvaloniaVersion { get; init; }

    public required string Runtime { get; init; }

    public required string OS { get; init; }

    public required string Architecture { get; init; }

    public required string Processor { get; init; }

    public required string RenderingSubsystem { get; init; }

    public required string RuntimePlatformServices { get; init; }

    public int LogicalProcessorCount { get; init; }

    public double ClientWidth { get; init; }

    public double ClientHeight { get; init; }

    public double RenderScaling { get; init; }

    public int WarmupIterations { get; init; }

    public int MeasuredIterations { get; init; }

    public int ScrollJumpsPerIteration { get; init; }

    public required string ScrollPattern { get; init; }

    public int AlignmentCallbacks { get; init; }

    public NativeWorkloadResult? FirstExpandedRender { get; init; }

    public NativeWorkloadResult? ExpandAllAndRender { get; init; }

    public NativeWorkloadResult? CollapseAllAndRender { get; init; }

    public NativeWorkloadResult? ScrollAndRender { get; init; }
}

internal static class NativeBenchmarkResultWriter
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
    };

    public static async Task WriteAsync(NativeBenchmarkResult result, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(result, JsonOptions));
    }
}
