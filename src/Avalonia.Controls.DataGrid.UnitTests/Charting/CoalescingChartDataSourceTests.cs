// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ProCharts;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Charting
{
    public sealed class CoalescingChartDataSourceTests
    {
        [Fact]
        public void Producer_Bursts_Post_Once_Without_Building_Or_Executing_On_The_Producer()
        {
            ProbeSource source = new();
            QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            int events = 0;
            adapter.DataInvalidated += (sender, _) =>
            {
                Assert.Same(adapter, sender); Assert.Same(context, SynchronizationContext.Current); events++;
            };
            for (int i = 0; i < 10000; i++) source.Emit();
            Assert.True(adapter.HasPendingUpdate); Assert.Equal(1, context.Posts); Assert.Equal(1, context.Pending);
            Assert.Equal(0, events); Assert.Equal(0, source.Builds);
            Assert.True(context.PumpOne());
            Assert.Equal(1, events); Assert.False(adapter.HasPendingUpdate); Assert.False(context.PumpOne());
            source.Emit(); Assert.Equal(2, context.Posts); context.PumpOne(); Assert.Equal(2, events);
        }

        [Fact]
        public void Reads_Forward_The_Same_Request_And_Snapshot_Without_Consuming_The_Pending_Event()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            ChartDataRequest request = new() { WindowStart = 7, WindowCount = 13, MaxPoints = 8 };
            source.Emit();
            Assert.Same(source.Snapshot, adapter.BuildSnapshot(request));
            Assert.Same(request, source.LastRequest); Assert.True(adapter.HasPendingUpdate);
            Assert.Null(adapter.GetTotalCategoryCount());
            Assert.False(adapter.TryBuildUpdate(request, source.Snapshot, out _));
            int events = 0; adapter.DataInvalidated += (_, _) => events++;
            context.PumpOne(); Assert.Equal(1, events);
        }

        [Fact]
        public void Incremental_Update_Contract_Receives_The_Actual_Consumers_Previous_Snapshot()
        {
            IncrementalProbe source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            ChartDataRequest request = new(); ChartDataSnapshot previous = ChartDataSnapshot.Empty;
            Assert.True(adapter.TryBuildUpdate(request, previous, out var update));
            Assert.Same(request, source.LastRequest); Assert.Same(previous, source.Previous);
            Assert.Equal(source.Update, update); Assert.Equal(731, adapter.GetTotalCategoryCount());
        }

        [Fact]
        public void Invalidations_During_Delivery_Are_Deferred_Without_Recursion_Or_Holding_The_State_Lock()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            int events = 0, depth = 0;
            adapter.DataInvalidated += (_, _) =>
            {
                Assert.Equal(1, ++depth);
                if (++events == 1)
                {
                    Task producer = Task.Run(() => { for (int i = 0; i < 100; i++) source.Emit(); });
                    Assert.True(producer.Wait(TimeSpan.FromSeconds(5)), "Delivery held the producer lock.");
                    Assert.True(adapter.HasPendingUpdate); Assert.Equal(0, context.Pending);
                    Assert.False(context.PumpOne());
                }
                depth--;
            };
            source.Emit(); context.PumpOne();
            Assert.Equal(1, events); Assert.Equal(1, context.Pending);
            context.PumpOne(); Assert.Equal(2, events); Assert.False(adapter.HasPendingUpdate);
        }

        [Fact]
        public void Delivery_Exception_Does_Not_Wedge_Future_Updates_Or_Drop_Reentrant_Work()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            int events = 0;
            adapter.DataInvalidated += (_, _) =>
            {
                if (++events == 1) { source.Emit(); throw new InvalidOperationException("subscriber"); }
            };
            source.Emit();
            Assert.Throws<InvalidOperationException>(() => context.PumpOne());
            Assert.Equal(1, context.Pending); Assert.True(adapter.HasPendingUpdate);
            context.PumpOne(); Assert.Equal(2, events);
            source.Emit(); context.PumpOne(); Assert.Equal(3, events);
        }

        [Fact]
        public void Failed_Post_Preserves_Dirty_State_And_Allows_Explicit_Or_Source_Retry()
        {
            ProbeSource source = new(); QueuedContext context = new() { FailNext = true };
            using CoalescingChartDataSource adapter = new(source, context);
            int events = 0; adapter.DataInvalidated += (_, _) => events++;
            Assert.Throws<InvalidOperationException>(() => source.Emit());
            Assert.True(adapter.HasPendingUpdate); Assert.Equal(0, context.Pending);
            adapter.RequestRefresh(); context.PumpOne(); Assert.Equal(1, events);
            context.FailNext = true;
            Assert.Throws<InvalidOperationException>(() => adapter.RequestRefresh());
            source.Emit(); context.PumpOne(); Assert.Equal(2, events); Assert.False(adapter.HasPendingUpdate);
        }

        [Fact]
        public void Dispose_Detaches_And_Suppresses_Queued_Or_Stale_Source_Callbacks_Without_Disposing_Source()
        {
            ProbeSource source = new(); QueuedContext context = new();
            CoalescingChartDataSource adapter = new(source, context);
            int events = 0; EventHandler subscriber = (_, _) => events++;
            adapter.DataInvalidated += subscriber;
            EventHandler? stale = source.SavedHandlers;
            source.Emit(); adapter.Dispose(); adapter.Dispose();
            Assert.Equal(0, source.SubscriberCount); Assert.True(adapter.IsDisposed); Assert.False(adapter.HasPendingUpdate);
            context.PumpOne(); stale!(source, EventArgs.Empty); source.Emit();
            Assert.Equal(0, events); Assert.Equal(1, context.Posts); Assert.False(source.Disposed);
            adapter.DataInvalidated -= subscriber;
            Assert.Throws<ObjectDisposedException>(() => adapter.DataInvalidated += subscriber);
            Assert.Throws<ObjectDisposedException>(() => adapter.RequestRefresh());
            Assert.Throws<ObjectDisposedException>(() => adapter.BuildSnapshot(new()));
            Assert.Throws<ObjectDisposedException>(() => adapter.GetTotalCategoryCount());
            Assert.Throws<ObjectDisposedException>(() => adapter.TryBuildUpdate(new(), ChartDataSnapshot.Empty, out _));
        }

        [Fact]
        public void Disposing_From_A_Subscriber_Cancels_Followup_Without_Deadlock()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            adapter.DataInvalidated += (_, _) => { source.Emit(); adapter.Dispose(); };
            source.Emit(); context.PumpOne(); Assert.False(context.PumpOne()); Assert.True(adapter.IsDisposed);
        }

        [Fact]
        public void Abandoned_Adapter_Is_Not_Retained_By_The_Source_Or_Queued_Callback()
        {
            ProbeSource source = new(); QueuedContext context = new();
            WeakReference<CoalescingChartDataSource> weak = CreateAbandoned(source, context);
            for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); }
            Assert.False(weak.TryGetTarget(out _));
            context.PumpOne(); source.Emit(); Assert.Equal(0, source.SubscriberCount);
            GC.KeepAlive(source); GC.KeepAlive(context);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference<CoalescingChartDataSource> CreateAbandoned(ProbeSource source, QueuedContext context)
        {
            CoalescingChartDataSource adapter = new(source, context);
            adapter.DataInvalidated += static (_, _) => throw new InvalidOperationException("Dead adapter delivered.");
            source.Emit();
            return new WeakReference<CoalescingChartDataSource>(adapter);
        }

        [Fact]
        public void Concurrent_Producers_Queue_One_Delivery_And_Preserve_All_Accepted_Inputs()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            int deliveredCount = 0;
            adapter.DataInvalidated += (_, _) => deliveredCount = source.Emitted;
            Parallel.For(0, 8, _ => { for (int i = 0; i < 1000; i++) source.Emit(); });
            Assert.Equal(8000, source.Emitted); Assert.Equal(1, context.Pending); Assert.Equal(1, context.Posts);
            context.PumpOne(); Assert.Equal(8000, deliveredCount);
        }

        [Fact]
        public void Pending_Invalidations_And_Reused_Queue_Delivery_Allocate_Nothing_In_The_Adapter()
        {
            ProbeSource source = new(); QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            for (int i = 0; i < 1000; i++) { source.Emit(); context.PumpOne(); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) source.Emit();
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
            context.PumpOne();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { source.Emit(); context.PumpOne(); }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void Latest_State_Delivery_Reduces_Model_Refreshes_But_Keeps_Final_Range_Data_And_Window_Follow()
        {
            StreamingRangeChartDataSource source = new(256);
            // Follow-latest intentionally normalizes a full-history window to an unlimited one.
            // Seed more than forty observations so this fixture establishes a genuinely bounded viewport.
            for (int i = 0; i < 60; i++) source.Append(new ChartRangeSample(i, i, i + 2));
            QueuedContext context = new();
            using CoalescingChartDataSource adapter = new(source, context);
            using ChartModel model = new();
            using (model.DeferRefresh())
            {
                model.Request.DownsampleMode = ChartDownsampleMode.None;
                model.DataSource = adapter;
            }
            Assert.True(model.ShowLatest(40));
            Assert.Equal(40, model.Snapshot.Categories.Count);
            int changes = 0; model.SnapshotChanged += (_, _) => changes++;
            ChartDataSnapshot initial = model.Snapshot;
            for (int i = 60; i <= 600; i++) source.Append(new ChartRangeSample(i, i, i + 2));
            Assert.Equal(0, changes); Assert.Same(initial, model.Snapshot);
            context.PumpOne(); Assert.Equal(1, changes);
            Assert.Equal(40, model.Snapshot.Categories.Count);
            Assert.Equal(561, model.Snapshot.Series[0].XValues![0]);
            Assert.Equal(600, model.Snapshot.Series[0].XValues![^1]);
            Assert.Equal(601, source.TotalSamples);
            Assert.Equal(source.BuildSnapshot(model.Request), model.Snapshot);
        }

        [Fact]
        public void Invalid_Arguments_Fail_Without_An_Implicit_Thread_Pool_Context()
        {
            ProbeSource source = new(); QueuedContext context = new();
            Assert.Throws<ArgumentNullException>(() => new CoalescingChartDataSource(null!, context));
            Assert.Throws<ArgumentNullException>(() => new CoalescingChartDataSource(source, null!));
            using CoalescingChartDataSource adapter = new(source, context);
            Assert.Throws<ArgumentNullException>(() => adapter.BuildSnapshot(null!));
            Assert.Throws<ArgumentNullException>(() => adapter.TryBuildUpdate(null!, source.Snapshot, out _));
            Assert.Throws<ArgumentNullException>(() => adapter.TryBuildUpdate(new(), null!, out _));
            Assert.Equal(0, context.Posts);
        }

        private sealed class QueuedContext : SynchronizationContext
        {
            private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new(4);
            public int Posts { get; private set; }
            public bool FailNext { get; set; }
            public int Pending { get { lock (_queue) return _queue.Count; } }
            public override void Post(SendOrPostCallback d, object? state)
            {
                lock (_queue)
                {
                    if (FailNext) { FailNext = false; throw new InvalidOperationException("Post failed."); }
                    Posts++; _queue.Enqueue((d, state));
                }
            }
            public bool PumpOne()
            {
                (SendOrPostCallback Callback, object? State) work;
                lock (_queue) { if (_queue.Count == 0) return false; work = _queue.Dequeue(); }
                SynchronizationContext? previous = Current;
                SetSynchronizationContext(this);
                try { work.Callback(work.State); }
                finally { SetSynchronizationContext(previous); }
                return true;
            }
        }

        private class ProbeSource : IChartDataSource, IDisposable
        {
            private EventHandler? _handlers;
            private int _emitted;
            public event EventHandler? DataInvalidated { add => _handlers += value; remove => _handlers -= value; }
            public int SubscriberCount => _handlers?.GetInvocationList().Length ?? 0;
            public EventHandler? SavedHandlers => _handlers;
            public int Emitted => Volatile.Read(ref _emitted);
            public int Builds { get; private set; }
            public ChartDataRequest? LastRequest { get; protected set; }
            public bool Disposed { get; private set; }
            public ChartDataSnapshot Snapshot { get; } = new(Array.Empty<string?>(), Array.Empty<ChartSeriesSnapshot>());
            public void Emit() { Interlocked.Increment(ref _emitted); _handlers?.Invoke(this, EventArgs.Empty); }
            public ChartDataSnapshot BuildSnapshot(ChartDataRequest request) { Builds++; LastRequest = request; return Snapshot; }
            public void Dispose() { Disposed = true; }
        }

        private sealed class IncrementalProbe : ProbeSource, IChartIncrementalDataSource, IChartWindowInfoProvider
        {
            public ChartDataSnapshot? Previous { get; private set; }
            public ChartDataUpdate Update { get; } = new(ChartDataSnapshot.Empty, ChartDataDelta.Full);
            public int? GetTotalCategoryCount() => 731;
            public bool TryBuildUpdate(ChartDataRequest request, ChartDataSnapshot previousSnapshot, out ChartDataUpdate update)
            { LastRequest = request; Previous = previousSnapshot; update = Update; return true; }
        }
    }
}
