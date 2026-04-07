# ProDiagnostics Remote Web DevTools (Static Site)

Static HTML/CSS/JavaScript diagnostics client for the `ProDiagnostics.Remote` protocol.

## Features
- WebSocket remote attach using protocol v1 binary framing.
- Left tree pane with scope switching (`Combined`, `Visual`, `Logical`) and selection sync.
- Right tab suite:
  - `Properties`
  - `Elements 3D` (depth projection from tree data)
  - `Resources`
  - `Assets`
  - `Events`
  - `Breakpoints`
  - `Logs`
  - `Metrics`
  - `Bindings` (view-model context + active binding diagnostics for selected node)
  - `Profiler`
  - `Styles`
  - `Transport`
  - `Code` (source metadata preview from snapshots)
  - `Settings`
- Mutation commands for property set, breakpoints, events control, and logs level configuration.
- Streaming ingestion for logs/events/metrics/profiler topics.
- Local transport diagnostics timeline (message direction/kind/bytes/summary).

## Run locally

Because this is an ES module-based static site, serve the folder with a local static HTTP server:

```bash
cd src/ProDiagnostics.Remote.WebClient
python3 -m http.server 8080
```

Open:

```
http://127.0.0.1:8080
```

## Remote endpoint

Default WebSocket URL:

```
ws://127.0.0.1:29414/attach
```

This should match your host-side remote attach HTTP transport endpoint path.
