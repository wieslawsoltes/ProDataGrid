// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    /// <summary>
    /// Creates reflection-free column definitions for items of a known type.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridColumnDefinitionProvider<TItem>
    {
        /// <summary>
        /// Creates a mutable set of column definitions for a grid instance.
        /// </summary>
        DataGridColumnDefinitionList CreateColumnDefinitions();
    }

    /// <summary>
    /// Compiles sorting descriptors into a typed comparer without property-path reflection.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridSortingCompiler<TItem>
    {
        /// <summary>
        /// Compiles model descriptors into a reflection-free item comparer.
        /// </summary>
        IComparer<TItem> CreateSortComparer(IReadOnlyList<SortingDescriptor> descriptors);
    }

    /// <summary>
    /// Compiles filtering descriptors into a typed predicate without property-path reflection.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridFilteringCompiler<TItem>
    {
        /// <summary>
        /// Compiles model descriptors into a reflection-free item predicate.
        /// </summary>
        Func<TItem, bool> CreateFilterPredicate(IReadOnlyList<FilteringDescriptor> descriptors);
    }

    /// <summary>
    /// Compiles search descriptors into a typed predicate without property-path reflection.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridSearchingCompiler<TItem>
    {
        /// <summary>
        /// Compiles model descriptors into a reflection-free item predicate.
        /// </summary>
        Func<TItem, bool> CreateSearchPredicate(IReadOnlyList<SearchDescriptor> descriptors);
    }

    /// <summary>
    /// Creates grid fast-path options associated with a generated or user-defined schema.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridFastPathOptionsProvider
    {
        /// <summary>
        /// Creates fast-path options appropriate for the generated schema.
        /// </summary>
        DataGridFastPathOptions CreateFastPathOptions();
    }

    /// <summary>
    /// Combines column creation, descriptor compilation, and fast-path configuration for an item type.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedSchema<TItem> :
        IDataGridColumnDefinitionProvider<TItem>,
        IDataGridSortingCompiler<TItem>,
        IDataGridFilteringCompiler<TItem>,
        IDataGridSearchingCompiler<TItem>,
        IDataGridFastPathOptionsProvider
    {
    }

    /// <summary>
    /// Associates a stable model identifier with a generated value accessor.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridColumnAccessorRegistration
    {
        /// <summary>
        /// Initializes a new accessor registration.
        /// </summary>
        public DataGridColumnAccessorRegistration(
            object columnId,
            string propertyPath,
            IDataGridColumnValueAccessor accessor,
            bool isSearchable = true)
        {
            ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
            PropertyPath = propertyPath;
            Accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            IsSearchable = isSearchable;
        }

        /// <summary>
        /// Gets the stable identifier used by sorting, filtering, and search descriptors.
        /// </summary>
        public object ColumnId { get; }

        /// <summary>
        /// Gets the optional property-path alias used only as a descriptor key.
        /// </summary>
        public string PropertyPath { get; }

        /// <summary>
        /// Gets the reflection-free accessor.
        /// </summary>
        public IDataGridColumnValueAccessor Accessor { get; }

        /// <summary>
        /// Gets a value indicating whether global searches include the accessor.
        /// </summary>
        public bool IsSearchable { get; }

        internal bool Matches(object columnId, string propertyPath)
        {
            if (!string.IsNullOrEmpty(propertyPath) &&
                (string.Equals(PropertyPath, propertyPath, StringComparison.Ordinal) ||
                 Equals(ColumnId, propertyPath)))
            {
                return true;
            }

            object normalized = NormalizeColumnId(columnId);
            return Equals(ColumnId, normalized) ||
                   (!string.IsNullOrEmpty(PropertyPath) && Equals(PropertyPath, normalized));
        }

        private static object NormalizeColumnId(object columnId)
        {
            if (columnId is DataGridColumnDefinition definition)
            {
                return definition.ColumnKey ?? definition.SortMemberPath ?? definition.Header;
            }

            if (columnId is DataGridColumn column)
            {
                return column.ColumnKey ?? column.SortMemberPath ?? column.Header;
            }

            return columnId;
        }
    }

    /// <summary>
    /// Compiles descriptor models to accessor-based delegates suitable for collection views,
    /// streaming sources, server-side adapters, and DynamicData pipelines.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedDataOperations<TItem> :
        IDataGridSortingCompiler<TItem>,
        IDataGridFilteringCompiler<TItem>,
        IDataGridSearchingCompiler<TItem>
    {
        private static readonly IComparer<TItem> s_noSort = Comparer<TItem>.Create(static (_, _) => 0);
        private static readonly Func<TItem, bool> s_matchAll = static _ => true;
        private readonly DataGridColumnAccessorRegistration[] _columns;

        /// <summary>
        /// Initializes operations from generated or user-defined accessor registrations.
        /// </summary>
        public DataGridGeneratedDataOperations(IReadOnlyList<DataGridColumnAccessorRegistration> columns)
        {
            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            _columns = new DataGridColumnAccessorRegistration[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                _columns[i] = columns[i] ?? throw new ArgumentException("Accessor registrations cannot contain null entries.", nameof(columns));
            }
        }

        /// <inheritdoc />
        public IComparer<TItem> CreateSortComparer(IReadOnlyList<SortingDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return s_noSort;
            }

            var entries = new List<SortEntry>(descriptors.Count);
            for (int i = 0; i < descriptors.Count; i++)
            {
                SortingDescriptor descriptor = descriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                DataGridColumnAccessorRegistration registration = Find(descriptor.ColumnId, descriptor.PropertyPath);
                if (registration == null)
                {
                    continue;
                }

                IComparer comparer = DataGridColumnValueAccessorComparer.Create(
                    registration.Accessor,
                    descriptor.Culture ?? CultureInfo.CurrentCulture,
                    descriptor.Comparer);

                entries.Add(new SortEntry(comparer, descriptor.Direction));
            }

            if (entries.Count == 0)
            {
                return s_noSort;
            }

            SortEntry[] compiled = entries.ToArray();
            return Comparer<TItem>.Create((left, right) => Compare(compiled, left, right));
        }

        /// <inheritdoc />
        public Func<TItem, bool> CreateFilterPredicate(IReadOnlyList<FilteringDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return s_matchAll;
            }

            var entries = new List<FilterEntry>(descriptors.Count);
            for (int i = 0; i < descriptors.Count; i++)
            {
                FilteringDescriptor descriptor = descriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                if (descriptor.Predicate != null)
                {
                    entries.Add(new FilterEntry(descriptor, null));
                    continue;
                }

                DataGridColumnAccessorRegistration registration = Find(descriptor.ColumnId, descriptor.PropertyPath);
                if (registration != null)
                {
                    entries.Add(new FilterEntry(descriptor, registration.Accessor));
                }
            }

            if (entries.Count == 0)
            {
                return s_matchAll;
            }

            FilterEntry[] compiled = entries.ToArray();
            return item => MatchesAllFilters(compiled, item);
        }

        /// <inheritdoc />
        public Func<TItem, bool> CreateSearchPredicate(IReadOnlyList<SearchDescriptor> descriptors)
        {
            if (descriptors == null || descriptors.Count == 0)
            {
                return s_matchAll;
            }

            var valid = new List<SearchDescriptor>(descriptors.Count);
            for (int i = 0; i < descriptors.Count; i++)
            {
                if (descriptors[i] != null)
                {
                    valid.Add(descriptors[i]);
                }
            }

            if (valid.Count == 0)
            {
                return s_matchAll;
            }

            SearchDescriptor[] compiled = valid.ToArray();
            return item => MatchesAnySearch(compiled, item);
        }

        private static int Compare(SortEntry[] entries, TItem left, TItem right)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                SortEntry entry = entries[i];
                int result = entry.Comparer.Compare(left, right);
                if (result == 0)
                {
                    continue;
                }

                return entry.Direction == System.ComponentModel.ListSortDirection.Descending ? -result : result;
            }

            return 0;
        }

        private static bool MatchesAllFilters(FilterEntry[] entries, TItem item)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                FilterEntry entry = entries[i];
                if (entry.Descriptor.Predicate != null)
                {
                    if (!entry.Descriptor.Predicate(item))
                    {
                        return false;
                    }

                    continue;
                }

                if (entry.Accessor is IDataGridColumnFilterAccessor filterAccessor &&
                    filterAccessor.TryMatch(item, entry.Descriptor, out bool match))
                {
                    if (!match)
                    {
                        return false;
                    }

                    continue;
                }

                object value = entry.Accessor?.GetValue(item);
                if (!DataGridGeneratedFilterMatcher.IsMatch(value, entry.Descriptor))
                {
                    return false;
                }
            }

            return true;
        }

        private bool MatchesAnySearch(SearchDescriptor[] descriptors, TItem item)
        {
            for (int descriptorIndex = 0; descriptorIndex < descriptors.Length; descriptorIndex++)
            {
                SearchDescriptor descriptor = descriptors[descriptorIndex];
                for (int columnIndex = 0; columnIndex < _columns.Length; columnIndex++)
                {
                    DataGridColumnAccessorRegistration column = _columns[columnIndex];
                    if (!column.IsSearchable || !IsColumnSelected(column, descriptor))
                    {
                        continue;
                    }

                    string text;
                    if (column.Accessor is IDataGridColumnTextAccessor textAccessor &&
                        textAccessor.TryGetText(
                            item,
                            converter: null,
                            converterParameter: null,
                            stringFormat: null,
                            descriptor.Culture ?? CultureInfo.CurrentCulture,
                            descriptor.Culture,
                            out text))
                    {
                        if (DataGridGeneratedSearchMatcher.IsMatch(text, descriptor))
                        {
                            return true;
                        }

                        continue;
                    }

                    object value = column.Accessor.GetValue(item);
                    text = Convert.ToString(value, descriptor.Culture ?? CultureInfo.CurrentCulture);
                    if (DataGridGeneratedSearchMatcher.IsMatch(text, descriptor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsColumnSelected(DataGridColumnAccessorRegistration column, SearchDescriptor descriptor)
        {
            if (descriptor.Scope != SearchScope.ExplicitColumns)
            {
                return true;
            }

            IReadOnlyList<object> ids = descriptor.ColumnIds;
            if (ids == null || ids.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (column.Matches(ids[i], propertyPath: null))
                {
                    return true;
                }
            }

            return false;
        }

        private DataGridColumnAccessorRegistration Find(object columnId, string propertyPath)
        {
            for (int i = 0; i < _columns.Length; i++)
            {
                if (_columns[i].Matches(columnId, propertyPath))
                {
                    return _columns[i];
                }
            }

            return null;
        }

        private readonly struct SortEntry
        {
            public SortEntry(IComparer comparer, System.ComponentModel.ListSortDirection direction)
            {
                Comparer = comparer;
                Direction = direction;
            }

            public IComparer Comparer { get; }

            public System.ComponentModel.ListSortDirection Direction { get; }
        }

        private readonly struct FilterEntry
        {
            public FilterEntry(FilteringDescriptor descriptor, IDataGridColumnValueAccessor accessor)
            {
                Descriptor = descriptor;
                Accessor = accessor;
            }

            public FilteringDescriptor Descriptor { get; }

            public IDataGridColumnValueAccessor Accessor { get; }
        }
    }

    internal static class DataGridGeneratedFilterMatcher
    {
        public static bool IsMatch(object source, FilteringDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return true;
            }

            StringComparison comparison = descriptor.StringComparisonMode ?? StringComparison.OrdinalIgnoreCase;
            CultureInfo culture = descriptor.Culture ?? CultureInfo.CurrentCulture;

            switch (descriptor.Operator)
            {
                case FilteringOperator.Equals:
                    return Equals(source, descriptor.Value);
                case FilteringOperator.NotEquals:
                    return !Equals(source, descriptor.Value);
                case FilteringOperator.Contains:
                    return Contains(source, descriptor.Value, comparison);
                case FilteringOperator.StartsWith:
                    return source is string starts && descriptor.Value is string prefix && starts.StartsWith(prefix, comparison);
                case FilteringOperator.EndsWith:
                    return source is string ends && descriptor.Value is string suffix && ends.EndsWith(suffix, comparison);
                case FilteringOperator.GreaterThan:
                    return Compare(source, descriptor.Value, culture) > 0;
                case FilteringOperator.GreaterThanOrEqual:
                    return Compare(source, descriptor.Value, culture) >= 0;
                case FilteringOperator.LessThan:
                    return Compare(source, descriptor.Value, culture) < 0;
                case FilteringOperator.LessThanOrEqual:
                    return Compare(source, descriptor.Value, culture) <= 0;
                case FilteringOperator.Between:
                    return descriptor.Values != null && descriptor.Values.Count >= 2 &&
                           Compare(source, descriptor.Values[0], culture) >= 0 &&
                           Compare(source, descriptor.Values[1], culture) <= 0;
                case FilteringOperator.In:
                    return ContainsValue(descriptor.Values, source);
                case FilteringOperator.Custom:
                    return descriptor.Predicate?.Invoke(source) ?? true;
                default:
                    return true;
            }
        }

        private static bool Contains(object source, object target, StringComparison comparison)
        {
            if (source is string text && target is string query)
            {
                return text.IndexOf(query, comparison) >= 0;
            }

            if (source is IEnumerable enumerable)
            {
                foreach (object value in enumerable)
                {
                    if (Equals(value, target))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsValue(IReadOnlyList<object> values, object source)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (Equals(source, values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Compare(object left, object right, CultureInfo culture)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (left is string leftText && right is string rightText)
            {
                return culture.CompareInfo.Compare(leftText, rightText);
            }

            if (left is IComparable comparable)
            {
                try
                {
                    object converted = Convert.ChangeType(right, left.GetType(), culture);
                    return comparable.CompareTo(converted);
                }
                catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException)
                {
                    return Comparer.DefaultInvariant.Compare(left, right);
                }
            }

            return Comparer.DefaultInvariant.Compare(left, right);
        }
    }

    internal static class DataGridGeneratedSearchMatcher
    {
        public static bool IsMatch(string text, SearchDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (string.IsNullOrEmpty(descriptor.Query))
            {
                return descriptor.AllowEmpty && text.Length > 0;
            }

            string normalizedText = Normalize(text, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
            string query = Normalize(descriptor.Query, descriptor.NormalizeWhitespace, descriptor.IgnoreDiacritics);
            StringComparison comparison = descriptor.Comparison ?? StringComparison.OrdinalIgnoreCase;

            if (descriptor.MatchMode == SearchMatchMode.Regex || descriptor.MatchMode == SearchMatchMode.Wildcard)
            {
                string pattern = descriptor.MatchMode == SearchMatchMode.Wildcard
                    ? WildcardToRegex(query)
                    : query;
                if (descriptor.WholeWord)
                {
                    pattern = $@"\b(?:{pattern})\b";
                }

                RegexOptions options = RegexOptions.CultureInvariant;
                if (IsIgnoreCase(comparison))
                {
                    options |= RegexOptions.IgnoreCase;
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

            string[] terms = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            {
                return false;
            }

            bool any = descriptor.TermMode == SearchTermCombineMode.Any;
            for (int i = 0; i < terms.Length; i++)
            {
                bool match = MatchesTerm(normalizedText, terms[i], descriptor.MatchMode, comparison, descriptor.WholeWord);
                if (match && any)
                {
                    return true;
                }

                if (!match && !any)
                {
                    return false;
                }
            }

            return !any;
        }

        private static bool MatchesTerm(
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
                    int endStart = text.Length - term.Length;
                    return endStart >= 0 && text.EndsWith(term, comparison) && IsWholeWord(text, endStart, term.Length, wholeWord);
                case SearchMatchMode.Equals:
                    return string.Equals(text, term, comparison);
                default:
                    int start = 0;
                    while (start <= text.Length - term.Length)
                    {
                        int index = text.IndexOf(term, start, comparison);
                        if (index < 0)
                        {
                            return false;
                        }

                        if (IsWholeWord(text, index, term.Length, wholeWord))
                        {
                            return true;
                        }

                        start = index + Math.Max(1, term.Length);
                    }

                    return false;
            }
        }

        private static bool IsWholeWord(string text, int start, int length, bool enabled)
        {
            if (!enabled)
            {
                return true;
            }

            int end = start + length;
            return (start == 0 || !IsWordCharacter(text[start - 1])) &&
                   (end >= text.Length || !IsWordCharacter(text[end]));
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string Normalize(string value, bool whitespace, bool diacritics)
        {
            if (!whitespace && !diacritics)
            {
                return value;
            }

            string source = diacritics ? value.Normalize(NormalizationForm.FormD) : value;
            var builder = new StringBuilder(source.Length);
            bool previousWhitespace = false;
            for (int i = 0; i < source.Length; i++)
            {
                char character = source[i];
                if (diacritics && CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (whitespace && char.IsWhiteSpace(character))
                {
                    if (previousWhitespace)
                    {
                        continue;
                    }

                    builder.Append(' ');
                    previousWhitespace = true;
                    continue;
                }

                previousWhitespace = false;
                builder.Append(character);
            }

            return whitespace ? builder.ToString().Trim() : builder.ToString();
        }

        private static string WildcardToRegex(string value)
        {
            var builder = new StringBuilder(value.Length * 2);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '*')
                {
                    builder.Append(".*");
                }
                else if (character == '?')
                {
                    builder.Append('.');
                }
                else
                {
                    builder.Append(Regex.Escape(character.ToString()));
                }
            }

            return builder.ToString();
        }

        private static bool IsIgnoreCase(StringComparison comparison)
        {
            return comparison == StringComparison.CurrentCultureIgnoreCase ||
                   comparison == StringComparison.InvariantCultureIgnoreCase ||
                   comparison == StringComparison.OrdinalIgnoreCase;
        }
    }
}
