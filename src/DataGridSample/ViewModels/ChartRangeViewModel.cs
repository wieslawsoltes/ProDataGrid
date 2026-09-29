using System;
using ProCharts;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>Editable analytical bands and a dense-data demonstration of coordinated interval reduction.</summary>
    public sealed class ChartRangeViewModel : ReactiveObject
    {
        private double _bandWidth = 2;
        private bool _showGaps = true;
        private bool _largeDataset;
        private bool _reduceData = true;

        public ChartRangeViewModel()
        {
            Chart = new ChartModel();
            Chart.Legend.Position = ChartLegendPosition.Bottom;
            Chart.CategoryAxis.Title = "Observation";
            Chart.ValueAxis.Title = "Signal";
            Chart.SeriesStyles = new[]
            {
                new ChartSeriesStyle { StrokeWidth = 1 },
                new ChartSeriesStyle { MarkerShape = ChartMarkerShape.None, StrokeWidth = 2 }
            };
            Chart.SnapshotChanged += (_, _) => this.RaisePropertyChanged(nameof(RenderingSummary));
            Rebuild();
        }

        public ChartModel Chart { get; }

        public double BandWidth
        {
            get => _bandWidth;
            set
            {
                if (!double.IsFinite(value) || value < 0 || value > 5) throw new ArgumentOutOfRangeException(nameof(value));
                if (_bandWidth == value) return;
                this.RaiseAndSetIfChanged(ref _bandWidth, value);
                Rebuild();
            }
        }

        public bool ShowGaps
        {
            get => _showGaps;
            set
            {
                if (_showGaps == value) return;
                this.RaiseAndSetIfChanged(ref _showGaps, value);
                Rebuild();
            }
        }

        public bool LargeDataset
        {
            get => _largeDataset;
            set
            {
                if (_largeDataset == value) return;
                this.RaiseAndSetIfChanged(ref _largeDataset, value);
                Chart.Request.WindowStart = null;
                Chart.Request.WindowCount = null;
                Rebuild();
            }
        }

        public bool ReduceData
        {
            get => _reduceData;
            set
            {
                if (_reduceData == value) return;
                this.RaiseAndSetIfChanged(ref _reduceData, value);
                ApplyDisplayPolicy();
            }
        }

        public string RenderingSummary
        {
            get
            {
                int total = (Chart.DataSource as IChartWindowInfoProvider)?.GetTotalCategoryCount() ?? 0;
                return $"{total:N0} source observations · {Chart.Snapshot.Categories.Count:N0} displayed intervals · " +
                    (LargeDataset && ReduceData ? "paired extrema (approximate display; gaps retained)" : "full-resolution window");
            }
        }

        private void ApplyDisplayPolicy()
        {
            Chart.Request.MaxPoints = LargeDataset && ReduceData ? 800 : null;
            Chart.Request.DownsampleMode = LargeDataset && ReduceData ? ChartDownsampleMode.MinMax : ChartDownsampleMode.None;
            this.RaisePropertyChanged(nameof(RenderingSummary));
        }

        private void Rebuild()
        {
            int count = LargeDataset ? 100000 : 96;
            double?[] values = new double?[count];
            for (int i = 0; i < values.Length; i++)
            {
                bool missing = LargeDataset ? (i >= 32000 && i < 33000) || (i >= 68000 && i < 68500) : i == 32 || i == 68;
                double t = LargeDataset ? i * 0.001 : i;
                values[i] = ShowGaps && missing ? null : 100 + Math.Sin(t * 0.14) * 12 + Math.Cos(t * 0.6) * 2 + t * 0.08;
            }
            ChartBandSeries bands = ChartIndicators.BollingerBands(new ChartSeriesSnapshot("Signal", ChartSeriesKind.Line, values),
                LargeDataset ? 64 : 8, BandWidth);
            Chart.CategoryAxis.Kind = LargeDataset ? ChartAxisKind.Value : ChartAxisKind.Category;
            ApplyDisplayPolicy();
            if (LargeDataset)
            {
                double[] x = new double[count];
                for (int i = 0; i < count; i++) x[i] = i + 1;
                Chart.DataSource = new RangeChartDataSource("Dense Bollinger envelope", bands.Lower.Values, bands.Upper.Values, x);
            }
            else Chart.DataSource = new BandSource(bands);
        }

        // Small original example keeps its category-positioned independent mean line. Dense mode
        // uses the reusable numeric-X range source; the reduction toggle reuses its owned input.
        private sealed class BandSource : IChartDataSource, IChartWindowInfoProvider
        {
            private readonly ChartBandSeries _bands;
            private int _start = -1;
            private int _count = -1;
            private ChartDataSnapshot? _cached;
            public BandSource(ChartBandSeries bands) { _bands = bands; }
            public event EventHandler? DataInvalidated { add { } remove { } }
            public int? GetTotalCategoryCount() => _bands.Middle.Values.Count;
            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
            {
                ArgumentNullException.ThrowIfNull(request);
                int start = Math.Clamp(request.WindowStart ?? 0, 0, _bands.Middle.Values.Count);
                int count = Math.Clamp(request.WindowCount ?? (_bands.Middle.Values.Count - start), 0, _bands.Middle.Values.Count - start);
                if (_cached != null && start == _start && count == _count) return _cached;
                double?[] lower = new double?[count], upper = new double?[count], middle = new double?[count];
                string?[] categories = new string?[count];
                for (int i = 0; i < count; i++)
                {
                    lower[i] = _bands.Lower.Values[start + i]; upper[i] = _bands.Upper.Values[start + i];
                    middle[i] = _bands.Middle.Values[start + i]; categories[i] = (start + i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                _start = start; _count = count;
                return _cached = new ChartDataSnapshot(Array.AsReadOnly(categories), Array.AsReadOnly(new[]
                {
                    ChartRangeSeries.CreateArea("Bollinger envelope", lower, upper),
                    new ChartSeriesSnapshot("Rolling mean", ChartSeriesKind.Line, Array.AsReadOnly(middle))
                }));
            }
        }
    }
}
