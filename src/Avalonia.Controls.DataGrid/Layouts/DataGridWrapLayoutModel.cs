// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Arranges variable-size DataGrid rows in virtualized wrapping rows or columns.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridWrapLayoutModel : DataGridLayoutModelBase
{
    private DataGridLayoutOrientation _orientation = DataGridLayoutOrientation.Horizontal;
    private double _horizontalSpacing;
    private double _verticalSpacing;
    private int _maximumCachedLines = 256;

    /// <summary>
    /// Gets or sets the direction in which items fill each line before wrapping.
    /// </summary>
    public DataGridLayoutOrientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value, DataGridLayoutInvalidationKind.Reset);
    }

    /// <summary>
    /// Gets or sets the horizontal distance between items or columns.
    /// </summary>
    public double HorizontalSpacing
    {
        get => _horizontalSpacing;
        set => SetProperty(ref _horizontalSpacing, value);
    }

    /// <summary>
    /// Gets or sets the vertical distance between items or rows.
    /// </summary>
    public double VerticalSpacing
    {
        get => _verticalSpacing;
        set => SetProperty(ref _verticalSpacing, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of recently measured line records retained for anchoring.
    /// </summary>
    /// <remarks>
    /// Values less than one use a single cached line. The default is 256 and does not grow with the
    /// item count.
    /// </remarks>
    public int MaximumCachedLines
    {
        get => _maximumCachedLines;
        set => SetProperty(ref _maximumCachedLines, value);
    }

    /// <inheritdoc/>
    public override IDataGridLayoutAlgorithm CreateAlgorithm()
    {
        return new DataGridWrapLayoutAlgorithm(this);
    }
}
