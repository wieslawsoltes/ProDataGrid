# ProDiagnostics

`ProDiagnostics` provides Avalonia developer tools UI as a standalone package. It focuses on runtime inspection and debugging:

- Visual and logical tree inspection.
- Property and style inspection with live values.
- Routed event tracking.
- Runtime logs capture and filtering.
- Runtime metrics capture and aggregation.
- Runtime profiler sampling (CPU/memory/GC deltas).
- Elements 3D visual-tree depth and stacking inspection.
- Transport/process UDP export settings for remote diagnostics viewers.
- ViewModels and bindings diagnostics.
- Layout exploration and renderer diagnostics overlays.
- In-window settings for overlay/inspection behavior.

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
