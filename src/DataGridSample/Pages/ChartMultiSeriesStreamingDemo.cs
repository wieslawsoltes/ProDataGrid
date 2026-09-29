// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ProCharts;
using ProCharts.Avalonia;
using ProCharts.Skia;

namespace DataGridSample.Pages
{
    /// <summary>A lifecycle-bound synthetic feed demonstrating the reusable synchronized source and dispatcher adapter.</summary>
    /// <remarks>The timer generates data on the UI thread; real worker delivery is covered separately by integration tests.</remarks>
    public sealed class ChartMultiSeriesStreamingDemo : UserControl
    {
        public const int HistoryCapacity = 8192;
        public const int SeedRows = 2048;
        public const int BatchRows = 128;
        public const int VisibleRows = 512;
        private readonly double[] _x = new double[BatchRows];
        private readonly double?[] _cells = new double?[BatchRows * 3];
        private readonly Button _run = new() { Content = "Run synthetic feed" };
        private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
        private readonly ProChartView _view = new()
        {
            MinHeight = 240,
            ChartStyle = new SkiaChartStyle { ShowLegend = true, PaddingRight = 60 }
        };
        private CoalescingChartDataSource? _delivered;
        private DispatcherTimer? _timer;
        private double _nextX;

        public ChartMultiSeriesStreamingDemo()
        {
            Grid root = new()
            {
                Margin = new Thickness(12), RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 10
            };
            TextBlock description = new()
            {
                Text = "Synchronized streams: all three channels commit together. Scatter uses the actual nonuniform X coordinates; gaps remain independent. The output count can exceed the per-channel selection budget.",
                TextWrapping = TextWrapping.Wrap
            };
            StackPanel commands = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
            _run.Click += (_, _) => SetRunning(!IsRunning);
            commands.Children.Add(_run);
            AddButton(commands, "Append 128 rows", AppendBatch);
            AddButton(commands, "Reset", ResetFeed);
            AddButton(commands, "Latest 512 rows", () => Model?.ShowLatest(VisibleRows));
            AddButton(commands, "128 / channel", () => SetPointBudget(128));
            AddButton(commands, "512 / channel", () => SetPointBudget(512));
            AddButton(commands, "Full window", () => SetPointBudget(null));
            Grid.SetRow(commands, 1); Grid.SetRow(_view, 2); Grid.SetRow(_status, 3);
            root.Children.Add(description); root.Children.Add(commands); root.Children.Add(_view); root.Children.Add(_status);
            Content = root;
            _status.Text = "Open this tab to initialize the feed. It starts paused; closing the tab stops its timer and releases its model.";
        }

        public StreamingMultiSeriesChartDataSource? Source { get; private set; }
        public ChartModel? Model { get; private set; }
        public ProChartView ChartView => _view;
        public bool IsRunning => _timer?.IsEnabled == true;
        public string? StatusText => _status.Text;

        public void AppendBatch()
        {
            Dispatcher.UIThread.VerifyAccess();
            RequireSource();
            PublishBatch();
        }

        public void ResetFeed()
        {
            Dispatcher.UIThread.VerifyAccess();
            SetRunning(false);
            StreamingMultiSeriesChartDataSource source = RequireSource();
            source.Clear(); _nextX = 0;
            Seed();
            Model!.ShowLatest(VisibleRows);
        }

        public void SetPointBudget(int? budget)
        {
            Dispatcher.UIThread.VerifyAccess();
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
            Dispatcher.UIThread.VerifyAccess();
            if (_timer == null)
            {
                if (running) throw new InvalidOperationException("Open the sample before starting its feed.");
                return;
            }
            if (running) _timer.Start(); else _timer.Stop();
            _run.Content = running ? "Pause feed" : "Run synthetic feed";
            UpdateStatus();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (Source != null) return;
            Source = new StreamingMultiSeriesChartDataSource(HistoryCapacity, new[]
            {
                new StreamingChartSeries("Reference", ChartSeriesKind.Scatter),
                new StreamingChartSeries("Tracking", ChartSeriesKind.Scatter),
                new StreamingChartSeries("Load", ChartSeriesKind.Scatter)
            });
            _nextX = 0; Seed();
            _delivered = ChartDataSourceDispatch.Create(Source);
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
            _view.ChartModel = Model;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += OnTick;
            _run.Content = "Run synthetic feed";
            UpdateStatus();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
            _view.ChartModel = null;
            if (Model != null) { Model.SnapshotChanged -= OnSnapshotChanged; Model.Dispose(); Model = null; }
            _delivered?.Dispose(); _delivered = null; Source = null;
            _run.Content = "Run synthetic feed";
            _status.Text = "Feed stopped and detached. Reopening starts a fresh paused session.";
            base.OnDetachedFromVisualTree(e);
        }

        private StreamingMultiSeriesChartDataSource RequireSource()
            => Source ?? throw new InvalidOperationException("Open the sample before changing its feed.");

        private void Seed()
        {
            for (int i = 0; i < SeedRows / BatchRows; i++) PublishBatch();
        }

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

        private void OnTick(object? sender, EventArgs e) => AppendBatch();
        private void OnSnapshotChanged(object? sender, EventArgs e) => UpdateStatus();

        private void UpdateStatus()
        {
            if (Source == null || Model == null) return;
            // This synthetic producer and model run on the UI thread. No worker can race this capture.
            StreamingMultiSeriesChartView captured = Source.BuildView(Model.Request);
            string budget = Model.Request.DownsampleMode == ChartDownsampleMode.None ? "full window" : $"{Model.Request.MaxPoints} per channel";
            _status.Text = $"{(IsRunning ? "Running" : "Paused")} | Accepted: {captured.TotalSamples:N0} rows | Retained: {captured.RetainedCount:N0}/{HistoryCapacity:N0} | Window: {captured.WindowCount:N0} | Shared output: {captured.SourceSampleIndices.Count:N0} rows | Budget: {budget}. Synthetic UI-thread timer; these counters are not FPS or latency measurements.";
        }

        private static void AddButton(Panel panel, string text, Action action)
        {
            Button button = new() { Content = text };
            button.Click += (_, _) => action();
            panel.Children.Add(button);
        }
    }
}
