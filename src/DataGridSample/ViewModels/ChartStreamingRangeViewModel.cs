using System;
using System.Windows.Input;
using ProCharts;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>A command-driven synthetic feed with optional persistent Bollinger calculation; no external connection or timer.</summary>
    public sealed class ChartStreamingRangeViewModel : ReactiveObject
    {
        private readonly ChartRangeSample[] _batch = new ChartRangeSample[64];
        private StreamingBollingerBands _indicator = new(32);
        private bool _reduceData = true;
        private bool _includeGaps = true;
        private bool _useCalculatedBands;
        private double _standardDeviations = 2;

        public ChartStreamingRangeViewModel()
        {
            Source = new StreamingRangeChartDataSource(2048, "Live interval envelope");
            Chart = new ChartModel();
            using (Chart.DeferRefresh())
            {
                Chart.CategoryAxis.Kind = ChartAxisKind.Value;
                Chart.CategoryAxis.Title = "Original observation";
                Chart.ValueAxis.Title = "Signal";
                Chart.Legend.Position = ChartLegendPosition.Bottom;
                Chart.SeriesStyles = new[] { new ChartSeriesStyle { StrokeWidth = 1 } };
                Chart.Request.MaxPoints = 256;
                Chart.Request.DownsampleMode = ChartDownsampleMode.MinMax;
                Chart.DataSource = Source;
                Reset();
            }
            Chart.SnapshotChanged += (_, _) =>
            {
                this.RaisePropertyChanged(nameof(RenderingSummary));
                this.RaisePropertyChanged(nameof(IndicatorSummary));
            };
            AppendCommand = ReactiveCommand.Create(AppendBatch);
            ResetCommand = ReactiveCommand.Create(Reset);
        }

        public ChartModel Chart { get; }
        public StreamingRangeChartDataSource Source { get; }
        public ICommand AppendCommand { get; }
        public ICommand ResetCommand { get; }
        public long CalculatorSamplesProcessed => _indicator.SamplesProcessed;
        public ChartBandValue CalculatedBand => _indicator.Current;

        public bool ReduceData
        {
            get => _reduceData;
            set
            {
                if (_reduceData == value) return;
                this.RaiseAndSetIfChanged(ref _reduceData, value);
                using (Chart.DeferRefresh())
                {
                    Chart.Request.MaxPoints = value ? 256 : null;
                    Chart.Request.DownsampleMode = value ? ChartDownsampleMode.MinMax : ChartDownsampleMode.None;
                }
            }
        }

        public bool IncludeGaps
        {
            get => _includeGaps;
            set => this.RaiseAndSetIfChanged(ref _includeGaps, value);
        }

        public bool UseCalculatedBands
        {
            get => _useCalculatedBands;
            set
            {
                if (_useCalculatedBands == value) return;
                this.RaiseAndSetIfChanged(ref _useCalculatedBands, value);
                Reset();
            }
        }

        public double StandardDeviations
        {
            get => _standardDeviations;
            set
            {
                if (!double.IsFinite(value) || value < 0 || value > 4) throw new ArgumentOutOfRangeException(nameof(value));
                if (_standardDeviations == value) return;
                this.RaiseAndSetIfChanged(ref _standardDeviations, value);
                _indicator = new StreamingBollingerBands(32, value);
                if (UseCalculatedBands) Reset();
            }
        }

        public string IndicatorSummary => UseCalculatedBands
            ? $"Bollinger window 32 · {_indicator.SamplesProcessed:N0} prices processed · " +
                (_indicator.IsReady ? $"mean {_indicator.Current.Middle:F2}" : "warming up after a gap")
            : "Supplied synthetic intervals. Enable calculated bands for a persistent 32-price Bollinger window.";

        public string RenderingSummary
        {
            get
            {
                StreamingRangeChartView view = Source.BuildView(Chart.Request);
                return $"{view.TotalSamples:N0} received · {view.RetainedCount:N0}/{Source.Capacity:N0} retained · " +
                    $"{view.Snapshot.Categories.Count:N0} displayed · first retained observation {view.FirstRetainedSampleIndex:N0}";
            }
        }

        private void AppendBatch()
        {
            long start = Source.TotalSamples;
            for (int i = 0; i < _batch.Length; i++) _batch[i] = Observation(start + i, IncludeGaps && i is >= 28 and < 32);
            Source.AppendRange(_batch);
        }

        private void Reset()
        {
            using (Chart.DeferRefresh())
            {
                _indicator.Reset();
                Source.Clear();
                Chart.Request.WindowStart = null; Chart.Request.WindowCount = null;
                ChartRangeSample[] seed = new ChartRangeSample[1024];
                for (int i = 0; i < seed.Length; i++) seed[i] = Observation(i, IncludeGaps && i is >= 490 and < 502);
                Source.AppendRange(seed);
            }
        }

        private ChartRangeSample Observation(long index, bool missing)
        {
            double center = 100 + Math.Sin(index * 0.018) * 12 + Math.Cos(index * 0.063) * 2;
            if (UseCalculatedBands)
            {
                ChartBandValue band = _indicator.Push(missing ? null : center);
                return new ChartRangeSample(index, band.Lower, band.Upper);
            }
            double width = 2 + (Math.Sin(index * 0.032) + 1) * 2;
            return missing ? new ChartRangeSample(index, null, null) : new ChartRangeSample(index, center - width, center + width);
        }
    }
}
