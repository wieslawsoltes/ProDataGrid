// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Arranges DataGrid rows in one virtualized horizontal or vertical line.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridStackLayoutModel : DataGridLayoutModelBase
{
    private DataGridLayoutOrientation _orientation = DataGridLayoutOrientation.Vertical;
    private double _spacing;
    private bool _disableVirtualization;

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

    /// <summary>
    /// Gets or sets a value indicating whether every row is realized and measured.
    /// </summary>
    public bool DisableVirtualization
    {
        get => _disableVirtualization;
        set => SetProperty(ref _disableVirtualization, value, DataGridLayoutInvalidationKind.Reset);
    }

    /// <inheritdoc/>
    public override IDataGridLayoutAlgorithm CreateAlgorithm()
    {
        return new DataGridStackLayoutAlgorithm(this, forceNonVirtualizing: false);
    }
}
