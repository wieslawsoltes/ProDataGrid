// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Generic;
using Avalonia.Input;

namespace Avalonia.Controls.DataGridDragDrop
{
#nullable enable
    internal sealed class DataGridRowDragInfo
    {
        public const string DataFormat = "Avalonia.Controls.DataGrid.RowDragInfo";
        public static readonly DataFormat<DataGridRowDragInfo> DataTransferFormat =
            new(DataFormatKind.Application, DataFormat);

        public DataGridRowDragInfo(
            DataGrid grid,
            IReadOnlyList<object> items,
            IReadOnlyList<int> indices,
            bool fromSelection)
            : this(grid, items, indices, fromSelection, null)
        {
        }

        public DataGridRowDragInfo(
            DataGrid grid,
            IReadOnlyList<object> items,
            IReadOnlyList<int> indices,
            bool fromSelection,
            DataGridRowDragSession? session)
        {
            Grid = grid;
            Items = items;
            Indices = indices;
            FromSelection = fromSelection;
            Session = session;
        }

        public DataGrid Grid { get; }

        public IReadOnlyList<object> Items { get; }

        public IReadOnlyList<int> Indices { get; }

        public bool FromSelection { get; }

        public DataGridRowDragSession? Session { get; }
    }
#nullable restore
}
