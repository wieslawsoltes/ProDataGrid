# Drag and Drop

ProDataGrid provides opt-in row drag and drop with built-in visuals, auto-scroll, and a first-class live drag-session API. You can target flat or hierarchical lists with pluggable handlers and keep drag feedback synchronized with modifier changes and target validation.

## Row Drag and Drop Quick Start

- Turn on `CanUserReorderRows="True"` and choose `RowDragHandle` (`RowHeader`, `Row`, or `RowHeaderAndRow`).
- Toggle `RowDragHandleVisible` to show or hide the grip.
- Configure `RowDragDropOptions` for allowed effects, thresholds, multi-row drag behavior, and selection-drag coordination.
- Use a `RowDropHandler` when you need custom validation or copy/move logic.
- Bind to `ActiveRowDragSession` or subscribe to `RowDragStarted` / `RowDragUpdated` / `RowDragCanceled` / `RowDragCompleted` when you need live state.

```xml
<DataGrid ItemsSource="{Binding Items}"
          CanUserReorderRows="True"
          RowDragHandle="RowHeaderAndRow"
          RowDragHandleVisible="{Binding ShowHandle}"
          RowDragDropOptions="{Binding Options}"
          RowDropHandler="{Binding DropHandler}"
          RowDragFeedbackOffset="18,20">
  <DataGrid.RowDragFeedbackTemplate>
    <DataTemplate x:DataType="DataGridDragDrop:DataGridRowDragSession">
      <Border Padding="10,8">
        <StackPanel>
          <TextBlock Text="{Binding EffectiveEffect}" />
          <TextBlock Text="{Binding FeedbackCaption}" />
        </StackPanel>
      </Border>
    </DataTemplate>
  </DataGrid.RowDragFeedbackTemplate>
</DataGrid>
```

```csharp
using Avalonia.Controls.DataGridDragDrop;
using Avalonia.Input;

public DataGridRowDragDropOptions Options { get; } = new()
{
    AllowedEffects = DragDropEffects.Move | DragDropEffects.Copy,
    DragSelectedRows = true,
    SuppressSelectionDragFromDragHandle = true
};

public IDataGridRowDropHandler DropHandler { get; } = new CustomRowDropHandler();
```

## Session API

`DataGridRowDragSession` represents the live state of one active drag operation.

Key properties include:

- `Items`, `SourceIndices`, `FromSelection`
- `PointerPosition`, `KeyModifiers`
- `HoveredRow`, `HoveredItem`, `TargetRow`, `TargetItem`
- `DropPosition`, `InsertIndex`
- `RequestedEffect`, `EffectiveEffect`
- `IsValidTarget`, `IsCanceled`, `ResultEffect`
- `FeedbackCaption`

`ActiveRowDragSession` is a read-only direct property on `DataGrid`. It is `null` when no drag is active.

## Lifecycle Events

Row drag/drop now exposes the full lifecycle:

- `RowDragStarting`
- `RowDragStarted`
- `RowDragUpdated`
- `RowDragCanceled`
- `RowDragCompleted`

`RowDragCompleted` now means a committed drop. Canceled or aborted drags raise only `RowDragCanceled`.

## Live Operation Recalculation

`IDataGridRowDropHandler.Validate` is called continuously during drag-over. The handler receives a `DataGridRowDropEventArgs` instance with:

- `RequestedEffect`: the modifier-derived effect before application validation
- `EffectiveEffect`: the writable application-approved effect
- `Session`: the active `DataGridRowDragSession`
- `PointerPosition`, `KeyModifiers`, `HoveredRow`, `HoveredItem`

Typical pattern:

```csharp
public bool Validate(DataGridRowDropEventArgs args)
{
    var valid = args.RequestedEffect == DragDropEffects.Copy
        ? CanCopyHere(args)
        : _reorder.Validate(args);

    if (!valid)
    {
        args.EffectiveEffect = DragDropEffects.None;
        if (args.Session != null)
        {
            args.Session.FeedbackCaption = "Drop here is not allowed.";
        }
        return false;
    }

    args.EffectiveEffect = args.RequestedEffect == DragDropEffects.Copy
        ? DragDropEffects.Copy
        : DragDropEffects.Move;

    if (args.Session != null)
    {
        args.Session.FeedbackCaption = args.EffectiveEffect == DragDropEffects.Copy
            ? "Copy rows here."
            : "Move rows here.";
    }

    return true;
}
```

This allows transitions such as `Move -> Copy -> None -> Move` during the same drag.

Built-in `DataGridRowReorderHandler` and `DataGridHierarchicalRowReorderHandler` remain move-only. If you want `Copy` semantics, provide a custom `IDataGridRowDropHandler` that validates copy targets and performs the copy operation.

## Selection-Drag Coordination

`DataGridRowDragDropOptions.SuppressSelectionDragFromDragHandle` controls whether the row-drag gesture wins over range-selection drag when the press starts on a configured row drag surface.

- `true` keeps click-to-select but prevents range-selection drag from taking over the intended row drag gesture.
- `false` restores the older behavior where selection drag can begin from the same surface.

## Drop Visuals and Feedback

Drag/drop exposes pseudo-classes you can style:

- `DataGrid:row-drag-enabled`, `DataGrid:row-drag-handle-visible`
- `DataGridRow:dragging`
- `DataGridRow:drag-over-before`, `DataGridRow:drag-over-after`, `DataGridRow:drag-over-inside`

Feedback layers:

- native cursor feedback from `DragEventArgs.DragEffects`
- built-in row/drop indicators
- optional `RowDragFeedbackTemplate` bound to the active session

The drop indicator uses `DropLocationIndicatorTemplate` and theme resources like `DataGridDropLocationIndicatorBrush` and `DataGridDropLocationIndicatorWidth` (see `Themes/Generic.xaml`).

## Advanced Customization

- Use `RowDragDropControllerFactory` to supply a custom controller when you need custom drag data or hit-testing.
- Implement `IDataGridRowDropHandler` to validate or reroute drops while keeping the built-in visuals and auto-scroll.
- See [Row Drag Sessions](row-drag-sessions.md) for the full session contract and the issue-299 design notes.

## Hierarchical Drag and Drop

Use `DataGridHierarchicalRowReorderHandler` to support before/after/inside drop targets in tree-like data sets.

```xml
<DataGrid CanUserReorderRows="True"
          HierarchicalRowsEnabled="True"
          RowDragHandle="RowHeaderAndRow"
          RowDragDropOptions="{Binding Options}"
          RowDropHandler="{Binding DropHandler}"
          HierarchicalModel="{Binding Model}" />
```

The top and bottom thirds of a hierarchical row resolve to `Before` / `After`; the middle resolves to `Inside`.
