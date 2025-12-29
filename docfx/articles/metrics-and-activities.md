# Metrics and Activities

ProDataGrid exposes OpenTelemetry-friendly diagnostics via `ActivitySource` and `Meter` so you can track row generation, virtualization, selection changes, and collection view operations. The diagnostics can be consumed in-process (OpenTelemetry) or streamed out-of-process using the ProDiagnostics UDP transport and viewer.

## Enabling diagnostics

Diagnostics are opt-in. Enable them early in app startup (before any grids are created):

```csharp
AppContext.SetSwitch("ProDataGrid.Diagnostics.IsEnabled", true);
```

## Installation

Install the transport library from NuGet:

```bash
dotnet add package ProDiagnostics.Transport
```

Install the viewer as a global tool:

```bash
dotnet tool install -g prodiagnostics-viewer
```

To update later:

```bash
dotnet tool update -g prodiagnostics-viewer
```

## Transport: ProDiagnostics UDP stream

`ProDiagnostics.Transport` provides a lightweight UDP protocol for streaming telemetry to an out-of-process viewer.

What it does:

- Sends `Hello` packets (session identity) plus Activity and Metric packets.
- Uses a binary protocol (`TelemetryProtocol.Version == 1`) capped at 64 KB per datagram.
- Supports tag limits (`MaxTagsPerMessage`) and wildcard filtering for source/meter names (`*` and `?` patterns).
- Ignores malformed packets on the receiver so the stream stays resilient.

### Exporter usage

```csharp
using ProDiagnostics.Transport;

var exporter = new DiagnosticsUdpExporter(new DiagnosticsUdpOptions
{
    Host = "127.0.0.1",
    Port = TelemetryProtocol.DefaultPort,
    HelloInterval = TimeSpan.FromSeconds(5),
    MaxTagsPerMessage = 32,
    IncludeActivityTags = true,
    IncludeMetricTags = true,
    ActivitySourceNames = new[] { "ProDataGrid.*" },
    MeterNames = new[] { "ProDataGrid.Diagnostic.Meter" }
});

exporter.Start();
```

Keep the exporter alive for the lifetime of the app and dispose it during shutdown.

## Viewer app

`ProDiagnostics.Viewer` is an Avalonia app that listens for UDP packets and visualizes them.

Run it from the repo:

```bash
dotnet run --project src/ProDiagnostics.Viewer/ProDiagnostics.Viewer.csproj
```

Viewer features:

- Session list populated from `Hello` packets (app name, process, machine, runtime).
- UDP port selection with pause/resume.
- Preset selection + reload (JSON files).
- Metrics and activities filters (name + alias matching).
- Column visibility toggles with resize/reorder in both grids.
- Metric sparklines + detailed tabs (double-click a metric row).
- Detail chart with min/avg/max thresholds based on the current window, plus pan/zoom and time-range selection.

## Presets

Presets are JSON files that live in a `Presets` folder. The viewer loads:

- Built-in presets from `Assets/Presets` in the app output.
- User presets from `%APPDATA%/ProDiagnostics.Viewer/Presets` (create the folder if it doesn't exist).

Preset example:

```json
{
  "name": "ProDataGrid",
  "description": "Focus on ProDataGrid diagnostics",
  "includeActivities": [
    "ProDataGrid.*"
  ],
  "includeMetrics": [
    "prodatagrid.*"
  ],
  "metricAliases": {
    "prodatagrid.refresh.time": "Refresh Time"
  },
  "activityAliases": {
    "ProDataGrid.DataGrid.RefreshRowsAndColumns": "Refresh Rows + Columns"
  }
}
```

## ActivitySource

ActivitySource name: `ProDataGrid.Diagnostic.Source`

Activities emitted:

- `ProDataGrid.DataGrid.RefreshRowsAndColumns`
- `ProDataGrid.DataGrid.RefreshRows`
- `ProDataGrid.DataGrid.UpdateDisplayedRows`
- `ProDataGrid.DataGrid.GenerateRow`
- `ProDataGrid.DataGrid.AutoGenerateColumns`
- `ProDataGrid.DataGrid.SelectionChanged`
- `ProDataGrid.CollectionView.Refresh`
- `ProDataGrid.CollectionView.Filter`
- `ProDataGrid.CollectionView.Sort`
- `ProDataGrid.CollectionView.Group`
- `ProDataGrid.CollectionView.GroupTemporary`
- `ProDataGrid.CollectionView.GroupPage`

Example listener:

```csharp
using System.Diagnostics;

var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "ProDataGrid.Diagnostic.Source",
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity =>
    {
        // Inspect activity.DisplayName and activity.Tags
    }
};

ActivitySource.AddActivityListener(listener);
```

## Metrics

Meter name: `ProDataGrid.Diagnostic.Meter`

Histograms (ms):

- `prodatagrid.refresh.time` - DataGrid refresh pass (rows + columns).
- `prodatagrid.rows.refresh.time` - Row refresh pass.
- `prodatagrid.rows.display.update.time` - Displayed rows update during virtualization.
- `prodatagrid.rows.generate.time` - Row generation and preparation.
- `prodatagrid.columns.autogen.time` - Auto-generated columns pass.
- `prodatagrid.selection.change.time` - SelectionChanged handling.
- `prodatagrid.collection.refresh.time` - Collection view refresh.
- `prodatagrid.collection.filter.time` - Filtering during refresh.
- `prodatagrid.collection.sort.time` - Sorting during refresh.
- `prodatagrid.collection.group.time` - Grouping during refresh.
- `prodatagrid.collection.group.temporary.time` - Temporary grouping for paging.
- `prodatagrid.collection.group.page.time` - Page-level grouping.

Counters:

- `prodatagrid.rows.realized.count` - Row containers realized.
- `prodatagrid.rows.recycled.count` - Row containers recycled.
- `prodatagrid.rows.prepared.count` - Row containers prepared.
- `prodatagrid.columns.autogen.count` - Columns auto-generated.
- `prodatagrid.selection.changed.count` - SelectionChanged events raised.

## Tags

Activities include context tags such as:

- `ClearRows`, `RecycleRows`, `AutoGenerateColumns`
- `Columns`, `Rows`, `SlotCount`
- `DisplayHeight`, `FirstDisplayedSlot`, `LastDisplayedSlot`, `DisplayedSlots`
- `RowIndex`, `Slot`, `Source` (`existing`, `new`, `recycled`, `own-container`)
- `AddedCount`, `RemovedCount`, `SelectionSource`, `UserInitiated`
- `SortDescriptions`, `GroupDescriptions`, `FilterEnabled`
- `PageSize`, `PageIndex`, `UsesLocalArray`, `IsGrouping`
- `AutoGeneratedColumns`

Counters use `Source` for row realization and `SelectionSource` for selection changes.

## OpenTelemetry integration

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddSource("ProDataGrid.Diagnostic.Source"))
    .WithMetrics(builder => builder.AddMeter("ProDataGrid.Diagnostic.Meter"));
```

## Notes

- Diagnostics are off by default; set the switch before any grid usage.
- UDP is best-effort and can drop packets; it is intended for live observability, not guaranteed delivery.
- Some metrics are nested (for example refresh includes filter/sort/group), so compare like-for-like scenarios.
