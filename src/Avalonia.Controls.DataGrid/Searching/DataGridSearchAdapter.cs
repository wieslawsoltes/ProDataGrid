// Copyright (c) Wiesław Šoltés. All rights reserved.
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
using System.Diagnostics.CodeAnalysis;
using Avalonia.Utilities;
using Avalonia.Threading;

namespace Avalonia.Controls.DataGridSearching
{
    /// <summary>
    /// Computes search results from descriptors against the view.
    /// </summary>
    [RequiresUnreferencedCode("DataGridSearchAdapter uses reflection to access item properties and is not compatible with trimming.")]
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridSearchAdapter : IDisposable
    {
        private readonly ISearchModel _model;
        private readonly Func<IEnumerable<DataGridColumn>> _columnProvider;
        private readonly Dictionary<(Type type, string property), Func<object, object>> _getterCache = new();
        private readonly HashSet<INotifyPropertyChanged> _itemSubscriptions = new();
        private IDataGridCollectionView _view;
        private bool _refreshQueued;
        private bool _isDisposed;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        DataGridSearchAdapter(
            ISearchModel model,
            Func<IEnumerable<DataGridColumn>> columnProvider)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _columnProvider = columnProvider ?? throw new ArgumentNullException(nameof(columnProvider));

            WeakEventHandlerManager.Subscribe<ISearchModel, SearchChangedEventArgs, DataGridSearchAdapter>(
                _model,
                nameof(ISearchModel.SearchChanged),
                OnModelSearchChanged);
        }

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        IDataGridCollectionView View => _view;

#if !DATAGRID_INTERNAL
        public
#else
        internal
#endif
        void AttachView(IDataGridCollectionView view)
        {
            if (ReferenceEquals(_view, view))
            {
                ApplyModelToView(_model.Descriptors);
                return;
            }

            DetachView();
            _view = view;

            if (_view is INotifyCollectionChanged incc)
            {
                WeakEventHandlerManager.Subscribe<INotifyCollectionChanged, NotifyCollectionChangedEventArgs, DataGridSearchAdapter>(
                    incc,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    View_CollectionChanged);
            }

            ApplyModelToView(_model.Descriptors);
        }

        public void Dispose()
        {
            _isDisposed = true;
            DetachView();
            WeakEventHandlerManager.Unsubscribe<SearchChangedEventArgs, DataGridSearchAdapter>(
                _model,
                nameof(ISearchModel.SearchChanged),
                OnModelSearchChanged);
        }

        protected virtual bool TryApplyModelToView(
            IReadOnlyList<SearchDescriptor> descriptors,
            IReadOnlyList<SearchDescriptor> previousDescriptors,
            out IReadOnlyList<SearchResult> results)
        {
            results = null;
            return false;
        }

        protected virtual bool TrackItemPropertyChanges => true;

