using System.Globalization;
using Xunit;

namespace ProCharts.Controls.Tests;

public sealed class SourceTests
{
    [Fact]
    public void Filter_shares_channels_and_preserves_original_indices()
    {
        TestSource source = new();
        using SeriesVisibilityChartDataSource filter = new(source);
        ChartDataRequest request = new();
        Assert.Same(source.Snapshot, filter.BuildSnapshot(request));
        filter.SetSeriesVisible(0, false);
        ChartDataSnapshot visible = filter.BuildSnapshot(request);
        Assert.Single(visible.Series);
        Assert.Same(source.Snapshot.Series[1], visible.Series[0]);
        Assert.Same(source.Snapshot.Categories, visible.Categories);
        Assert.Equal(new[] { 1 }, filter.OriginalSeriesIndices);
        Assert.Same(visible, filter.BuildSnapshot(request));
        Assert.Equal(20, filter.GetTotalCategoryCount());
        Assert.Equal(2, filter.UnfilteredSnapshot.Series.Count);
    }
    [Fact]
    public void Filter_can_hide_everything_and_restore_by_original_index()
    {
        TestSource source = new();
        using SeriesVisibilityChartDataSource filter = new(source);
        filter.SetSeriesVisible(0, false); filter.SetSeriesVisible(1, false);
        Assert.Empty(filter.BuildSnapshot(new()).Series);
        filter.SetSeriesVisible(1, true);
        Assert.Same(source.Snapshot.Series[1], filter.BuildSnapshot(new()).Series[0]);
        filter.ResetVisibility();
        Assert.Same(source.Snapshot, filter.BuildSnapshot(new()));
    }
    [Fact]
    public void Filter_invalidates_once_and_never_disposes_upstream()
    {
        TestSource source = new();
        SeriesVisibilityChartDataSource filter = new(source);
        int events = 0;
        filter.DataInvalidated += (_, _) => events++;
        filter.SetSeriesVisible(0, false); filter.SetSeriesVisible(0, false);
        Assert.Equal(1, events);
        source.Invalidate(); Assert.Equal(2, events);
        filter.Dispose(); filter.Dispose(); source.Invalidate();
        Assert.Equal(2, events); Assert.Equal(0, source.Subscriptions); Assert.False(source.Disposed);
        Assert.Throws<ObjectDisposedException>(() => filter.BuildSnapshot(new()));
    }
    [Fact]
    public void Filter_keeps_duplicate_names_independently_addressable()
    {
        TestSource source = new();
        using SeriesVisibilityChartDataSource filter = new(source);
        filter.SetSeriesVisible(1, false);
        Assert.Same(source.Snapshot.Series[0], filter.BuildSnapshot(new()).Series[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => filter.SetSeriesVisible(-1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => filter.IsSeriesVisible(-1));
    }
    [Theory]
    [InlineData("=SUM(A1:A2)")][InlineData("  +1")][InlineData("-cmd")][InlineData("@name")]
    [InlineData("\titem")][InlineData("\rline")][InlineData("\nline")]
    public void Csv_neutralizes_text_formulas(string value)
    {
        ChartDataSnapshot snapshot = new(new[] { value }, new[] { new ChartSeriesSnapshot(value, ChartSeriesKind.Line, new double?[] { -1.25 }) });
        string csv = ChartCsvExporter.Export(snapshot);
        Assert.Contains("\"'" + value + "\"", csv);
        Assert.Contains(",-1.25,", csv);
        Assert.DoesNotContain("\"'" + value + "\"", ChartCsvExporter.Export(snapshot, false));
    }
    [Fact]
    public void Csv_round_trips_quotes_line_breaks_and_all_channels_invariantly()
    {
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            ChartSeriesSnapshot series = new("A,\"B\"", ChartSeriesKind.Candlestick, new double?[] { 2.25 },
                xValues: new double[] { 10.5 }, sizeValues: new double?[] { 7.75 },
                openValues: new double?[] { 1.5 }, highValues: new double?[] { 3 }, lowValues: new double?[] { 1 });
            string csv = ChartCsvExporter.Export(new(new[] { "first\nsecond" }, new[] { series }));
            Assert.Equal("Series,Point,Category,X,Value,Open,High,Low,Size\r\n\"A,\"\"B\"\"\",0,\"first\nsecond\",10.5,2.25,1.5,3,1,7.75\r\n", csv);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
    [Fact]
    public void Csv_blanks_gaps_and_nonfinite_values_and_respects_cancellation()
    {
        ChartDataSnapshot snapshot = new(new string?[] { null, null, null }, new[] { new ChartSeriesSnapshot("Test", ChartSeriesKind.Line,
            new double?[] { null, double.NaN, double.PositiveInfinity }) });
        string csv = ChartCsvExporter.Export(snapshot);
        Assert.DoesNotContain("NaN", csv); Assert.DoesNotContain("Infinity", csv);
        using StringWriter writer = new();
        Assert.Throws<OperationCanceledException>(() => ChartCsvExporter.Write(snapshot, writer, cancellationToken: new CancellationToken(true)));
        writer.Write("still open"); Assert.EndsWith("still open", writer.ToString());
    }
    [Fact]
    public void Csv_validates_null_inputs()
    {
        Assert.Throws<ArgumentNullException>(() => ChartCsvExporter.Export(null!));
        Assert.Throws<ArgumentNullException>(() => ChartCsvExporter.Write(ChartDataSnapshot.Empty, null!));
    }
}

internal sealed class TestSource : IChartDataSource, IChartWindowInfoProvider, IDisposable
{
    private EventHandler? _invalidated;
    public int Subscriptions { get; private set; }
    public bool Disposed { get; private set; }
    public event EventHandler? DataInvalidated
    { add { _invalidated += value; Subscriptions++; } remove { _invalidated -= value; Subscriptions--; } }
    public ChartDataSnapshot Snapshot { get; } = new(Enumerable.Range(0, 20).Select(x => (string?)x.ToString()).ToArray(), new[]
    {
        new ChartSeriesSnapshot("Same name", ChartSeriesKind.Line, Enumerable.Range(0, 20).Select(x => (double?)x).ToArray()),
        new ChartSeriesSnapshot("Same name", ChartSeriesKind.Column, Enumerable.Range(0, 20).Select(x => (double?)(x + 1)).ToArray())
    });
    public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => Snapshot;
    public int? GetTotalCategoryCount() => 20;
    public void Invalidate() => _invalidated?.Invoke(this, EventArgs.Empty);
    public void Dispose() => Disposed = true;
}
