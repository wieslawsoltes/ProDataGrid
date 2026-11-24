// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Linq;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.RowHeightEstimators;

public class DataGridDefaultRowHeightEstimatorTests
{
    [AvaloniaFact]
    public void Each_DataGrid_Gets_Its_Own_Default_Estimator()
    {
        var first = new DataGrid { ItemsSource = new[] { "a" } };
        var second = new DataGrid { ItemsSource = new[] { "b" } };

        Assert.IsType<AdvancedRowHeightEstimator>(first.RowHeightEstimator);
        Assert.IsType<AdvancedRowHeightEstimator>(second.RowHeightEstimator);
        Assert.NotSame(first.RowHeightEstimator, second.RowHeightEstimator);
    }

    [AvaloniaFact]
    public void Laying_Out_A_DataGrid_Leaves_The_Default_Of_Unused_Grids_Unchanged()
    {
        var unused = new DataGrid();
        var shared = unused.RowHeightEstimator;
        var grid = new DataGrid { ItemsSource = Enumerable.Range(0, 100).Select(i => "Row " + i).ToList() };
        grid.Columns.Add(new DataGridTextColumn { Binding = new Binding(".") });
        var window = new Window { Width = 300, Height = 200 };
        window.SetThemeStyles();
        window.Content = grid;

        window.Show();
        try
        {
            grid.UpdateLayout();

            Assert.NotSame(shared, grid.RowHeightEstimator);
            Assert.True(grid.RowHeightEstimator.GetDiagnostics().CachedHeightCount > 0);
            Assert.Same(shared, unused.RowHeightEstimator);
            Assert.Equal(0, shared.GetDiagnostics().CachedHeightCount);
        }
        finally
        {
            window.Close();
        }
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