        protected virtual void OnViewCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
        }

        protected virtual void OnViewItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        private void OnModelSearchChanged(object sender, SearchChangedEventArgs e)
        {
            _refreshQueued = false;
            ApplyModelToView(e.NewDescriptors, e.OldDescriptors);
        }

        private void View_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnViewCollectionChanged(e);
            QueueRefreshFromViewChanges();
        }

        private void ApplyModelToView(
            IReadOnlyList<SearchDescriptor> descriptors,
            IReadOnlyList<SearchDescriptor> previousDescriptors = null)
        {
            if (_view == null)
            {
                _model.UpdateResults(Array.Empty<SearchResult>());
                ClearItemSubscriptions();
                return;
            }

            if (descriptors == null || descriptors.Count == 0)
            {
                if (_model.Results.Count > 0)
                {
                    _model.UpdateResults(Array.Empty<SearchResult>());
                }

                ClearItemSubscriptions();
                return;
            }

            if (TryApplyModelToView(descriptors, previousDescriptors, out var handledResults))
            {
                if (TrackItemPropertyChanges)
                {
                    UpdateItemSubscriptionsFromView();
                }
                else
                {
                    ClearItemSubscriptions();
                }
                _model.UpdateResults(handledResults ?? Array.Empty<SearchResult>());
                return;
            }

            var results = ComputeResults(descriptors, trackItems: TrackItemPropertyChanges);
            if (!TrackItemPropertyChanges)
            {
                ClearItemSubscriptions();
            }
            _model.UpdateResults(results);
        }

        private IReadOnlyList<SearchResult> ComputeResults(IReadOnlyList<SearchDescriptor> descriptors, bool trackItems)
        {
            if (_view == null || descriptors == null || descriptors.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            var trackedItems = trackItems ? new HashSet<INotifyPropertyChanged>() : null;
            var columns = BuildColumnInfos();
            if (columns.Count == 0)
            {
                UpdateItemSubscriptions(trackedItems);
                return Array.Empty<SearchResult>();
            }

            var plans = BuildPlans(descriptors, columns);
            if (plans.Count == 0)
            {
                UpdateItemSubscriptions(trackedItems);
                return Array.Empty<SearchResult>();
            }

            var results = new Dictionary<SearchCellKey, SearchResultBuilder>();

            int rowIndex = 0;
            foreach (var item in _view)
            {
                TrackItem(item, trackedItems);
                foreach (var plan in plans)
                {
                    for (int i = 0; i < plan.Columns.Count; i++)
                    {
                        var column = plan.Columns[i];
                        var text = GetColumnText(column, item, plan.Descriptor);
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
                            builder = new SearchResultBuilder(item, rowIndex, column.Column, column.Column.Index, text);
                            results.Add(key, builder);
                        }

                        builder.AddMatches(matches);
                    }
                }

                rowIndex++;
            }

            UpdateItemSubscriptions(trackedItems);

            if (results.Count == 0)
            {
                return Array.Empty<SearchResult>();
            }

            var list = results.Values
                .Select(r => r.Build())
                .OrderBy(r => r.RowIndex)
                .ThenBy(r => r.ColumnIndex)
                .ThenBy(r => r.Matches.Count > 0 ? r.Matches[0].Start : 0)
                .ToList();

            return list;
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
            foreach (var column in columns)
            {
                if (column == null)
                {
                    continue;
                }

                if (!DataGridColumnSearch.GetIsSearchable(column))
                {
                    continue;
                }

                var info = CreateColumnInfo(column);
                if (info != null)
                {
                    list.Add(info);
                }
            }

            return list;
        }

        private SearchColumnInfo CreateColumnInfo(DataGridColumn column)
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
            var accessor = DataGridColumnMetadata.GetValueAccessor(column);
            if (accessor != null)
            {
                valueGetter = accessor.GetValue;
            }
            else if (!string.IsNullOrEmpty(propertyPath))
            {
                valueGetter = GetGetter(propertyPath);
            }

            if (textProvider == null && valueGetter == null)
            {
                return null;
            }

            return new SearchColumnInfo(
                column,
                propertyPath,
                valueGetter,
                textProvider,
                stringFormat,
                converter,
                converterParameter,
                formatProvider);
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

            if (ReferenceEquals(id, column.Column))
            {
                return true;
            }

            var definition = DataGridColumnMetadata.GetDefinition(column.Column);
            if (definition != null)
            {
                if (ReferenceEquals(id, definition))
                {
                    return true;
                }

                if (definition.ColumnKey != null && Equals(definition.ColumnKey, id))
                {
                    return true;
                }
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

        private string GetColumnText(SearchColumnInfo column, object item, SearchDescriptor descriptor)
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

            if (column.ValueGetter == null)
            {
                return null;
            }

            var value = column.ValueGetter(item);
            if (value == null)
            {
                return null;
            }

            var culture = descriptor?.Culture ?? _view?.Culture ?? CultureInfo.CurrentCulture;
            var provider = column.FormatProvider ?? culture;

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

        private Func<object, object> GetGetter(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            Func<object, object> TryGetCached(Type type)
            {
                if (_getterCache.TryGetValue((type, propertyPath), out var cached))
                {
                    return cached;
                }

                return null;
            }

            return item =>
            {
                if (item == null)
                {
                    return null;
                }

                var type = item.GetType();
                var cached = TryGetCached(type);
                if (cached != null)
                {
                    return cached(item);
                }

                var compiled = new Func<object, object>(o => TypeHelper.GetNestedPropertyValue(o, propertyPath));
                _getterCache[(type, propertyPath)] = compiled;
                return compiled(item);
            };
        }

        private void DetachView()
        {
            if (_view is INotifyCollectionChanged incc)
            {
                WeakEventHandlerManager.Unsubscribe<NotifyCollectionChangedEventArgs, DataGridSearchAdapter>(
                    incc,
                    nameof(INotifyCollectionChanged.CollectionChanged),
                    View_CollectionChanged);
            }

            _view = null;
            _refreshQueued = false;
            ClearItemSubscriptions();
        }

        private void TrackItem(object item, HashSet<INotifyPropertyChanged> trackedItems)
        {
            if (trackedItems == null || item == null)
            {
                return;
            }

            if (item == DataGridCollectionView.NewItemPlaceholder || item is DataGridCollectionViewGroup)
            {
                return;
            }

            if (item is INotifyPropertyChanged inpc)
            {
                trackedItems.Add(inpc);
            }
        }

        private void UpdateItemSubscriptionsFromView()
        {
            if (_view == null)
            {
                ClearItemSubscriptions();
                return;
            }

            var items = new HashSet<INotifyPropertyChanged>();
            foreach (var item in _view)
            {
                TrackItem(item, items);
            }

            UpdateItemSubscriptions(items);
        }

        private void UpdateItemSubscriptions(HashSet<INotifyPropertyChanged> items)
        {
            if (items == null)
            {
                return;
            }

            if (_itemSubscriptions.Count > 0)
            {
                var toRemove = new List<INotifyPropertyChanged>();
                foreach (var existing in _itemSubscriptions)
                {
                    if (!items.Contains(existing))
                    {
                        WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridSearchAdapter>(
                            existing,
                            nameof(INotifyPropertyChanged.PropertyChanged),
                            Item_PropertyChanged);
                        toRemove.Add(existing);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    _itemSubscriptions.Remove(toRemove[i]);
                }
            }

            foreach (var item in items)
            {
                if (_itemSubscriptions.Add(item))
                {
                    WeakEventHandlerManager.Subscribe<INotifyPropertyChanged, PropertyChangedEventArgs, DataGridSearchAdapter>(
                        item,
                        nameof(INotifyPropertyChanged.PropertyChanged),
                        Item_PropertyChanged);
                }
            }
        }

        private void ClearItemSubscriptions()
        {
            if (_itemSubscriptions.Count == 0)
            {
                return;
            }

            foreach (var item in _itemSubscriptions)
            {
                WeakEventHandlerManager.Unsubscribe<PropertyChangedEventArgs, DataGridSearchAdapter>(
                    item,
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    Item_PropertyChanged);
            }

            _itemSubscriptions.Clear();
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!TrackItemPropertyChanges)
            {
                return;
            }

            if (_view == null || _model.Descriptors.Count == 0)
            {
                return;
            }

            OnViewItemPropertyChanged(sender, e);
            QueueRefreshFromViewChanges();
        }

        private void QueueRefreshFromViewChanges()
        {
            if (_isDisposed || _view == null)
            {
                return;
            }

            if (_refreshQueued)
            {
                return;
            }

            _refreshQueued = true;
            Dispatcher.UIThread.Post(ApplyQueuedRefresh, DispatcherPriority.Background);
        }

        private void ApplyQueuedRefresh()
        {
            if (!_refreshQueued)
            {
                return;
            }

            _refreshQueued = false;
            if (_isDisposed || _view == null)
            {
                return;
            }

            ApplyModelToView(_model.Descriptors);
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
                Func<object, object> valueGetter,
                Func<object, string> textProvider,
                string stringFormat,
                IValueConverter converter,
                object converterParameter,
                IFormatProvider formatProvider)
            {
                Column = column;
                PropertyPath = propertyPath;
                ValueGetter = valueGetter;
                TextProvider = textProvider;
                StringFormat = stringFormat;
                Converter = converter;
                ConverterParameter = converterParameter;
                FormatProvider = formatProvider;
            }

            public DataGridColumn Column { get; }

            public string PropertyPath { get; }

            public Func<object, object> ValueGetter { get; }

            public Func<object, string> TextProvider { get; }

            public string StringFormat { get; }

            public IValueConverter Converter { get; }

            public object ConverterParameter { get; }

            public IFormatProvider FormatProvider { get; }
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
                        if (string.Equals(text, term, comparison) && IsWholeWord(text, 0, term.Length, wholeWord))
                        {
                            matches.Add(new SearchMatch(0, term.Length));
                        }
                        break;
                    default:
                        FindAllOccurrences(text, term, comparison, wholeWord, matches);
                        break;
                }

                return matches;
            }

            private static void FindAllOccurrences(
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
                        matches.Add(new SearchMatch(index, term.Length));
                    }

                    startIndex = index + term.Length;
                }
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
                        var decomposed = source.ToString().Normalize(NormalizationForm.FormD);
                        foreach (var d in decomposed)
                        {
                            if (IsDiacritic(d))
                            {
                                continue;
                            }

                            AppendNormalizedCharacter(builder, map, d, i, normalizeWhitespace, ref wasWhitespace);
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

                builder.Append(ch);
                map.Add(sourceIndex);
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
                return normalizeWhitespace ? normalized.Text.Trim() : normalized.Text;
            }

            private static bool IsDiacritic(char ch)
            {
                return CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark;
            }

            private static IReadOnlyList<SearchMatch> MapMatches(IReadOnlyList<SearchMatch> matches, int[] map)
            {
                if (matches == null || matches.Count == 0)
                {
                    return Array.Empty<SearchMatch>();
                }

                if (map == null)
                {
                    return matches;
                }

                var mapped = new List<SearchMatch>();
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    if (match.Length == 0)
                    {
                        continue;
                    }

                    if (match.Start < 0 || match.Start >= map.Length)
                    {
                        continue;
                    }

                    var start = map[match.Start];
                    var endIndex = match.Start + match.Length - 1;
                    if (endIndex >= map.Length)
                    {
                        endIndex = map.Length - 1;
                    }

                    var end = map[endIndex];
                    var length = end >= start ? end - start + 1 : 0;
                    if (length > 0)
                    {
                        mapped.Add(new SearchMatch(start, length));
                    }
                }

                return MergeOverlaps(mapped);
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

        private readonly struct NormalizedText
        {
            public NormalizedText(string text, int[] map)
            {
                Text = text ?? string.Empty;
                Map = map;
            }

            public string Text { get; }

            public int[] Map { get; }
        }
    }
}
