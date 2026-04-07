// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Input;

namespace Avalonia.Controls.DataGridDragDrop
{
    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum DataGridRowDragHandle
    {
        RowHeader,
        Row,
        RowHeaderAndRow
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    enum DataGridRowDropPosition
    {
        Before,
        After,
        Inside
    }

    #if !DATAGRID_INTERNAL
    public
    #else
    internal
    #endif
    class DataGridRowDragDropOptions
    {
        public DragDropEffects AllowedEffects { get; set; } = DragDropEffects.Move;

        public double HorizontalDragThreshold { get; set; } = 4;

        public double VerticalDragThreshold { get; set; } = 4;

        public bool DragSelectedRows { get; set; } = true;

        public bool SuppressSelectionDragFromDragHandle { get; set; } = true;
    }
}
