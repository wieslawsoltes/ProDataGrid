// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Selects the visual container used for items arranged by a DataGrid layout model.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridLayoutPresentationMode
{
    /// <summary>
    /// Realizes conventional <see cref="DataGridRow"/> and <see cref="DataGridCell"/> containers.
    /// </summary>
    Rows,

    /// <summary>
    /// Realizes lightweight <see cref="DataGridItemContainer"/> containers whose content is built
    /// from <see cref="DataGrid.ItemTemplate"/>.
    /// </summary>
    Items
}
