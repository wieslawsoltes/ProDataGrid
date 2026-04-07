# Hosting and Attach Endpoints

This page covers how to expose diagnostics data to remote clients.

## `DevToolsRemoteAttachHost` (App-Level Host)

`DevToolsRemoteAttachHost` is the easiest way to publish diagnostics for an app window/root.

```csharp
private DevToolsRemoteAttachHost? _remoteAttachHost;

private void OnDesktopStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
{
    if (sender is not IClassicDesktopStyleApplicationLifetime lifetime || lifetime.MainWindow is null)
        return;

    _remoteAttachHost = new DevToolsRemoteAttachHost(
        lifetime.MainWindow,
        new DevToolsRemoteAttachHostOptions
        {
            HttpOptions = HttpAttachServerOptions.Default with
            {
                Port = 29414,
                Path = "/attach",
                BindingMode = HttpAttachBindingMode.Localhost
            },
            StartWithPreviewPaused = true,
            StartWithMetricsPaused = true,
            StartWithProfilerPaused = true
        });

    _remoteAttachHost.StartAsync().GetAwaiter().GetResult();
}

private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
{
    _remoteAttachHost?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    _remoteAttachHost = null;
}
```

Default WebSocket endpoint:

- `ws://127.0.0.1:29414/attach`

## Host Options

Key `DevToolsRemoteAttachHostOptions` members:

- `HttpOptions`: WebSocket/HTTP listener settings.
- `SessionManagerOptions`: session timeout and max sessions.
- `StreamHubOptions`: per-session queue and dispatch batch limits.
- `EnableMutationApi`: allow/disallow mutation commands.
- `EnableStreamingApi`: allow/disallow streams.
- `StartWithPreviewPaused`, `StartWithMetricsPaused`, `StartWithProfilerPaused`: stream startup policy.
- `EnableUdpTelemetryFallback`, `UdpTelemetryPort`: optional UDP bridge.
- `RequestTimeout`: per-request processing timeout.

## HTTP Binding and Security

`HttpAttachServerOptions` includes:

- `BindingMode`: `Localhost`, `ExplicitAddress`, `Any`
- `Port`
- `Path`
- `AccessPolicy`

`RemoteAccessPolicyOptions` controls network/token checks:

- `AllowAnyIp`
- `AllowedRemoteAddresses`
- `TokenValidator`

Default policy is localhost-only unless explicitly opened.

## Low-Level Backends (`ProDiagnostics.Remote`)

For advanced scenarios, host transports directly:

- `HttpAttachServer`
- `NamedPipeAttachServer`

`NamedPipeAttachServerOptions` supports:

- `PipeName`
- `CurrentUserOnly`
- `MaxServerInstances`
- platform support on Windows/Linux/macOS

This is useful when you need custom message routing around `RemoteReadOnlyMessageRouter` and `RemoteMutationMessageRouter`.

## DataGridSample

`DataGridSample` is wired to start `DevToolsRemoteAttachHost` automatically on app startup and stop it on exit, so remote clients can connect out of the box.
