// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Defines the direction in which a DataGrid layout places consecutive items.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridLayoutOrientation
{
    /// <summary>
    /// Items advance from top to bottom.
    /// </summary>
    Vertical,

    /// <summary>
    /// Items advance from left to right.
    /// </summary>
    Horizontal
}
