// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

#nullable disable

using System;
using Avalonia.Controls.Primitives;

namespace Avalonia.Controls
{
    /// <summary>
    /// Event args for DataGrid context menu requests.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    sealed class DataGridContextMenuEventArgs : EventArgs
    {
        private bool _handled;

        public DataGridContextMenuEventArgs(object item, DataGridColumn column, object originalSource)
        {
            Item = item;
            Column = column;
            OriginalSource = originalSource;
        }

        /// <summary>
        /// Gets the item associated with the hit cell, if any.
        /// </summary>
        public object Item { get; }

        /// <summary>
        /// Gets the column that was hit, if any.
        /// </summary>
        public DataGridColumn Column { get; }

        /// <summary>
        /// Set a flyout to display as the context menu. If set, the grid will show this and mark the event handled.
        /// </summary>
        public FlyoutBase Flyout
        {
            get => _flyout;
            set
            {
                _flyout = value;
                _handled = value != null;
            }
        }
        private FlyoutBase _flyout;

        /// <summary>
        /// Gets or sets a value indicating whether the event was handled.
        /// </summary>
        public bool Handled
        {
            get => _handled;
            set => _handled = value;
        }

        /// <summary>
        /// The original source control/object that triggered the context menu request, if available.
        /// </summary>
        public object OriginalSource { get; }
    }
}
