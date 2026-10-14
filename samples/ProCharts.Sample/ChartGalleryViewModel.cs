// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
using System.Reactive;
using ReactiveUI;
using ProCharts.Skia;

namespace ProCharts.Sample;

public sealed record GalleryExample(string Id, string Name, string Description, ChartSeriesKind Kind);

/// <summary>Framework-agnostic gallery state. The composition root supplies scheduling and theme changes.</summary>
public sealed class ChartGalleryViewModel : ReactiveObject, IDisposable
{
    private readonly Action<Action> _dispatch;
    private readonly Timer _timer;
    private GalleryExample _selectedExample;
    private ChartSeriesKind _selectedKind;
    private ChartModel _model = new();
    private SeriesVisibilityChartDataSource? _filter;
    private StreamingMultiSeriesChartDataSource? _stream;
    private bool _isStreaming, _isCompact, _showCrosshair = true, _showLegend = true, _showNavigator = true, _showLabels, _isLoading;
    private bool _disposed;
    private string? _error;
    private string _title = "", _subtitle = "", _dataSummary = "";
    private SkiaChartStyle _style = CreateStyle(false);
    private long _nextX;
    public IReadOnlyList<GalleryExample> Examples { get; } = Array.AsReadOnly(new[]
    {
        new GalleryExample("overview", "Overview", "Layered area, line and column series", ChartSeriesKind.Area),
        new GalleryExample("financial", "Financial", "OHLC candles and a moving average", ChartSeriesKind.Candlestick),
        new GalleryExample("scatter", "Scientific", "Numeric X, markers and bubble sizes", ChartSeriesKind.Scatter),
        new GalleryExample("range", "Confidence bands", "Intervals, gaps and numeric coordinates", ChartSeriesKind.RangeArea),
        new GalleryExample("stacked", "Composition", "Normalized stacked comparisons", ChartSeriesKind.StackedColumn100),
        new GalleryExample("distribution", "Distributions", "Histogram and statistical chart types", ChartSeriesKind.Histogram),
        new GalleryExample("heatmap", "Heatmap", "Two-dimensional value matrix", ChartSeriesKind.Heatmap),
        new GalleryExample("treemap", "Hierarchy", "Double-click branches to drill down", ChartSeriesKind.Treemap),
        new GalleryExample("donut", "Proportions", "Pie, donut and funnel charts", ChartSeriesKind.Donut),
        new GalleryExample("gauge", "Gauge", "Bounded radial indicators", ChartSeriesKind.Gauge),
        new GalleryExample("stream", "100,000 rows", "Bounded streaming and downsampling", ChartSeriesKind.Line),
        new GalleryExample("states", "Control states", "Empty, loading and error templates", ChartSeriesKind.Line)
    });
    public IReadOnlyList<ChartSeriesKind> ChartKinds { get; } = Array.AsReadOnly(Enum.GetValues<ChartSeriesKind>());
    public ReactiveCommand<Unit, Unit> ThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> TemplateCommand { get; }
    public ReactiveCommand<Unit, Unit> StreamCommand { get; }
    public ReactiveCommand<Unit, Unit> AppendCommand { get; }
    public ReactiveCommand<Unit, Unit> EmptyCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadingCommand { get; }
    public ReactiveCommand<Unit, Unit> ErrorCommand { get; }
    public ChartGalleryViewModel(Action<Action> dispatch, Action toggleTheme)
    {
        _dispatch = dispatch;
        _selectedExample = Examples[0];
        ThemeCommand = ReactiveCommand.Create(toggleTheme);
        TemplateCommand = ReactiveCommand.Create(() => IsCompact = !IsCompact);
        StreamCommand = ReactiveCommand.Create(() =>
        {
            if (_stream == null) { SelectedExample = Examples.First(x => x.Id == "stream"); }
            IsStreaming = !IsStreaming;
        });
        AppendCommand = ReactiveCommand.Create(() => { if (_stream == null) SelectedExample = Examples.First(x => x.Id == "stream"); AppendBatch(); });
        EmptyCommand = ReactiveCommand.Create(() => { IsLoading = false; ErrorMessage = null; ReplaceModel(new ChartModel()); Title = "No observations yet"; });
        ReloadCommand = ReactiveCommand.Create(LoadExample);
        LoadingCommand = ReactiveCommand.Create(() => { ErrorMessage = null; IsLoading = !IsLoading; });
        ErrorCommand = ReactiveCommand.Create(() => { IsLoading = false; ErrorMessage = ErrorMessage == null ? "The data provider is unavailable. This is a simulated recoverable error." : null; });
        _timer = new Timer(_ => _dispatch(() => { if (!_disposed && IsStreaming) AppendBatch(); }), null, 100, 100);
        LoadExample();
    }
    public GalleryExample SelectedExample
    {
        get => _selectedExample;
        set { if (value != null && !ReferenceEquals(_selectedExample, value)) { this.RaiseAndSetIfChanged(ref _selectedExample, value); LoadExample(); } }
    }
    public ChartSeriesKind SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_selectedKind == value) return;
            this.RaiseAndSetIfChanged(ref _selectedKind, value);
            LoadKind(value);
        }
    }
    public ChartModel Model { get => _model; private set => this.RaiseAndSetIfChanged(ref _model, value); }
    public SkiaChartStyle ChartStyle { get => _style; private set => this.RaiseAndSetIfChanged(ref _style, value); }
    public string Title { get => _title; private set => this.RaiseAndSetIfChanged(ref _title, value); }
    public string Subtitle { get => _subtitle; private set => this.RaiseAndSetIfChanged(ref _subtitle, value); }
    public string DataSummary { get => _dataSummary; private set => this.RaiseAndSetIfChanged(ref _dataSummary, value); }
    public bool IsCompact { get => _isCompact; set => this.RaiseAndSetIfChanged(ref _isCompact, value); }
    public bool ShowCrosshair { get => _showCrosshair; set => this.RaiseAndSetIfChanged(ref _showCrosshair, value); }
    public bool ShowLegend { get => _showLegend; set => this.RaiseAndSetIfChanged(ref _showLegend, value); }
    public bool ShowNavigator { get => _showNavigator; set => this.RaiseAndSetIfChanged(ref _showNavigator, value); }
    public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public string? ErrorMessage { get => _error; set => this.RaiseAndSetIfChanged(ref _error, value); }
    public bool ShowLabels
    { get => _showLabels; set { this.RaiseAndSetIfChanged(ref _showLabels, value); ChartStyle = CreateStyle(value); } }
    public bool IsStreaming
    { get => _isStreaming; private set { this.RaiseAndSetIfChanged(ref _isStreaming, value); this.RaisePropertyChanged(nameof(StreamCaption)); } }
    public string StreamCaption => IsStreaming ? "Pause feed" : "Start live feed";
    public long ReceivedRows => _stream?.TotalSamples ?? 0;
    private static SkiaChartStyle CreateStyle(bool labels) => new()
    {
        ShowDataLabels = labels, ShowCategoryGridlines = true, SeriesStrokeWidth = 2.3f,
        AreaFillOpacity = 0.18f, PaddingLeft = 56, PaddingRight = 24, PaddingBottom = 44,
        LabelSize = 11, HitTestRadius = 14, FinancialShowLastPriceLine = true,
        HistogramBinCount = 16, PieInnerRadiusRatio = 0.66f
    };
    private void LoadExample()
    {
        IsStreaming = false;
        IsLoading = false;
        ErrorMessage = null;
        this.RaiseAndSetIfChanged(ref _selectedKind, SelectedExample.Kind, nameof(SelectedKind));
        if (SelectedExample.Id == "stream") { LoadStream(); return; }
        if (SelectedExample.Id == "states") { ReplaceModel(new ChartModel()); Title = "A state for every situation"; Subtitle = "Use Empty, Loading and Error to exercise the control states."; return; }
        Install(GalleryData.Create(SelectedExample.Kind, SelectedExample.Id == "overview", SelectedExample.Id == "financial"), SelectedExample.Kind);
        Title = SelectedExample.Id == "overview" ? "Traffic by acquisition channel" : SelectedExample.Name;
        Subtitle = SelectedExample.Description + " · Synthetic demonstration data";
    }
    private void LoadKind(ChartSeriesKind kind)
    {
        IsStreaming = false; IsLoading = false; ErrorMessage = null;
        Install(GalleryData.Create(kind), kind);
        Title = kind.ToString();
        Subtitle = "Interactive chart type gallery · All data is synthetic";
    }
    private void Install(IChartDataSource source, ChartSeriesKind kind)
    {
        ChartModel model = new();
        if (source is not HierarchyChartDataSource)
        {
            SeriesVisibilityChartDataSource filter = new(source);
            model.DataSource = filter;
            ReplaceModel(model);
            _filter = filter;
        }
        else { model.DataSource = source; ReplaceModel(model); }
        Model.CategoryAxis.Title = kind is ChartSeriesKind.Scatter or ChartSeriesKind.Bubble or ChartSeriesKind.RangeArea ? "Position" : "Observation";
        Model.CategoryAxis.Kind = kind is ChartSeriesKind.Scatter or ChartSeriesKind.Bubble or ChartSeriesKind.RangeArea ? ChartAxisKind.Value : ChartAxisKind.Category;
        Model.ValueAxis.Title = "Value";
        Model.ValueAxis.ValueFormat = new ChartValueFormat { MaximumFractionDigits = 2 };
        DataSummary = $"{Model.Snapshot.Series.Count} SERIES  /  {((source as IChartWindowInfoProvider)?.GetTotalCategoryCount() ?? Model.Snapshot.Categories.Count):N0} ROWS";
    }
    private void LoadStream()
    {
        StreamingMultiSeriesChartDataSource stream = new(100_000, new[]
        {
            new StreamingChartSeries("CPU", style: GalleryData.Style(0)),
            new StreamingChartSeries("Memory", style: GalleryData.Style(1)),
            new StreamingChartSeries("Network", style: GalleryData.Style(2))
        });
        const int count = 100_000;
        double[] xs = new double[count];
        double?[] ys = new double?[count * 3];
        for (int i = 0; i < count; i++)
        {
            xs[i] = i;
            ys[i * 3] = 45 + 22 * Math.Sin(i * 0.002) + 3 * Math.Sin(i * 0.31);
            ys[i * 3 + 1] = 60 + 12 * Math.Cos(i * 0.0013);
            ys[i * 3 + 2] = 24 + 15 * Math.Sin(i * 0.0064);
        }
        stream.AppendRange(xs, ys);
        ChartModel model = new();
        model.Request.MaxPoints = 1200;
        model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
        SeriesVisibilityChartDataSource filter = new(stream);
        model.DataSource = filter;
        ReplaceModel(model);
        _filter = filter; _stream = stream; _nextX = count;
        Model.ValueAxis.Title = "Utilization (%)";
        Model.ValueAxis.Minimum = 0; Model.ValueAxis.Maximum = 100;
        Title = "Live infrastructure telemetry";
        Subtitle = "100,000 retained rows · 3 synchronized channels · min/max downsampling · 32 rows per update";
        DataSummary = "100,000 ROW CAPACITY  /  300,000 INPUT VALUES";
        this.RaisePropertyChanged(nameof(ReceivedRows));
    }
    private void AppendBatch()
    {
        if (_stream == null) return;
        double[] xs = new double[32]; double?[] ys = new double?[96];
        for (int i = 0; i < 32; i++)
        {
            double x = _nextX++;
            xs[i] = x;
            ys[i * 3] = 45 + 22 * Math.Sin(x * 0.002) + 3 * Math.Sin(x * 0.31);
            ys[i * 3 + 1] = 60 + 12 * Math.Cos(x * 0.0013);
            ys[i * 3 + 2] = 24 + 15 * Math.Sin(x * 0.0064);
        }
        _stream.AppendRange(xs, ys);
        this.RaisePropertyChanged(nameof(ReceivedRows));
    }
    private void ReplaceModel(ChartModel model)
    {
        IsStreaming = false;
        ChartModel old = Model;
        _filter?.Dispose(); _filter = null; _stream = null;
        Model = model;
        old.Dispose();
        this.RaisePropertyChanged(nameof(ReceivedRows));
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose(); _filter?.Dispose(); Model.Dispose();
        ThemeCommand.Dispose(); TemplateCommand.Dispose(); StreamCommand.Dispose(); AppendCommand.Dispose();
        EmptyCommand.Dispose(); ReloadCommand.Dispose(); LoadingCommand.Dispose(); ErrorCommand.Dispose();
    }
}
