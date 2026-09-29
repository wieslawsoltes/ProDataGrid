using System;
using System.Windows.Input;
using ProCharts;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>A command-driven synthetic interval feed; no timer or external connection is implied.</summary>
    public sealed class ChartStreamingRangeViewModel : ReactiveObject
    {
        private readonly ChartRangeSample[] _batch = new ChartRangeSample[64];
        private bool _reduceData = true;
        private bool _includeGaps = true;

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
            Chart.SnapshotChanged += (_, _) => this.RaisePropertyChanged(nameof(RenderingSummary));
            AppendCommand = ReactiveCommand.Create(AppendBatch);
            ResetCommand = ReactiveCommand.Create(Reset);
        }

        public ChartModel Chart { get; }
        public StreamingRangeChartDataSource Source { get; }
        public ICommand AppendCommand { get; }
        public ICommand ResetCommand { get; }

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
                Source.Clear();
                Chart.Request.WindowStart = null; Chart.Request.WindowCount = null;
                ChartRangeSample[] seed = new ChartRangeSample[1024];
                for (int i = 0; i < seed.Length; i++) seed[i] = Observation(i, IncludeGaps && i is >= 490 and < 502);
                Source.AppendRange(seed);
            }
        }

        private static ChartRangeSample Observation(long index, bool missing)
        {
            double center = 100 + Math.Sin(index * 0.018) * 12 + Math.Cos(index * 0.063) * 2;
            double width = 2 + (Math.Sin(index * 0.032) + 1) * 2;
            return missing ? new ChartRangeSample(index, null, null) : new ChartRangeSample(index, center - width, center + width);
        }
    }
}
