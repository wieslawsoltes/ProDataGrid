// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Avalonia.Controls.DataGridEditing
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridPointerEditContext
    {
        public DataGridPointerEditContext(DataGrid grid, bool isDoubleClick, DataGridEditTriggers editTriggers, KeyModifiers modifiers)
        {
            Grid = grid;
            IsDoubleClick = isDoubleClick;
            EditTriggers = editTriggers;
            Modifiers = modifiers;
        }

        public DataGrid Grid { get; }

        public bool IsDoubleClick { get; }

        public DataGridEditTriggers EditTriggers { get; }

        public KeyModifiers Modifiers { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridTextInputContext
    {
        public DataGridTextInputContext(DataGrid grid, object source, bool restrictToCells)
        {
            Grid = grid;
            Source = source;
            RestrictToCells = restrictToCells;
        }

        public DataGrid Grid { get; }

        public object Source { get; }

        public bool RestrictToCells { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridTextInputEditContext
    {
        public DataGridTextInputEditContext(
            DataGrid grid,
            string text,
            bool isEditing,
            bool isReadOnly,
            bool canEditCurrentCell,
            DataGridEditTriggers editTriggers,
            KeyModifiers modifiers)
        {
            Grid = grid;
            Text = text;
            IsEditing = isEditing;
            IsReadOnly = isReadOnly;
            CanEditCurrentCell = canEditCurrentCell;
            EditTriggers = editTriggers;
            Modifiers = modifiers;
        }

        public DataGrid Grid { get; }

        public string Text { get; }

        public bool IsEditing { get; }

        public bool IsReadOnly { get; }

        public bool CanEditCurrentCell { get; }

        public DataGridEditTriggers EditTriggers { get; }

        public KeyModifiers Modifiers { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    readonly struct DataGridTextInputApplyContext
    {
        public DataGridTextInputApplyContext(Control editingElement, string text)
        {
            EditingElement = editingElement;
            Text = text;
        }

        public Control EditingElement { get; }

        public string Text { get; }
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridEditingInteractionModel
    {
        bool IsTextInputFromGrid(DataGridTextInputContext context);

        bool ShouldBeginEditOnPointer(DataGridPointerEditContext context);

        string GetTextInputForEdit(DataGridTextInputEditContext context);

        bool TryApplyTextInput(DataGridTextInputApplyContext context);
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    interface IDataGridEditingInteractionModelFactory
    {
        IDataGridEditingInteractionModel Create();
    }

#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    class DataGridEditingInteractionModel : IDataGridEditingInteractionModel
    {
        public virtual bool IsTextInputFromGrid(DataGridTextInputContext context)
        {
            if (ReferenceEquals(context.Source, context.Grid))
            {
                return true;
            }

            if (context.Source is Visual visual)
            {
                if (context.RestrictToCells)
                {
                    var cell = visual.GetSelfAndVisualAncestors()
                        .OfType<DataGridCell>()
                        .FirstOrDefault();
                    return cell?.OwningGrid == context.Grid;
                }

                var grid = visual.GetSelfAndVisualAncestors()
                    .OfType<DataGrid>()
                    .FirstOrDefault();
                return grid == context.Grid;
            }

            return false;
        }

        public virtual bool ShouldBeginEditOnPointer(DataGridPointerEditContext context)
        {
            if (context.EditTriggers == DataGridEditTriggers.None)
            {
                return false;
            }

            if (context.IsDoubleClick)
            {
                return context.EditTriggers.HasFlag(DataGridEditTriggers.CellDoubleClick) ||
                       context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick);
            }

            return context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick);
        }

        public virtual string GetTextInputForEdit(DataGridTextInputEditContext context)
        {
            if (context.IsEditing || context.IsReadOnly)
            {
                return null;
            }

            if (!context.EditTriggers.HasFlag(DataGridEditTriggers.TextInput))
            {
                return null;
            }

            if (string.IsNullOrEmpty(context.Text))
            {
                return null;
            }

            if (!context.CanEditCurrentCell)
            {
                return null;
            }

            return context.Text;
        }

        public virtual bool TryApplyTextInput(DataGridTextInputApplyContext context)
        {
            if (context.EditingElement is TextBox textBox)
            {
                ApplyText(textBox, context.Text);
                return true;
            }

            var nestedTextBox = context.EditingElement
                .GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault();
            if (nestedTextBox != null)
            {
                ApplyText(nestedTextBox, context.Text);
                return true;
            }

            return false;
        }

        private static void ApplyText(TextBox textBox, string text)
        {
            textBox.Text = text;
            textBox.CaretIndex = text.Length;
            textBox.SelectionStart = text.Length;
            textBox.SelectionEnd = text.Length;
        }
    }
}
