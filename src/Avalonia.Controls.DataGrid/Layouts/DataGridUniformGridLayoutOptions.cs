// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Defines how uniform-grid items are aligned on the non-scrolling axis.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridUniformGridItemsJustification
{
    /// <summary>Places unused space after the items.</summary>
    Start,
    /// <summary>Splits unused space equally before and after the items.</summary>
    Center,
    /// <summary>Places unused space before the items.</summary>
    End,
    /// <summary>Distributes unused space evenly around each item.</summary>
    SpaceAround,
    /// <summary>Distributes unused space between adjacent items.</summary>
    SpaceBetween,
    /// <summary>Distributes unused space before, between, and after items.</summary>
    SpaceEvenly
}

/// <summary>
/// Defines how uniform-grid items consume unused space on the non-scrolling axis.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridUniformGridItemsStretch
{
    /// <summary>Retains the configured or measured item size.</summary>
    None,
    /// <summary>Stretches items on the non-scrolling axis.</summary>
    Fill,
    /// <summary>Stretches items while retaining their aspect ratio.</summary>
    Uniform
}
