// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;

namespace Avalonia.Controls
{
    /// <summary>Identifies the paging strategy for a generated remote query.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridPageMode
    {
        /// <summary>Request rows by zero-based offset.</summary>
        Offset,
        /// <summary>Request rows following an opaque provider cursor.</summary>
        Cursor
    }

    /// <summary>Describes one offset- or cursor-based page request.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridPageRequest : IEquatable<DataGridPageRequest>
    {
        private DataGridPageRequest(DataGridPageMode mode, int offset, int size, string cursor)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            Mode = mode;
            Offset = offset;
            Size = size;
            Cursor = cursor;
        }

        /// <summary>Gets the paging strategy.</summary>
        public DataGridPageMode Mode { get; }
        /// <summary>Gets the requested offset for offset paging.</summary>
        public int Offset { get; }
        /// <summary>Gets the requested page size.</summary>
        public int Size { get; }
        /// <summary>Gets the opaque continuation cursor for cursor paging.</summary>
        public string Cursor { get; }

        /// <summary>Creates an offset page request.</summary>
        public static DataGridPageRequest FromOffset(int offset, int size) =>
            new(DataGridPageMode.Offset, offset, size, null);

        /// <summary>Creates a cursor page request.</summary>
        public static DataGridPageRequest FromCursor(string cursor, int size) =>
            new(DataGridPageMode.Cursor, 0, size, cursor);

        /// <inheritdoc />
        public bool Equals(DataGridPageRequest other) =>
            Mode == other.Mode && Offset == other.Offset && Size == other.Size &&
            string.Equals(Cursor, other.Cursor, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is DataGridPageRequest other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine((int)Mode, Offset, Size, Cursor);

        /// <summary>Compares two page requests.</summary>
        public static bool operator ==(DataGridPageRequest left, DataGridPageRequest right) => left.Equals(right);

        /// <summary>Compares two page requests.</summary>
        public static bool operator !=(DataGridPageRequest left, DataGridPageRequest right) => !left.Equals(right);
    }

    /// <summary>Contains one immutable generated remote query.</summary>
    /// <typeparam name="TItem">The requested item type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRemoteQuery<TItem>
    {
        /// <summary>Initializes a query from generated operation descriptors.</summary>
        public DataGridRemoteQuery(
            long revision,
            IEnumerable<SortingDescriptor> sorting,
            IEnumerable<FilteringDescriptor> filtering,
            IEnumerable<SearchDescriptor> searching,
            DataGridPageRequest page,
            IEnumerable<string> groups = null)
        {
            Revision = revision;
            Sorting = Copy(sorting);
            Filtering = Copy(filtering);
            Searching = Copy(searching);
            Groups = Copy(groups);
            Page = page;
        }

        /// <summary>Gets the monotonic request revision.</summary>
        public long Revision { get; }
        /// <summary>Gets the immutable sorting descriptors.</summary>
        public IReadOnlyList<SortingDescriptor> Sorting { get; }
        /// <summary>Gets the immutable filtering descriptors.</summary>
        public IReadOnlyList<FilteringDescriptor> Filtering { get; }
        /// <summary>Gets the immutable searching descriptors.</summary>
        public IReadOnlyList<SearchDescriptor> Searching { get; }
        /// <summary>Gets stable generated field IDs used for grouping.</summary>
        public IReadOnlyList<string> Groups { get; }
        /// <summary>Gets the requested page.</summary>
        public DataGridPageRequest Page { get; }

        private static T[] Copy<T>(IEnumerable<T> values) =>
            values == null ? Array.Empty<T>() : new List<T>(values).ToArray();
    }

    /// <summary>Contains one page returned by a generated remote provider.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridQueryPage<TItem, TKey>
    {
        /// <summary>Initializes a remote query page.</summary>
        public DataGridQueryPage(
            long revision,
            IReadOnlyList<TItem> items,
            long? totalCount = null,
            string nextCursor = null,
            bool hasMore = false)
        {
            Revision = revision;
            Items = items ?? throw new ArgumentNullException(nameof(items));
            TotalCount = totalCount;
            NextCursor = nextCursor;
            HasMore = hasMore;
        }

        /// <summary>Gets the request revision fulfilled by this page.</summary>
        public long Revision { get; }
        /// <summary>Gets the returned rows.</summary>
        public IReadOnlyList<TItem> Items { get; }
        /// <summary>Gets the total row count, or null when the provider does not know it.</summary>
        public long? TotalCount { get; }
        /// <summary>Gets the next opaque cursor, when supplied.</summary>
        public string NextCursor { get; }
        /// <summary>Gets whether another page may be requested.</summary>
        public bool HasMore { get; }
    }

