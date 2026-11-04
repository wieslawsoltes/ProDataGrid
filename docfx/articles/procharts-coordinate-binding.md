# Live coordinate windows in chart models

`CoordinateWindowChartDataSource` connects a synchronized stream's fixed or trailing X window to the ordinary `ChartModel` data-source contract. A model can now follow elapsed coordinates without an application rebuilding and publishing detached snapshots for every source event.

## Compose source, coordinate policy and UI delivery

```csharp
using ProCharts;
using ProCharts.Avalonia;

// Create this consumer pipeline on the Avalonia UI thread and retain it in its owner.
var source = new StreamingMultiSeriesChartDataSource(65_536, new[]
{
    new StreamingChartSeries("Reference", ChartSeriesKind.Scatter),
    new StreamingChartSeries("Measured", ChartSeriesKind.Scatter)
});
using var coordinate = new CoordinateWindowChartDataSource(source, ChartCoordinateWindow.Latest(30));
using var delivered = ChartDataSourceDispatch.Create(coordinate);
using var model = new ChartModel();
using (model.DeferRefresh())
{
    model.CategoryAxis.Kind = ChartAxisKind.Value;
    model.Request.MaxPoints = 256;
    model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
    model.DataSource = delivered;
}
// Bind model to the view while this owner remains alive.
// Append ordered producer rows to source, not to the model or UI on a worker.
```

The declarations illustrate ownership and reverse disposal order; do not return these objects from a scope that has already disposed them. Keep long-lived models/adapters as owner fields. Neither adapter owns or clears the stream. Stop application producers as appropriate, then dispose the model, delivery adapter and coordinate adapter.

`ChartCoordinateWindow.Latest(30)` means thirty source X units. When X is seconds, that is thirty recorded seconds. OLE Automation dates use days; use `TimeSpan.FromMinutes(30).TotalDays` for thirty minutes of that encoding. The anchor is the newest accepted row, including missing values, not the current wall clock. No timer, timezone/calendar conversion or interpolation is introduced.

## Policies and ordinary navigation

The default `ChartCoordinateWindow.All` uses all retained rows. `Between(minimumX, maximumX)` selects an inclusive fixed interval. `Latest(span)` re-anchors to the newest retained row on each capture. Factories reject invalid/nonfinite bounds and negative spans before any policy change. Zero spans select the latest row. Optional immediate neighbors follow the same rules as the direct coordinate-query APIs, including empty ranges outside retained history.

Policies are immutable value objects, so both fixed endpoints change atomically:

```csharp
using (model.DeferRefresh())
{
    model.Interaction.FollowLatest = false;
    model.Request.WindowStart = null;
    model.Request.WindowCount = null;
    coordinate.Window = ChartCoordinateWindow.Between(1_000, 1_100);
}
```

Changing a policy raises one source invalidation; assigning the same policy is a no-op. Existing request fields are not silently discarded. The order is:

1. Resolve the coordinate policy and optional neighbors against retained source rows.
2. Clamp request `WindowStart` / `WindowCount` **within that resolved row domain**.
3. Apply the existing per-channel common-index reduction and capture owned values/identities.

The adapter's count provider reports the coordinate-filtered row count before ordinal windowing or reduction. Existing model row pan, zoom, `ShowLatest(n)` and follow-latest therefore operate inside this domain. A source's current count and a subsequent snapshot can observe different producer versions; capture always reclamps under one source lock.

The coordinate Latest policy is independent of the model's ordinal `Interaction.FollowLatest` flag. Latest keeps moving with the source even if an ordinal subwindow is manually panned. To inspect history without that movement, pin a fixed coordinate interval first. To return to all source rows, set `Window = ChartCoordinateWindow.All`; resetting only the model's ordinal request does not remove the coordinate policy. A pinned interval is not an archive: its data can eventually be evicted by source capacity.

## Snapshot identity, axes and threading

`coordinate.BuildView(model.Request)` returns the source's existing `StreamingMultiSeriesChartView`, including its original 64-bit identity map and retained-history metadata. It shares the source's single-entry cache with direct queries; there is no second numerical copy. Another reader may replace that cache but cannot mutate already returned views. Use an identity map only with hits on that exact snapshot. `WindowStart` in the result remains an absolute offset into captured retained history, not a coordinate-relative offset.

The policy filters data; **it does not set explicit renderer axis limits**. Auto axes fit the selected observations, not necessarily the requested interval's nominal endpoints. Set explicit axis limits separately when empty margins or a precise fixed plot extent are required. Existing Line/Area series remain category-positioned; use the tested numeric/date Scatter path for proportional X placement. This work does not add numeric-line interpolation or change crosshair/gesture coordinate semantics.

Policy changes and model/request/UI mutation belong to the consumer thread. Source producers may use the synchronized stream's documented thread-safety contract. The adapter forwards source events on the caller's thread; compose it before the coalescing dispatcher adapter as shown above for UI delivery. Policy state is captured at read entry, so a concurrent policy change can coexist with a finishing read using the previous policy. Events execute outside locks. Subscriber exceptions occur after committed source/policy changes and do not roll them back.

Disposal detaches events and releases source/subscriber references. Already executing reads or captured event handlers may complete; disposal does not wait for them. New reads, subscriptions and policy accesses fail afterward; removing a handler stays safe. A weak source subscription prevents an abandoned view/model from being retained by a long-lived stream, but deterministic disposal is still recommended.

## Sample and diagnostics

The existing **Synchronized streams** sample now includes **Latest 120 X units**, **Pin displayed interval** and **Latest 512 rows**. Pinning reads the displayed snapshot's coordinate endpoints, not a separately read producer version. Changing policies/detail does not replay accepted input. Closing/switching the sample still stops its synthetic timer and disposes both adapters/model. Compiled bindings and view-model commands remain separate from the lifecycle behavior.

The core differential suite compares every reduction mode and ordinal subwindow against existing direct source APIs. Additional tests cover gaps, neighbors, dates/extremes, empty/evicted windows, model navigation, ownership, callbacks, disposal/GC, concurrent capture and zero-allocation warm reads/counts. Real headless worker-to-UI tests exercise the full composition; sample tests route the actual buttons and export pinned/following native images.

```sh
dotnet run --project tests/ProCharts.Benchmarks/ProCharts.Benchmarks.csproj -c Release
```

The COORDINATE-BINDING section compares sixteen append-and-capture frames using a manual full-history capture plus linear scan, the already available direct trailing query, and the bound adapter. All accept the same rows and check selected values/gaps/identities. Inputs, constructor/seeding and full validation are excluded from timing; ingestion and all capture work/allocations are included. Two warmups precede seven rotating-order rounds. The deliberately expensive manual pattern is not the previous production renderer; the direct API is shown to avoid suggesting the adapter invents binary lookup or is faster than calling it directly.

Counting/lookup costs O(log retained rows); uncached selection/copying remains O(window rows × channels) under the source lock. Equivalent cached capture/count calls allocate nothing in the adapter/source, but new views, events, schedulers and rendering can allocate. Per-series budgets remain soft and disjoint features can retain the whole selected window. No physical-GPU, FPS, peak-memory or worst-case-latency improvement is implied.
