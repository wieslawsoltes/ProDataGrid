# Diagnostics and Tooling

ProDataGrid exposes lifecycle hooks and diagnostics helpers that make it easier to verify virtualization behavior and performance characteristics.

## Row Lifecycle Hooks

Instrumentation and diagnostics for row reuse are exposed through the existing container lifecycle overrides:

- `PrepareContainerForItemOverride` is called whenever a row is realized or re-used.
- `ClearContainerForItemOverride` runs before a row is recycled or removed; the row's `DataContext` is cleared here.
- `OnCleanUpVirtualizedItem` runs while recycling and sees the old `DataContext` before `ClearContainerForItemOverride` resets it.
- `NotifyRowPrepared`/`NotifyRowRecycling` are helper methods you can call from tests or custom presenters to trigger the same pipeline.

Example diagnostic subclass:

```csharp
sealed class TrackingDataGrid : DataGrid
{
  public List<object?> Prepared { get; } = new();
  public List<object?> Cleared { get; } = new();
  public List<object?> Cleaned { get; } = new();

  protected override void PrepareContainerForItemOverride(DataGridRow element, object item)
  {
    base.PrepareContainerForItemOverride(element, item);
    Prepared.Add(item);
  }

  protected override void ClearContainerForItemOverride(DataGridRow element, object item)
  {
    Cleared.Add(item);
    base.ClearContainerForItemOverride(element, item);
  }

  protected override void OnCleanUpVirtualizedItem(DataGridRow element)
  {
    if (element.DataContext is { } item)
    {
      Cleaned.Add(item);
    }
    base.OnCleanUpVirtualizedItem(element);
  }
}
```

Use these hooks to assert recycling order, verify placeholders vs real items, or capture telemetry on how often rows are realized and cleared in your scenarios.

## Recycling Diagnostics

`TrimRecycledContainers`, `KeepRecycledContainersInVisualTree`, and `RecycledContainerHidingMode` let you tune the recycling pool. The sample app includes a diagnostics page to visualize these settings.

## ProDiagnostics DevTools Integration

For runtime inspection beyond control-level hooks, use `ProDiagnostics`:

- Left tree pane with `Combined`/`Visual`/`Logical` scope tabs.
- Right diagnostics tabs (`Properties`, `Code`, `Elements 3D`, `Resources`, `Assets`, `Events`, `Breakpoints`, `Logs`, `Metrics`, `Bindings`, `Profiler`, `Styles`, `Transport`, `Settings`).
- Cross-tab selection synchronization (tree, properties, styles, code, 3D).
- Overlay tools (margin/padding, rulers, info panel, extension lines, live-hover overlay).
- Source-location tracing (portable PDB) visible in properties/styles/resources/assets/code.

Launch defaults:

- `F12` for standard in-process mode.
- `F11` for remote-enabled mode (loopback attach runtime).
- `Ctrl+Shift` to inspect the hovered control.

See [ProDiagnostics](prodiagnostics.md) for setup and [Remote Diagnostics Overview](remote-diagnostics/index.md) for out-of-process attach flows.

## Metrics and Activities

See `metrics-and-activities.md` for OpenTelemetry-friendly diagnostics and instrumentation details.
