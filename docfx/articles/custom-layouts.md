# Custom layouts

Applications can add layout types without deriving from DataGrid or replacing its presenter. A custom layout consists of a model and an algorithm:

- `IDataGridLayoutModel` owns serializable/bindable configuration and creates an algorithm;
- `IDataGridLayoutAlgorithm` measures, records bounds, arranges, handles item changes, and releases session resources;
- `IDataGridLayoutNavigation` is optional and adds geometry-aware navigation;
- `IDataGridLayoutContext` is the only container/estimator service the algorithm should use.

`DataGridLayoutModelBase` implements invalidation and allocation-free equality checks for property setters. Prefer it unless the model already has another base type.

## Minimal model

```csharp
public sealed class IndentedStackLayoutModel : DataGridLayoutModelBase
{
    private double _indent = 32;

    public double Indent
    {
        get => _indent;
        set => SetProperty(ref _indent, value);
    }

    public override IDataGridLayoutAlgorithm CreateAlgorithm() =>
        new IndentedStackLayoutAlgorithm(this);
}
```

`SetProperty` raises `LayoutInvalidated`. Use the overload with an explicit invalidation kind:

- `Arrange` when recorded sizes/positions remain valid and only final placement changes;
- `Measure` for size or spacing changes;
- `Reset` when cached state no longer describes the same coordinate system.

## Algorithm lifecycle

```csharp
public sealed class IndentedStackLayoutAlgorithm : IDataGridLayoutAlgorithm
{
    private readonly IndentedStackLayoutModel _model;

    public IndentedStackLayoutAlgorithm(IndentedStackLayoutModel model) =>
        _model = model;

    public void Initialize(IDataGridLayoutContext context)
    {
        context.LayoutState ??= new State();
    }

    public Size Measure(IDataGridLayoutContext context, Size availableSize)
    {
        // Determine an anchor, request intersecting elements, measure and record
        // their bounds, recycle outside the retained range, and return an extent.
        throw new NotImplementedException();
    }

    public Size Arrange(IDataGridLayoutContext context, Size finalSize)
    {
        foreach (Control element in context.RealizedElements)
        {
            int index = context.GetElementIndex(element);
            if (index >= 0 && context.TryGetLayoutBounds(index, out Rect bounds))
            {
                Vector offset = context.ScrollOffset;
                element.Arrange(new Rect(
                    bounds.X - offset.X,
                    bounds.Y - offset.Y,
                    bounds.Width,
                    bounds.Height));
            }
        }
        return finalSize;
    }

    public void OnItemsChanged(
        IDataGridLayoutContext context,
        NotifyCollectionChangedEventArgs change)
    {
        context.LayoutState = new State();
    }

    public void Uninitialize(IDataGridLayoutContext context)
    {
        // Dispose only resources owned by this session.
    }

    private sealed class State { }
}
```

The presenter calls `Initialize` once per activation of a retained session and `Uninitialize` when switching away or detaching. `OnItemsChanged` can process collection deltas, but resetting compact state is often cheaper and safer than retaining an item-count-sized index.

## Context rules

`GetOrCreateElementAt` prepares a normal DataGrid row/group container. The algorithm must never construct, reparent, cache, or dispose that control itself. `RecycleElement` marks a realized element as unnecessary; DataGrid performs the actual lifecycle work.

`GetEstimatedItemSize` and `GetEstimatedItemOffset` must be preferred over realizing off-screen items. The vertical stack estimate is backed by the DataGrid row-height index and supports fixed and variable rows.

Only bounds recorded with `SetLayoutBounds` are arranged. Bounds use layout-content coordinates. The built-in algorithms subtract `ScrollOffset` exactly once during arrange.

The context expects a contiguous realized index interval because the current DataGrid display store is range-based. A custom algorithm may leave visual gaps inside that interval, but it should not request disconnected index islands in one pass.

## Optional spatial navigation

Implement `IDataGridLayoutNavigation` on the algorithm when geometric order differs from ordinary row order.

```csharp
public bool SupportsNavigation(DataGridLayoutNavigationDirection direction) =>
    direction is DataGridLayoutNavigationDirection.Up or
        DataGridLayoutNavigationDirection.Down or
        DataGridLayoutNavigationDirection.PageUp or
        DataGridLayoutNavigationDirection.PageDown;

public bool TryGetNavigationBounds(
    IDataGridLayoutContext context,
    int itemIndex,
    Rect viewport,
    out Rect bounds)
{
    // Return exact cached bounds or a side-effect-free estimate.
}

public bool TryResolveNavigation(
    IDataGridLayoutContext context,
    in DataGridLayoutNavigationRequest request,
    out DataGridLayoutNavigationResult result)
{
    // Return false for a supported direction only at a true boundary.
}
```

Navigation queries must not invalidate layout, mutate persistent anchor state, or force realization. `Viewport`, `NavigationAnchor`, returned bounds, and bounds from the context all share layout-content coordinates. Preserve the anchor's cross-axis component when selecting among candidates on another line.

Do not claim directions that should retain standard DataGrid cell behavior. For example, a vertical custom stack normally owns `Up`, `Down`, page, first, and last, but not `Left` or `Right`.

## State and memory design

Good session state is bounded independently of `ItemCount`:

- one running average for item/line dimensions;
- a fixed-size ring buffer of nearby lines;
- a Fenwick/index structure already supplied by DataGrid for vertical row heights;
- a few anchor and extent scalars.

Avoid `Rect[itemCount]`, one dictionary entry per source item, or retaining row controls. If exact random access truly needs per-item data, expose a documented memory option on the model and test both limits.

## Testing a custom layout

Cover at least:

1. empty and one-item sequences;
2. exact viewport edges and spacing;
3. far random jumps without linear realization;
4. fixed and variable item sizes;
5. horizontal and vertical orientation when supported;
6. insert/remove/move/reset notifications;
7. invalidation kind for every model property;
8. supported, unsupported, boundary, page, and non-realized navigation;
9. attach/detach and externally retained model leak behavior;
10. runtime switching back to the same model instance.

The complete custom implementation used by the sample is `DataGridSample/Layouts/IndentedStackLayoutModel.cs`.
