// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridLogicalScrollableDetachTests
{
    [AvaloniaFact]
    public void Reparent_between_windows_with_layout_transform_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 200).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var mainWindow = new Window
        {
            Width = 480,
            Height = 320
        };

        mainWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var inlineHost = CreateHost();
        mainWindow.Content = inlineHost;

        var grid = new DataGrid
        {
            ItemsSource = items,
            UseLogicalScrollable = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            KeepRecycledContainersInVisualTree = true,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                mainWindow.Show();
                inlineHost.Content = grid;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                grid.ScrollIntoView(items[^1], grid.Columns[0]);
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                for (var i = 0; i < 5; i++)
                {
                    inlineHost.Content = null;
                    var toolWindow = CreateToolWindow(grid, out var toolHost);
                    try
                    {
                        toolWindow.Show(mainWindow);
                        Dispatcher.UIThread.RunJobs();
                        toolWindow.UpdateLayout();
                        grid.UpdateLayout();
                    }
                    finally
                    {
                        toolHost.Content = null;
                        toolWindow.Close();
                    }

                    Dispatcher.UIThread.RunJobs();
                    inlineHost.Content = grid;
                    Dispatcher.UIThread.RunJobs();
                    mainWindow.UpdateLayout();
                    grid.UpdateLayout();
                }
            });
        }
        finally
        {
            mainWindow.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Reparent_between_content_presenters_with_layout_transform_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 200).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var mainWindow = new Window
        {
            Width = 480,
            Height = 320
        };

        mainWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var inlineHost = new ContentPresenter();
        mainWindow.Content = inlineHost;

        var toolHost = new ContentPresenter();
        var toolTransform = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.97, 0.97),
            Child = toolHost
        };
        var toolWindow = new Window
        {
            Width = 420,
            Height = 260,
            Content = new Border
            {
                Padding = new Thickness(8),
                Child = toolTransform
            }
        };
        toolWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var grid = new DataGrid
        {
            ItemsSource = items,
            UseLogicalScrollable = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            KeepRecycledContainersInVisualTree = true,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                mainWindow.Show();
                inlineHost.Content = grid;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                grid.ScrollIntoView(items[^1], grid.Columns[0]);
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                for (var i = 0; i < 5; i++)
                {
                    inlineHost.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    toolHost.Content = grid;
                    toolWindow.Show(mainWindow);
                    Dispatcher.UIThread.RunJobs();
                    toolWindow.UpdateLayout();
                    grid.UpdateLayout();

                    toolHost.Content = null;
                    toolWindow.Hide();
                    Dispatcher.UIThread.RunJobs();

                    inlineHost.Content = grid;
                    Dispatcher.UIThread.RunJobs();
                    mainWindow.UpdateLayout();
                    grid.UpdateLayout();
                }
            });
        }
        finally
        {
            toolWindow.Close();
            mainWindow.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Reparent_during_selection_changes_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 120).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var selectionModel = new SelectionModel<object?>();

        var grid = new DataGrid
        {
            ItemsSource = items,
            Selection = selectionModel,
            SelectionMode = DataGridSelectionMode.Extended,
            UseLogicalScrollable = true,
            KeepRecycledContainersInVisualTree = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        var selectionView = new ItemsControl
        {
            ItemsSource = selectionModel.SelectedItems
        };

        var content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                grid,
                selectionView
            }
        };

        var mainWindow = new Window
        {
            Width = 480,
            Height = 320
        };
        mainWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var inlineHost = CreateHost();
        var reparentHost = CreateHost();
        var reparentTransform = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.97, 0.97),
            Child = reparentHost
        };

        mainWindow.Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                inlineHost,
                reparentTransform
            }
        };

        var reparented = false;
        EventHandler<SelectionModelSelectionChangedEventArgs> selectionChanged = (_, __) =>
        {
            if (reparented)
                return;

            reparented = true;
            inlineHost.Content = null;
            reparentHost.Content = content;
        };

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                mainWindow.Show();
                inlineHost.Content = content;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                Assert.Same(grid.CollectionView, selectionModel.Source);

                selectionModel.SelectionChanged += selectionChanged;

                selectionModel.Select(0);
                selectionModel.Select(1);
                selectionModel.Select(2);

                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                reparentHost.Content = null;
                inlineHost.Content = content;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();
            });
        }
        finally
        {
            selectionModel.SelectionChanged -= selectionChanged;
            mainWindow.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Reparent_between_windows_during_selection_changes_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 120).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var selectionModel = new SelectionModel<object?>();

        var grid = new DataGrid
        {
            ItemsSource = items,
            Selection = selectionModel,
            SelectionMode = DataGridSelectionMode.Extended,
            UseLogicalScrollable = true,
            KeepRecycledContainersInVisualTree = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        var selectionView = new ItemsControl
        {
            ItemsSource = selectionModel.SelectedItems
        };

        var content = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                grid,
                selectionView
            }
        };

        var mainWindow = new Window
        {
            Width = 480,
            Height = 320
        };
        mainWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var inlineHost = CreateHost();
        mainWindow.Content = inlineHost;

        var toolHost = CreateHost();
        var toolTransform = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.97, 0.97),
            Child = toolHost
        };
        var toolWindow = new Window
        {
            Width = 420,
            Height = 260,
            Content = new Border
            {
                Padding = new Thickness(8),
                Child = toolTransform
            }
        };
        toolWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var reparented = false;
        EventHandler<SelectionModelSelectionChangedEventArgs> selectionChanged = (_, __) =>
        {
            if (reparented)
                return;

            reparented = true;
            Dispatcher.UIThread.Post(() =>
            {
                inlineHost.Content = null;
                mainWindow.Hide();
                toolHost.Content = content;
                toolWindow.Show();
            });
        };

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                mainWindow.Show();
                inlineHost.Content = content;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                Assert.Same(grid.CollectionView, selectionModel.Source);

                selectionModel.SelectionChanged += selectionChanged;

                selectionModel.Select(0);
                selectionModel.Select(1);
                selectionModel.Select(2);

                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                toolWindow.UpdateLayout();
                grid.UpdateLayout();

                toolHost.Content = null;
                toolWindow.Hide();
                inlineHost.Content = content;
                mainWindow.Show();

                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();
            });
        }
        finally
        {
            selectionModel.SelectionChanged -= selectionChanged;
            toolWindow.Close();
            mainWindow.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Detach_defers_rows_presenter_cleanup_until_dispatcher()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 120).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var window = new Window
        {
            Width = 480,
            Height = 320
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        var grid = new DataGrid
        {
            ItemsSource = items,
            UseLogicalScrollable = true,
            KeepRecycledContainersInVisualTree = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            Height = 200
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        window.Content = grid;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            grid.ScrollIntoView(items[^1], grid.Columns[0]);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            grid.Columns.Add(new DataGridTextColumn { Header = "Extra", Binding = new Binding(nameof(AutoHideItem.Value)) });
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var rowsPresenter = grid.GetVisualDescendants()
                .OfType<DataGridRowsPresenter>()
                .Single();

            Assert.True(rowsPresenter.Children.OfType<DataGridRow>().Any());

            window.Content = null;

            Assert.True(rowsPresenter.Children.OfType<DataGridRow>().Any());

            Dispatcher.UIThread.RunJobs();

            Assert.False(rowsPresenter.Children.OfType<DataGridRow>().Any());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_tabs_with_runtime_columns_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 160).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var grid = CreateRuntimeColumnsGrid(items);

        var tabControl = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = "Grid",
                    Content = new Border
                    {
                        Padding = new Thickness(8),
                        Child = grid
                    }
                },
                new TabItem
                {
                    Header = "Other",
                    Content = new TextBlock { Text = "Placeholder tab" }
                }
            }
        };

        var window = new Window
        {
            Width = 720,
            Height = 420,
            Content = tabControl
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                tabControl.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                grid.UpdateLayout();

                grid.ScrollIntoView(items[^1], grid.Columns[0]);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                grid.UpdateLayout();

                AddRuntimeColumn(grid, 0);
                Dispatcher.UIThread.RunJobs();

                for (var i = 0; i < 8; i++)
                {
                    tabControl.SelectedIndex = 1;
                    AddRuntimeColumn(grid, i + 1);
                    tabControl.SelectedIndex = 0;
                    AddRuntimeColumn(grid, i + 101);

                    Dispatcher.UIThread.RunJobs();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    grid.UpdateLayout();
                }
            });
        }
        finally
        {
            window.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Docking_and_undocking_with_runtime_columns_does_not_throw()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 180).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var grid = CreateRuntimeColumnsGrid(items);

        var mainWindow = new Window
        {
            Width = 760,
            Height = 460
        };
        mainWindow.SetThemeStyles(DataGridTheme.SimpleV2);

        var inlineHost = CreateHost();
        mainWindow.Content = inlineHost;

        var toolWindow = CreateToolWindow(grid, out var toolHost);
        toolHost.Content = null;

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                mainWindow.Show();
                inlineHost.Content = grid;
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                grid.ScrollIntoView(items[^1], grid.Columns[0]);
                Dispatcher.UIThread.RunJobs();
                mainWindow.UpdateLayout();
                grid.UpdateLayout();

                AddRuntimeColumn(grid, 0);
                Dispatcher.UIThread.RunJobs();

                for (var i = 0; i < 8; i++)
                {
                    inlineHost.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    toolHost.Content = grid;
                    toolWindow.Show(mainWindow);
                    Dispatcher.UIThread.RunJobs();
                    toolWindow.UpdateLayout();
                    grid.UpdateLayout();

                    AddRuntimeColumn(grid, i + 1);

                    toolHost.Content = null;
                    toolWindow.Hide();
                    Dispatcher.UIThread.RunJobs();

                    AddRuntimeColumn(grid, i + 101);

                    inlineHost.Content = grid;
                    Dispatcher.UIThread.RunJobs();
                    mainWindow.UpdateLayout();
                    grid.UpdateLayout();
                }
            });
        }
        finally
        {
            toolWindow.Close();
            mainWindow.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void Runtime_columns_added_while_detached_rebuild_headers_on_reattach()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 48).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var grid = CreateRuntimeColumnsGrid(items);
        var extraA = new DataGridTextColumn
        {
            Header = "Extra A",
            Binding = new Binding(nameof(AutoHideItem.Value))
        };
        var extraB = new DataGridTextColumn
        {
            Header = "Extra B",
            Binding = new Binding(nameof(AutoHideItem.Value))
        };

        var window = new Window
        {
            Width = 640,
            Height = 420,
            Content = grid
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            grid.Columns.Add(extraA);
            grid.Columns.Add(extraB);

            window.Content = grid;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var headersPresenter = grid.GetVisualDescendants()
                .OfType<DataGridColumnHeadersPresenter>()
                .Single();

            Assert.Contains(extraA.HeaderCell, headersPresenter.Children);
            Assert.Contains(extraB.HeaderCell, headersPresenter.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Runtime_columns_mutated_while_detached_keep_column_state_consistent_after_reattach_cycles()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 64).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var grid = CreateRuntimeColumnsGrid(items);
        AddRuntimeColumn(grid, 10);
        AddRuntimeColumn(grid, 11);
        AddRuntimeColumn(grid, 12);

        var window = new Window
        {
            Width = 640,
            Height = 420,
            Content = grid
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            AssertRuntimeColumnStateInSync(grid);

            for (var i = 0; i < 6; i++)
            {
                window.Content = null;
                Dispatcher.UIThread.RunJobs();

                if (i % 2 == 0)
                {
                    if (grid.Columns.Count > 2)
                    {
                        grid.Columns.RemoveAt(grid.Columns.Count - 1);
                    }

                    AddRuntimeColumn(grid, 100 + i);
                    grid.Columns[^1].DisplayIndex = 0;
                }
                else
                {
                    var lastIndex = grid.Columns.Count - 1;
                    if (lastIndex > 1)
                    {
                        grid.Columns[1].DisplayIndex = lastIndex;
                    }

                    if (grid.Columns.Count > 3)
                    {
                        grid.Columns.RemoveAt(2);
                    }
                }

                window.Content = grid;
                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
                window.ApplyTemplate();
                grid.ApplyTemplate();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                grid.UpdateLayout();
                AssertRuntimeColumnStateInSync(grid);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Runtime_display_index_mutated_while_detached_is_reconciled_on_reattach()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 32).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var grid = CreateRuntimeColumnsGrid(items);
        AddRuntimeColumn(grid, 20);
        AddRuntimeColumn(grid, 21);

        var movedColumn = grid.Columns[^1];
        var firstColumn = grid.Columns[0];

        var window = new Window
        {
            Width = 640,
            Height = 420,
            Content = grid
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            AssertRuntimeColumnStateInSync(grid);

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            movedColumn.DisplayIndex = 0;

            window.Content = grid;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            AssertRuntimeColumnStateInSync(grid);
            Assert.Equal(0, movedColumn.DisplayIndex);
            Assert.Equal(1, firstColumn.DisplayIndex);

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            firstColumn.DisplayIndex = grid.Columns.Count - 1;

            window.Content = grid;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            AssertRuntimeColumnStateInSync(grid);
            Assert.Equal(grid.Columns.Count - 1, firstColumn.DisplayIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Runtime_multiple_display_index_mutations_while_detached_match_attached_behavior_after_reattach()
    {
        var items = new ObservableCollection<AutoHideItem>(
            Enumerable.Range(1, 32).Select(i => new AutoHideItem
            {
                Name = $"Item {i:000}",
                Value = i
            }));

        var moves = new (string Header, int DisplayIndex)[]
        {
            ("Extra 20", 0),
            ("Name", 5),
            ("Extra 18", 3),
            ("Value", 4),
            ("Extra 17", 1)
        };

        var baselineGrid = CreateRuntimeColumnsGrid(items);
        AddRuntimeColumn(baselineGrid, 17);
        AddRuntimeColumn(baselineGrid, 18);
        AddRuntimeColumn(baselineGrid, 19);
        AddRuntimeColumn(baselineGrid, 20);
        ApplyDisplayIndexMoves(baselineGrid, moves);
        var expectedOrder = GetHeaderOrderByDisplayIndex(baselineGrid);

        var grid = CreateRuntimeColumnsGrid(items);
        AddRuntimeColumn(grid, 17);
        AddRuntimeColumn(grid, 18);
        AddRuntimeColumn(grid, 19);
        AddRuntimeColumn(grid, 20);

        var window = new Window
        {
            Width = 640,
            Height = 420,
            Content = grid
        };
        window.SetThemeStyles(DataGridTheme.SimpleV2);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            ApplyDisplayIndexMoves(grid, moves);

            window.Content = grid;
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();
            window.ApplyTemplate();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            AssertRuntimeColumnStateInSync(grid);
            var actualOrder = GetHeaderOrderByDisplayIndex(grid);
            Assert.Equal(expectedOrder, actualOrder);
        }
        finally
        {
            window.Close();
        }
    }

    private static DataGrid CreateRuntimeColumnsGrid(ObservableCollection<AutoHideItem> items)
    {
        var grid = new DataGrid
        {
            ItemsSource = items,
            UseLogicalScrollable = true,
            KeepRecycledContainersInVisualTree = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            Height = 220
        };

        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(AutoHideItem.Name)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(AutoHideItem.Value)) });

        return grid;
    }

    private static void AddRuntimeColumn(DataGrid grid, int suffix)
    {
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = $"Extra {suffix}",
            Binding = new Binding(nameof(AutoHideItem.Value))
        });
    }

    private static void AssertRuntimeColumnStateInSync(DataGrid grid)
    {
        var columns = grid.Columns.ToArray();
        var expectedDisplayIndexes = Enumerable.Range(0, columns.Length).ToArray();
        var actualDisplayIndexes = columns
            .Select(column => column.DisplayIndex)
            .OrderBy(displayIndex => displayIndex)
            .ToArray();
        var actualIndexes = columns
            .Select(column => column.Index)
            .OrderBy(index => index)
            .ToArray();

        Assert.Equal(expectedDisplayIndexes, actualDisplayIndexes);
        Assert.Equal(expectedDisplayIndexes, actualIndexes);
    }

    private static void ApplyDisplayIndexMoves(DataGrid grid, (string Header, int DisplayIndex)[] moves)
    {
        foreach (var (header, displayIndex) in moves)
        {
            var column = grid.Columns
                .Single(column => string.Equals(column.Header?.ToString(), header, StringComparison.Ordinal));
            column.DisplayIndex = displayIndex;
        }
    }

    private static string[] GetHeaderOrderByDisplayIndex(DataGrid grid)
    {
        return grid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Select(column => column.Header?.ToString() ?? string.Empty)
            .ToArray();
    }

    private sealed class AutoHideItem
    {
        public string Name { get; set; } = string.Empty;

        public int Value { get; set; }
    }

    private static ContentControl CreateHost()
    {
        return new ContentControl
        {
            Template = new FuncControlTemplate<ContentControl>((parent, _) =>
            {
                return new ContentPresenter
                {
                    [!ContentPresenter.ContentProperty] = parent[!ContentControl.ContentProperty],
                    [!ContentPresenter.ContentTemplateProperty] = parent[!ContentControl.ContentTemplateProperty],
                    [!ContentPresenter.HorizontalContentAlignmentProperty] = parent[!ContentControl.HorizontalContentAlignmentProperty],
                    [!ContentPresenter.VerticalContentAlignmentProperty] = parent[!ContentControl.VerticalContentAlignmentProperty]
                };
            })
        };
    }

    private static Window CreateToolWindow(Control content, out ContentControl host)
    {
        host = CreateHost();
        host.Content = content;

        var toolTransform = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.97, 0.97),
            Child = host
        };

        var toolWindow = new Window
        {
            Width = 420,
            Height = 260,
            Content = new Border
            {
                Padding = new Thickness(8),
                Child = toolTransform
            }
        };

        toolWindow.SetThemeStyles(DataGridTheme.SimpleV2);
        return toolWindow;
    }
}
