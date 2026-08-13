// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Identifies the least expensive layout pass that can apply a model change.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
enum DataGridLayoutInvalidationKind
{
    /// <summary>
    /// Only existing item bounds need to be arranged again.
    /// </summary>
    Arrange,

    /// <summary>
    /// Item geometry and the extent need to be measured again.
    /// </summary>
    Measure,

    /// <summary>
    /// Cached geometry and realization state must be reset before measuring.
    /// </summary>
    Reset
}

/// <summary>
/// Provides data for <see cref="IDataGridLayoutModel.LayoutInvalidated"/>.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridLayoutInvalidatedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataGridLayoutInvalidatedEventArgs"/> class.
    /// </summary>
    /// <param name="kind">The required invalidation kind.</param>
    public DataGridLayoutInvalidatedEventArgs(DataGridLayoutInvalidationKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Gets the required invalidation kind.
    /// </summary>
    public DataGridLayoutInvalidationKind Kind { get; }
}
