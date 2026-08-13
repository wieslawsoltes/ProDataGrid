// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Arranges and measures every DataGrid row in one horizontal or vertical line.
/// </summary>
/// <remarks>
/// Use this layout for small collections that require size-to-content behavior. Prefer
/// <see cref="DataGridStackLayoutModel"/> for large collections.
/// </remarks>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridNonVirtualizingStackLayoutModel : DataGridLayoutModelBase
{
    private DataGridLayoutOrientation _orientation = DataGridLayoutOrientation.Vertical;
    private double _spacing;

    /// <summary>
    /// Gets or sets the direction in which rows are stacked.
    /// </summary>
    public DataGridLayoutOrientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value, DataGridLayoutInvalidationKind.Reset);
    }

    /// <summary>
    /// Gets or sets the distance between adjacent rows.
    /// </summary>
    public double Spacing
    {
        get => _spacing;
        set => SetProperty(ref _spacing, value);
    }

    /// <inheritdoc/>
    public override IDataGridLayoutAlgorithm CreateAlgorithm()
    {
        return new DataGridStackLayoutAlgorithm(this);
    }
}
