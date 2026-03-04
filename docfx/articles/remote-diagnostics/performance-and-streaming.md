# Performance and Streaming

This page summarizes runtime behavior that helps keep remote diagnostics responsive.

## Pause-First Defaults

`DevToolsRemoteAttachHostOptions` defaults:

- `StartWithPreviewPaused = true`
- `StartWithMetricsPaused = true`
- `StartWithProfilerPaused = true`

This avoids expensive stream fanout when a client connects but has not opened corresponding tabs yet.

## Paused Domains Do Not Publish

When metrics/profiler/preview are paused in `InProcessRemoteStreamSource`:

- stream payload publication is skipped
- periodic sampling timers are disabled where applicable

This reduces CPU and memory overhead compared with buffering unused updates.

## Bounded Stream Fanout

`RemoteStreamSessionHub` enforces per-session bounds:

- `MaxQueueLengthPerSession` (default `2048`)
- `MaxDispatchBatchSize` (default `256`)

Dropped stream items are counted and emitted in protocol monitor statistics.

## Active-Tab Refresh Strategy

Both managed and web clients should avoid refreshing every domain continuously.

Recommended pattern:

- load tree + selection first
- refresh only currently active right-pane tab
- subscribe to streams only for domains that need live data

## Transport Diagnostics

Use protocol monitor snapshots and transport UI to inspect:

- sent/received message counts and bytes
- keepalive cadence
- recent protocol timeline
- stream drop/failure counters

This helps distinguish network issues from UI rendering bottlenecks.

## Tuning Checklist

- Keep preview in `svg` transport unless raster is required.
- Reduce preview FPS and frame size for remote/high-latency links.
- Set conservative `maxRows` in web client settings.
- Pause metrics/profiler while not actively inspecting those tabs.
- Keep attach binding localhost-only unless remote access is required.
