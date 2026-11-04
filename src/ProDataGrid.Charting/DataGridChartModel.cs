// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Utilities;
using ProCharts;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

namespace ProDataGrid.Charting
{
    public enum DataGridChartAggregation
    {
        Sum,
        Average,
        Min,
        Max,
        Count,
        First,
        Last
    }

    public enum DataGridChartGroupMode
    {
        LeafItems,
        TopLevel
    }

    public sealed class DataGridChartSeriesDefinition : INotifyPropertyChanged
    {
        private string? _name;
        private string? _valuePath;
        private ChartSeriesKind _kind = ChartSeriesKind.Column;
        private Func<object, double?>? _valueSelector;
        private string? _formula;
        private string? _xValuePath;
        private Func<object, double?>? _xValueSelector;
        private string? _sizePath;
        private Func<object, double?>? _sizeSelector;
        private DataGridChartAggregation _aggregation = DataGridChartAggregation.Sum;
        private DataGridChartAggregation _xAggregation = DataGridChartAggregation.Average;
        private DataGridChartAggregation _sizeAggregation = DataGridChartAggregation.Average;
        private Func<double, string>? _dataLabelFormatter;
        private ChartSeriesStyle? _style;
        private ChartValueAxisAssignment _valueAxisAssignment = ChartValueAxisAssignment.Primary;
        private ChartTrendlineType _trendlineType = ChartTrendlineType.None;
        private int _trendlinePeriod = 2;
        private ChartErrorBarType _errorBarType = ChartErrorBarType.None;
        private double _errorBarValue = 1d;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public string? ValuePath
        {
            get => _valuePath;
            set
            {
                if (_valuePath == value)
                {
                    return;
                }

                _valuePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValuePath)));
            }
        }

        public Func<object, double?>? ValueSelector
        {
            get => _valueSelector;
            set
            {
                if (_valueSelector == value)
                {
                    return;
                }

                _valueSelector = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueSelector)));
            }
        }

        public string? Formula
        {
            get => _formula;
            set
            {
                if (_formula == value)
                {
                    return;
                }

                _formula = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Formula)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCalculated)));
            }
        }

        public bool IsCalculated => !string.IsNullOrWhiteSpace(_formula);

        public string? XValuePath
        {
            get => _xValuePath;
            set
            {
                if (_xValuePath == value)
                {
                    return;
                }

                _xValuePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XValuePath)));
            }
        }

        public Func<object, double?>? XValueSelector
        {
            get => _xValueSelector;
            set
            {
                if (_xValueSelector == value)
                {
                    return;
                }

                _xValueSelector = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XValueSelector)));
            }
        }

        public string? SizePath
        {
            get => _sizePath;
            set
            {
                if (_sizePath == value)
                {
                    return;
                }

                _sizePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizePath)));
            }
        }

        public Func<object, double?>? SizeSelector
        {
            get => _sizeSelector;
            set
            {
                if (_sizeSelector == value)
                {
                    return;
                }

                _sizeSelector = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeSelector)));
            }
        }

        public ChartSeriesKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value)
                {
                    return;
                }

                _kind = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Kind)));
            }
        }

        public DataGridChartAggregation Aggregation
        {
            get => _aggregation;
            set
            {
                if (_aggregation == value)
                {
                    return;
                }

                _aggregation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Aggregation)));
            }
        }

        public DataGridChartAggregation XAggregation
        {
            get => _xAggregation;
            set
            {
                if (_xAggregation == value)
                {
                    return;
                }

                _xAggregation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(XAggregation)));
            }
        }

        public DataGridChartAggregation SizeAggregation
        {
            get => _sizeAggregation;
            set
            {
                if (_sizeAggregation == value)
                {
                    return;
                }

                _sizeAggregation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizeAggregation)));
            }
        }

        public Func<double, string>? DataLabelFormatter
        {
            get => _dataLabelFormatter;
            set
            {
                if (_dataLabelFormatter == value)
                {
                    return;
                }

                _dataLabelFormatter = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataLabelFormatter)));
            }
        }

        public ChartSeriesStyle? Style
        {
            get => _style;
            set
            {
                if (ReferenceEquals(_style, value))
                {
                    return;
                }

                _style = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Style)));
            }
        }

        public ChartValueAxisAssignment ValueAxisAssignment
        {
            get => _valueAxisAssignment;
            set
            {
                if (_valueAxisAssignment == value)
                {
                    return;
                }

                _valueAxisAssignment = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueAxisAssignment)));
            }
        }

        public ChartTrendlineType TrendlineType
        {
            get => _trendlineType;
            set
            {
                if (_trendlineType == value)
                {
                    return;
                }

                _trendlineType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrendlineType)));
            }
        }

        public int TrendlinePeriod
        {
            get => _trendlinePeriod;
            set
            {
                var period = value < 2 ? 2 : value;
                if (_trendlinePeriod == period)
                {
                    return;
                }

                _trendlinePeriod = period;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrendlinePeriod)));
            }
        }

        public ChartErrorBarType ErrorBarType
        {
            get => _errorBarType;
            set
            {
                if (_errorBarType == value)
                {
                    return;
                }

                _errorBarType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorBarType)));
            }
        }

        public double ErrorBarValue
        {
            get => _errorBarValue;
            set
            {
                if (Math.Abs(_errorBarValue - value) < double.Epsilon)
                {
                    return;
                }

                _errorBarValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorBarValue)));
            }
        }
    }

    public sealed class DataGridChartModel : IChartIncrementalDataSource, IChartWindowInfoProvider, INotifyPropertyChanged, IDisposable
    {
        private IDataGridCollectionView? _view;
        private IEnumerable? _itemsSource;
        private string? _categoryPath;
        private Func<object, string?>? _categorySelector;
        private bool _autoRefresh = true;
        private CultureInfo _culture = CultureInfo.CurrentCulture;
        private INotifyCollectionChanged? _collectionSource;
        private INotifyCollectionChanged? _groupCollectionSource;
        private readonly HashSet<INotifyPropertyChanged> _trackedItems = new();
        private DataGridChartGroupMode _groupMode = DataGridChartGroupMode.LeafItems;
        private DataGridChartAggregation _downsampleAggregation = DataGridChartAggregation.Average;
        private ChartDownsampleMode _downsampleMode = ChartDownsampleMode.Adaptive;
        private bool _useIncrementalUpdates = true;
        private ChartDataCache? _activeCache;
        private ChartDataCache? _pendingCache;
        private ChartDataDelta? _pendingDelta;
        private bool _needsFullRebuild;
        private int _cacheVersion;
        private FormulaReferenceMode _formulaReferenceMode = FormulaReferenceMode.A1;
        private IFormulaParser? _formulaParser;
        private IFormulaFunctionRegistry? _formulaFunctionRegistry;
        private readonly FormulaCalculationSettings _formulaSettings;
        private readonly FormulaChartWorkbook _formulaWorkbook;
        private readonly FormulaChartWorksheet _formulaWorksheet;
        private readonly FormulaCellAddress _formulaAddress;
        private readonly FormulaEvaluator _formulaEvaluator = new FormulaEvaluator();
        private readonly DataGridChartFormulaResolver _formulaResolver;
        private readonly Dictionary<DataGridChartSeriesDefinition, SeriesFormulaState> _formulaStates = new();

        public DataGridChartModel()
        {
            Series = new ObservableCollection<DataGridChartSeriesDefinition>();
            WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridChartModel>(
                Series,
                nameof(INotifyCollectionChanged.CollectionChanged),
                OnSeriesCollectionChanged);
            _formulaSettings = new FormulaCalculationSettings
            {
                Culture = _culture,
                ReferenceMode = _formulaReferenceMode,
                DateSystem = FormulaDateSystem.Windows1900
            };
            _formulaWorkbook = new FormulaChartWorkbook("Chart", _formulaSettings);
            _formulaWorksheet = new FormulaChartWorksheet("Data", _formulaWorkbook);
            _formulaWorkbook.AddWorksheet(_formulaWorksheet);
            _formulaAddress = new FormulaCellAddress(_formulaWorksheet.Name, 1, 1);
            _formulaResolver = new DataGridChartFormulaResolver(this);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? DataInvalidated;

        public ObservableCollection<DataGridChartSeriesDefinition> Series { get; }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh == value)
                {
                    return;
                }

                _autoRefresh = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoRefresh)));
                RequestRefresh();
            }
        }

        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (Equals(_culture, value))
                {
                    return;
                }

                _culture = value ?? CultureInfo.CurrentCulture;
                _formulaSettings.Culture = _culture;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
                InvalidateFormulaCache();
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public FormulaReferenceMode FormulaReferenceMode
        {
            get => _formulaReferenceMode;
            set
            {
                if (_formulaReferenceMode == value)
                {
                    return;
                }

                _formulaReferenceMode = value;
                _formulaSettings.ReferenceMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormulaReferenceMode)));
                InvalidateFormulaCache();
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public IFormulaParser? FormulaParser
        {
            get => _formulaParser;
            set
            {
                if (ReferenceEquals(_formulaParser, value))
                {
                    return;
                }

                _formulaParser = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormulaParser)));
                InvalidateFormulaCache();
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public IFormulaFunctionRegistry? FormulaFunctionRegistry
        {
            get => _formulaFunctionRegistry;
            set
            {
                if (ReferenceEquals(_formulaFunctionRegistry, value))
                {
                    return;
                }

                _formulaFunctionRegistry = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormulaFunctionRegistry)));
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public IDataGridCollectionView? View
        {
            get => _view;
            set
            {
                if (ReferenceEquals(_view, value))
                {
                    return;
                }

                _view = value;
                UpdateCollectionSource();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(View)));
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public IEnumerable? ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (ReferenceEquals(_itemsSource, value))
                {
                    return;
                }

                _itemsSource = value;
                UpdateCollectionSource();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemsSource)));
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public string? CategoryPath
        {
            get => _categoryPath;
            set
            {
                if (_categoryPath == value)
                {
                    return;
                }

                _categoryPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategoryPath)));
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public Func<object, string?>? CategorySelector
        {
            get => _categorySelector;
            set
            {
                if (_categorySelector == value)
                {
                    return;
                }

                _categorySelector = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CategorySelector)));
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public DataGridChartGroupMode GroupMode
        {
            get => _groupMode;
            set
            {
                if (_groupMode == value)
                {
                    return;
                }

                _groupMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupMode)));
                UpdateGroupCollectionSource();
                ResetItemTracking();
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public DataGridChartAggregation DownsampleAggregation
        {
            get => _downsampleAggregation;
            set
            {
                if (_downsampleAggregation == value)
                {
                    return;
                }

                _downsampleAggregation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownsampleAggregation)));
                RequestRefresh();
            }
        }

        public ChartDownsampleMode DownsampleMode
        {
            get => _downsampleMode;
            set
            {
                if (_downsampleMode == value)
                {
                    return;
                }

                _downsampleMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DownsampleMode)));
                RequestRefresh();
            }
        }

        public bool UseIncrementalUpdates
        {
            get => _useIncrementalUpdates;
            set
            {
                if (_useIncrementalUpdates == value)
                {
                    return;
                }

                _useIncrementalUpdates = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseIncrementalUpdates)));
                ResetItemTracking();
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
            }
        }

        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
        {
            return BuildSnapshotInternal(request, out _);
        }

        public bool TryBuildUpdate(ChartDataRequest request, ChartDataSnapshot previousSnapshot, out ChartDataUpdate update)
        {
            var snapshot = BuildSnapshotInternal(request, out var delta);
            update = new ChartDataUpdate(snapshot, delta);
            return true;
        }

        public int? GetTotalCategoryCount()
        {
            if (IsGroupedModeActive())
            {
                var view = _view;
                if (view?.Groups != null)
                {
                    return view.Groups.Count;
                }
            }

            if (_view is ICollection viewCollection)
            {
                return viewCollection.Count;
            }

            if (_itemsSource is ICollection itemsCollection)
            {
                return itemsCollection.Count;
            }

            return null;
        }

        private ChartDataSnapshot BuildSnapshotInternal(ChartDataRequest request, out ChartDataDelta delta)
        {
            if (Series.Count == 0)
            {
                delta = ChartDataDelta.Full;
                _cacheVersion++;
                return new ChartDataSnapshot(Array.Empty<string?>(), Array.Empty<ChartSeriesSnapshot>(), _cacheVersion);
            }

            if (!_useIncrementalUpdates || _needsFullRebuild || _activeCache == null || !CanUseIncremental())
            {
                _activeCache = BuildCacheFromSource();
                _pendingCache = null;
                _pendingDelta = null;
                _needsFullRebuild = false;
                delta = ChartDataDelta.Full;
                _cacheVersion++;
                return BuildSnapshotFromCache(_activeCache, request, _cacheVersion);
            }

            if (_pendingCache != null)
            {
                _activeCache = _pendingCache;
                _pendingCache = null;
            }

            delta = _pendingDelta ?? ChartDataDelta.None;
            _pendingDelta = null;
            _cacheVersion++;
            return BuildSnapshotFromCache(_activeCache, request, _cacheVersion);
        }

        private bool CanUseIncremental()
        {
            var view = _view;
            if (view != null && view.IsGrouping && _groupMode == DataGridChartGroupMode.TopLevel)
            {
                return view.Groups != null;
            }

            return true;
        }

        private ChartDataCache BuildCacheFromSource()
        {
            var categories = new List<string?>();
            var seriesValues = new List<List<double?>>(Series.Count);
            var seriesXValues = new List<List<double>?>(Series.Count);
            var seriesSizeValues = new List<List<double?>?>(Series.Count);
            var hasXValues = false;
            var hasSizeValues = false;

            for (var i = 0; i < Series.Count; i++)
            {
                var definition = Series[i];
                seriesValues.Add(new List<double?>());
                if (definition.XValueSelector != null || !string.IsNullOrWhiteSpace(definition.XValuePath))
                {
                    seriesXValues.Add(new List<double>());
                    hasXValues = true;
                }
                else
                {
                    seriesXValues.Add(null);
                }

                if (definition.SizeSelector != null || !string.IsNullOrWhiteSpace(definition.SizePath))
                {
                    seriesSizeValues.Add(new List<double?>());
                    hasSizeValues = true;
                }
                else
                {
                    seriesSizeValues.Add(null);
                }
            }

            var view = _view;
            var useGroups = _groupMode == DataGridChartGroupMode.TopLevel &&
                            view != null &&
                            view.IsGrouping &&
                            view.Groups != null;

            if (useGroups)
            {
                BuildGroupedSeries(categories, seriesValues, seriesXValues, seriesSizeValues, view!);
            }
            else
            {
                BuildItemSeries(categories, seriesValues, seriesXValues, seriesSizeValues);
            }

            return new ChartDataCache(categories, seriesValues, seriesXValues, seriesSizeValues, hasXValues, hasSizeValues);
        }

        private ChartDataSnapshot BuildSnapshotFromCache(ChartDataCache cache, ChartDataRequest request, int version)
        {
            var categories = cache.Categories;
            var seriesValues = cache.SeriesValues;
            var seriesXValues = cache.SeriesXValues;
            var seriesSizeValues = cache.SeriesSizeValues;
            var hasXValues = cache.HasXValues;
            var hasSizeValues = cache.HasSizeValues;

            var windowStart = request?.WindowStart;
            var windowCount = request?.WindowCount;
            var maxPoints = request?.MaxPoints ?? 0;
            var downsampleMode = request?.DownsampleMode ?? ChartDownsampleMode.Adaptive;
            if (downsampleMode == ChartDownsampleMode.Adaptive)
            {
                downsampleMode = _downsampleMode;
            }

            var effectiveMode = ResolveDownsampleMode(downsampleMode);

            var needsWindow = windowStart.HasValue || windowCount.HasValue;
            var needsDownsample = maxPoints > 0 && effectiveMode != ChartDownsampleMode.None;

            if (needsWindow || needsDownsample)
            {
                var cloned = cache.Clone(windowStart, windowCount);
                categories = cloned.Categories;
                seriesValues = cloned.SeriesValues;
                seriesXValues = cloned.SeriesXValues;
                seriesSizeValues = cloned.SeriesSizeValues;
                hasXValues = cloned.HasXValues;
                hasSizeValues = cloned.HasSizeValues;

                if (needsDownsample)
                {
                    ApplyDownsample(
                        categories,
                        seriesValues,
                        hasXValues ? seriesXValues : null,
                        hasSizeValues ? seriesSizeValues : null,
                        effectiveMode,
                        maxPoints);
                }
            }

            var seriesSnapshots = new List<ChartSeriesSnapshot>(Series.Count);
            for (var i = 0; i < Series.Count; i++)
            {
                var definition = Series[i];
                var xValues = seriesXValues[i];
                var sizeValues = seriesSizeValues[i];
                seriesSnapshots.Add(new ChartSeriesSnapshot(
                    definition.Name,
                    definition.Kind,
                    seriesValues[i],
                    xValues != null && xValues.Count == seriesValues[i].Count ? xValues : null,
                    sizeValues != null && sizeValues.Count == seriesValues[i].Count ? sizeValues : null,
                    definition.DataLabelFormatter,
                    definition.ValueAxisAssignment,
                    definition.TrendlineType,
                    definition.TrendlinePeriod,
                    definition.ErrorBarType,
                    definition.ErrorBarValue,
                    definition.Style));
            }

            return new ChartDataSnapshot(categories, seriesSnapshots, version);
        }

        public void Dispose()
        {
            WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridChartModel>(
                Series,
                nameof(INotifyCollectionChanged.CollectionChanged),
                OnSeriesCollectionChanged);
            foreach (var definition in Series)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridChartModel>(
                    definition,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnSeriesDefinitionPropertyChanged);
            }

            ClearItemTracking();
            ClearCollectionSource();
        }

        private IEnumerable<object> EnumerateItems()
        {
            var view = _view;
            if (view != null)
            {
                if (view.IsGrouping && view.Groups != null)
                {
                    foreach (var group in view.Groups)
                    {
                        if (group is DataGridCollectionViewGroup dataGroup)
                        {
                            foreach (var item in EnumerateGroupItems(dataGroup))
                            {
                                yield return item;
                            }
                        }
                    }

                    yield break;
                }

                foreach (var item in view)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }

                yield break;
            }

            var source = _itemsSource;
            if (source == null)
            {
                yield break;
            }

            foreach (var item in source)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private void BuildItemSeries(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?> seriesXValues,
            List<List<double?>?> seriesSizeValues)
        {
            foreach (var item in EnumerateItems())
            {
                if (IsIgnoredItem(item))
                {
                    continue;
                }

                categories.Add(ResolveCategory(item));
                for (var i = 0; i < Series.Count; i++)
                {
                    var definition = Series[i];
                    seriesValues[i].Add(ResolveValue(definition, item));
                    var xValues = seriesXValues[i];
                    if (xValues != null)
                    {
                        xValues.Add(CoerceXValue(ResolveXValue(definition, item)));
                    }

                    var sizeValues = seriesSizeValues[i];
                    if (sizeValues != null)
                    {
                        sizeValues.Add(ResolveSizeValue(definition, item));
                    }
                }
            }
        }

        private void BuildGroupedSeries(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?> seriesXValues,
            List<List<double?>?> seriesSizeValues,
            IDataGridCollectionView view)
        {
            foreach (var group in view.Groups)
            {
                if (group is not DataGridCollectionViewGroup dataGroup)
                {
                    continue;
                }

                categories.Add(ConvertToString(dataGroup.Key, _culture));

                var aggregators = new SeriesAggregator[Series.Count];
                var xAggregators = new SeriesAggregator?[Series.Count];
                var sizeAggregators = new SeriesAggregator?[Series.Count];
                for (var i = 0; i < Series.Count; i++)
                {
                    aggregators[i] = new SeriesAggregator(Series[i].Aggregation);
                    if (seriesXValues[i] != null)
                    {
                        xAggregators[i] = new SeriesAggregator(Series[i].XAggregation);
                    }

                    if (seriesSizeValues[i] != null)
                    {
                        sizeAggregators[i] = new SeriesAggregator(Series[i].SizeAggregation);
                    }
                }

                foreach (var item in EnumerateGroupItems(dataGroup))
                {
                    if (IsIgnoredItem(item))
                    {
                        continue;
                    }

                    for (var i = 0; i < Series.Count; i++)
                    {
                        var value = ResolveValue(Series[i], item);
                        aggregators[i].Add(value);
                        if (xAggregators[i] != null)
                        {
                            var xValue = ResolveXValue(Series[i], item);
                            xAggregators[i]!.Add(xValue);
                        }

                        if (sizeAggregators[i] != null)
                        {
                            var sizeValue = ResolveSizeValue(Series[i], item);
                            sizeAggregators[i]!.Add(sizeValue);
                        }
                    }
                }

                for (var i = 0; i < Series.Count; i++)
                {
                    seriesValues[i].Add(aggregators[i].GetValue());
                    if (xAggregators[i] != null)
                    {
                        seriesXValues[i]!.Add(CoerceXValue(xAggregators[i]!.GetValue()));
                    }

                    if (sizeAggregators[i] != null)
                    {
                        seriesSizeValues[i]!.Add(sizeAggregators[i]!.GetValue());
                    }
                }
            }
        }

        private void InvalidateCache(ChartDataDelta delta)
        {
            _activeCache = null;
            _pendingCache = null;
            _pendingDelta = delta;
            _needsFullRebuild = true;
        }

        private void InvalidateFormulaCache()
        {
            _formulaStates.Clear();
        }

        private void QueueCollectionChange(NotifyCollectionChangedEventArgs e)
        {
            if (!_useIncrementalUpdates)
            {
                InvalidateCache(ChartDataDelta.Full);
                return;
            }

            if (!CanUseIncremental() || _activeCache == null)
            {
                InvalidateCache(ChartDataDelta.Full);
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                InvalidateCache(new ChartDataDelta(ChartDataDeltaKind.Reset));
                return;
            }

            if (IsGroupedModeActive())
            {
                if (!TryApplyGroupedCollectionChange(e, out var groupedDelta))
                {
                    InvalidateCache(ChartDataDelta.Full);
                    return;
                }

                if (groupedDelta.Kind == ChartDataDeltaKind.None)
                {
                    return;
                }

                if (_pendingDelta == null)
                {
                    _pendingDelta = groupedDelta;
                    return;
                }

                if (TryMergeDelta(_pendingDelta, groupedDelta, out var mergedGroupDelta))
                {
                    _pendingDelta = mergedGroupDelta;
                    return;
                }

                _pendingDelta = ChartDataDelta.Full;
                return;
            }

            if (!TryApplyCollectionChange(e, out var delta))
            {
                InvalidateCache(ChartDataDelta.Full);
                return;
            }

            if (_pendingDelta == null)
            {
                _pendingDelta = delta;
                return;
            }

            if (TryMergeDelta(_pendingDelta, delta, out var merged))
            {
                _pendingDelta = merged;
                return;
            }

            _pendingDelta = ChartDataDelta.Full;
        }

        private bool TryApplyCollectionChange(NotifyCollectionChangedEventArgs e, out ChartDataDelta delta)
        {
            delta = ChartDataDelta.Full;
            if (_activeCache == null)
            {
                return false;
            }

            var cache = EnsurePendingCache();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null)
                    {
                        return false;
                    }

                    if (!TryInsertItems(cache, e.NewStartingIndex, e.NewItems))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(ChartDataDeltaKind.Insert, e.NewStartingIndex, 0, e.NewItems.Count, BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryRemoveItems(cache, e.OldStartingIndex, e.OldItems.Count))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(ChartDataDeltaKind.Remove, e.OldStartingIndex, e.OldItems.Count, 0, BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems == null)
                    {
                        return false;
                    }

                    if (!TryReplaceItems(cache, e.NewStartingIndex, e.NewItems))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(ChartDataDeltaKind.Replace, e.NewStartingIndex, e.NewItems.Count, e.NewItems.Count, BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryMoveItems(cache, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(ChartDataDeltaKind.Move, e.NewStartingIndex, e.OldItems.Count, e.OldItems.Count, BuildSeriesIndices());
                    return true;
                default:
                    return false;
            }
        }

        private bool TryApplyGroupedCollectionChange(NotifyCollectionChangedEventArgs e, out ChartDataDelta delta)
        {
            delta = ChartDataDelta.Full;
            if (_activeCache == null)
            {
                return false;
            }

            var view = _view;
            if (view != null && view.Groups != null && view.Groups.Count != _activeCache.Categories.Count)
            {
                delta = ChartDataDelta.None;
                return true;
            }

            var cache = EnsurePendingCache();
            var updatedIndices = new HashSet<int>();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null)
                    {
                        return false;
                    }

                    if (!TryUpdateGroupsForItems(cache, e.NewItems, true, updatedIndices))
                    {
                        return false;
                    }

                    delta = BuildGroupUpdateDelta(updatedIndices);
                    return true;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryUpdateGroupsForItems(cache, e.OldItems, false, updatedIndices))
                    {
                        return false;
                    }

                    delta = BuildGroupUpdateDelta(updatedIndices);
                    return true;
                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems == null || e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryUpdateGroupsForItems(cache, e.OldItems, false, updatedIndices))
                    {
                        return false;
                    }

                    if (!TryUpdateGroupsForItems(cache, e.NewItems, true, updatedIndices))
                    {
                        return false;
                    }

                    delta = BuildGroupUpdateDelta(updatedIndices);
                    return true;
                case NotifyCollectionChangedAction.Move:
                    delta = ChartDataDelta.None;
                    return true;
                default:
                    return false;
            }
        }

        private bool TryApplyGroupCollectionChange(NotifyCollectionChangedEventArgs e, out ChartDataDelta delta)
        {
            delta = ChartDataDelta.Full;
            if (_activeCache == null)
            {
                return false;
            }

            var cache = EnsurePendingCache();
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems == null)
                    {
                        return false;
                    }

                    if (!TryInsertGroups(cache, e.NewStartingIndex, e.NewItems))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(
                        ChartDataDeltaKind.Insert,
                        e.NewStartingIndex,
                        0,
                        e.NewItems.Count,
                        BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryRemoveItems(cache, e.OldStartingIndex, e.OldItems.Count))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(
                        ChartDataDeltaKind.Remove,
                        e.OldStartingIndex,
                        e.OldItems.Count,
                        0,
                        BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems == null)
                    {
                        return false;
                    }

                    if (!TryReplaceGroups(cache, e.NewStartingIndex, e.NewItems))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(
                        ChartDataDeltaKind.Replace,
                        e.NewStartingIndex,
                        e.NewItems.Count,
                        e.NewItems.Count,
                        BuildSeriesIndices());
                    return true;
                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems == null)
                    {
                        return false;
                    }

                    if (!TryMoveItems(cache, e.OldStartingIndex, e.NewStartingIndex, e.OldItems.Count))
                    {
                        return false;
                    }

                    delta = new ChartDataDelta(
                        ChartDataDeltaKind.Move,
                        e.NewStartingIndex,
                        e.OldItems.Count,
                        e.OldItems.Count,
                        BuildSeriesIndices());
                    return true;
                default:
                    return false;
            }
        }

        private ChartDataCache EnsurePendingCache()
        {
            if (_pendingCache != null)
            {
                return _pendingCache;
            }

            _pendingCache = _activeCache!.Clone();
            return _pendingCache;
        }

        private bool TryInsertItems(ChartDataCache cache, int startIndex, IList items)
        {
            var index = startIndex < 0 ? cache.Categories.Count : startIndex;
            if (index > cache.Categories.Count)
            {
                return false;
            }

            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (items[itemIndex] is not object item || IsIgnoredItem(item))
                {
                    return false;
                }

                cache.Categories.Insert(index, ResolveCategory(item));
                for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
                {
                    var definition = Series[seriesIndex];
                    cache.SeriesValues[seriesIndex].Insert(index, ResolveValue(definition, item));
                    var xValues = cache.SeriesXValues[seriesIndex];
                    if (xValues != null)
                    {
                        xValues.Insert(index, CoerceXValue(ResolveXValue(definition, item)));
                    }

                    var sizeValues = cache.SeriesSizeValues[seriesIndex];
                    if (sizeValues != null)
                    {
                        sizeValues.Insert(index, ResolveSizeValue(definition, item));
                    }
                }

                index++;
            }

            return true;
        }

        private bool TryRemoveItems(ChartDataCache cache, int startIndex, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (startIndex < 0 || startIndex + count > cache.Categories.Count)
            {
                return false;
            }

            cache.Categories.RemoveRange(startIndex, count);
            for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
            {
                cache.SeriesValues[seriesIndex].RemoveRange(startIndex, count);
                cache.SeriesXValues[seriesIndex]?.RemoveRange(startIndex, count);
                cache.SeriesSizeValues[seriesIndex]?.RemoveRange(startIndex, count);
            }

            return true;
        }

        private bool TryReplaceItems(ChartDataCache cache, int startIndex, IList items)
        {
            if (startIndex < 0 || startIndex + items.Count > cache.Categories.Count)
            {
                return false;
            }

            var index = startIndex;
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (items[itemIndex] is not object item || IsIgnoredItem(item))
                {
                    return false;
                }

                cache.Categories[index] = ResolveCategory(item);
                for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
                {
                    var definition = Series[seriesIndex];
                    cache.SeriesValues[seriesIndex][index] = ResolveValue(definition, item);
                    var xValues = cache.SeriesXValues[seriesIndex];
                    if (xValues != null)
                    {
                        xValues[index] = CoerceXValue(ResolveXValue(definition, item));
                    }

                    var sizeValues = cache.SeriesSizeValues[seriesIndex];
                    if (sizeValues != null)
                    {
                        sizeValues[index] = ResolveSizeValue(definition, item);
                    }
                }

                index++;
            }

            return true;
        }

        private bool TryUpdateItem(ChartDataCache cache, int index, object item)
        {
            if (index < 0 || index >= cache.Categories.Count)
            {
                return false;
            }

            if (IsIgnoredItem(item))
            {
                return false;
            }

            cache.Categories[index] = ResolveCategory(item);
            for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
            {
                var definition = Series[seriesIndex];
                cache.SeriesValues[seriesIndex][index] = ResolveValue(definition, item);

                var xValues = cache.SeriesXValues[seriesIndex];
                if (xValues != null)
                {
                    xValues[index] = CoerceXValue(ResolveXValue(definition, item));
                }

                var sizeValues = cache.SeriesSizeValues[seriesIndex];
                if (sizeValues != null)
                {
                    sizeValues[index] = ResolveSizeValue(definition, item);
                }
            }

            return true;
        }

        private bool TryMoveItems(ChartDataCache cache, int oldIndex, int newIndex, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (oldIndex < 0 || newIndex < 0)
            {
                return false;
            }

            if (oldIndex + count > cache.Categories.Count)
            {
                return false;
            }

            if (newIndex > cache.Categories.Count)
            {
                return false;
            }

            var categorySlice = cache.Categories.GetRange(oldIndex, count);
            cache.Categories.RemoveRange(oldIndex, count);
            if (newIndex > oldIndex)
            {
                newIndex -= count;
            }

            cache.Categories.InsertRange(newIndex, categorySlice);

            for (var seriesIndex = 0; seriesIndex < Series.Count; seriesIndex++)
            {
                var values = cache.SeriesValues[seriesIndex];
                var valueSlice = values.GetRange(oldIndex, count);
                values.RemoveRange(oldIndex, count);
                values.InsertRange(newIndex, valueSlice);

                var xValues = cache.SeriesXValues[seriesIndex];
                if (xValues != null)
                {
                    var xSlice = xValues.GetRange(oldIndex, count);
                    xValues.RemoveRange(oldIndex, count);
                    xValues.InsertRange(newIndex, xSlice);
                }

                var sizeValues = cache.SeriesSizeValues[seriesIndex];
                if (sizeValues != null)
                {
                    var sizeSlice = sizeValues.GetRange(oldIndex, count);
                    sizeValues.RemoveRange(oldIndex, count);
                    sizeValues.InsertRange(newIndex, sizeSlice);
                }
            }

            return true;
        }

        private bool TryUpdateGroupedItem(ChartDataCache cache, object item)
        {
            var matches = new List<GroupMatch>();
            if (!TryResolveGroupsForItem(item, true, false, matches))
            {
                return false;
            }

            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!TryUpdateGroup(cache, match.Index, match.Group))
                {
                    return false;
                }

                QueueGroupUpdate(match.Index);
            }

            return true;
        }

        private bool TryUpdateGroupsForItems(ChartDataCache cache, IList items, bool allowItemScan, HashSet<int> updatedIndices)
        {
            if (items.Count == 0)
            {
                return true;
            }

            var allowMissing = !allowItemScan;
            var matches = new List<GroupMatch>();
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (items[itemIndex] is not object item || IsIgnoredItem(item))
                {
                    continue;
                }

                matches.Clear();
                if (!TryResolveGroupsForItem(item, allowItemScan, allowMissing, matches))
                {
                    return false;
                }

                for (var i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    if (updatedIndices.Contains(match.Index))
                    {
                        continue;
                    }

                    if (!TryUpdateGroup(cache, match.Index, match.Group))
                    {
                        return false;
                    }

                    updatedIndices.Add(match.Index);
                }
            }

            return true;
        }

        private ChartDataDelta BuildGroupUpdateDelta(HashSet<int> indices)
        {
            if (indices.Count == 0)
            {
                return ChartDataDelta.None;
            }

            var min = int.MaxValue;
            var max = int.MinValue;
            foreach (var index in indices)
            {
                min = Math.Min(min, index);
                max = Math.Max(max, index);
            }

            if (min == int.MaxValue || max == int.MinValue)
            {
                return ChartDataDelta.None;
            }

            return new ChartDataDelta(
                ChartDataDeltaKind.Update,
                min,
                0,
                Math.Max(1, max - min + 1),
                BuildSeriesIndices());
        }

        private bool TryResolveGroupsForItem(object item, bool allowItemScan, bool allowMissingGroups, List<GroupMatch> matches)
        {
            matches.Clear();
            var view = _view;
            if (view == null || view.Groups == null)
            {
                return false;
            }

            if (TryGetGroupKeys(item, out var keys, out var description))
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    if (TryFindGroupByKey(view, description, key, out var index, out var group) && group != null)
                    {
                        matches.Add(new GroupMatch(index, group));
                    }
                }

                if (matches.Count > 0)
                {
                    return true;
                }

                return allowMissingGroups;
            }

            if (!allowItemScan)
            {
                return false;
            }

            for (var i = 0; i < view.Groups.Count; i++)
            {
                if (view.Groups[i] is DataGridCollectionViewGroup dataGroup &&
                    GroupContainsItem(dataGroup, item))
                {
                    matches.Add(new GroupMatch(i, dataGroup));
                }
            }

            if (matches.Count > 0)
            {
                return true;
            }

            return allowMissingGroups;
        }

        private bool TryGetGroupKeys(object item, out IReadOnlyList<object?> keys, out DataGridGroupDescription? description)
        {
            keys = Array.Empty<object?>();
            description = GetTopLevelGroupDescription();
            if (description == null)
            {
                return false;
            }

            var culture = _view?.Culture ?? _culture;
            var key = description.GroupKeyFromItem(item, 0, culture);
            if (key is ICollection keyCollection)
            {
                var list = new List<object?>();
                foreach (var entry in keyCollection)
                {
                    list.Add(entry);
                }

                keys = list;
                return keys.Count > 0;
            }

            keys = new[] { key };
            return true;
        }

        private DataGridGroupDescription? GetTopLevelGroupDescription()
        {
            if (_view is DataGridCollectionView dataGridView)
            {
                var descriptions = dataGridView.GroupDescriptions;
                if (descriptions != null && descriptions.Count > 0)
                {
                    return descriptions[0];
                }
            }

            return null;
        }

        private static bool TryFindGroupByKey(
            IDataGridCollectionView view,
            DataGridGroupDescription? description,
            object? key,
            out int index,
            out DataGridCollectionViewGroup? group)
        {
            index = -1;
            group = null;
            if (view.Groups == null)
            {
                return false;
            }

            for (var i = 0; i < view.Groups.Count; i++)
            {
                if (view.Groups[i] is not DataGridCollectionViewGroup dataGroup)
                {
                    continue;
                }

                var matches = description != null
                    ? description.KeysMatch(dataGroup.Key, key)
                    : Equals(dataGroup.Key, key);
                if (matches)
                {
                    index = i;
                    group = dataGroup;
                    return true;
                }
            }

            return false;
        }

        private static bool GroupContainsItem(DataGridCollectionViewGroup group, object item)
        {
            foreach (var leaf in EnumerateGroupItems(group))
            {
                if (Equals(leaf, item))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryUpdateGroup(ChartDataCache cache, int groupIndex, DataGridCollectionViewGroup group)
        {
            if (groupIndex < 0 || groupIndex >= cache.Categories.Count)
            {
                return false;
            }

            var values = new double?[Series.Count];
            var xValues = cache.HasXValues ? new double[Series.Count] : null;
            var sizeValues = cache.HasSizeValues ? new double?[Series.Count] : null;

            FillGroupAggregates(group, cache, values, xValues, sizeValues);

            cache.Categories[groupIndex] = ConvertToString(group.Key, _culture);
            for (var i = 0; i < Series.Count; i++)
            {
                cache.SeriesValues[i][groupIndex] = values[i];

                if (cache.SeriesXValues[i] != null)
                {
                    cache.SeriesXValues[i]![groupIndex] = xValues![i];
                }

                if (cache.SeriesSizeValues[i] != null)
                {
                    cache.SeriesSizeValues[i]![groupIndex] = sizeValues![i];
                }
            }

            return true;
        }

        private void FillGroupAggregates(
            DataGridCollectionViewGroup group,
            ChartDataCache cache,
            double?[] values,
            double[]? xValues,
            double?[]? sizeValues)
        {
            var aggregators = new SeriesAggregator[Series.Count];
            var xAggregators = new SeriesAggregator?[Series.Count];
            var sizeAggregators = new SeriesAggregator?[Series.Count];

            for (var i = 0; i < Series.Count; i++)
            {
                aggregators[i] = new SeriesAggregator(Series[i].Aggregation);
                if (cache.SeriesXValues[i] != null && xValues != null)
                {
                    xAggregators[i] = new SeriesAggregator(Series[i].XAggregation);
                }

                if (cache.SeriesSizeValues[i] != null && sizeValues != null)
                {
                    sizeAggregators[i] = new SeriesAggregator(Series[i].SizeAggregation);
                }
            }

            foreach (var item in EnumerateGroupItems(group))
            {
                if (IsIgnoredItem(item))
                {
                    continue;
                }

                for (var i = 0; i < Series.Count; i++)
                {
                    aggregators[i].Add(ResolveValue(Series[i], item));
                    if (xAggregators[i] != null)
                    {
                        xAggregators[i]!.Add(ResolveXValue(Series[i], item));
                    }

                    if (sizeAggregators[i] != null)
                    {
                        sizeAggregators[i]!.Add(ResolveSizeValue(Series[i], item));
                    }
                }
            }

            for (var i = 0; i < Series.Count; i++)
            {
                values[i] = aggregators[i].GetValue();
                if (xAggregators[i] != null && xValues != null)
                {
                    xValues[i] = CoerceXValue(xAggregators[i]!.GetValue());
                }

                if (sizeAggregators[i] != null && sizeValues != null)
                {
                    sizeValues[i] = sizeAggregators[i]!.GetValue();
                }
            }
        }

        private bool TryInsertGroups(ChartDataCache cache, int startIndex, IList groups)
        {
            var index = startIndex < 0 ? cache.Categories.Count : startIndex;
            if (index > cache.Categories.Count)
            {
                return false;
            }

            var values = new double?[Series.Count];
            var xValues = cache.HasXValues ? new double[Series.Count] : null;
            var sizeValues = cache.HasSizeValues ? new double?[Series.Count] : null;

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groups[groupIndex] is not DataGridCollectionViewGroup dataGroup)
                {
                    return false;
                }

                FillGroupAggregates(dataGroup, cache, values, xValues, sizeValues);
                cache.Categories.Insert(index, ConvertToString(dataGroup.Key, _culture));
                for (var i = 0; i < Series.Count; i++)
                {
                    cache.SeriesValues[i].Insert(index, values[i]);
                    if (cache.SeriesXValues[i] != null)
                    {
                        cache.SeriesXValues[i]!.Insert(index, xValues![i]);
                    }

                    if (cache.SeriesSizeValues[i] != null)
                    {
                        cache.SeriesSizeValues[i]!.Insert(index, sizeValues![i]);
                    }
                }

                index++;
            }

            return true;
        }

        private bool TryReplaceGroups(ChartDataCache cache, int startIndex, IList groups)
        {
            if (startIndex < 0 || startIndex + groups.Count > cache.Categories.Count)
            {
                return false;
            }

            var values = new double?[Series.Count];
            var xValues = cache.HasXValues ? new double[Series.Count] : null;
            var sizeValues = cache.HasSizeValues ? new double?[Series.Count] : null;

            var index = startIndex;
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groups[groupIndex] is not DataGridCollectionViewGroup dataGroup)
                {
                    return false;
                }

                FillGroupAggregates(dataGroup, cache, values, xValues, sizeValues);
                cache.Categories[index] = ConvertToString(dataGroup.Key, _culture);
                for (var i = 0; i < Series.Count; i++)
                {
                    cache.SeriesValues[i][index] = values[i];
                    if (cache.SeriesXValues[i] != null)
                    {
                        cache.SeriesXValues[i]![index] = xValues![i];
                    }

                    if (cache.SeriesSizeValues[i] != null)
                    {
                        cache.SeriesSizeValues[i]![index] = sizeValues![i];
                    }
                }

                index++;
            }

            return true;
        }

        private void QueueGroupUpdate(int index)
        {
            var delta = new ChartDataDelta(ChartDataDeltaKind.Update, index, 1, 1, BuildSeriesIndices());
            if (_pendingDelta == null)
            {
                _pendingDelta = delta;
                return;
            }

            if (TryMergeDelta(_pendingDelta, delta, out var merged))
            {
                _pendingDelta = merged;
                return;
            }

            _pendingDelta = ChartDataDelta.Full;
        }

        private static bool TryMergeDelta(ChartDataDelta existing, ChartDataDelta next, out ChartDataDelta merged)
        {
            merged = existing;
            if (existing.IsFullRefresh || next.IsFullRefresh)
            {
                return false;
            }

            if (existing.Kind != next.Kind)
            {
                return false;
            }

            switch (existing.Kind)
            {
                case ChartDataDeltaKind.Insert:
                    if (next.Index != existing.Index + existing.NewCount)
                    {
                        return false;
                    }

                    merged = new ChartDataDelta(
                        ChartDataDeltaKind.Insert,
                        existing.Index,
                        0,
                        existing.NewCount + next.NewCount,
                        existing.SeriesIndices);
                    return true;
                case ChartDataDeltaKind.Remove:
                    if (next.Index != existing.Index)
                    {
                        return false;
                    }

                    merged = new ChartDataDelta(
                        ChartDataDeltaKind.Remove,
                        existing.Index,
                        existing.OldCount + next.OldCount,
                        0,
                        existing.SeriesIndices);
                    return true;
                case ChartDataDeltaKind.Replace:
                    if (next.Index != existing.Index + existing.NewCount)
                    {
                        return false;
                    }

                    merged = new ChartDataDelta(
                        ChartDataDeltaKind.Replace,
                        existing.Index,
                        existing.OldCount + next.OldCount,
                        existing.NewCount + next.NewCount,
                        MergeSeriesIndices(existing.SeriesIndices, next.SeriesIndices));
                    return true;
                case ChartDataDeltaKind.Update:
                    {
                        var start = Math.Min(existing.Index, next.Index);
                        var end = Math.Max(existing.Index + existing.NewCount, next.Index + next.NewCount);
                        merged = new ChartDataDelta(
                            ChartDataDeltaKind.Update,
                            start,
                            0,
                            Math.Max(1, end - start),
                            MergeSeriesIndices(existing.SeriesIndices, next.SeriesIndices));
                        return true;
                    }
                default:
                    return false;
            }
        }

        private static IReadOnlyList<int>? MergeSeriesIndices(IReadOnlyList<int>? existing, IReadOnlyList<int>? next)
        {
            if (existing == null || next == null)
            {
                return null;
            }

            if (existing.Count == 0)
            {
                return next;
            }

            if (next.Count == 0)
            {
                return existing;
            }

            var set = new HashSet<int>(existing);
            for (var i = 0; i < next.Count; i++)
            {
                set.Add(next[i]);
            }

            return new List<int>(set);
        }

        private List<int> BuildSeriesIndices()
        {
            var indices = new List<int>(Series.Count);
            for (var i = 0; i < Series.Count; i++)
            {
                indices.Add(i);
            }

            return indices;
        }

        private static IEnumerable<object> EnumerateGroupItems(DataGridCollectionViewGroup group)
        {
            foreach (var item in group.Items)
            {
                if (item is DataGridCollectionViewGroup subGroup)
                {
                    foreach (var leaf in EnumerateGroupItems(subGroup))
                    {
                        yield return leaf;
                    }
                }
                else if (item != null)
                {
                    yield return item;
                }
            }
        }

        private static bool IsIgnoredItem(object item)
        {
            if (ReferenceEquals(item, DataGridCollectionView.NewItemPlaceholder))
            {
                return true;
            }

            return item is DataGridCollectionViewGroup;
        }

        private string? ResolveCategory(object item)
        {
            if (_categorySelector != null)
            {
                return _categorySelector(item);
            }

            if (!string.IsNullOrWhiteSpace(_categoryPath))
            {
                var value = PropertyPathAccessor.GetValue(item, _categoryPath!);
                return ConvertToString(value, _culture);
            }

            return ConvertToString(item, _culture);
        }

        private double? ResolveValue(DataGridChartSeriesDefinition definition, object item)
        {
            if (definition.IsCalculated)
            {
                return EvaluateFormula(definition, item);
            }

            if (definition.ValueSelector != null)
            {
                return definition.ValueSelector(item);
            }

            if (!string.IsNullOrWhiteSpace(definition.ValuePath))
            {
                var value = PropertyPathAccessor.GetValue(item, definition.ValuePath!);
                return ConvertToNumber(value, _culture);
            }

            return ConvertToNumber(item, _culture);
        }

        private double? ResolveXValue(DataGridChartSeriesDefinition definition, object item)
        {
            if (definition.XValueSelector != null)
            {
                return definition.XValueSelector(item);
            }

            if (!string.IsNullOrWhiteSpace(definition.XValuePath))
            {
                var value = PropertyPathAccessor.GetValue(item, definition.XValuePath!);
                return ConvertToNumber(value, _culture);
            }

            return null;
        }

        private double? ResolveSizeValue(DataGridChartSeriesDefinition definition, object item)
        {
            if (definition.SizeSelector != null)
            {
                return definition.SizeSelector(item);
            }

            if (!string.IsNullOrWhiteSpace(definition.SizePath))
            {
                var value = PropertyPathAccessor.GetValue(item, definition.SizePath!);
                return ConvertToNumber(value, _culture);
            }

            return null;
        }

        private double? EvaluateFormula(DataGridChartSeriesDefinition definition, object item)
        {
            var state = EnsureFormulaState(definition);
            if (state == null || state.Expression == null || state.HasError)
            {
                return null;
            }

            _formulaResolver.SetItem(item);
            var context = new FormulaEvaluationContext(_formulaWorkbook, _formulaWorksheet, _formulaAddress, GetFormulaFunctionRegistry());
            var value = _formulaEvaluator.Evaluate(state.Expression, context, _formulaResolver);
            return ConvertFormulaValue(value, context.Address);
        }

        private SeriesFormulaState? EnsureFormulaState(DataGridChartSeriesDefinition definition)
        {
            if (!definition.IsCalculated)
            {
                return null;
            }

            if (!_formulaStates.TryGetValue(definition, out var state))
            {
                state = new SeriesFormulaState();
                _formulaStates[definition] = state;
            }

            var formulaText = definition.Formula;
            if (string.IsNullOrWhiteSpace(formulaText))
            {
                state.FormulaText = null;
                state.Expression = null;
                state.HasError = false;
                return null;
            }

            if (!string.Equals(state.FormulaText, formulaText, StringComparison.Ordinal))
            {
                state.FormulaText = formulaText;
                state.Expression = TryParseFormula(formulaText!, out var hasError);
                state.HasError = hasError;
            }

            return state;
        }

        private FormulaExpression? TryParseFormula(string formulaText, out bool hasError)
        {
            hasError = false;
            try
            {
                return GetFormulaParser().Parse(formulaText, CreateFormulaParseOptions());
            }
            catch (FormulaParseException)
            {
                hasError = true;
                return null;
            }
        }

        private FormulaParseOptions CreateFormulaParseOptions()
        {
            var options = new FormulaParseOptions
            {
                ReferenceMode = _formulaReferenceMode,
                AllowLeadingEquals = true
            };

            var listSeparator = _culture.TextInfo.ListSeparator;
            if (!string.IsNullOrEmpty(listSeparator))
            {
                var separator = listSeparator[0];
                if (!char.IsWhiteSpace(separator))
                {
                    options.ArgumentSeparator = separator;
                }
            }

            var decimalSeparator = _culture.NumberFormat.NumberDecimalSeparator;
            if (!string.IsNullOrEmpty(decimalSeparator))
            {
                options.DecimalSeparator = decimalSeparator[0];
            }

            return options;
        }

        private IFormulaParser GetFormulaParser()
        {
            _formulaParser ??= new ExcelFormulaParser();
            return _formulaParser;
        }

        private IFormulaFunctionRegistry GetFormulaFunctionRegistry()
        {
            _formulaFunctionRegistry ??= new ExcelFunctionRegistry();
            return _formulaFunctionRegistry;
        }

        private static double? ConvertFormulaValue(FormulaValue value, FormulaCellAddress address)
        {
            if (value.Kind == FormulaValueKind.Array)
            {
                value = FormulaCoercion.ApplyImplicitIntersection(value, address);
            }

            if (value.Kind == FormulaValueKind.Blank || value.Kind == FormulaValueKind.Error)
            {
                return null;
            }

            if (!FormulaCoercion.TryCoerceToNumber(value, out var number, out _))
            {
                return null;
            }

            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                return null;
            }

            return number;
        }

        private FormulaValue ConvertToFormulaValue(object? value)
        {
            if (value == null)
            {
                return FormulaValue.Blank;
            }

            if (value is FormulaValue formulaValue)
            {
                return formulaValue;
            }

            if (value is FormulaError error)
            {
                return FormulaValue.FromError(error);
            }

            if (value is string text)
            {
                return FormulaValue.FromText(text);
            }

            if (value is bool boolValue)
            {
                return FormulaValue.FromBoolean(boolValue);
            }

            if (value is double doubleValue)
            {
                return FormulaValue.FromNumber(doubleValue);
            }

            if (value is float floatValue)
            {
                return FormulaValue.FromNumber(floatValue);
            }

            if (value is decimal decimalValue)
            {
                return FormulaValue.FromNumber((double)decimalValue);
            }

            if (value is int intValue)
            {
                return FormulaValue.FromNumber(intValue);
            }

            if (value is long longValue)
            {
                return FormulaValue.FromNumber(longValue);
            }

            if (value is short shortValue)
            {
                return FormulaValue.FromNumber(shortValue);
            }

            if (value is byte byteValue)
            {
                return FormulaValue.FromNumber(byteValue);
            }

            if (value is DateTime dateValue)
            {
                return FormulaValue.FromNumber(dateValue.ToOADate());
            }

            if (value is TimeSpan timeValue)
            {
                return FormulaValue.FromNumber(timeValue.TotalDays);
            }

            if (value is IConvertible)
            {
                try
                {
                    return FormulaValue.FromNumber(Convert.ToDouble(value, _culture));
                }
                catch (FormatException)
                {
                    return FormulaValue.FromText(Convert.ToString(value, _culture) ?? string.Empty);
                }
                catch (InvalidCastException)
                {
                    return FormulaValue.FromText(Convert.ToString(value, _culture) ?? string.Empty);
                }
            }

            return FormulaValue.FromText(value.ToString() ?? string.Empty);
        }

        private static bool TryGetItemValue(object item, string path, out object? value)
        {
            if (item is IReadOnlyDictionary<string, object> readonlyObjectDictionary &&
                readonlyObjectDictionary.TryGetValue(path, out var readonlyObjectValue))
            {
                value = readonlyObjectValue;
                return true;
            }

            if (item is IReadOnlyDictionary<string, object?> readonlyDictionary &&
                readonlyDictionary.TryGetValue(path, out value))
            {
                return true;
            }

            if (item is IDictionary<string, object> dictionaryObject &&
                dictionaryObject.TryGetValue(path, out var objectValue))
            {
                value = objectValue;
                return true;
            }

            if (item is IDictionary<string, object?> dictionary &&
                dictionary.TryGetValue(path, out value))
            {
                return true;
            }

            if (item is IDictionary legacyDictionary && legacyDictionary.Contains(path))
            {
                value = legacyDictionary[path];
                return true;
            }

            return PropertyPathAccessor.TryGetValue(item, path, out value);
        }

        private static double CoerceXValue(double? value)
        {
            return value ?? double.NaN;
        }

        private static double? NormalizeXValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return null;
            }

            return value;
        }

        private static string? ConvertToString(object? value, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string text)
            {
                return text;
            }

            return Convert.ToString(value, culture);
        }

        private static double? ConvertToNumber(object? value, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            if (value is double doubleValue)
            {
                return doubleValue;
            }

            if (value is float floatValue)
            {
                return floatValue;
            }

            if (value is decimal decimalValue)
            {
                return (double)decimalValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (value is short shortValue)
            {
                return shortValue;
            }

            if (value is byte byteValue)
            {
                return byteValue;
            }

            if (value is bool boolValue)
            {
                return boolValue ? 1d : 0d;
            }

            if (value is DateTime dateValue)
            {
                return dateValue.ToOADate();
            }

            if (value is TimeSpan timeValue)
            {
                return timeValue.TotalDays;
            }

            if (value is IConvertible)
            {
                try
                {
                    return Convert.ToDouble(value, culture);
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (InvalidCastException)
                {
                    return null;
                }
            }

            return null;
        }

        private ChartDownsampleMode ResolveDownsampleMode(ChartDownsampleMode requested)
        {
            // Matrix cells, hierarchy weights, and gauge slots carry category identity.
            // Sampling/averaging them as line observations would change their meaning.
            for (var i = 0; i < Series.Count; i++)
            {
                if (Series[i].Kind is ChartSeriesKind.Heatmap or ChartSeriesKind.Treemap or
                    ChartSeriesKind.Sunburst or ChartSeriesKind.Gauge)
                {
                    return ChartDownsampleMode.None;
                }
            }

            if (requested == ChartDownsampleMode.Adaptive)
            {
                return ResolveAdaptiveDownsampleMode();
            }

            return requested;
        }

        private ChartDownsampleMode ResolveAdaptiveDownsampleMode()
        {
            if (Series.Count == 0)
            {
                return ChartDownsampleMode.None;
            }

            var hasLineLike = false;
            var hasCategory = false;

            for (var i = 0; i < Series.Count; i++)
            {
                switch (Series[i].Kind)
                {
                    case ChartSeriesKind.Line:
                    case ChartSeriesKind.Area:
                    case ChartSeriesKind.StackedArea:
                    case ChartSeriesKind.StackedArea100:
                    case ChartSeriesKind.Scatter:
                    case ChartSeriesKind.Bubble:
                        hasLineLike = true;
                        break;
                    case ChartSeriesKind.Column:
                    case ChartSeriesKind.Bar:
                    case ChartSeriesKind.StackedColumn:
                    case ChartSeriesKind.StackedBar:
                    case ChartSeriesKind.StackedColumn100:
                    case ChartSeriesKind.StackedBar100:
                    case ChartSeriesKind.Waterfall:
                    case ChartSeriesKind.Histogram:
                    case ChartSeriesKind.Pareto:
                        hasCategory = true;
                        break;
                }
            }

            if (hasLineLike)
            {
                return ChartDownsampleMode.Lttb;
            }

            if (hasCategory)
            {
                return ChartDownsampleMode.Bucket;
            }

            return ChartDownsampleMode.None;
        }

        private void ApplyDownsample(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            List<List<double?>?>? seriesSizeValues,
            ChartDownsampleMode mode,
            int maxPoints)
        {
            if (maxPoints <= 0 || categories.Count <= maxPoints)
            {
                return;
            }

            switch (mode)
            {
                case ChartDownsampleMode.Bucket:
                    ApplyBucketDownsample(categories, seriesValues, seriesXValues, seriesSizeValues, maxPoints);
                    return;
                case ChartDownsampleMode.MinMax:
                    ApplyMinMaxDownsample(categories, seriesValues, seriesXValues, seriesSizeValues, maxPoints);
                    return;
                case ChartDownsampleMode.Lttb:
                    ApplyLttbDownsample(categories, seriesValues, seriesXValues, seriesSizeValues, maxPoints);
                    return;
                case ChartDownsampleMode.Adaptive:
                case ChartDownsampleMode.None:
                default:
                    return;
            }
        }

        private void ApplyBucketDownsample(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            List<List<double?>?>? seriesSizeValues,
            int maxPoints)
        {
            var bucketSize = (int)Math.Ceiling(categories.Count / (double)maxPoints);
            if (bucketSize <= 1)
            {
                return;
            }

            var bucketCount = (int)Math.Ceiling(categories.Count / (double)bucketSize);
            var downsampledCategories = ChartListPool<string?>.Rent(bucketCount);
            var downsampledSeries = ChartListPool<List<double?>>.Rent(seriesValues.Count);
            List<List<double>?>? downsampledXValues = null;
            List<List<double?>?>? downsampledSizeValues = null;

            try
            {
                for (var i = 0; i < seriesValues.Count; i++)
                {
                    downsampledSeries.Add(ChartListPool<double?>.Rent(bucketCount));
                }

                if (seriesXValues != null)
                {
                    downsampledXValues = ChartListPool<List<double>?>.Rent(seriesValues.Count);
                    for (var i = 0; i < seriesValues.Count; i++)
                    {
                        downsampledXValues.Add(seriesXValues[i] != null ? ChartListPool<double>.Rent(bucketCount) : null);
                    }
                }

                if (seriesSizeValues != null)
                {
                    downsampledSizeValues = ChartListPool<List<double?>?>.Rent(seriesValues.Count);
                    for (var i = 0; i < seriesValues.Count; i++)
                    {
                        downsampledSizeValues.Add(seriesSizeValues[i] != null ? ChartListPool<double?>.Rent(bucketCount) : null);
                    }
                }

                for (var start = 0; start < categories.Count; start += bucketSize)
                {
                    var end = Math.Min(categories.Count, start + bucketSize);
                    downsampledCategories.Add(categories[start]);

                    for (var seriesIndex = 0; seriesIndex < seriesValues.Count; seriesIndex++)
                    {
                        var aggregator = new SeriesAggregator(_downsampleAggregation);
                        SeriesAggregator? xAggregator = null;
                        SeriesAggregator? sizeAggregator = null;
                        if (downsampledXValues != null && downsampledXValues[seriesIndex] != null)
                        {
                            xAggregator = new SeriesAggregator(Series[seriesIndex].XAggregation);
                        }

                        if (downsampledSizeValues != null && downsampledSizeValues[seriesIndex] != null)
                        {
                            sizeAggregator = new SeriesAggregator(Series[seriesIndex].SizeAggregation);
                        }

                        for (var i = start; i < end; i++)
                        {
                            aggregator.Add(seriesValues[seriesIndex][i]);
                            if (xAggregator != null)
                            {
                                var xValue = seriesXValues![seriesIndex]![i];
                                xAggregator.Add(NormalizeXValue(xValue));
                            }

                            if (sizeAggregator != null)
                            {
                                var sizeValue = seriesSizeValues![seriesIndex]![i];
                                sizeAggregator.Add(sizeValue);
                            }
                        }

                        downsampledSeries[seriesIndex].Add(aggregator.GetValue());
                        if (xAggregator != null)
                        {
                            downsampledXValues![seriesIndex]!.Add(CoerceXValue(xAggregator.GetValue()));
                        }

                        if (sizeAggregator != null)
                        {
                            downsampledSizeValues![seriesIndex]!.Add(sizeAggregator.GetValue());
                        }
                    }
                }

                categories.Clear();
                categories.AddRange(downsampledCategories);

                for (var i = 0; i < seriesValues.Count; i++)
                {
                    seriesValues[i].Clear();
                    seriesValues[i].AddRange(downsampledSeries[i]);
                }

                if (seriesXValues != null && downsampledXValues != null)
                {
                    for (var i = 0; i < seriesXValues.Count; i++)
                    {
                        if (seriesXValues[i] == null || downsampledXValues[i] == null)
                        {
                            continue;
                        }

                        seriesXValues[i]!.Clear();
                        seriesXValues[i]!.AddRange(downsampledXValues[i]!);
                    }
                }

                if (seriesSizeValues != null && downsampledSizeValues != null)
                {
                    for (var i = 0; i < seriesSizeValues.Count; i++)
                    {
                        if (seriesSizeValues[i] == null || downsampledSizeValues[i] == null)
                        {
                            continue;
                        }

                        seriesSizeValues[i]!.Clear();
                        seriesSizeValues[i]!.AddRange(downsampledSizeValues[i]!);
                    }
                }
            }
            finally
            {
                if (downsampledXValues != null)
                {
                    for (var i = 0; i < downsampledXValues.Count; i++)
                    {
                        var list = downsampledXValues[i];
                        if (list != null)
                        {
                            ChartListPool<double>.Return(list);
                        }
                    }

                    ChartListPool<List<double>?>.Return(downsampledXValues);
                }

                if (downsampledSizeValues != null)
                {
                    for (var i = 0; i < downsampledSizeValues.Count; i++)
                    {
                        var list = downsampledSizeValues[i];
                        if (list != null)
                        {
                            ChartListPool<double?>.Return(list);
                        }
                    }

                    ChartListPool<List<double?>?>.Return(downsampledSizeValues);
                }

                for (var i = 0; i < downsampledSeries.Count; i++)
                {
                    ChartListPool<double?>.Return(downsampledSeries[i]);
                }

                ChartListPool<List<double?>>.Return(downsampledSeries);
                ChartListPool<string?>.Return(downsampledCategories);
            }
        }

        private void ApplyMinMaxDownsample(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            List<List<double?>?>? seriesSizeValues,
            int maxPoints)
        {
            var count = categories.Count;
            if (count <= maxPoints)
            {
                return;
            }

            var target = Math.Max(2, maxPoints);
            var bucketCount = Math.Max(1, target / 2);
            var bucketSize = count / (double)bucketCount;
            var indices = ChartListPool<int>.Rent(target);

            try
            {
                for (var bucket = 0; bucket < bucketCount; bucket++)
                {
                    var start = (int)Math.Floor(bucket * bucketSize);
                    var end = (int)Math.Floor((bucket + 1) * bucketSize);
                    if (end <= start)
                    {
                        end = Math.Min(start + 1, count);
                    }

                    var minIndex = start;
                    var maxIndex = start;
                    var minValue = double.PositiveInfinity;
                    var maxValue = double.NegativeInfinity;

                    for (var i = start; i < end; i++)
                    {
                        var value = GetCompositeValue(seriesValues, i);
                        var numeric = value ?? 0d;
                        if (numeric < minValue)
                        {
                            minValue = numeric;
                            minIndex = i;
                        }

                        if (numeric > maxValue)
                        {
                            maxValue = numeric;
                            maxIndex = i;
                        }
                    }

                    if (minIndex <= maxIndex)
                    {
                        AppendIndex(indices, minIndex);
                        AppendIndex(indices, maxIndex);
                    }
                    else
                    {
                        AppendIndex(indices, maxIndex);
                        AppendIndex(indices, minIndex);
                    }
                }

                EnsureBoundaryIndices(indices, count);
                TrimIndices(indices, maxPoints);
                ApplyIndexSelection(categories, seriesValues, seriesXValues, seriesSizeValues, indices);
            }
            finally
            {
                ChartListPool<int>.Return(indices);
            }
        }

        private void ApplyLttbDownsample(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            List<List<double?>?>? seriesSizeValues,
            int maxPoints)
        {
            var count = categories.Count;
            if (count <= maxPoints)
            {
                return;
            }

            var indices = BuildLttbIndices(seriesValues, seriesXValues, count, maxPoints);
            try
            {
                ApplyIndexSelection(categories, seriesValues, seriesXValues, seriesSizeValues, indices);
            }
            finally
            {
                ChartListPool<int>.Return(indices);
            }
        }

        private List<int> BuildLttbIndices(
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            int count,
            int maxPoints)
        {
            var threshold = Math.Max(2, maxPoints);
            if (threshold >= count)
            {
                return BuildSequentialIndices(count);
            }

            var pool = ArrayPool<double>.Shared;
            var xBuffer = pool.Rent(count);
            var yBuffer = pool.Rent(count);
            try
            {
                var xSource = FindXValueSource(seriesXValues, count);
                for (var i = 0; i < count; i++)
                {
                    xBuffer[i] = xSource != null ? xSource[i] : i;
                    var value = GetCompositeValue(seriesValues, i);
                    yBuffer[i] = value ?? 0d;
                }

                return LttbSelectIndices(xBuffer, yBuffer, count, threshold);
            }
            finally
            {
                pool.Return(xBuffer);
                pool.Return(yBuffer);
            }
        }

        private static IReadOnlyList<double>? FindXValueSource(List<List<double>?>? seriesXValues, int count)
        {
            if (seriesXValues == null)
            {
                return null;
            }

            for (var i = 0; i < seriesXValues.Count; i++)
            {
                var values = seriesXValues[i];
                if (values != null && values.Count == count)
                {
                    return values;
                }
            }

            return null;
        }

        private static List<int> LttbSelectIndices(double[] xValues, double[] yValues, int count, int threshold)
        {
            if (threshold <= 1 || count == 0)
            {
                var indices = ChartListPool<int>.Rent(1);
                if (count > 0)
                {
                    indices.Add(0);
                }

                return indices;
            }

            if (threshold >= count)
            {
                return BuildSequentialIndices(count);
            }

            var pool = ArrayPool<int>.Shared;
            var sampled = pool.Rent(threshold);
            sampled[0] = 0;
            sampled[threshold - 1] = count - 1;

            try
            {
                var every = (count - 2d) / (threshold - 2);
                var a = 0;
                for (var i = 0; i < threshold - 2; i++)
                {
                    var rangeStart = (int)Math.Floor((i + 0) * every) + 1;
                    var rangeEnd = (int)Math.Floor((i + 1) * every) + 1;
                    var rangeNextEnd = (int)Math.Floor((i + 2) * every) + 1;

                    if (rangeEnd >= count)
                    {
                        rangeEnd = count - 1;
                    }

                    if (rangeNextEnd > count)
                    {
                        rangeNextEnd = count;
                    }

                    double avgX = 0d;
                    double avgY = 0d;
                    var avgCount = 0;
                    for (var j = rangeEnd; j < rangeNextEnd; j++)
                    {
                        avgX += xValues[j];
                        avgY += yValues[j];
                        avgCount++;
                    }

                    if (avgCount > 0)
                    {
                        avgX /= avgCount;
                        avgY /= avgCount;
                    }
                    else
                    {
                        avgX = xValues[a];
                        avgY = yValues[a];
                    }

                    double maxArea = -1d;
                    var nextA = rangeStart;
                    for (var j = rangeStart; j < rangeEnd; j++)
                    {
                        var area = Math.Abs(
                            (xValues[a] - avgX) * (yValues[j] - yValues[a]) -
                            (xValues[a] - xValues[j]) * (avgY - yValues[a])) * 0.5d;

                        if (area > maxArea)
                        {
                            maxArea = area;
                            nextA = j;
                        }
                    }

                    sampled[i + 1] = nextA;
                    a = nextA;
                }

                var indices = ChartListPool<int>.Rent(threshold);
                for (var i = 0; i < threshold; i++)
                {
                    indices.Add(sampled[i]);
                }

                return indices;
            }
            finally
            {
                pool.Return(sampled);
            }
        }

        private static void ApplyIndexSelection(
            List<string?> categories,
            List<List<double?>> seriesValues,
            List<List<double>?>? seriesXValues,
            List<List<double?>?>? seriesSizeValues,
            IReadOnlyList<int> indices)
        {
            var newCategories = ChartListPool<string?>.Rent(indices.Count);
            try
            {
                for (var i = 0; i < indices.Count; i++)
                {
                    var index = indices[i];
                    if (index < 0 || index >= categories.Count)
                    {
                        continue;
                    }

                    newCategories.Add(categories[index]);
                }

                categories.Clear();
                categories.AddRange(newCategories);
            }
            finally
            {
                ChartListPool<string?>.Return(newCategories);
            }

            for (var seriesIndex = 0; seriesIndex < seriesValues.Count; seriesIndex++)
            {
                var values = seriesValues[seriesIndex];
                var newValues = ChartListPool<double?>.Rent(indices.Count);
                try
                {
                    for (var i = 0; i < indices.Count; i++)
                    {
                        var index = indices[i];
                        if (index < 0 || index >= values.Count)
                        {
                            continue;
                        }

                        newValues.Add(values[index]);
                    }

                    values.Clear();
                    values.AddRange(newValues);
                }
                finally
                {
                    ChartListPool<double?>.Return(newValues);
                }

                if (seriesXValues != null && seriesXValues[seriesIndex] != null)
                {
                    var xValues = seriesXValues[seriesIndex]!;
                    var newXValues = ChartListPool<double>.Rent(indices.Count);
                    try
                    {
                        for (var i = 0; i < indices.Count; i++)
                        {
                            var index = indices[i];
                            if (index < 0 || index >= xValues.Count)
                            {
                                continue;
                            }

                            newXValues.Add(xValues[index]);
                        }

                        xValues.Clear();
                        xValues.AddRange(newXValues);
                    }
                    finally
                    {
                        ChartListPool<double>.Return(newXValues);
                    }
                }

                if (seriesSizeValues != null && seriesSizeValues[seriesIndex] != null)
                {
                    var sizeValues = seriesSizeValues[seriesIndex]!;
                    var newSizeValues = ChartListPool<double?>.Rent(indices.Count);
                    try
                    {
                        for (var i = 0; i < indices.Count; i++)
                        {
                            var index = indices[i];
                            if (index < 0 || index >= sizeValues.Count)
                            {
                                continue;
                            }

                            newSizeValues.Add(sizeValues[index]);
                        }

                        sizeValues.Clear();
                        sizeValues.AddRange(newSizeValues);
                    }
                    finally
                    {
                        ChartListPool<double?>.Return(newSizeValues);
                    }
                }
            }
        }

        private static double? GetCompositeValue(List<List<double?>> seriesValues, int index)
        {
            double? selected = null;
            var maxAbs = 0d;
            for (var i = 0; i < seriesValues.Count; i++)
            {
                var values = seriesValues[i];
                if (index < 0 || index >= values.Count)
                {
                    continue;
                }

                var value = values[index];
                if (!value.HasValue)
                {
                    continue;
                }

                var abs = Math.Abs(value.Value);
                if (selected == null || abs > maxAbs)
                {
                    selected = value.Value;
                    maxAbs = abs;
                }
            }

            return selected;
        }

        private static List<int> BuildSequentialIndices(int count)
        {
            var indices = ChartListPool<int>.Rent(count);
            for (var i = 0; i < count; i++)
            {
                indices.Add(i);
            }

            return indices;
        }

        private static void AppendIndex(List<int> indices, int index)
        {
            if (indices.Count == 0 || indices[indices.Count - 1] != index)
            {
                indices.Add(index);
            }
        }

        private static void EnsureBoundaryIndices(List<int> indices, int count)
        {
            if (count == 0)
            {
                return;
            }

            if (indices.Count == 0 || indices[0] != 0)
            {
                indices.Insert(0, 0);
            }

            var lastIndex = count - 1;
            if (indices[indices.Count - 1] != lastIndex)
            {
                indices.Add(lastIndex);
            }
        }

        private static void TrimIndices(List<int> indices, int maxPoints)
        {
            if (indices.Count <= maxPoints)
            {
                return;
            }

            var trimmed = ChartListPool<int>.Rent(maxPoints);
            try
            {
                var step = indices.Count / (double)maxPoints;
                for (var i = 0; i < maxPoints; i++)
                {
                    var index = (int)Math.Floor(i * step);
                    if (index < 0)
                    {
                        index = 0;
                    }

                    if (index >= indices.Count)
                    {
                        index = indices.Count - 1;
                    }

                    trimmed.Add(indices[index]);
                }

                indices.Clear();
                indices.AddRange(trimmed);
            }
            finally
            {
                ChartListPool<int>.Return(trimmed);
            }
        }

        private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                _formulaStates.Clear();
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DataGridChartSeriesDefinition definition)
                    {
                        WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridChartModel>(
                            definition,
                            nameof(INotifyPropertyChanged.PropertyChanged),
                            OnSeriesDefinitionPropertyChanged);
                        _formulaStates.Remove(definition);
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DataGridChartSeriesDefinition definition)
                    {
                        WeakEventHandlerManager.Subscribe<DataGridChartSeriesDefinition, PropertyChangedEventArgs, DataGridChartModel>(
                            definition,
                            nameof(INotifyPropertyChanged.PropertyChanged),
                            OnSeriesDefinitionPropertyChanged);
                    }
                }
            }

            InvalidateCache(ChartDataDelta.Full);
            RequestRefresh();
        }

        private void OnSeriesDefinitionPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is DataGridChartSeriesDefinition definition)
            {
                _formulaStates.Remove(definition);
            }

            InvalidateCache(ChartDataDelta.Full);
            RequestRefresh();
        }

        private void UpdateCollectionSource()
        {
            var nextSource = _view as INotifyCollectionChanged ?? _itemsSource as INotifyCollectionChanged;
            var changed = !ReferenceEquals(_collectionSource, nextSource);
            if (changed)
            {
                ClearCollectionSource();
                _collectionSource = nextSource;
                if (_collectionSource != null)
                {
                    WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridChartModel>(
                        _collectionSource,
                        nameof(INotifyCollectionChanged.CollectionChanged),
                        OnCollectionChanged);
                }
            }

            UpdateGroupCollectionSource();
            ResetItemTracking();
        }

        private void ClearCollectionSource()
        {
            if (_collectionSource == null)
            {
                ClearGroupCollectionSource();
                return;
            }

            WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridChartModel>(
                _collectionSource,
                nameof(INotifyCollectionChanged.CollectionChanged),
                OnCollectionChanged);
            _collectionSource = null;
            ClearGroupCollectionSource();
        }

        private void UpdateGroupCollectionSource()
        {
            var nextSource = IsGroupedModeActive() ? _view?.Groups as INotifyCollectionChanged : null;
            if (ReferenceEquals(_groupCollectionSource, nextSource))
            {
                return;
            }

            ClearGroupCollectionSource();
            _groupCollectionSource = nextSource;
            if (_groupCollectionSource != null)
            {
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridChartModel>(
                    _groupCollectionSource,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    OnGroupCollectionChanged);
            }
        }

        private void ClearGroupCollectionSource()
        {
            if (_groupCollectionSource == null)
            {
                return;
            }

            WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridChartModel>(
                _groupCollectionSource,
                nameof(INotifyCollectionChanged.CollectionChanged),
                OnGroupCollectionChanged);
            _groupCollectionSource = null;
        }

        private bool IsGroupedModeActive()
        {
            var view = _view;
            return _groupMode == DataGridChartGroupMode.TopLevel &&
                   view != null &&
                   view.IsGrouping &&
                   view.Groups != null;
        }

        private bool ShouldTrackItemChanges()
        {
            if (!_useIncrementalUpdates || !CanUseIncremental())
            {
                return false;
            }

            if (_view != null)
            {
                if (_view.Filter != null)
                {
                    return false;
                }

                if (_view.SortDescriptions.Count > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void ResetItemTracking()
        {
            ClearItemTracking();
            if (!ShouldTrackItemChanges())
            {
                return;
            }

            foreach (var item in EnumerateItems())
            {
                AttachItem(item);
            }
        }

        private void ClearItemTracking()
        {
            foreach (var tracked in _trackedItems)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridChartModel>(
                    tracked,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnItemPropertyChanged);
            }

            _trackedItems.Clear();
        }

        private void UpdateItemTracking(NotifyCollectionChangedEventArgs e)
        {
            if (!ShouldTrackItemChanges())
            {
                ClearItemTracking();
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ResetItemTracking();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is object)
                    {
                        DetachItem(item);
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is object)
                    {
                        AttachItem(item);
                    }
                }
            }
        }

        private void AttachItem(object item)
        {
            if (item is INotifyPropertyChanged inpc && _trackedItems.Add(inpc))
            {
                WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, DataGridChartModel>(
                    inpc,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnItemPropertyChanged);
            }
        }

        private void DetachItem(object item)
        {
            if (item is INotifyPropertyChanged inpc && _trackedItems.Remove(inpc))
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridChartModel>(
                    inpc,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    OnItemPropertyChanged);
            }
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_useIncrementalUpdates || !ShouldTrackItemChanges())
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            if (_activeCache == null || sender is not object item || IsIgnoredItem(item))
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            if (IsGroupedModeActive())
            {
                var pendingCache = EnsurePendingCache();
                if (!TryUpdateGroupedItem(pendingCache, item))
                {
                    InvalidateCache(ChartDataDelta.Full);
                    RequestRefresh();
                    return;
                }

                RequestRefresh();
                return;
            }

            if (!TryGetItemIndex(item, out var index))
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            var cache = EnsurePendingCache();
            if (!TryUpdateItem(cache, index, item))
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            QueueItemUpdate(index);
            RequestRefresh();
        }

        private bool TryGetItemIndex(object item, out int index)
        {
            if (_view is IList viewList)
            {
                index = viewList.IndexOf(item);
                return index >= 0;
            }

            if (_itemsSource is IList list)
            {
                index = list.IndexOf(item);
                return index >= 0;
            }

            index = -1;
            return false;
        }

        private void QueueItemUpdate(int index)
        {
            var delta = new ChartDataDelta(ChartDataDeltaKind.Update, index, 1, 1, BuildSeriesIndices());
            if (_pendingDelta == null)
            {
                _pendingDelta = delta;
                return;
            }

            if (TryMergeDelta(_pendingDelta, delta, out var merged))
            {
                _pendingDelta = merged;
                return;
            }

            _pendingDelta = ChartDataDelta.Full;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateGroupCollectionSource();
            UpdateItemTracking(e);
            QueueCollectionChange(e);
            RequestRefresh();
        }

        private void OnGroupCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_useIncrementalUpdates)
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            if (!IsGroupedModeActive() || _activeCache == null)
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                InvalidateCache(new ChartDataDelta(ChartDataDeltaKind.Reset));
                RequestRefresh();
                return;
            }

            if (!TryApplyGroupCollectionChange(e, out var delta))
            {
                InvalidateCache(ChartDataDelta.Full);
                RequestRefresh();
                return;
            }

            if (_pendingDelta == null)
            {
                _pendingDelta = delta;
            }
            else if (TryMergeDelta(_pendingDelta, delta, out var merged))
            {
                _pendingDelta = merged;
            }
            else
            {
                _pendingDelta = ChartDataDelta.Full;
            }

            RequestRefresh();
        }

        private void RequestRefresh()
        {
            if (!_autoRefresh)
            {
                return;
            }

            DataInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private readonly struct GroupMatch
        {
            public GroupMatch(int index, DataGridCollectionViewGroup group)
            {
                Index = index;
                Group = group;
            }

            public int Index { get; }

            public DataGridCollectionViewGroup Group { get; }
        }

        private sealed class ChartDataCache
        {
            public ChartDataCache(
                List<string?> categories,
                List<List<double?>> seriesValues,
                List<List<double>?> seriesXValues,
                List<List<double?>?> seriesSizeValues,
                bool hasXValues,
                bool hasSizeValues)
            {
                Categories = categories;
                SeriesValues = seriesValues;
                SeriesXValues = seriesXValues;
                SeriesSizeValues = seriesSizeValues;
                HasXValues = hasXValues;
                HasSizeValues = hasSizeValues;
            }

            public List<string?> Categories { get; }

            public List<List<double?>> SeriesValues { get; }

            public List<List<double>?> SeriesXValues { get; }

            public List<List<double?>?> SeriesSizeValues { get; }

            public bool HasXValues { get; }

            public bool HasSizeValues { get; }

            public ChartDataCache Clone() => Clone(null, null);

            public ChartDataCache Clone(int? windowStart, int? windowCount)
            {
                var start = Math.Clamp(windowStart ?? 0, 0, Categories.Count);
                var count = Math.Clamp(windowCount ?? (Categories.Count - start), 0, Categories.Count - start);
                var categories = Categories.GetRange(start, count);
                var seriesValues = new List<List<double?>>(SeriesValues.Count);
                var seriesXValues = new List<List<double>?>(SeriesXValues.Count);
                var seriesSizeValues = new List<List<double?>?>(SeriesSizeValues.Count);

                for (var i = 0; i < SeriesValues.Count; i++)
                {
                    seriesValues.Add(SeriesValues[i].GetRange(start, count));
                }

                for (var i = 0; i < SeriesXValues.Count; i++)
                {
                    seriesXValues.Add(SeriesXValues[i] != null ? SeriesXValues[i]!.GetRange(start, count) : null);
                }

                for (var i = 0; i < SeriesSizeValues.Count; i++)
                {
                    seriesSizeValues.Add(SeriesSizeValues[i] != null ? SeriesSizeValues[i]!.GetRange(start, count) : null);
                }

                return new ChartDataCache(categories, seriesValues, seriesXValues, seriesSizeValues, HasXValues, HasSizeValues);
            }
        }

        private sealed class SeriesAggregator
        {
            private readonly DataGridChartAggregation _aggregation;
            private double _sum;
            private int _count;
            private double _min;
            private double _max;
            private double? _first;
            private double? _last;
            private bool _hasValue;

            public SeriesAggregator(DataGridChartAggregation aggregation)
            {
                _aggregation = aggregation;
            }

            public void Add(double? value)
            {
                if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
                {
                    return;
                }

                if (!_hasValue)
                {
                    _min = value.Value;
                    _max = value.Value;
                    _first = value;
                    _hasValue = true;
                }

                _last = value;
                _sum += value.Value;
                _count++;
                _min = Math.Min(_min, value.Value);
                _max = Math.Max(_max, value.Value);
            }

            public double? GetValue()
            {
                if (_aggregation == DataGridChartAggregation.Count)
                {
                    return _count;
                }

                if (!_hasValue)
                {
                    return null;
                }

                return _aggregation switch
                {
                    DataGridChartAggregation.Sum => _sum,
                    DataGridChartAggregation.Average => _count == 0 ? null : _sum / _count,
                    DataGridChartAggregation.Min => _min,
                    DataGridChartAggregation.Max => _max,
                    DataGridChartAggregation.First => _first,
                    DataGridChartAggregation.Last => _last,
                    _ => _sum
                };
            }
        }

        private sealed class SeriesFormulaState
        {
            public string? FormulaText { get; set; }

            public FormulaExpression? Expression { get; set; }

            public bool HasError { get; set; }
        }

        private sealed class FormulaChartWorkbook : IFormulaWorkbook
        {
            private readonly List<IFormulaWorksheet> _worksheets = new();

            public FormulaChartWorkbook(string name, FormulaCalculationSettings settings)
            {
                Name = name;
                Settings = settings;
            }

            public string Name { get; }

            public IReadOnlyList<IFormulaWorksheet> Worksheets => _worksheets;

            public FormulaCalculationSettings Settings { get; }

            public void AddWorksheet(IFormulaWorksheet worksheet)
            {
                if (worksheet == null)
                {
                    throw new ArgumentNullException(nameof(worksheet));
                }

                _worksheets.Add(worksheet);
            }

            public IFormulaWorksheet GetWorksheet(string name)
            {
                for (var i = 0; i < _worksheets.Count; i++)
                {
                    if (string.Equals(_worksheets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return _worksheets[i];
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        private sealed class FormulaChartWorksheet : IFormulaWorksheet
        {
            public FormulaChartWorksheet(string name, IFormulaWorkbook workbook)
            {
                Name = name;
                Workbook = workbook;
            }

            public string Name { get; }

            public IFormulaWorkbook Workbook { get; }

            public IFormulaCell GetCell(int row, int column)
            {
                return new FormulaChartCell(new FormulaCellAddress(Name, row, column));
            }

            public bool TryGetCell(int row, int column, out IFormulaCell cell)
            {
                cell = new FormulaChartCell(new FormulaCellAddress(Name, row, column));
                return true;
            }
        }

        private sealed class FormulaChartCell : IFormulaCell
        {
            public FormulaChartCell(FormulaCellAddress address)
            {
                Address = address;
                Value = FormulaValue.Blank;
            }

            public FormulaCellAddress Address { get; }

            public string? Formula { get; set; }

            public FormulaExpression? Expression { get; set; }

            public FormulaValue Value { get; set; }
        }

        private sealed class DataGridChartFormulaResolver : IFormulaValueResolver, IFormulaStructuredReferenceResolver
        {
            private readonly DataGridChartModel _owner;
            private object? _item;

            public DataGridChartFormulaResolver(DataGridChartModel owner)
            {
                _owner = owner;
            }

            public void SetItem(object item)
            {
                _item = item;
            }

            public bool TryResolveName(FormulaEvaluationContext context, string name, out FormulaValue value)
            {
                if (_item != null && TryGetItemValue(_item, name, out var raw))
                {
                    value = _owner.ConvertToFormulaValue(raw);
                    return true;
                }

                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
                return false;
            }

            public bool TryResolveReference(FormulaEvaluationContext context, FormulaReference reference, out FormulaValue value)
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            public bool TryResolveStructuredReference(
                FormulaEvaluationContext context,
                FormulaStructuredReference reference,
                out FormulaValue value)
            {
                if (_item == null)
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
                    return false;
                }

                if (reference.Scope != FormulaStructuredReferenceScope.None &&
                    reference.Scope != FormulaStructuredReferenceScope.Data &&
                    reference.Scope != FormulaStructuredReferenceScope.ThisRow)
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    return true;
                }

                if (string.IsNullOrWhiteSpace(reference.ColumnStart))
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
                    return false;
                }

                if (reference.IsColumnRange)
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    return true;
                }

                if (TryGetItemValue(_item, reference.ColumnStart!, out var raw))
                {
                    value = _owner.ConvertToFormulaValue(raw);
                    return true;
                }

                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Name));
                return false;
            }
        }

        private static class PropertyPathAccessor
        {
            private static readonly Dictionary<AccessorKey, AccessorEntry> Accessors = new();
            private static readonly object AccessorLock = new();

            public static object? GetValue(object target, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var accessor = GetAccessor(target.GetType(), path);
                return accessor(target).Value;
            }

            public static bool TryGetValue(object target, string path, out object? value)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    value = null;
                    return false;
                }

                var accessor = GetAccessor(target.GetType(), path);
                var result = accessor(target);
                value = result.Value;
                return result.Found;
            }

            private static Func<object, AccessorResult> GetAccessor(Type type, string path)
            {
                var key = new AccessorKey(type, path);
                lock (AccessorLock)
                {
                    if (Accessors.TryGetValue(key, out var accessor))
                    {
                        return accessor.Accessor;
                    }

                    var entry = CreateAccessor(path);
                    Accessors[key] = entry;
                    return entry.Accessor;
                }
            }

            private static AccessorEntry CreateAccessor(string path)
            {
                var parts = path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                return new AccessorEntry(target =>
                {
                    object? current = target;
                    for (var i = 0; i < parts.Length; i++)
                    {
                        if (current == null)
                        {
                            return new AccessorResult(true, null);
                        }

                        var part = parts[i];
                        var type = current.GetType();
                        var property = type.GetProperty(part);
                        if (property != null)
                        {
                            current = property.GetValue(current);
                            continue;
                        }

                        var field = type.GetField(part);
                        if (field != null)
                        {
                            current = field.GetValue(current);
                            continue;
                        }

                        return new AccessorResult(false, null);
                    }

                    return new AccessorResult(true, current);
                });
            }

            private sealed class AccessorEntry
            {
                public AccessorEntry(Func<object, AccessorResult> accessor)
                {
                    Accessor = accessor;
                }

                public Func<object, AccessorResult> Accessor { get; }
            }

            private readonly struct AccessorResult
            {
                public AccessorResult(bool found, object? value)
                {
                    Found = found;
                    Value = value;
                }

                public bool Found { get; }

                public object? Value { get; }
            }

            private readonly struct AccessorKey : IEquatable<AccessorKey>
            {
                public AccessorKey(Type type, string path)
                {
                    Type = type;
                    Path = path;
                }

                public Type Type { get; }

                public string Path { get; }

                public bool Equals(AccessorKey other)
                {
                    return Type == other.Type && string.Equals(Path, other.Path, StringComparison.Ordinal);
                }

                public override bool Equals(object? obj)
                {
                    return obj is AccessorKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hash = 17;
                        hash = (hash * 31) + Type.GetHashCode();
                        hash = (hash * 31) + Path.GetHashCode();
                        return hash;
                    }
                }
            }
        }
    }
}
