using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridFormulas;
using ProCharts;
using ProCharts.Skia;
using ProDataGrid.Charting;
using ProDataGrid.ExcelSample.Helpers;
using ProDataGrid.ExcelSample.Models;
using ReactiveUI;
using SkiaSharp;

namespace ProDataGrid.ExcelSample.ViewModels;

public sealed class ChartPanelViewModel : ReactiveObject, IDisposable
{
    private const int MaxPreviewPoints = 200;
    private readonly SpreadsheetSelectionState _selection;
    private readonly IScheduler _uiScheduler;
    private readonly CompositeDisposable _subscriptions = new();
    private SheetViewModel? _sheet;
    private SpreadsheetCellRange? _activeRange;
    private EventHandler<DataGridFormulaInvalidatedEventArgs>? _formulaInvalidatedHandler;
    private ChartTypeOption _selectedChartType;
    private string _rangeText = string.Empty;
    private string _statusText = "Select a range to chart.";
    private bool _autoApplySelection;
    private bool _hasChart;
    private bool _showPlaceholder = true;
    private bool _isEnabled;
    private SkiaChartStyle _chartStyle = new();

    public ChartPanelViewModel(SpreadsheetSelectionState selection, IScheduler uiScheduler)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _uiScheduler = uiScheduler ?? throw new ArgumentNullException(nameof(uiScheduler));

        ChartData = new DataGridChartModel
        {
            GroupMode = DataGridChartGroupMode.LeafItems,
            DownsampleAggregation = DataGridChartAggregation.Average,
            UseIncrementalUpdates = true
        };

        Chart = new ChartModel
        {
            DataSource = ChartData
        };
        Chart.Request.MaxPoints = MaxPreviewPoints;

        ChartStyle = BuildChartStyle();

        ChartTypes = new ObservableCollection<ChartTypeOption>(BuildChartTypes());
        _selectedChartType = ChartTypes[0];

        ApplySelectionCommand = ReactiveCommand.Create(ApplySelection);
        ClearChartCommand = ReactiveCommand.Create(ClearChart);

