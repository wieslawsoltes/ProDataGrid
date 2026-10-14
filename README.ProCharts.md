# ProCharts templated Avalonia controls

The charting workspace is isolated from the DataGrid samples in `ProCharts.Controls.slnx`.
It contains four reusable libraries, five lookless controls, a shared MVVM gallery,
a desktop host, a real Avalonia WebAssembly host, headless tests and browser interaction tests.

Start here: [integration, templating, data ownership and validation](docs/procharts-controls.md).

```sh
dotnet run --project samples/ProCharts.Sample.Desktop/ProCharts.Sample.Desktop.csproj

dotnet workload install wasm-tools
dotnet publish samples/ProCharts.Sample.Browser/ProCharts.Sample.Browser.csproj -c Release -o artifacts/browser
python -m http.server 8000 --directory artifacts/browser/wwwroot
```

The gallery demonstrates 35 series kinds, viewport history, native legends, customizable tooltips,
PNG/SVG/CSV downloads, replacement control templates, light/dark themes, recovery states,
and 100,000 retained streaming rows across three synchronized channels.
All sample data is synthetic. Packages target .NET 8/10; the samples target .NET 10 and Avalonia 12.1.0.

The controls use the existing ProCharts Skia raster surface; this is not a new GPU-compute renderer.
See the integration guide for exact boundaries and the workflow artifacts for validation of a specific commit.
