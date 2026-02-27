// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    /// <summary>
    /// Defines how <see cref="DataGridCustomDrawingCell"/> caches text layout objects.
    /// </summary>
    enum DataGridCustomDrawingTextLayoutCacheMode
    {
        /// <summary>
        /// Cache text layout per realized cell instance.
        /// </summary>
        PerCell = 0,

        /// <summary>
        /// Use a shared bounded cache across realized cells in the same column.
        /// </summary>
        Shared = 1
    }
}
