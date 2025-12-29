# ProDataGrid

[![Build](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/build.yml)

[![Release](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml/badge.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/actions/workflows/release.yml)
[![GitHub Release](https://img.shields.io/github/v/release/wieslawsoltes/Avalonia.Controls.DataGrid.svg)](https://github.com/wieslawsoltes/Avalonia.Controls.DataGrid/releases)

| Badge | Package | Downloads |
| --- | --- | --- |
| [![NuGet](https://img.shields.io/nuget/v/ProDataGrid.svg)](https://www.nuget.org/packages/ProDataGrid/) | [ProDataGrid](https://www.nuget.org/packages/ProDataGrid/) | [![Downloads](https://img.shields.io/nuget/dt/ProDataGrid.svg)](https://www.nuget.org/packages/ProDataGrid/) |
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

## Documentation

- DocFX articles: `docfx/articles/intro.md`
- DocFX entry page: `docfx/index.md`

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
