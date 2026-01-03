// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.DataGridSearching;
using DataGridSample.Models;

namespace DataGridSample.Adapters
{
    /// <summary>
    /// Adapter factory that translates SearchModel descriptors into a DynamicData search predicate.
    /// It can push search criteria upstream while letting the grid compute match highlighting.
    /// </summary>
    public sealed class DynamicDataHierarchicalSearchAdapterFactory : IDataGridSearchAdapterFactory
    {
        private const string ItemPrefix = "Item.";
        private readonly Action<string> _log;

        public DynamicDataHierarchicalSearchAdapterFactory(Action<string> log)
        {
            _log = log;
            SearchItemPredicate = static _ => true;
            SearchPredicate = static _ => true;
        }

        public Func<HierarchicalStreamingItem, bool> SearchItemPredicate { get; private set; }

        public Func<HierarchicalStreamingItem, bool> SearchPredicate { get; private set; }

        public DataGridSearchAdapter Create(DataGrid grid, ISearchModel model)
        {
            return new DynamicDataSearchAdapter(model, () => grid.ColumnDefinitions, UpdatePredicate, _log);
        }

        public void UpdatePredicate(IReadOnlyList<SearchDescriptor> descriptors)
        {
            SearchItemPredicate = BuildItemPredicate(descriptors);
            SearchPredicate = item => MatchesAny(item, SearchItemPredicate);
            _log($"Upstream search updated: {Describe(descriptors)}");
        }

        private static Func<HierarchicalStreamingItem, bool> BuildItemPredicate(IReadOnlyList<SearchDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return static _ => true;
            }

            var compiled = new List<Func<HierarchicalStreamingItem, bool>>();
            foreach (var descriptor in descriptors)
            {
                var predicate = Compile(descriptor);
                if (predicate != null)
                {
                    compiled.Add(predicate);
                }
            }

            if (compiled.Count == 0)
            {
                return static _ => true;
            }

            return item =>
            {
                for (int i = 0; i < compiled.Count; i++)
                {
                    if (compiled[i](item))
                    {
                        return true;
                    }
                }

                return false;
            };
        }

        private static bool MatchesAny(HierarchicalStreamingItem root, Func<HierarchicalStreamingItem, bool> predicate)
        {
            if (predicate(root))
            {
                return true;
            }

            if (root.Children.Count == 0)
            {
                return false;
            }

            var stack = new Stack<HierarchicalStreamingItem>(root.Children);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (predicate(current))
                {
                    return true;
                }

                if (current.Children.Count == 0)
                {
                    continue;
                }

                for (int i = 0; i < current.Children.Count; i++)
                {
                    stack.Push(current.Children[i]);
                }
            }

