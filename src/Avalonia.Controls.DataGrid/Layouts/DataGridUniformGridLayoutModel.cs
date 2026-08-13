// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Avalonia.Controls.DataGridLayouts;

/// <summary>
/// Arranges DataGrid rows in a virtualized, wrapping grid of equally sized cells.
/// </summary>
#if !DATAGRID_INTERNAL
public
#else
internal
#endif
sealed class DataGridUniformGridLayoutModel : DataGridLayoutModelBase
{
    private DataGridLayoutOrientation _orientation = DataGridLayoutOrientation.Horizontal;
    private double _minItemWidth = double.NaN;
    private double _minItemHeight = double.NaN;
    private double _minRowSpacing;
    private double _minColumnSpacing;
    private int _maximumRowsOrColumns = int.MaxValue;
    private DataGridUniformGridItemsJustification _itemsJustification;
    private DataGridUniformGridItemsStretch _itemsStretch;

    /// <summary>
    /// Gets or sets the direction in which items fill each line before wrapping.
    /// </summary>
    public DataGridLayoutOrientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value, DataGridLayoutInvalidationKind.Reset);
    }

    /// <summary>
    /// Gets or sets the minimum item width. The default is <see cref="double.NaN"/>, which uses
    /// the first measured item width.
    /// </summary>
    public double MinItemWidth
    {
        get => _minItemWidth;
        set => SetProperty(ref _minItemWidth, value);
    }

    /// <summary>
    /// Gets or sets the minimum item height. The default is <see cref="double.NaN"/>, which uses
    /// the first measured item height.
    /// </summary>
    public double MinItemHeight
    {
        get => _minItemHeight;
        set => SetProperty(ref _minItemHeight, value);
    }

    /// <summary>
    /// Gets or sets the minimum vertical distance between cells.
    /// </summary>
    public double MinRowSpacing
    {
        get => _minRowSpacing;
        set => SetProperty(ref _minRowSpacing, value);
    }

    /// <summary>
    /// Gets or sets the minimum horizontal distance between cells.
    /// </summary>
    public double MinColumnSpacing
    {
        get => _minColumnSpacing;
        set => SetProperty(ref _minColumnSpacing, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of items in a row or column.
    /// </summary>
    public int MaximumRowsOrColumns
    {
        get => _maximumRowsOrColumns;
        set => SetProperty(ref _maximumRowsOrColumns, value);
    }

    /// <summary>
    /// Gets or sets how unused space is distributed on the non-scrolling axis.
    /// </summary>
    public DataGridUniformGridItemsJustification ItemsJustification
    {
        get => _itemsJustification;
        set => SetProperty(ref _itemsJustification, value);
    }

    /// <summary>
    /// Gets or sets how cells consume unused space on the non-scrolling axis.
    /// </summary>
    public DataGridUniformGridItemsStretch ItemsStretch
    {
        get => _itemsStretch;
        set => SetProperty(ref _itemsStretch, value);
    }

    /// <inheritdoc/>
    public override IDataGridLayoutAlgorithm CreateAlgorithm()
    {
        return new DataGridUniformGridLayoutAlgorithm(this);
    }
}
