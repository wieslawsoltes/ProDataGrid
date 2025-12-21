// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Avalonia.Controls.DataGridSearching
{
    public enum SearchMatchMode
    {
        Contains,
        StartsWith,
        EndsWith,
        Equals,
        Regex,
        Wildcard
    }

    public enum SearchTermCombineMode
    {
        Any,
        All
    }

    public enum SearchScope
    {
        AllColumns,
        VisibleColumns,
        ExplicitColumns
    }

    public enum SearchHighlightMode
    {
        None,
        Cell,
        TextAndCell
    }

    public sealed class SearchDescriptor : IEquatable<SearchDescriptor>
    {
        public SearchDescriptor(
            string query,
            SearchMatchMode matchMode = SearchMatchMode.Contains,
            SearchTermCombineMode termMode = SearchTermCombineMode.Any,
            SearchScope scope = SearchScope.AllColumns,
            IReadOnlyList<object> columnIds = null,
            StringComparison? comparison = null,
            CultureInfo culture = null,
            bool wholeWord = false,
            bool normalizeWhitespace = true,
            bool ignoreDiacritics = false,
            bool allowEmpty = false)
        {
            Query = query ?? string.Empty;
            MatchMode = matchMode;
            TermMode = termMode;
            Scope = scope;
            ColumnIds = columnIds;
            Comparison = comparison;
            Culture = culture;
            WholeWord = wholeWord;
            NormalizeWhitespace = normalizeWhitespace;
            IgnoreDiacritics = ignoreDiacritics;
            AllowEmpty = allowEmpty;
        }

        public string Query { get; }

        public SearchMatchMode MatchMode { get; }

        public SearchTermCombineMode TermMode { get; }

        public SearchScope Scope { get; }

        public IReadOnlyList<object> ColumnIds { get; }

        public StringComparison? Comparison { get; }

        public CultureInfo Culture { get; }

        public bool WholeWord { get; }

        public bool NormalizeWhitespace { get; }

        public bool IgnoreDiacritics { get; }

        public bool AllowEmpty { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as SearchDescriptor);
        }

        public bool Equals(SearchDescriptor other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return string.Equals(Query, other.Query, StringComparison.Ordinal)
                && MatchMode == other.MatchMode
                && TermMode == other.TermMode
                && Scope == other.Scope
                && ColumnIdsEqual(ColumnIds, other.ColumnIds)
                && Comparison == other.Comparison
                && Equals(Culture, other.Culture)
                && WholeWord == other.WholeWord
                && NormalizeWhitespace == other.NormalizeWhitespace
                && IgnoreDiacritics == other.IgnoreDiacritics
                && AllowEmpty == other.AllowEmpty;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + (Query?.GetHashCode() ?? 0);
                hash = (hash * 23) + MatchMode.GetHashCode();
                hash = (hash * 23) + TermMode.GetHashCode();
                hash = (hash * 23) + Scope.GetHashCode();
                hash = (hash * 23) + ColumnIdsHash(ColumnIds);
                hash = (hash * 23) + (Comparison?.GetHashCode() ?? 0);
                hash = (hash * 23) + (Culture?.GetHashCode() ?? 0);
                hash = (hash * 23) + WholeWord.GetHashCode();
                hash = (hash * 23) + NormalizeWhitespace.GetHashCode();
                hash = (hash * 23) + IgnoreDiacritics.GetHashCode();
                hash = (hash * 23) + AllowEmpty.GetHashCode();
                return hash;
            }
        }

        private static bool ColumnIdsEqual(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            var matched = new bool[right.Count];

            for (int i = 0; i < left.Count; i++)
            {
                var value = left[i];
                bool found = false;

                for (int j = 0; j < right.Count; j++)
                {
                    if (matched[j])
                    {
                        continue;
                    }

                    if (Equals(value, right[j]))
                    {
                        matched[j] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ColumnIdsHash(IReadOnlyList<object> values)
        {
            if (values == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                int sum = 0;
                int weighted = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    var itemHash = values[i]?.GetHashCode() ?? 0;
                    sum += itemHash;
                    weighted += (itemHash * 397);
                }
                hash = (hash * 23) + sum;
                hash = (hash * 23) + weighted;
                hash = (hash * 23) + values.Count;
                return hash;
            }
        }
    }

    public sealed class SearchMatch
    {
        public SearchMatch(int start, int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Start = start;
            Length = length;
        }

        public int Start { get; }

        public int Length { get; }
    }

    public sealed class SearchResult
    {
        public SearchResult(
            object item,
            int rowIndex,
            object columnId,
            int columnIndex,
            string text,
            IReadOnlyList<SearchMatch> matches)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            RowIndex = rowIndex;
            ColumnId = columnId ?? throw new ArgumentNullException(nameof(columnId));
            ColumnIndex = columnIndex;
            Text = text;
            Matches = matches ?? Array.Empty<SearchMatch>();
        }

        public object Item { get; }

        public int RowIndex { get; }

        public object ColumnId { get; }

        public int ColumnIndex { get; }

        public string Text { get; }

        public IReadOnlyList<SearchMatch> Matches { get; }
    }

    public class SearchChangedEventArgs : EventArgs
    {
        public SearchChangedEventArgs(IReadOnlyList<SearchDescriptor> oldDescriptors, IReadOnlyList<SearchDescriptor> newDescriptors)
        {
            OldDescriptors = oldDescriptors ?? Array.Empty<SearchDescriptor>();
            NewDescriptors = newDescriptors ?? Array.Empty<SearchDescriptor>();
        }

        public IReadOnlyList<SearchDescriptor> OldDescriptors { get; }

        public IReadOnlyList<SearchDescriptor> NewDescriptors { get; }
    }

    public class SearchResultsChangedEventArgs : EventArgs
    {
        public SearchResultsChangedEventArgs(IReadOnlyList<SearchResult> oldResults, IReadOnlyList<SearchResult> newResults)
        {
            OldResults = oldResults ?? Array.Empty<SearchResult>();
            NewResults = newResults ?? Array.Empty<SearchResult>();
        }

        public IReadOnlyList<SearchResult> OldResults { get; }

        public IReadOnlyList<SearchResult> NewResults { get; }
    }

    public class SearchCurrentChangedEventArgs : EventArgs
    {
        public SearchCurrentChangedEventArgs(int oldIndex, int newIndex, SearchResult oldResult, SearchResult newResult)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            OldResult = oldResult;
            NewResult = newResult;
        }

        public int OldIndex { get; }

        public int NewIndex { get; }

        public SearchResult OldResult { get; }

        public SearchResult NewResult { get; }
    }

    public interface ISearchModel : INotifyPropertyChanged
    {
        IReadOnlyList<SearchDescriptor> Descriptors { get; }

        IReadOnlyList<SearchResult> Results { get; }

        SearchHighlightMode HighlightMode { get; set; }

        bool HighlightCurrent { get; set; }

        bool UpdateSelectionOnNavigate { get; set; }

        bool WrapNavigation { get; set; }

        int CurrentIndex { get; }

        SearchResult CurrentResult { get; }

        event EventHandler<SearchChangedEventArgs> SearchChanged;

        event EventHandler<SearchResultsChangedEventArgs> ResultsChanged;

        event EventHandler<SearchCurrentChangedEventArgs> CurrentChanged;

        void Apply(IEnumerable<SearchDescriptor> descriptors);

        void Clear();

        void SetOrUpdate(SearchDescriptor descriptor);

        bool MoveNext();

        bool MovePrevious();

        bool MoveTo(int index);

        void UpdateResults(IReadOnlyList<SearchResult> results);

        void BeginUpdate();

        void EndUpdate();

        IDisposable DeferRefresh();
    }

    public interface IDataGridSearchModelFactory
    {
        ISearchModel Create();
    }

    public sealed class SearchModel : ISearchModel
    {
        private readonly List<SearchDescriptor> _descriptors = new();
        private readonly List<SearchResult> _results = new();
        private readonly IReadOnlyList<SearchDescriptor> _readOnlyDescriptors;
        private readonly IReadOnlyList<SearchResult> _readOnlyResults;
        private int _currentIndex = -1;
        private int _updateNesting;
        private bool _hasPendingChange;
        private List<SearchDescriptor> _pendingOldDescriptors;

        public SearchModel()
        {
            HighlightMode = SearchHighlightMode.Cell;
            HighlightCurrent = true;
            UpdateSelectionOnNavigate = false;
            WrapNavigation = true;
            _readOnlyDescriptors = _descriptors.AsReadOnly();
            _readOnlyResults = _results.AsReadOnly();
        }

        public IReadOnlyList<SearchDescriptor> Descriptors => _readOnlyDescriptors;

        public IReadOnlyList<SearchResult> Results => _readOnlyResults;

        private SearchHighlightMode _highlightMode;
        private bool _highlightCurrent;
        private bool _updateSelectionOnNavigate;
        private bool _wrapNavigation;

        public SearchHighlightMode HighlightMode
        {
            get => _highlightMode;
            set
            {
                if (_highlightMode != value)
                {
                    _highlightMode = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightMode)));
                }
            }
        }

        public bool HighlightCurrent
        {
            get => _highlightCurrent;
            set
            {
                if (_highlightCurrent != value)
                {
                    _highlightCurrent = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightCurrent)));
                }
            }
        }

        public bool UpdateSelectionOnNavigate
        {
            get => _updateSelectionOnNavigate;
            set
            {
                if (_updateSelectionOnNavigate != value)
                {
                    _updateSelectionOnNavigate = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateSelectionOnNavigate)));
                }
            }
        }

        public bool WrapNavigation
        {
            get => _wrapNavigation;
            set
            {
                if (_wrapNavigation != value)
                {
                    _wrapNavigation = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WrapNavigation)));
                }
            }
        }

        public int CurrentIndex => _currentIndex;

        public SearchResult CurrentResult => _currentIndex >= 0 && _currentIndex < _results.Count ? _results[_currentIndex] : null;

        public event EventHandler<SearchChangedEventArgs> SearchChanged;

        public event EventHandler<SearchResultsChangedEventArgs> ResultsChanged;

        public event EventHandler<SearchCurrentChangedEventArgs> CurrentChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Apply(IEnumerable<SearchDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            ApplyState(new List<SearchDescriptor>(descriptors));
        }

        public void Clear()
        {
            if (_descriptors.Count == 0)
            {
                return;
            }

            ApplyState(new List<SearchDescriptor>());
        }

        public void SetOrUpdate(SearchDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var next = new List<SearchDescriptor>(_descriptors);
            int index = IndexOf(descriptor);
            if (index >= 0)
            {
                next[index] = descriptor;
            }
            else
            {
                next.Add(descriptor);
            }

            ApplyState(next);
        }

        public bool MoveNext()
        {
            if (_results.Count == 0)
            {
                return false;
            }

            int nextIndex = _currentIndex + 1;
            if (nextIndex >= _results.Count)
            {
                if (!WrapNavigation)
                {
                    return false;
                }

                nextIndex = 0;
            }

            return SetCurrentIndex(nextIndex);
        }

        public bool MovePrevious()
        {
            if (_results.Count == 0)
            {
                return false;
            }

            int nextIndex = _currentIndex - 1;
            if (nextIndex < 0)
            {
                if (!WrapNavigation)
                {
                    return false;
                }

                nextIndex = _results.Count - 1;
            }

            return SetCurrentIndex(nextIndex);
        }

        public bool MoveTo(int index)
        {
            if (index < 0 || index >= _results.Count)
            {
                return false;
            }

            return SetCurrentIndex(index);
        }

        public void BeginUpdate()
        {
            _updateNesting++;
        }

        public void EndUpdate()
        {
            if (_updateNesting == 0)
            {
                throw new InvalidOperationException("EndUpdate called without a matching BeginUpdate.");
            }

            _updateNesting--;

            if (_updateNesting == 0 && _hasPendingChange)
            {
                var oldDescriptors = _pendingOldDescriptors ?? new List<SearchDescriptor>();
                _pendingOldDescriptors = null;
                _hasPendingChange = false;
                RaiseSearchChanged(oldDescriptors, new List<SearchDescriptor>(_descriptors));
            }
        }

        public IDisposable DeferRefresh()
        {
            BeginUpdate();
            return new UpdateScope(this);
        }

        public void UpdateResults(IReadOnlyList<SearchResult> results)
        {
            var next = results == null ? Array.Empty<SearchResult>() : results.ToArray();

            var oldResults = new List<SearchResult>(_results);
            _results.Clear();
            _results.AddRange(next);

            ResultsChanged?.Invoke(this, new SearchResultsChangedEventArgs(oldResults, new List<SearchResult>(_results)));

            if (_results.Count == 0)
            {
                SetCurrentIndexInternal(-1);
                return;
            }

            if (_currentIndex < 0)
            {
                SetCurrentIndexInternal(0);
            }
            else if (_currentIndex >= _results.Count)
            {
                SetCurrentIndexInternal(_results.Count - 1);
            }
        }

        private bool SetCurrentIndex(int index)
        {
            if (index < 0 || index >= _results.Count)
            {
                return false;
            }

            return SetCurrentIndexInternal(index);
        }

        private bool SetCurrentIndexInternal(int index)
        {
            if (_currentIndex == index)
            {
                return false;
            }

            var oldIndex = _currentIndex;
            var oldResult = CurrentResult;
            _currentIndex = index;
            var newResult = CurrentResult;

            CurrentChanged?.Invoke(this, new SearchCurrentChangedEventArgs(oldIndex, _currentIndex, oldResult, newResult));
            return true;
        }

        private void ApplyState(List<SearchDescriptor> next)
        {
            for (int i = 0; i < next.Count; i++)
            {
                if (next[i] == null)
                {
                    throw new ArgumentException("Search descriptors cannot contain null entries.", nameof(next));
                }
            }

            if (SequenceEqual(_descriptors, next))
            {
                return;
            }

            var oldDescriptors = new List<SearchDescriptor>(_descriptors);
            _descriptors.Clear();
            _descriptors.AddRange(next);

            if (_updateNesting > 0)
            {
                if (!_hasPendingChange)
                {
                    _pendingOldDescriptors = oldDescriptors;
                }

                _hasPendingChange = true;
                return;
            }

            RaiseSearchChanged(oldDescriptors, new List<SearchDescriptor>(_descriptors));
        }

        private void RaiseSearchChanged(IReadOnlyList<SearchDescriptor> oldDescriptors, IReadOnlyList<SearchDescriptor> newDescriptors)
        {
            SearchChanged?.Invoke(this, new SearchChangedEventArgs(oldDescriptors, newDescriptors));
        }

        private int IndexOf(SearchDescriptor descriptor)
        {
            for (int i = 0; i < _descriptors.Count; i++)
            {
                if (IsSameDescriptorKey(_descriptors[i], descriptor))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsSameDescriptorKey(SearchDescriptor existing, SearchDescriptor incoming)
        {
            if (existing == null || incoming == null)
            {
                return false;
            }

            if (existing.Scope != incoming.Scope)
            {
                return false;
            }

            if (existing.Scope != SearchScope.ExplicitColumns)
            {
                return true;
            }

            return ColumnIdsMatch(existing.ColumnIds, incoming.ColumnIds);
        }

        private static bool ColumnIdsMatch(IReadOnlyList<object> left, IReadOnlyList<object> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            var matched = new bool[right.Count];

            for (int i = 0; i < left.Count; i++)
            {
                var value = left[i];
                bool found = false;

                for (int j = 0; j < right.Count; j++)
                {
                    if (matched[j])
                    {
                        continue;
                    }

                    if (Equals(value, right[j]))
                    {
                        matched[j] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SequenceEqual(List<SearchDescriptor> left, List<SearchDescriptor> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class UpdateScope : IDisposable
        {
            private readonly SearchModel _owner;
            private bool _disposed;

            public UpdateScope(SearchModel owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _owner.EndUpdate();
                _disposed = true;
            }
        }
    }
}
