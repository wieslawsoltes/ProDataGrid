// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Utils;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Avalonia.Controls.DataGridSearching
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridAccessorSearchAdapter : DataGridSearchAdapter
    {
        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private readonly bool _throwOnMissingAccessor;
        private readonly DataGridFastPathOptions _options;
        private readonly bool _enableHighPerformanceSearching;
        private readonly bool _highPerformanceSearchTrackItemChanges;
        private readonly IncrementalSearchResultService _incrementalSearchService;
        private List<SearchDescriptorPlan> _activePlans;

        public DataGridAccessorSearchAdapter(
            ISearchModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            DataGridFastPathOptions options = null)
            : base(model, columnProvider)
        {
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));
            _throwOnMissingAccessor = options?.ThrowOnMissingAccessor ?? false;
            _options = options;
            _enableHighPerformanceSearching = options?.EnableHighPerformanceSearching ?? true;
            _highPerformanceSearchTrackItemChanges = options?.HighPerformanceSearchTrackItemChanges ?? true;
            if (_enableHighPerformanceSearching)
            {
                _incrementalSearchService = new IncrementalSearchResultService(_highPerformanceSearchTrackItemChanges);
            }
        }

        protected override bool TrackItemPropertyChanges =>
            !_enableHighPerformanceSearching || _highPerformanceSearchTrackItemChanges;

        protected override void OnViewCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_enableHighPerformanceSearching || e == null || _activePlans == null || _incrementalSearchService == null)
            {
                return;
            }

            bool canRemapReset = e.Action == NotifyCollectionChangedAction.Reset &&
                View is DataGridCollectionView { IsReorderOnlyReset: true };
            _incrementalSearchService.RecordCollectionChange(e, canRemapReset);
        }

        protected override void OnViewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_enableHighPerformanceSearching ||
                !_highPerformanceSearchTrackItemChanges ||
                sender == null ||
                _activePlans == null ||
                _incrementalSearchService == null)
            {
                return;
            }

            if (sender == DataGridCollectionView.NewItemPlaceholder || sender is DataGridCollectionViewGroup)
            {
                return;
            }

            _incrementalSearchService.RecordItemChange(sender);
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<SearchDescriptor> descriptors,
            IReadOnlyList<SearchDescriptor> previousDescriptors,
            out IReadOnlyList<SearchResult> results)
        {
            _ = previousDescriptors;

            if (_enableHighPerformanceSearching &&
                _incrementalSearchService != null &&
                _activePlans != null &&
                _incrementalSearchService.TryApplyPendingChanges(
                    descriptors,
                    BuildRowResults,
                    TryGetUniqueRowIndex,
                    TryRemapReorderedResults,
                    out results))
            {
                return true;
            }

            results = ComputeResults(descriptors, out var plans);
            if (_enableHighPerformanceSearching && _incrementalSearchService != null)
            {
                if (descriptors == null || descriptors.Count == 0)
                {
                    _activePlans = null;
                    _incrementalSearchService.ClearState();
                }
                else
                {
                    _activePlans = plans ?? new List<SearchDescriptorPlan>();
                    _incrementalSearchService.SetActiveState(descriptors, results);
                }
            }

            return true;
        }

        private bool TryRemapReorderedResults(
            IReadOnlyList<SearchResult> currentResults,
            out IReadOnlyList<SearchResult> remappedResults)
        {
            remappedResults = null;
            if (currentResults == null || currentResults.Count == 0)
            {
                remappedResults = Array.Empty<SearchResult>();
                return true;
            }

            var requiredIndexes = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
            for (int i = 0; i < currentResults.Count; i++)
            {
                object item = currentResults[i].Item;
                if (item == null)
                {
                    return false;
                }

                requiredIndexes.TryAdd(item, -1);
            }

            int rowIndex = 0;
            foreach (object item in View)
            {
                if (item != null && requiredIndexes.TryGetValue(item, out int existingIndex))
                {
                    if (existingIndex >= 0)
                    {
                        return false;
                    }

                    requiredIndexes[item] = rowIndex;
                }

                rowIndex++;
            }

            foreach (KeyValuePair<object, int> pair in requiredIndexes)
            {
                if (pair.Value < 0)
                {
                    return false;
                }
            }

            var remapped = new List<SearchResult>(currentResults.Count);
            for (int i = 0; i < currentResults.Count; i++)
            {
                SearchResult current = currentResults[i];
                remapped.Add(new SearchResult(
                    current.Item,
                    requiredIndexes[current.Item],
                    current.ColumnId,
                    current.ColumnIndex,
                    current.Text,
                    current.Matches));
            }

            remapped.Sort(static (left, right) =>
            {
                int comparison = left.RowIndex.CompareTo(right.RowIndex);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = left.ColumnIndex.CompareTo(right.ColumnIndex);
                if (comparison != 0)
                {
                    return comparison;
                }

                int leftStart = left.Matches.Count > 0 ? left.Matches[0].Start : 0;
                int rightStart = right.Matches.Count > 0 ? right.Matches[0].Start : 0;
                return leftStart.CompareTo(rightStart);
            });

            remappedResults = remapped;
            return true;
        }

        private bool TryGetUniqueRowIndex(object item, out int rowIndex)
        {
            rowIndex = -1;
            if (item == null || item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return true;
            }

            var view = View;
            if (view is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!ReferenceEquals(list[i], item))
                    {
                        continue;
                    }

                    if (rowIndex >= 0)
                    {
                        return false;
                    }

                    rowIndex = i;
                }

                return true;
            }

            int index = 0;
            foreach (var candidate in view)
            {
                if (!ReferenceEquals(candidate, item))
                {
                    index++;
                    continue;
                }

                if (rowIndex >= 0)
                {
                    return false;
                }

                rowIndex = index;
                index++;
            }

            return true;
        }

        private IReadOnlyList<SearchResult> BuildRowResults(object item, int rowIndex)
        {
            if (item == null || item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return Array.Empty<SearchResult>();
            }

            var view = View;
            if (view == null || _activePlans == null || _activePlans.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            var rowResults = new Dictionary<DataGridColumn, SearchResultBuilder>();
            for (int planIndex = 0; planIndex < _activePlans.Count; planIndex++)
            {
                var plan = _activePlans[planIndex];
                for (int columnIndex = 0; columnIndex < plan.Columns.Count; columnIndex++)
                {
                    var column = plan.Columns[columnIndex];
                    var text = GetColumnText(column, item, plan.Descriptor, view);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    var matches = SearchTextMatcher.FindMatches(text, plan.PreparedDescriptor);
                    if (matches == null || matches.Count == 0)
                    {
                        continue;
                    }

                    if (!rowResults.TryGetValue(column.Column, out var builder))
                    {
                        builder = new SearchResultBuilder(item, rowIndex, column.Column, column.ColumnIndex, text);
                        rowResults.Add(column.Column, builder);
                    }

                    builder.AddMatches(matches);
                }
            }

            if (rowResults.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            return rowResults.Values
                .Select(r => r.Build())
                .OrderBy(r => r.ColumnIndex)
                .ThenBy(r => r.Matches.Count > 0 ? r.Matches[0].Start : 0)
                .ToList();
        }

        private IReadOnlyList<SearchResult> ComputeResults(IReadOnlyList<SearchDescriptor> descriptors, out List<SearchDescriptorPlan> plans)
        {
            plans = new List<SearchDescriptorPlan>();
            var view = View;
            if (view == null || descriptors == null || descriptors.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            var columns = BuildColumnInfos();
            if (columns.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            plans = BuildPlans(descriptors, columns);
            if (plans.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            var results = new Dictionary<SearchCellKey, SearchResultBuilder>();

            int rowIndex = 0;
            foreach (var item in view)
            {
                foreach (var plan in plans)
                {
                    for (int i = 0; i < plan.Columns.Count; i++)
                    {
                        var column = plan.Columns[i];
                        var text = GetColumnText(column, item, plan.Descriptor, view);
                        if (string.IsNullOrEmpty(text))
                        {
                            continue;
                        }

                        var matches = SearchTextMatcher.FindMatches(text, plan.PreparedDescriptor);
                        if (matches == null || matches.Count == 0)
                        {
                            continue;
                        }

                        var key = new SearchCellKey(rowIndex, column.Column);
                        if (!results.TryGetValue(key, out var builder))
                        {
                            builder = new SearchResultBuilder(item, rowIndex, column.Column, column.ColumnIndex, text);
                            results.Add(key, builder);
                        }

                        builder.AddMatches(matches);
                    }
                }

                rowIndex++;
            }

            if (results.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            return results.Values
                .Select(r => r.Build())
                .OrderBy(r => r.RowIndex)
                .ThenBy(r => r.ColumnIndex)
                .ThenBy(r => r.Matches.Count > 0 ? r.Matches[0].Start : 0)
                .ToList();
        }

        private List<SearchDescriptorPlan> BuildPlans(
            IReadOnlyList<SearchDescriptor> descriptors,
            List<SearchColumnInfo> columns)
        {
            var plans = new List<SearchDescriptorPlan>();

            foreach (var descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    continue;
                }

                var searchColumns = FilterColumnsForDescriptor(descriptor, columns);
                if (searchColumns.Count == 0)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(descriptor.Query) && !descriptor.AllowEmpty)
                {
                    continue;
                }

                var preparedDescriptor = SearchTextMatcher.Prepare(descriptor);
                if (preparedDescriptor == null)
                {
                    continue;
                }

                plans.Add(new SearchDescriptorPlan(descriptor, searchColumns, preparedDescriptor));
            }

            return plans;
        }

        private List<SearchColumnInfo> BuildColumnInfos()
        {
            var columns = _columnProvider?.Invoke();
            if (columns == null)
            {
                return new List<SearchColumnInfo>();
            }

            var list = new List<SearchColumnInfo>();
            int index = 0;
            foreach (var column in columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (column is DataGridFillerColumn)
                {
                    continue;
                }

                var fallbackIndex = index;
                index++;

                if (!DataGridColumnSearch.GetIsSearchable(column))
                {
                    continue;
                }

                var info = CreateColumnInfo(column, fallbackIndex);
                if (info != null)
                {
                    list.Add(info);
                }

            }

            return list;
        }

        private SearchColumnInfo CreateColumnInfo(DataGridColumn column, int fallbackIndex)
        {
            var textProvider = DataGridColumnSearch.GetTextProvider(column);
            var formatProvider = DataGridColumnSearch.GetFormatProvider(column);
            var searchPath = DataGridColumnSearch.GetSearchMemberPath(column);

            string propertyPath = searchPath;
            if (string.IsNullOrEmpty(propertyPath))
            {
                propertyPath = column.GetSortPropertyName();
            }

            IValueConverter converter = null;
            object converterParameter = null;
            string stringFormat = null;

            if (column is DataGridBoundColumn boundColumn)
            {
                stringFormat = BindingCloneHelper.GetStringFormat(boundColumn.Binding);
                converter = BindingCloneHelper.GetConverter(boundColumn.Binding);
                converterParameter = BindingCloneHelper.GetConverterParameter(boundColumn.Binding);
            }

            Func<object, object> valueGetter = null;
            IDataGridColumnTextAccessor textAccessor = null;
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null)
            {
                valueGetter = accessor.GetValue;
                textAccessor = accessor as IDataGridColumnTextAccessor;
            }

            if (textProvider == null && valueGetter == null)
            {
                if (_throwOnMissingAccessor)
                {
                    _options?.ReportMissingAccessor(
                        DataGridFastPathFeature.Searching,
                        column,
                        DataGridColumnMetadata.GetColumnId(column),
                        $"Search requires a value accessor for column '{column.Header}'.");
                    throw new InvalidOperationException($"Search requires a value accessor for column '{column.Header}'.");
                }

                _options?.ReportMissingAccessor(
                    DataGridFastPathFeature.Searching,
                    column,
                    DataGridColumnMetadata.GetColumnId(column),
                    $"Search skipped because no value accessor was found for column '{column.Header}'.");
                return null;
            }

            var columnIndex = column.Index >= 0 ? column.Index : fallbackIndex;

            return new SearchColumnInfo(
                column,
                propertyPath,
                columnIndex,
                valueGetter,
                textProvider,
                stringFormat,
                converter,
                converterParameter,
                formatProvider,
                textAccessor);
        }

        private List<SearchColumnInfo> FilterColumnsForDescriptor(SearchDescriptor descriptor, List<SearchColumnInfo> columns)
        {
            var result = new List<SearchColumnInfo>();

            foreach (var column in columns)
            {
                if (descriptor.Scope == SearchScope.VisibleColumns && !column.Column.IsVisible)
                {
                    continue;
                }

                if (descriptor.Scope == SearchScope.ExplicitColumns &&
                    !IsColumnSelected(descriptor.ColumnIds, column))
                {
                    continue;
                }

                result.Add(column);
            }

            return result;
        }

        private static bool IsColumnSelected(IReadOnlyList<object> columnIds, SearchColumnInfo column)
        {
            if (columnIds == null || columnIds.Count == 0)
            {
                return false;
            }

            foreach (var id in columnIds)
            {
                if (IsSameColumnId(id, column))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameColumnId(object id, SearchColumnInfo column)
        {
            if (id == null || column == null)
            {
                return false;
            }

            if (DataGridColumnMetadata.MatchesColumnId(column.Column, id))
            {
                return true;
            }

            if (id is string path)
            {
                if (!string.IsNullOrEmpty(column.PropertyPath) &&
                    string.Equals(column.PropertyPath, path, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetColumnText(SearchColumnInfo column, object item, SearchDescriptor descriptor, IDataGridCollectionView view)
        {
            if (item == null || item == DataGridCollectionView.NewItemPlaceholder)
            {
                return null;
            }

            if (item is DataGridCollectionViewGroup)
            {
                return null;
            }

            if (column.TextProvider != null)
            {
                return column.TextProvider(item);
            }

            var culture = descriptor?.Culture ?? view?.Culture ?? CultureInfo.CurrentCulture;
            var provider = column.FormatProvider ?? culture;

            if (column.TextAccessor != null &&
                column.TextAccessor.TryGetText(
                    item,
                    column.Converter,
                    column.ConverterParameter,
                    column.StringFormat,
                    culture,
                    provider,
                    out var accessText))
            {
                return accessText;
            }

            if (column.ValueGetter == null)
            {
                return null;
            }

            var value = column.ValueGetter(item);
            if (value == null)
            {
                return null;
            }

            object formattedValue = value;
            if (column.Converter != null)
            {
                formattedValue = column.Converter.Convert(value, typeof(string), column.ConverterParameter, culture);
            }

            if (!string.IsNullOrEmpty(column.StringFormat))
            {
                return string.Format(provider, column.StringFormat, formattedValue);
            }

            return Convert.ToString(formattedValue, provider);
        }

        private readonly struct SearchCellKey : IEquatable<SearchCellKey>
        {
            public SearchCellKey(int rowIndex, DataGridColumn column)
            {
                RowIndex = rowIndex;
                Column = column;
            }

            public int RowIndex { get; }

            public DataGridColumn Column { get; }

            public bool Equals(SearchCellKey other)
            {
                return RowIndex == other.RowIndex && ReferenceEquals(Column, other.Column);
            }

            public override bool Equals(object obj)
            {
                return obj is SearchCellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RowIndex * 397) ^ (Column?.GetHashCode() ?? 0);
                }
            }
        }

        private sealed class SearchColumnInfo
        {
            public SearchColumnInfo(
                DataGridColumn column,
                string propertyPath,
                int columnIndex,
                Func<object, object> valueGetter,
                Func<object, string> textProvider,
                string stringFormat,
                IValueConverter converter,
                object converterParameter,
                IFormatProvider formatProvider,
                IDataGridColumnTextAccessor textAccessor)
            {
                Column = column;
                PropertyPath = propertyPath;
                ColumnIndex = columnIndex;
                ValueGetter = valueGetter;
                TextProvider = textProvider;
                StringFormat = stringFormat;
                Converter = converter;
                ConverterParameter = converterParameter;
                FormatProvider = formatProvider;
                TextAccessor = textAccessor;
            }

            public DataGridColumn Column { get; }

            public string PropertyPath { get; }

            public int ColumnIndex { get; }

            public Func<object, object> ValueGetter { get; }

            public Func<object, string> TextProvider { get; }

            public string StringFormat { get; }

            public IValueConverter Converter { get; }

            public object ConverterParameter { get; }

            public IFormatProvider FormatProvider { get; }

            public IDataGridColumnTextAccessor TextAccessor { get; }
        }

        private sealed class SearchDescriptorPlan
        {
            public SearchDescriptorPlan(
                SearchDescriptor descriptor,
                List<SearchColumnInfo> columns,
                SearchTextMatcher.PreparedDescriptor preparedDescriptor)
            {
                Descriptor = descriptor;
                Columns = columns;
                PreparedDescriptor = preparedDescriptor;
            }

            public SearchDescriptor Descriptor { get; }

            public List<SearchColumnInfo> Columns { get; }

            public SearchTextMatcher.PreparedDescriptor PreparedDescriptor { get; }
        }

        private sealed class SearchResultBuilder
        {
            private readonly List<SearchMatch> _matches = new();

            public SearchResultBuilder(object item, int rowIndex, DataGridColumn column, int columnIndex, string text)
            {
                Item = item;
                RowIndex = rowIndex;
                Column = column;
                ColumnIndex = columnIndex;
                Text = text;
            }

            public object Item { get; }

            public int RowIndex { get; }

            public DataGridColumn Column { get; }

            public int ColumnIndex { get; }

            public string Text { get; }

            public void AddMatches(IReadOnlyList<SearchMatch> matches)
            {
                if (matches == null || matches.Count == 0)
                {
                    return;
                }

                _matches.AddRange(matches);
            }

            public SearchResult Build()
            {
                var merged = SearchTextMatcher.MergeOverlaps(_matches);
                return new SearchResult(Item, RowIndex, Column, ColumnIndex, Text, merged);
            }
        }

        private static class SearchTextMatcher
        {
            internal sealed class PreparedDescriptor
            {
                public PreparedDescriptor(
                    SearchMatchMode matchMode,
                    SearchTermCombineMode termMode,
                    StringComparison comparison,
                    bool wholeWord,
                    bool normalizeWhitespace,
                    bool ignoreDiacritics,
                    bool allowEmpty,
                    bool hasQuery,
                    bool valid,
                    IReadOnlyList<string> terms,
                    Regex regex)
                {
                    MatchMode = matchMode;
                    TermMode = termMode;
                    Comparison = comparison;
                    WholeWord = wholeWord;
                    NormalizeWhitespace = normalizeWhitespace;
                    IgnoreDiacritics = ignoreDiacritics;
                    AllowEmpty = allowEmpty;
                    HasQuery = hasQuery;
                    Valid = valid;
                    Terms = terms ?? Array.Empty<string>();
                    Regex = regex;
                }

                public SearchMatchMode MatchMode { get; }

                public SearchTermCombineMode TermMode { get; }

                public StringComparison Comparison { get; }

                public bool WholeWord { get; }

                public bool NormalizeWhitespace { get; }

                public bool IgnoreDiacritics { get; }

                public bool AllowEmpty { get; }

                public bool HasQuery { get; }

                public bool Valid { get; }

                public IReadOnlyList<string> Terms { get; }

                public Regex Regex { get; }
            }

            public static PreparedDescriptor Prepare(SearchDescriptor descriptor)
            {
                if (descriptor == null)
                {
                    return null;
                }

                var comparison = descriptor.Comparison ?? StringComparison.OrdinalIgnoreCase;
                var hasQuery = !string.IsNullOrEmpty(descriptor.Query);

                if (!hasQuery)
                {
                    return new PreparedDescriptor(
                        descriptor.MatchMode,
                        descriptor.TermMode,
                        comparison,
                        descriptor.WholeWord,
                        descriptor.NormalizeWhitespace,
                        descriptor.IgnoreDiacritics,
                        descriptor.AllowEmpty,
                        hasQuery: false,
                        valid: true,
                        terms: Array.Empty<string>(),
                        regex: null);
                }

                var normalizedQuery = NormalizeQuery(descriptor.Query, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
                if (descriptor.MatchMode == SearchMatchMode.Regex || descriptor.MatchMode == SearchMatchMode.Wildcard)
                {
                    var pattern = descriptor.MatchMode == SearchMatchMode.Wildcard
                        ? WildcardToRegex(normalizedQuery)
                        : normalizedQuery;

                    if (descriptor.WholeWord)
                    {
                        pattern = $@"\b(?:{pattern})\b";
                    }

                    var options = RegexOptions.Compiled;
                    if (IsIgnoreCase(comparison))
                    {
                        options |= RegexOptions.IgnoreCase;
                    }

                    if (IsCultureInvariant(comparison))
                    {
                        options |= RegexOptions.CultureInvariant;
                    }

                    try
                    {
                        var regex = new Regex(pattern, options);
                        return new PreparedDescriptor(
                            descriptor.MatchMode,
                            descriptor.TermMode,
                            comparison,
                            descriptor.WholeWord,
                            descriptor.NormalizeWhitespace,
                            descriptor.IgnoreDiacritics,
                            descriptor.AllowEmpty,
                            hasQuery: true,
                            valid: true,
                            terms: Array.Empty<string>(),
                            regex: regex);
                    }
                    catch (ArgumentException)
                    {
                        return new PreparedDescriptor(
                            descriptor.MatchMode,
                            descriptor.TermMode,
                            comparison,
                            descriptor.WholeWord,
                            descriptor.NormalizeWhitespace,
                            descriptor.IgnoreDiacritics,
                            descriptor.AllowEmpty,
                            hasQuery: true,
                            valid: false,
                            terms: Array.Empty<string>(),
                            regex: null);
                    }
                }

                var terms = Tokenize(normalizedQuery);
                return new PreparedDescriptor(
                    descriptor.MatchMode,
                    descriptor.TermMode,
                    comparison,
                    descriptor.WholeWord,
                    descriptor.NormalizeWhitespace,
                    descriptor.IgnoreDiacritics,
                    descriptor.AllowEmpty,
                    hasQuery: true,
                    valid: true,
                    terms: terms,
                    regex: null);
            }

            public static IReadOnlyList<SearchMatch> FindMatches(string text, SearchDescriptor descriptor)
            {
                return FindMatches(text, Prepare(descriptor));
            }

            public static IReadOnlyList<SearchMatch> FindMatches(string text, PreparedDescriptor descriptor)
            {
                if (descriptor == null)
                {
                    return Array.Empty<SearchMatch>();
                }

                if (string.IsNullOrEmpty(text))
                {
                    return Array.Empty<SearchMatch>();
                }

                if (!descriptor.HasQuery)
                {
                    if (!descriptor.AllowEmpty)
                    {
                        return Array.Empty<SearchMatch>();
                    }

                    return text.Length == 0 ? Array.Empty<SearchMatch>() : new[] { new SearchMatch(0, text.Length) };
                }

                if (!descriptor.Valid)
                {
                    return Array.Empty<SearchMatch>();
                }

                var normalized = NormalizeText(text, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
                if (descriptor.MatchMode == SearchMatchMode.Regex || descriptor.MatchMode == SearchMatchMode.Wildcard)
                {
                    if (descriptor.Regex == null)
                    {
                        return Array.Empty<SearchMatch>();
                    }

                    var matches = new List<SearchMatch>();
                    foreach (Match match in descriptor.Regex.Matches(normalized.Text))
                    {
                        if (!match.Success || match.Length == 0)
                        {
                            continue;
                        }

                        matches.Add(new SearchMatch(match.Index, match.Length));
                    }

                    return MapMatches(matches, normalized.Map);
                }

                if (descriptor.Terms.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var collected = new List<SearchMatch>();
                foreach (var term in descriptor.Terms)
                {
                    if (string.IsNullOrEmpty(term))
                    {
                        continue;
                    }

                    var termMatches = FindTermMatches(normalized.Text, term, descriptor.MatchMode, descriptor.Comparison, descriptor.WholeWord);
                    if (termMatches.Count == 0)
                    {
                        if (descriptor.TermMode == SearchTermCombineMode.All)
                        {
                            return Array.Empty<SearchMatch>();
                        }

                        continue;
                    }

                    collected.AddRange(termMatches);
                }

                if (collected.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var merged = MergeOverlaps(collected);
                return MapMatches(merged, normalized.Map);
            }

            public static IReadOnlyList<SearchMatch> MergeOverlaps(IReadOnlyList<SearchMatch> matches)
            {
                if (matches == null || matches.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var ordered = matches
                    .Where(m => m != null && m.Length > 0)
                    .OrderBy(m => m.Start)
                    .ThenBy(m => m.Length)
                    .ToList();

                if (ordered.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var merged = new List<SearchMatch> { ordered[0] };

                for (int i = 1; i < ordered.Count; i++)
                {
                    var current = ordered[i];
                    var last = merged[merged.Count - 1];
                    var lastEndExclusive = last.Start + last.Length;

                    if (current.Start < lastEndExclusive)
                    {
                        var currentEnd = current.Start + current.Length;
                        var newEnd = Math.Max(lastEndExclusive, currentEnd);
                        merged[merged.Count - 1] = new SearchMatch(last.Start, newEnd - last.Start);
                    }
                    else
                    {
                        merged.Add(current);
                    }
                }

                return merged;
            }

            private static List<SearchMatch> FindTermMatches(
                string text,
                string term,
                SearchMatchMode mode,
                StringComparison comparison,
                bool wholeWord)
            {
                var matches = new List<SearchMatch>();

                switch (mode)
                {
                    case SearchMatchMode.StartsWith:
                        if (text.StartsWith(term, comparison) && IsWholeWord(text, 0, term.Length, wholeWord))
                        {
                            matches.Add(new SearchMatch(0, term.Length));
                        }
                        break;
                    case SearchMatchMode.EndsWith:
                        if (text.EndsWith(term, comparison))
                        {
                            var start = text.Length - term.Length;
                            if (IsWholeWord(text, start, term.Length, wholeWord))
                            {
                                matches.Add(new SearchMatch(start, term.Length));
                            }
                        }
                        break;
                    case SearchMatchMode.Equals:
                        if (string.Equals(text, term, comparison))
                        {
                            matches.Add(new SearchMatch(0, term.Length));
                        }
                        break;
                    case SearchMatchMode.Contains:
                        AppendMatches(text, term, comparison, wholeWord, matches);
                        break;
                    default:
                        AppendMatches(text, term, comparison, wholeWord, matches);
                        break;
                }

                return matches;
            }

            private static void AppendMatches(
                string text,
                string term,
                StringComparison comparison,
                bool wholeWord,
                List<SearchMatch> matches)
            {
                if (string.IsNullOrEmpty(term))
                {
                    return;
                }

                int index = 0;
                while (index >= 0)
                {
                    index = text.IndexOf(term, index, comparison);
                    if (index < 0)
                    {
                        break;
                    }

                    if (IsWholeWord(text, index, term.Length, wholeWord))
                    {
                        matches.Add(new SearchMatch(index, term.Length));
                    }

                    index += term.Length;
                }
            }

            private static bool IsWholeWord(string text, int start, int length, bool wholeWord)
            {
                if (!wholeWord)
                {
                    return true;
                }

                var end = start + length;

                if (start > 0)
                {
                    var prev = text[start - 1];
                    if (char.IsLetterOrDigit(prev) || prev == '_')
                    {
                        return false;
                    }
                }

                if (end < text.Length)
                {
                    var next = text[end];
                    if (char.IsLetterOrDigit(next) || next == '_')
                    {
                        return false;
                    }
                }

                return true;
            }

            private static NormalizedText NormalizeText(string text, bool normalizeWhitespace, bool ignoreDiacritics)
            {
                if (!normalizeWhitespace && !ignoreDiacritics)
                {
                    return new NormalizedText(text, null);
                }

                var needsWhitespaceNormalization = NeedsWhitespaceNormalization(text, normalizeWhitespace);
                if (!ignoreDiacritics && !needsWhitespaceNormalization)
                {
                    return new NormalizedText(text, null);
                }

                var isAscii = ignoreDiacritics && IsAscii(text);
                if (ignoreDiacritics && isAscii && !needsWhitespaceNormalization)
                {
                    return new NormalizedText(text, null);
                }

                var builder = new StringBuilder(text.Length);
                var map = new List<int>(text.Length);
                bool wasWhitespace = false;

                for (int i = 0; i < text.Length; i++)
                {
                    var source = text[i];
                    if (ignoreDiacritics && !isAscii && source > 0x7F)
                    {
                        var normalized = source.ToString().Normalize(NormalizationForm.FormD);
                        foreach (var nc in normalized)
                        {
                            if (CharUnicodeInfo.GetUnicodeCategory(nc) == UnicodeCategory.NonSpacingMark)
                            {
                                continue;
                            }

                            AppendNormalizedCharacter(builder, map, nc, i, normalizeWhitespace, ref wasWhitespace);
                        }
                    }
                    else
                    {
                        AppendNormalizedCharacter(builder, map, source, i, normalizeWhitespace, ref wasWhitespace);
                    }
                }

                return new NormalizedText(builder.ToString(), map.ToArray());
            }

            private static void AppendNormalizedCharacter(
                StringBuilder builder,
                List<int> map,
                char ch,
                int sourceIndex,
                bool normalizeWhitespace,
                ref bool wasWhitespace)
            {
                if (normalizeWhitespace && char.IsWhiteSpace(ch))
                {
                    if (wasWhitespace)
                    {
                        return;
                    }

                    ch = ' ';
                    wasWhitespace = true;
                }
                else
                {
                    wasWhitespace = false;
                }

                map.Add(sourceIndex);
                builder.Append(ch);
            }

            private static bool NeedsWhitespaceNormalization(string text, bool normalizeWhitespace)
            {
                if (!normalizeWhitespace)
                {
                    return false;
                }

                bool wasWhitespace = false;
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (!char.IsWhiteSpace(ch))
                    {
                        wasWhitespace = false;
                        continue;
                    }

                    if (ch != ' ' || wasWhitespace)
                    {
                        return true;
                    }

                    wasWhitespace = true;
                }

                return false;
            }

            private static bool IsAscii(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] > 0x7F)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static string NormalizeQuery(string query, bool normalizeWhitespace, bool ignoreDiacritics)
            {
                if (!normalizeWhitespace && !ignoreDiacritics)
                {
                    return query;
                }

                var normalized = NormalizeText(query, normalizeWhitespace, ignoreDiacritics);
                return normalized.Text;
            }

            private static List<string> Tokenize(string text)
            {
                var list = new List<string>();
                var split = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < split.Length; i++)
                {
                    list.Add(split[i]);
                }

                return list;
            }

            private static bool IsIgnoreCase(StringComparison? comparison)
            {
                return comparison == StringComparison.OrdinalIgnoreCase
                    || comparison == StringComparison.InvariantCultureIgnoreCase
                    || comparison == StringComparison.CurrentCultureIgnoreCase;
            }

            private static bool IsCultureInvariant(StringComparison? comparison)
            {
                if (!comparison.HasValue)
                {
                    return true;
                }

                switch (comparison.Value)
                {
                    case StringComparison.Ordinal:
                    case StringComparison.OrdinalIgnoreCase:
                    case StringComparison.InvariantCulture:
                    case StringComparison.InvariantCultureIgnoreCase:
                        return true;
                    default:
                        return false;
                }
            }

            private static string WildcardToRegex(string pattern)
            {
                return Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
            }

            private static IReadOnlyList<SearchMatch> MapMatches(IReadOnlyList<SearchMatch> matches, int[] map)
            {
                if (map == null || map.Length == 0)
                {
                    return matches ?? Array.Empty<SearchMatch>();
                }

                if (matches == null || matches.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var mapped = new List<SearchMatch>();
                foreach (var match in matches)
                {
                    if (match == null || match.Length == 0)
                    {
                        continue;
                    }

                    var start = match.Start;
                    var end = match.Start + match.Length - 1;

                    if (start >= map.Length || end >= map.Length)
                    {
                        continue;
                    }

                    var mappedStart = map[start];
                    var mappedEnd = map[end];
                    var length = mappedEnd - mappedStart + 1;
                    if (length <= 0)
                    {
                        continue;
                    }

                    mapped.Add(new SearchMatch(mappedStart, length));
                }

                return mapped.Count == 0 ? Array.Empty<SearchMatch>() : mapped;
            }

            private readonly struct NormalizedText
            {
                public NormalizedText(string text, int[] map)
                {
                    Text = text;
                    Map = map;
                }

                public string Text { get; }

                public int[] Map { get; }
            }
        }
    }
}
