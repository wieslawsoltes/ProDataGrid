using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Controls.DataGridClipboard;
using Avalonia.Controls.DataGridConditionalFormatting;
using Avalonia.Controls.DataGridFiltering;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.DataGridSearching;
using Avalonia.Controls.DataGridSorting;
using Avalonia.Controls.Selection;
using Xunit;
using static Avalonia.Controls.DataGridTests.LeakTestHelpers;
using static Avalonia.Controls.DataGridTests.LeakTestSession;

namespace Avalonia.Controls.DataGridTests;

public class LeakTests
{
    // Need to have the collection as field, so GC will not free it
    private readonly ObservableCollection<string> _observableCollection = new();

    [ReleaseFact]
    public void DataGrid_Is_Freed()
    {
        // When attached to INotifyCollectionChanged, DataGrid will subscribe to its events, potentially causing leak
        var (gridRef, viewRef) = RunInSession(() => RunDataGridIsFreed(_observableCollection));
        AssertCollected(gridRef, viewRef);

        GC.KeepAlive(_observableCollection);
    }

    [ReleaseFact]
    public void DataGrid_ItemsSourceSwap_DoesNotLeak()
    {
        var itemsA = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };

        var itemsB = new ObservableCollection<RowItem>
        {
            new RowItem("C"),
            new RowItem("D")
        };
        (WeakReference GridRef, WeakReference ViewRef) Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = itemsA
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(RowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles(DataGridTheme.SimpleV2);
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            grid.ItemsSource = itemsB;
            Dispatcher.UIThread.RunJobs();
            grid.ItemsSource = null;
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);
            var viewRef = new WeakReference(grid.CollectionView!);

            CleanupWindow(window);

