// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Defines the surface used to initiate column reordering.
    /// </summary>
    enum DataGridColumnDragHandle
    {
        /// <summary>
        /// Allows column reordering from anywhere on the column header.
        /// </summary>
        ColumnHeader,
        /// <summary>
        /// Allows column reordering only from the drag handle grip.
        /// </summary>
        DragHandle
    }
}
