# Remote Protocol

`ProDiagnostics.Remote` uses a framed binary protocol over attach transports (WebSocket and named pipe).

## Protocol Basics

- Protocol version: `1`
- Frame header: 6 bytes (`version[1] + kind[1] + payloadLength[4]`)
- Default heartbeat interval: 5 seconds
- Default session timeout: 30 seconds
- Default max payload: `RemoteProtocol.MaxFramePayloadBytes`

## Message Kinds

- `Hello`
- `HelloAck`
- `HelloReject`
- `KeepAlive`
- `Disconnect`
- `Request`
- `Response`
- `Stream`
- `Error`

## Capability Negotiation

Client sends requested features in `Hello`. Host returns enabled features in `HelloAck`.

Common feature flags:

- `read-only`
- `mutation`
- `streaming`
- `trees`
- `selection`
- `properties`
- `preview`
- `code`
- `bindings`
- `styles`
- `resources`
- `assets`
- `elements3d`
- `overlay`
- `breakpoints`
- `events`
- `logs`
- `metrics`
- `profiler`

## Naming Conventions

- Request methods: `diagnostics.<domain>.<resource>.<action>`
- Snapshot reads: `diagnostics.<domain>.snapshot.get`
- Streams: `diagnostics.stream.<domain>`

## Read-Only Methods (Selected)

- `diagnostics.preview.capabilities.get`
- `diagnostics.preview.snapshot.get`
- `diagnostics.tree.snapshot.get`
- `diagnostics.selection.get`
- `diagnostics.properties.snapshot.get`
- `diagnostics.code.documents.get`
- `diagnostics.code.resolve-node`
- `diagnostics.bindings.snapshot.get`
- `diagnostics.styles.snapshot.get`
- `diagnostics.resources.snapshot.get`
- `diagnostics.assets.snapshot.get`
- `diagnostics.events.snapshot.get`
- `diagnostics.breakpoints.snapshot.get`
- `diagnostics.logs.snapshot.get`
- `diagnostics.metrics.snapshot.get`
- `diagnostics.profiler.snapshot.get`
- `diagnostics.elements3d.snapshot.get`
- `diagnostics.overlay.options.get`

## Mutation Methods (Selected)

- `diagnostics.inspect.hovered`
- `diagnostics.selection.set`
- `diagnostics.preview.paused.set`
- `diagnostics.preview.settings.set`
- `diagnostics.preview.input.inject`
- `diagnostics.properties.set`
- `diagnostics.state.pseudoclass.set`
- `diagnostics.code.document.open`
- `diagnostics.elements3d.root.set`
- `diagnostics.elements3d.root.reset`
- `diagnostics.elements3d.filters.set`
- `diagnostics.overlay.options.set`
- `diagnostics.overlay.live-hover.set`
- `diagnostics.breakpoints.*`
- `diagnostics.events.*`
- `diagnostics.logs.*`
- `diagnostics.metrics.*`
- `diagnostics.profiler.*`

## Stream Topics

- `diagnostics.stream.selection`
- `diagnostics.stream.preview`
- `diagnostics.stream.logs`
- `diagnostics.stream.events`
- `diagnostics.stream.metrics`
- `diagnostics.stream.profiler`

## Snapshot Metadata

Snapshot DTOs carry:

- `snapshotVersion`: schema version number.
- `generation`: monotonic snapshot generation.

Inspectable data rows also carry stable identifiers such as `nodeId`, `nodePath`, and domain row `id` where applicable.

## Serialization

Protocol payloads use `System.Text.Json` with source-generated metadata (`RemoteJsonSerializerContext`) to avoid reflection-only serializer paths.
