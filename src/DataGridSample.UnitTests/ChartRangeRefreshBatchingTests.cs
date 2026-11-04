using DataGridSample.ViewModels;
using ProCharts;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartRangeRefreshBatchingTests
{
    [Fact]
    public void Density_And_Resolution_Changes_Publish_One_Coherent_Snapshot_Each()
    {
        ChartRangeViewModel model = new();
        using var chart = model.Chart;
        chart.Request.WindowStart = 20; chart.Request.WindowCount = 50;
        int changes = 0;
        chart.SnapshotChanged += (_, _) => changes++;
        model.LargeDataset = true;
        Assert.Equal(1, changes);
        Assert.InRange(chart.Snapshot.Categories.Count, 6, 800);
        Assert.Equal(ChartAxisKind.Value, chart.CategoryAxis.Kind);
        var source = chart.DataSource;
        changes = 0;
        model.ReduceData = false;
        Assert.Equal(1, changes); Assert.Equal(100000, chart.Snapshot.Categories.Count); Assert.Same(source, chart.DataSource);
        changes = 0;
        model.ReduceData = true;
        Assert.Equal(1, changes); Assert.InRange(chart.Snapshot.Categories.Count, 6, 800);
        changes = 0;
        model.LargeDataset = false;
        Assert.Equal(1, changes); Assert.Equal(96, chart.Snapshot.Categories.Count);
        Assert.Equal(ChartAxisKind.Category, chart.CategoryAxis.Kind);
    }
}
