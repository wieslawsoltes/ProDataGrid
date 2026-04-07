# Web Client

The repository contains a static remote diagnostics client:

- path: `src/ProDiagnostics.Remote.WebClient`
- stack: HTML + CSS + ES modules
- protocol: `ProDiagnostics.Remote` v1 framing over WebSocket

## Run Locally

Serve the folder with a static HTTP server:

```sh
cd src/ProDiagnostics.Remote.WebClient
python3 -m http.server 8080
```

Open:

- `http://127.0.0.1:8080`

Connect to:

- `ws://127.0.0.1:29414/attach`

## UI Libraries

The client integrates optional libraries loaded from CDN:

- Tabulator (data grids)
- jsTree (tree UI)
- Chart.js (metrics/profiler/depth charts)
- svg-pan-zoom (SVG pan/zoom for preview and Elements 3D)

If they are unavailable, the app falls back to basic rendering paths.

## Feature Coverage

Web tabs currently include:

- `Properties`
- `Preview`
- `Elements 3D`
- `Resources`
- `Assets`
- `Events`
- `Breakpoints`
- `Logs`
- `Metrics`
- `Bindings`
- `Profiler`
- `Styles`
- `Transport`
- `Code`
- `Settings`

Selection state is synchronized with the active tree scope (`Combined`, `Visual`, `Logical`).

## Mutations and Editors

The web client supports remote control flows such as:

- property set/clear/null
- pseudo-class updates
- breakpoints add/remove/toggle/clear
- events defaults/clear/disable-all
- logs level and max-entry settings
- metrics/profiler pause-resume
- elements 3D root/filter settings
- code document open

## Settings and Limits

Client-side settings (saved to local storage):

- auto-refresh on selection
- auto-refresh tree on connect
- max streamed rows retained per table

These settings limit browser memory pressure during long sessions.
