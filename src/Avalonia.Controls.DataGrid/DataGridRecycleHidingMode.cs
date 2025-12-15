// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls
{
    /// <summary>
    /// Controls how recycled containers are hidden when removed from the viewport.
    /// </summary>
    public enum DataGridRecycleHidingMode
    {
        /// <summary>
        /// Move recycled containers far offscreen to avoid stale layout bounds (default).
        /// </summary>
        MoveOffscreen = 0,

        /// <summary>
        /// Only set IsVisible to false, leaving the last arranged bounds intact.
        /// </summary>
        SetIsVisibleOnly = 1
    }
}
