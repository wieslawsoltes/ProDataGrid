using System.Linq;
using System.Reactive.Concurrency;
using Avalonia.Headless.XUnit;
using ProDataGrid.ExcelSample.Models;
using ProDataGrid.ExcelSample.ViewModels;
using Xunit;

namespace ProDataGrid.ExcelSample.Tests;

public sealed class ChartPanelViewModelTests
{
    [AvaloniaFact]
    public void ChartPanelViewModel_MapsSelectionToSeries()
    {
        var selection = new SpreadsheetSelectionState();
        using var viewModel = new ChartPanelViewModel(selection, ImmediateScheduler.Instance);
        var sheet = new SheetViewModel("Sheet1", rowCount: 5, columnCount: 4);
        viewModel.SetSheet(sheet);
        viewModel.AutoApplySelection = true;
        viewModel.IsEnabled = true;

        selection.SelectedRange = new SpreadsheetCellRange(
            new SpreadsheetCellReference(0, 0),
            new SpreadsheetCellReference(2, 2));

        Assert.Equal("A1:C3", viewModel.RangeText);
        Assert.True(viewModel.HasChart);
        Assert.Equal(2, viewModel.ChartData.Series.Count);
        Assert.Equal("B", viewModel.ChartData.Series[0].Name);
    }

    [AvaloniaFact]
    public void ChartPanelViewModel_UsesSingleSeriesForPie()
    {
        var selection = new SpreadsheetSelectionState();
        using var viewModel = new ChartPanelViewModel(selection, ImmediateScheduler.Instance);
        var sheet = new SheetViewModel("Sheet1", rowCount: 4, columnCount: 4);
        viewModel.SetSheet(sheet);
        viewModel.AutoApplySelection = true;
        viewModel.IsEnabled = true;

        viewModel.SelectedChartType = viewModel.ChartTypes.First(option => option.IsSingleSeries);

        selection.SelectedRange = new SpreadsheetCellRange(
            new SpreadsheetCellReference(0, 0),
            new SpreadsheetCellReference(3, 3));

        Assert.True(viewModel.HasChart);
        Assert.Single(viewModel.ChartData.Series);
    }
}
