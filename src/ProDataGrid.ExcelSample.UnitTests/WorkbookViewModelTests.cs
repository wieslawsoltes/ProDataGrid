using System;
using System.Reactive;
using System.Reactive.Concurrency;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Headless.XUnit;
using ProDataGrid.ExcelSample.Models;
using ProDataGrid.ExcelSample.ViewModels;
using Xunit;

namespace ProDataGrid.ExcelSample.Tests;

public sealed class WorkbookViewModelTests
{
    [AvaloniaFact]
    public void SearchText_UpdatesSearchDescriptors()
    {
        using var viewModel = new WorkbookViewModel(ImmediateScheduler.Instance, ImmediateScheduler.Instance, startLiveUpdates: false);

        viewModel.SearchText = "alpha";

        Assert.Single(viewModel.SearchModel.Descriptors);
        Assert.Equal("alpha", viewModel.SearchModel.Descriptors[0].Query);

        viewModel.SearchText = "";

        Assert.Empty(viewModel.SearchModel.Descriptors);
    }

    [AvaloniaFact]
    public void FilterText_UpdatesFilteringDescriptors()
    {
        using var viewModel = new WorkbookViewModel(ImmediateScheduler.Instance, ImmediateScheduler.Instance, startLiveUpdates: false);

        viewModel.FilterText = "Item";

        Assert.Single(viewModel.FilteringModel.Descriptors);
        var descriptor = viewModel.FilteringModel.Descriptors[0];
        Assert.Equal(FilteringOperator.Contains, descriptor.Operator);
        Assert.Equal("Item", descriptor.Value);

        viewModel.FilterText = "";

        Assert.Empty(viewModel.FilteringModel.Descriptors);
    }

    [AvaloniaFact]
    public void ReorderSheetCommand_MovesSheets()
    {
        using var viewModel = new WorkbookViewModel(ImmediateScheduler.Instance, ImmediateScheduler.Instance, startLiveUpdates: false);

        var first = viewModel.Sheets[0];
        var second = viewModel.Sheets[1];
        var third = viewModel.Sheets[2];

        viewModel.ReorderSheetCommand.Execute(new SheetTabReorderRequest(0, 3)).Subscribe(new NoopObserver());

        Assert.Equal(second, viewModel.Sheets[0]);
        Assert.Equal(third, viewModel.Sheets[1]);
        Assert.Equal(first, viewModel.Sheets[2]);
        Assert.Equal(first, viewModel.SelectedSheet);
    }

    [AvaloniaFact]
    public void CancelFormulaCommand_RestoresFormulaText()
    {
        using var viewModel = new WorkbookViewModel(ImmediateScheduler.Instance, ImmediateScheduler.Instance, startLiveUpdates: false);

        viewModel.SelectionState.CurrentCell = new SpreadsheetCellReference(0, 0);
        var original = viewModel.FormulaText;

        viewModel.FormulaText = "123";

        viewModel.CancelFormulaCommand.Execute().Subscribe(new NoopObserver());

        Assert.Equal(original, viewModel.FormulaText);
    }

    [AvaloniaFact]
    public void FormulaBarVisibility_Toggles()
    {
        using var viewModel = new WorkbookViewModel(ImmediateScheduler.Instance, ImmediateScheduler.Instance, startLiveUpdates: false);

        Assert.True(viewModel.IsFormulaBarVisible);

        viewModel.IsFormulaBarVisible = false;

        Assert.False(viewModel.IsFormulaBarVisible);
    }

    private sealed class NoopObserver : IObserver<Unit>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Unit value)
        {
        }
    }
}
