// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace ProDataGrid.FormulaEngine
{
    public sealed class FormulaCalculationEngine
    {
        private readonly IFormulaParser _parser;
        private readonly IFormulaFunctionRegistry _functionRegistry;
        private readonly FormulaEvaluator _evaluator = new FormulaEvaluator();
        private readonly FormulaDependencyGraph _dependencyGraph = new FormulaDependencyGraph();
        private readonly Dictionary<FormulaCellAddress, FormulaRangeAddress> _spillRanges = new();
        private readonly Dictionary<FormulaCellAddress, FormulaCellAddress> _spillOwners = new();
        private readonly HashSet<FormulaCellAddress> _volatileCells = new();
        private readonly object _spillLock = new object();

        public FormulaCalculationEngine(IFormulaParser parser, IFormulaFunctionRegistry functionRegistry)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        public FormulaDependencyGraph DependencyGraph => _dependencyGraph;

        public IReadOnlyCollection<FormulaCellAddress> VolatileCells => _volatileCells;

        public IDisposable TrackNameChanges(IFormulaWorkbook workbook)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            return new NameChangeSubscription(this, workbook);
        }

        public void SetCellFormula(IFormulaWorksheet worksheet, int row, int column, string? formulaText)
        {
            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            var cell = worksheet.GetCell(row, column);
            ClearSpillRange(worksheet.Workbook, cell.Address);
            cell.Formula = formulaText;

            if (string.IsNullOrWhiteSpace(formulaText))
            {
                cell.Expression = null;
                _dependencyGraph.ClearCell(cell.Address);
                _volatileCells.Remove(cell.Address);
                return;
            }

            var options = worksheet.Workbook.Settings.CreateParseOptions();
            var observer = worksheet.Workbook.Settings.CalculationObserver;
            FormulaExpression expression;
            if (observer != null)
            {
                var watch = Stopwatch.StartNew();
                expression = _parser.Parse(formulaText, options);
                watch.Stop();
                observer.OnExpressionParsed(cell.Address, formulaText, watch.Elapsed);
            }
            else
            {
                expression = _parser.Parse(formulaText, options);
            }
            cell.Expression = expression;
            var worksheetNames = worksheet as IFormulaNameProvider;
            var workbookNames = worksheet.Workbook as IFormulaNameProvider;
            _dependencyGraph.SetFormula(cell.Address, expression, worksheet.Workbook, worksheetNames, workbookNames);
            UpdateVolatileFlag(worksheet.Workbook, worksheet, cell.Address, expression);
        }

        public void RefreshDependenciesForNames(IFormulaWorkbook workbook)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            RefreshDependenciesForNames(workbook, _dependencyGraph.GetFormulaCells());
        }

        public void RefreshDependenciesForNames(
            IFormulaWorkbook workbook,
            IEnumerable<FormulaCellAddress> formulaCells)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (formulaCells == null)
            {
                throw new ArgumentNullException(nameof(formulaCells));
            }

            foreach (var address in formulaCells)
            {
                var worksheet = ResolveWorksheet(workbook, address);
                var cell = worksheet.GetCell(address.Row, address.Column);
                var expression = cell.Expression;
                if (expression == null)
                {
                    if (string.IsNullOrWhiteSpace(cell.Formula))
                    {
                        continue;
                    }

                    var options = workbook.Settings.CreateParseOptions();
                    var observer = workbook.Settings.CalculationObserver;
                    if (observer != null)
                    {
                        var watch = Stopwatch.StartNew();
                        expression = _parser.Parse(cell.Formula, options);
                        watch.Stop();
                        observer.OnExpressionParsed(address, cell.Formula, watch.Elapsed);
                    }
                    else
                    {
                        expression = _parser.Parse(cell.Formula, options);
                    }
                    cell.Expression = expression;
                }

                var worksheetNames = worksheet as IFormulaNameProvider;
                var workbookNames = workbook as IFormulaNameProvider;
                _dependencyGraph.SetFormula(address, expression, workbook, worksheetNames, workbookNames);
                UpdateVolatileFlag(workbook, worksheet, address, expression);
            }
        }

        public FormulaCalculationMode GetCalculationMode(IFormulaWorkbook workbook, IFormulaWorksheet worksheet)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (worksheet == null)
            {
                throw new ArgumentNullException(nameof(worksheet));
            }

            return ResolveCalculationMode(workbook, worksheet);
        }

        public FormulaRecalculationResult RecalculateIfAutomatic(
            IFormulaWorkbook workbook,
            IEnumerable<FormulaCellAddress> dirtyCells)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (dirtyCells == null)
            {
                throw new ArgumentNullException(nameof(dirtyCells));
            }

            var filtered = FilterDirtyCellsByCalculationMode(workbook, dirtyCells);
            if (filtered.Count == 0)
            {
                return new FormulaRecalculationResult(Array.Empty<FormulaCellAddress>(), Array.Empty<FormulaCellAddress>());
            }

            return RecalculateInternal(workbook, filtered, includeVolatile: true);
        }

        public FormulaRecalculationResult Recalculate(
            IFormulaWorkbook workbook,
            IEnumerable<FormulaCellAddress> dirtyCells)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (dirtyCells == null)
            {
                throw new ArgumentNullException(nameof(dirtyCells));
            }

            return RecalculateInternal(workbook, dirtyCells, includeVolatile: true);
        }

        private FormulaRecalculationResult RecalculateInternal(
            IFormulaWorkbook workbook,
            IEnumerable<FormulaCellAddress> dirtyCells,
            bool includeVolatile)
        {
            var dirtySet = new HashSet<FormulaCellAddress>(dirtyCells);
            if (includeVolatile)
            {
                foreach (var cell in _volatileCells)
                {
                    dirtySet.Add(cell);
                }
            }

            var observer = workbook.Settings.CalculationObserver;
            var recalcWatch = observer != null ? Stopwatch.StartNew() : null;
            observer?.OnRecalculationStarted(workbook, dirtySet);

            var expandedDirtyCells = ExpandDirtyCells(dirtySet);
            var enableParallel = workbook.Settings.EnableParallelCalculation;
            var hasOrder = false;
            List<FormulaCellAddress> order;
            List<FormulaCellAddress> cycle;
            List<List<FormulaCellAddress>>? levels = null;

            if (enableParallel)
            {
                hasOrder = _dependencyGraph.TryGetRecalculationLevels(expandedDirtyCells, out levels, out cycle);
                levels ??= new List<List<FormulaCellAddress>>();
                order = FlattenLevels(levels);
            }
            else
            {
                hasOrder = _dependencyGraph.TryGetRecalculationOrder(expandedDirtyCells, out order, out cycle);
            }

            if (!hasOrder && !workbook.Settings.EnableIterativeCalculation)
            {
                ApplyCycleErrors(workbook, cycle);
            }

            var allowCircular = workbook.Settings.EnableIterativeCalculation;
            if (enableParallel && levels != null)
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, workbook.Settings.MaxDegreeOfParallelism)
                };

                foreach (var level in levels)
                {
                    Parallel.ForEach(
                        level,
                        options,
                        () => new WorkbookValueResolver(_parser, allowCircular),
                        (address, state, localResolver) =>
                        {
                            EvaluateCell(workbook, localResolver, address);
                            return localResolver;
                        },
                        _ => { });
                }
            }
            else
            {
                var resolver = new WorkbookValueResolver(_parser, allowCircular);
                foreach (var address in order)
                {
                    EvaluateCell(workbook, resolver, address);
                }
            }

            if (cycle.Count > 0 && allowCircular)
            {
                var resolver = new WorkbookValueResolver(_parser, allowCircular);
                EvaluateCycleIteratively(workbook, resolver, cycle);
            }

            if (recalcWatch != null)
            {
                recalcWatch.Stop();
                observer!.OnRecalculationCompleted(workbook, order, cycle, recalcWatch.Elapsed);
            }

            return new FormulaRecalculationResult(order, cycle);
        }

        public FormulaReferenceShiftResult InsertRows(
            IFormulaWorkbook workbook,
            string sheetName,
            int rowIndex,
            int count,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.InsertRows(sheetName, rowIndex, count),
                formatter);
        }

        public FormulaReferenceShiftResult DeleteRows(
            IFormulaWorkbook workbook,
            string sheetName,
            int rowIndex,
            int count,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.DeleteRows(sheetName, rowIndex, count),
                formatter);
        }

        public FormulaReferenceShiftResult InsertColumns(
            IFormulaWorkbook workbook,
            string sheetName,
            int columnIndex,
            int count,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.InsertColumns(sheetName, columnIndex, count),
                formatter);
        }

        public FormulaReferenceShiftResult DeleteColumns(
            IFormulaWorkbook workbook,
            string sheetName,
            int columnIndex,
            int count,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.DeleteColumns(sheetName, columnIndex, count),
                formatter);
        }

        public FormulaReferenceShiftResult RenameSheet(
            IFormulaWorkbook workbook,
            string oldName,
            string newName,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.RenameSheet(oldName, newName),
                formatter);
        }

        public FormulaReferenceShiftResult RenameTable(
            IFormulaWorkbook workbook,
            string oldName,
            string newName,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.RenameTable(oldName, newName),
                formatter);
        }

        public FormulaReferenceShiftResult RenameTableColumn(
            IFormulaWorkbook workbook,
            string tableName,
            string oldName,
            string newName,
            IFormulaFormatter? formatter = null)
        {
            return ApplyReferenceUpdate(
                workbook,
                FormulaReferenceUpdate.RenameTableColumn(tableName, oldName, newName),
                formatter);
        }

        private void EvaluateCell(
            IFormulaWorkbook workbook,
            WorkbookValueResolver resolver,
            FormulaCellAddress address)
        {
            var worksheet = ResolveWorksheet(workbook, address);
            var cell = worksheet.GetCell(address.Row, address.Column);
            ClearSpillRange(workbook, address);

            if (cell.Expression == null)
            {
                if (string.IsNullOrWhiteSpace(cell.Formula))
                {
                    return;
                }

                var options = workbook.Settings.CreateParseOptions();
                var observer = workbook.Settings.CalculationObserver;
                FormulaExpression expression;
                if (observer != null)
                {
                    var watch = Stopwatch.StartNew();
                    expression = _parser.Parse(cell.Formula, options);
                    watch.Stop();
                    observer.OnExpressionParsed(address, cell.Formula, watch.Elapsed);
                }
                else
                {
                    expression = _parser.Parse(cell.Formula, options);
                }
                cell.Expression = expression;
                var worksheetNames = worksheet as IFormulaNameProvider;
                var workbookNames = workbook as IFormulaNameProvider;
                _dependencyGraph.SetFormula(address, expression, workbook, worksheetNames, workbookNames);
            }

            if (cell.Expression == null)
            {
                return;
            }

            var evaluationObserver = workbook.Settings.CalculationObserver;
            var evaluationWatch = evaluationObserver != null ? Stopwatch.StartNew() : null;

            var context = new FormulaEvaluationContext(workbook, worksheet, address, _functionRegistry);
            var value = _evaluator.Evaluate(cell.Expression, context, resolver).ToCellResult();
            if (value.Kind == FormulaValueKind.Array)
            {
                if (workbook.Settings.EnableDynamicArrays)
                {
                    cell.Value = ApplySpill(workbook, worksheet, address, value.AsArray());
                    if (evaluationWatch != null)
                    {
                        evaluationWatch.Stop();
                        evaluationObserver!.OnCellEvaluated(address, cell.Value, evaluationWatch.Elapsed);
                    }
                    return;
                }

                value = FormulaCoercion.ApplyImplicitIntersection(value, address);
            }

            cell.Value = value;
            if (evaluationWatch != null)
            {
                evaluationWatch.Stop();
                evaluationObserver!.OnCellEvaluated(address, cell.Value, evaluationWatch.Elapsed);
            }
        }

        private IReadOnlyCollection<FormulaCellAddress> ExpandDirtyCells(
            IEnumerable<FormulaCellAddress> dirtyCells)
        {
            var expanded = new HashSet<FormulaCellAddress>();
            var queue = new Queue<FormulaCellAddress>();

            foreach (var cell in dirtyCells)
            {
                if (expanded.Add(cell))
                {
                    queue.Enqueue(cell);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (TryGetSpillRange(cell, out var range))
                {
                    foreach (var spillCell in EnumerateRange(range))
                    {
                        if (expanded.Add(spillCell))
                        {
                            queue.Enqueue(spillCell);
                        }
                    }
                }

                if (TryGetSpillOwner(cell, out var owner) && expanded.Add(owner))
                {
                    queue.Enqueue(owner);
                }
            }

            return expanded;
        }

        private FormulaValue ApplySpill(
            IFormulaWorkbook workbook,
            IFormulaWorksheet worksheet,
            FormulaCellAddress anchor,
            FormulaArray array)
        {
            var spillArray = EnsureSpillArray(array, anchor);
            var range = CreateSpillRange(worksheet, anchor, spillArray);

            if (HasSpillConflict(worksheet, anchor, range))
            {
                return FormulaValue.FromError(new FormulaError(FormulaErrorType.Spill));
            }

            WriteSpillValues(worksheet, anchor, range, spillArray);
            SetSpillRange(anchor, range);
            return FormulaValue.FromArray(spillArray);
        }

        private void ClearSpillRange(IFormulaWorkbook workbook, FormulaCellAddress anchor)
        {
            if (!TryGetSpillRange(anchor, out var range))
            {
                return;
            }

            var worksheet = ResolveWorksheet(workbook, anchor);
            foreach (var address in EnumerateRange(range))
            {
                if (address.Equals(anchor))
                {
                    continue;
                }

                if (TryGetSpillOwner(address, out var owner) && owner.Equals(anchor))
                {
                    RemoveSpillOwner(address);
                    if (worksheet.TryGetCell(address.Row, address.Column, out var cell) &&
                        string.IsNullOrWhiteSpace(cell.Formula))
                    {
                        cell.Value = FormulaValue.Blank;
                    }
                }
            }

            RemoveSpillRange(anchor);
        }

        private bool HasSpillConflict(
            IFormulaWorksheet worksheet,
            FormulaCellAddress anchor,
            FormulaRangeAddress range)
        {
            foreach (var address in EnumerateRange(range))
            {
                if (address.Equals(anchor))
                {
                    continue;
                }

                if (TryGetSpillOwner(address, out var owner) && !owner.Equals(anchor))
                {
                    return true;
                }

                if (worksheet.TryGetCell(address.Row, address.Column, out var cell))
                {
                    if (!string.IsNullOrWhiteSpace(cell.Formula))
                    {
                        return true;
                    }

                    if (cell.Value.Kind != FormulaValueKind.Blank)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void WriteSpillValues(
            IFormulaWorksheet worksheet,
            FormulaCellAddress anchor,
            FormulaRangeAddress range,
            FormulaArray array)
        {
            var startRow = range.Start.Row;
            var startColumn = range.Start.Column;
            for (var row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (var column = range.Start.Column; column <= range.End.Column; column++)
                {
                    var address = new FormulaCellAddress(range.Start.SheetName, row, column);
                    if (address.Equals(anchor))
                    {
                        continue;
                    }

                    var targetRow = row - startRow;
                    var targetColumn = column - startColumn;
                    var value = array.IsPresent(targetRow, targetColumn)
                        ? array[targetRow, targetColumn]
                        : FormulaValue.Blank;
                    var cell = worksheet.GetCell(row, column);
                    cell.Value = value;
                    SetSpillOwner(address, anchor);
                }
            }
        }

        private static FormulaArray EnsureSpillArray(FormulaArray array, FormulaCellAddress anchor)
        {
            var result = new FormulaArray(array.RowCount, array.ColumnCount, anchor, array.HasMask);
            for (var row = 0; row < array.RowCount; row++)
            {
                for (var column = 0; column < array.ColumnCount; column++)
                {
                    var present = array.IsPresent(row, column);
                    result.SetValue(row, column, array[row, column], present);
                }
            }

            return result;
        }

        private static FormulaRangeAddress CreateSpillRange(
            IFormulaWorksheet worksheet,
            FormulaCellAddress anchor,
            FormulaArray array)
        {
            var endRow = anchor.Row + array.RowCount - 1;
            var endColumn = anchor.Column + array.ColumnCount - 1;
            var end = new FormulaCellAddress(worksheet.Name, endRow, endColumn);
            return new FormulaRangeAddress(anchor, end);
        }

        private static IEnumerable<FormulaCellAddress> EnumerateRange(FormulaRangeAddress range)
        {
            for (var row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (var column = range.Start.Column; column <= range.End.Column; column++)
                {
                    yield return new FormulaCellAddress(range.Start.SheetName, row, column);
                }
            }
        }

        private static List<FormulaCellAddress> FlattenLevels(List<List<FormulaCellAddress>> levels)
        {
            var count = 0;
            for (var i = 0; i < levels.Count; i++)
            {
                count += levels[i].Count;
            }

            var order = new List<FormulaCellAddress>(count);
            for (var i = 0; i < levels.Count; i++)
            {
                order.AddRange(levels[i]);
            }

            return order;
        }

        private bool TryGetSpillRange(FormulaCellAddress anchor, out FormulaRangeAddress range)
        {
            lock (_spillLock)
            {
                return _spillRanges.TryGetValue(anchor, out range);
            }
        }

        private bool TryGetSpillOwner(FormulaCellAddress address, out FormulaCellAddress owner)
        {
            lock (_spillLock)
            {
                return _spillOwners.TryGetValue(address, out owner);
            }
        }

        private void SetSpillRange(FormulaCellAddress anchor, FormulaRangeAddress range)
        {
            lock (_spillLock)
            {
                _spillRanges[anchor] = range;
            }
        }

        private void RemoveSpillRange(FormulaCellAddress anchor)
        {
            lock (_spillLock)
            {
                _spillRanges.Remove(anchor);
            }
        }

        private void SetSpillOwner(FormulaCellAddress address, FormulaCellAddress anchor)
        {
            lock (_spillLock)
            {
                _spillOwners[address] = anchor;
            }
        }

        private bool RemoveSpillOwner(FormulaCellAddress address)
        {
            lock (_spillLock)
            {
                return _spillOwners.Remove(address);
            }
        }

        private bool HasSpills()
        {
            lock (_spillLock)
            {
                return _spillRanges.Count > 0;
            }
        }

        private List<FormulaCellAddress> GetSpillAnchors()
        {
            lock (_spillLock)
            {
                return new List<FormulaCellAddress>(_spillRanges.Keys);
            }
        }

        private void ClearSpills()
        {
            lock (_spillLock)
            {
                _spillRanges.Clear();
                _spillOwners.Clear();
            }
        }

        private void EvaluateCycleIteratively(
            IFormulaWorkbook workbook,
            WorkbookValueResolver resolver,
            IReadOnlyList<FormulaCellAddress> cycle)
        {
            if (cycle == null || cycle.Count == 0)
            {
                return;
            }

            var maxIterations = Math.Max(1, workbook.Settings.IterativeMaxIterations);
            var tolerance = Math.Max(0, workbook.Settings.IterativeTolerance);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var maxDelta = 0d;
                foreach (var address in cycle)
                {
                    var worksheet = ResolveWorksheet(workbook, address);
                    var cell = worksheet.GetCell(address.Row, address.Column);
                    var previous = cell.Value;
                    EvaluateCell(workbook, resolver, address);
                    var delta = GetIterationDelta(previous, cell.Value);
                    if (delta > maxDelta)
                    {
                        maxDelta = delta;
                    }
                }

                if (maxDelta <= tolerance)
                {
                    break;
                }
            }
        }

        private static double GetIterationDelta(FormulaValue previous, FormulaValue current)
        {
            if (previous.Kind == FormulaValueKind.Number && current.Kind == FormulaValueKind.Number)
            {
                return Math.Abs(previous.AsNumber() - current.AsNumber());
            }

            return previous.Equals(current) ? 0d : double.PositiveInfinity;
        }

        private static void ApplyCycleErrors(IFormulaWorkbook workbook, IReadOnlyList<FormulaCellAddress> cycle)
        {
            if (cycle == null || cycle.Count == 0)
            {
                return;
            }

            var errorValue = FormulaValue.FromError(new FormulaError(FormulaErrorType.Circ));
            foreach (var address in cycle)
            {
                var worksheet = ResolveWorksheet(workbook, address);
                var cell = worksheet.GetCell(address.Row, address.Column);
                cell.Value = errorValue;
            }
        }

        private FormulaReferenceShiftResult ApplyReferenceUpdate(
            IFormulaWorkbook workbook,
            FormulaReferenceUpdate update,
            IFormulaFormatter? formatter)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (update.Count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(update));
            }

            ClearAllSpills(workbook);

            var parseOptions = workbook.Settings.CreateParseOptions();

            var formulaCells = _dependencyGraph.GetFormulaCells();
            var updatedCells = new List<FormulaCellAddress>(formulaCells.Count);
            var removedCells = new List<FormulaCellAddress>();
            var updatedFormulas = new Dictionary<FormulaCellAddress, FormulaExpression>();

            foreach (var oldAddress in formulaCells)
            {
                if (!TryShiftCellAddress(oldAddress, update, out var newAddress))
                {
                    removedCells.Add(oldAddress);
                    continue;
                }

                var worksheet = ResolveWorksheet(workbook, newAddress);
                var cell = worksheet.GetCell(newAddress.Row, newAddress.Column);
                var expression = cell.Expression;
                if (expression == null)
                {
                    if (string.IsNullOrWhiteSpace(cell.Formula))
                    {
                        continue;
                    }

                    expression = _parser.Parse(cell.Formula, parseOptions);
                    cell.Expression = expression;
                }

                var updatedExpression = FormulaReferenceUpdater.Update(
                    expression,
                    oldAddress,
                    newAddress,
                    update,
                    out var changed);

                if (changed)
                {
                    cell.Expression = updatedExpression;
                    if (formatter != null && !string.IsNullOrWhiteSpace(cell.Formula))
                    {
                        var formatOptions = CreateFormatOptions(workbook, cell.Formula);
                        cell.Formula = formatter.Format(updatedExpression, formatOptions);
                    }
                }

                updatedCells.Add(newAddress);
                updatedFormulas[newAddress] = updatedExpression;
                UpdateVolatileFlag(workbook, worksheet, newAddress, updatedExpression);
            }

            UpdateNameExpressions(workbook, update);

            _dependencyGraph.Clear();
            foreach (var pair in updatedFormulas)
            {
                var worksheet = ResolveWorksheet(workbook, pair.Key);
                var worksheetNames = worksheet as IFormulaNameProvider;
                var workbookNames = workbook as IFormulaNameProvider;
                _dependencyGraph.SetFormula(pair.Key, pair.Value, workbook, worksheetNames, workbookNames);
            }

            if (removedCells.Count > 0)
            {
                foreach (var removed in removedCells)
                {
                    _volatileCells.Remove(removed);
                }
            }

            return new FormulaReferenceShiftResult(updatedCells, removedCells);
        }

        private static FormulaCalculationMode ResolveCalculationMode(IFormulaWorkbook workbook, IFormulaWorksheet worksheet)
        {
            if (worksheet is IFormulaCalculationModeProvider worksheetMode)
            {
                return worksheetMode.CalculationMode;
            }

            return workbook.Settings.CalculationMode;
        }

        private List<FormulaCellAddress> FilterDirtyCellsByCalculationMode(
            IFormulaWorkbook workbook,
            IEnumerable<FormulaCellAddress> dirtyCells)
        {
            var filtered = new List<FormulaCellAddress>();
            foreach (var cell in dirtyCells)
            {
                var worksheet = ResolveWorksheet(workbook, cell);
                if (ResolveCalculationMode(workbook, worksheet) == FormulaCalculationMode.Automatic)
                {
                    filtered.Add(cell);
                }
            }

            return filtered;
        }

        private static bool TryShiftCellAddress(
            FormulaCellAddress address,
            FormulaReferenceUpdate update,
            out FormulaCellAddress shifted)
        {
            shifted = address;
            if (update.Kind == FormulaReferenceUpdateKind.RenameSheet)
            {
                if (!string.IsNullOrWhiteSpace(address.SheetName) &&
                    string.Equals(address.SheetName, update.OldName, StringComparison.OrdinalIgnoreCase))
                {
                    shifted = address.WithSheet(update.NewName);
                }

                return true;
            }

            if (update.Kind == FormulaReferenceUpdateKind.RenameTable ||
                update.Kind == FormulaReferenceUpdateKind.RenameTableColumn)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(update.SheetName) ||
                !string.Equals(address.SheetName, update.SheetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var row = address.Row;
            var column = address.Column;

            if (update.Kind == FormulaReferenceUpdateKind.InsertRows)
            {
                if (row >= update.Index)
                {
                    row += update.Count;
                }
            }
            else if (update.Kind == FormulaReferenceUpdateKind.DeleteRows)
            {
                var deleteEnd = update.Index + update.Count - 1;
                if (row >= update.Index && row <= deleteEnd)
                {
                    return false;
                }

                if (row > deleteEnd)
                {
                    row -= update.Count;
                }
            }
            else if (update.Kind == FormulaReferenceUpdateKind.InsertColumns)
            {
                if (column >= update.Index)
                {
                    column += update.Count;
                }
            }
            else if (update.Kind == FormulaReferenceUpdateKind.DeleteColumns)
            {
                var deleteEnd = update.Index + update.Count - 1;
                if (column >= update.Index && column <= deleteEnd)
                {
                    return false;
                }

                if (column > deleteEnd)
                {
                    column -= update.Count;
                }
            }

            shifted = new FormulaCellAddress(address.SheetName, row, column);
            return true;
        }

        private static FormulaFormatOptions CreateFormatOptions(IFormulaWorkbook workbook, string? formulaText)
        {
            var culture = workbook.Settings.Culture ?? CultureInfo.InvariantCulture;
            var listSeparator = culture.TextInfo.ListSeparator;
            var argumentSeparator = string.IsNullOrEmpty(listSeparator) ? ',' : listSeparator[0];
            var decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
            var decimalChar = string.IsNullOrEmpty(decimalSeparator) ? '.' : decimalSeparator[0];

            return new FormulaFormatOptions
            {
                ReferenceMode = workbook.Settings.ReferenceMode,
                ArgumentSeparator = argumentSeparator,
                DecimalSeparator = decimalChar,
                IncludeLeadingEquals = formulaText?.TrimStart().StartsWith("=", StringComparison.Ordinal) == true,
                Culture = culture
            };
        }

        private void UpdateNameExpressions(IFormulaWorkbook workbook, FormulaReferenceUpdate update)
        {
            if (workbook is IFormulaNameCollection workbookNames)
            {
                UpdateNameCollection(workbookNames, workbook, workbook.Worksheets.Count > 0 ? workbook.Worksheets[0].Name : null, update);
            }

            foreach (var worksheet in workbook.Worksheets)
            {
                if (worksheet is IFormulaNameCollection sheetNames)
                {
                    UpdateNameCollection(sheetNames, workbook, worksheet.Name, update);
                }
            }
        }

        private void UpdateVolatileFlag(
            IFormulaWorkbook workbook,
            IFormulaWorksheet worksheet,
            FormulaCellAddress address,
            FormulaExpression? expression)
        {
            if (expression == null)
            {
                _volatileCells.Remove(address);
                return;
            }

            var worksheetNames = worksheet as IFormulaNameProvider;
            var workbookNames = workbook as IFormulaNameProvider;
            var nameStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (IsVolatileExpression(expression, workbook, worksheetNames, workbookNames, worksheet.Name, nameStack))
            {
                _volatileCells.Add(address);
            }
            else
            {
                _volatileCells.Remove(address);
            }
        }

        private bool IsVolatileExpression(FormulaExpression expression, IFormulaWorkbook workbook,
            IFormulaNameProvider? worksheetNames, IFormulaNameProvider? workbookNames,
            string? sheetName, HashSet<string> nameStack, HashSet<string>? locals = null)
        {
            bool Visit(FormulaExpression item, HashSet<string>? scope)
                => IsVolatileExpression(item, workbook, worksheetNames, workbookNames, sheetName, nameStack, scope);
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Unary:
                    return Visit(((FormulaUnaryExpression)expression).Operand, locals);
                case FormulaExpressionKind.Binary:
                    var binary = (FormulaBinaryExpression)expression;
                    return Visit(binary.Left, locals) || Visit(binary.Right, locals);
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    if (Visit(invocation.Target, locals)) return true;
                    foreach (var argument in invocation.Arguments) if (Visit(argument, locals)) return true;
                    return false;
                case FormulaExpressionKind.ArrayLiteral:
                    var array = (FormulaArrayExpression)expression;
                    for (var row = 0; row < array.RowCount; row++)
                        for (var column = 0; column < array.ColumnCount; column++)
                            if (Visit(array[row, column], locals)) return true;
                    return false;
                case FormulaExpressionKind.FunctionCall:
                    var call = (FormulaFunctionCallExpression)expression;
                    if (locals == null || !locals.Contains(call.Name))
                    {
                        if (_functionRegistry.TryGetFunction(call.Name, out var function))
                        {
                            if (function.Info.IsVolatile) return true;
                        }
                        else if (Visit(new FormulaNameExpression(call.Name), locals)) return true;
                    }
                    return FormulaBindingTraversal.VisitArguments(call, locals, Visit);
                case FormulaExpressionKind.Name:
                    var name = ((FormulaNameExpression)expression).Name;
                    if (locals != null && locals.Contains(name)) return false;
                    var scopeKey = CreateNameScopeKey(sheetName, name);
                    if (!nameStack.Add(scopeKey)) return false;
                    try
                    {
                        if (worksheetNames != null && worksheetNames.TryGetName(name, out var sheetExpression))
                            return Visit(sheetExpression, null);
                        if (workbookNames != null && workbookNames.TryGetName(name, out var workbookExpression))
                            return Visit(workbookExpression, null);
                    }
                    finally { nameStack.Remove(scopeKey); }
                    return false;
                default:
                    return false;
            }
        }

        private static string CreateNameScopeKey(string? sheetName, string name)
        {
            return string.IsNullOrWhiteSpace(sheetName)
                ? name
                : $"{sheetName}!{name}";
        }

        private void UpdateNameCollection(
            IFormulaNameCollection names,
            IFormulaWorkbook workbook,
            string? sheetName,
            FormulaReferenceUpdate update)
        {
            var nameList = new List<string>(names.Names);
            if (nameList.Count == 0)
            {
                return;
            }

            var origin = new FormulaCellAddress(sheetName, 1, 1);
            foreach (var name in nameList)
            {
                if (!names.TryGetName(name, out var expression))
                {
                    continue;
                }

                var updated = FormulaReferenceUpdater.Update(expression, origin, origin, update, out var changed);
                if (changed)
                {
                    names.SetExpression(name, updated);
                }
            }
        }

        private void ClearAllSpills(IFormulaWorkbook workbook)
        {
            if (!HasSpills())
            {
                return;
            }

            var anchors = GetSpillAnchors();
            foreach (var anchor in anchors)
            {
                ClearSpillRange(workbook, anchor);
            }

            ClearSpills();
        }

        private static IFormulaWorksheet ResolveWorksheet(IFormulaWorkbook workbook, FormulaCellAddress address)
        {
            if (!string.IsNullOrWhiteSpace(address.SheetName))
            {
                return workbook.GetWorksheet(address.SheetName);
            }

            if (workbook.Worksheets.Count == 0)
            {
                throw new InvalidOperationException("Workbook contains no worksheets.");
            }

            return workbook.Worksheets[0];
        }

        private sealed class NameChangeSubscription : IDisposable
        {
            private readonly FormulaCalculationEngine _engine;
            private readonly IFormulaWorkbook _workbook;
            private readonly List<NameChangeHandler> _handlers = new();
            private bool _disposed;

            public NameChangeSubscription(FormulaCalculationEngine engine, IFormulaWorkbook workbook)
            {
                _engine = engine;
                _workbook = workbook;

                Attach(workbook);
                foreach (var worksheet in workbook.Worksheets)
                {
                    Attach(worksheet);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var handler in _handlers)
                {
                    handler.Notifier.NameChanged -= handler.Handler;
                }

                _handlers.Clear();
                _disposed = true;
            }

            private void Attach(object target)
            {
                if (target is IFormulaNameChangeNotifier notifier)
                {
                    EventHandler<FormulaNameChangedEventArgs> handler = (_, e) => OnNameChanged(target, e);
                    notifier.NameChanged += handler;
                    _handlers.Add(new NameChangeHandler(notifier, handler));
                }
            }

            private void OnNameChanged(object scope, FormulaNameChangedEventArgs e)
            {
                var formulaCells = ResolveAffectedFormulaCells(scope, e);
                if (formulaCells.Count == 0)
                {
                    return;
                }

                _engine.RefreshDependenciesForNames(_workbook, formulaCells);
                _engine.RecalculateIfAutomatic(_workbook, formulaCells);
            }

            private IReadOnlyCollection<FormulaCellAddress> ResolveAffectedFormulaCells(
                object? sender,
                FormulaNameChangedEventArgs e)
            {
                if (sender is IFormulaWorksheet worksheet)
                {
                    if (e.Name == null || e.ChangeKind == FormulaNameChangeKind.Cleared)
                    {
                        return _engine.DependencyGraph.GetFormulaCellsForNameScope(worksheet.Name, true);
                    }

                    return _engine.DependencyGraph.GetFormulaCellsForName(e.Name, worksheet.Name, true);
                }

                if (sender is IFormulaWorkbook)
                {
                    if (e.Name == null || e.ChangeKind == FormulaNameChangeKind.Cleared)
                    {
                        return _engine.DependencyGraph.GetFormulaCellsForNameScope(null, false);
                    }

                    return _engine.DependencyGraph.GetFormulaCellsForName(e.Name, null, false);
                }

                return Array.Empty<FormulaCellAddress>();
            }

            private sealed class NameChangeHandler
            {
                public NameChangeHandler(
                    IFormulaNameChangeNotifier notifier,
                    EventHandler<FormulaNameChangedEventArgs> handler)
                {
                    Notifier = notifier;
                    Handler = handler;
                }

                public IFormulaNameChangeNotifier Notifier { get; }

                public EventHandler<FormulaNameChangedEventArgs> Handler { get; }
            }
        }
    }

    public sealed class FormulaRecalculationResult
    {
        public FormulaRecalculationResult(
            IReadOnlyList<FormulaCellAddress> recalculated,
            IReadOnlyList<FormulaCellAddress> cycle)
        {
            Recalculated = recalculated ?? throw new ArgumentNullException(nameof(recalculated));
            Cycle = cycle ?? Array.Empty<FormulaCellAddress>();
        }

        public IReadOnlyList<FormulaCellAddress> Recalculated { get; }

        public IReadOnlyList<FormulaCellAddress> Cycle { get; }

        public bool HasCycle => Cycle.Count > 0;
    }
}
