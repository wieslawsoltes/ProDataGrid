using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Xunit;
namespace Avalonia.Controls.DataGridTests.Selection;
public class ReviewDuplicateCellSelectionTests
{
    [AvaloniaFact]
    public void Default_Cell_Selection_Distinguishes_Duplicate_Item_Occurrences()
    {
        object repeated = new object();
        var grid = new DataGrid { AutoGenerateColumns = false, SelectionUnit = DataGridSelectionUnit.Cell,
            ItemsSource = new ObservableCollection<object> { repeated, repeated, new object() } };
        var column = new DataGridTextColumn();
        grid.Columns.Add(column);
        var window = new Window { Width = 400, Height = 300, Content = grid };
        window.SetThemeStyles();
        try
        {
            window.Show(); grid.UpdateLayout();
            grid.SelectedCells = new ObservableCollection<DataGridCellInfo> { new(repeated, column, 1, 0) };
            Assert.False(grid.TrackSelectedCellsByItem);
            Assert.True(grid.IsCellSelected(1, 0));
            Assert.False(grid.IsCellSelected(0, 0));
        }
        finally { window.Close(); }
    }
}
