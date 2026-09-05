// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridDetachedConstraintTests
{
    [AvaloniaTheory]
    [InlineData(DataGridTheme.SimpleV2, true)]
    [InlineData(DataGridTheme.SimpleV2, false)]
    [InlineData(DataGridTheme.FluentV2, true)]
    [InlineData(DataGridTheme.FluentV2, false)]
    public void Detached_grid_accepts_constraint_changes_and_reapplies_them_on_attach(
        DataGridTheme theme, bool minimum)
    {
        var column = new DataGridTextColumn { Header = new string('W', 40), Width = DataGridLength.Auto };
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = new[] { "row" },
            MinColumnWidth = minimum ? 320 : 20,
            MaxColumnWidth = minimum ? 1000 : 160
        };
        grid.Columns.Add(column);
        grid.Columns.Add(new DataGridTextColumn { Header = "Remainder", Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var window = new Window { Width = 500, Height = 200, Content = grid };
        window.SetThemeStyles(theme);
        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            // Auto sizing retains an intrinsic desired width when star layout or
            // a constraint limits its display width. Model that public width state.
            column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto, 600, minimum ? 320 : 160);
            Assert.True(column.Width.DesiredValue > column.Width.DisplayValue);
            window.Content = null;
            Assert.Null(column.OwningGrid);
            if (minimum) grid.MinColumnWidth = 160;
            else grid.MaxColumnWidth = 300;
            window.Content = grid;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(grid, column.OwningGrid);
            Assert.InRange(column.ActualWidth, grid.MinColumnWidth, grid.MaxColumnWidth);
            Assert.InRange(column.ActualWidth, minimum ? 160 : 299, minimum ? 1000 : 301);
            window.Close();
            // Closed windows can still receive application dynamic-resource updates.
            if (minimum) grid.MinColumnWidth = 100;
            else grid.MaxColumnWidth = 400;
        }
        finally { window.Close(); }
    }
}
