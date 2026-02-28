# Custom Drawing Columns and Skia Draw Operations

`DataGridCustomDrawingColumn` is a bound column that renders display cell content through `DataGridCustomDrawingCell`. It is read-only by default, and supports opt-in editing via `IsReadOnly="False"`. It supports:

- low-level text rendering using the control text path (`FormattedText`),
- custom `ICustomDrawOperation` rendering (`DrawingContext.Custom(...)`),
- composition custom-visual rendering for draw operations (`RenderBackend=CompositionCustomVisual`),
- optional draw-operation-driven measure/arrange fast path,
- shared text layout caching for high-frequency text scenarios.

Use this column when you need lower render overhead than template columns and you want full control over drawing behavior.

## API Surface

Core types:

- `DataGridCustomDrawingColumn`
- `DataGridCustomDrawingCell`
- `DataGridCustomDrawingColumnDefinition` (for `ColumnDefinitionsSource`)
- `DataGridCustomDrawingMode` (`Text`, `DrawOperation`, `TextAndDrawOperation`)
- `DataGridCustomDrawingRenderBackend` (`ImmediateDrawOperation`, `CompositionCustomVisual`)
- `DataGridCustomDrawingTextLayoutCacheMode` (`PerCell`, `Shared`)
- `IDataGridCellDrawOperationFactory`
- `IDataGridCellDrawOperationMeasureProvider` (optional)
- `IDataGridCellDrawOperationArrangeProvider` (optional)
- `IDataGridCellDrawOperationItemCache` (optional per-item cache contract)
- `DataGridCellDrawOperationContext`
- `DataGridCellDrawOperationMeasureContext`
- `DataGridCellDrawOperationArrangeContext`

Primary column properties:

| Property | Type | Default | Notes |
| --- | --- | --- | --- |
| `Binding` | `IBinding` | n/a | Bound value used as source text/value for the cell. |
| `DrawOperationFactory` | `IDataGridCellDrawOperationFactory` | `null` | Factory creating per-cell draw operations. |
| `DrawingMode` | `DataGridCustomDrawingMode` | `Text` | Selects text path, draw-operation path, or both. |
| `RenderBackend` | `DataGridCustomDrawingRenderBackend` | `ImmediateDrawOperation` | Selects immediate draw-op rendering or composition custom-visual backend for draw-operation modes. |
| `TextLayoutCacheMode` | `DataGridCustomDrawingTextLayoutCacheMode` | `PerCell` | Per-cell text layout cache or shared per-column cache. |
| `SharedTextLayoutCacheCapacity` | `int` | `1024` | Max entry count for shared layout cache (minimum effective value: `1`). |
| `DrawOperationLayoutFastPath` | `bool` | `false` | Opt-in measure/arrange fast path driven by draw-operation provider interfaces. |
| `RenderInvalidationToken` | `int` | `0` | Increment to force redraw of realized custom drawing display cells. |
| `LayoutInvalidationToken` | `int` | `0` | Increment to force measure/arrange + redraw of realized custom drawing display cells. |
| `FontFamily`, `FontSize`, `FontStyle`, `FontWeight`, `FontStretch`, `Foreground`, `TextAlignment`, `TextTrimming` | standard text properties | inherited/default | Applied to `DataGridCustomDrawingCell`. |

## Rendering and Layout Pipeline

1. `DataGridCustomDrawingColumn` creates `DataGridCustomDrawingCell` for each realized cell.
2. Cell resolves display text from `Value` (string or `Convert.ToString(...)`).
3. Measure path:
   - If `DrawOperationLayoutFastPath=true`, `DrawingMode=DrawOperation`, and factory implements `IDataGridCellDrawOperationMeasureProvider`, `TryMeasure(...)` is used.
   - Otherwise cell measures text using `FormattedText`.
4. Arrange path:
   - If fast path is enabled and factory implements `IDataGridCellDrawOperationArrangeProvider`, `TryArrange(...)` is used.
   - Otherwise default arrange path is used.
