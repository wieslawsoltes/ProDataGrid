// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.Specialized;

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Computes row realization, measurement, and arrangement for a DataGrid layout model.
/// </summary>
/// <remarks>
/// Implementations request and recycle row containers through <see cref="IDataGridLayoutContext"/>.
/// The DataGrid remains responsible for container creation, preparation, selection, editing, and recycling.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
interface IDataGridLayoutAlgorithm
{
    /// <summary>
    /// Initializes the algorithm for a layout context.
    /// </summary>
    /// <param name="context">The owning grid layout context.</param>
    void Initialize(IDataGridLayoutContext context);

    /// <summary>
    /// Measures the layout and realizes the rows needed by the realization window.
    /// </summary>
    /// <param name="context">The owning grid layout context.</param>
    /// <param name="availableSize">The available layout size.</param>
    /// <returns>The estimated extent required by the layout.</returns>
    Size Measure(IDataGridLayoutContext context, Size availableSize);

    /// <summary>
    /// Arranges realized rows using bounds recorded in the layout context.
    /// </summary>
    /// <param name="context">The owning grid layout context.</param>
    /// <param name="finalSize">The final viewport size.</param>
    /// <returns>The arranged size.</returns>
    Size Arrange(IDataGridLayoutContext context, Size finalSize);

    /// <summary>
    /// Notifies the algorithm that the items collection changed.
    /// </summary>
    /// <param name="context">The owning grid layout context.</param>
    /// <param name="change">The collection change.</param>
    void OnItemsChanged(IDataGridLayoutContext context, NotifyCollectionChangedEventArgs change);

    /// <summary>
    /// Releases context-specific resources before the algorithm is detached.
    /// </summary>
    /// <param name="context">The owning grid layout context.</param>
    void Uninitialize(IDataGridLayoutContext context);
}
