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
public class ProChart : TemplatedControl
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

    public static readonly DirectProperty<ProChart, ChartModel?> ModelProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartModel?>(nameof(Model), x => x.Model, (x, v) => x.Model = v);
    public ChartModel? Model { get => _model; set => SetAndRaise(ModelProperty, ref _model, value); }
    public static readonly DirectProperty<ProChart, IChartExportService?> ExportServiceProperty =
        AvaloniaProperty.RegisterDirect<ProChart, IChartExportService?>(nameof(ExportService), x => x.ExportService, (x, v) => x.ExportService = v);
    public IChartExportService? ExportService { get => _exportService; set => SetAndRaise(ExportServiceProperty, ref _exportService, value); }
    public static readonly DirectProperty<ProChart, ChartPointerTool> PointerToolProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartPointerTool>(nameof(PointerTool), x => x.PointerTool, (x, v) => x.PointerTool = v);
    public ChartPointerTool PointerTool
    {
        get => _pointerTool;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (SetAndRaise(PointerToolProperty, ref _pointerTool, value) && Model != null)
                Model.Interaction.PointerTool = value;
        }
    }
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProChart, string>(nameof(Title), "Chart");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly StyledProperty<string> SubtitleProperty =
        AvaloniaProperty.Register<ProChart, string>(nameof(Subtitle), "");
    public string Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(Header), null);
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(Footer), null);
    public object? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(HeaderTemplate), null);
    public IDataTemplate? HeaderTemplate { get => GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(FooterTemplate), null);
    public IDataTemplate? FooterTemplate { get => GetValue(FooterTemplateProperty); set => SetValue(FooterTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> LegendItemTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(LegendItemTemplate), null);
    public IDataTemplate? LegendItemTemplate { get => GetValue(LegendItemTemplateProperty); set => SetValue(LegendItemTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> ToolTipTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(ToolTipTemplate), null);
    public IDataTemplate? ToolTipTemplate { get => GetValue(ToolTipTemplateProperty); set => SetValue(ToolTipTemplateProperty, value); }
    public static readonly StyledProperty<IDataTemplate?> EmptyTemplateProperty =
        AvaloniaProperty.Register<ProChart, IDataTemplate?>(nameof(EmptyTemplate), null);
    public IDataTemplate? EmptyTemplate { get => GetValue(EmptyTemplateProperty); set => SetValue(EmptyTemplateProperty, value); }
    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<ProChart, object?>(nameof(EmptyContent), "No data to display");
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }
    public static readonly StyledProperty<ControlTheme?> LegendThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(LegendTheme), null);
    public ControlTheme? LegendTheme { get => GetValue(LegendThemeProperty); set => SetValue(LegendThemeProperty, value); }
    public static readonly StyledProperty<ControlTheme?> ToolbarThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(ToolbarTheme), null);
    public ControlTheme? ToolbarTheme { get => GetValue(ToolbarThemeProperty); set => SetValue(ToolbarThemeProperty, value); }
    public static readonly StyledProperty<ControlTheme?> NavigatorThemeProperty =
        AvaloniaProperty.Register<ProChart, ControlTheme?>(nameof(NavigatorTheme), null);
    public ControlTheme? NavigatorTheme { get => GetValue(NavigatorThemeProperty); set => SetValue(NavigatorThemeProperty, value); }
    public static readonly StyledProperty<SkiaChartStyle?> ChartStyleProperty =
        AvaloniaProperty.Register<ProChart, SkiaChartStyle?>(nameof(ChartStyle), null);
    public SkiaChartStyle? ChartStyle { get => GetValue(ChartStyleProperty); set => SetValue(ChartStyleProperty, value); }
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowLegend), true);
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowToolbar), true);
    public bool ShowToolbar { get => GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }
    public static readonly StyledProperty<bool> ShowNavigatorProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowNavigator), true);
    public bool ShowNavigator { get => GetValue(ShowNavigatorProperty); set => SetValue(ShowNavigatorProperty, value); }
    public static readonly StyledProperty<bool> ShowCrosshairProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowCrosshair), true);
    public bool ShowCrosshair { get => GetValue(ShowCrosshairProperty); set => SetValue(ShowCrosshairProperty, value); }
    public static readonly StyledProperty<bool> ShowToolTipsProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(ShowToolTips), true);
    public bool ShowToolTips { get => GetValue(ShowToolTipsProperty); set => SetValue(ShowToolTipsProperty, value); }
    public static readonly StyledProperty<bool> EnablePanZoomProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(EnablePanZoom), true);
    public bool EnablePanZoom { get => GetValue(EnablePanZoomProperty); set => SetValue(EnablePanZoomProperty, value); }
    public static readonly StyledProperty<bool> EnableKeyboardNavigationProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(EnableKeyboardNavigation), true);
    public bool EnableKeyboardNavigation { get => GetValue(EnableKeyboardNavigationProperty); set => SetValue(EnableKeyboardNavigationProperty, value); }
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<ProChart, bool>(nameof(IsLoading), false);
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ProChart, string?>(nameof(ErrorMessage), null);
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<ProChart, double>(nameof(ZoomStep), 0.25, validate: value => double.IsFinite(value) && value > 0 && value <= 10);
    public double ZoomStep { get => GetValue(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }
    public static readonly StyledProperty<int> MinWindowCountProperty =
        AvaloniaProperty.Register<ProChart, int>(nameof(MinWindowCount), 2, validate: value => value >= 1);
    public int MinWindowCount { get => GetValue(MinWindowCountProperty); set => SetValue(MinWindowCountProperty, value); }
    private bool _hasData = false;
    public static readonly DirectProperty<ProChart, bool> HasDataProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(HasData), x => x.HasData);
    public bool HasData { get => _hasData; private set => SetAndRaise(HasDataProperty, ref _hasData, value); }
    private bool _isEmpty = true;
    public static readonly DirectProperty<ProChart, bool> IsEmptyProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsEmpty), x => x.IsEmpty);
    public bool IsEmpty { get => _isEmpty; private set => SetAndRaise(IsEmptyProperty, ref _isEmpty, value); }
    private bool _hasError = false;
    public static readonly DirectProperty<ProChart, bool> HasErrorProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(HasError), x => x.HasError);
    public bool HasError { get => _hasError; private set => SetAndRaise(HasErrorProperty, ref _hasError, value); }
    private bool _isLegendVisible = true;
    public static readonly DirectProperty<ProChart, bool> IsLegendVisibleProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsLegendVisible), x => x.IsLegendVisible);
    public bool IsLegendVisible { get => _isLegendVisible; private set => SetAndRaise(IsLegendVisibleProperty, ref _isLegendVisible, value); }
    private bool _isExporting = false;
    public static readonly DirectProperty<ProChart, bool> IsExportingProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsExporting), x => x.IsExporting);
    public bool IsExporting { get => _isExporting; private set => SetAndRaise(IsExportingProperty, ref _isExporting, value); }
    private string _statusText = "No data";
    public static readonly DirectProperty<ProChart, string> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<ProChart, string>(nameof(StatusText), x => x.StatusText);
    public string StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }
    private string? _exportError = null;
    public static readonly DirectProperty<ProChart, string?> ExportErrorProperty =
        AvaloniaProperty.RegisterDirect<ProChart, string?>(nameof(ExportError), x => x.ExportError);
    public string? ExportError { get => _exportError; private set => SetAndRaise(ExportErrorProperty, ref _exportError, value); }
    private ChartPointInfo? _hoverPoint = null;
    public static readonly DirectProperty<ProChart, ChartPointInfo?> HoverPointProperty =
        AvaloniaProperty.RegisterDirect<ProChart, ChartPointInfo?>(nameof(HoverPoint), x => x.HoverPoint);
    public ChartPointInfo? HoverPoint { get => _hoverPoint; private set => SetAndRaise(HoverPointProperty, ref _hoverPoint, value); }
    private bool _isToolTipVisible = false;
    public static readonly DirectProperty<ProChart, bool> IsToolTipVisibleProperty =
        AvaloniaProperty.RegisterDirect<ProChart, bool>(nameof(IsToolTipVisible), x => x.IsToolTipVisible);
    public bool IsToolTipVisible { get => _isToolTipVisible; private set => SetAndRaise(IsToolTipVisibleProperty, ref _isToolTipVisible, value); }
    private double _toolTipX = 0;
    public static readonly DirectProperty<ProChart, double> ToolTipXProperty =
        AvaloniaProperty.RegisterDirect<ProChart, double>(nameof(ToolTipX), x => x.ToolTipX);
    public double ToolTipX { get => _toolTipX; private set => SetAndRaise(ToolTipXProperty, ref _toolTipX, value); }
    private double _toolTipY = 0;
    public static readonly DirectProperty<ProChart, double> ToolTipYProperty =
        AvaloniaProperty.RegisterDirect<ProChart, double>(nameof(ToolTipY), x => x.ToolTipY);
    public double ToolTipY { get => _toolTipY; private set => SetAndRaise(ToolTipYProperty, ref _toolTipY, value); }

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
                if (_legend[i].Name != (snapshot.Series[i].Name ?? $"Series {i + 1}")) { same = false; break; }
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
            IReadOnlyList<SKColor> palette = ChartStyle?.SeriesColors ?? SkiaChartStyle.DefaultSeriesColors;
            SKColor color = palette.Count > 0 ? palette[i % palette.Count] : SKColors.DodgerBlue;
            ChartColor? pinned = series.Style?.StrokeColor ?? series.Style?.FillColor;
            if (pinned is ChartColor core) color = new SKColor(core.Red, core.Green, core.Blue, core.Alpha);
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
        if ((end - start.Value).Length > 5) return;
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
