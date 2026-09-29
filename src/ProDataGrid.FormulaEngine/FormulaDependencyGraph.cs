// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProDataGrid.FormulaEngine
{
    public sealed class FormulaDependencyGraph
    {
        private readonly IFormulaFunctionRegistry? _functionRegistry;

        /// <summary>Creates a graph with conventional LET/LAMBDA binding analysis.</summary>
        /// <remarks>Without a registry, callable names are conservatively treated as possible defined names.
        /// Use the registry constructor to match registered-function precedence and custom binding metadata.</remarks>
        public FormulaDependencyGraph() { }

        /// <summary>Creates a graph that uses the same function precedence and binding metadata as evaluation.</summary>
        public FormulaDependencyGraph(IFormulaFunctionRegistry functionRegistry)
        {
            _functionRegistry = functionRegistry ?? throw new ArgumentNullException(nameof(functionRegistry));
        }

        private readonly Dictionary<FormulaCellAddress, HashSet<FormulaCellAddress>> _dependencies = new();
        private readonly Dictionary<FormulaCellAddress, HashSet<FormulaCellAddress>> _dependents = new();
        private readonly Dictionary<FormulaCellAddress, HashSet<string>> _nameDependencies = new();
        private readonly Dictionary<string, HashSet<FormulaCellAddress>> _nameDependents =
            new(StringComparer.OrdinalIgnoreCase);

        public void SetFormula(FormulaCellAddress cell, FormulaExpression? expression)
        {
            SetFormula(cell, expression, null, null, null);
        }

        public void SetFormula(
            FormulaCellAddress cell,
            FormulaExpression? expression,
            IFormulaNameProvider? worksheetNames,
            IFormulaNameProvider? workbookNames)
        {
            SetFormula(cell, expression, null, worksheetNames, workbookNames);
        }

        public void SetFormula(
            FormulaCellAddress cell,
            FormulaExpression? expression,
            IFormulaWorkbook? workbook,
            IFormulaNameProvider? worksheetNames,
            IFormulaNameProvider? workbookNames)
        {
            RemoveDependencies(cell);
            RemoveNameDependencies(cell);

            if (expression == null)
            {
                _dependencies.Remove(cell);
                _nameDependencies.Remove(cell);
                return;
            }

            CollectDependencies(
                cell,
                expression,
                workbook,
                worksheetNames,
                workbookNames,
                out var dependencies,
                out var nameDependencies);
            _dependencies[cell] = dependencies;
            if (nameDependencies.Count > 0)
            {
                _nameDependencies[cell] = nameDependencies;
                foreach (var name in nameDependencies)
                {
                    if (!_nameDependents.TryGetValue(name, out var dependents))
                    {
                        dependents = new HashSet<FormulaCellAddress>();
                        _nameDependents[name] = dependents;
                    }
                    dependents.Add(cell);
                }
            }
            else
            {
                _nameDependencies.Remove(cell);
            }

            foreach (var dependency in dependencies)
            {
                if (!_dependents.TryGetValue(dependency, out var dependents))
                {
                    dependents = new HashSet<FormulaCellAddress>();
                    _dependents[dependency] = dependents;
                }
                dependents.Add(cell);
            }
        }

        public void ClearCell(FormulaCellAddress cell)
        {
            RemoveDependencies(cell);
            RemoveNameDependencies(cell);
            _dependencies.Remove(cell);
            _nameDependencies.Remove(cell);
        }

        public IReadOnlyCollection<FormulaCellAddress> GetDependencies(FormulaCellAddress cell)
        {
            return _dependencies.TryGetValue(cell, out var dependencies)
                ? dependencies
                : Array.Empty<FormulaCellAddress>();
        }

        public IReadOnlyCollection<FormulaCellAddress> GetDependents(FormulaCellAddress cell)
        {
            return _dependents.TryGetValue(cell, out var dependents)
                ? dependents
                : Array.Empty<FormulaCellAddress>();
        }

        public IReadOnlyCollection<FormulaCellAddress> GetFormulaCellsForName(
            string name,
            string? sheetName,
            bool isWorksheetScope)
        {
            var key = CreateNameScopeKey(name, sheetName, isWorksheetScope);
            return _nameDependents.TryGetValue(key, out var dependents)
                ? dependents
                : Array.Empty<FormulaCellAddress>();
        }

        public IReadOnlyCollection<FormulaCellAddress> GetFormulaCellsForNameScope(
            string? sheetName,
            bool isWorksheetScope)
        {
            var result = new HashSet<FormulaCellAddress>();
            foreach (var pair in _nameDependents)
            {
                if (!IsScopeMatch(pair.Key, sheetName, isWorksheetScope))
                {
                    continue;
                }

                foreach (var cell in pair.Value)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        public IReadOnlyList<FormulaCellAddress> GetFormulaCells()
        {
            return new List<FormulaCellAddress>(_dependencies.Keys);
        }

        public void Clear()
        {
            _dependencies.Clear();
            _dependents.Clear();
            _nameDependencies.Clear();
            _nameDependents.Clear();
        }

        public bool TryGetRecalculationOrder(
            IEnumerable<FormulaCellAddress> dirtyCells,
            out List<FormulaCellAddress> order,
            out List<FormulaCellAddress> cycle)
        {
            if (dirtyCells == null)
            {
                throw new ArgumentNullException(nameof(dirtyCells));
            }

            var affected = CollectAffectedCells(dirtyCells);
            var inDegree = new Dictionary<FormulaCellAddress, int>();
            var adjacency = new Dictionary<FormulaCellAddress, List<FormulaCellAddress>>();

            foreach (var node in affected)
            {
                inDegree[node] = 0;
            }

            foreach (var node in affected)
            {
                if (!_dependencies.TryGetValue(node, out var dependencies))
                {
                    continue;
                }

                foreach (var dependency in dependencies)
                {
                    if (!affected.Contains(dependency))
                    {
                        continue;
                    }

                    inDegree[node] = inDegree[node] + 1;
                    if (!adjacency.TryGetValue(dependency, out var list))
                    {
                        list = new List<FormulaCellAddress>();
                        adjacency[dependency] = list;
                    }
                    list.Add(node);
                }
            }

            var queue = new Queue<FormulaCellAddress>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0)
                {
                    queue.Enqueue(pair.Key);
                }
            }

            order = new List<FormulaCellAddress>(affected.Count);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                order.Add(node);

                if (!adjacency.TryGetValue(node, out var list))
                {
                    continue;
                }

                foreach (var dependent in list)
                {
                    var next = inDegree[dependent] - 1;
                    inDegree[dependent] = next;
                    if (next == 0)
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            if (order.Count == affected.Count)
            {
                cycle = new List<FormulaCellAddress>();
                return true;
            }

            cycle = new List<FormulaCellAddress>();
            foreach (var pair in inDegree)
            {
                if (pair.Value > 0)
                {
                    cycle.Add(pair.Key);
                }
            }

            return false;
        }

        public bool TryGetRecalculationLevels(
            IEnumerable<FormulaCellAddress> dirtyCells,
            out List<List<FormulaCellAddress>> levels,
            out List<FormulaCellAddress> cycle)
        {
            if (dirtyCells == null)
            {
                throw new ArgumentNullException(nameof(dirtyCells));
            }

            var affected = CollectAffectedCells(dirtyCells);
            var inDegree = new Dictionary<FormulaCellAddress, int>();
            var adjacency = new Dictionary<FormulaCellAddress, List<FormulaCellAddress>>();

            foreach (var node in affected)
            {
                inDegree[node] = 0;
            }

            foreach (var node in affected)
            {
                if (!_dependencies.TryGetValue(node, out var dependencies))
                {
                    continue;
                }

                foreach (var dependency in dependencies)
                {
                    if (!affected.Contains(dependency))
                    {
                        continue;
                    }

                    inDegree[node] = inDegree[node] + 1;
                    if (!adjacency.TryGetValue(dependency, out var list))
                    {
                        list = new List<FormulaCellAddress>();
                        adjacency[dependency] = list;
                    }
                    list.Add(node);
                }
            }

            var queue = new Queue<FormulaCellAddress>();
            foreach (var pair in inDegree)
            {
                if (pair.Value == 0)
                {
                    queue.Enqueue(pair.Key);
                }
            }

            levels = new List<List<FormulaCellAddress>>();
            var processed = 0;
            while (queue.Count > 0)
            {
                var levelCount = queue.Count;
                var level = new List<FormulaCellAddress>(levelCount);
                for (var i = 0; i < levelCount; i++)
                {
                    var node = queue.Dequeue();
                    level.Add(node);
                    processed++;

                    if (!adjacency.TryGetValue(node, out var list))
                    {
                        continue;
                    }

                    foreach (var dependent in list)
                    {
                        var next = inDegree[dependent] - 1;
                        inDegree[dependent] = next;
                        if (next == 0)
                        {
                            queue.Enqueue(dependent);
                        }
                    }
                }

                levels.Add(level);
            }

            if (processed == affected.Count)
            {
                cycle = new List<FormulaCellAddress>();
                return true;
            }

            cycle = new List<FormulaCellAddress>();
            foreach (var pair in inDegree)
            {
                if (pair.Value > 0)
                {
                    cycle.Add(pair.Key);
                }
            }

            return false;
        }

        private void RemoveDependencies(FormulaCellAddress cell)
        {
            if (!_dependencies.TryGetValue(cell, out var existing))
            {
                return;
            }

            foreach (var dependency in existing)
            {
                if (_dependents.TryGetValue(dependency, out var dependents))
                {
                    dependents.Remove(cell);
                    if (dependents.Count == 0)
                    {
                        _dependents.Remove(dependency);
                    }
                }
            }
        }

        private void RemoveNameDependencies(FormulaCellAddress cell)
        {
            if (!_nameDependencies.TryGetValue(cell, out var existing))
            {
                return;
            }

            foreach (var name in existing)
            {
                if (_nameDependents.TryGetValue(name, out var dependents))
                {
                    dependents.Remove(cell);
                    if (dependents.Count == 0)
                    {
                        _nameDependents.Remove(name);
                    }
                }
            }
        }

        private void CollectDependencies(
            FormulaCellAddress origin,
            FormulaExpression expression,
            IFormulaWorkbook? workbook,
            IFormulaNameProvider? worksheetNames,
            IFormulaNameProvider? workbookNames,
            out HashSet<FormulaCellAddress> dependencies,
            out HashSet<string> nameDependencies)
        {
            var references = new List<FormulaReference>();
            var structuredReferences = new List<FormulaStructuredReference>();
            var nameStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            nameDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectReferences(
                expression,
                references,
                structuredReferences,
                nameDependencies,
                worksheetNames,
                workbookNames,
                origin.SheetName,
                nameStack);

            dependencies = new HashSet<FormulaCellAddress>();
            foreach (var reference in references)
            {
                if (!TryExpandReference(reference, origin, workbook, out var expanded))
                {
                    continue;
                }

                foreach (var address in expanded)
                {
                    dependencies.Add(address);
                }
            }

            if (structuredReferences.Count > 0 &&
                workbook is IFormulaStructuredReferenceDependencyResolver dependencyResolver)
            {
                foreach (var structured in structuredReferences)
                {
                    if (!dependencyResolver.TryGetStructuredReferenceDependencies(structured, out var structuredDeps))
                    {
                        continue;
                    }

                    foreach (var address in structuredDeps)
                    {
                        dependencies.Add(address);
                    }
                }
            }
        }

        private HashSet<FormulaCellAddress> CollectAffectedCells(IEnumerable<FormulaCellAddress> dirtyCells)
        {
            var affected = new HashSet<FormulaCellAddress>();
            var queue = new Queue<FormulaCellAddress>();

            foreach (var cell in dirtyCells)
            {
                queue.Enqueue(cell);
                if (_dependencies.ContainsKey(cell))
                {
                    affected.Add(cell);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!_dependents.TryGetValue(cell, out var dependents))
                {
                    continue;
                }

                foreach (var dependent in dependents)
                {
                    if (affected.Add(dependent))
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            return affected;
        }

        private void CollectReferences(
            FormulaExpression expression, List<FormulaReference> references,
            List<FormulaStructuredReference> structuredReferences, HashSet<string> nameDependencies,
            IFormulaNameProvider? worksheetNames, IFormulaNameProvider? workbookNames,
            string? sheetName, HashSet<string> nameStack, HashSet<string>? locals = null)
        {
            void Visit(FormulaExpression item, HashSet<string>? scope)
                => CollectReferences(item, references, structuredReferences, nameDependencies,
                    worksheetNames, workbookNames, sheetName, nameStack, scope);
            switch (expression.Kind)
            {
                case FormulaExpressionKind.Reference:
                    references.Add(((FormulaReferenceExpression)expression).Reference);
                    return;
                case FormulaExpressionKind.StructuredReference:
                    structuredReferences.Add(((FormulaStructuredReferenceExpression)expression).Reference);
                    return;
                case FormulaExpressionKind.Unary:
                    Visit(((FormulaUnaryExpression)expression).Operand, locals);
                    return;
                case FormulaExpressionKind.Binary:
                    var binary = (FormulaBinaryExpression)expression;
                    Visit(binary.Left, locals);
                    Visit(binary.Right, locals);
                    return;
                case FormulaExpressionKind.Invocation:
                    var invocation = (FormulaInvocationExpression)expression;
                    Visit(invocation.Target, locals);
                    foreach (var arg in invocation.Arguments) Visit(arg, locals);
                    return;
                case FormulaExpressionKind.FunctionCall:
                    var call = (FormulaFunctionCallExpression)expression;
                    // Also register unresolved callable names so later definitions invalidate dependencies.
                    if (_functionRegistry == null || !_functionRegistry.TryGetFunction(call.Name, out _))
                        Visit(new FormulaNameExpression(call.Name), locals);
                    FormulaBindingTraversal.VisitArguments(call, locals,
                        FormulaBindingTraversal.GetBindingKind(call, locals, _functionRegistry),
                        (arg, scope) => { Visit(arg, scope); return false; });
                    return;
                case FormulaExpressionKind.Name:
                    var name = ((FormulaNameExpression)expression).Name;
                    if (locals != null && locals.Contains(name)) return;
                    if (!TryGetNameExpression(worksheetNames, workbookNames, name, sheetName, out var resolved, out var scopeKey))
                    {
                        RegisterUnresolvedName(nameDependencies, name, sheetName, worksheetNames, workbookNames);
                        return;
                    }
                    nameDependencies.Add(scopeKey);
                    if (!nameStack.Add(scopeKey)) return;
                    try { Visit(resolved, null); }
                    finally { nameStack.Remove(scopeKey); }
                    return;
                case FormulaExpressionKind.ArrayLiteral:
                    var array = (FormulaArrayExpression)expression;
                    for (var row = 0; row < array.RowCount; row++)
                        for (var column = 0; column < array.ColumnCount; column++) Visit(array[row, column], locals);
                    return;
            }
        }

        private static bool TryExpandReference(
            FormulaReference reference,
            FormulaCellAddress origin,
            IFormulaWorkbook? workbook,
            out IEnumerable<FormulaCellAddress> addresses)
        {
            addresses = Array.Empty<FormulaCellAddress>();

            if (reference.Start.Sheet is { } sheetRef)
            {
                if (sheetRef.IsExternal)
                {
                    return false;
                }

                if (sheetRef.IsRange)
                {
                    if (workbook == null)
                    {
                        return false;
                    }

                    if (!TryGetSheetRange(workbook, sheetRef, out var sheets))
                    {
                        return false;
                    }

                    var list = new List<FormulaCellAddress>();
                    foreach (var sheet in sheets)
                    {
                        if (!TryResolveRangeOnSheet(reference, origin, sheet.Name, out var range))
                        {
                            continue;
                        }

                        for (var row = range.Start.Row; row <= range.End.Row; row++)
                        {
                            for (var column = range.Start.Column; column <= range.End.Column; column++)
                            {
                                list.Add(new FormulaCellAddress(sheet.Name, row, column));
                            }
                        }
                    }

                    addresses = list;
                    return true;
                }
            }

            if (reference.Kind == FormulaReferenceKind.Cell)
            {
                if (FormulaReferenceResolver.TryResolveCell(reference.Start, origin, out var address))
                {
                    addresses = new[] { address };
                    return true;
                }
                return false;
            }

            if (!FormulaReferenceResolver.TryResolveRange(reference, origin, out var resolvedRange))
            {
                return false;
            }

            var expanded = new List<FormulaCellAddress>();
            for (var row = resolvedRange.Start.Row; row <= resolvedRange.End.Row; row++)
            {
                for (var column = resolvedRange.Start.Column; column <= resolvedRange.End.Column; column++)
                {
                    expanded.Add(new FormulaCellAddress(resolvedRange.Start.SheetName, row, column));
                }
            }

            addresses = expanded;
            return true;
        }

        private static bool TryGetSheetRange(
            IFormulaWorkbook workbook,
            FormulaSheetReference sheetRef,
            out IReadOnlyList<IFormulaWorksheet> sheets)
        {
            sheets = Array.Empty<IFormulaWorksheet>();
            var targetWorkbook = workbook;
            if (sheetRef.IsExternal)
            {
                if (workbook is not IFormulaWorkbookResolver workbookResolver ||
                    string.IsNullOrWhiteSpace(sheetRef.WorkbookName) ||
                    !workbookResolver.TryGetWorkbook(sheetRef.WorkbookName, out targetWorkbook))
                {
                    return false;
                }
            }

            var startName = sheetRef.StartSheetName;
            if (string.IsNullOrWhiteSpace(startName))
            {
                return false;
            }

            var endName = sheetRef.EndSheetName ?? startName;
            var worksheetList = targetWorkbook.Worksheets;
            var startIndex = IndexOfSheet(worksheetList, startName);
            var endIndex = IndexOfSheet(worksheetList, endName);
            if (startIndex < 0 || endIndex < 0)
            {
                return false;
            }

            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            var list = new List<IFormulaWorksheet>();
            for (var i = startIndex; i <= endIndex; i++)
            {
                list.Add(worksheetList[i]);
            }

            sheets = list;
            return true;
        }

        private static int IndexOfSheet(IReadOnlyList<IFormulaWorksheet> sheets, string name)
        {
            for (var i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryResolveRangeOnSheet(
            FormulaReference reference,
            FormulaCellAddress origin,
            string sheetName,
            out FormulaRangeAddress range)
        {
            range = default;
            if (!TryResolveCellOnSheet(reference.Start, origin, sheetName, out var start))
            {
                return false;
            }

            if (!TryResolveCellOnSheet(reference.End, origin, sheetName, out var end))
            {
                return false;
            }

            range = new FormulaRangeAddress(start, end);
            return true;
        }

        private static bool TryResolveCellOnSheet(
            FormulaReferenceAddress address,
            FormulaCellAddress origin,
            string sheetName,
            out FormulaCellAddress resolved)
        {
            resolved = default;

            var row = address.Mode == FormulaReferenceMode.A1
                ? address.Row
                : address.RowIsAbsolute ? address.Row : origin.Row + address.Row;

            var column = address.Mode == FormulaReferenceMode.A1
                ? address.Column
                : address.ColumnIsAbsolute ? address.Column : origin.Column + address.Column;

            if (row <= 0 || column <= 0)
            {
                return false;
            }

            resolved = new FormulaCellAddress(sheetName, row, column);
            return true;
        }

        private static bool TryGetNameExpression(
            IFormulaNameProvider? worksheetNames,
            IFormulaNameProvider? workbookNames,
            string name,
            string? sheetName,
            out FormulaExpression expression,
            out string scopeKey)
        {
            if (worksheetNames != null && worksheetNames.TryGetName(name, out expression))
            {
                scopeKey = CreateNameScopeKey(name, sheetName, true);
                return true;
            }

            if (workbookNames != null && workbookNames.TryGetName(name, out expression))
            {
                scopeKey = CreateNameScopeKey(name, sheetName, false);
                return true;
            }

            expression = null!;
            scopeKey = string.Empty;
            return false;
        }

        private static void RegisterUnresolvedName(
            HashSet<string> nameDependencies,
            string name,
            string? sheetName,
            IFormulaNameProvider? worksheetNames,
            IFormulaNameProvider? workbookNames)
        {
            if (worksheetNames != null && !string.IsNullOrWhiteSpace(sheetName))
            {
                nameDependencies.Add(CreateNameScopeKey(name, sheetName, true));
            }

            if (workbookNames != null)
            {
                nameDependencies.Add(CreateNameScopeKey(name, sheetName, false));
            }
        }

        private static string CreateNameScopeKey(string name, string? sheetName, bool isWorksheetScope)
        {
            return isWorksheetScope
                ? $"{sheetName ?? string.Empty}!{name}"
                : name;
        }

        private static bool IsScopeMatch(string scopeKey, string? sheetName, bool isWorksheetScope)
        {
            if (isWorksheetScope)
            {
                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    return false;
                }

                var prefix = $"{sheetName}!";
                return scopeKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return scopeKey.IndexOf('!') < 0;
        }
    }
}
