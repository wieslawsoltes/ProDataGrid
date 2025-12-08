using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Selection;

public class DataGridSelectionPagingTests
{
    [AvaloniaFact]
    public void SelectionModel_With_Paging_Persists_Selection_Across_Pages()
    {
        var items = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        var view = new DataGridCollectionView(items)
        {
            PageSize = 2
        };

        var selection = new SelectionModel<string> { SingleSelect = false };

        var grid = CreateGrid(view, selection);
        grid.UpdateLayout();

        selection.Select(0); // first page
        selection.Select(3); // third page (zero-based global index)
        grid.UpdateLayout();

        Assert.Contains(items[0], selection.SelectedItems);
        Assert.Contains(items[3], selection.SelectedItems);
        Assert.Equal(2, selection.SelectedItems.Count);

        var selectedBeforePaging = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selectedBeforePaging);

        view.MoveToNextPage(); // page 1 (items B, C)
        grid.UpdateLayout();

        var selectedAfterPage1 = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selectedAfterPage1);

        view.MoveToLastPage(); // page 2 (items D, E)
        grid.UpdateLayout();

        var selectedAfterLast = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selectedAfterLast);
    }

    [AvaloniaFact]
    public void SelectedItems_Binding_Persists_Selection_When_Paging()
    {
        var items = new ObservableCollection<string> { "A", "B", "C", "D", "E" };
        var view = new DataGridCollectionView(items)
        {
            PageSize = 2
        };

        var boundSelection = new ObservableCollection<object>
        {
            items[0],
            items[3]
        };

        var grid = CreateGrid(view);
        grid.SelectedItems = boundSelection;
        grid.UpdateLayout();

        var selected = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selected);
        Assert.Equal(boundSelection.Count, selected.Length);

        view.MoveToNextPage();
        grid.UpdateLayout();

        selected = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selected);
        Assert.Equal(boundSelection.Count, selected.Length);

        view.MoveToLastPage();
        grid.UpdateLayout();

        selected = grid.SelectedItems.Cast<string>().OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "D" }, selected);
        Assert.Equal(boundSelection.Count, selected.Length);
    }

    private static DataGrid CreateGrid(IEnumerable items, SelectionModel<string>? selection = null)
    {
        var root = new Window
        {
            Width = 250,
            Height = 150,
            Styles =
            {
                new StyleInclude((System.Uri?)null)
                {
                    Source = new System.Uri("avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml")
                },
            }
        };

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Extended,
            AutoGenerateColumns = true,
        };

        if (selection != null)
        {
            grid.Selection = selection;
        }

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(".")
        });

        root.Content = grid;
        root.Show();
        return grid;
    }
}
