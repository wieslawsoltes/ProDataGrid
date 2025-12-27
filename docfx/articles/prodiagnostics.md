# ProDiagnostics

`ProDiagnostics` provides Avalonia developer tools UI as a standalone package. It focuses on runtime inspection and debugging:

- Visual and logical tree inspection.
- Property and style inspection with live values.
- Routed event tracking.
- Layout exploration and renderer diagnostics overlays.

## Installation

Install from NuGet:

```sh
dotnet add package ProDiagnostics
```

## Quick Start

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
