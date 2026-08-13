// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Optionally describes how a layout model presents data items and estimates unrealized item sizes.
/// </summary>
/// <remarks>
/// Custom layout models implement this focused capability to opt into item-template presentation.
/// Models that do not implement it retain conventional row and cell presentation.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
interface IDataGridLayoutPresentationModel
{
    /// <summary>
    /// Gets the visual presentation used for data items.
    /// </summary>
    DataGridLayoutPresentationMode PresentationMode { get; }

    /// <summary>
    /// Gets the fallback size used for an item that has not been measured yet.
    /// </summary>
    Size ItemSizeEstimate { get; }
}
