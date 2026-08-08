// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.DataGridPivoting;
using DataGridSample.Models;
using ProCharts;
using ProCharts.Skia;
using ProDataGrid.Charting;
using ProDataGrid.SourceGeneration;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace DataGridSample.ViewModels;

[GenerateDataGridViewModel(typeof(GeneratedPivotChartRow), ProviderName = "GeneratedPivotChartRowSchema")]
[GenerateDataGridView(
    typeof(GeneratedPivotChartRow),
    ViewName = "GeneratedPivotChartGrid",
    ViewNamespace = "DataGridSample.Pages",
    Framework = DataGridViewFramework.ReactiveUI,
    Recipe = DataGridViewRecipe.Analytics,
    Title = "Generated analytics source",
    AutomationId = "generated-pivot-chart-grid")]
public sealed partial class GeneratedPivotChartViewModel : ReactiveObject, IDisposable
{
    private readonly ObservableCollection<GeneratedPivotChartRow> _source = [];
    private int _nextId = 9001;
    private int _nextPeriod = 5;
    private int _metricIndex;
    private bool _disposed;

    [Reactive]
    private string _status = "Generated pivot and chart selectors share one canonical schema.";

    [Reactive]
    private string _selectedMetric = "Revenue";

    [Reactive]
    private int _sourceRowCount;

    [Reactive]
    private int _pivotRowCount;

    [Reactive]
    private int _pivotColumnCount;

    [Reactive]
    private int _directSeriesCount;

    public GeneratedPivotChartViewModel()
    {
        AddBaselineRows();
        Items = GeneratedPivotChartRowSchema.CreateCollectionView(_source, sourceIsInGroupOrder: false);

        Pivot = GeneratedPivotChartRowSchema.CreatePivotTableModel(Items, ConfigurePivot);
        PivotChart = new PivotChartModel
        {
            Pivot = Pivot,
            SeriesSource = PivotChartSeriesSource.Columns,
            ValueField = Pivot.ValueFields[0],
            IncludeSubtotals = false,
            IncludeGrandTotals = false
        };
        PivotChartSource = new PivotChartDataSource
        {
            PivotChart = PivotChart,
            SeriesKind = ChartSeriesKind.Column
        };
        PivotChartModel = new ChartModel { DataSource = PivotChartSource };

        DirectChartSource = DataGridGeneratedChartAdapter.CreateModel(
            Items,
            GeneratedPivotChartRowSchema.AnalyticsFields);
        DirectChartSource.GroupMode = DataGridChartGroupMode.TopLevel;
        DirectChartSource.Series[0].Kind = ChartSeriesKind.Column;
        DirectChartSource.Series[1].Kind = ChartSeriesKind.Line;
        DirectChartModel = new ChartModel { DataSource = DirectChartSource };

        ChartStyle = new SkiaChartStyle
        {
            ShowGridlines = true,
            ShowCategoryGridlines = true,
            LegendFlow = SkiaLegendFlow.Row,
            LegendWrap = true
        };

        AddPeriodCommand = ReactiveCommand.Create(AddPeriod);
        RemovePeriodCommand = ReactiveCommand.Create(RemovePeriod);
        ToggleMetricCommand = ReactiveCommand.Create(ToggleMetric);
        ToggleSeriesSourceCommand = ReactiveCommand.Create(ToggleSeriesSource);
        RestoreCommand = ReactiveCommand.Create(Restore);

        Publish("Loaded deterministic grouped source, generated pivot fields, and two chart projections.");
    }

    public DataGridCollectionView Items { get; }

    public PivotTableModel Pivot { get; }

    public PivotChartModel PivotChart { get; }

    public PivotChartDataSource PivotChartSource { get; }

    public ChartModel PivotChartModel { get; }

    public DataGridChartModel DirectChartSource { get; }

    public ChartModel DirectChartModel { get; }

    public SkiaChartStyle ChartStyle { get; }

    public ReactiveCommand<RxVoid, RxVoid> AddPeriodCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RemovePeriodCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ToggleMetricCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ToggleSeriesSourceCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RestoreCommand { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DirectChartModel.Dispose();
        DirectChartSource.Dispose();
        PivotChartModel.Dispose();
        PivotChartSource.Dispose();
        PivotChart.Dispose();
        Pivot.Dispose();
    }