5. Render path:
   - Text draws when mode includes text (`Text`/`TextAndDrawOperation`) or when draw operation is unavailable.
   - Draw operation draws when factory is available and mode includes draw operations (`DrawOperation`/`TextAndDrawOperation`).
   - Backend is selected by `RenderBackend`:
     - `ImmediateDrawOperation`: draw op is submitted via `context.Custom(...)`.
     - `CompositionCustomVisual`: draw op snapshot is sent to a composition custom visual handler, which owns invalidation/render.

## Direct Column Usage (XAML)

This is the same pattern used by the variable-height Skia sample page.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:customDrawing="clr-namespace:DataGridSample.CustomDrawing"
             xmlns:models="clr-namespace:DataGridSample.Models">
  <UserControl.Resources>
    <customDrawing:SkiaTextCellDrawOperationFactory x:Key="DefaultSkiaTextFactory"
                                                     Padding="4,2,4,2"
                                                     TextAlignment="Left"
                                                     VerticalAlignment="Center"
                                                     UseItemCacheContract="True"
                                                     ItemCacheSlot="0" />
  </UserControl.Resources>

  <DataGrid ItemsSource="{Binding Items}" AutoGenerateColumns="False">
    <DataGrid.Columns>
      <DataGridCustomDrawingColumn Header="Title"
                                   Binding="{Binding Title}"
                                   Width="*"
                                   DrawingMode="DrawOperation"
                                   RenderBackend="CompositionCustomVisual"
                                   DrawOperationFactory="{StaticResource DefaultSkiaTextFactory}"
                                   TextLayoutCacheMode="Shared"
                                   SharedTextLayoutCacheCapacity="4096"
                                   DrawOperationLayoutFastPath="True"
                                   Foreground="{DynamicResource ThemeForegroundBrush}"
                                   x:DataType="models:VariableHeightItem" />
    </DataGrid.Columns>
  </DataGrid>
</UserControl>
```

## Draw Operation Factory (Skia API Lease)

Implement `IDataGridCellDrawOperationFactory` to render with `ICustomDrawOperation`. For Skia, lease the current `ISkiaSharpApiLeaseFeature` in `Render(...)`.

```csharp
public sealed class SkiaTextCellDrawOperationFactory :
    IDataGridCellDrawOperationFactory,
    IDataGridCellDrawOperationMeasureProvider,
    IDataGridCellDrawOperationArrangeProvider
{
    public ICustomDrawOperation CreateDrawOperation(DataGridCellDrawOperationContext context)
    {
        return new SkiaTextCellDrawOperation(context);
    }

    public bool TryMeasure(DataGridCellDrawOperationMeasureContext context, out Size desiredSize)
    {
        // Return draw-operation text metrics (optionally cached) for fast measure.
        desiredSize = new Size(120, 20);
        return true;
    }

    public bool TryArrange(DataGridCellDrawOperationArrangeContext context, out Size arrangedSize)
    {
        // Usually pass through final size.
        arrangedSize = context.FinalSize;
        return true;
    }
}

internal sealed class SkiaTextCellDrawOperation : ICustomDrawOperation
{
    private readonly DataGridCellDrawOperationContext _context;

    public SkiaTextCellDrawOperation(DataGridCellDrawOperationContext context)
    {
        _context = context;
        Bounds = context.Bounds;
    }

    public Rect Bounds { get; }
    public void Dispose() { }
    public bool HitTest(Point p) => Bounds.Contains(p);
    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        using ISkiaSharpApiLease? lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
        if (lease is null)
        {
            return;
        }

        // Use lease.SkCanvas and draw text/glyphs/shapes here.
    }
}
```

Context objects provide cell, column, item, value, resolved text, typography, foreground, and selection/current flags. Measure/arrange contexts additionally provide `AvailableSize` / `FinalSize`, trimming, alignment, and flow direction.

## Composition Backend

`RenderBackend="CompositionCustomVisual"` routes draw-operation rendering through a composition custom visual attached to each realized `DataGridCustomDrawingCell`.

- UI thread: resolves cell state and creates draw-operation snapshots via `DrawOperationFactory`.
- Composition handler: receives snapshots, stores the latest operation, and requests redraw via composition invalidation.
- Render thread: executes the stored draw operation in the composition visual render pass.

Use composition backend when you want stronger invalidation control for high-frequency custom-draw scenarios (for example live animation/update loops) while preserving the same draw-operation factory contract.

If the composition host cannot be created for a realized cell, rendering falls back to the immediate draw-operation path for compatibility.

## Column Definitions (MVVM / DataGrid Design Pattern)

`DataGridCustomDrawingColumnDefinition` exposes the same custom drawing settings for `ColumnDefinitionsSource`.

```csharp
public DataGridColumnDefinitionList ColumnDefinitions { get; }

