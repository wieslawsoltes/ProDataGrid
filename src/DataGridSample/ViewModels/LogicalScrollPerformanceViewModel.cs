using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using DataGridSample.Collections;
using DataGridSample.Models;
using DataGridSample.Mvvm;

namespace DataGridSample.ViewModels;

public sealed class LogicalScrollPerformanceViewModel : ObservableObject
{
    private const int DefaultRowCount = 200_000;
    private int _itemCount = DefaultRowCount;
    private string _selectedEstimator = "Advanced";
    private string _summary = "Preparing rows...";
    private IDataGridRowHeightEstimator _rowHeightEstimator = new AdvancedRowHeightEstimator();
    private bool _isRegenerating;

    public LogicalScrollPerformanceViewModel()
    {
        Estimators = new[] { "Advanced", "Caching", "Default" };
        Rows = new ObservableRangeCollection<LogicalScrollPerformanceRow>();
        RegenerateCommand = new RelayCommand(
            _ => _ = PopulateRowsAsync(),
            _ => !IsRegenerating);
        _ = PopulateRowsAsync();
    }

    public ObservableRangeCollection<LogicalScrollPerformanceRow> Rows { get; }

    public IReadOnlyList<string> Estimators { get; }

    public RelayCommand RegenerateCommand { get; }

    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public string SelectedEstimator
    {
        get => _selectedEstimator;
        set
        {
            if (SetProperty(ref _selectedEstimator, value))
            {
                RowHeightEstimator = CreateEstimator(value);
                UpdateSummary(0);
            }
        }
    }

    public IDataGridRowHeightEstimator RowHeightEstimator
    {
        get => _rowHeightEstimator;
        private set => SetProperty(ref _rowHeightEstimator, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsRegenerating
    {
        get => _isRegenerating;
        private set
        {
            if (SetProperty(ref _isRegenerating, value))
            {
                RegenerateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private static IDataGridRowHeightEstimator CreateEstimator(string? name)
    {
        return name switch
        {
            "Caching" => new CachingRowHeightEstimator(),
            "Default" => new DefaultRowHeightEstimator(),
            _ => new AdvancedRowHeightEstimator()
        };
    }

    private async Task PopulateRowsAsync()
    {
        if (IsRegenerating)
        {
            return;
        }

        IsRegenerating = true;
        var targetCount = ItemCount;
        Summary = $"Rows: {targetCount:n0} | Regenerating... | Estimator: {SelectedEstimator}";

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var rows = await Task.Run(() =>
            {
                var random = new Random(12345);
                var utcNow = DateTime.UtcNow;
                var data = new List<LogicalScrollPerformanceRow>(targetCount);
                for (int i = 1; i <= targetCount; i++)
                {
                    data.Add(new LogicalScrollPerformanceRow
                    {
                        Id = i,
                        Category = $"CAT-{i % 50:D2}",
                        Code = $"CODE-{i % 1000:D4}",
                        Amount = Math.Round((random.NextDouble() * 100000) - 50000, 2),
                        CreatedAt = utcNow.AddSeconds(-i),
                        Description = $"Row {i} | sample payload for scroll performance checks"
                    });
                }

                return data;
            }).ConfigureAwait(true);

            Rows.ResetWith(rows);

            stopwatch.Stop();
            UpdateSummary(stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            Summary = $"Regenerate failed: {ex.Message} | Estimator: {SelectedEstimator}";
        }
        finally
        {
            IsRegenerating = false;
        }
    }

    private void UpdateSummary(double loadSeconds)
    {
        Summary = loadSeconds > 0
            ? $"Rows: {Rows.Count:n0} | Loaded in {loadSeconds:N2}s | Estimator: {SelectedEstimator}"
            : $"Rows: {Rows.Count:n0} | Estimator: {SelectedEstimator}";
    }
}
