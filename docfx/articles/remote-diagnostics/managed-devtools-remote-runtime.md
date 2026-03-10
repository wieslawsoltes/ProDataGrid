# Managed DevTools Remote Runtime

Managed DevTools supports two launch modes:

- `F12`: standard in-process diagnostics runtime.
- `F11`: remote-enabled runtime (loopback host + remote client + domain adapters).

## Why Remote-Enabled Mode

Remote-enabled mode validates the same protocol/contracts used by out-of-process clients while keeping a local DevTools window.

It is useful to:

- verify remote parity before using external tools.
- isolate expensive streams (preview/metrics/profiler) behind pause controls.
- test remote selection synchronization behavior from the managed UI.

## Loopback Runtime

`DevToolsRemoteLoopbackSession` bootstraps:

- a local `DevToolsRemoteAttachHost`
- a `RemoteDiagnosticsClient`
- typed domain services (`ReadOnly`, `Mutation`, `Stream`)

`MainViewModel` binds page view models to these domain services and subscribes to remote selection stream updates.

## Configure Remote Launch Behavior

You can set defaults in `DevToolsOptions`:

```csharp
this.AttachDevTools(new DevToolsOptions
{
    UseRemoteRuntime = true, // open in remote mode when ConnectOnStartup = true
    RemoteLoopbackOptions = new DevToolsRemoteLoopbackOptions
    {
        UseDynamicPort = true,
        HostOptions = new DevToolsRemoteAttachHostOptions
        {
            StartWithPreviewPaused = true,
            StartWithMetricsPaused = true,
            StartWithProfilerPaused = true
        }
    }
});
```

## Selection and Tab Sync

Remote mode keeps selection synchronized between:

- tree tabs (`Combined`, `Visual`, `Logical`)
- `Properties`
- `Code`
- `Bindings`
- `Styles`
- `Elements 3D`
- Ctrl+Shift inspect operations

This is driven through remote selection snapshots/stream payloads.

## Overlay and Inspect Commands

Overlay controls are available in remote mode through mutation commands:

- set overlay options (info/rulers/extension lines/highlight/clipping)
- toggle live-hover overlay
- inspect hovered target (`diagnostics.inspect.hovered`)

## Stream Defaults

When hosted through `DevToolsRemoteAttachHost`, preview/metrics/profiler can start paused (default host policy) and resume on-demand from UI actions.