        _subscriptions.Add(
            _selection.Changed
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => OnSelectionChanged()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.SelectedChartType)
                .Skip(1)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => RefreshFromActiveRange()));

        _subscriptions.Add(
            this.WhenAnyValue(vm => vm.AutoApplySelection)
                .Skip(1)
                .ObserveOn(_uiScheduler)
                .Subscribe(_ => RefreshFromActiveRange()));
    }

    public ObservableCollection<ChartTypeOption> ChartTypes { get; }

    public ChartTypeOption SelectedChartType
    {
        get => _selectedChartType;
        set => this.RaiseAndSetIfChanged(ref _selectedChartType, value);
    }

    public DataGridChartModel ChartData { get; }

    public ChartModel Chart { get; }

    public SkiaChartStyle ChartStyle
    {
        get => _chartStyle;
        private set => this.RaiseAndSetIfChanged(ref _chartStyle, value);
    }

    public string RangeText
    {
        get => _rangeText;
        private set => this.RaiseAndSetIfChanged(ref _rangeText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool AutoApplySelection
    {
        get => _autoApplySelection;
        set => this.RaiseAndSetIfChanged(ref _autoApplySelection, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isEnabled, value);

            if (_isEnabled)
            {
                OnSelectionChanged();
                if (!AutoApplySelection)
                {
                    RefreshFromActiveRange();
                }
            }
        }
    }

    public bool HasChart
    {
        get => _hasChart;
        private set => this.RaiseAndSetIfChanged(ref _hasChart, value);
    }

    public bool ShowPlaceholder
    {
        get => _showPlaceholder;
        private set => this.RaiseAndSetIfChanged(ref _showPlaceholder, value);
    }

    public ReactiveCommand<Unit, Unit> ApplySelectionCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearChartCommand { get; }

    public void SetSheet(SheetViewModel sheet)
    {
        if (ReferenceEquals(_sheet, sheet))
        {
            return;
        }

        if (_sheet != null && _formulaInvalidatedHandler != null)
        {
            _sheet.FormulaModel.Invalidated -= _formulaInvalidatedHandler;
        }

        _sheet = sheet;

        if (_sheet != null)
        {
            _formulaInvalidatedHandler = (_, __) => RefreshChart();
            _sheet.FormulaModel.Invalidated += _formulaInvalidatedHandler;
        }

        RefreshFromActiveRange();
    }

    public void Dispose()
    {
        if (_sheet != null && _formulaInvalidatedHandler != null)
        {
            _sheet.FormulaModel.Invalidated -= _formulaInvalidatedHandler;
        }

        _subscriptions.Dispose();
    }

    private void OnSelectionChanged()
    {
        if (!IsEnabled)
        {
            return;
        }

        if (TryGetSelectionRange(out var range))
        {
            RangeText = range.ToA1Range();
            StatusText = AutoApplySelection
                ? $"Charting {RangeText}"
                : "Selection ready. Click Use selection to chart.";
        }
        else
        {
            RangeText = string.Empty;
            StatusText = "Select a range to chart.";
        }

        if (AutoApplySelection)
        {
            ApplySelection();
        }
    }

    private void RefreshFromActiveRange()
    {
        if (!IsEnabled)
        {
            return;
        }

        if (_activeRange.HasValue)
        {
            BuildChartFromRange(_activeRange.Value);
            return;
        }

        if (AutoApplySelection)
        {
            ApplySelection();
        }
    }

    private void ApplySelection()
    {
        if (!IsEnabled)
        {
            return;
        }

        if (!TryGetSelectionRange(out var range))
        {
            ClearChart();
            return;
        }

        _activeRange = range;
        BuildChartFromRange(range);
    }

    private bool TryGetSelectionRange(out SpreadsheetCellRange range)
    {
        range = default;

        if (_selection.SelectedRange.HasValue)
        {
            range = _selection.SelectedRange.Value;
            return true;
        }

        if (_selection.CurrentCell.HasValue)
        {
            range = new SpreadsheetCellRange(_selection.CurrentCell.Value, _selection.CurrentCell.Value);
            return true;
        }

        return false;
    }

    private void BuildChartFromRange(SpreadsheetCellRange range)
    {
        if (_sheet == null)
        {
            ClearChart();
            return;
        }

        if (!TryClampRange(range, out var rowStart, out var rowEnd, out var columnStart, out var columnEnd))
        {
            ClearChart();
            return;
        }

        var rows = new ObservableCollection<SpreadsheetRow>();
        for (var rowIndex = rowStart; rowIndex <= rowEnd; rowIndex++)
        {
            rows.Add(_sheet.Rows[rowIndex]);
        }

        var categoryColumnIndex = TryResolveCategoryColumn(columnStart, columnEnd);
        var seriesStart = categoryColumnIndex.HasValue ? categoryColumnIndex.Value + 1 : columnStart;

        var seriesDefinitions = BuildSeriesDefinitions(seriesStart, columnEnd);
        if (seriesDefinitions.Count == 0)
        {
            ClearChart();
            StatusText = "Select numeric columns to chart.";
            return;
        }

        ChartData.Series.Clear();
        foreach (var definition in seriesDefinitions)
        {
            ChartData.Series.Add(definition);
        }

        ChartData.ItemsSource = rows;
        ChartData.CategoryPath = null;
        ChartData.CategorySelector = categoryColumnIndex.HasValue
            ? item => FormatCategory((SpreadsheetRow)item, categoryColumnIndex.Value)
            : item => ((SpreadsheetRow)item).RowIndex.ToString(CultureInfo.CurrentCulture);

        Chart.Legend.IsVisible = seriesDefinitions.Count > 1;
        Chart.CategoryAxis.Title = categoryColumnIndex.HasValue
            ? ExcelColumnName.FromIndex(categoryColumnIndex.Value)
            : "Row";
        Chart.ValueAxis.Title = "Values";

        SetChartPresence(true);
        StatusText = $"Charting {range.ToA1Range()}";
        Chart.Refresh();
    }

    private bool TryClampRange(SpreadsheetCellRange range, out int rowStart, out int rowEnd, out int columnStart, out int columnEnd)
    {
        rowStart = rowEnd = columnStart = columnEnd = 0;

        if (_sheet == null)
        {
            return false;
        }

        var rowCount = _sheet.Rows.Count;
        var columnCount = _sheet.ColumnDefinitions.Count;
        if (rowCount == 0 || columnCount == 0)
        {
            return false;
        }

        rowStart = Math.Clamp(range.Start.RowIndex, 0, rowCount - 1);
        rowEnd = Math.Clamp(range.End.RowIndex, 0, rowCount - 1);
        columnStart = Math.Clamp(range.Start.ColumnIndex, 0, columnCount - 1);
        columnEnd = Math.Clamp(range.End.ColumnIndex, 0, columnCount - 1);

        if (rowStart > rowEnd || columnStart > columnEnd)
        {
            return false;
        }

        return true;
    }

    private int? TryResolveCategoryColumn(int columnStart, int columnEnd)
    {
        if (_sheet == null)
        {
            return null;
        }

        if (columnEnd - columnStart < 1)
        {
            return null;
        }

        var definition = _sheet.ColumnDefinitions[columnStart];
        return definition is DataGridTextColumnDefinition ? columnStart : null;
    }

    private List<DataGridChartSeriesDefinition> BuildSeriesDefinitions(int columnStart, int columnEnd)
    {
        var result = new List<DataGridChartSeriesDefinition>();
        if (_sheet == null)
        {
            return result;
        }

        for (var columnIndex = columnStart; columnIndex <= columnEnd; columnIndex++)
        {
            var definition = _sheet.ColumnDefinitions[columnIndex];
            if (!IsNumericColumn(definition))
            {
                continue;
            }

            var series = new DataGridChartSeriesDefinition
            {
                Name = ExcelColumnName.FromIndex(columnIndex),
                Kind = SelectedChartType.SeriesKind,
                Aggregation = DataGridChartAggregation.First
            };

            var capturedIndex = columnIndex;
            series.ValueSelector = item => ResolveCellValue((SpreadsheetRow)item, capturedIndex, definition);

            result.Add(series);

            if (SelectedChartType.IsSingleSeries)
            {
                break;
            }
        }

        return result;
    }

    private static bool IsNumericColumn(DataGridColumnDefinition definition)
    {
        return definition is DataGridNumericColumnDefinition || definition is DataGridFormulaColumnDefinition;
    }

    private string FormatCategory(SpreadsheetRow row, int columnIndex)
    {
        var value = row.GetCell(columnIndex);
        return value == null
            ? string.Empty
            : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private double? ResolveCellValue(SpreadsheetRow row, int columnIndex, DataGridColumnDefinition definition)
    {
        object? raw = definition is DataGridFormulaColumnDefinition formulaDefinition
            ? _sheet?.FormulaModel.Evaluate(row, formulaDefinition)
            : row.GetCell(columnIndex);

        return ConvertToNumber(raw);
    }

    private static double? ConvertToNumber(object? value)
    {
        if (value == null)
        {
            return null;
        }

        return value switch
        {
            double number => number,
            float number => number,
            decimal number => (double)number,
            int number => number,
            long number => number,
            short number => number,
            byte number => number,
            string text => double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed) ? parsed : null,
            _ => TryConvertFallback(value)
        };
    }

    private static double? TryConvertFallback(object value)
    {
        try
        {
            return Convert.ToDouble(value, CultureInfo.CurrentCulture);
        }
        catch
        {
            return null;
        }
    }

    private void ClearChart()
    {
        _activeRange = null;
        ChartData.Series.Clear();
        ChartData.ItemsSource = Array.Empty<SpreadsheetRow>();
        ChartData.CategorySelector = null;
        Chart.Legend.IsVisible = false;
        SetChartPresence(false);
        StatusText = "Select a range to chart.";
        Chart.Refresh();
    }

    private void RefreshChart()
    {
        if (!IsEnabled || !HasChart)
        {
            return;
        }

        Chart.Refresh();
    }

    private void SetChartPresence(bool hasChart)
    {
        HasChart = hasChart;
        ShowPlaceholder = !hasChart;
    }

    private static IReadOnlyList<ChartTypeOption> BuildChartTypes()
    {
        return new[]
        {
            new ChartTypeOption("Column", ChartSeriesKind.Column),
            new ChartTypeOption("Line", ChartSeriesKind.Line),
            new ChartTypeOption("Area", ChartSeriesKind.Area),
            new ChartTypeOption("Stacked Column", ChartSeriesKind.StackedColumn),
            new ChartTypeOption("Stacked Area", ChartSeriesKind.StackedArea),
            new ChartTypeOption("Bar", ChartSeriesKind.Bar),
            new ChartTypeOption("Stacked Bar", ChartSeriesKind.StackedBar),
            new ChartTypeOption("Pie", ChartSeriesKind.Pie, isSingleSeries: true),
            new ChartTypeOption("Donut", ChartSeriesKind.Donut, isSingleSeries: true)
        };
    }

    private static SkiaChartStyle BuildChartStyle()
    {
        return new SkiaChartStyle
        {
            ShowGridlines = true,
            ShowCategoryGridlines = true,
            ShowDataLabels = false,
            LegendPosition = ChartLegendPosition.Bottom,
            LegendFlow = SkiaLegendFlow.Row,
            LegendWrap = true,
            SeriesColors = new[]
            {
                new SKColor(33, 115, 70),
                new SKColor(0, 120, 212),
                new SKColor(242, 153, 74),
                new SKColor(155, 81, 224),
                new SKColor(52, 152, 219)
            }
        };
    }
}
