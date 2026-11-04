using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Avalonia.Layout;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridResizeLayoutTests
{
    [AvaloniaTheory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 80)]
    [InlineData(true, 80)]
    public async Task Star_Columns_And_Rows_Converge_When_An_Embedded_Grid_Is_Resized(bool autoHide, int itemCount)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(index => $"Property {index}: a value that wraps in a narrow column").ToArray();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ItemsSource = items
        };
        ScrollViewer.SetAllowAutoHide(grid, autoHide);
        foreach (var weight in new[] { 1, 2 })
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = weight == 1 ? "Name" : "Value",
                Width = new DataGridLength(weight, DataGridLengthUnitType.Star),
                MinWidth = 90,
                CellTemplate = new FuncDataTemplate<string>((value, _) =>
                    new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap })
            });
        }

        var panel = new Grid { RowDefinitions = new RowDefinitions("48,*") };
        Grid.SetRow(grid, 1);
        panel.Children.Add(grid);
        var window = new Window { Content = panel, Width = 600, Height = 500 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        var previousSink = Logger.Sink;
        var log = new LayoutLogSink
        {
            Describe = () => $"window={window.Bounds}; grid={grid.Bounds}; measured={grid.RowsPresenterAvailableSize}; widths={string.Join(",", grid.Columns.Select(column => column.ActualWidth))}"
        };
        Logger.Sink = log;
        try
        {
            window.Show();
            Assert.True(grid.TryFindResource(typeof(DataGrid), out var resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();
            var rows = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().Single();
            var headers = grid.GetVisualDescendants().OfType<DataGridColumnHeadersPresenter>().Single();
            Assert.True(grid.UseLogicalScrollable);
            foreach (var size in new[] { new Size(600, 500), new Size(317, 271), new Size(823, 643), new Size(190, 359), new Size(600, 500) })
            {
                log.Phase = size.ToString();
                window.Width = size.Width;
                window.Height = size.Height;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                window.UpdateLayout();
                Assert.Equal(size, window.ClientSize);
                Assert.Equal(size.Height - 48, grid.Bounds.Height);
                Assert.True(rows.IsMeasureValid && rows.IsArrangeValid);
                Assert.True(headers.IsMeasureValid && headers.IsArrangeValid);
                Assert.InRange(rows.Viewport.Height, 1, grid.Bounds.Height);
                Assert.Equal(Math.Max(180, grid.CellsWidth), grid.Columns.Sum(column => column.ActualWidth), 6);
                if (itemCount > 0)
                    Assert.NotEmpty(rows.Children.OfType<DataGridRow>());
            }

            if (itemCount > 0)
            {
                log.Phase = "ScrollIntoView";
                grid.ScrollIntoView(items[^1], grid.Columns[1]);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                window.UpdateLayout();
                Assert.Contains(rows.Children.OfType<DataGridRow>(), row => Equals(row.DataContext, items[^1]));
            }
            Assert.True(log.Cycles.Count == 0, string.Join(Environment.NewLine, log.Cycles));
        }
        finally
        {
            window.Close();
            Logger.Sink = previousSink;
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridTheme.Simple)]
    [InlineData(DataGridTheme.SimpleV2)]
    [InlineData(DataGridTheme.Fluent)]
    [InlineData(DataGridTheme.FluentV2)]
    public void Width_Resize_With_Fixed_Columns_Measures_Each_Visible_Row_Once(DataGridTheme theme)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = Enumerable.Range(0, 1000).Select(index => $"Row {index}").ToArray(),
            Height = 160,
            VerticalAlignment = VerticalAlignment.Top,
            RowHeight = 28,
            ColumnHeaderHeight = 26,
        };
        for (int column = 0; column < 47; column++)
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Width = new DataGridLength(90),
                CellTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = value }),
            });
        }
        var window = new Window { Width = 700, Height = 1000, Content = grid };
        window.SetThemeStyles(theme);
        ScrollViewer.SetAllowAutoHide(grid, false);
        void PumpLayout()
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
        try
        {
            window.Show();
            Assert.True(grid.TryFindResource(typeof(DataGrid), out var resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
            PumpLayout();
            // Warm both widths so this checks recurring resize work, not initial realization.
            window.Width = 900;
            PumpLayout();
            window.Width = 700;
            PumpLayout();
            int rowCount = grid.DisplayData.NumDisplayedScrollingElements;
            Assert.InRange(rowCount, 1, 6);
            long measuredRows = 0;
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == DataGridDiagnostics.MeterName &&
                    instrument.Name == DataGridDiagnostics.Meters.RowsMeasuredCountName)
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, count, _, _) => measuredRows += count);
            listener.Start();

            window.Width = 900;
            PumpLayout();

            Assert.True(grid.IsMeasureValid && grid.IsArrangeValid);
            Assert.Equal(rowCount, grid.DisplayData.NumDisplayedScrollingElements);
            Assert.Equal(rowCount, measuredRows);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(DataGridTheme.Simple)]
    [InlineData(DataGridTheme.Fluent)]
    public void Legacy_Presenter_Uses_Parent_Measure_Constraint_After_A_Smaller_Arrange(DataGridTheme theme)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = Enumerable.Range(0, 100).ToArray(),
            RowHeight = 28,
        };
        grid.Columns.Add(new DataGridTextColumn { Width = new DataGridLength(100) });
        var window = new Window { Width = 600, Height = 600, Content = grid };
        window.SetThemeStyles(theme);
        try
        {
            window.Show();
            Assert.True(grid.TryFindResource(typeof(DataGrid), out var resource));
            grid.Theme = Assert.IsType<ControlTheme>(resource);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.False(grid.UseLogicalScrollable);
            var presenter = grid.GetVisualDescendants().OfType<DataGridRowsPresenter>().Single();
            var constraint = new Size(300, 300);
            presenter.Measure(constraint);
            presenter.Arrange(new Rect(0, 0, 300, 200));
            presenter.InvalidateMeasure();
            presenter.Measure(constraint);

            Assert.Equal(constraint, grid.RowsPresenterAvailableSize);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class LayoutLogSink : ILogSink
    {
        public List<string> Cycles { get; } = new();
        public string Phase { get; set; } = "Show";
        public Func<string> Describe { get; init; } = () => string.Empty;
        public bool IsEnabled(LogEventLevel level, string area) => area == LogArea.Layout;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            => Log(level, area, source, messageTemplate, Array.Empty<object?>());

        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
        {
            if (area == LogArea.Layout && messageTemplate.Contains("Layout cycle", StringComparison.Ordinal) && Cycles.Count < 10)
            {
                Cycles.Add($"{Phase}: {string.Join(", ", propertyValues)}; {Describe()}");
            }
        }
    }
}
