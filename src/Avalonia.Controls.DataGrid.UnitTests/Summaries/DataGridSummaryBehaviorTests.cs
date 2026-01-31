// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Summaries;

public class DataGridSummaryBehaviorTests
{
    [AvaloniaFact]
    public void SummaryCell_Content_Updates_When_Value_Changes_With_ContentTemplate()
    {
        var cell = new DataGridSummaryCell();
        var description = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum,
            ContentTemplate = new FuncDataTemplate<object>((_, _) => new TextBlock())
        };

        cell.Description = description;
        cell.Value = 1;

        Assert.Equal(1, cell.Content);

        cell.Value = 2;

        Assert.Equal(2, cell.Content);
    }

    [AvaloniaFact]
    public void SummaryCell_Prefers_Exact_Scope_Over_Both()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 },
            new() { Value = 2 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var bothDescription = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum,
            Scope = DataGridSummaryScope.Both
        };

        var totalDescription = new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Count,
            Scope = DataGridSummaryScope.Total
        };

        column.Summaries.Add(bothDescription);
        column.Summaries.Add(totalDescription);

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            grid.RecalculateSummaries();

            var cell = grid.TotalSummaryRow.Cells[0];

            Assert.Same(totalDescription, cell.Description);
            Assert.Equal(2, cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryCell_Detaches_From_Column_Changes_When_Removed()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        var themeBefore = new ControlTheme(typeof(DataGridSummaryCell));
        var themeAfter = new ControlTheme(typeof(DataGridSummaryCell));
        column.SummaryCellTheme = themeBefore;

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Same(themeBefore, cell.Theme);

            grid.ColumnsInternal.Remove(column);
            column.SummaryCellTheme = themeAfter;

            Assert.Same(themeBefore, cell.Theme);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TotalSummaryRow_Detaches_When_Grid_Unloaded()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        var summaryRow = grid.TotalSummaryRow;

        Assert.NotNull(summaryRow);
        Assert.Same(grid, summaryRow.OwningGrid);

        window.Content = null;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(summaryRow.OwningGrid);

        window.Close();
    }

    [AvaloniaFact]
    public void TotalSummaryRow_Reattaches_OwnerRow()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            var summaryRow = grid.TotalSummaryRow;
            Assert.NotNull(summaryRow);
            Assert.NotNull(summaryRow.CellsPresenter);
            Assert.Same(summaryRow, summaryRow.CellsPresenter.OwnerRow);

            window.Content = null;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            window.Content = grid;
            window.UpdateLayout();
            grid.UpdateLayout();

            Assert.NotNull(summaryRow.CellsPresenter);
            Assert.Same(summaryRow, summaryRow.CellsPresenter.OwnerRow);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryService_Updates_Timer_Interval_When_Delay_Changes()
    {
        var grid = new DataGrid();
        var summaryService = grid.SummaryService;
        summaryService.ScheduleRecalculation();

        var timerField = typeof(DataGridSummaryService).GetField("_debounceTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        var timer = (DispatcherTimer?)timerField?.GetValue(summaryService);

        Assert.NotNull(timer);

        grid.SummaryRecalculationDelayMs = 250;

        Assert.Equal(TimeSpan.FromMilliseconds(250), timer!.Interval);

        summaryService.Dispose();
    }

    [AvaloniaFact]
    public void SummaryRecalculated_Raises_When_GroupDescriptions_Change()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Group = "A", Value = 1 },
            new() { Group = "B", Value = 2 }
        };

        var view = new DataGridCollectionView(items);

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = view,
            ShowGroupSummary = true,
            SummaryRecalculationDelayMs = 0
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Count
        });
        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            var recalculated = 0;
            grid.SummaryRecalculated += (_, __) => recalculated++;

            recalculated = 0;
            view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(SummaryItem.Group)));

            Assert.True(recalculated > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Uses_CollectionView_Filter_When_ItemsSource_Is_List()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 },
            new() { Value = 2 },
            new() { Value = 3 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            var view = (DataGridCollectionView)grid.CollectionView;
            view.Filter = item => item is SummaryItem summary && summary.Value > 1;
            view.Refresh();

            grid.RecalculateSummaries();

            Assert.Equal(5m, grid.TotalSummaryRow.Cells[0].Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Recalculates_When_Column_Binding_Changes()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1, OtherValue = 10 },
            new() { Value = 2, OtherValue = 20 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true,
            SummaryRecalculationDelayMs = 0
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            grid.RecalculateSummaries();

            Assert.Equal(3m, Assert.IsType<decimal>(grid.TotalSummaryRow.Cells[0].Value));

            column.Binding = new Binding(nameof(SummaryItem.OtherValue));
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(30m, Assert.IsType<decimal>(grid.TotalSummaryRow.Cells[0].Value));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Summary_Recalculates_When_Cell_Edit_Commits()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true,
            SummaryRecalculationDelayMs = 0
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            grid.RecalculateSummaries();
            Assert.Equal(1m, Assert.IsType<decimal>(grid.TotalSummaryRow.Cells[0].Value));

            var slot = grid.SlotFromRowIndex(0);
            Assert.True(grid.UpdateSelectionAndCurrency(column.Index, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false));
            grid.UpdateLayout();

            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();

            var cell = FindCell(grid, items[0], column.Index);
            var textBox = Assert.IsType<TextBox>(cell.Content);
            textBox.Text = "5";

            Assert.True(grid.CommitEdit());
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(5m, Assert.IsType<decimal>(grid.TotalSummaryRow.Cells[0].Value));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryCache_Is_Cleared_When_Column_Removed()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            grid.RecalculateSummaries();

            var cache = GetSummaryCache(grid);

            Assert.True(cache.Count > 0);

            grid.ColumnsInternal.Remove(column);

            Assert.Empty(cache);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryRow_Clears_Cells_And_Cache_When_Columns_Cleared()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };
        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        try
        {
            grid.RecalculateSummaries();

            var cache = GetSummaryCache(grid);

            Assert.True(grid.TotalSummaryRow.Cells.Count > 0);
            Assert.True(cache.Count > 0);

            grid.ColumnsInternal.Clear();

            Assert.Empty(grid.TotalSummaryRow.Cells);
            Assert.Empty(cache);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryService_Disposed_On_Detach_And_Reinitialized_On_Attach()
    {
        var window = new Window
        {
            Width = 300,
            Height = 200
        };
        window.SetThemeStyles();

        var grid = new DataGrid();

        window.Content = grid;
        window.Show();
        window.UpdateLayout();

        var initialService = grid.SummaryService;
        Assert.NotNull(initialService);

        window.Content = null;
        window.UpdateLayout();

        Assert.Null(grid.SummaryService);

        window.Content = grid;
        window.UpdateLayout();

        Assert.NotNull(grid.SummaryService);
        Assert.NotSame(initialService, grid.SummaryService);

        window.Close();
    }

    [AvaloniaFact]
    public void SummaryCell_Uses_CollectionView_Culture_For_DisplayText()
    {
        var view = new DataGridCollectionView(new ObservableCollection<SummaryItem>())
        {
            Culture = new CultureInfo("fr-FR")
        };

        var grid = new DataGrid
        {
            ItemsSource = view
        };

        var row = new DataGridSummaryRow
        {
            OwningGrid = grid
        };

        var cell = new DataGridSummaryCell
        {
            OwningRow = row,
            Description = new DataGridAggregateSummaryDescription
            {
                Aggregate = DataGridAggregateType.Sum,
                StringFormat = "N2"
            }
        };

        cell.Value = 1.2m;

        Assert.Equal("1,20", cell.DisplayText);
    }

    [AvaloniaFact]
    public void SummaryCell_Defaults_To_Grid_Alignment_When_Column_Not_Set()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);
        grid.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Right;

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Equal(HorizontalAlignment.Right, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryCell_Uses_Column_Alignment_When_Set()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value)),
            SummaryCellHorizontalContentAlignment = HorizontalAlignment.Center
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);
        grid.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Right;

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Equal(HorizontalAlignment.Center, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryCell_Updates_When_Grid_Alignment_Changes()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);

        try
        {
            grid.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Left;
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Equal(HorizontalAlignment.Left, cell.HorizontalContentAlignment);

            grid.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Right;
            Assert.Equal(HorizontalAlignment.Right, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SummaryCell_Updates_When_Column_Alignment_Changes()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridTextColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];

            column.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Center;
            Assert.Equal(HorizontalAlignment.Center, cell.HorizontalContentAlignment);

            column.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Right;
            Assert.Equal(HorizontalAlignment.Right, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NumericColumn_SummaryAlignment_Defaults_To_Right()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridNumericColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Equal(HorizontalAlignment.Right, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void NumericColumn_SummaryAlignment_Respects_Grid_Override()
    {
        var items = new ObservableCollection<SummaryItem>
        {
            new() { Value = 1 }
        };

        var column = new DataGridNumericColumn
        {
            Header = "Value",
            Binding = new Binding(nameof(SummaryItem.Value))
        };

        var (window, grid, _) = CreateSummaryGrid(items, column);
        grid.SummaryCellHorizontalContentAlignment = HorizontalAlignment.Left;

        try
        {
            var cell = grid.TotalSummaryRow.Cells[0];
            Assert.Equal(HorizontalAlignment.Left, cell.HorizontalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    private static (Window window, DataGrid grid, DataGridColumn column) CreateSummaryGrid(
        ObservableCollection<SummaryItem> items,
        DataGridColumn column)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300
        };
        window.SetThemeStyles();

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ShowTotalSummary = true
        };

        column.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Sum
        });

        grid.ColumnsInternal.Add(column);

        window.Content = grid;
        window.Show();
        grid.UpdateLayout();

        return (window, grid, column);
    }

    private static IDictionary GetSummaryCache(DataGrid grid)
    {
        var summaryService = grid.SummaryService;
        var cacheField = typeof(DataGridSummaryService).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        var cache = (DataGridSummaryCache)cacheField!.GetValue(summaryService)!;

        var dictionaryField = typeof(DataGridSummaryCache).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        return (IDictionary)dictionaryField!.GetValue(cache)!;
    }

    private static DataGridRow FindRow(SummaryItem item, DataGrid grid)
    {
        return grid
            .GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .First(r => ReferenceEquals(r.DataContext, item));
    }

    private static DataGridCell FindCell(DataGrid grid, SummaryItem item, int columnIndex)
    {
        var row = FindRow(item, grid);
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            if (cell.OwningColumn?.Index == columnIndex)
            {
                return cell;
            }
        }

        throw new InvalidOperationException($"Could not find cell for column {columnIndex}.");
    }

    private sealed class SummaryItem
    {
        public int Value { get; set; }

        public int OtherValue { get; set; }

        public string Group { get; set; } = string.Empty;
    }
}
