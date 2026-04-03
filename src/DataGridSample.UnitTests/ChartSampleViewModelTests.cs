using DataGridSample.ViewModels;
using ProCharts;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartSampleViewModelTests
{
    [Theory]
    [InlineData(ChartSampleKind.Candlestick, ChartSeriesKind.Candlestick, true)]
    [InlineData(ChartSampleKind.HollowCandlestick, ChartSeriesKind.HollowCandlestick, true)]
    [InlineData(ChartSampleKind.Ohlc, ChartSeriesKind.Ohlc, true)]
    [InlineData(ChartSampleKind.Hlc, ChartSeriesKind.Hlc, false)]
    [InlineData(ChartSampleKind.HeikinAshi, ChartSeriesKind.HeikinAshi, true)]
    [InlineData(ChartSampleKind.Renko, ChartSeriesKind.Renko, true)]
    [InlineData(ChartSampleKind.Range, ChartSeriesKind.Range, true)]
    [InlineData(ChartSampleKind.LineBreak, ChartSeriesKind.LineBreak, true)]
    [InlineData(ChartSampleKind.Kagi, ChartSeriesKind.Kagi, true)]
    [InlineData(ChartSampleKind.PointFigure, ChartSeriesKind.PointFigure, true)]
    public void FinancialSamples_UseExpectedSnapshotKind(
        ChartSampleKind sampleKind,
        ChartSeriesKind seriesKind,
        bool expectsOpenValues)
    {
        var viewModel = new ChartSampleViewModel(sampleKind);

        Assert.True(viewModel.IsFinancialSample);
        Assert.False(viewModel.IsSalesSample);
        Assert.NotEmpty(viewModel.FinancialItems);
        Assert.True(viewModel.SupportsFinancialSettings);
        Assert.True(viewModel.ChartStyle.FinancialShowLastPriceLine);

        var snapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);
        var series = Assert.Single(snapshot.Series);

        Assert.Equal(seriesKind, series.Kind);
        Assert.NotNull(series.HighValues);
        Assert.NotNull(series.LowValues);
        Assert.Equal(expectsOpenValues, series.OpenValues is not null);
        if (seriesKind == ChartSeriesKind.PointFigure)
        {
            Assert.NotNull(series.SizeValues);
        }
        Assert.InRange(snapshot.Categories.Count, 1, 40);
    }

    [Fact]
    public void RenkoSample_ChangingBrickSize_RefreshesDerivedSnapshot()
    {
        var viewModel = new ChartSampleViewModel(ChartSampleKind.Renko);
        var initialSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        viewModel.FinancialBrickSize = 4.0d;

        var updatedSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        Assert.Equal(ChartSeriesKind.Renko, Assert.Single(updatedSnapshot.Series).Kind);
        Assert.NotEqual(initialSnapshot.Categories.Count, updatedSnapshot.Categories.Count);
    }

    [Fact]
    public void RangeSample_ChangingBrickSize_RefreshesDerivedSnapshot()
    {
        var viewModel = new ChartSampleViewModel(ChartSampleKind.Range);
        var initialSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        viewModel.FinancialBrickSize = 2.8d;

        var updatedSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        Assert.Equal(ChartSeriesKind.Range, Assert.Single(updatedSnapshot.Series).Kind);
        Assert.NotEqual(initialSnapshot.Categories.Count, updatedSnapshot.Categories.Count);
    }

    [Fact]
    public void HollowCandleSample_UsesPreviousCloseDrivenKind()
    {
        var viewModel = new ChartSampleViewModel(ChartSampleKind.HollowCandlestick);

        Assert.False(viewModel.SupportsFinancialHollowToggle);

        var snapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);
        Assert.Equal(ChartSeriesKind.HollowCandlestick, Assert.Single(snapshot.Series).Kind);
    }

    [Fact]
    public void KagiSample_ChangingReversalAmount_RefreshesDerivedSnapshot()
    {
        var viewModel = new ChartSampleViewModel(ChartSampleKind.Kagi);
        var initialSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        viewModel.FinancialKagiReversalAmount = 3.5d;

        var updatedSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        Assert.Equal(ChartSeriesKind.Kagi, Assert.Single(updatedSnapshot.Series).Kind);
        Assert.NotEqual(initialSnapshot.Categories.Count, updatedSnapshot.Categories.Count);
    }

    [Fact]
    public void PointFigureSample_ChangingReversalBoxes_RefreshesDerivedSnapshot()
    {
        var viewModel = new ChartSampleViewModel(ChartSampleKind.PointFigure);
        var initialSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        viewModel.FinancialPointFigureReversalBoxes = 2;

        var updatedSnapshot = viewModel.Chart.DataSource!.BuildSnapshot(viewModel.Chart.Request);

        var series = Assert.Single(updatedSnapshot.Series);
        Assert.Equal(ChartSeriesKind.PointFigure, series.Kind);
        Assert.NotNull(series.SizeValues);
        Assert.NotEqual(initialSnapshot.Categories.Count, updatedSnapshot.Categories.Count);
    }
}
