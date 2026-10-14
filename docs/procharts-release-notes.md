# ProCharts templated controls — initial tested preview

## Included

Five lookless controls (`ProChart`, `ProChartLegend`, `ProChartToolbar`, `ProChartNavigator`, `ProChartToolTip`), a replaceable default/compact template, typed tooltip and point events, native legend toggles with stable original series colors, viewport commands/history, loading/error/empty states, and PNG/SVG/CSV exports.

The shared ReactiveUI gallery runs through separate Avalonia desktop and WebAssembly hosts. It exercises 35 existing chart series kinds, financial indicators, and a bounded 100,000-row three-channel streaming source. The chart libraries are independent of the DataGrid libraries.

## Continuation hardening

Tooltips are invalidated when immutable snapshots or axis formatting change, rather than leaving obsolete values under a stationary pointer. Loading/error/disabled transitions clear hover immediately. The chart removes and restores pointer handlers on visual detachment/reattachment without duplicating point activation. Pending exports are cancellable on detach, and overlapping save calls do not start duplicate work. Native legend visibility now respects `ChartLegendPosition.None` without changing the caller's model.

New automated regressions use actual Avalonia headless input for those lifecycle paths. Additional Chromium runs exercise fractional DPI (1.25), high DPI (2), and emulated tablet touch, including plot pan, native buttons/legend interaction, retemplating and PNG dimensions. These are emulated input checks, not physical-device certification.

The delivery workflow also creates a clean chart-only source ZIP, recompiles/runs its tests after extraction outside the repository, and builds an external XAML consumer using a fresh NuGet cache. Package hashes are compared with the exact local archives to prevent accidentally testing previously published packages.

## Delivery and validation

Delivery is gated on successful managed tests, existing chart regressions, the actual WebAssembly interaction suite, device checks, source round-trip and package-consumer validation. Every folder includes build identity and SHA-256 file manifests. `validation/summary.json` reports the exact executed counts and source revision; retained warnings and logs are not discarded. No NuGet.org publication or GitHub Pages deployment is performed by this workflow.

NuGet delivery uses the preview version `12.1.0.6-templated.1` for all four libraries. The repository's existing shared version is not changed. The source build uses the repository's pinned SDK and Avalonia 12.1.0. The desktop output is framework-dependent and is not an installer.

## Scope boundaries

This is a tested initial preview, not a claim of every feature in a commercial charting suite. The series layer reuses the existing cached Skia raster surface; it is not a new GPU-compute renderer. There is no new 3D engine, visual annotation editor, designer extension or full point-by-point screen-reader model. Browser validation covers Chromium, not Safari/Firefox. The desktop application is built on Linux; Windows/macOS native startup is not certified by these checks.

See `docs/procharts-controls.md` for the templating contract, ownership/threading rules, numeric-X limitations, soft downsampling budget, host export services and complete integration instructions.
