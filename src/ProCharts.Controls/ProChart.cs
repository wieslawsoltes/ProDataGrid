// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using ProCharts.Avalonia;
using ProCharts.Skia;
using SkiaSharp;

namespace ProCharts.Controls;

/// <summary>A lookless chart with independently replaceable plot, chrome, legend, tooltip and navigator templates.</summary>
/// <remarks>
/// The model and data source remain caller-owned. Use the UI dispatcher for model mutations. Templates may
/// omit optional parts; exports require PART_Plot. Retemplating and detach remove all external subscriptions.
/// The default surface uses ProChartView's cached Skia raster renderer, not a new GPU-series renderer.
/// </remarks>
[TemplatePart("PART_Plot", typeof(ProChartView))]
[TemplatePart("PART_ToolTip", typeof(ProChartToolTip))]
[PseudoClasses(":empty", ":loading", ":error", ":exporting", ":hover")]
public partial class ProChart : TemplatedControl
{
    private ChartModel? _model;
    private IChartExportService? _exportService;
    private ProChartView? _plot;
    private ProChartToolTip? _toolTip;
    private bool _attached;
    private Point? _pressPoint;
    private ChartPointerTool _pointerTool;
    private readonly ObservableCollection<ChartLegendItem> _legend = new();
    private readonly List<ChartCommand> _commands = new();
    private object? _legendSource;
    private CancellationTokenSource? _exportCancellation;

    /// <summary>Native legend entries, including disabled series so they can be restored.</summary>
    public ReadOnlyObservableCollection<ChartLegendItem> LegendItems { get; }
    public IReadOnlyList<ChartPointerTool> PointerTools { get; } = Array.AsReadOnly(Enum.GetValues<ChartPointerTool>());
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetViewCommand { get; }
    public ICommand UndoViewCommand { get; }
    public ICommand RedoViewCommand { get; }
    public ICommand LatestCommand { get; }
    public ICommand ExportPngCommand { get; }
    public ICommand ExportSvgCommand { get; }
    public ICommand ExportCsvCommand { get; }
    /// <summary>The current save operation, available to deterministic host tests.</summary>
    public Task LastExportTask { get; private set; } = Task.CompletedTask;
    public static readonly RoutedEvent<ChartPointInvokedEventArgs> PointInvokedEvent =
        RoutedEvent.Register<ProChart, ChartPointInvokedEventArgs>(nameof(PointInvoked), RoutingStrategies.Bubble);
    public event EventHandler<ChartPointInvokedEventArgs>? PointInvoked
    { add => AddHandler(PointInvokedEvent, value); remove => RemoveHandler(PointInvokedEvent, value); }
    public static readonly RoutedEvent<RoutedEventArgs> ViewportChangedEvent =
        RoutedEvent.Register<ProChart, RoutedEventArgs>(nameof(ViewportChanged), RoutingStrategies.Bubble);
    public event EventHandler<RoutedEventArgs>? ViewportChanged
    { add => AddHandler(ViewportChangedEvent, value); remove => RemoveHandler(ViewportChangedEvent, value); }

