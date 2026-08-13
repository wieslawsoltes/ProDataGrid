// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.ComponentModel;

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Describes a model-driven row layout and creates the algorithm that executes it.
/// </summary>
/// <remarks>
/// Layout models are safe to keep in a view model. Per-control realization state is stored by
/// <see cref="IDataGridLayoutContext"/>, so the same model can be assigned to more than one grid.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
interface IDataGridLayoutModel : INotifyPropertyChanged
{
    /// <summary>
    /// Raised when a model change requires the owning grid to run layout again.
    /// </summary>
    event EventHandler<DataGridLayoutInvalidatedEventArgs>? LayoutInvalidated;

    /// <summary>
    /// Creates an algorithm instance for one DataGrid layout context.
    /// </summary>
    /// <returns>A new layout algorithm instance.</returns>
    IDataGridLayoutAlgorithm CreateAlgorithm();
}