    /// <summary>Executes generated queries without prescribing networking or persistence.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridQueryProvider<TItem, TKey>
    {
        /// <summary>Executes one immutable generated query.</summary>
        ValueTask<DataGridQueryPage<TItem, TKey>> ExecuteAsync(
            DataGridRemoteQuery<TItem> query,
            CancellationToken cancellationToken);
    }

    /// <summary>Reports remote-query loading, result, error, and stale-response state.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridRemoteQueryStateChangedEventArgs : EventArgs
    {
        internal DataGridRemoteQueryStateChangedEventArgs(long revision, bool isLoading, bool isStale, Exception error)
        {
            Revision = revision;
            IsLoading = isLoading;
            IsStale = isStale;
            Error = error;
        }

        /// <summary>Gets the affected revision.</summary>
        public long Revision { get; }
        /// <summary>Gets whether a current request is executing.</summary>
        public bool IsLoading { get; }
        /// <summary>Gets whether the response was suppressed as stale.</summary>
        public bool IsStale { get; }
        /// <summary>Gets the provider failure, when present.</summary>
        public Exception Error { get; }
    }

    /// <summary>
    /// Coordinates cancellation, debounce, stale suppression, field translation, and optional page caching.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedRemoteQueryController<TItem, TKey> : IDisposable
    {
        private readonly object _gate = new();
        private readonly IDataGridQueryProvider<TItem, TKey> _provider;
        private readonly Dictionary<string, DataGridQueryPage<TItem, TKey>> _cache = new(StringComparer.Ordinal);
        private readonly Queue<string> _cacheOrder = new();
        private CancellationTokenSource _activeCancellation;
        private bool _disposed;
        private long _revision;

        /// <summary>Initializes a remote query controller.</summary>
        public DataGridGeneratedRemoteQueryController(
            IDataGridQueryProvider<TItem, TKey> provider,
            TimeSpan debounce = default,
            int pageCacheCapacity = 0,
            Func<string, string> fieldNameTranslator = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (debounce < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(debounce));
            }
            if (pageCacheCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageCacheCapacity));
            }

            Debounce = debounce;
            PageCacheCapacity = pageCacheCapacity;
            FieldNameTranslator = fieldNameTranslator ?? IdentityFieldName;
        }

        /// <summary>Gets the request debounce interval.</summary>
        public TimeSpan Debounce { get; }
        /// <summary>Gets the maximum cached page count.</summary>
        public int PageCacheCapacity { get; }
        /// <summary>Gets the stable-field to backend-field translator.</summary>
        public Func<string, string> FieldNameTranslator { get; }
        /// <summary>Gets whether the current revision is loading.</summary>
        public bool IsLoading { get; private set; }
        /// <summary>Gets the latest current provider error.</summary>
        public Exception LastError { get; private set; }
        /// <summary>Gets the latest accepted page.</summary>
        public DataGridQueryPage<TItem, TKey> LastPage { get; private set; }
        /// <summary>Gets the latest issued revision.</summary>
        public long Revision => Interlocked.Read(ref _revision);

        /// <summary>Occurs whenever observable remote-query state changes.</summary>
        public event EventHandler<DataGridRemoteQueryStateChangedEventArgs> StateChanged;

        /// <summary>Translates one stable generated field ID for the backend.</summary>
        public string TranslateField(string stableFieldId) =>
            FieldNameTranslator(stableFieldId ?? throw new ArgumentNullException(nameof(stableFieldId)));

        /// <summary>Executes the latest query and returns null for canceled or stale work.</summary>
        public async ValueTask<DataGridQueryPage<TItem, TKey>> ExecuteLatestAsync(
            Func<long, DataGridRemoteQuery<TItem>> queryFactory,
            string cacheKey = null,
            CancellationToken cancellationToken = default)
        {
            if (queryFactory == null)
            {
                throw new ArgumentNullException(nameof(queryFactory));
            }

            CancellationTokenSource requestCancellation;
            long revision;
            lock (_gate)
            {
                ThrowIfDisposed();
                revision = ++_revision;
                _activeCancellation?.Cancel();
                requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _activeCancellation = requestCancellation;
                IsLoading = true;
                LastError = null;
            }

            RaiseState(revision, true, false, null);
            try
            {
                if (!string.IsNullOrEmpty(cacheKey) && TryGetCachedPage(cacheKey, out DataGridQueryPage<TItem, TKey> cached))
                {
                    return AcceptPage(revision, new DataGridQueryPage<TItem, TKey>(
                        revision, cached.Items, cached.TotalCount, cached.NextCursor, cached.HasMore));
                }

                if (Debounce > TimeSpan.Zero)
                {
                    await Task.Delay(Debounce, requestCancellation.Token).ConfigureAwait(false);
                }

                DataGridRemoteQuery<TItem> query = queryFactory(revision) ??
                    throw new InvalidOperationException("The generated remote query factory returned null.");
                if (query.Revision != revision)
                {
                    throw new InvalidOperationException("The generated remote query revision does not match the controller revision.");
                }

                DataGridQueryPage<TItem, TKey> page =
                    await _provider.ExecuteAsync(query, requestCancellation.Token).ConfigureAwait(false);
                if (page == null)
                {
                    throw new InvalidOperationException("The remote query provider returned null.");
                }
                if (page.Revision != revision || revision != Revision)
                {
                    RaiseState(revision, false, true, null);
                    return null;
                }

                if (!string.IsNullOrEmpty(cacheKey))
                {
                    CachePage(cacheKey, page);
                }

                return AcceptPage(revision, page);
            }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
            {
                RaiseState(revision, false, true, null);
                return null;
            }
            catch (Exception error)
            {
                lock (_gate)
                {
                    if (revision == _revision)
                    {
                        IsLoading = false;
                        LastError = error;
                    }
                }
                RaiseState(revision, false, revision != Revision, error);
                throw;
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeCancellation, requestCancellation))
                    {
                        _activeCancellation = null;
                        IsLoading = false;
                    }
                }
                requestCancellation.Dispose();
            }
        }

        /// <summary>Attempts to get a cached page by caller-defined stable key.</summary>
        public bool TryGetCachedPage(string cacheKey, out DataGridQueryPage<TItem, TKey> page)
        {
            if (string.IsNullOrEmpty(cacheKey) || PageCacheCapacity == 0)
            {
                page = null;
                return false;
            }

            lock (_gate)
            {
                return _cache.TryGetValue(cacheKey, out page);
            }
        }

        /// <summary>Clears all cached remote pages.</summary>
        public void ClearCache()
        {
            lock (_gate)
            {
                _cache.Clear();
                _cacheOrder.Clear();
            }
        }

        /// <summary>Cancels current work and releases the controller.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _activeCancellation?.Cancel();
                _activeCancellation = null;
                _cache.Clear();
                _cacheOrder.Clear();
                IsLoading = false;
            }
        }

        private DataGridQueryPage<TItem, TKey> AcceptPage(long revision, DataGridQueryPage<TItem, TKey> page)
        {
            bool stale;
            lock (_gate)
            {
                stale = revision != _revision;
                if (!stale)
                {
                    LastPage = page;
                    LastError = null;
                    IsLoading = false;
                }
            }
            RaiseState(revision, false, stale, null);
            return stale ? null : page;
        }

        private void CachePage(string cacheKey, DataGridQueryPage<TItem, TKey> page)
        {
            if (PageCacheCapacity == 0)
            {
                return;
            }

            lock (_gate)
            {
                if (!_cache.ContainsKey(cacheKey))
                {
                    _cacheOrder.Enqueue(cacheKey);
                }
                _cache[cacheKey] = page;
                while (_cache.Count > PageCacheCapacity && _cacheOrder.Count > 0)
                {
                    string oldest = _cacheOrder.Dequeue();
                    _cache.Remove(oldest);
                }
            }
        }

        private void RaiseState(long revision, bool isLoading, bool isStale, Exception error) =>
            StateChanged?.Invoke(this, new DataGridRemoteQueryStateChangedEventArgs(revision, isLoading, isStale, error));

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private static string IdentityFieldName(string value) => value;
    }
}
