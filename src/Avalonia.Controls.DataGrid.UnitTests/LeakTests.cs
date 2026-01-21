using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Collections;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.Controls.DataGridTests;

[DotMemoryUnit(FailIfRunWithoutSupport = false)]
public class LeakTests
{
    // Need to have the collection as field, so GC will not free it
    private readonly ObservableCollection<string> _observableCollection = new();

    public LeakTests(ITestOutputHelper output)
    {
        DotMemoryUnitTestOutput.SetOutputMethod(output.WriteLine);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void DataGrid_Is_Freed()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

        // When attached to INotifyCollectionChanged, DataGrid will subscribe to its events, potentially causing leak
        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));

            return await session.Dispatch(
                () => {
                    var window = new Window
                    {
                        Content = new DataGrid
                        {
                            ItemsSource = _observableCollection
                        }
                    };

                    window.SetThemeStyles();
                    window.Show();

                    // Do a layout and make sure that DataGrid gets added to visual tree.
                    window.Show();
                    Assert.IsType<DataGrid>(window.Presenter?.Child);

                    // Clear the content and ensure the DataGrid is removed.
                    window.Content = null;
                    Dispatcher.UIThread.RunJobs();
                    Assert.Null(window.Presenter.Child);

                    return window;
                },
                CancellationToken.None);
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
        {
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount);
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGridCollectionView>()).ObjectsCount);
        });

        GC.KeepAlive(result);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void DataGrid_ItemsSourceSwap_DoesNotLeak()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

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

        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));

            return await session.Dispatch(
                () =>
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

                    window.SetThemeStyles();
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.IsType<DataGrid>(window.Presenter?.Child);

                    grid.ItemsSource = itemsB;
                    Dispatcher.UIThread.RunJobs();
                    grid.ItemsSource = null;
                    Dispatcher.UIThread.RunJobs();

                    window.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    return window;
                },
                CancellationToken.None);
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
        {
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount);
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGridCollectionView>()).ObjectsCount);
        });

        GC.KeepAlive(itemsA);
        GC.KeepAlive(itemsB);
        GC.KeepAlive(result);
    }
    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void ContentControl_TemplateSwap_DoesNotLeak_DataGrid()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

        var items = new[]
        {
            new SwapItem("A"),
            new SwapItem("B")
        };

        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));

            return await session.Dispatch(
                () =>
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
                        return grid;
                    }));

                    var window = new Window { Content = contentControl };
                    window.Show();
                    Dispatcher.UIThread.RunJobs();

                    contentControl.Content = items[0];
                    Dispatcher.UIThread.RunJobs();

                    for (var i = 0; i < 50; i++)
                    {
                        contentControl.Content = items[i % items.Length];
                        Dispatcher.UIThread.RunJobs();
                    }

                    window.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    return window;
                },
                CancellationToken.None);
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount));

        GC.KeepAlive(result);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void ContentControl_TemplateSwap_BackgroundThread_DoesNotLeak_DataGrid()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));
            ReproMainViewModel? viewModel = null;
            Window? window = null;

            await session.Dispatch(
                () =>
                {
                    viewModel = new ReproMainViewModel();

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

                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                },
                CancellationToken.None);

            viewModel!.StartStopCommand.Execute(null);

            var runUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(300);
            while (DateTime.UtcNow < runUntil)
            {
                await Task.Delay(10);
                await session.Dispatch(() => Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            }

            viewModel.StartStopCommand.Execute(null);

            for (var i = 0; i < 100 && viewModel.IsRunning; i++)
            {
                await Task.Delay(10);
                await session.Dispatch(() => Dispatcher.UIThread.RunJobs(), CancellationToken.None);
            }

            await session.Dispatch(
                () =>
                {
                    window!.Content = null;
                    Dispatcher.UIThread.RunJobs();
                },
                CancellationToken.None);

            return window!;
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount));

        GC.KeepAlive(result);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void DataGrid_ExternalCollectionView_Grouping_DoesNotLeak()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

        var items = new ObservableCollection<GroupedRowItem>
        {
            new GroupedRowItem("A 1", "Group A"),
            new GroupedRowItem("A 2", "Group A"),
            new GroupedRowItem("B 1", "Group B"),
        };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedRowItem.Group)));
        view.Refresh();

        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));

            return await session.Dispatch(
                () =>
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
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.IsType<DataGrid>(window.Presenter?.Child);

                    window.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    return window;
                },
                CancellationToken.None);
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount));

        GC.KeepAlive(view);
        GC.KeepAlive(result);
    }

    [Fact]
    [SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "Needed for dotMemoryUnit to work")]
    public void DataGrid_ExternalCollectionView_GroupingToggle_DoesNotLeak()
    {
        if (!dotMemoryApi.IsEnabled)
        {
            return;
        }

        var items = new ObservableCollection<GroupedRowItem>
        {
            new GroupedRowItem("A 1", "Group A"),
            new GroupedRowItem("A 2", "Group A"),
            new GroupedRowItem("B 1", "Group B"),
        };
        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedRowItem.Group)));
        view.Refresh();

        var run = async () =>
        {
            using var session = HeadlessUnitTestSession.StartNew(typeof(Application));

            return await session.Dispatch(
                () =>
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
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    Assert.IsType<DataGrid>(window.Presenter?.Child);

                    grid.ExpandAllGroups();
                    Dispatcher.UIThread.RunJobs();
                    grid.CollapseAllGroups();
                    Dispatcher.UIThread.RunJobs();

                    view.GroupDescriptions.Clear();
                    view.Refresh();
                    Dispatcher.UIThread.RunJobs();

                    window.Content = null;
                    Dispatcher.UIThread.RunJobs();

                    return window;
                },
                CancellationToken.None);
        };

        var result = run().GetAwaiter().GetResult();

        dotMemory.Check(memory =>
            Assert.Equal(0, memory.GetObjects(where => where.Type.Is<DataGrid>()).ObjectsCount));

        GC.KeepAlive(view);
        GC.KeepAlive(items);
        GC.KeepAlive(result);
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
                _cts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    while (_cts?.IsCancellationRequested != true)
                    {
                        const int Delay = 20;
                        await Task.Delay(Delay);
                        SelectedItem = Items[0];

                        await Task.Delay(Delay);
                        SelectedItem = Items[1];
                    }

                    SelectedItem = null;
                    _cts = null;
                });
            }
            else
            {
                _cts?.Cancel();
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
