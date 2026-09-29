using System;
using ProCharts;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>Range-area example using generated observations, editable band width and real data gaps.</summary>
    public sealed class ChartRangeViewModel : ReactiveObject
    {
        private double _bandWidth = 2;
        private bool _showGaps = true;

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

        private void Rebuild()
        {
            double?[] values = new double?[96];
            for (int i = 0; i < values.Length; i++)
                values[i] = ShowGaps && (i == 32 || i == 68) ? null : 100 + Math.Sin(i * 0.14) * 12 + Math.Cos(i * 0.6) * 2 + i * 0.08;
            ChartBandSeries bands = ChartIndicators.BollingerBands(new ChartSeriesSnapshot("Signal", ChartSeriesKind.Line, values), 8, BandWidth);
            Chart.DataSource = new BandSource(bands);
        }

        // All state is owned by the view model; no timer, global event or external source needs detaching.
        // Source replacement is handled by ChartModel. Windowing occurs after calculating full-history bands.
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