            return (gridRef, viewRef);
        }

        var (gridRef, viewRef) = RunInSession(Run);

        AssertCollected(gridRef, viewRef);

        GC.KeepAlive(itemsA);
        GC.KeepAlive(itemsB);
    }

    [ReleaseFact]
    public void ContentControl_TemplateSwap_DoesNotLeak_DataGrid()
    {
        var items = new[]
        {
            new SwapItem("A"),
            new SwapItem("B")
        };
        var gridRefs = new List<WeakReference>();
        var viewRefs = new List<WeakReference>();
        void Run()
        {
            var contentControl = new ContentControl();
            contentControl.DataTemplates.Add(new FuncDataTemplate<SwapItem>((item, _) =>
            {
                var grid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    ItemsSource = item.Rows
                };
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new Binding(nameof(RowItem.Name))
                });
                gridRefs.Add(new WeakReference(grid));
                if (grid.CollectionView != null)
                {
                    viewRefs.Add(new WeakReference(grid.CollectionView));
                }
                return grid;
            }));

            var window = new Window { Content = contentControl };
            window.SetThemeStyles(DataGridTheme.SimpleV2);
            ShowWindow(window);

            contentControl.Content = items[0];
            Dispatcher.UIThread.RunJobs();

            for (var i = 0; i < 50; i++)
            {
                contentControl.Content = items[i % items.Length];
                Dispatcher.UIThread.RunJobs();
            }

            CleanupWindow(window);
        }

        RunInSession(Run);

        AssertCollected(gridRefs.ToArray());
        if (viewRefs.Count > 0)
        {
            AssertCollected(viewRefs.ToArray());
        }
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public async Task ContentControl_TemplateSwap_BackgroundThread_DoesNotLeak_DataGrid()
    {
        var gridRefs = await RunInSessionAsync(async () =>
        {
            var gridRefs = new List<WeakReference>();
            var viewModel = new ReproMainViewModel();
            Window? window = null;

            var menuItem = new MenuItem { Header = "Start/Stop" };
            menuItem.Bind(MenuItem.CommandProperty, new Binding(nameof(ReproMainViewModel.StartStopCommand)));
            var menu = new Menu();
            menu.Items.Add(menuItem);

            var listBox = new ListBox { Width = 100 };
            listBox.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(ReproMainViewModel.Items)));
            listBox.Bind(ListBox.SelectedItemProperty, new Binding(nameof(ReproMainViewModel.SelectedItem))
            {
                Mode = BindingMode.TwoWay
            });
            listBox.ItemTemplate = new FuncDataTemplate<ReproItemViewModel>((_, _) =>
            {
                var textBlock = new TextBlock();
                textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ReproItemViewModel.Name)));
                return textBlock;
            });

            var contentControl = new ContentControl();
            contentControl.Bind(ContentControl.ContentProperty, new Binding(nameof(ReproMainViewModel.SelectedItem)));
            contentControl.DataTemplates.Add(new FuncDataTemplate<ReproItemViewModel>((_, _) =>
            {
                var stack = new StackPanel();

                var nameBlock = new TextBlock();
                nameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ReproItemViewModel.Name)));
                stack.Children.Add(nameBlock);

                var grid = new DataGrid();
                grid.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(ReproItemViewModel.Rows)));
                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Name",
                    Binding = new Binding(nameof(ReproRowViewModel.Name))
                });
                gridRefs.Add(new WeakReference(grid));
                stack.Children.Add(grid);

                return stack;
            }));

            var dockPanel = new DockPanel();
            DockPanel.SetDock(menu, Dock.Top);
            DockPanel.SetDock(listBox, Dock.Left);
            dockPanel.Children.Add(menu);
            dockPanel.Children.Add(listBox);
            dockPanel.Children.Add(contentControl);

            window = new Window
            {
                Content = dockPanel,
                DataContext = viewModel
            };

            window.SetThemeStyles(DataGridTheme.SimpleV2);
            ShowWindow(window);

            viewModel.StartStopCommand.Execute(null);

            var runUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(300);
            while (DateTime.UtcNow < runUntil)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }

            viewModel.StartStopCommand.Execute(null);

            for (var i = 0; i < 100 && viewModel.IsRunning; i++)
            {
                await Task.Delay(10);
                Dispatcher.UIThread.RunJobs();
            }

            CleanupWindow(window!);

            return gridRefs.ToArray();
        });

        AssertCollected(gridRefs);
    }

    [ReleaseFact]
    public void DataGrid_ExternalCollectionView_Grouping_DoesNotLeak()
    {
        var items = new ObservableCollection<GroupedRowItem>
        {
            new GroupedRowItem("A 1", "Group A"),
            new GroupedRowItem("A 2", "Group A"),
            new GroupedRowItem("B 1", "Group B"),
        };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedRowItem.Group)));
        view.Refresh();
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = view
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(GroupedRowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles(DataGridTheme.SimpleV2);
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            grid.ColumnDefinitionsSource = null;
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);
        AssertCollected(gridRef);

        GC.KeepAlive(view);
    }

    [ReleaseFact]
    public void DataGrid_ExternalCollectionView_GroupingToggle_DoesNotLeak()
    {
        var items = new ObservableCollection<GroupedRowItem>
        {
            new GroupedRowItem("A 1", "Group A"),
            new GroupedRowItem("A 2", "Group A"),
            new GroupedRowItem("B 1", "Group B"),
        };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedRowItem.Group)));
        view.Refresh();
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = view
            };
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(GroupedRowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            grid.ExpandAllGroups();
            Dispatcher.UIThread.RunJobs();
            grid.CollapseAllGroups();
            Dispatcher.UIThread.RunJobs();

            view.GroupDescriptions.Clear();
            view.Refresh();
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);
        AssertCollected(gridRef);

        GC.KeepAlive(view);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_BoundColumns_DoesNotLeak()
    {
        var columns = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(RowItem.Name))
            }
        };

        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        var gridRef = RunInSession(() => RunBoundColumns(columns, items));

        AssertCollected(gridRef);

        GC.KeepAlive(columns);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ColumnsSwap_WithSpecializedColumns_DoesNotLeak()
    {
        var columnsA = new ObservableCollection<DataGridColumn>
        {
            new DataGridCheckBoxColumn
            {
                Header = "Check",
                Binding = new Binding(nameof(BoolRowItem.IsChecked))
            }
        };

        var columnsB = new ObservableCollection<DataGridColumn>
        {
            new DataGridToggleSwitchColumn
            {
                Header = "Toggle",
                Binding = new Binding(nameof(BoolRowItem.IsChecked))
            }
        };

        var items = new ObservableCollection<BoolRowItem>
        {
            new BoolRowItem(true),
            new BoolRowItem(false)
        };
        var gridRef = RunInSession(() => RunColumnsSwapWithSpecializedColumns(columnsA, columnsB, items));

        AssertCollected(gridRef);

        GC.KeepAlive(columnsA);
        GC.KeepAlive(columnsB);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ColumnDefinitionsSource_DoesNotLeak()
    {
        var definitions = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<RowItem, string>(x => x.Name)
            }
        };

        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        var gridRef = RunInSession(() => RunColumnDefinitionsSource(definitions, items));

        AssertCollected(gridRef);

        GC.KeepAlive(definitions);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ColumnDefinitionsSourceSwap_DoesNotLeak()
    {
        var definitionsA = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<RowItem, string>(x => x.Name)
            }
        };

        var definitionsB = new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Name",
                Binding = DataGridBindingDefinition.Create<RowItem, string>(x => x.Name),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            }
        };

        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        var gridRef = RunInSession(() => RunColumnDefinitionsSourceSwap(definitionsA, definitionsB, items));

        AssertCollected(gridRef);

        GC.KeepAlive(definitionsA);
        GC.KeepAlive(definitionsB);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_SelectedItemsBinding_DoesNotLeak()
    {
        var selectedItems = new ObservableCollection<object>();
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                ItemsSource = items,
                SelectedItems = selectedItems
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(selectedItems);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_SelectedCellsBinding_DoesNotLeak()
    {
        var selectedCells = new ObservableCollection<DataGridCellInfo>();
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                ItemsSource = items,
                SelectedCells = selectedCells
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(selectedCells);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ExternalSelectionModel_DoesNotLeak()
    {
        var selectionModel = new SelectionModel<object>();
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                ItemsSource = items,
                Selection = selectionModel
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(selectionModel);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ExternalStateModels_DoesNotLeak()
    {
        var sortingModel = new SortingModel();
        var filteringModel = new FilteringModel();
        var searchModel = new SearchModel();
        var formattingModel = new ConditionalFormattingModel();
        var hierarchicalModel = new HierarchicalModel();
        var fastPathOptions = new DataGridFastPathOptions();
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                SortingModel = sortingModel,
                FilteringModel = filteringModel,
                SearchModel = searchModel,
                ConditionalFormattingModel = formattingModel,
                HierarchicalModel = hierarchicalModel,
                FastPathOptions = fastPathOptions
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(RowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(sortingModel);
        GC.KeepAlive(filteringModel);
        GC.KeepAlive(searchModel);
        GC.KeepAlive(formattingModel);
        GC.KeepAlive(hierarchicalModel);
        GC.KeepAlive(fastPathOptions);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ExternalEditingElement_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Name",
                CellTemplate = new FuncDataTemplate<RowItem>((item, _) =>
                {
                    var textBlock = new TextBlock();
                    textBlock.Bind(TextBlock.TextProperty, new Binding(nameof(RowItem.Name)));
                    return textBlock;
                }),
                CellEditingTemplate = new FuncDataTemplate<RowItem>((item, _) =>
                {
                    var textBox = new TextBox();
                    textBox.Bind(TextBox.TextProperty, new Binding(nameof(RowItem.Name))
                    {
                        Mode = BindingMode.TwoWay
                    });
                    return textBox;
                })
            });

            var externalBox = new TextBox();

            var panel = new StackPanel();
            panel.Children.Add(grid);
            panel.Children.Add(externalBox);

            var window = new Window
            {
                Content = panel
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            grid.BeginEdit();
            Dispatcher.UIThread.RunJobs();

            externalBox.Focus();
            Dispatcher.UIThread.RunJobs();

            panel.Children.Remove(grid);
            RunJobsAndRender();
            for (var i = 0; i < 3; i++)
            {
                ExecuteLayoutPass(window);
                RunJobsAndRender();
            }
            window.UpdateLayout();
            RunJobsAndRender();

            return new WeakReference(grid);
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ValidationSubscription_DoesNotLeak()
    {
        var items = new ObservableCollection<ValidatingRowItem>
        {
            new ValidatingRowItem("ok")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(ValidatingRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.SelectedItem = items[0];
            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            grid.ScrollIntoView(items[0], grid.Columns[0]);
            Dispatcher.UIThread.RunJobs();
            PumpLayout(grid);

            Assert.True(grid.BeginEdit());
            Dispatcher.UIThread.RunJobs();

            var row = grid.EditingRow;
            Assert.NotNull(row);
            var cell = row!.Cells[grid.CurrentColumnIndex];
            var textBox = cell.Content as TextBox;
            Assert.NotNull(textBox);

            textBox!.DataContext = items[0];
            textBox!.Text = "bad";
            Dispatcher.UIThread.RunJobs();

            Assert.False(grid.CommitEdit());
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_NotifyDataErrorInfoValidation_DoesNotLeak()
    {
        var items = new ObservableCollection<NotifyDataErrorRowItem>
        {
            new NotifyDataErrorRowItem("A")
        };
        items[0].SetErrors(new[] { "Invalid" });
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(NotifyDataErrorRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.ScrollIntoView(items[0], grid.Columns[0]);
            Dispatcher.UIThread.RunJobs();
            PumpLayout(grid);

            grid.SelectedItem = items[0];
            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            Dispatcher.UIThread.RunJobs();
            Assert.True(grid.BeginEdit());
            Dispatcher.UIThread.RunJobs();

            var row = grid.EditingRow;
            Assert.NotNull(row);
            Assert.False(row!.IsValid);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_NotifyDataErrorInfo_ErrorsChanged_DoesNotLeak()
    {
        var items = new ObservableCollection<NotifyDataErrorRowItem>
        {
            new NotifyDataErrorRowItem("A")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(NotifyDataErrorRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.SelectedItem = items[0];
            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            Dispatcher.UIThread.RunJobs();
            Assert.True(grid.BeginEdit());
            Dispatcher.UIThread.RunJobs();

            items[0].SetErrors(new object[] { "Invalid" });
            Dispatcher.UIThread.RunJobs();
            items[0].SetErrors(Array.Empty<object>());
            Dispatcher.UIThread.RunJobs();

            grid.CancelEdit();
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ClipboardImport_DoesNotLeak()
    {
        var items = new ObservableCollection<EditableRowItem>
        {
            new EditableRowItem("A"),
            new EditableRowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(EditableRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.SelectedItem = items[0];
            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            Dispatcher.UIThread.RunJobs();

            Assert.True(grid.PasteText("Pasted"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Pasted", items[0].Name);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ClipboardImportModelSwap_DoesNotLeak()
    {
        var items = new ObservableCollection<EditableRowItem>
        {
            new EditableRowItem("A"),
            new EditableRowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(EditableRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var customImportModel = new TrackingClipboardImportModel();
            var customImportFactory = new TrackingClipboardImportModelFactory();
            grid.ClipboardImportModel = customImportModel;

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.SelectedItem = items[0];
            grid.CurrentCell = new DataGridCellInfo(items[0], grid.Columns[0], 0, 0);
            Dispatcher.UIThread.RunJobs();

            Assert.True(grid.PasteText("Swap A"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(customImportModel.Calls > 0);

            grid.ClipboardImportModelFactory = customImportFactory;
            grid.ClipboardImportModel = null;
            Dispatcher.UIThread.RunJobs();

            Assert.True(grid.PasteText("Swap B"));
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(customImportFactory.Model);
            Assert.True(customImportFactory.Model!.Calls > 0);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_ClipboardExporterSwap_DoesNotLeak()
    {
        var items = new ObservableCollection<EditableRowItem>
        {
            new EditableRowItem("A"),
            new EditableRowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(EditableRowItem.Name))
                {
                    Mode = BindingMode.TwoWay
                }
            });

            var customExporter = new TrackingClipboardExporter();
            var customFormatExporter = new TrackingClipboardFormatExporter();
            grid.ClipboardExporter = customExporter;

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);

            grid.SelectedItems.Add(items[0]);
            Dispatcher.UIThread.RunJobs();

            grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Text);
            Dispatcher.UIThread.RunJobs();
            Assert.True(customExporter.Calls > 0);

            grid.ClipboardExporter = null;
            grid.ClipboardFormatExporters = new IDataGridClipboardFormatExporter[] { customFormatExporter };
            Dispatcher.UIThread.RunJobs();

            grid.CopySelectionToClipboard(DataGridClipboardExportFormat.Text);
            Dispatcher.UIThread.RunJobs();
            Assert.True(customFormatExporter.Calls > 0);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_SummaryRows_WithExternalColumns_DoesNotLeak()
    {
        var columns = new ObservableCollection<DataGridColumn>();
        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        };
        nameColumn.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Count,
            Scope = DataGridSummaryScope.Both
        });
        columns.Add(nameColumn);

        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                Columns = columns,
                ShowTotalSummary = true,
                ShowGroupSummary = true,
                GroupSummaryPosition = DataGridGroupSummaryPosition.Both
            };
            if (grid.CollectionView is DataGridCollectionView view)
            {
                view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(RowItem.Name)));
            }

            var window = new Window
            {
                Width = 400,
                Height = 300
            };

            window.SetThemeStyles(DataGridTheme.SimpleV2);
            window.Content = grid;
            window.Show();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<DataGrid>(window.Presenter?.Child);
            Assert.NotNull(grid.TotalSummaryRow);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(columns);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_SummaryRows_ToggleVisibility_DoesNotLeak()
    {
        var columns = new ObservableCollection<DataGridColumn>();
        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(RowItem.Name))
        };
        nameColumn.Summaries.Add(new DataGridAggregateSummaryDescription
        {
            Aggregate = DataGridAggregateType.Count,
            Scope = DataGridSummaryScope.Both
        });
        columns.Add(nameColumn);

        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                Columns = columns
            };
            if (grid.CollectionView is DataGridCollectionView view)
            {
                view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(RowItem.Name)));
            }

            var window = new Window
            {
                Width = 400,
                Height = 300
            };

            window.SetThemeStyles(DataGridTheme.SimpleV2);
            window.Content = grid;
            window.Show();
            grid.UpdateLayout();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            grid.GroupSummaryPosition = DataGridGroupSummaryPosition.Both;
            grid.ShowTotalSummary = true;
            grid.ShowGroupSummary = true;
            grid.RecalculateSummaries();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(grid.TotalSummaryRow);

            grid.ShowGroupSummary = false;
            grid.ShowTotalSummary = false;
            grid.RecalculateSummaries();
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(columns);
        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_RowDetailsTemplate_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible,
                RowDetailsTemplate = new FuncDataTemplate<RowItem>((item, _) =>
                {
                    var details = new Border
                    {
                        Margin = new Avalonia.Thickness(4),
                        Child = new TextBlock { Text = "Details" }
                    };
                    return details;
                })
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(RowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_RowDetailsTemplateSwap_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var templateA = new FuncDataTemplate<RowItem>((item, _) =>
            {
                var details = new Border
                {
                    Padding = new Avalonia.Thickness(2),
                    Child = new TextBlock { Text = "Details A" }
                };
                return details;
            });

            var templateB = new FuncDataTemplate<RowItem>((item, _) =>
            {
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = "Details B" });
                return panel;
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                ItemsSource = items,
                RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible,
                RowDetailsTemplate = templateA
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(RowItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            grid.RowDetailsTemplate = templateB;
            Dispatcher.UIThread.RunJobs();
            grid.RowDetailsTemplate = templateA;
            Dispatcher.UIThread.RunJobs();

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_DragSelectionAutoScroll_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = true,
                ItemsSource = items
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            StartDragAutoScroll(grid);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_FillHandleAutoScroll_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = true,
                ItemsSource = items
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            StartFillAutoScroll(grid);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_RowDragDropAutoScroll_DoesNotLeak()
    {
        var items = new ObservableCollection<RowItem>
        {
            new RowItem("A"),
            new RowItem("B")
        };
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = true,
                CanUserReorderRows = true,
                ItemsSource = items
            };

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            StartRowDragDropAutoScroll(grid);

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(items);
    }

    [ReleaseFact]
    public void DataGrid_HierarchicalGuardTimer_DoesNotLeak()
    {
        var roots = new ObservableCollection<TreeItem>
        {
            new TreeItem("Root", new List<TreeItem>
            {
                new TreeItem("Child")
            })
        };

        var model = new HierarchicalModel(new HierarchicalOptions
        {
            ChildrenSelector = item => ((TreeItem)item).Children
        });
        model.SetRoots(roots);
        WeakReference Run()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                HierarchicalModel = model,
                HierarchicalRowsEnabled = true
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new Binding(nameof(TreeItem.Name))
            });

            var window = new Window
            {
                Content = grid
            };

            window.SetThemeStyles();
            ShowWindow(window);
            Assert.IsType<DataGrid>(window.Presenter?.Child);

            Assert.True(grid.TryToggleHierarchicalAtSlot(0));

            var gridRef = new WeakReference(grid);

            CleanupWindow(window);

            return gridRef;
        }

        var gridRef = RunInSession(Run);

        AssertCollected(gridRef);

        GC.KeepAlive(roots);
        GC.KeepAlive(model);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunBoundColumns(
        ObservableCollection<DataGridColumn> columns,
        ObservableCollection<RowItem> items)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            Columns = columns,
            SummaryRecalculationDelayMs = 0
        };

        var window = new Window
        {
            Content = grid
        };

        window.SetThemeStyles();
        ShowWindow(window);
        Assert.IsType<DataGrid>(window.Presenter?.Child);

        var gridRef = new WeakReference(grid);

        CleanupWindow(window);
        Assert.Null(columns[0].OwningGrid);

        return gridRef;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunColumnDefinitionsSource(
        ObservableCollection<DataGridColumnDefinition> definitions,
        ObservableCollection<RowItem> items)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ColumnDefinitionsSource = definitions
        };

        var window = new Window
        {
            Content = grid
        };

        window.SetThemeStyles();
        ShowWindow(window);
        Assert.IsType<DataGrid>(window.Presenter?.Child);

        var gridRef = new WeakReference(grid);

        CleanupWindow(window);

        return gridRef;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunColumnDefinitionsSourceSwap(
        ObservableCollection<DataGridColumnDefinition> definitionsA,
        ObservableCollection<DataGridColumnDefinition> definitionsB,
        ObservableCollection<RowItem> items)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            ColumnDefinitionsSource = definitionsA
        };

        var window = new Window
        {
            Content = grid
        };

        window.SetThemeStyles();
        ShowWindow(window);
        Assert.IsType<DataGrid>(window.Presenter?.Child);

        grid.ColumnDefinitionsSource = definitionsB;
        Dispatcher.UIThread.RunJobs();

        grid.ColumnDefinitionsSource = null;
        Dispatcher.UIThread.RunJobs();

        grid.ItemsSource = null;
        Dispatcher.UIThread.RunJobs();

        var gridRef = new WeakReference(grid);

        CleanupWindow(window);

        return gridRef;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RunColumnsSwapWithSpecializedColumns(
        ObservableCollection<DataGridColumn> columnsA,
        ObservableCollection<DataGridColumn> columnsB,
        ObservableCollection<BoolRowItem> items)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            ItemsSource = items,
            Columns = columnsA
        };

        var window = new Window
        {
            Content = grid
        };

        window.SetThemeStyles();
        ShowWindow(window);
        Assert.IsType<DataGrid>(window.Presenter?.Child);

        // Clear bound columns before swapping to avoid inline binding conflicts.
        grid.Columns = null;
        Dispatcher.UIThread.RunJobs();
        grid.Columns = columnsB;
        Dispatcher.UIThread.RunJobs();

        var gridRef = new WeakReference(grid);

        grid.ItemsSource = null;
        Dispatcher.UIThread.RunJobs();

        CleanupWindow(window);

        Assert.Null(columnsA[0].OwningGrid);
        Assert.Null(columnsB[0].OwningGrid);

        return gridRef;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference GridRef, WeakReference ViewRef) RunDataGridIsFreed(ObservableCollection<string> items)
    {
        var grid = new DataGrid
        {
            ItemsSource = items
        };
        var window = new Window
        {
            Content = grid
        };

        window.SetThemeStyles(DataGridTheme.SimpleV2);
        ShowWindow(window);
        Assert.IsType<DataGrid>(window.Presenter?.Child);

        var gridRef = new WeakReference(grid);
        var viewRef = new WeakReference(grid.CollectionView!);

        CleanupWindow(window);
        Assert.Null(window.Presenter.Child);

        return (gridRef, viewRef);
    }

    private static void StartDragAutoScroll(DataGrid grid)
    {
        SetPrivateField(grid, "_isDraggingSelection", true);
        InvokePrivateMethod(grid, "StartDragAutoScroll");
    }

    private static void StartFillAutoScroll(DataGrid grid)
    {
        SetPrivateField(grid, "_isFillHandleDragging", true);
        InvokePrivateMethod(grid, "StartFillAutoScroll");
    }

    private static void StartRowDragDropAutoScroll(DataGrid grid)
    {
        var controller = GetPrivateField(grid, "_rowDragDropController");
        Assert.NotNull(controller);
        InvokePrivateMethod(controller!, "StartAutoScroll", new[] { typeof(int) }, new object[] { 1 });
    }

    private sealed class SwapItem
    {
        public SwapItem(string name)
        {
            Rows = new ObservableCollection<RowItem>
            {
                new RowItem($"{name} 1"),
                new RowItem($"{name} 2")
            };
        }

        public ObservableCollection<RowItem> Rows { get; }
    }

    private sealed class RowItem
    {
        public RowItem(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    private sealed class BoolRowItem
    {
        public BoolRowItem(bool isChecked)
        {
            IsChecked = isChecked;
        }

        public bool IsChecked { get; set; }
    }

    private sealed class EditableRowItem
    {
        public EditableRowItem(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }

    private sealed class ValidatingRowItem
    {
        private string _name;

        public ValidatingRowItem(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (value == "bad")
                {
                    throw new DataValidationException("Invalid value.");
                }

                _name = value;
            }
        }
    }

    private sealed class NotifyDataErrorRowItem : INotifyDataErrorInfo
    {
        private readonly List<object> _errors = new();
        private string _name;

        public NotifyDataErrorRowItem(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public bool HasErrors => _errors.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName == nameof(Name))
            {
                return _errors;
            }

            return Array.Empty<object>();
        }

        public void SetErrors(IEnumerable<object> errors)
        {
            _errors.Clear();
            _errors.AddRange(errors);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name)));
        }
    }

    private sealed class TrackingClipboardExporter : IDataGridClipboardExporter
    {
        public int Calls { get; private set; }

        public IAsyncDataTransfer? BuildClipboardData(DataGridClipboardExportContext context)
        {
            Calls++;

            var item = new DataTransferItem();
            item.Set(DataFormat.Text, "export");
            var transfer = new DataTransfer();
            transfer.Add(item);
            return transfer;
        }
    }

    private sealed class TrackingClipboardFormatExporter : IDataGridClipboardFormatExporter
    {
        public int Calls { get; private set; }

        public bool TryExport(DataGridClipboardExportContext context, DataTransferItem item)
        {
            Calls++;
            item.Set(DataFormat.Text, "format");
            return true;
        }
    }

    private sealed class TrackingClipboardImportModel : IDataGridClipboardImportModel
    {
        public int Calls { get; private set; }

        public bool Paste(DataGridClipboardImportContext context)
        {
            Calls++;
            return true;
        }
    }

    private sealed class TrackingClipboardImportModelFactory : IDataGridClipboardImportModelFactory
    {
        public TrackingClipboardImportModel? Model { get; private set; }

        public IDataGridClipboardImportModel Create()
        {
            Model = new TrackingClipboardImportModel();
            return Model;
        }
    }

    private sealed class TreeItem
    {
        public TreeItem(string name, List<TreeItem>? children = null)
        {
            Name = name;
            Children = children ?? new List<TreeItem>();
        }

        public string Name { get; }

        public List<TreeItem> Children { get; }
    }

    private sealed class GroupedRowItem
    {
        public GroupedRowItem(string name, string group)
        {
            Name = name;
            Group = group;
        }

        public string Name { get; }

        public string Group { get; }
    }

    private sealed class ReproMainViewModel : INotifyPropertyChanged
    {
        private CancellationTokenSource? _cts;
        private ReproItemViewModel? _selectedItem;

        public ReproMainViewModel()
        {
            Items = new List<ReproItemViewModel>
            {
                new ReproItemViewModel("A"),
                new ReproItemViewModel("B")
            };
            StartStopCommand = new SimpleCommand(StartStop);
        }

        public List<ReproItemViewModel> Items { get; }

        public ReproItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!ReferenceEquals(_selectedItem, value))
                {
                    _selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
                }
            }
        }

        public ICommand StartStopCommand { get; }

        public bool IsRunning => _cts != null;

        private void StartStop()
        {
            if (_cts == null)
            {
                var cts = new CancellationTokenSource();
                _cts = cts;
                Task.Run(async () =>
                {
                    try
                    {
                        const int Delay = 20;
                        while (!cts.IsCancellationRequested)
                        {
                            await Task.Delay(Delay, cts.Token).ConfigureAwait(false);
                            SelectedItem = Items[0];

                            await Task.Delay(Delay, cts.Token).ConfigureAwait(false);
                            SelectedItem = Items[1];
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    finally
                    {
                        SelectedItem = null;
                        _cts = null;
                    }
                });
            }
            else
            {
                _cts.Cancel();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class ReproItemViewModel : INotifyPropertyChanged
    {
        private string _name;

        public ReproItemViewModel(string name)
        {
            _name = name;
            Rows = Enumerable.Range(1, 10)
                .Select(x => new ReproRowViewModel($"name {x}"))
                .ToList();
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        public List<ReproRowViewModel> Rows { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class ReproRowViewModel : INotifyPropertyChanged
    {
        private string _name;

        public ReproRowViewModel(string name)
        {
            _name = name;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class SimpleCommand : ICommand
    {
        private readonly Action _execute;

        public SimpleCommand(Action execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged;
    }
}
