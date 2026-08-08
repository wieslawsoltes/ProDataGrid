// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls
{
    /// <summary>
    /// Identifies the active content projection for a generated DataGrid view.
    /// </summary>
#if !DATAGRID_INTERNAL
    public
#else
    internal
#endif
    enum DataGridGeneratedViewState
    {
        /// <summary>The generated DataGrid is visible.</summary>
        Content = 0,

        /// <summary>A loading projection is visible.</summary>
        Loading = 1,

        /// <summary>An empty-result projection is visible.</summary>
        Empty = 2,

        /// <summary>An error projection is visible.</summary>
        Error = 3
    }
}
