// Copyright (c) Wieslaw Soltes. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

#nullable disable

using Avalonia;
using Avalonia.Rendering;

namespace Avalonia.Controls
{
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
    class DataGridSelectionOverlay : Canvas, ICustomHitTest
    {
        internal Control FillHandle { get; set; }

        public bool HitTest(Point point)
        {
            var handle = FillHandle;
            if (handle == null || !handle.IsVisible)
            {
                return false;
            }

            // Only hit test when the pointer is over the fill handle so the overlay
            // doesn't block cell interactions.
            return handle.Bounds.Contains(point);
        }
    }
}
