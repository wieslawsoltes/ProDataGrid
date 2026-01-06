// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using Avalonia.Input;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    sealed class DataGridKeyboardGestures
    {
        public KeyGesture Tab { get; set; }

        public KeyGesture MoveUp { get; set; }

        public KeyGesture MoveDown { get; set; }

        public KeyGesture MoveLeft { get; set; }

        public KeyGesture MoveRight { get; set; }

        public KeyGesture MovePageUp { get; set; }

        public KeyGesture MovePageDown { get; set; }

        public KeyGesture MoveHome { get; set; }

        public KeyGesture MoveEnd { get; set; }

        public KeyGesture Enter { get; set; }

        public KeyGesture CancelEdit { get; set; }

        public KeyGesture BeginEdit { get; set; }

        public KeyGesture SelectAll { get; set; }

        public KeyGesture Copy { get; set; }

        public KeyGesture CopyAlternate { get; set; }

        public KeyGesture Paste { get; set; }

        public KeyGesture PasteAlternate { get; set; }

        public KeyGesture Delete { get; set; }

        public KeyGesture ExpandAll { get; set; }

        public static DataGridKeyboardGestures CreateDefault(KeyModifiers commandModifiers)
        {
            return new DataGridKeyboardGestures
            {
                Tab = new KeyGesture(Key.Tab),
                MoveUp = new KeyGesture(Key.Up),
                MoveDown = new KeyGesture(Key.Down),
                MoveLeft = new KeyGesture(Key.Left),
                MoveRight = new KeyGesture(Key.Right),
                MovePageUp = new KeyGesture(Key.PageUp),
                MovePageDown = new KeyGesture(Key.PageDown),
                MoveHome = new KeyGesture(Key.Home),
                MoveEnd = new KeyGesture(Key.End),
                Enter = new KeyGesture(Key.Enter),
                CancelEdit = new KeyGesture(Key.Escape),
                BeginEdit = new KeyGesture(Key.F2),
                SelectAll = new KeyGesture(Key.A, commandModifiers),
                Copy = new KeyGesture(Key.C, commandModifiers),
                CopyAlternate = new KeyGesture(Key.Insert, commandModifiers),
                Paste = new KeyGesture(Key.V, commandModifiers),
                PasteAlternate = new KeyGesture(Key.Insert, KeyModifiers.Shift),
                Delete = new KeyGesture(Key.Delete),
                ExpandAll = new KeyGesture(Key.Multiply)
            };
        }
    }
}