    private static void ConfigurePivot(PivotTableModel pivot)
    {
        pivot.RowFields[0].SortDirection = ListSortDirection.Ascending;
        pivot.RowFields[0].ShowSubtotals = false;
        pivot.ColumnFields[0].SortDirection = ListSortDirection.Ascending;
        pivot.ColumnFields[0].ShowSubtotals = false;
        pivot.Layout.RowLayout = PivotRowLayout.Tabular;
        pivot.Layout.ValuesPosition = PivotValuesPosition.Columns;
        pivot.Layout.ShowRowSubtotals = false;
        pivot.Layout.ShowColumnSubtotals = false;
        pivot.Layout.ShowRowGrandTotals = true;
        pivot.Layout.ShowColumnGrandTotals = true;
    }

    private void AddPeriod()
    {
        string period = $"P{_nextPeriod++}";
        AddPeriodRows(period, 1.08d + (_nextPeriod % 3) * 0.04d);
        RefreshCharts();
        Publish($"Added {period} as one observable batch of three regional rows.");
    }

    private void RemovePeriod()
    {
        if (_source.Count <= 3)
        {
            return;
        }

        string period = _source[^1].Period;
        while (_source.Count > 0 && string.Equals(_source[^1].Period, period, StringComparison.Ordinal))
        {
            _source.RemoveAt(_source.Count - 1);
        }

        RefreshCharts();
        Publish($"Removed {period}; pivot and chart projections observed the collection delta.");
    }

    private void ToggleMetric()
    {
        _metricIndex = (_metricIndex + 1) % Pivot.ValueFields.Count;
        PivotChart.ValueField = Pivot.ValueFields[_metricIndex];
        SelectedMetric = Pivot.ValueFields[_metricIndex].Header ?? Pivot.ValueFields[_metricIndex].Key?.ToString() ?? "Value";
        PivotChartModel.Refresh();
        Publish($"Pivot chart now uses the generated {SelectedMetric} value field.");
    }

    private void ToggleSeriesSource()
    {
        PivotChart.SeriesSource = PivotChart.SeriesSource == PivotChartSeriesSource.Columns
            ? PivotChartSeriesSource.Rows
            : PivotChartSeriesSource.Columns;
        PivotChartModel.Refresh();
        Publish($"Pivot chart series now come from {PivotChart.SeriesSource.ToString().ToLowerInvariant()}.");
    }

    private void Restore()
    {
        _source.Clear();
        _nextId = 9001;
        _nextPeriod = 5;
        _metricIndex = 0;
        AddBaselineRows();
        PivotChart.ValueField = Pivot.ValueFields[0];
        PivotChart.SeriesSource = PivotChartSeriesSource.Columns;
        SelectedMetric = Pivot.ValueFields[0].Header ?? "Revenue";
        RefreshCharts();
        Publish("Restored the deterministic four-period analytics source.");
    }

    private void AddBaselineRows()
    {
        AddPeriodRows("P1", 0.88d);
        AddPeriodRows("P2", 0.96d);
        AddPeriodRows("P3", 1.04d);
        AddPeriodRows("P4", 1.12d);
    }

    private void AddPeriodRows(string period, double factor)
    {
        AddRow(period, "North", "Direct", 128_000d * factor, 31_000d * factor, 82);
        AddRow(period, "South", "Partner", 101_000d * factor, 19_500d * factor, 67);
        AddRow(period, "West", "Direct", 116_000d * factor, 27_000d * factor, 74);
    }

    private void AddRow(string period, string region, string channel, double revenue, double profit, int units)
    {
        _source.Add(new GeneratedPivotChartRow
        {
            Id = _nextId++,
            Period = period,
            Region = region,
            Channel = channel,
            Revenue = Math.Round(revenue, 2),
            Profit = Math.Round(profit, 2),
            Units = units
        });
    }

    private void RefreshCharts()
    {
        Pivot.Refresh();
        PivotChart.Refresh();
        PivotChartModel.Refresh();
        DirectChartModel.Refresh();
    }

    private void Publish(string message)
    {
        SourceRowCount = _source.Count;
        PivotRowCount = Pivot.Rows.Count;
        PivotColumnCount = Pivot.ColumnDefinitions.Count;
        DirectSeriesCount = DirectChartSource.Series.Count;
        Status = message;
    }
}