public MyViewModel(IDataGridCellDrawOperationFactory factory)
{
    ColumnDefinitions = new DataGridColumnDefinitionList
    {
        new DataGridCustomDrawingColumnDefinition
        {
            Header = "Description",
            Binding = DataGridBindingDefinition.Create<RowItem, string>(x => x.Description),
            DrawingMode = DataGridCustomDrawingMode.DrawOperation,
            RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual,
            DrawOperationFactory = factory,
            TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared,
            SharedTextLayoutCacheCapacity = 4096,
            DrawOperationLayoutFastPath = true
        }
    };
}
```

```xml
<DataGrid ItemsSource="{Binding Items}"
          ColumnDefinitionsSource="{Binding ColumnDefinitions}"
          AutoGenerateColumns="False" />
```

Typed builder support is also available:

```csharp
var builder = DataGridColumnDefinitionBuilder.For<RowItem>();

var definition = builder.CustomDrawing(
    header: "Description",
    property: descriptionPropertyInfo,
    getter: row => row.Description,
    setter: null,
    configure: d =>
    {
        d.DrawingMode = DataGridCustomDrawingMode.DrawOperation;
        d.RenderBackend = DataGridCustomDrawingRenderBackend.CompositionCustomVisual;
        d.DrawOperationFactory = factory;
        d.TextLayoutCacheMode = DataGridCustomDrawingTextLayoutCacheMode.Shared;
        d.SharedTextLayoutCacheCapacity = 4096;
        d.DrawOperationLayoutFastPath = true;
    });
```

## Text Layout Caching

`TextLayoutCacheMode` controls `FormattedText` reuse:

- `PerCell`: cache is local to each realized `DataGridCustomDrawingCell`.
- `Shared`: all realized cells in the same `DataGridCustomDrawingColumn` share a bounded LRU cache.

Shared cache keys include text, typography, alignment, trimming, flow direction, culture, and max text width/height constraints. This makes shared cache mode suitable for repeated text patterns across many rows.

Use larger `SharedTextLayoutCacheCapacity` when:

- the dataset is large,
- repeated texts are common,
- typography/constraints are stable.

## Per-Item Draw Metrics Cache Contract (Opt-In)

For very large item sets, factory-level LRU caches can still add contention and churn in hot paths.
`IDataGridCellDrawOperationItemCache` lets row items store cache entries directly, so factories can resolve metrics without touching shared cache lists/dictionaries.

Interface:

```csharp
public interface IDataGridCellDrawOperationItemCache
{
    bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value);
    void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value);
}
```

Factory opt-in knobs (sample `SkiaTextCellDrawOperationFactory`):

- `UseItemCacheContract`: enables item-contract cache lookup.
- `ItemCacheSlot`: slot index written/read on each item for this factory instance.

Typical item implementation (array-backed, no dictionary/list):

```csharp
public sealed class RowItem : IDataGridCellDrawOperationItemCache
{
    private SlotEntry[]? _entries;

