# Editing Interaction Model

The editing interaction model controls how pointer and text input begin edits, and how initial text is applied to editors. It complements `EditTriggers` by allowing custom behavior and focus rules.

## Quick Start

Use the default model (already assigned) or supply your own:

```csharp
dataGrid.EditingInteractionModel = new DataGridEditingInteractionModel();
```

## Custom model

Example: require Alt+Click for single-click editing while keeping double-click edits:

```csharp
using Avalonia.Controls.DataGridEditing;
using Avalonia.Input;

public sealed class AltClickEditingInteractionModel : DataGridEditingInteractionModel
{
    public override bool ShouldBeginEditOnPointer(DataGridPointerEditContext context)
    {
        if (context.IsDoubleClick)
        {
            return context.EditTriggers.HasFlag(DataGridEditTriggers.CellDoubleClick) ||
                   context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick);
        }

        if (!context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick))
            return false;

        return context.Modifiers.HasFlag(KeyModifiers.Alt);
    }
}
```

Assign the model:

```csharp
dataGrid.EditingInteractionModel = new AltClickEditingInteractionModel();
```

## Factories and overrides

- Use `EditingInteractionModelFactory` to create instances per grid.
- Override `CreateEditingInteractionModel()` for grid subclasses.

## Samples

See the sample gallery for the editing interaction model page.
