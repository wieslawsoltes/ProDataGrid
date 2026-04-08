using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Collections;
using DataGridSample.Charting;
using DataGridSample.Models;
using ProCharts;
using ProCharts.Skia;
using ProDataGrid.Charting;
using SkiaSharp;

namespace DataGridSample.ViewModels
{
    public enum ChartSampleKind
    {
        Line,
        Area,
        Column,
        Bar,
        StackedColumn,
        StackedBar,
        StackedArea,
        Waterfall,
        Histogram,
        Pareto,
        Radar,
        BoxWhisker,
        Funnel,
        Scatter,
        Bubble,
        Pie,
        Donut,
        Combo,
        CalculatedMeasures,
        Candlestick,
        HollowCandlestick,
        Ohlc,
        Hlc,
        HeikinAshi,
        Renko,
        Range,
        LineBreak,
        Kagi,
        PointFigure
    }

    public enum ChartGroupBy
    {
        None,
        Region,
        Segment,
        Category,
        Product
    }

    public sealed class ChartPalette
    {
        public ChartPalette(string name, IReadOnlyList<SKColor> colors)
        {
            Name = name;
            Colors = colors;
        }

        public string Name { get; }

        public IReadOnlyList<SKColor> Colors { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class ChartSampleViewModel : INotifyPropertyChanged, IDisposable
    {
        private enum ChartSeriesFormat
        {
            Default,
            Currency,
            Number
        }

        private readonly List<ChartSeriesFormat> _seriesFormats = new();
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly IReadOnlyList<ChartPalette> _palettes;
        private readonly FinancialChartSampleDataSource _financialChartDataSource;
        private SkiaChartStyle _chartStyle = new();
        private ChartPalette _selectedPalette;
        private bool _isFinancialSample;
        private string _dataTabDescription = "Sort, filter, and group the grid to see the chart update.";
        private int _seriesStyleCountHint;

        private bool _showLegend = true;
        private ChartLegendPosition _legendPosition = ChartLegendPosition.Right;
        private SkiaLegendFlow _legendFlow = SkiaLegendFlow.Column;
        private bool _legendWrap = true;
        private bool _legendGroupStackedSeries;
        private bool _showToolTips = true;
        private bool _showGridlines = true;
        private bool _showCategoryGridlines = true;
        private bool _showDataLabels;
        private bool _useFormattedLabels = true;
        private bool _showValueAxis = true;
        private bool _showCategoryAxis = true;
        private int _axisTickCount = 5;
        private double _seriesStrokeWidth = 2d;
        private double _axisStrokeWidth = 1d;
        private double _areaFillOpacity = 0.25d;
        private double _bubbleMinRadius = 6d;
        private double _bubbleMaxRadius = 24d;
        private double _bubbleFillOpacity = 0.65d;
        private double _bubbleStrokeWidth = 1d;
        private double _labelSize = 11d;
        private double _legendTextSize = 11d;
        private double _dataLabelTextSize = 10d;
        private double _dataLabelPadding = 3d;
        private double _dataLabelOffset = 6d;
        private ChartAxisKind _categoryAxisKind = ChartAxisKind.Category;
        private string? _categoryAxisTitle;
        private string? _valueAxisTitle;
        private string _categoryAxisMinimumText = string.Empty;
        private string _categoryAxisMaximumText = string.Empty;
        private string _valueAxisMinimumText = string.Empty;
        private string _valueAxisMaximumText = string.Empty;
        private ChartGroupBy _groupBy = ChartGroupBy.None;
        private DataGridChartAggregation _downsampleAggregation = DataGridChartAggregation.Average;
        private int _maxPoints = 200;
        private double _financialBodyWidthRatio = 0.56d;
        private double _financialBoxWidthRatio = 0.82d;
        private double _financialTickWidthRatio = 0.22d;
        private double _financialWickStrokeWidth = 1.2d;
        private double _financialBodyStrokeWidth = 1d;
        private double _financialBodyFillOpacity = 0.45d;
        private double _financialLastPriceLineWidth = 1.1d;
        private double _financialBrickSize = 1.5d;
        private int _financialLineBreakPeriod = 3;
        private double _financialKagiReversalAmount = 1.8d;
        private double _financialPointFigureBoxSize = 1.2d;
        private int _financialPointFigureReversalBoxes = 3;
        private bool _financialHollowBullishBodies;
        private bool _financialShowLastPriceLine = true;

        public ChartSampleViewModel(ChartSampleKind kind)
        {
            Kind = kind;
            ToolTipFormatter = FormatToolTip;
            Items = new ObservableCollection<SalesRecord>(SalesRecordSampleData.CreateSalesRecords(400));
            FinancialItems = new ObservableCollection<FinancialCandleRecord>(CreateFinancialCandles());
            ItemsView = new DataGridCollectionView(Items);
            _financialChartDataSource = new FinancialChartSampleDataSource(FinancialItems, ChartSeriesKind.Candlestick);

            ChartData = new DataGridChartModel
            {
                View = ItemsView,
                CategoryPath = nameof(SalesRecord.OrderDate),
                GroupMode = DataGridChartGroupMode.LeafItems,
                DownsampleAggregation = _downsampleAggregation,
                Culture = _culture,
                UseIncrementalUpdates = false
            };

            Chart = new ChartModel();

            _palettes = CreatePalettes();
            _selectedPalette = _palettes[0];

            ConfigureForKind(kind);
            ApplyGrouping();
            ApplyAxisSettings();
            ApplyFormatting();
            UpdateChartStyle();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ChartSampleKind Kind { get; }

        public string Title { get; private set; } = string.Empty;

        public string Description { get; private set; } = string.Empty;

        public ObservableCollection<SalesRecord> Items { get; }

        public ObservableCollection<FinancialCandleRecord> FinancialItems { get; }

        public DataGridCollectionView ItemsView { get; }

        public DataGridChartModel ChartData { get; }

        public ChartModel Chart { get; }

        public SkiaChartStyle ChartStyle
        {
            get => _chartStyle;
            private set
            {
                if (ReferenceEquals(_chartStyle, value))
                {
                    return;
                }

                _chartStyle = value;
                OnPropertyChanged(nameof(ChartStyle));
            }
        }

        public IReadOnlyList<ChartPalette> Palettes => _palettes;

        public ChartPalette SelectedPalette
        {
            get => _selectedPalette;
            set
            {
                if (value == null)
                {
                    return;
                }

                if (!SetField(ref _selectedPalette, value, nameof(SelectedPalette)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public IReadOnlyList<ChartLegendPosition> LegendPositions { get; } = new[]
        {
            ChartLegendPosition.Top,
            ChartLegendPosition.Bottom,
            ChartLegendPosition.Left,
            ChartLegendPosition.Right
        };

        public IReadOnlyList<SkiaLegendFlow> LegendFlows { get; } = new[]
        {
            SkiaLegendFlow.Column,
            SkiaLegendFlow.Row
        };

        public IReadOnlyList<ChartAxisKind> CategoryAxisKinds { get; } = new[]
        {
            ChartAxisKind.Category,
            ChartAxisKind.Value,
            ChartAxisKind.DateTime
        };

        public IReadOnlyList<ChartGroupBy> GroupByOptions { get; } = new[]
        {
            ChartGroupBy.None,
            ChartGroupBy.Region,
            ChartGroupBy.Segment,
            ChartGroupBy.Category,
            ChartGroupBy.Product
        };

        public IReadOnlyList<DataGridChartAggregation> DownsampleAggregations { get; } = new[]
        {
            DataGridChartAggregation.Sum,
            DataGridChartAggregation.Average,
            DataGridChartAggregation.Min,
            DataGridChartAggregation.Max,
            DataGridChartAggregation.Count,
            DataGridChartAggregation.First,
            DataGridChartAggregation.Last
        };

        public bool IsFinancialSample => _isFinancialSample;

        public bool IsSalesSample => !_isFinancialSample;

        public bool SupportsCategoryAxisKind => !_isFinancialSample;

        public bool SupportsCategoryAxisRange => !_isFinancialSample;

        public bool SupportsGroupingOptions => !_isFinancialSample;

        public bool SupportsDownsampleAggregation => !_isFinancialSample;

        public bool SupportsMaxPoints => !_isFinancialSample;

        public bool SupportsFinancialSettings => _isFinancialSample;

        public bool SupportsFinancialBodyWidth =>
            Kind == ChartSampleKind.Candlestick ||
            Kind == ChartSampleKind.HollowCandlestick ||
            Kind == ChartSampleKind.HeikinAshi ||
            Kind == ChartSampleKind.Range;

        public bool SupportsFinancialBoxWidth =>
            Kind == ChartSampleKind.Renko ||
            Kind == ChartSampleKind.LineBreak ||
            Kind == ChartSampleKind.PointFigure;

        public bool SupportsFinancialTickWidth =>
            Kind == ChartSampleKind.Ohlc || Kind == ChartSampleKind.Hlc;

        public bool SupportsFinancialWickSettings =>
            Kind == ChartSampleKind.Candlestick ||
            Kind == ChartSampleKind.HollowCandlestick ||
            Kind == ChartSampleKind.HeikinAshi ||
            Kind == ChartSampleKind.Ohlc ||
            Kind == ChartSampleKind.Hlc ||
            Kind == ChartSampleKind.Range ||
            Kind == ChartSampleKind.Kagi ||
            Kind == ChartSampleKind.PointFigure;

        public bool SupportsFinancialBodySettings =>
            Kind == ChartSampleKind.Candlestick ||
            Kind == ChartSampleKind.HollowCandlestick ||
            Kind == ChartSampleKind.HeikinAshi ||
            Kind == ChartSampleKind.Range ||
            Kind == ChartSampleKind.Renko ||
            Kind == ChartSampleKind.LineBreak;

        public bool SupportsFinancialHollowToggle =>
            SupportsFinancialBodySettings && Kind != ChartSampleKind.HollowCandlestick;

        public bool SupportsBrickSize =>
            Kind == ChartSampleKind.Renko || Kind == ChartSampleKind.Range;

        public bool SupportsLineBreakPeriod => Kind == ChartSampleKind.LineBreak;

        public bool SupportsKagiReversalAmount => Kind == ChartSampleKind.Kagi;

        public bool SupportsPointFigureBoxSize => Kind == ChartSampleKind.PointFigure;

        public bool SupportsPointFigureReversalBoxes => Kind == ChartSampleKind.PointFigure;

        public string FinancialPrimaryStrokeLabel =>
            Kind == ChartSampleKind.Kagi || Kind == ChartSampleKind.PointFigure
                ? "Line stroke width"
                : "Wick stroke width";

        public string FinancialBrickSizeLabel =>
            Kind == ChartSampleKind.Renko
                ? "Renko brick size"
                : Kind == ChartSampleKind.Range
                    ? "Range bar size"
                    : "Point & Figure box size";

        public string DataTabDescription => _dataTabDescription;

        public Func<SkiaChartHitTestResult, string> ToolTipFormatter { get; }

        public bool ShowLegend
        {
            get => _showLegend;
            set
            {
                if (!SetField(ref _showLegend, value, nameof(ShowLegend)))
                {
                    return;
                }

                Chart.Legend.IsVisible = value;
            }
        }

        public ChartLegendPosition LegendPosition
        {
            get => _legendPosition;
            set
            {
                if (!SetField(ref _legendPosition, value, nameof(LegendPosition)))
                {
                    return;
                }

                Chart.Legend.Position = value;
            }
        }

        public SkiaLegendFlow LegendFlow
        {
            get => _legendFlow;
            set
            {
                if (!SetField(ref _legendFlow, value, nameof(LegendFlow)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool LegendWrap
        {
            get => _legendWrap;
            set
            {
                if (!SetField(ref _legendWrap, value, nameof(LegendWrap)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool LegendGroupStackedSeries
        {
            get => _legendGroupStackedSeries;
            set
            {
                if (!SetField(ref _legendGroupStackedSeries, value, nameof(LegendGroupStackedSeries)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool ShowToolTips
        {
            get => _showToolTips;
            set => SetField(ref _showToolTips, value, nameof(ShowToolTips));
        }

        public bool ShowGridlines
        {
            get => _showGridlines;
            set
            {
                if (!SetField(ref _showGridlines, value, nameof(ShowGridlines)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool ShowCategoryGridlines
        {
            get => _showCategoryGridlines;
            set
            {
                if (!SetField(ref _showCategoryGridlines, value, nameof(ShowCategoryGridlines)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool ShowDataLabels
        {
            get => _showDataLabels;
            set
            {
                if (!SetField(ref _showDataLabels, value, nameof(ShowDataLabels)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool UseFormattedLabels
        {
            get => _useFormattedLabels;
            set
            {
                if (!SetField(ref _useFormattedLabels, value, nameof(UseFormattedLabels)))
                {
                    return;
                }

                ApplyFormatting();
            }
        }

        public bool ShowValueAxis
        {
            get => _showValueAxis;
            set
            {
                if (!SetField(ref _showValueAxis, value, nameof(ShowValueAxis)))
                {
                    return;
                }

                Chart.ValueAxis.IsVisible = value;
            }
        }

        public bool ShowCategoryAxis
        {
            get => _showCategoryAxis;
            set
            {
                if (!SetField(ref _showCategoryAxis, value, nameof(ShowCategoryAxis)))
                {
                    return;
                }

                Chart.CategoryAxis.IsVisible = value;
            }
        }

        public int AxisTickCount
        {
            get => _axisTickCount;
            set
            {
                if (!SetField(ref _axisTickCount, value, nameof(AxisTickCount)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double SeriesStrokeWidth
        {
            get => _seriesStrokeWidth;
            set
            {
                if (!SetField(ref _seriesStrokeWidth, value, nameof(SeriesStrokeWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double AxisStrokeWidth
        {
            get => _axisStrokeWidth;
            set
            {
                if (!SetField(ref _axisStrokeWidth, value, nameof(AxisStrokeWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double AreaFillOpacity
        {
            get => _areaFillOpacity;
            set
            {
                if (!SetField(ref _areaFillOpacity, value, nameof(AreaFillOpacity)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double BubbleMinRadius
        {
            get => _bubbleMinRadius;
            set
            {
                if (!SetField(ref _bubbleMinRadius, value, nameof(BubbleMinRadius)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double BubbleMaxRadius
        {
            get => _bubbleMaxRadius;
            set
            {
                if (!SetField(ref _bubbleMaxRadius, value, nameof(BubbleMaxRadius)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double BubbleFillOpacity
        {
            get => _bubbleFillOpacity;
            set
            {
                if (!SetField(ref _bubbleFillOpacity, value, nameof(BubbleFillOpacity)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double BubbleStrokeWidth
        {
            get => _bubbleStrokeWidth;
            set
            {
                if (!SetField(ref _bubbleStrokeWidth, value, nameof(BubbleStrokeWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double LabelSize
        {
            get => _labelSize;
            set
            {
                if (!SetField(ref _labelSize, value, nameof(LabelSize)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double LegendTextSize
        {
            get => _legendTextSize;
            set
            {
                if (!SetField(ref _legendTextSize, value, nameof(LegendTextSize)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double DataLabelTextSize
        {
            get => _dataLabelTextSize;
            set
            {
                if (!SetField(ref _dataLabelTextSize, value, nameof(DataLabelTextSize)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double DataLabelPadding
        {
            get => _dataLabelPadding;
            set
            {
                if (!SetField(ref _dataLabelPadding, value, nameof(DataLabelPadding)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double DataLabelOffset
        {
            get => _dataLabelOffset;
            set
            {
                if (!SetField(ref _dataLabelOffset, value, nameof(DataLabelOffset)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public ChartAxisKind CategoryAxisKind
        {
            get => _categoryAxisKind;
            set
            {
                if (!SetField(ref _categoryAxisKind, value, nameof(CategoryAxisKind)))
                {
                    return;
                }

                Chart.CategoryAxis.Kind = value;
                Chart.CategoryAxis.Minimum = ParseCategoryAxisValue(_categoryAxisMinimumText);
                Chart.CategoryAxis.Maximum = ParseCategoryAxisValue(_categoryAxisMaximumText);
                ApplyFormatting();
            }
        }

        public string? CategoryAxisTitle
        {
            get => _categoryAxisTitle;
            set
            {
                if (!SetField(ref _categoryAxisTitle, value, nameof(CategoryAxisTitle)))
                {
                    return;
                }

                Chart.CategoryAxis.Title = value;
            }
        }

        public string? ValueAxisTitle
        {
            get => _valueAxisTitle;
            set
            {
                if (!SetField(ref _valueAxisTitle, value, nameof(ValueAxisTitle)))
                {
                    return;
                }

                Chart.ValueAxis.Title = value;
            }
        }

        public string CategoryAxisMinimumText
        {
            get => _categoryAxisMinimumText;
            set
            {
                if (!SetField(ref _categoryAxisMinimumText, value, nameof(CategoryAxisMinimumText)))
                {
                    return;
                }

                Chart.CategoryAxis.Minimum = ParseCategoryAxisValue(value);
            }
        }

        public string CategoryAxisMaximumText
        {
            get => _categoryAxisMaximumText;
            set
            {
                if (!SetField(ref _categoryAxisMaximumText, value, nameof(CategoryAxisMaximumText)))
                {
                    return;
                }

                Chart.CategoryAxis.Maximum = ParseCategoryAxisValue(value);
            }
        }

        public string ValueAxisMinimumText
        {
            get => _valueAxisMinimumText;
            set
            {
                if (!SetField(ref _valueAxisMinimumText, value, nameof(ValueAxisMinimumText)))
                {
                    return;
                }

                Chart.ValueAxis.Minimum = ParseNumericValue(value);
            }
        }

        public string ValueAxisMaximumText
        {
            get => _valueAxisMaximumText;
            set
            {
                if (!SetField(ref _valueAxisMaximumText, value, nameof(ValueAxisMaximumText)))
                {
                    return;
                }

                Chart.ValueAxis.Maximum = ParseNumericValue(value);
            }
        }

        public ChartGroupBy GroupBy
        {
            get => _groupBy;
            set
            {
                if (!SetField(ref _groupBy, value, nameof(GroupBy)))
                {
                    return;
                }

                ApplyGrouping();
            }
        }

        public DataGridChartAggregation DownsampleAggregation
        {
            get => _downsampleAggregation;
            set
            {
                if (!SetField(ref _downsampleAggregation, value, nameof(DownsampleAggregation)))
                {
                    return;
                }

                ChartData.DownsampleAggregation = value;
            }
        }

        public int MaxPoints
        {
            get => _maxPoints;
            set
            {
                if (!SetField(ref _maxPoints, value, nameof(MaxPoints)))
                {
                    return;
                }

                Chart.Request.MaxPoints = _maxPoints > 0 ? _maxPoints : null;
            }
        }

        public double FinancialBodyWidthRatio
        {
            get => _financialBodyWidthRatio;
            set
            {
                if (!SetField(ref _financialBodyWidthRatio, value, nameof(FinancialBodyWidthRatio)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialBoxWidthRatio
        {
            get => _financialBoxWidthRatio;
            set
            {
                if (!SetField(ref _financialBoxWidthRatio, value, nameof(FinancialBoxWidthRatio)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialTickWidthRatio
        {
            get => _financialTickWidthRatio;
            set
            {
                if (!SetField(ref _financialTickWidthRatio, value, nameof(FinancialTickWidthRatio)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialWickStrokeWidth
        {
            get => _financialWickStrokeWidth;
            set
            {
                if (!SetField(ref _financialWickStrokeWidth, value, nameof(FinancialWickStrokeWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialBodyStrokeWidth
        {
            get => _financialBodyStrokeWidth;
            set
            {
                if (!SetField(ref _financialBodyStrokeWidth, value, nameof(FinancialBodyStrokeWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialBodyFillOpacity
        {
            get => _financialBodyFillOpacity;
            set
            {
                if (!SetField(ref _financialBodyFillOpacity, value, nameof(FinancialBodyFillOpacity)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialLastPriceLineWidth
        {
            get => _financialLastPriceLineWidth;
            set
            {
                if (!SetField(ref _financialLastPriceLineWidth, value, nameof(FinancialLastPriceLineWidth)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool FinancialHollowBullishBodies
        {
            get => _financialHollowBullishBodies;
            set
            {
                if (!SetField(ref _financialHollowBullishBodies, value, nameof(FinancialHollowBullishBodies)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public bool FinancialShowLastPriceLine
        {
            get => _financialShowLastPriceLine;
            set
            {
                if (!SetField(ref _financialShowLastPriceLine, value, nameof(FinancialShowLastPriceLine)))
                {
                    return;
                }

                UpdateChartStyle();
            }
        }

        public double FinancialBrickSize
        {
            get => _financialBrickSize;
            set
            {
                if (!SetField(ref _financialBrickSize, value, nameof(FinancialBrickSize)))
                {
                    return;
                }

                RefreshFinancialDataSource();
            }
        }

        public int FinancialLineBreakPeriod
        {
            get => _financialLineBreakPeriod;
            set
            {
                if (!SetField(ref _financialLineBreakPeriod, value, nameof(FinancialLineBreakPeriod)))
                {
                    return;
                }

                RefreshFinancialDataSource();
            }
        }

        public double FinancialKagiReversalAmount
        {
            get => _financialKagiReversalAmount;
            set
            {
                if (!SetField(ref _financialKagiReversalAmount, value, nameof(FinancialKagiReversalAmount)))
                {
                    return;
                }

                RefreshFinancialDataSource();
            }
        }

        public double FinancialPointFigureBoxSize
        {
            get => _financialPointFigureBoxSize;
            set
            {
                if (!SetField(ref _financialPointFigureBoxSize, value, nameof(FinancialPointFigureBoxSize)))
                {
                    return;
                }

                RefreshFinancialDataSource();
            }
        }

        public int FinancialPointFigureReversalBoxes
        {
            get => _financialPointFigureReversalBoxes;
            set
            {
                if (!SetField(ref _financialPointFigureReversalBoxes, value, nameof(FinancialPointFigureReversalBoxes)))
                {
                    return;
                }

                RefreshFinancialDataSource();
            }
        }

        private void ConfigureForKind(ChartSampleKind kind)
        {
            ChartData.Series.Clear();
            _seriesFormats.Clear();
            _seriesStyleCountHint = 0;
            _isFinancialSample = false;
            _dataTabDescription = "Sort, filter, and group the grid to see the chart update.";
            Chart.DataSource = ChartData;
            Chart.Request.WindowStart = null;
            Chart.Request.WindowCount = null;
            Chart.SecondaryValueAxis.IsVisible = false;
            Chart.SecondaryValueAxis.Title = null;
            Chart.SecondaryValueAxis.Minimum = null;
            Chart.SecondaryValueAxis.Maximum = null;
            Chart.SecondaryValueAxis.Kind = ChartAxisKind.Value;
            Chart.SecondaryValueAxis.LabelFormatter = null;

            switch (kind)
            {
                case ChartSampleKind.Line:
                    Title = "Line chart";
                    Description = "Trend lines over order dates using sales and profit totals.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Order date";
                    _valueAxisTitle = "Sales / Profit";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 200;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Line, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Line, ChartSeriesFormat.Currency, trendlineType: ChartTrendlineType.Linear);
                    break;
                case ChartSampleKind.Area:
                    Title = "Area chart";
                    Description = "Filled trend areas for sales and profit across time.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Order date";
                    _valueAxisTitle = "Sales / Profit";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 200;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Area, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Area, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Column:
                    Title = "Column chart";
                    Description = "Compare sales and profit totals grouped by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Column, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Column, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Bar:
                    Title = "Bar chart";
                    Description = "Horizontal bars showing sales and profit by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Bar, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Bar, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.StackedColumn:
                    Title = "Stacked column chart";
                    Description = "Stacked totals of sales and profit grouped by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _legendGroupStackedSeries = true;
                    _showDataLabels = true;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.StackedColumn, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.StackedColumn, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.StackedBar:
                    Title = "Stacked bar chart";
                    Description = "Stacked horizontal bars for sales and profit by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _legendGroupStackedSeries = true;
                    _showDataLabels = true;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.StackedBar, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.StackedBar, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.StackedArea:
                    Title = "Stacked area chart";
                    Description = "Stacked areas for sales and profit across order dates.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Order date";
                    _valueAxisTitle = "Sales / Profit";
                    _legendGroupStackedSeries = true;
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 200;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.StackedArea, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.StackedArea, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Waterfall:
                    Title = "Waterfall chart";
                    Description = "Sequential profit changes over time with running totals.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Order date";
                    _valueAxisTitle = "Profit change";
                    _showDataLabels = true;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 60;
                    AddSeries("Profit change", nameof(SalesRecord.Profit), ChartSeriesKind.Waterfall, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Histogram:
                    Title = "Histogram";
                    Description = "Distribution of sales values grouped into bins.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Sales bins";
                    _valueAxisTitle = "Count";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 0;
                    AddSeries("Sales distribution", nameof(SalesRecord.Sales), ChartSeriesKind.Histogram, ChartSeriesFormat.Number);
                    break;
                case ChartSampleKind.Pareto:
                    Title = "Pareto chart";
                    Description = "Sales distribution with cumulative percentage trend.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Sales bins";
                    _valueAxisTitle = "Count";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 0;
                    Chart.SecondaryValueAxis.IsVisible = true;
                    Chart.SecondaryValueAxis.Title = "Cumulative %";
                    Chart.SecondaryValueAxis.Minimum = 0;
                    Chart.SecondaryValueAxis.Maximum = 100;
                    Chart.SecondaryValueAxis.LabelFormatter = value => (value / 100d).ToString("P0", _culture);
                    AddSeries("Sales distribution", nameof(SalesRecord.Sales), ChartSeriesKind.Pareto, ChartSeriesFormat.Number);
                    break;
                case ChartSampleKind.Radar:
                    Title = "Radar chart";
                    Description = "Compare sales and profit by region in a radial layout.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = false;
                    _showGridlines = true;
                    _showCategoryGridlines = false;
                    _showCategoryAxis = false;
                    _showValueAxis = false;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Radar, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Radar, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.BoxWhisker:
                    Title = "Box & whisker chart";
                    Description = "Distribution of sales and profit values.";
                    ChartData.CategoryPath = nameof(SalesRecord.OrderDate);
                    _categoryAxisTitle = "Series";
                    _valueAxisTitle = "Value";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.BoxWhisker, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.BoxWhisker, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Funnel:
                    Title = "Funnel chart";
                    Description = "Sales totals by region shown as a funnel.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _showGridlines = false;
                    _showCategoryGridlines = false;
                    _showCategoryAxis = false;
                    _showValueAxis = false;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Funnel, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Scatter:
                    Title = "Scatter chart";
                    Description = "Profit plotted against sales using a numeric category axis.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisKind = ChartAxisKind.Value;
                    _categoryAxisTitle = "Sales";
                    _valueAxisTitle = "Profit";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 200;
                    AddSeries(
                        "Profit vs Sales",
                        nameof(SalesRecord.Profit),
                        ChartSeriesKind.Scatter,
                        ChartSeriesFormat.Currency,
                        xValuePath: nameof(SalesRecord.Sales),
                        errorBarType: ChartErrorBarType.Percentage,
                        errorBarValue: 10d);
                    break;
                case ChartSampleKind.Bubble:
                    Title = "Bubble chart";
                    Description = "Profit plotted against sales with quantity driving bubble size.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisKind = ChartAxisKind.Value;
                    _categoryAxisTitle = "Sales";
                    _valueAxisTitle = "Profit";
                    _showDataLabels = false;
                    _showCategoryGridlines = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 200;
                    AddSeries(
                        "Profit vs Sales",
                        nameof(SalesRecord.Profit),
                        ChartSeriesKind.Bubble,
                        ChartSeriesFormat.Currency,
                        xValuePath: nameof(SalesRecord.Sales),
                        sizePath: nameof(SalesRecord.Quantity));
                    break;
                case ChartSampleKind.Pie:
                    Title = "Pie chart";
                    Description = "Sales share by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _showGridlines = false;
                    _showCategoryGridlines = false;
                    _showCategoryAxis = false;
                    _showValueAxis = false;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Pie, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Donut:
                    Title = "Donut chart";
                    Description = "Sales share by region with a hollow center.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _showGridlines = false;
                    _showCategoryGridlines = false;
                    _showCategoryAxis = false;
                    _showValueAxis = false;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Donut, ChartSeriesFormat.Currency);
                    break;
                case ChartSampleKind.Combo:
                    Title = "Combo chart";
                    Description = "Columns for sales with a profit trend line by region.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Sales / Profit";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = false;
                    _downsampleAggregation = DataGridChartAggregation.Sum;
                    _maxPoints = 0;
                    Chart.SecondaryValueAxis.IsVisible = true;
                    Chart.SecondaryValueAxis.Title = "Profit";
                    AddSeries("Sales", nameof(SalesRecord.Sales), ChartSeriesKind.Column, ChartSeriesFormat.Currency);
                    AddSeries("Profit", nameof(SalesRecord.Profit), ChartSeriesKind.Line, ChartSeriesFormat.Currency, valueAxisAssignment: ChartValueAxisAssignment.Secondary);
                    break;
                case ChartSampleKind.CalculatedMeasures:
                    Title = "Calculated measures";
                    Description = "Excel-style formulas compute per-row measures before aggregation.";
                    ChartData.CategoryPath = nameof(SalesRecord.Region);
                    _categoryAxisTitle = "Region";
                    _valueAxisTitle = "Per-unit values";
                    _groupBy = ChartGroupBy.Region;
                    _showDataLabels = true;
                    _downsampleAggregation = DataGridChartAggregation.Average;
                    _maxPoints = 0;
                    var listSeparator = ChartData.Culture.TextInfo.ListSeparator;
                    var decimalSeparator = ChartData.Culture.NumberFormat.NumberDecimalSeparator;
                    var argumentSeparator = string.IsNullOrWhiteSpace(listSeparator)
                        ? ","
                        : listSeparator[0].ToString();
                    if (!string.IsNullOrEmpty(decimalSeparator) &&
                        argumentSeparator.Length > 0 &&
                        argumentSeparator[0] == decimalSeparator[0])
                    {
                        argumentSeparator = ";";
                    }
                    AddFormulaSeries(
                        "Sales per Unit",
                        $"IF(Quantity=0{argumentSeparator}0{argumentSeparator}Sales/Quantity)",
                        ChartSeriesKind.Column,
                        ChartSeriesFormat.Currency,
                        DataGridChartAggregation.Average);
                    AddFormulaSeries(
                        "Profit per Unit",
                        $"IF(Quantity=0{argumentSeparator}0{argumentSeparator}Profit/Quantity)",
                        ChartSeriesKind.Line,
                        ChartSeriesFormat.Currency,
                        DataGridChartAggregation.Average);
                    break;
                case ChartSampleKind.Candlestick:
                    ConfigureFinancialSample(
                        "Candlestick chart",
                        "Intraday OHLC candles for a simulated trading instrument, with adjustable body width, hollow bullish candles, and a last-price line.",
                        ChartSeriesKind.Candlestick);
                    break;
                case ChartSampleKind.HollowCandlestick:
                    ConfigureFinancialSample(
                        "Hollow candle chart",
                        "Classic hollow candles use body fill for the session move and outline color for close-versus-previous-close direction.",
                        ChartSeriesKind.HollowCandlestick);
                    break;
                case ChartSampleKind.Ohlc:
                    ConfigureFinancialSample(
                        "OHLC chart",
                        "Open-high-low-close bars for the same intraday data set, using professional tick and wick styling controls.",
                        ChartSeriesKind.Ohlc);
                    break;
                case ChartSampleKind.Hlc:
                    ConfigureFinancialSample(
                        "HLC chart",
                        "High-low-close bars for compact range analysis when the open tick is not needed.",
                        ChartSeriesKind.Hlc);
                    break;
                case ChartSampleKind.HeikinAshi:
                    ConfigureFinancialSample(
                        "Heikin-Ashi chart",
                        "Derived candles smooth the raw session noise to emphasize directional runs and reversals.",
                        ChartSeriesKind.HeikinAshi);
                    break;
                case ChartSampleKind.Renko:
                    ConfigureFinancialSample(
                        "Renko chart",
                        "Price-only bricks derived from the source candles. Adjust brick size to tighten or loosen trend sensitivity.",
                        ChartSeriesKind.Renko);
                    break;
                case ChartSampleKind.Range:
                    ConfigureFinancialSample(
                        "Range chart",
                        "Derived price bars compress time and only print when the session moves far enough to complete a fixed trading range.",
                        ChartSeriesKind.Range);
                    break;
                case ChartSampleKind.LineBreak:
                    ConfigureFinancialSample(
                        "Line break chart",
                        "Three-line break style boxes derived from close direction, useful for trend confirmation without fixed time spacing.",
                        ChartSeriesKind.LineBreak);
                    break;
                case ChartSampleKind.Kagi:
                    ConfigureFinancialSample(
                        "Kagi chart",
                        "Reversal-threshold price lines compress repeated moves into stepped directional segments that emphasize supply and demand swings.",
                        ChartSeriesKind.Kagi);
                    break;
                case ChartSampleKind.PointFigure:
                    ConfigureFinancialSample(
                        "Point & figure chart",
                        "Column-based X/O price action that removes time and focuses on box-size and reversal-driven breakouts.",
                        ChartSeriesKind.PointFigure);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown chart sample.");
            }

            ChartData.DownsampleAggregation = _downsampleAggregation;
            Chart.Request.MaxPoints = !_isFinancialSample && _maxPoints > 0 ? _maxPoints : null;
        }

        private void ApplyAxisSettings()
        {
            Chart.Legend.IsVisible = _showLegend;
            Chart.Legend.Position = _legendPosition;
            Chart.CategoryAxis.IsVisible = _showCategoryAxis;
            Chart.CategoryAxis.Kind = _categoryAxisKind;
            Chart.CategoryAxis.Title = _categoryAxisTitle;
            Chart.CategoryAxis.Minimum = ParseCategoryAxisValue(_categoryAxisMinimumText);
            Chart.CategoryAxis.Maximum = ParseCategoryAxisValue(_categoryAxisMaximumText);
            Chart.ValueAxis.IsVisible = _showValueAxis;
            Chart.ValueAxis.Title = _valueAxisTitle;
            Chart.ValueAxis.Minimum = ParseNumericValue(_valueAxisMinimumText);
            Chart.ValueAxis.Maximum = ParseNumericValue(_valueAxisMaximumText);
        }

        private void ApplyGrouping()
        {
            if (_isFinancialSample)
            {
                Chart.Refresh();
                return;
            }

            ItemsView.GroupDescriptions.Clear();
            var path = ResolveGroupPath(_groupBy);
            if (!string.IsNullOrWhiteSpace(path))
            {
                ItemsView.GroupDescriptions.Add(new DataGridPathGroupDescription(path));
                ChartData.GroupMode = DataGridChartGroupMode.TopLevel;
            }
            else
            {
                ChartData.GroupMode = DataGridChartGroupMode.LeafItems;
            }

            Chart.Refresh();
        }

        private void ApplyFormatting()
        {
            for (var i = 0; i < ChartData.Series.Count; i++)
            {
                var series = ChartData.Series[i];
                if (!_useFormattedLabels)
                {
                    series.DataLabelFormatter = null;
                    continue;
                }

                var format = i < _seriesFormats.Count ? _seriesFormats[i] : ChartSeriesFormat.Default;
                series.DataLabelFormatter = value => FormatValue(format, value);
            }

            if (_useFormattedLabels)
            {
                var axisFormat = _seriesFormats.Contains(ChartSeriesFormat.Currency)
                    ? ChartSeriesFormat.Currency
                    : ChartSeriesFormat.Number;

                Chart.ValueAxis.LabelFormatter = value => FormatValue(axisFormat, value);

                Chart.CategoryAxis.LabelFormatter = _categoryAxisKind switch
                {
                    ChartAxisKind.DateTime => value => FormatDateValue(value),
                    ChartAxisKind.Value => value => FormatValue(axisFormat, value),
                    _ => null
                };
            }
            else
            {
                Chart.ValueAxis.LabelFormatter = null;
                Chart.CategoryAxis.LabelFormatter = null;
            }
        }

        private void UpdateChartStyle()
        {
            var palette = _selectedPalette ?? (_palettes.Count > 0 ? _palettes[0] : null);
            var style = new SkiaChartStyle(ChartStyle)
            {
                AxisTickCount = _axisTickCount,
                ShowGridlines = _showGridlines,
                ShowCategoryGridlines = _showCategoryGridlines,
                ShowDataLabels = _showDataLabels,
                LegendFlow = _legendFlow,
                LegendWrap = _legendWrap,
                LegendGroupStackedSeries = _legendGroupStackedSeries,
                SeriesStrokeWidth = (float)_seriesStrokeWidth,
                AxisStrokeWidth = (float)_axisStrokeWidth,
                AreaFillOpacity = (float)_areaFillOpacity,
                BubbleMinRadius = (float)_bubbleMinRadius,
                BubbleMaxRadius = (float)_bubbleMaxRadius,
                BubbleFillOpacity = (float)_bubbleFillOpacity,
                BubbleStrokeWidth = (float)_bubbleStrokeWidth,
                LabelSize = (float)_labelSize,
                LegendTextSize = (float)_legendTextSize,
                DataLabelTextSize = (float)_dataLabelTextSize,
                DataLabelPadding = (float)_dataLabelPadding,
                DataLabelOffset = (float)_dataLabelOffset,
                SeriesColors = palette?.Colors ?? SkiaChartStyle.DefaultSeriesColors,
                SeriesStyles = BuildSeriesStyles(),
                FinancialIncreaseColor = new SKColor(42, 214, 168),
                FinancialDecreaseColor = new SKColor(255, 84, 104),
                FinancialBodyFillOpacity = (float)_financialBodyFillOpacity,
                FinancialBodyWidthRatio = (float)_financialBodyWidthRatio,
                FinancialBoxWidthRatio = (float)_financialBoxWidthRatio,
                FinancialTickWidthRatio = (float)_financialTickWidthRatio,
                FinancialWickStrokeWidth = (float)_financialWickStrokeWidth,
                FinancialBodyStrokeWidth = (float)_financialBodyStrokeWidth,
                FinancialHollowBullishBodies = _financialHollowBullishBodies,
                FinancialShowLastPriceLine = _financialShowLastPriceLine,
                FinancialLastPriceLineColor = SKColors.Transparent,
                FinancialLastPriceLineWidth = (float)_financialLastPriceLineWidth,
                FinancialLastPriceLabelText = new SKColor(10, 18, 28)
            };

            ChartStyle = style;
        }

        private IReadOnlyList<SkiaChartSeriesStyle>? BuildSeriesStyles()
        {
            if (_seriesStyleCountHint == 0)
            {
                return null;
            }

            var palette = _selectedPalette ?? (_palettes.Count > 0 ? _palettes[0] : null);
            var colors = palette?.Colors ?? SkiaChartStyle.DefaultSeriesColors;
            var count = Math.Max(2, _seriesStyleCountHint);
            var styles = new List<SkiaChartSeriesStyle>(count);

            for (var i = 0; i < count; i++)
            {
                var baseColor = colors[i % colors.Count];
                if (i == 0)
                {
                    styles.Add(new SkiaChartSeriesStyle
                    {
                        FillGradient = new SkiaChartGradient
                        {
                            Direction = SkiaGradientDirection.Vertical,
                            Colors = new[]
                            {
                                baseColor,
                                Blend(baseColor, SKColors.White, 0.7f)
                            }
                        },
                        MarkerShape = SkiaMarkerShape.Circle,
                        MarkerSize = 3.5f
                    });
                }
                else if (i == 1)
                {
                    styles.Add(new SkiaChartSeriesStyle
                    {
                        LineStyle = SkiaLineStyle.DashDot,
                        MarkerShape = SkiaMarkerShape.Diamond,
                        MarkerSize = 4f,
                        MarkerStrokeColor = SKColors.White,
                        MarkerStrokeWidth = 1f
                    });
                }
                else
                {
                    styles.Add(new SkiaChartSeriesStyle());
                }
            }

            return styles;
        }

        private void ConfigureFinancialSample(string title, string description, ChartSeriesKind kind)
        {
            Title = title;
            Description = description;
            _isFinancialSample = true;
            _dataTabDescription = kind switch
            {
                ChartSeriesKind.Renko => "Edit the source OHLC rows below to refresh the derived Renko bricks.",
                ChartSeriesKind.Range => "Edit the source OHLC rows below to refresh the derived range bars.",
                ChartSeriesKind.LineBreak => "Edit the source OHLC rows below to refresh the derived line break boxes.",
                ChartSeriesKind.HeikinAshi => "Edit the source OHLC rows below to refresh the derived Heikin-Ashi candles.",
                ChartSeriesKind.HollowCandlestick => "Edit the source OHLC rows below to refresh the hollow candle session state and previous-close coloring.",
                ChartSeriesKind.Kagi => "Edit the source OHLC rows below to refresh the derived Kagi reversal segments.",
                ChartSeriesKind.PointFigure => "Edit the source OHLC rows below to refresh the derived Point & Figure columns.",
                _ => "Edit OHLC and volume values below to refresh the chart."
            };
            _showLegend = false;
            _showGridlines = true;
            _showCategoryGridlines = true;
            _showDataLabels = false;
            _showCategoryAxis = true;
            _showValueAxis = true;
            _categoryAxisKind = ChartAxisKind.Category;
            _categoryAxisTitle =
                kind == ChartSeriesKind.Renko ||
                kind == ChartSeriesKind.Range ||
                kind == ChartSeriesKind.LineBreak ||
                kind == ChartSeriesKind.Kagi ||
                kind == ChartSeriesKind.PointFigure
                    ? "Derived step"
                    : "Time";
            _valueAxisTitle = "Price";
            _downsampleAggregation = DataGridChartAggregation.Average;
            _maxPoints = 0;
            _financialBodyWidthRatio = 0.56d;
            _financialBoxWidthRatio = 0.82d;
            _financialTickWidthRatio = 0.22d;
            _financialWickStrokeWidth = 1.2d;
            _financialBodyStrokeWidth = 1d;
            _financialBodyFillOpacity =
                kind == ChartSeriesKind.Renko || kind == ChartSeriesKind.LineBreak
                    ? 0.62d
                    : kind == ChartSeriesKind.Range
                        ? 0.52d
                    : 0.45d;
            _financialLastPriceLineWidth = 1.1d;
            _financialHollowBullishBodies =
                kind == ChartSeriesKind.Candlestick ||
                kind == ChartSeriesKind.HeikinAshi ||
                kind == ChartSeriesKind.Range ||
                kind == ChartSeriesKind.Renko ||
                kind == ChartSeriesKind.LineBreak;
            if (kind == ChartSeriesKind.HollowCandlestick)
            {
                _financialHollowBullishBodies = false;
            }
            _financialShowLastPriceLine = true;
            _financialBrickSize = 1.5d;
            _financialLineBreakPeriod = 3;
            _financialKagiReversalAmount = 1.8d;
            _financialPointFigureBoxSize = 1.2d;
            _financialPointFigureReversalBoxes = 3;
            AddSeriesFormat(ChartSeriesFormat.Currency);
            _financialChartDataSource.SeriesKind = kind;
            _financialChartDataSource.BrickSize = _financialBrickSize;
            _financialChartDataSource.RangeSize = _financialBrickSize;
            _financialChartDataSource.LineBreakPeriod = _financialLineBreakPeriod;
            _financialChartDataSource.KagiReversalAmount = _financialKagiReversalAmount;
            _financialChartDataSource.PointFigureBoxSize = _financialPointFigureBoxSize;
            _financialChartDataSource.PointFigureReversalBoxes = _financialPointFigureReversalBoxes;
            Chart.DataSource = _financialChartDataSource;
            var windowCount = Math.Min(40, FinancialItems.Count);
            Chart.Request.WindowCount = windowCount;
            Chart.Request.WindowStart = Math.Max(0, FinancialItems.Count - windowCount);
        }

        private void RefreshFinancialDataSource()
        {
            if (!_isFinancialSample)
            {
                return;
            }

            _financialChartDataSource.BrickSize = _financialBrickSize;
            _financialChartDataSource.RangeSize = _financialBrickSize;
            _financialChartDataSource.LineBreakPeriod = _financialLineBreakPeriod;
            _financialChartDataSource.KagiReversalAmount = _financialKagiReversalAmount;
            _financialChartDataSource.PointFigureBoxSize = _financialPointFigureBoxSize;
            _financialChartDataSource.PointFigureReversalBoxes = _financialPointFigureReversalBoxes;
            _financialChartDataSource.Invalidate();
            Chart.Refresh();
        }

        private static SKColor Blend(SKColor from, SKColor to, float t)
        {
            if (t < 0f)
            {
                t = 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
            }

            var r = (byte)Math.Round(from.Red + (to.Red - from.Red) * t);
            var g = (byte)Math.Round(from.Green + (to.Green - from.Green) * t);
            var b = (byte)Math.Round(from.Blue + (to.Blue - from.Blue) * t);
            var a = (byte)Math.Round(from.Alpha + (to.Alpha - from.Alpha) * t);
            return new SKColor(r, g, b, a);
        }

        private DataGridChartSeriesDefinition AddSeries(
            string name,
            string valuePath,
            ChartSeriesKind kind,
            ChartSeriesFormat format,
            DataGridChartAggregation aggregation = DataGridChartAggregation.Sum,
            string? xValuePath = null,
            string? sizePath = null,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary,
            ChartTrendlineType trendlineType = ChartTrendlineType.None,
            int trendlinePeriod = 2,
            ChartErrorBarType errorBarType = ChartErrorBarType.None,
            double errorBarValue = 1d)
        {
            var definition = new DataGridChartSeriesDefinition
            {
                Name = name,
                ValuePath = valuePath,
                Kind = kind,
                Aggregation = aggregation,
                XValuePath = xValuePath,
                SizePath = sizePath,
                ValueAxisAssignment = valueAxisAssignment,
                TrendlineType = trendlineType,
                TrendlinePeriod = trendlinePeriod,
                ErrorBarType = errorBarType,
                ErrorBarValue = errorBarValue
            };

            AddSeriesFormat(format);
            ChartData.Series.Add(definition);
            return definition;
        }

        private DataGridChartSeriesDefinition AddFormulaSeries(
            string name,
            string formula,
            ChartSeriesKind kind,
            ChartSeriesFormat format,
            DataGridChartAggregation aggregation = DataGridChartAggregation.Sum,
            ChartValueAxisAssignment valueAxisAssignment = ChartValueAxisAssignment.Primary)
        {
            var definition = new DataGridChartSeriesDefinition
            {
                Name = name,
                Formula = formula,
                Kind = kind,
                Aggregation = aggregation,
                ValueAxisAssignment = valueAxisAssignment
            };

            AddSeriesFormat(format);
            ChartData.Series.Add(definition);
            return definition;
        }

        private void AddSeriesFormat(ChartSeriesFormat format)
        {
            _seriesFormats.Add(format);
            _seriesStyleCountHint++;
        }

        private static IReadOnlyList<FinancialCandleRecord> CreateFinancialCandles()
        {
            var list = new List<FinancialCandleRecord>(64);
            var timestamp = new DateTime(2026, 4, 2, 9, 30, 0, DateTimeKind.Local);
            var price = 184.20d;

            for (var i = 0; i < 64; i++)
            {
                var open = price;
                var drift = i switch
                {
                    < 10 => 0.34d,
                    < 22 => -0.48d,
                    < 36 => 0.18d,
                    < 50 => 0.41d,
                    _ => -0.12d
                };
                var wave = Math.Sin(i * 0.45d) * 0.62d;
                var pulse = i % 13 == 0 ? 1.15d : 0d;
                var close = Math.Clamp(open + drift + wave + pulse - (i % 7 == 0 ? 0.55d : 0d), 176d, 192d);
                var high = Math.Max(open, close) + 0.55d + Math.Abs(Math.Cos(i * 0.31d)) * 1.1d;
                var low = Math.Min(open, close) - 0.48d - Math.Abs(Math.Sin(i * 0.38d)) * 0.92d;
                var volume = 240000d + Math.Abs(close - open) * 145000d + (Math.Sin(i * 0.29d) + 1d) * 35000d;

                list.Add(new FinancialCandleRecord
                {
                    Timestamp = timestamp.AddMinutes(i * 15),
                    Open = Math.Round(open, 2),
                    High = Math.Round(high, 2),
                    Low = Math.Round(low, 2),
                    Close = Math.Round(close, 2),
                    Volume = Math.Round(volume, 0)
                });

                price = close;
            }

            return list;
        }

        private static IReadOnlyList<ChartPalette> CreatePalettes()
        {
            return new[]
            {
                new ChartPalette("Classic", new[]
                {
                    new SKColor(33, 150, 243),
                    new SKColor(244, 67, 54),
                    new SKColor(76, 175, 80),
                    new SKColor(255, 152, 0),
                    new SKColor(156, 39, 176)
                }),
                new ChartPalette("Warm", new[]
                {
                    new SKColor(255, 87, 34),
                    new SKColor(255, 193, 7),
                    new SKColor(205, 220, 57),
                    new SKColor(255, 152, 0),
                    new SKColor(121, 85, 72)
                }),
                new ChartPalette("Cool", new[]
                {
                    new SKColor(3, 169, 244),
                    new SKColor(0, 188, 212),
                    new SKColor(76, 175, 80),
                    new SKColor(63, 81, 181),
                    new SKColor(96, 125, 139)
                }),
                new ChartPalette("Mono", new[]
                {
                    new SKColor(55, 71, 79),
                    new SKColor(96, 125, 139),
                    new SKColor(120, 144, 156),
                    new SKColor(144, 164, 174),
                    new SKColor(176, 190, 197)
                })
            };
        }

        private string FormatToolTip(SkiaChartHitTestResult hit)
        {
            var valueFormat = ChartSeriesFormat.Default;
            if (hit.SeriesIndex >= 0 && hit.SeriesIndex < _seriesFormats.Count)
            {
                valueFormat = _seriesFormats[hit.SeriesIndex];
            }

            if ((hit.SeriesKind == ChartSeriesKind.Candlestick ||
                 hit.SeriesKind == ChartSeriesKind.HollowCandlestick ||
                 hit.SeriesKind == ChartSeriesKind.Ohlc ||
                 hit.SeriesKind == ChartSeriesKind.HeikinAshi ||
                 hit.SeriesKind == ChartSeriesKind.Range ||
                 hit.SeriesKind == ChartSeriesKind.Renko ||
                 hit.SeriesKind == ChartSeriesKind.LineBreak ||
                 hit.SeriesKind == ChartSeriesKind.Kagi ||
                 hit.SeriesKind == ChartSeriesKind.PointFigure) &&
                hit.OpenValue.HasValue &&
                hit.HighValue.HasValue &&
                hit.LowValue.HasValue &&
                hit.CloseValue.HasValue)
            {
                var openText = FormatValue(valueFormat, hit.OpenValue.Value);
                var highText = FormatValue(valueFormat, hit.HighValue.Value);
                var lowText = FormatValue(valueFormat, hit.LowValue.Value);
                var closeText = FormatValue(valueFormat, hit.CloseValue.Value);
                var header = !string.IsNullOrWhiteSpace(hit.Category)
                    ? $"{hit.SeriesName ?? "Series"} - {hit.Category}"
                    : hit.SeriesName ?? "Series";
                return $"{header}: O {openText}, H {highText}, L {lowText}, C {closeText}";
            }

            if (hit.SeriesKind == ChartSeriesKind.Hlc &&
                hit.HighValue.HasValue &&
                hit.LowValue.HasValue &&
                hit.CloseValue.HasValue)
            {
                var highText = FormatValue(valueFormat, hit.HighValue.Value);
                var lowText = FormatValue(valueFormat, hit.LowValue.Value);
                var closeText = FormatValue(valueFormat, hit.CloseValue.Value);
                var header = !string.IsNullOrWhiteSpace(hit.Category)
                    ? $"{hit.SeriesName ?? "Series"} - {hit.Category}"
                    : hit.SeriesName ?? "Series";
                return $"{header}: H {highText}, L {lowText}, C {closeText}";
            }

            var valueText = FormatValue(valueFormat, hit.Value);
            if ((hit.SeriesKind == ChartSeriesKind.Scatter || hit.SeriesKind == ChartSeriesKind.Bubble) &&
                hit.XValue.HasValue &&
                !double.IsNaN(hit.XValue.Value) &&
                !double.IsInfinity(hit.XValue.Value))
            {
                var xText = FormatValue(ChartSeriesFormat.Number, hit.XValue.Value);
                return $"{hit.SeriesName ?? "Series"}: ({xText}, {valueText})";
            }

            if (!string.IsNullOrWhiteSpace(hit.Category))
            {
                return $"{hit.SeriesName ?? "Series"} - {hit.Category}: {valueText}";
            }

            return $"{hit.SeriesName ?? "Series"}: {valueText}";
        }

        private string FormatValue(ChartSeriesFormat format, double value)
        {
            return format switch
            {
                ChartSeriesFormat.Currency => value.ToString("C0", _culture),
                ChartSeriesFormat.Number => value.ToString("N0", _culture),
                _ => value.ToString("G", _culture)
            };
        }

        private string FormatDateValue(double value)
        {
            try
            {
                return DateTime.FromOADate(value).ToString("d", _culture);
            }
            catch (ArgumentException)
            {
                return value.ToString("G", _culture);
            }
        }

        private static string? ResolveGroupPath(ChartGroupBy groupBy)
        {
            return groupBy switch
            {
                ChartGroupBy.Region => nameof(SalesRecord.Region),
                ChartGroupBy.Segment => nameof(SalesRecord.Segment),
                ChartGroupBy.Category => nameof(SalesRecord.Category),
                ChartGroupBy.Product => nameof(SalesRecord.Product),
                _ => null
            };
        }

        private double? ParseCategoryAxisValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (_categoryAxisKind == ChartAxisKind.DateTime)
            {
                if (DateTime.TryParse(text, _culture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var date))
                {
                    return date.ToOADate();
                }
            }

            return ParseNumericValue(text);
        }

        private double? ParseNumericValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, _culture, out var value))
            {
                return value;
            }

            return null;
        }

        private bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void Dispose()
        {
            Chart.Dispose();
            ChartData.Dispose();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
