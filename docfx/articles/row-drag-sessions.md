# Row Drag Sessions

Issue [#299](https://github.com/wieslawsoltes/ProDataGrid/issues/299) requested a proper row drag session model with live operation updates, modifier handling, custom feedback support, and better coordination with selection drag.

This article documents the resulting API and the deliberate divergences from the original proposal.

## What Was Added

Public API additions:

- `DataGridRowDragSession`
- `DataGrid.ActiveRowDragSession`
- `DataGrid.RowDragFeedbackTemplate`
- `DataGrid.RowDragFeedbackOffset`
- `DataGrid.RowDragStarted`
- `DataGrid.RowDragUpdated`
- `DataGrid.RowDragCanceled`

Existing APIs were enriched:

- `DataGridRowDragStartingEventArgs.Session`
- `DataGridRowDragCompletedEventArgs.Session`
- `DataGridRowDropEventArgs.Session`
- `DataGridRowDropEventArgs.PointerPosition`
- `DataGridRowDropEventArgs.KeyModifiers`
- `DataGridRowDropEventArgs.HoveredRow`
- `DataGridRowDropEventArgs.HoveredItem`
- `DataGridRowDragDropOptions.SuppressSelectionDragFromDragHandle`

Existing drop handlers now use a corrected effect split:

- `DataGridRowDropEventArgs.RequestedEffect`: modifier-derived request
- `DataGridRowDropEventArgs.EffectiveEffect`: writable approved effect

## Session Semantics

`DataGridRowDragSession` is the single source of truth for the active drag.

Use it for:

- source items and indices
- live pointer and modifier state
- hovered row vs actionable target row
- current drop zone and insert index
- modifier-requested effect vs application-approved effect
- custom feedback text through `FeedbackCaption`

Important distinction:

- `RequestedEffect` is the modifier-derived request for the current moment.
- `EffectiveEffect` is the effect that the application has approved after validation.

That distinction is what allows the control to support real-time transitions such as `Copy -> None -> Move` in one drag.

## Terminal Lifecycle

The terminal events are now disjoint:

- `RowDragCompleted` means a drop committed.
- `RowDragCanceled` means the drag ended without a committed drop.

## Update Flow

During `DragOver`, the controller:

1. Resolves the shared session from the drag payload.
2. Updates pointer, modifier, hovered row, target row, and drop zone state.
3. Computes the modifier-derived `RequestedEffect`.
4. Initializes the session `EffectiveEffect`.
5. Calls `IDataGridRowDropHandler.Validate`.
6. Lets the handler mutate `EffectiveEffect`.
7. Finalizes `IsValidTarget` and the coerced effective effect.
8. Raises `RowDragUpdated`.
9. Applies the final `EffectiveEffect` to native feedback and in-grid visuals.

## Handler Pattern

Use `Validate` for continuous recomputation and `Execute` for the committed drop:

```csharp
private sealed class MyDropHandler : IDataGridRowDropHandler
{
    private readonly DataGridRowReorderHandler _reorder = new();

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
            args.Session.FeedbackCaption = $"{args.EffectiveEffect} {args.Items.Count} row(s).";
        }

        return true;
    }

    public bool Execute(DataGridRowDropEventArgs args)
    {
        return _reorder.Execute(args);
    }
}
```

Built-in reorder handlers stay move-only. Copy scenarios are expected to use a custom handler that approves `EffectiveEffect = Copy` and performs the copy in `Execute`.

## Feedback Template

`RowDragFeedbackTemplate` renders inside the grid adorner layer and binds to the active session.

Use it to show:

- the current effective operation
- invalid-target explanations
- target-specific captions
- item counts or domain-specific status

Example:

```xml
<DataGrid RowDragFeedbackOffset="18,20">
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

## Selection Coordination

`SuppressSelectionDragFromDragHandle` exists to solve the gesture conflict raised in the issue:

- row drag should start from the configured drag surface
- press-to-select should still work
- selection range-drag should not take over the same gesture when drag intent is clear

This is especially important when `RowDragHandle="Row"` or `RowDragHandle="RowHeaderAndRow"`.

## Divergences From Issue 299

There is one intentional divergence from the original proposal.

No separate native source-side drag badge API was added.
Reason: Avalonia exposes effect feedback through `DragEffects`, but not a public source-side custom drag-UI pipeline comparable to WPF `GiveFeedback`. The grid therefore adds a cross-platform templated in-grid feedback layer.

## Samples

The sample app contains two pages that exercise the full issue-299 scope:

- `Row Drag & Drop`: move/copy transitions, invalid pinned-lane targets, custom feedback badge, and the new selection-drag option.
- `Hierarchical Row Drag & Drop`: before/after/inside targeting, invalid inside drops for leaf-only nodes, and live session inspection.