    public ProChart()
    {
        LegendItems = new ReadOnlyObservableCollection<ChartLegendItem>(_legend);
        ZoomInCommand = Command(_ => Model?.ZoomWindow(1 + ZoomStep, 0.5, MinWindowCount), CanNavigate);
        ZoomOutCommand = Command(_ => Model?.ZoomWindow(1 / (1 + ZoomStep), 0.5, MinWindowCount), CanNavigate);
        ResetViewCommand = Command(_ => { Model?.ResetWindow(); Model?.ResetValueRange(); ClearHover(); }, CanNavigate);
        UndoViewCommand = Command(_ => Model?.UndoWindow(), () => CanNavigate() && Model!.CanUndoWindow);
        RedoViewCommand = Command(_ => Model?.RedoWindow(), () => CanNavigate() && Model!.CanRedoWindow);
        LatestCommand = Command(_ => Model?.ShowLatest(256), CanNavigate);
        ExportPngCommand = Command(_ => LastExportTask = SaveAsync(ChartExportFormat.Png), CanExport);
        ExportSvgCommand = Command(_ => LastExportTask = SaveAsync(ChartExportFormat.Svg), CanExport);
        ExportCsvCommand = Command(_ => LastExportTask = SaveAsync(ChartExportFormat.Csv), CanExport);
    }
    private ChartCommand Command(Action<object?> execute, Func<bool> canExecute)
    { ChartCommand command = new(execute, canExecute); _commands.Add(command); return command; }
    private bool CanNavigate() => Model != null && HasData && !IsLoading && !HasError && EnablePanZoom && IsEffectivelyEnabled;
    private bool CanExport() => _plot != null && HasData && !IsExporting && !IsLoading && !HasError && IsEffectivelyEnabled;
    protected override AutomationPeer OnCreateAutomationPeer() => new ProChartAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DetachPlot();
        base.OnApplyTemplate(e);
        _plot = e.NameScope.Find<ProChartView>("PART_Plot");
        _toolTip = e.NameScope.Find<ProChartToolTip>("PART_ToolTip");
        if (_plot != null)
        {
            _plot.AddHandler(PointerMovedEvent, OnPlotMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            _plot.AddHandler(PointerExitedEvent, OnPlotExited);
            _plot.AddHandler(PointerPressedEvent, OnPlotPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _plot.AddHandler(PointerReleasedEvent, OnPlotReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        ClearHover();
        UpdateState(true);
    }
    private void DetachPlot()
    {
        if (_plot == null) return;
        _plot.RemoveHandler(PointerMovedEvent, OnPlotMoved);
        _plot.RemoveHandler(PointerExitedEvent, OnPlotExited);
        _plot.RemoveHandler(PointerPressedEvent, OnPlotPressed);
        _plot.RemoveHandler(PointerReleasedEvent, OnPlotReleased);
        _plot = null;
        _toolTip = null;
        _pressPoint = null;
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (Model != null) Model.PropertyChanged += OnModelChanged;
        UpdateState(true);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        if (Model != null) Model.PropertyChanged -= OnModelChanged;
        _exportCancellation?.Cancel();
        ClearHover();
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            if (_attached && change.OldValue is ChartModel oldModel) oldModel.PropertyChanged -= OnModelChanged;
            if (_attached && Model != null) Model.PropertyChanged += OnModelChanged;
            ClearHover();
            UpdateState(true);
        }
        else if (change.Property == ChartStyleProperty || change.Property == ShowLegendProperty ||
                 change.Property == IsLoadingProperty || change.Property == ErrorMessageProperty ||
                 change.Property == EnablePanZoomProperty || change.Property == IsEnabledProperty)
            UpdateState(change.Property == ChartStyleProperty);
        else if (change.Property == ShowToolTipsProperty && !ShowToolTips) ClearHover();
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, Model)) return;
        if (e.PropertyName == nameof(ChartModel.Interaction))
        {
            SetAndRaise(PointerToolProperty, ref _pointerTool, Model!.Interaction.PointerTool);
            return; // Crosshair movement must not rebuild the legend or scan the dataset.
        }
        if (e.PropertyName is nameof(ChartModel.Request) or nameof(ChartModel.ValueAxis))
        { ClearHover(); RaiseEvent(new RoutedEventArgs(ViewportChangedEvent)); }
        UpdateState(e.PropertyName is nameof(ChartModel.DataSource) or nameof(ChartModel.Theme) or nameof(ChartModel.SeriesStyles));
    }
    private void UpdateState(bool rebuildLegend = false)
    {
        ChartDataSnapshot snapshot = Model?.Snapshot ?? ChartDataSnapshot.Empty;
        bool data = false;
        long points = 0;
        foreach (ChartSeriesSnapshot series in snapshot.Series)
        {
            points += series.Values.Count;
            if (data) continue;
            for (int i = 0; i < series.Values.Count; i++)
                if (series.Values[i] is double value && double.IsFinite(value)) { data = true; break; }
        }
        HasData = data;
        HasError = !string.IsNullOrEmpty(ErrorMessage);
        IsEmpty = !data && !IsLoading && !HasError;
        IsLegendVisible = ShowLegend && Model?.Legend.IsVisible != false;
        if (!IsExporting) StatusText = $"{snapshot.Series.Count:N0} series · {points:N0} displayed points";
        if (Model != null) SetAndRaise(PointerToolProperty, ref _pointerTool, Model.Interaction.PointerTool);
        PseudoClasses.Set(":empty", IsEmpty);
        PseudoClasses.Set(":loading", IsLoading);
        PseudoClasses.Set(":error", HasError);
        UpdatePresentation(rebuildLegend);
        UpdateLegend(rebuildLegend);
        foreach (ChartCommand command in _commands) command.Refresh();
    }
    private void UpdateLegend(bool force)
    {
        SeriesVisibilityChartDataSource? filter = Model?.DataSource as SeriesVisibilityChartDataSource;
        ChartDataSnapshot snapshot = filter?.UnfilteredSnapshot ?? Model?.Snapshot ?? ChartDataSnapshot.Empty;
        if (!ReferenceEquals(_legendSource, Model?.DataSource)) { force = true; _legendSource = Model?.DataSource; }
        if (!force && _legend.Count == snapshot.Series.Count)
        {
            bool same = true;
            for (int i = 0; i < _legend.Count; i++)
                if (_legend[i].Name != (snapshot.Series[i].Name ?? $"Series {i + 1}") ||
                    _legend[i].Swatch is not ISolidColorBrush brush ||
                    brush.Color.ToUInt32() != (uint)GetLegendColor(i, snapshot.Series[i])) { same = false; break; }
            if (same)
            {
                for (int i = 0; i < _legend.Count; i++) _legend[i].UpdateVisibility(filter?.IsSeriesVisible(i) ?? true);
                return;
            }
        }
        _legend.Clear();
        for (int i = 0; i < snapshot.Series.Count; i++)
        {
            ChartSeriesSnapshot series = snapshot.Series[i];
            SKColor color = GetLegendColor(i, series);
            int index = i;
            _legend.Add(new ChartLegendItem(index, series.Name ?? $"Series {i + 1}",
                new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue)),
                filter?.IsSeriesVisible(index) ?? true,
                filter == null ? null : visible => filter.SetSeriesVisible(index, visible)));
        }
    }
    private void OnPlotMoved(object? sender, PointerEventArgs e)
    {
        if (!ShowToolTips || IsLoading || HasError || _plot == null ||
            e.GetCurrentPoint(_plot).Properties.IsLeftButtonPressed) { ClearHover(); return; }
        Point position = e.GetPosition(_plot);
        SkiaChartHitTestResult? hit = _plot.HitTest(position);
        if (!hit.HasValue) { ClearHover(); return; }
        if (HoverPoint == null || HoverPoint.Hit != hit.Value) HoverPoint = new ChartPointInfo(hit.Value, Model);
        double width = _toolTip?.Bounds.Width ?? 0;
        double height = _toolTip?.Bounds.Height ?? 0;
        ToolTipX = Math.Clamp(position.X + 14, 0, Math.Max(0, _plot.Bounds.Width - Math.Max(220, width)));
        ToolTipY = Math.Clamp(position.Y + 14, 0, Math.Max(0, _plot.Bounds.Height - Math.Max(90, height)));
        IsToolTipVisible = true;
        PseudoClasses.Set(":hover", true);
    }
    private void OnPlotExited(object? sender, PointerEventArgs e) => ClearHover();
    private void OnPlotPressed(object? sender, PointerPressedEventArgs e)
    { if (_plot != null && e.GetCurrentPoint(_plot).Properties.IsLeftButtonPressed) _pressPoint = e.GetPosition(_plot); }
    private void OnPlotReleased(object? sender, PointerReleasedEventArgs e)
    {
        Point? start = _pressPoint;
        _pressPoint = null;
        if (_plot == null || start == null || e.InitialPressMouseButton != MouseButton.Left) return;
        Point end = e.GetPosition(_plot);
        double dx = end.X - start.Value.X, dy = end.Y - start.Value.Y;
        if (dx * dx + dy * dy > 25) return;
        SkiaChartHitTestResult? hit = _plot.HitTest(end);
        if (hit.HasValue) RaiseEvent(new ChartPointInvokedEventArgs(PointInvokedEvent, new ChartPointInfo(hit.Value, Model)));
    }
    private void ClearHover()
    { HoverPoint = null; IsToolTipVisible = false; PseudoClasses.Set(":hover", false); }

    /// <summary>Encodes the displayed data or plot. Requires an arranged PART_Plot for image exports.</summary>
    public ChartExportPayload CreateExport(ChartExportFormat format)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (_plot == null && format != ChartExportFormat.Csv) throw new InvalidOperationException("The template has no PART_Plot.");
        byte[] bytes = format switch
        {
            ChartExportFormat.Png => _plot!.ExportPng(),
            ChartExportFormat.Svg => Encoding.UTF8.GetBytes(_plot!.ExportSvg()),
            _ => Encoding.UTF8.GetBytes(ChartCsvExporter.Export(Model?.Snapshot ?? ChartDataSnapshot.Empty))
        };
        if (bytes.Length == 0) throw new InvalidOperationException("The chart must be arranged at a nonzero size before export.");
        string extension = format.ToString().ToLowerInvariant();
        return new ChartExportPayload("chart." + extension, format switch
        { ChartExportFormat.Png => "image/png", ChartExportFormat.Svg => "image/svg+xml", _ => "text/csv;charset=utf-8" }, bytes);
    }
    /// <summary>Saves an export and reports errors through ExportError instead of an unobserved async-void exception.</summary>
    public async Task SaveAsync(ChartExportFormat format)
    {
        if (IsExporting) return;
        _exportCancellation = new CancellationTokenSource();
        IsExporting = true;
        ExportError = null;
        PseudoClasses.Set(":exporting", true);
        UpdateState();
        try
        {
            StatusText = "Exporting chart…";
            ChartExportPayload payload = CreateExport(format);
            await (ExportService ?? new StorageChartExportService(this)).SaveAsync(payload, _exportCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ExportError = error.Message; }
        finally
        {
            _exportCancellation.Dispose();
            _exportCancellation = null;
            IsExporting = false;
            PseudoClasses.Set(":exporting", false);
            UpdateState();
        }
    }
}

internal sealed class ProChartAutomationPeer(ProChart owner) : ControlAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(ProChart);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    protected override string? GetNameCore() => string.IsNullOrEmpty(base.GetNameCore()) ? owner.Title : base.GetNameCore();
    protected override string? GetHelpTextCore() => owner.StatusText + ". Use the chart toolbar or focus the plot: arrows pan, plus/minus zoom, Home resets.";
}
