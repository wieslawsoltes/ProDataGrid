// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Provides bounded local distinct values through a direct generated getter.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedDistinctValueProvider<TItem, TValue>
    {
        private readonly Func<TItem, TValue> _getter;
        private readonly IEqualityComparer<TValue> _comparer;

        /// <summary>Initializes a generated local distinct-value provider.</summary>
        public DataGridGeneratedDistinctValueProvider(string columnKey, Func<TItem, TValue> getter, IEqualityComparer<TValue> comparer = null)
        {
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
        }

        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }

        /// <summary>Enumerates distinct values with explicit scan and result bounds.</summary>
        public IReadOnlyList<TValue> GetValues(
            IEnumerable<TItem> source,
            int maximumSourceItems = 100000,
            int maximumResults = 1000,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (maximumSourceItems <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSourceItems));
            if (maximumResults <= 0) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            var values = new List<TValue>(Math.Min(maximumResults, 64));
            var seen = new HashSet<TValue>(_comparer);
            int visited = 0;
            foreach (TItem item in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (visited++ >= maximumSourceItems) break;
                TValue value = _getter(item);
                if (seen.Add(value))
                {
                    values.Add(value);
                    if (values.Count >= maximumResults) break;
                }
            }
            return values.AsReadOnly();
        }
    }

    /// <summary>Contains context for a remote generated distinct-value query.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedDistinctValueQuery
    {
        /// <summary>Initializes query context.</summary>
        public DataGridGeneratedDistinctValueQuery(long revision, string columnKey, string searchText, int maximumResults)
        {
            Revision = revision;
            ColumnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            SearchText = searchText;
            MaximumResults = maximumResults;
        }
        /// <summary>Gets the monotonic query revision.</summary>
        public long Revision { get; }
        /// <summary>Gets the stable column key.</summary>
        public string ColumnKey { get; }
        /// <summary>Gets optional editor search text.</summary>
        public string SearchText { get; }
        /// <summary>Gets the requested result bound.</summary>
        public int MaximumResults { get; }
    }

    /// <summary>Loads typed remote distinct values.</summary>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridGeneratedRemoteDistinctValueProvider<TValue>
    {
        /// <summary>Executes a bounded distinct-value query.</summary>
        ValueTask<IReadOnlyList<TValue>> ExecuteAsync(DataGridGeneratedDistinctValueQuery query, CancellationToken cancellationToken);
    }

    /// <summary>Coordinates cancellable, stale-safe remote distinct-value requests.</summary>
    /// <typeparam name="TValue">The field value type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedRemoteDistinctValueController<TValue> : IDisposable
    {
        private readonly string _columnKey;
        private readonly IDataGridGeneratedRemoteDistinctValueProvider<TValue> _provider;
        private CancellationTokenSource _pending;
        private bool _disposed;

        /// <summary>Initializes a remote distinct-value controller.</summary>
        public DataGridGeneratedRemoteDistinctValueController(string columnKey, IDataGridGeneratedRemoteDistinctValueProvider<TValue> provider)
        {
            _columnKey = columnKey ?? throw new ArgumentNullException(nameof(columnKey));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Values = Array.Empty<TValue>();
        }

        /// <summary>Raised when values, loading, or error state changes.</summary>
        public event EventHandler StateChanged;
        /// <summary>Gets the latest accepted values.</summary>
        public IReadOnlyList<TValue> Values { get; private set; }
        /// <summary>Gets whether a query is active.</summary>
        public bool IsLoading { get; private set; }
        /// <summary>Gets the latest provider error.</summary>
        public Exception Error { get; private set; }
        /// <summary>Gets current revision.</summary>
        public long Revision { get; private set; }

        /// <summary>Loads a new query and suppresses stale results.</summary>
        public async ValueTask<bool> LoadAsync(string searchText = null, int maximumResults = 1000, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (maximumResults <= 0) throw new ArgumentOutOfRangeException(nameof(maximumResults));
            _pending?.Cancel();
            _pending?.Dispose();
            _pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken pendingToken = _pending.Token;
            long revision = ++Revision;
            var query = new DataGridGeneratedDistinctValueQuery(revision, _columnKey, searchText, maximumResults);
            IsLoading = true;
            Error = null;
            StateChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                IReadOnlyList<TValue> values = await _provider.ExecuteAsync(query, pendingToken).ConfigureAwait(false);
                if (revision != Revision) return false;
                Values = values ?? Array.Empty<TValue>();
                IsLoading = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (OperationCanceledException) when (pendingToken.IsCancellationRequested)
            {
                if (revision == Revision)
                {
                    IsLoading = false;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
                return false;
            }
            catch (Exception exception)
            {
                if (revision == Revision)
                {
                    IsLoading = false;
                    Error = exception;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _pending?.Cancel();
            _pending?.Dispose();
            _disposed = true;
        }
    }
}
