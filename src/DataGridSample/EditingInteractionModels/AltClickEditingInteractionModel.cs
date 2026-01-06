using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Input;

namespace DataGridSample.EditingInteractionModels
{
    public sealed class AltClickEditingInteractionModel : DataGridEditingInteractionModel
    {
        public override bool ShouldBeginEditOnPointer(DataGridPointerEditContext context)
        {
            if (context.IsDoubleClick)
            {
                if (!context.EditTriggers.HasFlag(DataGridEditTriggers.CellDoubleClick) &&
                    !context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick))
                {
                    return false;
                }
            }
            else if (!context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick))
            {
                return false;
            }

            return context.Modifiers.HasFlag(KeyModifiers.Alt);
        }
    }
}
