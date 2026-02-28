// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridCustomDrawingRenderBackend
    {
        /// <summary>
        /// Uses the immediate custom draw operation path (`DrawingContext.Custom(...)`).
        /// </summary>
        ImmediateDrawOperation = 0,

        /// <summary>
        /// Uses a composition custom visual host for draw-operation rendering.
        /// </summary>
        CompositionCustomVisual = 1
    }
}
