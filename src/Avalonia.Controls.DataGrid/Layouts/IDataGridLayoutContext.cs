// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Provides DataGrid realization services and per-grid state to a layout algorithm.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
interface IDataGridLayoutContext
{
    /// <summary>
    /// Gets the number of layout items, including visible group rows when grouping is active.
    /// </summary>
    int ItemCount { get; }

    /// <summary>
    /// Gets the viewport plus the configured realization cache in layout coordinates.
    /// </summary>
    Rect RealizationRect { get; }

    /// <summary>
    /// Gets the preferred anchor index, or <c>-1</c> when no anchor is available.
    /// </summary>
    int RecommendedAnchorIndex { get; }

    /// <summary>
    /// Gets or sets the estimated origin of the layout extent.
    /// </summary>
    Point LayoutOrigin { get; set; }

    /// <summary>
    /// Gets or sets per-grid state owned by the active layout algorithm.
    /// </summary>
    object? LayoutState { get; set; }

    /// <summary>
    /// Gets or creates the row container for an item index.
    /// </summary>
    /// <param name="index">The zero-based layout item index.</param>
    /// <returns>The prepared row or group container.</returns>
    Control GetOrCreateElementAt(int index);

    /// <summary>
    /// Recycles a realized container that is no longer required.
    /// </summary>
    /// <param name="element">The container to recycle.</param>
    void RecycleElement(Control element);

    /// <summary>
    /// Gets the layout item index represented by a realized container.
    /// </summary>
    /// <param name="element">The realized container.</param>
    /// <returns>The item index, or <c>-1</c> when the element is not realized by this context.</returns>
    int GetElementIndex(Control element);

    /// <summary>
    /// Gets the best known item size without forcing realization.
    /// </summary>
    /// <param name="index">The zero-based layout item index.</param>
    /// <returns>The measured size when cached; otherwise the current estimate.</returns>
    Size GetEstimatedItemSize(int index);

    /// <summary>
    /// Records layout bounds for a realized item.
    /// </summary>
    /// <param name="index">The zero-based layout item index.</param>
    /// <param name="bounds">The item bounds in layout coordinates.</param>
    void SetLayoutBounds(int index, Rect bounds);

    /// <summary>
    /// Attempts to get previously recorded layout bounds for an item.
    /// </summary>
    /// <param name="index">The zero-based layout item index.</param>
    /// <param name="bounds">Receives the item bounds when available.</param>
    /// <returns><c>true</c> when bounds were found.</returns>
    bool TryGetLayoutBounds(int index, out Rect bounds);
}
