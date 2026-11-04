// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.RowHeightEstimators;

public class DataGridDefaultRowHeightEstimatorTests
{
    [AvaloniaFact]
    public void Each_DataGrid_Gets_Its_Own_Default_Estimator()
    {
        var first = new DataGrid();
        var second = new DataGrid();

        Assert.IsType<AdvancedRowHeightEstimator>(first.RowHeightEstimator);
        Assert.IsType<AdvancedRowHeightEstimator>(second.RowHeightEstimator);
        Assert.NotSame(first.RowHeightEstimator, second.RowHeightEstimator);
    }

    [AvaloniaFact]
    public void A_Style_Still_Replaces_The_Default_Estimator()
    {
        var styled = new DefaultRowHeightEstimator();
        var window = new Window();
        window.Styles.Add(new Style(x => x.OfType<DataGrid>())
        {
            Setters = { new Setter(DataGrid.RowHeightEstimatorProperty, styled) }
        });
        var grid = new DataGrid();
        window.Content = grid;

        window.Show();
        try
        {
            Assert.Same(styled, grid.RowHeightEstimator);
        }
        finally
        {
            window.Close();
        }
    }
}
