# ProCharts templated Avalonia controls

## Projects and quick start

The focused solution is `ProCharts.Controls.slnx`. Reusable libraries target .NET 8 and .NET 10. The desktop and browser samples use .NET 10, Avalonia 12.1.0 and ReactiveUI 24. The repository's `global.json` selects the SDK; `Directory.Packages.props` centrally pins dependencies. Restore requires NuGet access. The browser build requires the .NET `wasm-tools` workload.

```sh
# Desktop
dotnet run --project samples/ProCharts.Sample.Desktop/ProCharts.Sample.Desktop.csproj

# Actual Avalonia WebAssembly application
dotnet workload install wasm-tools
dotnet publish samples/ProCharts.Sample.Browser/ProCharts.Sample.Browser.csproj -c Release -o artifacts/browser
python -m http.server 8000 --directory artifacts/browser/wwwroot
```

Open the served page over HTTP. The host page only starts the .NET application; it does not render charts in HTML or JavaScript. Desktop publish output is framework-dependent unless explicitly published self-contained. It is not an OS installer.

The four independent library layers are `ProCharts` (model, data sources, axes, indicators and viewport), `ProCharts.Skia` (rendering, hit testing and image export), `ProCharts.Avalonia` (the low-level chart surface and dispatcher adapters), and `ProCharts.Controls` (lookless controls and themes). None of these libraries requires the DataGrid control. The packages can be produced with `dotnet pack`; this work does not publish them to NuGet.org.

## Application integration

Reference `ProCharts.Controls` and add the default resources after the application's Fluent theme:

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceInclude Source="avares://ProCharts.Controls/Themes/Generic.axaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

Use the control from a typed view whose view model exposes a `ChartModel`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:charts="using:ProCharts.Controls"
             xmlns:vm="using:YourApplication"
             x:DataType="vm:ChartViewModel">
  <charts:ProChart Model="{Binding Model}"
                   Title="Telemetry"
                   Subtitle="Observed values"
                   ShowNavigator="True" />
</UserControl>
```

A minimal source/model composition, created on the UI thread:

```csharp
using ProCharts;

