// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;

namespace ProCharts
{
    /// <summary>Binds a fixed or trailing coordinate window of a synchronized stream to the ordinary chart-model contract.</summary>
    /// <remarks>
    /// Compose with CoalescingChartDataSource for worker-to-consumer delivery. This adapter does not own its source,
    /// create a scheduler or set renderer axes. Configure a numeric/date/log category axis for proportional Scatter X.
    /// Request WindowStart/WindowCount select an ordinal subwindow WITHIN the coordinate-filtered, neighbor-expanded
    /// rows; MaxPoints is then applied through the source's existing shared-index selector. The underlying source lock
    /// covers resolution and capture, with no temporary full-history snapshot, second data copy or request allocation.
    /// </remarks>
    public sealed class CoordinateWindowChartDataSource : IChartDataSource, IChartWindowInfoProvider, IDisposable
    {
        private readonly object _gate = new();
        private readonly SourceSubscription _subscription;
        private StreamingMultiSeriesChartDataSource? _source;
        private ChartCoordinateWindow _window;
        private EventHandler? _handlers;

        /// <summary>Creates a non-owning live view. The default window selects all retained observations.</summary>
        public CoordinateWindowChartDataSource(StreamingMultiSeriesChartDataSource source, ChartCoordinateWindow window = default)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _window = window;
            _subscription = new SourceSubscription(this);
            source.DataInvalidated += _subscription.Handler;
        }

        /// <summary>Raised once for a changed policy or forwarded source event, outside adapter/source locks.</summary>
        /// <remarks>
        /// Events run synchronously on the caller's thread. Subscriber exceptions occur after the source/policy change
        /// and do not roll it back. Concurrent producers may notify out of version order; UI models require dispatch.
        /// </remarks>
        public event EventHandler? DataInvalidated
        {
            add { lock (_gate) { ThrowIfDisposed(); _handlers += value; } }
            remove { lock (_gate) _handlers -= value; }
        }

        /// <summary>Gets or atomically replaces the selection policy. Equal policies do not raise another event.</summary>
        /// <remarks>
        /// Policy changes do not rewrite the consumer model's ordinal requests or axis limits. Configure the policy
        /// and model/request state on the consumer thread, using model deferral when resetting ordinal windows.
        /// A read concurrent with a policy change may complete using the policy captured at its entry.
        /// </remarks>
        public ChartCoordinateWindow Window
        {
            get { lock (_gate) { ThrowIfDisposed(); return _window; } }
            set
            {
                EventHandler? handlers;
                lock (_gate)
                {
                    ThrowIfDisposed();
                    if (_window == value) return;
                    _window = value;
                    handlers = _handlers;
                }
                handlers?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Gets whether this adapter has released its source and subscribers.</summary>
        public bool IsDisposed { get { lock (_gate) return _source == null; } }

        /// <summary>Captures source-owned immutable values, shared X and original identities for the configured window.</summary>
        /// <remarks>
        /// Returned WindowStart is relative to the source's retained history, not the coordinate view. Identities belong
        /// to this exact snapshot/session. Capture reuses the source's single-entry cache; another consumer can replace
        /// that cache without changing existing views. Source storage and per-series soft-budget conventions are unchanged.
        /// </remarks>
        public StreamingMultiSeriesChartView BuildView(ChartDataRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var state = CaptureState();
            return state.Source.CaptureCoordinateWindow(state.Window, request);
        }

        /// <inheritdoc />
        public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) => BuildView(request).Snapshot;

        /// <summary>Counts coordinate-filtered rows, including neighbors, before ordinal windowing or reduction.</summary>
        /// <remarks>
        /// Uses binary bounds without allocating/copying the history. A separate count and subsequent capture can see
        /// different producer versions, as with other live providers; the capture itself always reclamps atomically.
        /// ChartModel ordinal pan/zoom/follow-latest applies within this filtered row domain, not the whole source.
        /// </remarks>
        public int? GetTotalCategoryCount()
        {
            var state = CaptureState();
            return state.Source.CountCoordinateWindow(state.Window);
        }

        /// <summary>Detaches notifications and releases references without clearing or disposing the underlying stream.</summary>
        /// <remarks>
        /// Idempotent. Reads/subscriptions/policy access after disposal fail; removing a handler is still safe.
        /// In-flight reads or already captured subscriber invocations may finish; Dispose does not wait for them.
        /// Deterministically dispose the model, outer delivery adapter and this adapter in that order.
        /// </remarks>
        public void Dispose()
        {
            StreamingMultiSeriesChartDataSource? source;
            lock (_gate)
            {
                source = _source;
                if (source == null) return;
                _source = null;
                _handlers = null;
            }
            source.DataInvalidated -= _subscription.Handler;
        }

        private (StreamingMultiSeriesChartDataSource Source, ChartCoordinateWindow Window) CaptureState()
        {
            lock (_gate) { ThrowIfDisposed(); return (_source!, _window); }
        }

        private void ForwardInvalidation()
        {
            EventHandler? handlers;
            lock (_gate) { if (_source == null) return; handlers = _handlers; }
            handlers?.Invoke(this, EventArgs.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (_source == null) throw new ObjectDisposedException(nameof(CoordinateWindowChartDataSource));
        }

        // A long-lived stream must not retain an abandoned view/model graph. Dispose still detaches immediately.
        private sealed class SourceSubscription
        {
            private readonly WeakReference<CoordinateWindowChartDataSource> _owner;
            public SourceSubscription(CoordinateWindowChartDataSource owner)
            { _owner = new(owner); Handler = OnInvalidated; }
            public EventHandler Handler { get; }
            private void OnInvalidated(object? sender, EventArgs args)
            {
                if (_owner.TryGetTarget(out var owner)) owner.ForwardInvalidation();
                else if (sender is IChartDataSource source) source.DataInvalidated -= Handler;
            }
        }
    }
}
