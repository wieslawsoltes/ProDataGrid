# Coalesced chart updates and worker-to-UI delivery

`CoalescingChartDataSource` combines source invalidations that arrive before a consumer callback is serviced. Accepted source observations are unchanged; intermediate presentation snapshots are intentionally skipped. This is useful when a producer emits many small updates and the consumer otherwise rebuilds its snapshot for every event.

## Avalonia integration

Create and bind the adapter on the UI thread, and keep it alive for the consumer model's lifetime:

```csharp
using ProCharts;
using ProCharts.Avalonia;

// Run this setup on the Avalonia UI thread. Keep these objects in the owning component.
var source = new StreamingRangeChartDataSource(65_536, "Live envelope");
using var delivered = ChartDataSourceDispatch.Create(source);
using var model = new ChartModel();
using (model.DeferRefresh())
{
    model.CategoryAxis.Kind = ChartAxisKind.Value;
    model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
    model.Request.MaxPoints = 2_000;
    model.DataSource = delivered;
}
// Bind model to ProChartView.ChartModel while this owner is alive.
// A producer may append to source; do not mutate model, its requests or the view on a worker.
// At shutdown, stop/join the producer as appropriate, then dispose model followed by delivered.
```

The `using` declarations illustrate ownership/disposal order, not a factory that returns already disposed objects. In a long-lived application component, keep both as fields and dispose them when that component closes. The adapter does not own or dispose the underlying source. Independently dispose any source that implements its own disposal contract.

`ChartDataSourceDispatch.Create(source, priority)` uses the Avalonia UI dispatcher; the default priority is Background. It requires UI-thread construction and does not change a global synchronization context. A burst normally produces one queued delivery. The UI context decides when to service it: this is not a fixed-rate throttle, timer, debounce interval, vsync scheduler or guarantee of one redraw per frame. Actual UI scheduling and rendering may allocate resources independently of this adapter.

## UI-independent scheduling

The core adapter only depends on `IChartDataSource` and an explicit `System.Threading.SynchronizationContext`:

```csharp
using var delivered = new CoalescingChartDataSource(source, consumerContext);
```

The context must implement `Post` asynchronously, invoke each successfully posted callback exactly once, and not enqueue a callback when `Post` throws. The adapter never silently chooses the thread pool or the current context. Pass the actual consumer's context; a generic thread-pool context does not provide UI affinity.

There is at most one scheduled or delivering callback. An invalidation during delivery sets pending state and schedules a subsequent callback after subscriber invocation finishes, rather than recursively rebuilding the model. The adapter calls neither external scheduling code nor subscribers while holding its state lock. Concurrent producer invalidations are combined; input ordering and storage remain the wrapped source's responsibility.

## Data, incremental updates and thread safety

**Only notifications are dispatched.** `BuildSnapshot`, `TryBuildUpdate` and the retained-count provider run synchronously on their caller's thread. The adapter does not copy data, virtualize storage, make a mutable collection thread-safe or serialize a non-thread-safe source's operations. Use a source that supports the real concurrent producer/consumer pattern, such as the bounded streaming sources, or provide an application-level data ownership boundary. Source request objects and model state still belong to the consumer thread.

The adapter forwards `IChartIncrementalDataSource.TryBuildUpdate` with the consumer's actual previous snapshot. An incremental source must handle that snapshot/version gap correctly or return false, allowing a full rebuild. The adapter does not invent a delta for a skipped presentation state. `IChartWindowInfoProvider` is forwarded when supported, preserving follow-latest/window behavior.

Reading a snapshot manually does not consume a queued invalidation: subscribers may still need that notification. `RequestRefresh()` explicitly schedules one, coalescing with pending work. No event is posted merely by constructing the adapter. `HasPendingUpdate` reports invalidations not yet consumed by delivery; a running callback has consumed its original invalidation, but additional producer work can set the property true again.

The adapter does not drop accepted observations. Nevertheless, a bounded source may evict old observations according to its own capacity, and coalescing deliberately does not display every intermediate state. Persist/process observations at the producer when every transition matters; chart presentation is not an audit log. Stateful indicators should still consume every relevant observation before display notification reduction.

## Exceptions and lifecycle

A failed `Post` leaves the update pending and clears the scheduled flag. The exception propagates to the caller that attempted scheduling. A later source invalidation or `RequestRefresh()` retries; no automatic retry loop or background timer is created. Subscriber exceptions propagate on the consumer context. Cleanup in `finally` allows future updates to schedule and preserves invalidations received during the failing callback. Applications must handle/report exceptions at their normal dispatcher boundary.

`Dispose()` detaches the source handler, releases subscribers and suppresses queued callbacks that have not begun delivery. It is idempotent and does not wait for already delivering subscribers or in-flight source reads. A callback that already captured its subscribers may finish. Stop producers and coordinate consumer shutdown explicitly when the application requires stricter ordering. Public reads/new subscriptions/RequestRefresh after disposal fail; removing a subscription remains safe.

Source and queued subscriptions contain only a weak adapter reference. Abandoning an adapter therefore does not let a never-serviced dispatcher callback retain the model/source object graph. Deterministic disposal is still recommended to detach immediately. Underlying data source ownership is never transferred.

## Performance choices and verification

Producer-side `AppendRange` is often the simpler solution when the feed can form batches itself. `ChartModel.DeferRefresh()` combines consumer-side configuration changes. Coalescing adds an explicit scheduling boundary when independent/small producer notifications would otherwise rebuild the model many times. These mechanisms can be composed, but fewer presentation callbacks do not make an individual snapshot scan or renderer faster.

The core adapter performs no per-invalidation managed allocation after construction when used with a reusable test queue; real contexts can allocate scheduled jobs, and snapshot creation/subscribers can allocate independently. Steady-state tests distinguish adapter cost from source and scheduler cost.

The existing range-streaming benchmark now includes immediate scalar delivery, coalesced scalar delivery and the existing producer-batch alternative. It processes identical observations through actual `ChartModel` instances, verifies final numerical arrays and counts, and reports intermediate snapshot counts explicitly. It uses a deterministic same-thread queue to isolate policy cost, not to time physical UI scheduling. Headless tests separately validate a worker producer's actual Avalonia UI-thread delivery, native export and hit mapping.

```sh
dotnet run --project tests/ProCharts.RangeStreaming.Benchmarks/ProCharts.RangeStreaming.Benchmarks.csproj -c Release
```

The read-only charting workflow retains raw results in `range-streaming-performance.txt` beside its tests and `CoalescedWorkerRange` PNG/SVG preview. Two warmups and seven rotating-order runs are measured. No GPU throughput, screen presentation, peak memory, worst-case latency or FPS improvement is implied.