    public bool TryGetCellDrawCacheEntry(int cacheSlot, int cacheKey, out object value)
    {
        if (_entries is not null && cacheSlot >= 0 && cacheSlot < _entries.Length)
        {
            SlotEntry entry = _entries[cacheSlot];
            if (entry.HasValue && entry.CacheKey == cacheKey && entry.Value is not null)
            {
                value = entry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public void SetCellDrawCacheEntry(int cacheSlot, int cacheKey, object value)
    {
        if (cacheSlot < 0)
        {
            return;
        }

        EnsureCapacity(cacheSlot + 1)[cacheSlot] = new SlotEntry
        {
            HasValue = true,
            CacheKey = cacheKey,
            Value = value
        };
    }
}
```

Sample page wiring (`VariableHeightSkiaCustomDrawPage.axaml`) uses separate slots per column factory (`0..4`) and enables `UseItemCacheContract="True"` on each resource.

## Fast Path Guidance

Enable `DrawOperationLayoutFastPath` only when:

- `DrawingMode` is `DrawOperation`,
- your factory implements measure (and optionally arrange) providers,
- measured metrics are consistent with rendered output.

Recommended approach for high-performance variable-height cells:

1. Cache draw-operation text metrics in the factory (LRU/bounded dictionary) or via `IDataGridCellDrawOperationItemCache` on row items.
2. Return those metrics from `TryMeasure(...)`.
3. Return `context.FinalSize` from `TryArrange(...)` unless custom arrange math is required.
4. Keep `TextLayoutCacheMode=Shared` for fallback text path and hybrid modes.

## Explicit Invalidation for Custom Draw

When draw output depends on external mutable state (animation phase, external diagnostics values, etc.) and bound row values do not change, use explicit invalidation.

### Column API

`DataGridCustomDrawingColumn` now exposes:

- `InvalidateCustomDrawingCells(bool invalidateMeasure = false, bool clearSharedTextLayoutCache = false)`
- `RenderInvalidationToken`
- `LayoutInvalidationToken`

Typical usage:

```csharp
// Render-only refresh (recommended for most animations).
myCustomDrawingColumn.InvalidateCustomDrawingCells();

// Force measure/arrange and clear shared text cache when layout-affecting state changed.
myCustomDrawingColumn.InvalidateCustomDrawingCells(
    invalidateMeasure: true,
    clearSharedTextLayoutCache: true);
```

### Factory-Driven Invalidation (Optional)

If your draw-operation factory can emit invalidation events, implement `IDataGridCellDrawOperationInvalidationSource`.
When assigned to `DrawOperationFactory`, `DataGridCustomDrawingColumn` subscribes automatically and invalidates realized cells when the factory raises `Invalidated`.

```csharp
public sealed class AnimatedFactory :
    IDataGridCellDrawOperationFactory,
    IDataGridCellDrawOperationInvalidationSource
{
    public event EventHandler<DataGridCellDrawOperationInvalidatedEventArgs>? Invalidated;

    public void Tick(float phase)
    {
        // update internal render state
        Invalidated?.Invoke(this, new DataGridCellDrawOperationInvalidatedEventArgs());
    }
}
```

## Editing Support

`DataGridCustomDrawingColumn` supports the standard DataGrid editing pipeline when `IsReadOnly` is set to `False`.

- Display mode remains custom drawing (`DataGridCustomDrawingCell`).
- Editing mode uses a `TextBox` editor bound to the same `Binding`.
- Editor theme uses `DataGridCellTextBoxTheme`.
- Font/foreground/text alignment column properties are applied to both display and editing elements.

Example:

```xml
<DataGridCustomDrawingColumn Header="Title"
                             Binding="{Binding Title}"
                             DrawingMode="DrawOperation"
                             DrawOperationFactory="{StaticResource TitleSkiaTextFactory}"
                             IsReadOnly="False"
                             Foreground="{DynamicResource ThemeForegroundBrush}"
                             x:DataType="models:VariableHeightItem" />
```

## End-to-End Sample

Sample app page:

- Tab: `Variable Height Scrolling (Skia Custom Draw)`
- Tab: `Custom Drawing Columns (Live Updates)`
- Files:
  - `src/DataGridSample/Pages/VariableHeightSkiaCustomDrawPage.axaml`
  - `src/DataGridSample/CustomDrawing/SkiaTextCellDrawOperationFactory.cs`
  - `src/DataGridSample/Pages/CustomDrawingLiveUpdatesPage.axaml`
  - `src/DataGridSample/CustomDrawing/SkiaAnimatedTextCellDrawOperationFactory.cs`

Run:

```bash
dotnet run --project src/DataGridSample/DataGridSample.csproj
```

## Related Articles

- [Column Types Reference](column-types-reference.md)
- [Column Definitions](column-definitions.md)
- [Column Definitions: Fast Path Overview](column-definitions-fast-path-overview.md)
- [Scrolling and Virtualization](scrolling-virtualization.md)
