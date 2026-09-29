// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable enable

using System.Collections.Generic;

namespace ProCharts
{
    /// <summary>Renderer-independent navigation contract for sources that display typed hierarchy snapshots.</summary>
    /// <remarks>UI callers should invoke navigation on their UI thread. Sources publish ordinary data invalidations.</remarks>
    public interface IChartHierarchyNavigator
    {
        /// <summary>Gets the root currently represented by the source.</summary>
        ChartHierarchyNode CurrentRoot { get; }
        /// <summary>Gets whether a previous root can be restored.</summary>
        bool CanDrillUp { get; }
        /// <summary>Enters a non-root branch using its stable identifier within the current subtree.</summary>
        bool TryDrillDown(string nodeId);
        /// <summary>Restores the previous navigation root.</summary>
        bool TryDrillUp();
        /// <summary>Restores the original root and clears navigation history.</summary>
        bool ResetNavigation();
        /// <summary>Returns an owned root-to-current breadcrumb path.</summary>
        IReadOnlyList<ChartHierarchyNode> GetPath();
    }
}
