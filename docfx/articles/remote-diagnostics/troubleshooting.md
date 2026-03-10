# Remote Diagnostics Troubleshooting

## Connected, But No Data Appears

Symptoms:

- Handshake succeeds.
- KeepAlive messages continue.
- Trees/properties remain empty.

Checks:

- Verify the client is sending snapshot requests (for example `diagnostics.tree.snapshot.get`).
- Verify the selected scope (`combined`/`visual`/`logical`) matches expected target.
- In web client, click `Refresh` on tree and active tab after connect.

## `Reflection-based serialization has been disabled`

Cause:

- `System.Text.Json` reflection serializer path is blocked in app settings/AOT mode.

Resolution:

- Use protocol APIs that pass source-generated metadata (`RemoteJsonSerializerContext`).
- Keep host/client on current `ProDiagnostics.Remote` package so serializer context includes all request/response types.

## `Call from invalid thread`

Cause:

- Diagnostics UI objects were accessed from a non-UI thread.

Resolution:

- Ensure read/mutation operations that touch Avalonia object graph are marshaled to UI thread (host adapters already do this).
- Avoid custom host wrappers that bypass the built-in router/source implementations.

## Request Timeout (`Remote request timed out: ...`)

Cause:

- host-side snapshot generation too slow
- blocked UI thread
- dead/closed connection

Resolution:

- increase request timeout (`DevToolsRemoteAttachHostOptions.RequestTimeout` / client `RequestTimeout`)
- pause expensive streams and retry
- request a narrower scope or lighter snapshot payload

## Preview/3D Tab Appears Blank

Checks:

- Confirm stream is not paused.
- Confirm SVG/PNG transport supported by host.
- For Elements 3D SVG export, Skia backend is required for vector export path.
- Retry with smaller frame dimensions and lower zoom/depth settings.

## Metrics/Profiler Show No Live Updates

Cause:

- Remote hosts start with preview/metrics/profiler paused by default.

Resolution:

- Resume from client UI (`Pause`/`Resume` toggle).
- Or override host defaults via `DevToolsRemoteAttachHostOptions`:
  - `StartWithMetricsPaused = false`
  - `StartWithProfilerPaused = false`

## Remote Access Denied

Cause:

- `RemoteAccessPolicyOptions` rejected remote address/token.

Resolution:

- Keep localhost binding for local debugging.
- For remote hosts, configure `AllowAnyIp`/`AllowedRemoteAddresses` and optional `TokenValidator`.
- Confirm endpoint path matches host (`/attach` by default).

## Version/Contract Mismatch

Symptoms:

- handshake succeeds but data fields are missing or commands fail validation.

Resolution:

- keep host and clients on matching branch/package revision
- ensure requested feature flags are supported by host `HelloAck.EnabledFeatures`