var source = new StreamingMultiSeriesChartDataSource(10_000, new[]
{
    new StreamingChartSeries("Temperature", style: new ChartSeriesStyle
    {
        StrokeColor = ChartColor.FromRgb(129, 140, 248),
        StrokeWidth = 2
    })
});
var visibility = new SeriesVisibilityChartDataSource(source);
var model = new ChartModel { DataSource = visibility };
model.Request.MaxPoints = 500;
model.Request.DownsampleMode = ChartDownsampleMode.MinMax;
source.AppendRange(new double[] { 0, 1, 2 }, new double?[] { 21, 22, 21.5 });
// Expose model from the view model. Dispose model and visibility when their owner closes.
```

Both sample hosts register the Avalonia ReactiveUI scheduler with `UseReactiveUI`. Application commands must deliver state changes on the UI scheduler. The reusable controls use ordinary `ICommand` and do not require ReactiveUI.

## Full templating contract

`ProChart`, `ProChartLegend`, `ProChartToolbar`, `ProChartNavigator` and `ProChartToolTip` derive from `TemplatedControl`. Their default visuals are in `Themes/Generic.axaml`. `ProChartCompact` demonstrates replacing the entire chart template while preserving the model, viewport and commands.

`PART_Plot` is a `ProChartView`; the chart uses it for hit testing, point activation and image export. Omitting it is supported, but image export reports a clear error and its commands are disabled. `PART_ToolTip` is optional. Other named parts are composition conventions rather than mandatory code-behind dependencies. Replacement templates must forward their chosen input/display properties and supply their own layout/state visuals.

Customization properties include `Header`, `HeaderTemplate`, `Footer`, `FooterTemplate`, `LegendItemTemplate`, `ToolTipTemplate`, `EmptyContent`, `EmptyTemplate`, `LegendTheme`, `ToolbarTheme`, `NavigatorTheme`, `ChartStyle`, `Padding`, `Background`, `BorderBrush` and `BorderThickness`. Tooltip data contexts are `ChartPointInfo`; legend entries are `ChartLegendItem`. The controls never replace the application's `DataContext`.

Pseudo-classes are `:empty`, `:loading`, `:error`, `:exporting` and `:hover`. Both provided themes display data-availability states and disable the plot during loading or errors. The compact example intentionally omits the navigator and default header/footer/status layout. Application templates decide which optional chrome to include.

A native legend can hide and restore series when `Model.DataSource` is a `SeriesVisibilityChartDataSource`. Hidden entries remain in `LegendItems`, including when every series is hidden. Identity is the original series **index**, not its display name. Call `ResetVisibility()` after replacing an upstream schema. Snapshot styles, theme styles and explicit overrides are projected into `EffectiveChartStyle` without mutating them. Palette and override indices refer to original source series, keeping colors stable when a neighbor is hidden. Replacement templates must bind the plot's `ChartStyle` to `EffectiveChartStyle`, not directly to the input `ChartStyle`. Typed hierarchy sources remain directly bound in the gallery so their drill-down service stays available.

## Interaction and presentation

Commands cover zoom in/out, fit/reset, viewport undo/redo, latest-window following and PNG/SVG/CSV export. Input properties include `PointerTool`, `EnablePanZoom`, `EnableKeyboardNavigation`, `ZoomStep` and `MinWindowCount`, together with optional crosshair, legend, navigator, tooltip and toolbar visibility. The toolbar offers the crosshair, pan, rectangle zoom and measurement modes supplied by the existing plot surface.

The plot supports wheel zoom, pointer pan, keyboard arrows and +/-, Home/End, and Ctrl+Z/Ctrl+Y. Native buttons, checkboxes, a ComboBox and a Slider retain Avalonia keyboard/focus behavior. `PointInvoked` bubbles typed point information. `ViewportChanged` bubbles model viewport changes. Tooltips format values using their assigned primary or secondary axis. Programmatic model/axis/indicator APIs remain usable without the templated control.

The existing renderer exposes 35 series kinds through the gallery: cartesian lines/areas/bars, financial transformations, scatter/bubble/ranges, stacked and normalized series, histogram/Pareto/box-and-whisker, radar/funnel/pie/donut, heatmap, treemap/sunburst and gauge. The financial example adds an SMA indicator. Every sample uses deterministic synthetic data, not real market or business data.

## Streaming, ownership and performance

The 100,000-row gallery retains three synchronized channels (300,000 values) in a bounded ring buffer. It appends batches of 32 rows and coalesces timer delivery so stalled UI work cannot enqueue an unbounded backlog. Its viewport uses min/max selection of original samples. For these three gap-free channels, a 400-point per-channel budget produces a shared union of at most 1,200 aligned rows, and at most 3,600 drawn values across all channels. Row capacity and displayed point count are different quantities.

`MaxPoints` is a **per-channel soft selection budget**. Selected extrema, finite-run endpoints and gaps are retained; the union of indices is shared across channels. Many disjoint gaps or additional channels can increase the final count. Snapshot selection is O(window rows x channels), not constant-time rendering or a frames-per-second guarantee.

The chart does not own or dispose the assigned `ChartModel` or source. Its model/part subscriptions are managed on attachment, detachment, replacement and retemplating. The sample view model owns and disposes its models and adapters. The visibility adapter does not dispose its upstream source.

`ChartModel` and the visibility adapter belong on the consumer thread. For worker producers, compose `source -> ChartDataSourceDispatch.Create(source) -> SeriesVisibilityChartDataSource -> ChartModel`. Create the dispatcher adapter on the UI thread and dispose adapters separately. Dispatching notifications does not make an arbitrary source safe for concurrent mutation. The streaming source is synchronized, but competing producers must serialize monotonically increasing X values; it does not join late or out-of-order timestamps.

The plot uses the existing cached Skia raster/WriteableBitmap surface. Avalonia's browser compositor may use WebGL2 with software fallback, but chart series have **not** been rewritten as GPU compute shaders. Line/area data remain category-indexed; numeric X placement is available for scatter, bubble and range-area paths. Storing X values in a line source does not change that geometry contract. Use captured streaming-view row identities to map reduced hits back to source rows.

## Export and host integration

`CreateExport(format)` encodes the arranged plot into PNG/SVG or the displayed snapshot into CSV. Plot exports do not include surrounding toolbar/header chrome. PNG/SVG require an arranged, nonzero `PART_Plot`; SVG is a vector plot export, not an Avalonia screenshot.

`SaveAsync(format)` uses `IChartExportService` or the default storage-provider save picker. Hosts without a save picker must supply a service, as the browser sample does. Save failures are observed in `ExportError`; cancellation is not an error. `IsExporting` prevents overlapping saves, and `LastExportTask` supports deterministic command tests. Detaching cancels a pending save token.

CSV uses invariant numbers and CRLF records. It escapes quotes/newlines, emits OHLC/bubble channels, leaves missing or nonfinite cells empty, and neutralizes common spreadsheet formula prefixes in text by default. This is the **displayed/reduced snapshot**, not an automatic full-history query. Use `ChartCsvExporter.Write` with a caller-owned writer and full-resolution snapshot for larger exports.

## Validation and boundaries

```sh
dotnet test tests/ProCharts.Controls.Tests/ProCharts.Controls.Tests.csproj -c Release
python -m pip install playwright==1.55.0 Pillow==11.3.0
python -m playwright install --with-deps chromium
python tests/ProCharts.Browser/validate.py --root artifacts/browser/wwwroot --output artifacts/browser-tests
```

The browser suite performs actual mouse/keyboard interaction using read-only Avalonia visual-tree bounds, verifies real downloads, exercises template replacement and recovery states, and captures every renderer's plot. The exhaustive type-gallery section explicitly calls the sample's selection API: these are renderer smoke tests, not exhaustive mathematical coverage of every algorithm. Exact source identity, logs, JSON reports, screenshots and TRX files are retained by the workflow. Consult the report from the specific commit for pass/fail results.

This addition does not claim every commercial charting feature. It does not add a 3D chart engine, point-by-point screen-reader navigation, graphical annotation editor, designer extension, full managed-AOT validation matrix or new GPU renderer. Desktop hosts still require exercising on each target OS, and Safari/Firefox/touch devices require separate validation beyond the Chromium suite. Existing chart algorithms are exercised separately by the repository's chart regression suite.
