// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Threading;
using System.Windows.Input;
using ProCharts;
using ReactiveUI;

namespace DataGridSample.ViewModels
{
    /// <summary>Consumer-thread-owned synthetic feed state. UI scheduling and visual lifetime are supplied by a behavior.</summary>
    public sealed class ChartMultiSeriesStreamingViewModel : ReactiveObject
    {
        public const int HistoryCapacity = 8192;
        public const int SeedRows = 2048;
        public const int BatchRows = 128;
        public const int VisibleRows = 512;
        private readonly double[] _x = new double[BatchRows];
        private readonly double?[] _cells = new double?[BatchRows * 3];
        private CoalescingChartDataSource? _delivered;
        private double _nextX;
        private bool _isRunning;
        private string _status = "Open this tab to initialize the feed. It starts paused; leaving the tab releases the active session.";

        public ChartMultiSeriesStreamingViewModel()
        {
            RunCommand = ReactiveCommand.Create(() => SetRunning(!IsRunning));
            AppendCommand = ReactiveCommand.Create(AppendBatch);
            ResetCommand = ReactiveCommand.Create(ResetFeed);
            LatestCommand = ReactiveCommand.Create(() => Model?.ShowLatest(VisibleRows));
            SmallBudgetCommand = ReactiveCommand.Create(() => SetPointBudget(128));
            LargeBudgetCommand = ReactiveCommand.Create(() => SetPointBudget(512));
            FullWindowCommand = ReactiveCommand.Create(() => SetPointBudget(null));
        }

        public StreamingMultiSeriesChartDataSource? Source { get; private set; }
        public ChartModel? Model { get; private set; }
        public bool IsActive => Source != null;
        public bool IsRunning => _isRunning;
        public string RunLabel => IsRunning ? "Pause feed" : "Run synthetic feed";
        public string StatusText => _status;
        public ICommand RunCommand { get; }
        public ICommand AppendCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand LatestCommand { get; }
        public ICommand SmallBudgetCommand { get; }
        public ICommand LargeBudgetCommand { get; }
        public ICommand FullWindowCommand { get; }

        /// <summary>Begins a fresh paused session on the owning consumer thread, with an explicit delivery context.</summary>
        public void Activate(SynchronizationContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (IsActive) return;
            Source = new StreamingMultiSeriesChartDataSource(HistoryCapacity, new[]
            {
                new StreamingChartSeries("Reference", ChartSeriesKind.Scatter),
                new StreamingChartSeries("Tracking", ChartSeriesKind.Scatter),
                new StreamingChartSeries("Load", ChartSeriesKind.Scatter)
            });
            try
            {
                _nextX = 0; Seed();
                _delivered = new CoalescingChartDataSource(Source, context);
                Model = new ChartModel();
                using (Model.DeferRefresh())
                {
                    Model.CategoryAxis.Kind = ChartAxisKind.Value;
                    Model.CategoryAxis.Title = "Shared source X";
                    Model.Request.MaxPoints = 128; Model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
                    Model.DataSource = _delivered;
                }
                Model.ShowLatest(VisibleRows);
                Model.SnapshotChanged += OnSnapshotChanged;
                RaiseSessionChanged(); UpdateStatus();
            }
            catch { Deactivate(); throw; }
        }

        /// <summary>Ends the active session; pending deliveries cannot update the disposed model. Reactivation is supported.</summary>
        public void Deactivate()
        {
            SetRunning(false);
            if (Model != null) { Model.SnapshotChanged -= OnSnapshotChanged; Model.Dispose(); Model = null; }
            _delivered?.Dispose(); _delivered = null; Source = null;
            RaiseSessionChanged();
            SetStatus("Feed stopped and detached. Reopening starts a fresh paused session.");
        }

        public void AppendBatch() { RequireSource(); PublishBatch(); }

        public void ResetFeed()
        {
            SetRunning(false);
            StreamingMultiSeriesChartDataSource source = RequireSource();
            source.Clear(); _nextX = 0; Seed();
            Model!.ShowLatest(VisibleRows);
        }

        public void SetPointBudget(int? budget)
        {
            RequireSource();
            if (budget is <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
            using (Model!.DeferRefresh())
            {
                Model.Request.DownsampleMode = budget.HasValue ? ChartDownsampleMode.MinMax : ChartDownsampleMode.None;
                Model.Request.MaxPoints = budget;
            }
            UpdateStatus();
        }

        public void SetRunning(bool running)
        {
            if (running) RequireSource();
            if (_isRunning == running) return;
            this.RaiseAndSetIfChanged(ref _isRunning, running, nameof(IsRunning));
            this.RaisePropertyChanged(nameof(RunLabel)); UpdateStatus();
        }

        private StreamingMultiSeriesChartDataSource RequireSource()
            => Source ?? throw new InvalidOperationException("Open the sample before changing its feed.");

        private void Seed() { for (int i = 0; i < SeedRows / BatchRows; i++) PublishBatch(); }

        private void PublishBatch()
        {
            StreamingMultiSeriesChartDataSource source = RequireSource();
            long first = source.TotalSamples;
            for (int i = 0; i < BatchRows; i++)
            {
                long row = first + i;
                _nextX += 0.8 + row % 7 * 0.04;
                _x[i] = _nextX;
                _cells[i * 3] = 80 + Math.Sin(row * 0.03) * 10;
                _cells[i * 3 + 1] = row % 223 < 12 ? null : 50 + Math.Cos(row * 0.037) * 8;
                _cells[i * 3 + 2] = row % 311 < 9 ? null : 20 + Math.Sin(row * 0.021) * 5;
            }
            source.AppendRange(_x, _cells);
        }

        private void OnSnapshotChanged(object? sender, EventArgs e) => UpdateStatus();
        private void SetStatus(string value) => this.RaiseAndSetIfChanged(ref _status, value, nameof(StatusText));
        private void RaiseSessionChanged()
        {
            this.RaisePropertyChanged(nameof(Source)); this.RaisePropertyChanged(nameof(Model));
            this.RaisePropertyChanged(nameof(IsActive));
        }
        private void UpdateStatus()
        {
            if (Source == null || Model == null) return;
            StreamingMultiSeriesChartView captured = Source.BuildView(Model.Request);
            string budget = Model.Request.DownsampleMode == ChartDownsampleMode.None ? "full window" : $"{Model.Request.MaxPoints} per channel";
            SetStatus($"{(IsRunning ? "Running" : "Paused")} | Accepted: {captured.TotalSamples:N0} rows | Retained: {captured.RetainedCount:N0}/{HistoryCapacity:N0} | Window: {captured.WindowCount:N0} | Shared output: {captured.SourceSampleIndices.Count:N0} rows | Budget: {budget}. Synthetic consumer-thread feed; these counters are not FPS or latency measurements.");
        }
    }
}
