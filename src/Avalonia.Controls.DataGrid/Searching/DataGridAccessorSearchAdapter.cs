// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.MarkupExtensions;

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

        public DataGridAccessorSearchAdapter(
            ISearchModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider,
            DataGridFastPathOptions options = null)
            : base(model, columnProvider)
        {
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));
            _throwOnMissingAccessor = options?.ThrowOnMissingAccessor ?? false;
            _options = options;
        }

        protected override bool TryApplyModelToView(
            IReadOnlyList<SearchDescriptor> descriptors,
            IReadOnlyList<SearchDescriptor> previousDescriptors,
            out IReadOnlyList<SearchResult> results)
        {
            results = ComputeResults(descriptors);
            return true;
        }

        private IReadOnlyList<SearchResult> ComputeResults(IReadOnlyList<SearchDescriptor> descriptors)
        {
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

            var plans = BuildPlans(descriptors, columns);
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

                        var matches = SearchTextMatcher.FindMatches(text, plan.Descriptor);
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

                plans.Add(new SearchDescriptorPlan(descriptor, searchColumns));
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
                if (boundColumn.Binding is Binding binding)
                {
                    stringFormat = binding.StringFormat;
                    converter = binding.Converter;
                    converterParameter = binding.ConverterParameter;
                }
                else if (boundColumn.Binding is CompiledBindingExtension compiledBinding)
                {
                    stringFormat = compiledBinding.StringFormat;
                    converter = compiledBinding.Converter;
                    converterParameter = compiledBinding.ConverterParameter;
                }
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
            public SearchDescriptorPlan(SearchDescriptor descriptor, List<SearchColumnInfo> columns)
            {
                Descriptor = descriptor;
                Columns = columns;
            }

            public SearchDescriptor Descriptor { get; }

            public List<SearchColumnInfo> Columns { get; }
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
            public static IReadOnlyList<SearchMatch> FindMatches(string text, SearchDescriptor descriptor)
            {
                if (descriptor == null)
                {
                    return Array.Empty<SearchMatch>();
                }

                if (string.IsNullOrEmpty(text))
                {
                    return Array.Empty<SearchMatch>();
                }

                if (string.IsNullOrEmpty(descriptor.Query))
                {
                    if (!descriptor.AllowEmpty)
                    {
                        return Array.Empty<SearchMatch>();
                    }

                    return text.Length == 0 ? Array.Empty<SearchMatch>() : new[] { new SearchMatch(0, text.Length) };
                }

                var normalized = NormalizeText(text, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
                var query = NormalizeQuery(descriptor.Query, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);

                if (descriptor.MatchMode == SearchMatchMode.Regex || descriptor.MatchMode == SearchMatchMode.Wildcard)
                {
                    var pattern = descriptor.MatchMode == SearchMatchMode.Wildcard
                        ? WildcardToRegex(query)
                        : query;

                    if (descriptor.WholeWord)
                    {
                        pattern = $@"\\b(?:{pattern})\\b";
                    }

                    var options = RegexOptions.Compiled;
                    if (IsIgnoreCase(descriptor.Comparison))
                    {
                        options |= RegexOptions.IgnoreCase;
                    }

                    if (IsCultureInvariant(descriptor.Comparison))
                    {
                        options |= RegexOptions.CultureInvariant;
                    }

                    try
                    {
                        var matches = new List<SearchMatch>();
                        foreach (Match match in Regex.Matches(normalized.Text, pattern, options))
                        {
                            if (!match.Success || match.Length == 0)
                            {
                                continue;
                            }

                            matches.Add(new SearchMatch(match.Index, match.Length));
                        }

                        return MapMatches(matches, normalized.Map);
                    }
                    catch (ArgumentException)
                    {
                        return Array.Empty<SearchMatch>();
                    }
                }

                var terms = Tokenize(query);
                if (terms.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                var comparison = descriptor.Comparison ?? StringComparison.OrdinalIgnoreCase;
                var collected = new List<SearchMatch>();

                foreach (var term in terms)
                {
                    if (string.IsNullOrEmpty(term))
                    {
                        continue;
                    }

                    var termMatches = FindTermMatches(normalized.Text, term, descriptor.MatchMode, comparison, descriptor.WholeWord);
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

                var builder = new StringBuilder(text.Length);
                List<int> map = null;

                int lastWhitespace = -1;
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];

                    if (normalizeWhitespace && char.IsWhiteSpace(ch))
                    {
                        if (lastWhitespace >= 0)
                        {
                            continue;
                        }

                        lastWhitespace = builder.Length;
                        ch = ' ';
                    }
                    else
                    {
                        lastWhitespace = -1;
                    }

                    if (ignoreDiacritics)
                    {
                        var normalized = ch.ToString().Normalize(NormalizationForm.FormD);
                        foreach (var nc in normalized)
                        {
                            if (CharUnicodeInfo.GetUnicodeCategory(nc) != UnicodeCategory.NonSpacingMark)
                            {
                                if (map == null)
                                {
                                    map = new List<int>(text.Length);
                                }

                                map.Add(i);
                                builder.Append(nc);
                            }
                        }
                    }
                    else
                    {
                        if (map == null)
                        {
                            map = new List<int>(text.Length);
                        }

                        map.Add(i);
                        builder.Append(ch);
                    }
                }

                return new NormalizedText(builder.ToString(), map?.ToArray());
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
                return comparison == StringComparison.InvariantCulture
                    || comparison == StringComparison.InvariantCultureIgnoreCase;
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