            return false;
        }

        private static Func<HierarchicalStreamingItem, bool>? Compile(SearchDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            var columns = SelectColumns(descriptor);
            if (columns.Count == 0)
            {
                return null;
            }

            return item =>
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    var text = columns[i].Getter(item);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    if (TextMatcher.HasMatch(text, descriptor))
                    {
                        return true;
                    }
                }

                return false;
            };
        }

        private static List<ColumnSelector> SelectColumns(SearchDescriptor descriptor)
        {
            if (descriptor.Scope != SearchScope.ExplicitColumns)
            {
                return Columns.ToList();
            }

            if (descriptor.ColumnIds == null || descriptor.ColumnIds.Count == 0)
            {
                return new List<ColumnSelector>();
            }

            var selected = new List<ColumnSelector>();
            foreach (var id in descriptor.ColumnIds)
            {
                if (id is not string path)
                {
                    continue;
                }

                var normalized = NormalizePath(path);
                var column = Columns.FirstOrDefault(c =>
                    string.Equals(c.Id, path, StringComparison.Ordinal) ||
                    string.Equals(c.NormalizedId, normalized, StringComparison.Ordinal));
                if (column != null)
                {
                    selected.Add(column);
                }
            }

            return selected;
        }

        private static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (path.StartsWith(ItemPrefix, StringComparison.Ordinal))
            {
                return path.Substring(ItemPrefix.Length);
            }

            return string.Equals(path, "Item", StringComparison.Ordinal) ? null : path;
        }

        private static readonly ColumnSelector[] Columns =
        {
            new("Item.Id", nameof(HierarchicalStreamingItem.Id), item => item.Id.ToString(CultureInfo.InvariantCulture)),
            new("Item.Name", nameof(HierarchicalStreamingItem.Name), item => item.Name),
            new("Item.Price", nameof(HierarchicalStreamingItem.Price), item => item.PriceDisplay),
            new("Item.UpdatedAt", nameof(HierarchicalStreamingItem.UpdatedAt), item => item.UpdatedAtDisplay)
        };

        private static string Describe(IReadOnlyList<SearchDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return "(none)";
            }

            return string.Join(", ", descriptors.Where(d => d != null).Select(d =>
                $"{d.Query} ({d.MatchMode}, {d.Scope})"));
        }

        private sealed class DynamicDataSearchAdapter : DataGridSearchAdapter
        {
            private readonly Action<IReadOnlyList<SearchDescriptor>> _update;
            private readonly Action<string> _log;

            public DynamicDataSearchAdapter(
                ISearchModel model,
                Func<IEnumerable<DataGridColumn>> columns,
                Action<IReadOnlyList<SearchDescriptor>> update,
                Action<string> log)
                : base(model, columns)
            {
                _update = update;
                _log = log;
            }

            protected override bool TryApplyModelToView(
                IReadOnlyList<SearchDescriptor> descriptors,
                IReadOnlyList<SearchDescriptor> previousDescriptors,
                out IReadOnlyList<SearchResult> results)
            {
                _update(descriptors);
                _log($"Applied to DynamicData: {Describe(descriptors)}");
                results = Array.Empty<SearchResult>();
                return false;
            }
        }

        private sealed class ColumnSelector
        {
            public ColumnSelector(string id, string normalizedId, Func<HierarchicalStreamingItem, string> getter)
            {
                Id = id;
                NormalizedId = normalizedId;
                Getter = getter;
            }

            public string Id { get; }

            public string NormalizedId { get; }

            public Func<HierarchicalStreamingItem, string> Getter { get; }
        }

        private static class TextMatcher
        {
            public static bool HasMatch(string text, SearchDescriptor descriptor)
            {
                if (descriptor == null || string.IsNullOrEmpty(text))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(descriptor.Query))
                {
                    return descriptor.AllowEmpty && text.Length > 0;
                }

                var normalizedText = NormalizeText(text, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
                var query = NormalizeQuery(descriptor.Query, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);

                if (descriptor.MatchMode == SearchMatchMode.Regex || descriptor.MatchMode == SearchMatchMode.Wildcard)
                {
                    var pattern = descriptor.MatchMode == SearchMatchMode.Wildcard
                        ? WildcardToRegex(query)
                        : query;

                    if (descriptor.WholeWord)
                    {
                        pattern = $@"\b(?:{pattern})\b";
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
                        return Regex.IsMatch(normalizedText, pattern, options);
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                }

                var terms = Tokenize(query);
                if (terms.Count == 0)
                {
                    return false;
                }

                var comparison = descriptor.Comparison ?? StringComparison.OrdinalIgnoreCase;
                bool any = descriptor.TermMode == SearchTermCombineMode.Any;

                foreach (var term in terms)
                {
                    if (string.IsNullOrEmpty(term))
                    {
                        continue;
                    }

                    var matched = FindTermMatch(normalizedText, term, descriptor.MatchMode, comparison, descriptor.WholeWord);
                    if (matched && any)
                    {
                        return true;
                    }

                    if (!matched && !any)
                    {
                        return false;
                    }
                }

                return !any;
            }

            private static bool FindTermMatch(
                string text,
                string term,
                SearchMatchMode mode,
                StringComparison comparison,
                bool wholeWord)
            {
                switch (mode)
                {
                    case SearchMatchMode.StartsWith:
                        return text.StartsWith(term, comparison) && IsWholeWord(text, 0, term.Length, wholeWord);
                    case SearchMatchMode.EndsWith:
                        if (!text.EndsWith(term, comparison))
                        {
                            return false;
                        }

                        var start = text.Length - term.Length;
                        return IsWholeWord(text, start, term.Length, wholeWord);
                    case SearchMatchMode.Equals:
                        return string.Equals(text, term, comparison) && IsWholeWord(text, 0, term.Length, wholeWord);
                    default:
                        return FindAllOccurrences(text, term, comparison, wholeWord);
                }
            }

            private static bool FindAllOccurrences(
                string text,
                string term,
                StringComparison comparison,
                bool wholeWord)
            {
                if (string.IsNullOrEmpty(term))
                {
                    return false;
                }

                int startIndex = 0;
                while (startIndex < text.Length)
                {
                    int index = text.IndexOf(term, startIndex, comparison);
                    if (index < 0)
                    {
                        break;
                    }

                    if (IsWholeWord(text, index, term.Length, wholeWord))
                    {
                        return true;
                    }

                    startIndex = index + term.Length;
                }

                return false;
            }

            private static bool IsWholeWord(string text, int start, int length, bool wholeWord)
            {
                if (!wholeWord)
                {
                    return true;
                }

                bool startBoundary = start == 0 || !IsWordChar(text[start - 1]);
                int endIndex = start + length;
                bool endBoundary = endIndex >= text.Length || !IsWordChar(text[endIndex]);

                return startBoundary && endBoundary;
            }

            private static bool IsWordChar(char c)
            {
                return char.IsLetterOrDigit(c) || c == '_';
            }

            private static List<string> Tokenize(string query)
            {
                var terms = new List<string>();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return terms;
                }

                var builder = new StringBuilder();
                bool inQuote = false;

                foreach (var ch in query)
                {
                    if (ch == '"')
                    {
                        inQuote = !inQuote;
                        continue;
                    }

                    if (!inQuote && char.IsWhiteSpace(ch))
                    {
                        Flush(builder, terms);
                        continue;
                    }

                    builder.Append(ch);
                }

                Flush(builder, terms);
                return terms;
            }

            private static void Flush(StringBuilder builder, List<string> terms)
            {
                if (builder.Length == 0)
                {
                    return;
                }

                var term = builder.ToString().Trim();
                if (!string.IsNullOrEmpty(term))
                {
                    terms.Add(term);
                }

                builder.Clear();
            }

            private static string NormalizeText(string text, bool normalizeWhitespace, bool ignoreDiacritics)
            {
                if (!normalizeWhitespace && !ignoreDiacritics)
                {
                    return text;
                }

                var chars = new List<char>();
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (ignoreDiacritics)
                    {
                        var decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
                        foreach (var d in decomposed)
                        {
                            if (IsDiacritic(d))
                            {
                                continue;
                            }

                            chars.Add(d);
                        }
                    }
                    else
                    {
                        chars.Add(ch);
                    }
                }

                if (!normalizeWhitespace)
                {
                    return new string(chars.ToArray());
                }

                var builder = new StringBuilder();
                bool wasWhitespace = false;

                for (int i = 0; i < chars.Count; i++)
                {
                    var ch = chars[i];
                    bool isWhitespace = char.IsWhiteSpace(ch);
                    if (isWhitespace)
                    {
                        if (wasWhitespace)
                        {
                            continue;
                        }

                        builder.Append(' ');
                        wasWhitespace = true;
                    }
                    else
                    {
                        builder.Append(ch);
                        wasWhitespace = false;
                    }
                }

                return builder.ToString();
            }

            private static string NormalizeQuery(string query, bool normalizeWhitespace, bool ignoreDiacritics)
            {
                if (!normalizeWhitespace && !ignoreDiacritics)
                {
                    return query;
                }

                var normalized = NormalizeText(query, normalizeWhitespace, ignoreDiacritics);
                return normalizeWhitespace ? normalized.Trim() : normalized;
            }

            private static bool IsDiacritic(char ch)
            {
                return CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark;
            }

            private static string WildcardToRegex(string pattern)
            {
                var builder = new StringBuilder();
                foreach (var ch in pattern)
                {
                    switch (ch)
                    {
                        case '*':
                            builder.Append(".*");
                            break;
                        case '?':
                            builder.Append(".");
                            break;
                        default:
                            builder.Append(Regex.Escape(ch.ToString()));
                            break;
                    }
                }

                return builder.ToString();
            }

            private static bool IsIgnoreCase(StringComparison? comparison)
            {
                if (!comparison.HasValue)
                {
                    return true;
                }

                switch (comparison.Value)
                {
                    case StringComparison.CurrentCultureIgnoreCase:
                    case StringComparison.InvariantCultureIgnoreCase:
                    case StringComparison.OrdinalIgnoreCase:
                        return true;
                    default:
                        return false;
                }
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
        }
    }
}
