// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridRowDetailsScrollExtentTests
{
    private const double DetailsHeight = 400;

    public static TheoryData<string> Estimators => new()
    {
        nameof(DefaultRowHeightEstimator),
        nameof(AdvancedRowHeightEstimator),
        nameof(CachingRowHeightEstimator),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Estimators))]
    public void Details_Shown_After_The_Rows_Were_Measured_Are_Part_Of_The_Rows_Height(string estimator)
    {
        var items = new ObservableCollection<DetailsItem>(Enumerable.Range(0, 40).Select(i => new DetailsItem(i)));
        var (window, grid) = Show(items, 500, estimator);
        try
        {
            double rowHeight = RowHeight(grid);
            items[5].IsExpanded = true;
            grid.GetVisualDescendants().OfType<DataGridRow>().Single(r => r.DataContext == items[5]).AreDetailsVisible = true;
            Pump(window);

            Assert.Equal(40 * rowHeight + DetailsHeight, grid.GetEdgedRowsHeight(), 3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Estimators))]
    public void Rows_Inserted_Above_A_Shown_Details_Row_Move_The_Vertical_Offset_By_Their_Own_Height(string estimator)
    {
        var items = new ObservableCollection<DetailsItem>(Enumerable.Range(0, 40).Select(i => new DetailsItem(i)));
        items[6].IsExpanded = true;
        var (window, grid) = Show(items, 150, estimator);
        try
        {
            double rowHeight = RowHeight(grid);
            grid.UpdateScroll(new Vector(0, -6 * rowHeight));
            Pump(window);
            Assert.Equal(items[6], grid.DisplayData.GetScrollingElements().OfType<DataGridRow>().First().DataContext);
            double before = grid.GetVerticalOffset();

            for (int i = 0; i < 3; i++)
            {
                items.Insert(0, new DetailsItem(100 + i));
                Pump(window);
            }

            Assert.Equal(items[9], grid.DisplayData.GetScrollingElements().OfType<DataGridRow>().First().DataContext);
            Assert.Equal(before + 3 * rowHeight, grid.GetVerticalOffset(), 3);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid) Show(ObservableCollection<DetailsItem> items, double height, string estimator)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed,
            RowDetailsTemplate = new FuncDataTemplate<DetailsItem>((_, _) => new Border { Height = DetailsHeight }),
            RowHeightEstimator = estimator switch
            {
                nameof(AdvancedRowHeightEstimator) => new AdvancedRowHeightEstimator(),
                nameof(CachingRowHeightEstimator) => new CachingRowHeightEstimator(),
                _ => new DefaultRowHeightEstimator(),
            },
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding(nameof(DetailsItem.Id)) });
        grid.LoadingRow += (_, e) => e.Row.AreDetailsVisible = e.Row.DataContext is DetailsItem { IsExpanded: true };
        var window = new Window { Width = 400, Height = height };
        window.SetThemeStyles();
        window.Content = grid;
        window.Show();
        Pump(window);
        return (window, grid);
    }

    private static double RowHeight(DataGrid grid) =>
        grid.GetVisualDescendants().OfType<DataGridRow>().First(r => r.DataContext is DetailsItem { IsExpanded: false }).DesiredSize.Height;

    private static void Pump(Window window)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private sealed class DetailsItem
    {
        public DetailsItem(int id) => Id = id;

        public int Id { get; }

        public bool IsExpanded { get; set; }
    }
}
