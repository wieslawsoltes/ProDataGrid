// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls;

/// <summary>
/// Selects how realized rows and cells are attached to the grid visual tree.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridVisualLayoutMode
{
    /// <summary>
    /// Uses the compatibility layout where each row owns a nested cells presenter.
    /// </summary>
    Nested,

    /// <summary>
    /// Attaches realized rows and cells as siblings under one rows presenter and computes
    /// their pixel bounds in a single layout pass.
    /// </summary>
    Flat,

    /// <summary>
    /// Draws realized display cells on a single virtual surface instead of creating a
    /// retained <see cref="DataGridCell"/> control for every visible row and column.
    /// Interactive editors and unsupported control templates are materialized on demand.
    /// </summary>
    Virtualized
}
