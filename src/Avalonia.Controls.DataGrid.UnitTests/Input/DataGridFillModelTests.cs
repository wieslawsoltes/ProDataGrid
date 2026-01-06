using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.DataGridTests;
using Avalonia.Controls.DataGridFilling;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Input;

public class DataGridFillModelTests
{
    [AvaloniaFact]
    public void DefaultFillModel_Fills_Number_Series_Vertically()
    {
        var items = new ObservableCollection<NumericRow>
        {
            new NumericRow { A = 1 },
            new NumericRow { A = 2 },
            new NumericRow(),
            new NumericRow()
        };

        var (window, grid) = CreateNumericGrid(items);
        try
        {
            var source = new DataGridCellRange(0, 1, 0, 0);
            var target = new DataGridCellRange(0, 3, 0, 0);

            grid.FillModel.ApplyFill(new DataGridFillContext(grid, source, target));

            Assert.Equal(3, items[2].A);
            Assert.Equal(4, items[3].A);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DefaultFillModel_Copies_Text_When_No_Series()
    {
        var items = new ObservableCollection<TextRow>
        {
            new TextRow { A = "Red", B = "Blue" }
        };

        var (window, grid) = CreateTextGrid(items);
        try
        {
            var source = new DataGridCellRange(0, 0, 0, 1);
            var target = new DataGridCellRange(0, 0, 0, 3);

            grid.FillModel.ApplyFill(new DataGridFillContext(grid, source, target));

            Assert.Equal("Red", items[0].C);
            Assert.Equal("Blue", items[0].D);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CustomFillModel_Can_Override_Default()
    {
        var items = new ObservableCollection<TextRow>
        {
            new TextRow { A = "A", B = "B" }
        };

        var (window, grid) = CreateTextGrid(items);
        try
        {
            grid.FillModel = new ConstantFillModel("X");

            var source = new DataGridCellRange(0, 0, 0, 1);
            var target = new DataGridCellRange(0, 0, 0, 3);

            grid.FillModel.ApplyFill(new DataGridFillContext(grid, source, target));

            Assert.Equal("X", items[0].C);
            Assert.Equal("X", items[0].D);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window Window, DataGrid Grid) CreateNumericGrid(ObservableCollection<NumericRow> items)
    {
        var window = CreateWindow();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.ColumnsInternal.Add(new DataGridNumericColumn
        {
            Header = "A",
            Binding = new Binding(nameof(NumericRow.A))
        });
        grid.ColumnsInternal.Add(new DataGridNumericColumn
        {
            Header = "B",
            Binding = new Binding(nameof(NumericRow.B))
        });
        grid.ColumnsInternal.Add(new DataGridNumericColumn
        {
            Header = "C",
            Binding = new Binding(nameof(NumericRow.C))
        });
        grid.ColumnsInternal.Add(new DataGridNumericColumn
        {
            Header = "D",
            Binding = new Binding(nameof(NumericRow.D))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private static (Window Window, DataGrid Grid) CreateTextGrid(ObservableCollection<TextRow> items)
    {
        var window = CreateWindow();

        var grid = new DataGrid
        {
            ItemsSource = items,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.Cell,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "A",
            Binding = new Binding(nameof(TextRow.A))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "B",
            Binding = new Binding(nameof(TextRow.B))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "C",
            Binding = new Binding(nameof(TextRow.C))
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "D",
            Binding = new Binding(nameof(TextRow.D))
        });

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid);
    }

    private static Window CreateWindow()
    {
        var window = new Window
        {
            Width = 640,
            Height = 480
        };

        window.SetThemeStyles();
        return window;
    }

    private sealed class ConstantFillModel : IDataGridFillModel
    {
        private readonly string _text;

        public ConstantFillModel(string text)
        {
            _text = text;
        }

        public void ApplyFill(DataGridFillContext context)
        {
            var source = context.SourceRange;
            var target = context.TargetRange;

            for (var rowIndex = target.StartRow; rowIndex <= target.EndRow; rowIndex++)
            {
                using var scope = context.BeginRowEdit(rowIndex, out var item);
                if (item == null)
                {
                    continue;
                }

                for (var columnIndex = target.StartColumn; columnIndex <= target.EndColumn; columnIndex++)
                {
                    if (source.Contains(rowIndex, columnIndex))
                    {
                        continue;
                    }

                    context.TrySetCellText(item, columnIndex, _text);
                }
            }
        }
    }

    private sealed class NumericRow
    {
        public int A { get; set; }

        public int B { get; set; }

        public int C { get; set; }

        public int D { get; set; }
    }

    private sealed class TextRow
    {
        public string A { get; set; } = string.Empty;

        public string B { get; set; } = string.Empty;

        public string C { get; set; } = string.Empty;

        public string D { get; set; } = string.Empty;
    }
}
