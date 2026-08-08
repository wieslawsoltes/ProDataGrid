// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.Controls
{
    /// <summary>Identifies one update entering a generated streaming pipeline.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedStreamUpdateKind
    {
        /// <summary>Append an item without replacing an existing key.</summary>
        Append,
        /// <summary>Add or replace an item by stable key.</summary>
        Upsert,
        /// <summary>Remove an item by stable key.</summary>
        Remove,
        /// <summary>Reconcile a complete keyed snapshot.</summary>
        ReplaceSnapshot
    }

    /// <summary>Defines behavior when a generated stream buffer reaches its capacity.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedStreamOverflowPolicy
    {
        /// <summary>Reject the write so an asynchronous producer can wait and retry.</summary>
        Wait,
        /// <summary>Discard the incoming update.</summary>
        DropNewest,
        /// <summary>Discard the oldest queued update.</summary>
        DropOldest,
        /// <summary>Replace a queued update having the same key, otherwise discard the oldest update.</summary>
        CoalesceByKey
    }

    /// <summary>Represents an immutable generated streaming update.</summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedStreamUpdate<TItem, TKey>
    {
        private DataGridGeneratedStreamUpdate(
            DataGridGeneratedStreamUpdateKind kind,
            long revision,
            TItem item,
            TKey key,
            IReadOnlyList<TItem> snapshot,
            bool hasKey)
        {
            Kind = kind;
            Revision = revision;
            Item = item;
            Key = key;
            Snapshot = snapshot;
            HasKey = hasKey;
        }

        /// <summary>Gets the update kind.</summary>
        public DataGridGeneratedStreamUpdateKind Kind { get; }

        /// <summary>Gets the producer revision.</summary>
        public long Revision { get; }

        /// <summary>Gets the item for append and upsert updates.</summary>
        public TItem Item { get; }

        /// <summary>Gets the stable key for keyed updates.</summary>
        public TKey Key { get; }

        /// <summary>Gets the items for a snapshot update.</summary>
        public IReadOnlyList<TItem> Snapshot { get; }

        /// <summary>Gets a value indicating whether this update carries a key.</summary>
        public bool HasKey { get; }

        /// <summary>Creates an append update.</summary>
        public static DataGridGeneratedStreamUpdate<TItem, TKey> Append(long revision, TItem item) =>
            new(DataGridGeneratedStreamUpdateKind.Append, revision, item, default, null, false);

        /// <summary>Creates a keyed upsert update.</summary>
        public static DataGridGeneratedStreamUpdate<TItem, TKey> Upsert(long revision, TKey key, TItem item) =>
            new(DataGridGeneratedStreamUpdateKind.Upsert, revision, item, ValidateKey(key), null, true);

        /// <summary>Creates a keyed remove update.</summary>
        public static DataGridGeneratedStreamUpdate<TItem, TKey> Remove(long revision, TKey key) =>
            new(DataGridGeneratedStreamUpdateKind.Remove, revision, default, ValidateKey(key), null, true);

        /// <summary>Creates a complete snapshot update.</summary>
        public static DataGridGeneratedStreamUpdate<TItem, TKey> ReplaceSnapshot(long revision, IReadOnlyList<TItem> items) =>
            new(DataGridGeneratedStreamUpdateKind.ReplaceSnapshot, revision, default, default,
                items ?? throw new ArgumentNullException(nameof(items)), false);

        private static TKey ValidateKey(TKey key) =>
            key == null ? throw new ArgumentNullException(nameof(key)) : key;
    }

    /// <summary>Provides a consistent snapshot of generated stream counters.</summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridGeneratedStreamMetrics
    {
        internal DataGridGeneratedStreamMetrics(
            int queued,
            long accepted,
            long coalesced,
            long dropped,
            long applied,
            long stale,
            long lastAppliedRevision)
        {
            Queued = queued;
            Accepted = accepted;
            Coalesced = coalesced;
            Dropped = dropped;
            Applied = applied;
            Stale = stale;
            LastAppliedRevision = lastAppliedRevision;
        }

        /// <summary>Gets the current queue length.</summary>
        public int Queued { get; }
        /// <summary>Gets accepted update count.</summary>
        public long Accepted { get; }
        /// <summary>Gets keyed updates merged before application.</summary>
        public long Coalesced { get; }
        /// <summary>Gets updates discarded due to overflow.</summary>
        public long Dropped { get; }
        /// <summary>Gets updates reported as applied.</summary>
        public long Applied { get; }
        /// <summary>Gets updates rejected because their revision was stale.</summary>
        public long Stale { get; }
        /// <summary>Gets the latest applied revision.</summary>
        public long LastAppliedRevision { get; }
    }

    /// <summary>
    /// Provides a bounded, thread-safe queue with keyed coalescing for generated streaming adapters.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedStreamBuffer<TItem, TKey>
    {
        private readonly object _gate = new();
        private readonly int _capacity;
        private readonly DataGridGeneratedStreamOverflowPolicy _overflowPolicy;
        private readonly LinkedList<DataGridGeneratedStreamUpdate<TItem, TKey>> _queue = new();
        private readonly Dictionary<TKey, LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>>> _keyedNodes;
        private long _accepted;
        private long _coalesced;
        private long _dropped;
        private long _applied;
        private long _stale;
        private long _lastAppliedRevision = -1;

        /// <summary>Initializes a bounded stream buffer.</summary>
        public DataGridGeneratedStreamBuffer(
            int capacity,
            DataGridGeneratedStreamOverflowPolicy overflowPolicy = DataGridGeneratedStreamOverflowPolicy.CoalesceByKey,
            IEqualityComparer<TKey> keyComparer = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _overflowPolicy = overflowPolicy;
            _keyedNodes = new Dictionary<TKey, LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>>>(
                keyComparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Gets the maximum number of queued updates.</summary>
        public int Capacity => _capacity;

        /// <summary>Gets the configured overflow policy.</summary>
        public DataGridGeneratedStreamOverflowPolicy OverflowPolicy => _overflowPolicy;

        /// <summary>Gets a consistent metrics snapshot.</summary>
        public DataGridGeneratedStreamMetrics Metrics
        {
            get
            {
                lock (_gate)
                {
                    return new DataGridGeneratedStreamMetrics(
                        _queue.Count, _accepted, _coalesced, _dropped, _applied, _stale, _lastAppliedRevision);
                }
            }
        }

        /// <summary>
        /// Attempts to enqueue an update. A false result means the producer should wait or account for rejection.
        /// </summary>
        public bool TryEnqueue(in DataGridGeneratedStreamUpdate<TItem, TKey> update)
        {
            lock (_gate)
            {
                if (update.Revision <= _lastAppliedRevision)
                {
                    _stale++;
                    return false;
                }

                if (_overflowPolicy == DataGridGeneratedStreamOverflowPolicy.CoalesceByKey &&
                    update.HasKey && _keyedNodes.TryGetValue(update.Key, out LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>> node))
                {
                    node.Value = update;
                    _accepted++;
                    _coalesced++;
                    return true;
                }

                if (_queue.Count == _capacity)
                {
                    if (_overflowPolicy == DataGridGeneratedStreamOverflowPolicy.Wait)
                    {
                        return false;
                    }

                    if (_overflowPolicy == DataGridGeneratedStreamOverflowPolicy.DropNewest)
                    {
                        _dropped++;
                        return false;
                    }

                    RemoveNode(_queue.First);
                    _dropped++;
                }

                LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>> added = _queue.AddLast(update);
                if (update.HasKey)
                {
                    _keyedNodes[update.Key] = added;
                }

                _accepted++;
                return true;
            }
        }

        /// <summary>Attempts to dequeue the oldest update.</summary>
        public bool TryDequeue(out DataGridGeneratedStreamUpdate<TItem, TKey> update)
        {
            lock (_gate)
            {
                if (_queue.First == null)
                {
                    update = default;
                    return false;
                }

                update = _queue.First.Value;
                RemoveNode(_queue.First);
                return true;
            }
        }

        /// <summary>Drains up to the destination length without allocating.</summary>
        public int Drain(Span<DataGridGeneratedStreamUpdate<TItem, TKey>> destination)
        {
            lock (_gate)
            {
                int count = Math.Min(destination.Length, _queue.Count);
                for (int index = 0; index < count; index++)
                {
                    LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>> node = _queue.First;
                    destination[index] = node.Value;
                    RemoveNode(node);
                }

                return count;
            }
        }

        /// <summary>Reports a successfully applied batch and advances stale-revision protection.</summary>
        public void MarkApplied(long revision, int updateCount)
        {
            if (updateCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(updateCount));
            }

            lock (_gate)
            {
                if (revision > _lastAppliedRevision)
                {
                    _lastAppliedRevision = revision;
                }

                _applied += updateCount;
            }
        }

        /// <summary>Clears queued updates while preserving cumulative metrics.</summary>
        public void Clear()
        {
            lock (_gate)
            {
                _queue.Clear();
                _keyedNodes.Clear();
            }
        }

        private void RemoveNode(LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>> node)
        {
            DataGridGeneratedStreamUpdate<TItem, TKey> update = node.Value;
            _queue.Remove(node);
            if (update.HasKey && _keyedNodes.TryGetValue(update.Key, out LinkedListNode<DataGridGeneratedStreamUpdate<TItem, TKey>> keyed) &&
                ReferenceEquals(keyed, node))
            {
                _keyedNodes.Remove(update.Key);
            }
        }
    }

    /// <summary>
    /// Pumps asynchronous item sources through a bounded generated buffer and one callback per batch.
    /// </summary>
    /// <typeparam name="TItem">The row item type.</typeparam>
    /// <typeparam name="TKey">The stable key type.</typeparam>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridGeneratedAsyncStreamPump<TItem, TKey> : IDisposable
    {
        private readonly IDataGridItemKey<TItem, TKey> _keyAccessor;
        private readonly DataGridGeneratedStreamBuffer<TItem, TKey> _buffer;
        private readonly int _batchSize;
        private readonly Func<ReadOnlyMemory<DataGridGeneratedStreamUpdate<TItem, TKey>>, CancellationToken, ValueTask> _applyBatch;
        private readonly CancellationTokenSource _disposeCancellation = new();
        private bool _disposed;

        /// <summary>Initializes an asynchronous stream pump.</summary>
        public DataGridGeneratedAsyncStreamPump(
            IDataGridItemKey<TItem, TKey> keyAccessor,
            Func<ReadOnlyMemory<DataGridGeneratedStreamUpdate<TItem, TKey>>, CancellationToken, ValueTask> applyBatch,
            int capacity = 1024,
            int batchSize = 128,
            DataGridGeneratedStreamOverflowPolicy overflowPolicy = DataGridGeneratedStreamOverflowPolicy.CoalesceByKey,
            IEqualityComparer<TKey> keyComparer = null)
        {
            if (batchSize <= 0 || batchSize > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            _keyAccessor = keyAccessor ?? throw new ArgumentNullException(nameof(keyAccessor));
            _applyBatch = applyBatch ?? throw new ArgumentNullException(nameof(applyBatch));
            _batchSize = batchSize;
            _buffer = new DataGridGeneratedStreamBuffer<TItem, TKey>(capacity, overflowPolicy, keyComparer);
        }

        /// <summary>Gets the underlying bounded buffer metrics.</summary>
        public DataGridGeneratedStreamMetrics Metrics => _buffer.Metrics;

        /// <summary>Occurs after the source completes and its final batch is applied.</summary>
        public event EventHandler Completed;

        /// <summary>Occurs when source enumeration or batch application fails.</summary>
        public event Action<Exception> Faulted;

        /// <summary>Consumes an asynchronous enumerable in append or keyed-upsert mode.</summary>
        public async Task RunAsync(
            IAsyncEnumerable<TItem> source,
            DataGridGeneratedStreamUpdateKind mode = DataGridGeneratedStreamUpdateKind.Upsert,
            long initialRevision = 0,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (mode != DataGridGeneratedStreamUpdateKind.Append && mode != DataGridGeneratedStreamUpdateKind.Upsert)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            ThrowIfDisposed();
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _disposeCancellation.Token);
            try
            {
                long revision = initialRevision;
                await using IAsyncEnumerator<TItem> enumerator = source.GetAsyncEnumerator(linked.Token);
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    revision++;
                    DataGridGeneratedStreamUpdate<TItem, TKey> update = mode == DataGridGeneratedStreamUpdateKind.Append
                        ? DataGridGeneratedStreamUpdate<TItem, TKey>.Append(revision, enumerator.Current)
                        : DataGridGeneratedStreamUpdate<TItem, TKey>.Upsert(
                            revision, _keyAccessor.GetKey(enumerator.Current), enumerator.Current);
                    if (!_buffer.TryEnqueue(update) &&
                        _buffer.OverflowPolicy == DataGridGeneratedStreamOverflowPolicy.Wait)
                    {
                        await DrainAndApplyAsync(linked.Token).ConfigureAwait(false);
                        if (!_buffer.TryEnqueue(update))
                        {
                            throw new InvalidOperationException("Generated stream buffer could not accept an update after draining.");
                        }
                    }

                    if (_buffer.Metrics.Queued >= _batchSize)
                    {
                        await DrainAndApplyAsync(linked.Token).ConfigureAwait(false);
                    }
                }

                while (_buffer.Metrics.Queued > 0)
                {
                    await DrainAndApplyAsync(linked.Token).ConfigureAwait(false);
                }

                Completed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !linked.IsCancellationRequested)
            {
                Faulted?.Invoke(exception);
                throw;
            }
        }

        /// <summary>Consumes a channel reader in append or keyed-upsert mode.</summary>
        public Task RunAsync(
            ChannelReader<TItem> source,
            DataGridGeneratedStreamUpdateKind mode = DataGridGeneratedStreamUpdateKind.Upsert,
            long initialRevision = 0,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return RunAsync(source.ReadAllAsync(cancellationToken), mode, initialRevision, cancellationToken);
        }

        /// <summary>Cancels active ingestion and releases the pump lifetime.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeCancellation.Cancel();
            _disposeCancellation.Dispose();
            _buffer.Clear();
        }

        private async ValueTask DrainAndApplyAsync(CancellationToken cancellationToken)
        {
            DataGridGeneratedStreamUpdate<TItem, TKey>[] rented =
                ArrayPool<DataGridGeneratedStreamUpdate<TItem, TKey>>.Shared.Rent(_batchSize);
            try
            {
                int count = _buffer.Drain(rented.AsSpan(0, _batchSize));
                if (count == 0)
                {
                    return;
                }

                await _applyBatch(new ReadOnlyMemory<DataGridGeneratedStreamUpdate<TItem, TKey>>(rented, 0, count), cancellationToken)
                    .ConfigureAwait(false);
                long revision = rented[0].Revision;
                for (int index = 1; index < count; index++)
                {
                    revision = Math.Max(revision, rented[index].Revision);
                }

                _buffer.MarkApplied(revision, count);
            }
            finally
            {
                Array.Clear(rented, 0, Math.Min(_batchSize, rented.Length));
                ArrayPool<DataGridGeneratedStreamUpdate<TItem, TKey>>.Shared.Return(rented);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
