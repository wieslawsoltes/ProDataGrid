# Remote Diagnostics Overview

`ProDiagnostics` now supports remote-first diagnostics workflows through `ProDiagnostics.Remote` and a static web client.

This lets you inspect and control an Avalonia app:

- In-process (managed DevTools window).
- Local loopback remote mode (managed DevTools over remote protocol).
- Out-of-process from a browser (static web client).
- Out-of-process from custom .NET clients.

## Remote Stack

Core pieces:

- `ProDiagnostics.Remote`: protocol contracts, serializer, transports, routers, streaming hub, security policy, monitoring.
- `DevToolsRemoteAttachHost`: host adapter that exposes an inspected `AvaloniaObject` tree over WebSocket attach.
- `DevToolsRemoteLoopbackSession`: local host + client bootstrap used by remote-enabled managed DevTools mode.
- `src/ProDiagnostics.Remote.WebClient`: static HTML/CSS/JS diagnostics UI.

## Quick Start Paths

### 1. Managed DevTools (remote-enabled)

- Attach DevTools normally.
- Press `F11` to open remote-enabled mode.
- Use `F12` for standard in-process mode.

### 2. Host endpoint for external clients

- Start `DevToolsRemoteAttachHost` on app startup.
- Default endpoint: `ws://127.0.0.1:29414/attach`.
- Connect from browser or custom client.

### 3. Static web client

- Serve `src/ProDiagnostics.Remote.WebClient` with any static HTTP server.
- Connect to the host WebSocket endpoint.

## Feature Coverage

Remote protocol coverage includes snapshots, commands, and streams for:

- Trees and selection
- Properties and pseudo-classes
- Code/source documents
- Bindings
- Styles
- Resources
- Assets
- Events
- Breakpoints
- Logs
- Metrics
- Profiler
- Elements 3D
- Overlay options
- Preview and preview input injection

## Related Topics

- [Remote Protocol](protocol.md)
- [Hosting and Attach Endpoints](hosting-and-attach.md)
- [Managed DevTools Remote Runtime](managed-devtools-remote-runtime.md)
- [Web Client](web-client.md)
- [Preview and Elements 3D](preview-and-elements3d.md)
- [Performance and Streaming](performance-and-streaming.md)
- [Troubleshooting](troubleshooting.md)
