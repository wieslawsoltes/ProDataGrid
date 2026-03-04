# ProDiagnostics

`ProDiagnostics` is a standalone diagnostics package for Avalonia applications. It includes:

- A full managed DevTools window (tree inspection, properties, styles, events, logs, metrics, profiler, code, overlays, 3D inspection, and transport settings).
- A remote attach backend (`ProDiagnostics.Remote`) with HTTP and named-pipe transports.
- A static web remote client (`src/ProDiagnostics.Remote.WebClient`) that uses the same protocol surface.

## Installation

Install the managed DevTools package:

```sh
dotnet add package ProDiagnostics
```

Install optional remote contracts/transport primitives when hosting custom attach flows:

```sh
dotnet add package ProDiagnostics.Remote
```

Install optional UDP telemetry exporter package for external metrics/activity viewers:

```sh
dotnet add package ProDiagnostics.Transport
```

## Attach DevTools

Attach after app initialization:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.MainWindow = new MainWindow();

    base.OnFrameworkInitializationCompleted();
    this.AttachDevTools();
}
```

Launch gestures:

- `F12`: standard in-process DevTools mode.
- `F11`: remote-enabled DevTools mode (local loopback host/client runtime).
- `Ctrl+Shift`: inspect hovered control in the inspected app.

You can override launch behavior through `DevToolsOptions`:

```csharp
this.AttachDevTools(new DevToolsOptions
{
    Gesture = new KeyGesture(Key.F12),
    RemoteGesture = new KeyGesture(Key.F11),
    UseRemoteRuntime = false, // F12 default
    EnableRemoteGesture = true,
    LiveHoverOverlay = true,
    ShowOverlayInfo = true,
    ShowOverlayRulers = true,
    ShowOverlayExtensionLines = true
});
```

## Workspace Layout

The DevTools window is split into two panes:

- Left pane: always-visible tree tabs `Combined`, `Visual`, `Logical`.
- Right pane: diagnostics work tabs:
  - `Properties` (first tab)
  - `Code`
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
  - `Settings`

A splitter is provided between panes for resize.

## Selection and Sync

Selection is synchronized across:

- Tree tabs (`Combined`/`Visual`/`Logical`)
- `Properties`
- `Code`
- `Bindings`
- `Styles`
- `Elements 3D`
- `Ctrl+Shift` hover-inspect actions

This is driven by a shared selection model so tab switches keep context instead of dropping selection.

## Source Locations and Code Navigation

Source-location information is resolved via portable PDB metadata and is surfaced in:

- Properties and value frames
- Styles/style resolution rows
- Resources and assets rows
- Code tab documents (XAML and C#)

Tooltips include full source location text and an `Open` action. Code/source selection can round-trip with tree selection where source mapping is available.

## Elements 3D and Overlays

`Elements 3D` provides:

- Exploded 3D visual layering with orbit/yaw/pitch/roll, pan, zoom, and reset.
- Optional flattened 2D stacked-layers mode with per-row layer packing.
- Depth-range and max-layer visibility filtering.
- Root scoping (`Set selected root`, `Reset main root`) that keeps branch context.
- Grid and 3D selection synchronization.

Overlay tooling includes:

- Margin/padding visualization
- Overlay info panel
- Rulers
- Extension lines
- Live-hover overlay mode

Overlay adorners are rendered without clipping to the adorned target bounds.

## Logs, Metrics, and Profiler

- Logs support level filters, max entry limits, and clear behavior.
- Metrics and profiler support pause/resume and retention settings.
- Remote runtime paths avoid publishing metrics/profiler payloads while paused.

In remote-hosted scenarios, preview/metrics/profiler streams start paused by default to reduce startup overhead.

## Transport and External Viewer

`Transport` tab includes UDP export settings and runtime counters.

Viewer launch supports placeholders in arguments:

- `{host}`
- `{port}`
- `{protocol}`

Example:

```txt
--host {host} --port {port} --protocol {protocol}
```

## Remote Attach Host (Out-of-Process Clients)

`DevToolsRemoteAttachHost` can expose the inspected app over WebSocket attach:

```csharp
private DevToolsRemoteAttachHost? _remoteAttachHost;

private void OnDesktopStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
{
    if (sender is not IClassicDesktopStyleApplicationLifetime lifetime || lifetime.MainWindow is null)
        return;

    _remoteAttachHost = new DevToolsRemoteAttachHost(lifetime.MainWindow);
    _remoteAttachHost.StartAsync().GetAwaiter().GetResult();
}

private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
{
    _remoteAttachHost?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    _remoteAttachHost = null;
}
```

Default endpoint:

- `ws://127.0.0.1:29414/attach`

See the dedicated remote section for protocol, host options, web client, and performance guidance.

## Related Articles

- [Diagnostics and Tooling](diagnostics-and-tooling.md)
- [Metrics and Activities](metrics-and-activities.md)
- [Remote Diagnostics Overview](remote-diagnostics/index.md)
