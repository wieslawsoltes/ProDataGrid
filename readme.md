# ProDataGrid

[![Build](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml)

[![Release](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml)
[![GitHub Release](https://img.shields.io/github/v/release/wieslawsoltes/Avalonia.Controls.DataGrid.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/releases)

| Badge | Package | Downloads |
| --- | --- | --- |
| [![NuGet](https://img.shields.io/nuget/v/ProDataGrid.svg)](https://www.nuget.org/packages/ProDataGrid/) | [ProDataGrid](https://www.nuget.org/packages/ProDataGrid/) | [![Downloads](https://img.shields.io/nuget/dt/ProDataGrid.svg)](https://www.nuget.org/packages/ProDataGrid/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDataGrid.Charting.svg)](https://www.nuget.org/packages/ProDataGrid.Charting/) | [ProDataGrid.Charting](https://www.nuget.org/packages/ProDataGrid.Charting/) | [![Downloads](https://img.shields.io/nuget/dt/ProDataGrid.Charting.svg)](https://www.nuget.org/packages/ProDataGrid.Charting/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDataGrid.FormulaEngine.svg)](https://www.nuget.org/packages/ProDataGrid.FormulaEngine/) | [ProDataGrid.FormulaEngine](https://www.nuget.org/packages/ProDataGrid.FormulaEngine/) | [![Downloads](https://img.shields.io/nuget/dt/ProDataGrid.FormulaEngine.svg)](https://www.nuget.org/packages/ProDataGrid.FormulaEngine/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDataGrid.FormulaEngine.Excel.svg)](https://www.nuget.org/packages/ProDataGrid.FormulaEngine.Excel/) | [ProDataGrid.FormulaEngine.Excel](https://www.nuget.org/packages/ProDataGrid.FormulaEngine.Excel/) | [![Downloads](https://img.shields.io/nuget/dt/ProDataGrid.FormulaEngine.Excel.svg)](https://www.nuget.org/packages/ProDataGrid.FormulaEngine.Excel/) |
| [![NuGet](https://img.shields.io/nuget/v/ProCharts.svg)](https://www.nuget.org/packages/ProCharts/) | [ProCharts](https://www.nuget.org/packages/ProCharts/) | [![Downloads](https://img.shields.io/nuget/dt/ProCharts.svg)](https://www.nuget.org/packages/ProCharts/) |
| [![NuGet](https://img.shields.io/nuget/v/ProCharts.Avalonia.svg)](https://www.nuget.org/packages/ProCharts.Avalonia/) | [ProCharts.Avalonia](https://www.nuget.org/packages/ProCharts.Avalonia/) | [![Downloads](https://img.shields.io/nuget/dt/ProCharts.Avalonia.svg)](https://www.nuget.org/packages/ProCharts.Avalonia/) |
| [![NuGet](https://img.shields.io/nuget/v/ProCharts.Skia.svg)](https://www.nuget.org/packages/ProCharts.Skia/) | [ProCharts.Skia](https://www.nuget.org/packages/ProCharts.Skia/) | [![Downloads](https://img.shields.io/nuget/dt/ProCharts.Skia.svg)](https://www.nuget.org/packages/ProCharts.Skia/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDiagnostics.svg)](https://www.nuget.org/packages/ProDiagnostics/) | [ProDiagnostics](https://www.nuget.org/packages/ProDiagnostics/) | [![Downloads](https://img.shields.io/nuget/dt/ProDiagnostics.svg)](https://www.nuget.org/packages/ProDiagnostics/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDiagnostics.Transport.svg)](https://www.nuget.org/packages/ProDiagnostics.Transport/) | [ProDiagnostics.Transport](https://www.nuget.org/packages/ProDiagnostics.Transport/) | [![Downloads](https://img.shields.io/nuget/dt/ProDiagnostics.Transport.svg)](https://www.nuget.org/packages/ProDiagnostics.Transport/) |
| [![NuGet](https://img.shields.io/nuget/v/ProDiagnostics.Viewer.svg)](https://www.nuget.org/packages/ProDiagnostics.Viewer/) | [ProDiagnostics.Viewer](https://www.nuget.org/packages/ProDiagnostics.Viewer/) | [![Downloads](https://img.shields.io/nuget/dt/ProDiagnostics.Viewer.svg)](https://www.nuget.org/packages/ProDiagnostics.Viewer/) |

ProDataGrid is a high-performance DataGrid control for Avalonia.

## Quick Start

Install the package:

```sh
dotnet add package ProDataGrid
```

Include the theme in `App.axaml`:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
</Application.Styles>
```

```xml
<Application.Styles>
  <SimpleTheme />
  <StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Simple.v2.xaml" />
</Application.Styles>
```

Basic XAML usage:

```xml
<DataGrid ItemsSource="{Binding People}"
          AutoGenerateColumns="False"
          UseLogicalScrollable="True">
  <DataGrid.Columns>
    <DataGridTextColumn Header="ID" Binding="{Binding Id}" Width="60" />
    <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*" />
    <DataGridCheckBoxColumn Header="Active" Binding="{Binding IsActive}" Width="80" />
  </DataGrid.Columns>
</DataGrid>
```

### Performance-sensitive grids

The compatibility themes remain the default. Large grids can additionally load
`Themes/Optimized.xaml` and opt individual grids or columns into smaller retained
Avalonia templates, direct typed text access, or drawn display cells. Start with the
feature-preserving retained themes when applications rely on normal Avalonia controls,
layout, validation, automation, row details, or editing:

```xml
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Optimized.xaml" />

<DataGrid RowTheme="{StaticResource DataGridOptimizedFeatureUnfrozenRowTheme}"
          CellTheme="{StaticResource DataGridOptimizedCellTheme}"
          ColumnHeaderTheme="{StaticResource DataGridOptimizedFeatureColumnHeaderTheme}"
          UseLightweightFiller="True" />
```

See [Optimized retained and drawn cell paths](docfx/articles/optimized-cell-paths.md)
for the retained-layout, direct-content, hierarchy, binding-fallback, and drawn-display
options and their compatibility trade-offs.

### Source generators

The optional `ProDataGrid.SourceGenerators` incremental analyzer produces reflection-free,
AOT-ready schemas, typed columns and fast-path accessors. It can also augment partial
ViewModels and generate operation controllers, registries, and code-only Avalonia or
ReactiveUI views, with invalid configurations reported as compile-time diagnostics.

```xml
<PackageReference Include="ProDataGrid.SourceGenerators" PrivateAssets="all" />
```

Annotate a row model and, optionally, a partial ViewModel:

```csharp
using ProDataGrid.SourceGeneration;

[GenerateDataGridColumns(ProviderName = "TradeGridSchema", Strict = true)]
public sealed class TradeRow
{
    [DataGridKey]
    public int Id { get; init; }

    [DataGridColumn(Header = "Symbol", Order = 0)]
    public string Symbol { get; init; } = string.Empty;

    [DataGridColumn(DataGridColumnKind.Numeric, Header = "Price", Order = 1)]
    public decimal Price { get; init; }
}

[GenerateDataGridViewModel(typeof(TradeRow), ProviderName = "TradeGridSchema")]
public sealed partial class TradesViewModel : ReactiveObject { }
```

The generated `ColumnDefinitions` and `FastPathOptions` properties bind directly to the grid:

```xml
<DataGrid ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          FastPathOptions="{Binding FastPathOptions}"
          AutoGenerateColumns="False" />
```

See the [source-generator guide](docfx/articles/source-generators-feature-spec.md) for
schema discovery, data operations, generated views, customization, and diagnostics.

### More than DataGrid

ProDataGrid ships as a family of packages beyond the grid itself. Quick starts below cover the most common add-ons.

#### Charting

ProCharts is an Excel-quality charting library built for ProDataGrid. `ProDataGrid.Charting` bridges grid data and pivots into chart series.

Quick start:

```sh
dotnet add package ProCharts.Avalonia
dotnet add package ProDataGrid.Charting
```

```csharp
using ProCharts;

var chartModel = new ChartModel
{
    Title = "Revenue by Quarter",
    Series = new List<ChartSeries>
    {
        ChartSeries.Line("Q1", new[] { 120d, 140d, 110d, 180d }),
        ChartSeries.Line("Q2", new[] { 160d, 130d, 150d, 210d })
    },
    XAxis = new ChartAxis { Title = "Quarter", Kind = ChartAxisKind.Category },
    YAxis = new ChartAxis { Title = "Revenue", Kind = ChartAxisKind.Value }
};
```

Bind `chartModel` to `ProChartView.ChartModel` in your Avalonia view.

#### Formula Engine

The formula engine provides Excel-compatible parsing, evaluation, and recalculation. Use it in DataGrid formula columns or wire it to your own data model.

Quick start:

```sh
dotnet add package ProDataGrid.FormulaEngine
dotnet add package ProDataGrid.FormulaEngine.Excel
```

```csharp
var definition = new DataGridFormulaColumnDefinition
{
    Header = "Total",
    Formula = "=[Price] * [Quantity]",
    AllowCellFormulas = true
};
```

#### ProDiagnostics

ProDiagnostics provides Avalonia developer tools UI for runtime inspection and debugging, with optional out-of-process transport and a viewer app.

Quick start:

```sh
dotnet add package ProDiagnostics
```

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.MainWindow = new MainWindow();

    base.OnFrameworkInitializationCompleted();
    this.AttachDevTools();
}
```

Optional streaming + viewer:

```sh
dotnet add package ProDiagnostics.Transport
dotnet tool install -g prodiagnostics-viewer
```

## Nightly Packages (GitHub Packages)

Nightly builds are published to GitHub Packages with versions like `11.3.9-nightly.20241227.123`.

Add the GitHub Packages feed (requires a GitHub token with `read:packages`):

```sh
dotnet nuget add source https://nuget.pkg.github.com/wieslawsoltes/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text
```

GitHub Packages NuGet docs: https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry

Install a nightly package:

```sh
dotnet add package ProDataGrid --source github --prerelease
```

Or pin a specific nightly version:

```sh
dotnet add package ProDataGrid --source github --version 11.3.9-nightly.20241227.123
```

## Documentation

- [DocFX articles](docfx/articles/intro.md)
- [DocFX entry page](docfx/index.md)
- [Optimized retained and drawn cell paths](docfx/articles/optimized-cell-paths.md)
- [Hierarchy automation](docfx/articles/hierarchical-automation.md)

## Testing

Run standard tests:

```sh
dotnet test
```

Leak tests use Avalonia-style WeakReference/GC checks. Use the helper script to run only leak tests:

```powershell
.\tools\run-leak-tests.ps1
```

## ProDiagnostics

`ProDiagnostics` provides Avalonia developer tools UI as a standalone package. It focuses on runtime inspection and debugging:

- Visual and logical tree inspection.
- Property and style inspection with live values.
- Routed event tracking.
- Layout exploration and renderer diagnostics overlays.

### Installation

Install from NuGet:

```sh
dotnet add package ProDiagnostics
```

### Quick start

Attach DevTools after application initialization:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        desktop.MainWindow = new MainWindow();

    base.OnFrameworkInitializationCompleted();
    this.AttachDevTools();
}
```

By default DevTools opens with `F12`. You can also attach to a `TopLevel` or provide a custom key gesture or options.

## ProDiagnostics.Transport

`ProDiagnostics.Transport` streams metrics and activities over UDP so you can observe diagnostics out-of-process.

### Installation

Install from NuGet:

```sh
dotnet add package ProDiagnostics.Transport
```

### Quick start

Export diagnostics to the viewer:

```csharp
using ProDiagnostics.Transport;

var exporter = new DiagnosticsUdpExporter(new DiagnosticsUdpOptions
{
    Host = "127.0.0.1",
    Port = TelemetryProtocol.DefaultPort,
    ActivitySourceNames = new[] { "ProDataGrid.*" },
    MeterNames = new[] { "ProDataGrid.Diagnostic.Meter" }
});

exporter.Start();
```

Keep the exporter alive for the lifetime of the app and dispose it during shutdown.

## ProDiagnostics.Viewer

`ProDiagnostics.Viewer` is an Avalonia UI for live metrics and activities streamed over UDP.

### Installation

Install as a global tool:

```sh
dotnet tool install -g prodiagnostics-viewer
```

### Quick start

Run the viewer:

```sh
prodiagnostics-viewer
```

## License

ProDataGrid is licensed under the MIT License (see `licence.md`).

ProDiagnostics is licensed under the MIT License (see `licence.md`).

The original `Avalonia.Controls.DataGrid` and `Avalonia.Diagnostics` license is preserved in `licence-avalonia.md`.
