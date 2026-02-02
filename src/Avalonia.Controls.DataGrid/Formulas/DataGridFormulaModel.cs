// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.Utilities;
using ProDataGrid.FormulaEngine;
using ProDataGrid.FormulaEngine.Excel;

namespace Avalonia.Controls.DataGridFormulas
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridFormulaInvalidatedEventArgs : EventArgs
    {
        public DataGridFormulaInvalidatedEventArgs(
            IReadOnlyList<object>? items = null,
            bool requiresRefresh = false)
        {
            Items = items;
            RequiresRefresh = requiresRefresh;
        }

        public IReadOnlyList<object>? Items { get; }

        public bool RequiresRefresh { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridFormulaModel
    {
        event EventHandler<DataGridFormulaInvalidatedEventArgs>? Invalidated;

        int FormulaVersion { get; }

        void Attach(DataGrid grid);

        void Detach();

        object? Evaluate(object item, DataGridFormulaColumnDefinition column);

        void Invalidate();

        FormulaCalculationMode CalculationMode { get; set; }

        void Recalculate();

        string? GetCellFormula(object item, DataGridFormulaColumnDefinition column);

        bool TrySetCellFormula(object item, DataGridFormulaColumnDefinition column, string? formulaText, out string? error);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridFormulaModel : IDataGridFormulaModel, IDisposable, INotifyPropertyChanged
    {
        private const string DefaultSheetName = "Sheet1";
        private const string DefaultWorkbookName = "DataGrid";

        private readonly FormulaCalculationSettings _settings;
        private readonly ExcelFormulaParser _parser;
        private readonly ExcelFunctionRegistry _functions;
        private readonly ExcelFormulaFormatter _formatter;
        private readonly FormulaCalculationEngine _engine;
        private readonly DataGridFormulaWorkbook _workbook;
        private readonly DataGridFormulaWorksheet _worksheet;
        private readonly Dictionary<FormulaCellAddress, DataGridFormulaCell> _formulaCells = new();
        private readonly Dictionary<FormulaCellAddress, FormulaValue> _spillValues = new();
        private readonly Dictionary<string, FormulaExpression> _nameExpressions =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NamedRangeEntry> _namedRanges =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IDataGridColumnValueAccessor> _accessors =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataGridFormulaColumnDefinition> _formulaColumns =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ColumnIndexEntry> _columnIndexMap = new();
        private readonly Dictionary<string, int> _columnIndexByName =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _columnIndexByProperty =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<DataGridFormulaColumnDefinition, int> _formulaColumnIndexMap = new();
        private readonly Dictionary<DataGridColumn, string?> _columnNameByColumn = new();
        private readonly Dictionary<INotifyPropertyChanged, int> _itemSubscriptionCounts = new();
        private readonly HashSet<FormulaCellAddress> _dirtyCells = new();
        private readonly HashSet<object> _dirtyItems = new();
        private bool _columnsDirty = true;
        private bool _formulasDirty = true;
        private bool _invalidatePending;
        private bool _requiresRefresh;
        private bool _suppressDefinitionUpdates;
        private DataGrid? _grid;
        private INotifyCollectionChanged? _columnsCollection;
        private IDisposable? _itemsSourceSubscription;
        private INotifyCollectionChanged? _collectionChanged;
        private IDisposable? _nameSubscription;
        private int _formulaVersion;
        private string _sheetName = DefaultSheetName;
        private string _workbookName = DefaultWorkbookName;

        public DataGridFormulaModel(
            FormulaCalculationSettings? settings = null,
            ExcelFormulaParser? parser = null,
            ExcelFunctionRegistry? functions = null)
        {
            _settings = settings ?? new FormulaCalculationSettings
            {
                ReferenceMode = FormulaReferenceMode.A1,
                Culture = CultureInfo.CurrentCulture,
                DateSystem = FormulaDateSystem.Windows1900
            };
            _parser = parser ?? new ExcelFormulaParser();
            _functions = functions ?? new ExcelFunctionRegistry();
            _formatter = new ExcelFormulaFormatter();
            _engine = new FormulaCalculationEngine(_parser, _functions);
            _workbook = new DataGridFormulaWorkbook(this);
            _worksheet = new DataGridFormulaWorksheet(this, _workbook);
            _workbook.AddWorksheet(_worksheet);
        }

        public event EventHandler<DataGridFormulaInvalidatedEventArgs>? Invalidated;

        public event PropertyChangedEventHandler? PropertyChanged;

        public FormulaCalculationMode CalculationMode
        {
            get => _settings.CalculationMode;
            set
            {
                if (_settings.CalculationMode == value)
                {
                    return;
                }

                _settings.CalculationMode = value;
                QueueRecalculate();
            }
        }

        public int FormulaVersion
        {
            get => _formulaVersion;
            private set
            {
                if (_formulaVersion == value)
                {
                    return;
                }

                _formulaVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormulaVersion)));
            }
        }

        internal bool HasFormulas => _formulaColumns.Count > 0 || _formulaCells.Count > 0 || _spillValues.Count > 0;

        public void Attach(DataGrid grid)
        {
            if (ReferenceEquals(_grid, grid))
            {
                return;
            }

            Detach();
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _columnsDirty = true;
            _formulasDirty = true;
            UpdateNames(grid.Name);

            if (grid.Columns is INotifyCollectionChanged columnsChanged)
            {
                _columnsCollection = columnsChanged;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridFormulaModel>(
                    columnsChanged,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    Columns_CollectionChanged);
            }

            grid.ColumnDisplayIndexChanged += Columns_DisplayIndexChanged;
            grid.SummaryRecalculated += Grid_SummaryRecalculated;
            grid.CellEditEnding += Grid_CellEditEnding;

            _nameSubscription = grid
                .GetObservable(StyledElement.NameProperty)
                .Subscribe(Grid_NameChanged);

            _itemsSourceSubscription = grid
                .GetObservable(DataGrid.ItemsSourceProperty)
                .Subscribe(ItemsSource_Changed);

            AttachItemsSource(grid.ItemsSource);
            QueueStructureRefresh();
        }

        public void Detach()
        {
            if (_grid == null)
            {
                return;
            }

            _grid.ColumnDisplayIndexChanged -= Columns_DisplayIndexChanged;
            _grid.SummaryRecalculated -= Grid_SummaryRecalculated;
            _grid.CellEditEnding -= Grid_CellEditEnding;

            _nameSubscription?.Dispose();
            _nameSubscription = null;

            if (_columnsCollection != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridFormulaModel>(
                    _columnsCollection,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    Columns_CollectionChanged);
                _columnsCollection = null;
            }

            _itemsSourceSubscription?.Dispose();
            _itemsSourceSubscription = null;
            DetachItemsSource();

            _grid = null;
            _formulaCells.Clear();
            _spillValues.Clear();
            _dirtyCells.Clear();
            _dirtyItems.Clear();
            _columnsDirty = true;
            _formulasDirty = true;
        }

        public void Dispose()
        {
            Detach();
        }

        public object? Evaluate(object item, DataGridFormulaColumnDefinition column)
        {
            if (item == null || column == null)
            {
                return null;
            }

            EnsureColumnMaps();
            if (!TryGetRowIndex(item, out var rowIndex))
            {
                return null;
            }

            if (!_formulaColumnIndexMap.TryGetValue(column, out var columnIndex))
            {
                return null;
            }
            var address = CreateAddress(rowIndex, columnIndex);
            var value = GetCellValue(address);
            if (value.Kind == FormulaValueKind.Array)
            {
                value = FormulaCoercion.ApplyImplicitIntersection(value, address);
            }

            return ConvertToObject(value, column.ValueType);
        }

        public void Invalidate()
        {
            if (_formulasDirty)
            {
                QueueStructureRefresh();
                return;
            }

            EnsureInitialized();
            MarkAllFormulaCellsDirty();
            QueueRecalculate();
        }

        public void Recalculate()
        {
            EnsureInitialized();
            RecalculateAll();
        }

        public string? GetCellFormula(object item, DataGridFormulaColumnDefinition column)
        {
            if (item == null || column == null)
            {
                return null;
            }

            EnsureInitialized();
            if (!TryGetRowIndex(item, out var rowIndex))
            {
                return null;
            }

            if (!_formulaColumnIndexMap.TryGetValue(column, out var columnIndex))
            {
                return null;
            }

            var address = CreateAddress(rowIndex, columnIndex);
            if (_formulaCells.TryGetValue(address, out var cell))
            {
                return cell.Formula ?? column.Formula;
            }

            return column.Formula;
        }

        public bool TrySetCellFormula(object item, DataGridFormulaColumnDefinition column, string? formulaText, out string? error)
        {
            error = null;
            if (item == null || column == null)
            {
                error = "Invalid formula target.";
                return false;
            }

            EnsureInitialized();
            if (!TryGetRowIndex(item, out var rowIndex))
            {
                error = "Row not found.";
                return false;
            }

            if (!_formulaColumnIndexMap.TryGetValue(column, out var columnIndex))
            {
                error = "Formula column not found.";
                return false;
            }

            var normalized = NormalizeFormulaText(formulaText);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                ClearFormulaCell(rowIndex, columnIndex);
                MarkDirtyCell(CreateAddress(rowIndex, columnIndex), item);
                QueueRecalculate();
                return true;
            }

            try
            {
                SetFormulaCell(rowIndex, columnIndex, normalized, isOverride: true);
                MarkDirtyCell(CreateAddress(rowIndex, columnIndex), item);
                QueueRecalculate();
                return true;
            }
            catch (FormulaParseException ex)
            {
                error = ex.Message;
                SetFormulaParseError(rowIndex, columnIndex, normalized);
                MarkDirtyCell(CreateAddress(rowIndex, columnIndex), item);
                QueueRecalculate();
                return false;
            }
        }

        public bool TrySetNamedRange(string name, string? formulaText, out string? error)
        {
            error = null;
            var trimmed = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                error = "Name is required.";
                return false;
            }

            if (!IsValidName(trimmed))
            {
                error = "Invalid name.";
                return false;
            }

            EnsureColumnMaps();
            if (_columnIndexByName.ContainsKey(trimmed))
            {
                error = "Name conflicts with a column header.";
                return false;
            }

            var normalized = NormalizeFormulaText(formulaText);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (_namedRanges.Remove(trimmed))
                {
                    BuildNameExpressions();
                    _engine.RefreshDependenciesForNames(_workbook);
                    QueueRecalculate();
                }

                return true;
            }

            try
            {
                var expression = _parser.Parse(normalized, _settings.CreateParseOptions());
                _namedRanges[trimmed] = new NamedRangeEntry(normalized, expression);
                BuildNameExpressions();
                _engine.RefreshDependenciesForNames(_workbook);
                QueueRecalculate();
                return true;
            }
            catch (FormulaParseException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryGetNamedRange(string name, out string? formulaText)
        {
            formulaText = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (_namedRanges.TryGetValue(name.Trim(), out var entry))
            {
                formulaText = entry.FormulaText;
                return true;
            }

            return false;
        }

        internal void InvalidateColumns()
        {
            _columnsDirty = true;
            QueueStructureRefresh();
        }

        internal void NotifyColumnDefinitionChanged(DataGridFormulaColumnDefinition definition, string propertyName)
        {
            if (_suppressDefinitionUpdates)
            {
                return;
            }

            switch (propertyName)
            {
                case nameof(DataGridFormulaColumnDefinition.Formula):
                    UpdateFormulaColumn(definition);
                    break;
                case nameof(DataGridFormulaColumnDefinition.FormulaName):
                    _columnsDirty = true;
                    QueueStructureRefresh();
                    break;
            }
        }

        private void ItemsSource_Changed(IEnumerable? source)
        {
            DetachItemsSource();
            AttachItemsSource(source);
            QueueStructureRefresh();
        }

        private void Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            _columnsDirty = true;
            if (_grid == null)
            {
                QueueStructureRefresh();
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (!TryHandleColumnInsert(e.NewItems))
                    {
                        QueueStructureRefresh();
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    if (!TryHandleColumnRemove(e.OldItems))
                    {
                        QueueStructureRefresh();
                    }
                    break;
                case NotifyCollectionChangedAction.Replace:
                    QueueStructureRefresh();
                    break;
                case NotifyCollectionChangedAction.Move:
                    QueueStructureRefresh();
                    break;
                case NotifyCollectionChangedAction.Reset:
                    QueueStructureRefresh();
                    break;
            }
        }

        private void Columns_DisplayIndexChanged(object? sender, DataGridColumnEventArgs e)
        {
            _columnsDirty = true;
            QueueStructureRefresh();
        }

        private void AttachItemsSource(IEnumerable? source)
        {
            if (source == null)
            {
                return;
            }

            if (source is INotifyCollectionChanged incc)
            {
                _collectionChanged = incc;
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridFormulaModel>(
                    _collectionChanged,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    Items_CollectionChanged);
            }

            foreach (var item in source)
            {
                AddItemSubscription(item);
            }
        }

        private void DetachItemsSource()
        {
            if (_collectionChanged != null)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridFormulaModel>(
                    _collectionChanged,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    Items_CollectionChanged);
                _collectionChanged = null;
            }

            foreach (var item in _itemSubscriptionCounts.Keys)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridFormulaModel>(
                    item,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    Item_PropertyChanged);
            }

            _itemSubscriptionCounts.Clear();
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                DetachItemsSource();
                if (_grid != null)
                {
                    AttachItemsSource(_grid.ItemsSource);
                }
                QueueStructureRefresh();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    RemoveItemSubscription(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    AddItemSubscription(item);
                }
            }

            if (!TryHandleRowChange(e))
            {
                QueueStructureRefresh();
            }
        }

        private void AddItemSubscription(object? item)
        {
            if (item is not INotifyPropertyChanged inpc)
            {
                return;
            }

            if (_itemSubscriptionCounts.TryGetValue(inpc, out var count))
            {
                _itemSubscriptionCounts[inpc] = count + 1;
                return;
            }

            _itemSubscriptionCounts[inpc] = 1;
            WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, DataGridFormulaModel>(
                inpc,
                nameof(INotifyPropertyChanged.PropertyChanged),
                Item_PropertyChanged);
        }

        private void RemoveItemSubscription(object? item)
        {
            if (item is not INotifyPropertyChanged inpc)
            {
                return;
            }

            if (!_itemSubscriptionCounts.TryGetValue(inpc, out var count))
            {
                return;
            }

            count--;
            if (count <= 0)
            {
                _itemSubscriptionCounts.Remove(inpc);
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridFormulaModel>(
                    inpc,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    Item_PropertyChanged);
                return;
            }

            _itemSubscriptionCounts[inpc] = count;
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == null)
            {
                return;
            }

            EnsureInitialized();
            if (!TryGetRowIndex(sender, out var rowIndex))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(e.PropertyName))
            {
                MarkRowDirty(rowIndex, sender);
                QueueRecalculate();
                return;
            }

            if (TryGetColumnIndexForProperty(e.PropertyName!, out var columnIndex))
            {
                MarkDirtyCell(CreateAddress(rowIndex, columnIndex), sender);
                QueueRecalculate();
                return;
            }

            MarkRowDirty(rowIndex, sender);
            QueueRecalculate();
        }

        private void Grid_SummaryRecalculated(object? sender, DataGridSummaryRecalculatedEventArgs e)
        {
            EnsureInitialized();
            MarkAllFormulaCellsDirty();
            QueueRecalculate();
        }

        private void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (e.Column == null || e.Row == null || e.EditingElement == null)
            {
                return;
            }

            if (e.EditingElement is not TextBox textBox)
            {
                return;
            }

            if (_grid == null)
            {
                return;
            }

            var definition = DataGridColumnMetadata.GetDefinition(e.Column) as DataGridFormulaColumnDefinition;
            if (definition == null || !definition.AllowCellFormulas)
            {
                return;
            }

            var item = e.Row.DataContext;
            if (item == null)
            {
                return;
            }

            if (!TrySetCellFormula(item, definition, textBox.Text, out _))
            {
                // Formula errors are surfaced as values; editing continues.
            }
        }

        private void Grid_NameChanged(string? name)
        {
            if (_grid == null)
            {
                return;
            }

            var resolved = string.IsNullOrWhiteSpace(name) ? DefaultSheetName : name;
            if (string.Equals(resolved, _sheetName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldSheet = _sheetName;
            var oldWorkbook = _workbookName;
            UpdateNames(name);

            if (!_formulasDirty && _formulaCells.Count > 0)
            {
                ApplySheetRename(oldSheet, _sheetName);
                ApplyTableRename(oldSheet, _sheetName);
                ApplyTableRename(oldWorkbook, _workbookName);
            }

            QueueStructureRefresh();
        }

        private void QueueStructureRefresh()
        {
            _formulasDirty = true;
            QueueRecalculate(requiresRefresh: true);
        }

        private void QueueRecalculate(bool requiresRefresh = false)
        {
            if (requiresRefresh)
            {
                _requiresRefresh = true;
            }

            if (_invalidatePending)
            {
                return;
            }

            _invalidatePending = true;
            var weakSelf = new WeakReference<DataGridFormulaModel>(this);
            Dispatcher.UIThread.Post(() =>
            {
                if (!weakSelf.TryGetTarget(out var model))
                {
                    return;
                }

                model._invalidatePending = false;
                model.ProcessInvalidation();
            }, DispatcherPriority.Background);
        }

        private void ProcessInvalidation()
        {
            var hadStructureChange = _formulasDirty || _columnsDirty;
            EnsureInitialized();

            if (!hadStructureChange)
            {
                RecalculateDirtyCells();
            }

            _dirtyCells.Clear();
            var items = _dirtyItems.Count > 0 ? new List<object>(_dirtyItems) : null;
            _dirtyItems.Clear();

            var requiresRefresh = _requiresRefresh;
            _requiresRefresh = false;
            Invalidated?.Invoke(this, new DataGridFormulaInvalidatedEventArgs(items, requiresRefresh));
        }

        private void EnsureInitialized()
        {
            EnsureColumnMaps();
            if (_formulasDirty)
            {
                InitializeFormulas();
            }
        }

        private void EnsureColumnMaps()
        {
            if (!_columnsDirty)
            {
                return;
            }

            var previousNames = new Dictionary<DataGridColumn, string?>(_columnNameByColumn);

            _accessors.Clear();
            _formulaColumns.Clear();
            _columnIndexMap.Clear();
            _columnIndexByName.Clear();
            _columnIndexByProperty.Clear();
            _formulaColumnIndexMap.Clear();
            _columnNameByColumn.Clear();

            if (_grid == null)
            {
                _columnsDirty = false;
                return;
            }

            var columns = _grid.ColumnsItemsInternal;
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var column = columns[columnIndex];
                var definition = DataGridColumnMetadata.GetDefinition(column);
                var name = ResolveColumnName(column, definition);
                var formulaDefinition = definition as DataGridFormulaColumnDefinition;
                var accessor = formulaDefinition == null ? DataGridColumnMetadata.GetValueAccessor(column) : null;

                if (formulaDefinition == null && accessor == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (formulaDefinition != null)
                    {
                        _formulaColumns[name] = formulaDefinition;
                    }
                    else if (accessor != null)
                    {
                        _accessors[name] = accessor;
                    }

                    _columnIndexByName[name] = _columnIndexMap.Count + 1;
                }

                if (definition?.ColumnKey is string columnKey && !string.IsNullOrWhiteSpace(columnKey))
                {
                    _columnIndexByProperty[columnKey] = _columnIndexMap.Count + 1;
                }

                if (!string.IsNullOrWhiteSpace(definition?.SortMemberPath))
                {
                    _columnIndexByProperty[definition.SortMemberPath] = _columnIndexMap.Count + 1;
                }

                var ordinal = _columnIndexMap.Count + 1;
                var cacheKey = BuildColumnCacheKey(name, column, definition, ordinal);
                _columnIndexMap.Add(new ColumnIndexEntry(column, name, cacheKey, accessor, formulaDefinition, definition?.ColumnKey as string, definition?.SortMemberPath, ordinal));
                _columnNameByColumn[column] = name;
                if (formulaDefinition != null)
                {
                    _formulaColumnIndexMap[formulaDefinition] = ordinal;
                }
            }

            _columnsDirty = false;
            HandleColumnRenames(previousNames);
            BuildNameExpressions();
        }

        private void InitializeFormulas()
        {
            if (_grid == null)
            {
                _formulasDirty = false;
                return;
            }

            EnsureColumnMaps();
            var overrides = SnapshotOverrides();
            _formulaCells.Clear();
            _spillValues.Clear();
            _engine.DependencyGraph.Clear();
            _nameExpressions.Clear();
            BuildNameExpressions();

            var rowCount = _grid.DataConnection?.Count ?? 0;
            if (rowCount <= 0 || _formulaColumnIndexMap.Count == 0)
            {
                _formulasDirty = false;
                IncrementVersion();
                return;
            }

            for (var row = 1; row <= rowCount; row++)
            {
                for (var column = 0; column < _columnIndexMap.Count; column++)
                {
                    var entry = _columnIndexMap[column];
                    if (entry.FormulaDefinition == null)
                    {
                        continue;
                    }

                    var formulaText = NormalizeFormulaText(entry.FormulaDefinition.Formula);
                    if (string.IsNullOrWhiteSpace(formulaText))
                    {
                        if (entry.FormulaDefinition.AllowCellFormulas)
                        {
                            GetOrCreateFormulaCell(row, entry.Index);
                        }

                        continue;
                    }

                    SetFormulaCell(row, entry.Index, formulaText, isOverride: false);
                }
            }

            foreach (var overrideEntry in overrides)
            {
                var address = overrideEntry.Key;
                if (address.Row <= 0 || address.Column <= 0)
                {
                    continue;
                }

                if (address.Row > rowCount || address.Column > _columnIndexMap.Count)
                {
                    continue;
                }

                SetFormulaCell(address.Row, address.Column, overrideEntry.Value, isOverride: true);
            }

            _formulasDirty = false;
            RecalculateAll();
        }

        private void RecalculateAll()
        {
            var formulas = _engine.DependencyGraph.GetFormulaCells();
            if (formulas.Count == 0)
            {
                IncrementVersion();
                return;
            }

            var result = _engine.Recalculate(_workbook, formulas);
            if (result.Recalculated.Count > 0 || result.HasCycle)
            {
                IncrementVersion();
            }
        }

        private void RecalculateDirtyCells()
        {
            if (_dirtyCells.Count == 0)
            {
                return;
            }

            var result = _engine.RecalculateIfAutomatic(_workbook, _dirtyCells);
            if (result.Recalculated.Count > 0 || result.HasCycle)
            {
                IncrementVersion();
            }
        }

        private void IncrementVersion()
        {
            FormulaVersion++;
        }

        private void MarkDirtyCell(FormulaCellAddress address, object? item = null)
        {
            if (_dirtyCells.Add(address) && item != null)
            {
                _dirtyItems.Add(item);
            }
        }

        private void MarkRowDirty(int rowIndex, object? item = null)
        {
            if (rowIndex <= 0 || _columnIndexMap.Count == 0)
            {
                return;
            }

            for (var column = 1; column <= _columnIndexMap.Count; column++)
            {
                MarkDirtyCell(CreateAddress(rowIndex, column), item);
            }
        }

        private void MarkAllFormulaCellsDirty()
        {
            foreach (var address in _engine.DependencyGraph.GetFormulaCells())
            {
                MarkDirtyCell(address);
            }
        }

        private bool TryGetColumnIndexForProperty(string propertyName, out int columnIndex)
        {
            if (_columnIndexByProperty.TryGetValue(propertyName, out columnIndex))
            {
                return true;
            }

            columnIndex = 0;
            return false;
        }

        private bool TryGetRowIndex(object item, out int rowIndex)
        {
            rowIndex = 0;
            if (_grid?.DataConnection == null)
            {
                return false;
            }

            var index = _grid.DataConnection.IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            rowIndex = index + 1;
            return true;
        }

        private bool TryGetItemAtRow(int rowIndex, out object? item)
        {
            item = null;
            if (_grid?.DataConnection == null)
            {
                return false;
            }

            if (rowIndex <= 0 || rowIndex > _grid.DataConnection.Count)
            {
                return false;
            }

            item = _grid.DataConnection.GetDataItem(rowIndex - 1);
            if (item == DataGridCollectionView.NewItemPlaceholder)
            {
                item = null;
            }

            return true;
        }

        private bool TryGetColumnEntry(int columnIndex, out ColumnIndexEntry? entry)
        {
            if (columnIndex <= 0 || columnIndex > _columnIndexMap.Count)
            {
                entry = null;
                return false;
            }

            entry = _columnIndexMap[columnIndex - 1];
            return entry != null;
        }

        private bool TryGetColumnRange(
            FormulaStructuredReference reference,
            out int startIndex,
            out int endIndex)
        {
            startIndex = 0;
            endIndex = 0;

            var columnCount = _columnIndexMap.Count;
            if (columnCount == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(reference.ColumnStart))
            {
                startIndex = 1;
                endIndex = columnCount;
                return true;
            }

            if (!_columnIndexByName.TryGetValue(reference.ColumnStart, out startIndex))
            {
                return false;
            }

            endIndex = startIndex;
            if (!string.IsNullOrWhiteSpace(reference.ColumnEnd))
            {
                if (!_columnIndexByName.TryGetValue(reference.ColumnEnd, out endIndex))
                {
                    return false;
                }
            }

            if (endIndex < startIndex)
            {
                var temp = startIndex;
                startIndex = endIndex;
                endIndex = temp;
            }

            return true;
        }

        private bool HasTotalsRow()
        {
            return _grid?.ShowTotalSummary == true;
        }

        private FormulaValue GetHeaderValue(int columnIndex)
        {
            if (!TryGetColumnEntry(columnIndex, out var entry) || entry == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            var headerText = entry.Name;
            if (string.IsNullOrWhiteSpace(headerText))
            {
                headerText = entry.Column.Header != null
                    ? Convert.ToString(entry.Column.Header, _settings.Culture)
                    : string.Empty;
            }

            return string.IsNullOrWhiteSpace(headerText)
                ? FormulaValue.Blank
                : FormulaValue.FromText(headerText);
        }

        private FormulaValue GetTotalsValue(int columnIndex)
        {
            if (!TryGetColumnEntry(columnIndex, out var entry) || entry == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            if (_grid?.SummaryService == null)
            {
                return FormulaValue.Blank;
            }

            if (entry.Column.Summaries == null || entry.Column.Summaries.Count == 0)
            {
                return FormulaValue.Blank;
            }

            var summary = entry.Column.Summaries[0];
            var value = _grid.SummaryService.GetTotalSummaryValue(entry.Column, summary);
            return ConvertToFormulaValue(value);
        }

        private FormulaCellAddress CreateAddress(int rowIndex, int columnIndex)
        {
            return new FormulaCellAddress(_worksheet.Name, rowIndex, columnIndex);
        }

        private FormulaValue GetCellValue(FormulaCellAddress address)
        {
            if (_formulaCells.TryGetValue(address, out var formulaCell))
            {
                return formulaCell.Value;
            }

            if (_spillValues.TryGetValue(address, out var spilled))
            {
                return spilled;
            }

            return GetDataValue(address.Row, address.Column);
        }

        private void SetCellValue(FormulaCellAddress address, FormulaValue value)
        {
            if (_formulaCells.TryGetValue(address, out var formulaCell))
            {
                formulaCell.Value = value;
                return;
            }

            if (value.Kind == FormulaValueKind.Blank)
            {
                _spillValues.Remove(address);
                return;
            }

            _spillValues[address] = value;
        }

        private FormulaValue GetDataValue(int rowIndex, int columnIndex)
        {
            if (!TryGetColumnEntry(columnIndex, out var entry) || entry == null)
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
            }

            if (!TryGetItemAtRow(rowIndex, out var item) || item == null)
            {
                return FormulaValue.Blank;
            }

            if (entry.FormulaDefinition != null)
            {
                var address = CreateAddress(rowIndex, columnIndex);
                if (_formulaCells.TryGetValue(address, out var formulaCell))
                {
                    return formulaCell.Value;
                }
            }

            if (entry.Accessor != null)
            {
                return ConvertToFormulaValue(entry.Accessor.GetValue(item));
            }

            return FormulaValue.Blank;
        }

        private FormulaValue ResolveCellValue(int rowIndex, int columnIndex)
        {
            var address = CreateAddress(rowIndex, columnIndex);
            var value = GetCellValue(address);
            if (value.Kind == FormulaValueKind.Array)
            {
                value = FormulaCoercion.ApplyImplicitIntersection(value, address);
            }

            return value;
        }

        private DataGridFormulaCell GetOrCreateFormulaCell(int rowIndex, int columnIndex)
        {
            var address = CreateAddress(rowIndex, columnIndex);
            if (_formulaCells.TryGetValue(address, out var cell))
            {
                return cell;
            }

            cell = new DataGridFormulaCell(address);
            _formulaCells[address] = cell;
            return cell;
        }

        private void SetFormulaCell(int rowIndex, int columnIndex, string formulaText, bool isOverride)
        {
            var cell = GetOrCreateFormulaCell(rowIndex, columnIndex);
            cell.IsOverride = isOverride;
            _engine.SetCellFormula(_worksheet, rowIndex, columnIndex, formulaText);
        }

        private void ClearFormulaCell(int rowIndex, int columnIndex)
        {
            _engine.SetCellFormula(_worksheet, rowIndex, columnIndex, null);
            var address = CreateAddress(rowIndex, columnIndex);
            if (_formulaCells.TryGetValue(address, out var cell))
            {
                cell.IsOverride = false;
                cell.Value = FormulaValue.Blank;
            }
        }

        private void SetFormulaParseError(int rowIndex, int columnIndex, string formulaText)
        {
            var cell = GetOrCreateFormulaCell(rowIndex, columnIndex);
            cell.IsOverride = true;
            cell.Formula = formulaText;
            cell.Expression = null;
            cell.Value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Value));
            _engine.DependencyGraph.ClearCell(cell.Address);
        }

        private void UpdateFormulaColumn(DataGridFormulaColumnDefinition definition)
        {
            EnsureInitialized();
            if (_grid == null)
            {
                return;
            }

            if (!_formulaColumnIndexMap.TryGetValue(definition, out var columnIndex))
            {
                return;
            }

            var formulaText = NormalizeFormulaText(definition.Formula);
            var rowCount = _grid.DataConnection?.Count ?? 0;
            for (var row = 1; row <= rowCount; row++)
            {
                var address = CreateAddress(row, columnIndex);
                if (_formulaCells.TryGetValue(address, out var cell) && cell.IsOverride)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(formulaText))
                {
                    ClearFormulaCell(row, columnIndex);
                }
                else
                {
                    SetFormulaCell(row, columnIndex, formulaText, isOverride: false);
                }

                MarkDirtyCell(address);
            }

            QueueRecalculate();
        }

        private Dictionary<FormulaCellAddress, string> SnapshotOverrides()
        {
            var overrides = new Dictionary<FormulaCellAddress, string>();
            foreach (var pair in _formulaCells)
            {
                if (pair.Value.IsOverride && !string.IsNullOrWhiteSpace(pair.Value.Formula))
                {
                    overrides[pair.Key] = pair.Value.Formula!;
                }
            }

            return overrides;
        }

        private void HandleColumnRenames(Dictionary<DataGridColumn, string?> previousNames)
        {
            if (_grid == null || previousNames.Count == 0)
            {
                return;
            }

            var tableName = GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return;
            }

            foreach (var entry in _columnIndexMap)
            {
                if (!previousNames.TryGetValue(entry.Column, out var oldName))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                if (string.Equals(oldName, entry.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ApplyTableColumnRename(tableName, oldName, entry.Name);
            }
        }

        private void ApplyTableColumnRename(string tableName, string oldName, string newName)
        {
            if (_formulasDirty || _formulaCells.Count == 0)
            {
                return;
            }

            var result = _engine.RenameTableColumn(_workbook, tableName, oldName, newName, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
                QueueRecalculate();
            }
        }

        private void ApplyTableRename(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            if (_formulasDirty || _formulaCells.Count == 0)
            {
                return;
            }

            var result = _engine.RenameTable(_workbook, oldName, newName, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
                QueueRecalculate();
            }
        }

        private void ApplySheetRename(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            if (_formulasDirty || _formulaCells.Count == 0)
            {
                return;
            }

            var result = _engine.RenameSheet(_workbook, oldName, newName, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
                QueueRecalculate();
            }
        }

        private void UpdateColumnFormulasFromCells()
        {
            if (_suppressDefinitionUpdates || _grid == null)
            {
                return;
            }

            _suppressDefinitionUpdates = true;
            try
            {
                foreach (var entry in _columnIndexMap)
                {
                    if (entry.FormulaDefinition == null)
                    {
                        continue;
                    }

                    var formulaText = FindColumnFormulaText(entry.Index);
                    if (string.IsNullOrWhiteSpace(formulaText))
                    {
                        continue;
                    }

                    if (!string.Equals(entry.FormulaDefinition.Formula, formulaText, StringComparison.Ordinal))
                    {
                        entry.FormulaDefinition.Formula = formulaText;
                    }
                }
            }
            finally
            {
                _suppressDefinitionUpdates = false;
            }
        }

        private string? FindColumnFormulaText(int columnIndex)
        {
            foreach (var pair in _formulaCells)
            {
                var cell = pair.Value;
                if (cell.Address.Column == columnIndex && !cell.IsOverride && !string.IsNullOrWhiteSpace(cell.Formula))
                {
                    return cell.Formula;
                }
            }

            return null;
        }

        private bool TryHandleRowChange(NotifyCollectionChangedEventArgs e)
        {
            if (_grid == null || _formulasDirty)
            {
                return false;
            }

            if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex >= 0)
            {
                return ApplyRowInsert(e.NewStartingIndex + 1, e.NewItems?.Count ?? 0);
            }

            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldStartingIndex >= 0)
            {
                return ApplyRowDelete(e.OldStartingIndex + 1, e.OldItems?.Count ?? 0);
            }

            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                MarkAllFormulaCellsDirty();
                QueueRecalculate();
                return true;
            }

            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                if (e.NewStartingIndex >= 0)
                {
                    MarkRowDirty(e.NewStartingIndex + 1);
                    QueueRecalculate();
                    return true;
                }
            }

            return false;
        }

        private bool ApplyRowInsert(int rowIndex, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            ShiftRows(rowIndex, count, insert: true);
            var result = _engine.InsertRows(_workbook, _worksheet.Name, rowIndex, count, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
            }

            var rowCount = _grid?.DataConnection?.Count ?? 0;
            var start = rowIndex;
            var end = Math.Min(rowCount, rowIndex + count - 1);
            for (var row = start; row <= end; row++)
            {
                for (var column = 0; column < _columnIndexMap.Count; column++)
                {
                    var entry = _columnIndexMap[column];
                    if (entry.FormulaDefinition == null)
                    {
                        continue;
                    }

                    var formulaText = NormalizeFormulaText(entry.FormulaDefinition.Formula);
                    if (string.IsNullOrWhiteSpace(formulaText))
                    {
                        continue;
                    }

                    SetFormulaCell(row, entry.Index, formulaText, isOverride: false);
                    MarkDirtyCell(CreateAddress(row, entry.Index));
                }
            }

            QueueRecalculate();
            return true;
        }

        private bool ApplyRowDelete(int rowIndex, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            RemoveRows(rowIndex, count);
            var result = _engine.DeleteRows(_workbook, _worksheet.Name, rowIndex, count, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
            }

            QueueRecalculate();
            return true;
        }

        private void ShiftRows(int rowIndex, int count, bool insert)
        {
            if (_formulaCells.Count == 0 && _spillValues.Count == 0)
            {
                return;
            }

            var updated = new Dictionary<FormulaCellAddress, DataGridFormulaCell>();
            foreach (var pair in _formulaCells)
            {
                var address = pair.Key;
                var row = address.Row;
                if (insert)
                {
                    if (row >= rowIndex)
                    {
                        row += count;
                    }
                }
                else
                {
                    var deleteEnd = rowIndex + count - 1;
                    if (row >= rowIndex && row <= deleteEnd)
                    {
                        continue;
                    }

                    if (row > deleteEnd)
                    {
                        row -= count;
                    }
                }

                var newAddress = new FormulaCellAddress(_worksheet.Name, row, address.Column);
                pair.Value.UpdateAddress(newAddress);
                updated[newAddress] = pair.Value;
            }

            _formulaCells.Clear();
            foreach (var pair in updated)
            {
                _formulaCells[pair.Key] = pair.Value;
            }

            ShiftSpills(rowIndex, count, insert, isRow: true);
        }

        private void RemoveRows(int rowIndex, int count)
        {
            ShiftRows(rowIndex, count, insert: false);
        }

        private bool TryHandleColumnInsert(IList? items)
        {
            if (_grid == null || _formulasDirty)
            {
                return false;
            }

            if (items == null || items.Count == 0)
            {
                return true;
            }

            foreach (var item in items)
            {
                if (item is not DataGridColumn column)
                {
                    return false;
                }

                var index = _grid.ColumnsItemsInternal.IndexOf(column);
                if (index < 0)
                {
                    return false;
                }

                if (!ApplyColumnInsert(index + 1))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryHandleColumnRemove(IList? items)
        {
            if (_grid == null || _formulasDirty)
            {
                return false;
            }

            if (items == null || items.Count == 0)
            {
                return true;
            }

            foreach (var item in items)
            {
                if (item is not DataGridColumn column)
                {
                    return false;
                }

                if (!TryGetColumnIndex(column, out var columnIndex))
                {
                    return false;
                }

                if (!ApplyColumnDelete(columnIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ApplyColumnInsert(int columnIndex)
        {
            EnsureColumnMaps();
            ShiftColumns(columnIndex, 1, insert: true);
            var result = _engine.InsertColumns(_workbook, _worksheet.Name, columnIndex, 1, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
            }

            if (_grid == null)
            {
                return false;
            }

            if (columnIndex - 1 < _columnIndexMap.Count)
            {
                var entry = _columnIndexMap[columnIndex - 1];
                if (entry.FormulaDefinition != null)
                {
                    var formulaText = NormalizeFormulaText(entry.FormulaDefinition.Formula);
                    if (!string.IsNullOrWhiteSpace(formulaText))
                    {
                        var rowCount = _grid.DataConnection?.Count ?? 0;
                        for (var row = 1; row <= rowCount; row++)
                        {
                            SetFormulaCell(row, entry.Index, formulaText, isOverride: false);
                            MarkDirtyCell(CreateAddress(row, entry.Index));
                        }
                    }
                }
            }

            QueueRecalculate();
            return true;
        }

        private bool ApplyColumnDelete(int columnIndex)
        {
            EnsureColumnMaps();
            RemoveColumns(columnIndex, 1);
            var result = _engine.DeleteColumns(_workbook, _worksheet.Name, columnIndex, 1, _formatter);
            if (result.UpdatedCells.Count > 0)
            {
                UpdateColumnFormulasFromCells();
                foreach (var address in result.UpdatedCells)
                {
                    MarkDirtyCell(address);
                }
            }

            QueueRecalculate();
            return true;
        }

        private void ShiftColumns(int columnIndex, int count, bool insert)
        {
            if (_formulaCells.Count == 0 && _spillValues.Count == 0)
            {
                return;
            }

            var updated = new Dictionary<FormulaCellAddress, DataGridFormulaCell>();
            foreach (var pair in _formulaCells)
            {
                var address = pair.Key;
                var column = address.Column;
                if (insert)
                {
                    if (column >= columnIndex)
                    {
                        column += count;
                    }
                }
                else
                {
                    var deleteEnd = columnIndex + count - 1;
                    if (column >= columnIndex && column <= deleteEnd)
                    {
                        continue;
                    }

                    if (column > deleteEnd)
                    {
                        column -= count;
                    }
                }

                var newAddress = new FormulaCellAddress(_worksheet.Name, address.Row, column);
                pair.Value.UpdateAddress(newAddress);
                updated[newAddress] = pair.Value;
            }

            _formulaCells.Clear();
            foreach (var pair in updated)
            {
                _formulaCells[pair.Key] = pair.Value;
            }

            ShiftSpills(columnIndex, count, insert, isRow: false);
        }

        private void RemoveColumns(int columnIndex, int count)
        {
            ShiftColumns(columnIndex, count, insert: false);
        }

        private void ShiftSpills(int index, int count, bool insert, bool isRow)
        {
            if (_spillValues.Count == 0)
            {
                return;
            }

            var updated = new Dictionary<FormulaCellAddress, FormulaValue>();
            foreach (var pair in _spillValues)
            {
                var address = pair.Key;
                var row = address.Row;
                var column = address.Column;

                if (isRow)
                {
                    if (insert)
                    {
                        if (row >= index)
                        {
                            row += count;
                        }
                    }
                    else
                    {
                        var deleteEnd = index + count - 1;
                        if (row >= index && row <= deleteEnd)
                        {
                            continue;
                        }

                        if (row > deleteEnd)
                        {
                            row -= count;
                        }
                    }
                }
                else
                {
                    if (insert)
                    {
                        if (column >= index)
                        {
                            column += count;
                        }
                    }
                    else
                    {
                        var deleteEnd = index + count - 1;
                        if (column >= index && column <= deleteEnd)
                        {
                            continue;
                        }

                        if (column > deleteEnd)
                        {
                            column -= count;
                        }
                    }
                }

                var newAddress = new FormulaCellAddress(_worksheet.Name, row, column);
                updated[newAddress] = pair.Value;
            }

            _spillValues.Clear();
            foreach (var pair in updated)
            {
                _spillValues[pair.Key] = pair.Value;
            }
        }

        private bool TryGetColumnIndex(DataGridColumn column, out int columnIndex)
        {
            for (var i = 0; i < _columnIndexMap.Count; i++)
            {
                if (ReferenceEquals(_columnIndexMap[i].Column, column))
                {
                    columnIndex = _columnIndexMap[i].Index;
                    return true;
                }
            }

            columnIndex = 0;
            return false;
        }

        private void UpdateNames(string? gridName)
        {
            if (string.IsNullOrWhiteSpace(gridName))
            {
                _sheetName = DefaultSheetName;
                _workbookName = DefaultWorkbookName;
                return;
            }

            _sheetName = gridName;
            _workbookName = gridName;
        }

        private string GetTableName()
        {
            if (!string.IsNullOrWhiteSpace(_grid?.Name))
            {
                return _grid!.Name;
            }

            return _workbookName;
        }

        private void BuildNameExpressions()
        {
            _nameExpressions.Clear();
            if (_columnIndexByName.Count == 0)
            {
                AppendNamedRanges();
                return;
            }

            foreach (var name in _columnIndexByName.Keys)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (_nameExpressions.ContainsKey(name))
                {
                    continue;
                }

                var structured = BuildStructuredName(name);
                try
                {
                    var expression = _parser.Parse(structured, new FormulaParseOptions
                    {
                        ReferenceMode = _settings.ReferenceMode
                    });
                    _nameExpressions[name] = expression;
                }
                catch (FormulaParseException)
                {
                    // Ignore invalid name expressions.
                }
            }

            AppendNamedRanges();
        }

        private void AppendNamedRanges()
        {
            if (_namedRanges.Count == 0)
            {
                return;
            }

            foreach (var pair in _namedRanges)
            {
                if (_nameExpressions.ContainsKey(pair.Key))
                {
                    continue;
                }

                _nameExpressions[pair.Key] = pair.Value.Expression;
            }
        }

        private static string BuildStructuredName(string columnName)
        {
            var escaped = EscapeStructuredName(columnName);
            return $"[@{escaped}]";
        }

        private static string EscapeStructuredName(string name)
        {
            var needsBrackets = false;
            for (var i = 0; i < name.Length; i++)
            {
                var ch = name[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.')
                {
                    needsBrackets = true;
                    break;
                }
            }

            var escaped = name.Replace("]", "]]");
            return needsBrackets ? $"[{escaped}]" : escaped;
        }

        private bool TryGetNameExpression(string name, out FormulaExpression expression)
        {
            if (_nameExpressions.TryGetValue(name, out expression!))
            {
                return true;
            }

            expression = null!;
            return false;
        }

        private bool IsStructuredReferenceMatch(FormulaStructuredReference reference)
        {
            if (reference.Sheet.HasValue &&
                !string.Equals(reference.Sheet.Value.StartSheetName, _sheetName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(reference.TableName))
            {
                return true;
            }

            var gridName = _grid?.Name;
            if (!string.IsNullOrWhiteSpace(gridName) &&
                string.Equals(reference.TableName, gridName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(reference.TableName, _workbookName, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveStructuredReference(
            FormulaEvaluationContext context,
            FormulaStructuredReference reference,
            out FormulaValue value)
        {
            EnsureColumnMaps();

            if (!IsStructuredReferenceMatch(reference))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            if (!TryGetColumnRange(reference, out var startIndex, out var endIndex))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            var isThisRow = reference.Scope == FormulaStructuredReferenceScope.ThisRow ||
                (reference.Scope == FormulaStructuredReferenceScope.None && string.IsNullOrWhiteSpace(reference.TableName));

            if (isThisRow && string.IsNullOrWhiteSpace(reference.ColumnStart))
            {
                value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                return true;
            }

            if (isThisRow)
            {
                var rowIndex = context.Address.Row;
                if (startIndex == endIndex)
                {
                    value = ResolveCellValue(rowIndex, startIndex);
                    return true;
                }

                var columns = endIndex - startIndex + 1;
                var array = new FormulaArray(1, columns, new FormulaCellAddress(_worksheet.Name, rowIndex, startIndex));
                for (var column = 0; column < columns; column++)
                {
                    array[0, column] = ResolveCellValue(rowIndex, startIndex + column);
                }

                value = FormulaValue.FromArray(array);
                return true;
            }

            var rowCount = _grid?.DataConnection?.Count ?? 0;
            var columnCount = endIndex - startIndex + 1;

            if (reference.Scope == FormulaStructuredReferenceScope.Headers)
            {
                var headerArray = new FormulaArray(1, columnCount);
                for (var column = 0; column < columnCount; column++)
                {
                    headerArray[0, column] = GetHeaderValue(startIndex + column);
                }

                value = FormulaValue.FromArray(headerArray);
                return true;
            }

            if (reference.Scope == FormulaStructuredReferenceScope.Totals)
            {
                if (!HasTotalsRow())
                {
                    value = FormulaValue.FromError(new FormulaError(FormulaErrorType.Ref));
                    return true;
                }

                var totalsArray = new FormulaArray(1, columnCount);
                for (var column = 0; column < columnCount; column++)
                {
                    totalsArray[0, column] = GetTotalsValue(startIndex + column);
                }

                value = FormulaValue.FromArray(totalsArray);
                return true;
            }

            if (reference.Scope == FormulaStructuredReferenceScope.All)
            {
                var hasTotals = HasTotalsRow();
                var totalRows = 1 + rowCount + (hasTotals ? 1 : 0);
                var resultArray = new FormulaArray(totalRows, columnCount);
                var rowCursor = 0;

                for (var column = 0; column < columnCount; column++)
                {
                    resultArray[rowCursor, column] = GetHeaderValue(startIndex + column);
                }

                rowCursor++;

                for (var row = 0; row < rowCount; row++)
                {
                    var rowIndex = row + 1;
                    for (var column = 0; column < columnCount; column++)
                    {
                        resultArray[rowCursor, column] = ResolveCellValue(rowIndex, startIndex + column);
                    }

                    rowCursor++;
                }

                if (hasTotals)
                {
                    for (var column = 0; column < columnCount; column++)
                    {
                        resultArray[rowCursor, column] = GetTotalsValue(startIndex + column);
                    }
                }

                value = FormulaValue.FromArray(resultArray);
                return true;
            }

            if (rowCount <= 0)
            {
                value = FormulaValue.FromArray(new FormulaArray(1, columnCount));
                return true;
            }

            var result = new FormulaArray(rowCount, columnCount, new FormulaCellAddress(_worksheet.Name, 1, startIndex));
            for (var row = 0; row < rowCount; row++)
            {
                var rowIndex = row + 1;
                for (var column = 0; column < columnCount; column++)
                {
                    result[row, column] = ResolveCellValue(rowIndex, startIndex + column);
                }
            }

            value = FormulaValue.FromArray(result);
            return true;
        }

        private bool TryGetStructuredReferenceDependencies(
            FormulaStructuredReference reference,
            out IEnumerable<FormulaCellAddress> dependencies)
        {
            dependencies = Array.Empty<FormulaCellAddress>();
            EnsureColumnMaps();

            if (!IsStructuredReferenceMatch(reference))
            {
                return false;
            }

            if (!TryGetColumnRange(reference, out var startIndex, out var endIndex))
            {
                return false;
            }

            if (reference.Scope == FormulaStructuredReferenceScope.Headers ||
                reference.Scope == FormulaStructuredReferenceScope.Totals ||
                reference.Scope == FormulaStructuredReferenceScope.ThisRow)
            {
                return true;
            }

            var rowCount = _grid?.DataConnection?.Count ?? 0;
            if (rowCount <= 0)
            {
                return true;
            }

            var list = new List<FormulaCellAddress>();
            for (var row = 1; row <= rowCount; row++)
            {
                for (var column = startIndex; column <= endIndex; column++)
                {
                    list.Add(new FormulaCellAddress(_worksheet.Name, row, column));
                }
            }

            dependencies = list;
            return true;
        }

        private static string BuildColumnCacheKey(
            string? name,
            DataGridColumn column,
            DataGridColumnDefinition? definition,
            int index)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            if (definition?.ColumnKey != null)
            {
                return definition.ColumnKey.ToString();
            }

            if (column?.Header != null)
            {
                return column.Header.ToString();
            }

            return $"Column{index}";
        }

        private static string? ResolveColumnName(DataGridColumn column, DataGridColumnDefinition? definition)
        {
            if (definition is DataGridFormulaColumnDefinition formulaDefinition &&
                !string.IsNullOrWhiteSpace(formulaDefinition.FormulaName))
            {
                return formulaDefinition.FormulaName;
            }

            if (definition?.ColumnKey is string key && !string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            if (!string.IsNullOrWhiteSpace(definition?.SortMemberPath))
            {
                return definition.SortMemberPath;
            }

            if (definition?.Header is string header && !string.IsNullOrWhiteSpace(header))
            {
                return header;
            }

            if (column.Header is string columnHeader && !string.IsNullOrWhiteSpace(columnHeader))
            {
                return columnHeader;
            }

            return null;
        }

        private static string? NormalizeFormulaText(string? formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                return null;
            }

            var trimmed = formula.Trim();
            if (trimmed.StartsWith("=", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return $"={trimmed}";
        }

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            if (LooksLikeCellReference(trimmed))
            {
                return false;
            }

            var first = trimmed[0];
            if (!char.IsLetter(first) && first != '_' && first != '\\')
            {
                return false;
            }

            for (var i = 1; i < trimmed.Length; i++)
            {
                var ch = trimmed[i];
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LooksLikeCellReference(string text)
        {
            var index = 0;
            while (index < text.Length && char.IsLetter(text[index]))
            {
                index++;
            }

            if (index == 0 || index == text.Length)
            {
                return false;
            }

            for (var i = index; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    return false;
                }
            }

            return true;
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
                if (ExcelDateConverter.TryConvert(dateValue, _settings.DateSystem, out var serial))
                {
                    return FormulaValue.FromNumber(serial);
                }
            }

            if (value is DateTimeOffset dateOffset)
            {
                if (ExcelDateConverter.TryConvert(dateOffset.DateTime, _settings.DateSystem, out var serial))
                {
                    return FormulaValue.FromNumber(serial);
                }
            }

            if (value is TimeSpan timeSpan)
            {
                return FormulaValue.FromNumber(timeSpan.TotalDays);
            }

            return FormulaValue.FromText(value.ToString() ?? string.Empty);
        }

        private object? ConvertToObject(FormulaValue value, Type? targetType)
        {
            object? result = value.Kind switch
            {
                FormulaValueKind.Number => value.AsNumber(),
                FormulaValueKind.Text => value.AsText(),
                FormulaValueKind.Boolean => value.AsBoolean(),
                FormulaValueKind.Error => value.AsError().ToString(),
                FormulaValueKind.Blank => null,
                _ => value.ToString()
            };

            if (result == null || targetType == null)
            {
                return result;
            }

            var resolved = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (resolved.IsInstanceOfType(result))
            {
                return result;
            }

            try
            {
                return Convert.ChangeType(result, resolved, _settings.Culture);
            }
            catch
            {
                return result;
            }
        }

        private sealed class ColumnIndexEntry
        {
            public ColumnIndexEntry(
                DataGridColumn column,
                string? name,
                string cacheKey,
                IDataGridColumnValueAccessor? accessor,
                DataGridFormulaColumnDefinition? formulaDefinition,
                string? columnKey,
                string? sortMemberPath,
                int index)
            {
                Column = column;
                Name = name;
                CacheKey = cacheKey;
                Accessor = accessor;
                FormulaDefinition = formulaDefinition;
                ColumnKey = columnKey;
                SortMemberPath = sortMemberPath;
                Index = index;
            }

            public DataGridColumn Column { get; }

            public string? Name { get; }

            public string CacheKey { get; }

            public IDataGridColumnValueAccessor? Accessor { get; }

            public DataGridFormulaColumnDefinition? FormulaDefinition { get; }

            public string? ColumnKey { get; }

            public string? SortMemberPath { get; }

            public int Index { get; }
        }

        private readonly struct NamedRangeEntry
        {
            public NamedRangeEntry(string formulaText, FormulaExpression expression)
            {
                FormulaText = formulaText;
                Expression = expression;
            }

            public string FormulaText { get; }

            public FormulaExpression Expression { get; }
        }

        private sealed class DataGridFormulaWorkbook : IFormulaWorkbook,
            IFormulaNameProvider,
            IFormulaStructuredReferenceResolver,
            IFormulaStructuredReferenceDependencyResolver
        {
            private readonly DataGridFormulaModel _owner;
            private readonly List<IFormulaWorksheet> _worksheets = new();

            public DataGridFormulaWorkbook(DataGridFormulaModel owner)
            {
                _owner = owner;
            }

            public string Name => _owner._workbookName;

            public IReadOnlyList<IFormulaWorksheet> Worksheets => _worksheets;

            public FormulaCalculationSettings Settings => _owner._settings;

            public void AddWorksheet(IFormulaWorksheet worksheet)
            {
                _worksheets.Add(worksheet);
            }

            public IFormulaWorksheet GetWorksheet(string name)
            {
                foreach (var worksheet in _worksheets)
                {
                    if (string.Equals(worksheet.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return worksheet;
                    }
                }

                throw new InvalidOperationException($"Worksheet '{name}' not found.");
            }

            public bool TryGetName(string name, out FormulaExpression expression)
            {
                return _owner.TryGetNameExpression(name, out expression!);
            }

            public bool TryResolveStructuredReference(
                FormulaEvaluationContext context,
                FormulaStructuredReference reference,
                out FormulaValue value)
            {
                return _owner.TryResolveStructuredReference(context, reference, out value);
            }

            public bool TryGetStructuredReferenceDependencies(
                FormulaStructuredReference reference,
                out IEnumerable<FormulaCellAddress> dependencies)
            {
                return _owner.TryGetStructuredReferenceDependencies(reference, out dependencies);
            }
        }

        private sealed class DataGridFormulaWorksheet : IFormulaWorksheet, IFormulaCalculationModeProvider
        {
            private readonly DataGridFormulaModel _owner;

            public DataGridFormulaWorksheet(DataGridFormulaModel owner, IFormulaWorkbook workbook)
            {
                _owner = owner;
                Workbook = workbook;
            }

            public string Name => _owner._sheetName;

            public IFormulaWorkbook Workbook { get; }

            public FormulaCalculationMode CalculationMode => _owner.CalculationMode;

            public IFormulaCell GetCell(int row, int column)
            {
                if (_owner.IsFormulaColumn(column))
                {
                    return _owner.GetOrCreateFormulaCell(row, column);
                }

                return new DataGridValueCell(_owner, new FormulaCellAddress(Name, row, column));
            }

            public bool TryGetCell(int row, int column, out IFormulaCell cell)
            {
                cell = GetCell(row, column);
                return true;
            }
        }

        private bool IsFormulaColumn(int columnIndex)
        {
            return TryGetColumnEntry(columnIndex, out var entry) && entry?.FormulaDefinition != null;
        }

        private sealed class DataGridFormulaCell : IFormulaCell
        {
            private FormulaCellAddress _address;

            public DataGridFormulaCell(FormulaCellAddress address)
            {
                _address = address;
                Value = FormulaValue.Blank;
            }

            public FormulaCellAddress Address => _address;

            public string? Formula { get; set; }

            public FormulaExpression? Expression { get; set; }

            public FormulaValue Value { get; set; }

            public bool IsOverride { get; set; }

            public void UpdateAddress(FormulaCellAddress address)
            {
                _address = address;
            }
        }

        private sealed class DataGridValueCell : IFormulaCell
        {
            private readonly DataGridFormulaModel _owner;

            public DataGridValueCell(DataGridFormulaModel owner, FormulaCellAddress address)
            {
                _owner = owner;
                Address = address;
            }

            public FormulaCellAddress Address { get; }

            public string? Formula
            {
                get => null;
                set { }
            }

            public FormulaExpression? Expression
            {
                get => null;
                set { }
            }

            public FormulaValue Value
            {
                get => _owner.GetCellValue(Address);
                set => _owner.SetCellValue(Address, value);
            }
        }
    }
}
