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
    /// Defines how a <see cref="DataGridCustomDrawingCell"/> renders its content.
    /// </summary>
    enum DataGridCustomDrawingMode
    {
        /// <summary>
        /// Renders using the built-in text path only.
        /// </summary>
        Text = 0,

        /// <summary>
        /// Renders using only a custom draw operation provided by <see cref="IDataGridCellDrawOperationFactory"/>.
        /// </summary>
        DrawOperation = 1,

        /// <summary>
        /// Renders text first and then executes the custom draw operation.
        /// </summary>
        TextAndDrawOperation = 2
    }
}
