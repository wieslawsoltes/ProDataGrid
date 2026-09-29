// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Threading;

namespace ProCharts
{
    /// <summary>Coalesces source invalidations and delivers them asynchronously on an explicit synchronization context.</summary>
    /// <remarks>
    /// This schedules notifications, not source mutations or snapshot reads. The source must support concurrent
    /// reads/updates when producers run on another thread. No observations are dropped by this adapter, but intermediate
    /// notifications are deliberately combined. At most one callback is queued or delivering at a time. Updates during
    /// delivery schedule a later callback rather than recursively rebuilding a model. The context must implement Post
    /// asynchronously, exactly once per successful call, and must not enqueue a callback when Post throws.
    /// </remarks>
    public sealed class CoalescingChartDataSource : IChartIncrementalDataSource, IChartWindowInfoProvider, IDisposable
    {
        private static readonly SendOrPostCallback DeliverCallback = static state => ((Subscription)state!).Deliver();
        private readonly object _gate = new();
        private readonly SynchronizationContext _context;
        private readonly Subscription _subscription;
        private IChartDataSource? _source;
        private EventHandler? _handlers;
        private bool _pending, _scheduled, _disposed;

        /// <summary>Wraps a source without owning it. No implicit context or thread-pool fallback is selected.</summary>
        /// <remarks>
        /// Construct and bind the consumer model on its owning thread. The constructor does not post an initial event;
        /// the model can read its initial snapshot normally. Use RequestRefresh to explicitly schedule another read.
        /// </remarks>
        public CoalescingChartDataSource(IChartDataSource source, SynchronizationContext context)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _subscription = new Subscription(this);
            source.DataInvalidated += _subscription.Handler;
        }

        /// <summary>Raised on the supplied context, once for all invalidations preceding that delivery.</summary>
        /// <remarks>Subscriber exceptions propagate on the context; later updates can still schedule delivery.</remarks>
        public event EventHandler? DataInvalidated
        {
            add { lock (_gate) { ThrowIfDisposed(); _handlers += value; } }
            remove { lock (_gate) _handlers -= value; }
        }

        /// <summary>Gets whether at least one invalidation is waiting to be delivered.</summary>
        /// <remarks>A running callback has already consumed its invalidation; new updates can set this true again.</remarks>
        public bool HasPendingUpdate { get { lock (_gate) return _pending; } }

        /// <summary>Gets whether this adapter has been disposed.</summary>
        public bool IsDisposed { get { lock (_gate) return _disposed; } }

        /// <summary>Schedules a consumer invalidation, coalescing it with an already pending delivery.</summary>
        /// <remarks>
        /// May be called from any thread. If Post throws, the update remains pending and the exception propagates to
        /// this caller. A later RequestRefresh or source event retries scheduling. No automatic retry loop is started.
        /// </remarks>
        public void RequestRefresh() => Invalidate(throwIfDisposed: true);

        /// <summary>Forwards a snapshot read on the caller's thread without copying or consuming a pending notification.</summary>
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return GetSource().BuildSnapshot(request);
        }

        /// <summary>Forwards the source's retained-count provider; returns null when the source does not provide one.</summary>
        public int? GetTotalCategoryCount() => (GetSource() as IChartWindowInfoProvider)?.GetTotalCategoryCount();

        /// <summary>Forwards incremental updates using the consumer's actual previous snapshot, never a fabricated delta.</summary>
        /// <remarks>A source without incremental support returns false so ChartModel uses a full snapshot.</remarks>
        public bool TryBuildUpdate(ChartDataRequest request, ChartDataSnapshot previousSnapshot, out ChartDataUpdate update)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(previousSnapshot);
            IChartDataSource source = GetSource();
            if (source is IChartIncrementalDataSource incremental)
                return incremental.TryBuildUpdate(request, previousSnapshot, out update);
            update = default;
            return false;
        }

        /// <summary>Detaches from the source, releases subscribers and suppresses callbacks not already delivering.</summary>
        /// <remarks>
        /// The underlying source is not disposed. An in-flight snapshot read or event delivery may complete; Dispose
        /// does not block a worker or wait for subscribers. Dispose the consumer model and this adapter separately.
        /// Queued callbacks hold only a weak reference to the adapter and cannot retain its model/source graph.
        /// </remarks>
        public void Dispose()
        {
            IChartDataSource? source;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _pending = false; _scheduled = false; _handlers = null;
                source = _source; _source = null;
            }
            source!.DataInvalidated -= _subscription.Handler;
        }

        private IChartDataSource GetSource()
        {
            lock (_gate) { ThrowIfDisposed(); return _source!; }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CoalescingChartDataSource));
        }

        private void Invalidate(bool throwIfDisposed)
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    if (throwIfDisposed) ThrowIfDisposed();
                    return;
                }
                _pending = true;
                if (_scheduled) return;
                _scheduled = true;
            }
            PostDelivery();
        }

        private void PostDelivery()
        {
            // Do not call external scheduling code under the lock. Dispose may race this call;
            // a subsequently queued weak callback will see the disposed state and do nothing.
            lock (_gate) { if (_disposed) return; }
            try { _context.Post(DeliverCallback, _subscription); }
            catch
            {
                lock (_gate) _scheduled = false;
                throw;
            }
        }

        private void Deliver()
        {
            EventHandler? handlers;
            lock (_gate)
            {
                if (_disposed) return;
                if (!_pending) { _scheduled = false; return; }
                _pending = false;
                handlers = _handlers;
            }
            try { handlers?.Invoke(this, EventArgs.Empty); }
            finally
            {
                bool again;
                lock (_gate)
                {
                    again = !_disposed && _pending;
                    _scheduled = again;
                }
                if (again) PostDelivery();
            }
        }

        private sealed class Subscription
        {
            private readonly WeakReference<CoalescingChartDataSource> _owner;
            public Subscription(CoalescingChartDataSource owner)
            { _owner = new(owner); Handler = OnInvalidated; }
            public EventHandler Handler { get; }
            public void Deliver() { if (_owner.TryGetTarget(out var owner)) owner.Deliver(); }
            private void OnInvalidated(object? sender, EventArgs args)
            {
                if (_owner.TryGetTarget(out var owner)) owner.Invalidate(throwIfDisposed: false);
                else if (sender is IChartDataSource source) source.DataInvalidated -= Handler;
            }
        }
    }
}
