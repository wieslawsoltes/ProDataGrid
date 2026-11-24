// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.ObjectModel;
using System.Linq;
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

namespace Avalonia.Controls.DataGridTests;

public class DataGridAutoHeightHeadlessTests
{
    private const double TestRowHeight = 28;
    private const double TestHeaderHeight = 26;

    [AvaloniaFact]
    public void DataGrid_AutoHeight_Updates_When_Items_Change_In_StackPanel()
    {
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);
        var items = CreateItems(2);
        var grid = BuildGrid(items, useLogicalScrollable: false);

        var root = new Window
        {
            Width = 600,
            Height = 800,
            Content = new StackPanel
            {
                Children =
                {
                    grid
                }
            }
        };

        root.SetThemeStyles(DataGridTheme.SimpleV2);
        root.Show();
        PumpLayout(grid);

        var initialHeight = grid.Bounds.Height;

        items.Add(new AutoHeightItem(3, "Item 3"));
        PumpLayout(grid);
        var expandedHeight = grid.Bounds.Height;

        Assert.True(expandedHeight > initialHeight + TestRowHeight - 1,
            $"Expected grid to grow. Initial={initialHeight}, Expanded={expandedHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");

        items.RemoveAt(items.Count - 1);
        PumpLayout(grid);
        var shrunkHeight = grid.Bounds.Height;

        Assert.True(shrunkHeight < expandedHeight - 1,
            $"Expected grid to shrink. Expanded={expandedHeight}, Shrunk={shrunkHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");
    }

    [AvaloniaFact]
    public void DataGrid_AutoHeight_Updates_When_Items_Change_In_StackPanel_WithLogicalScrollable()
    {
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);
        var items = CreateItems(2);
        var grid = BuildGrid(items, useLogicalScrollable: true);

        var root = new Window
        {
            Width = 600,
            Height = 800,
            Content = new StackPanel
            {
                Children =
                {
                    grid
                }
            }
        };

        root.SetThemeStyles(DataGridTheme.SimpleV2);
        root.Show();
        PumpLayout(grid);

        var initialHeight = grid.Bounds.Height;

        items.Add(new AutoHeightItem(3, "Item 3"));
        PumpLayout(grid);
        var expandedHeight = grid.Bounds.Height;

        Assert.True(expandedHeight > initialHeight + TestRowHeight - 1,
            $"Expected grid to grow. Initial={initialHeight}, Expanded={expandedHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");

        items.RemoveAt(items.Count - 1);
        PumpLayout(grid);
        var shrunkHeight = grid.Bounds.Height;

        Assert.True(shrunkHeight < expandedHeight - 1,
            $"Expected grid to shrink. Expanded={expandedHeight}, Shrunk={shrunkHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");
    }

    [AvaloniaFact]
    public void DataGrid_AutoHeight_Updates_When_Nested_In_ListBox()
    {
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);
        var group = new AutoHeightGroup("Group A", CreateItems(2));
        var groups = new ObservableCollection<AutoHeightGroup> { group };
        DataGrid? capturedGrid = null;

        var listBox = new ListBox
        {
            ItemsSource = groups,
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel()),
            ItemTemplate = new FuncDataTemplate<AutoHeightGroup>((item, _) =>
            {
                var grid = BuildGrid(item.Items, useLogicalScrollable: false);
                capturedGrid ??= grid;
                return new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = item.Name },
                        grid
                    }
                };
            })
        };
        listBox.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
            }
        });

        var root = new Window
        {
            Width = 600,
            Height = 800,
            Content = listBox
        };

        root.SetThemeStyles(DataGridTheme.SimpleV2);
        root.Show();
        PumpLayout(listBox);

        var grid = capturedGrid ?? listBox.ContainerFromIndex(0)?
            .GetVisualDescendants()
            .OfType<DataGrid>()
            .FirstOrDefault();
        Assert.NotNull(grid);
        PumpLayout(grid!);

        var initialHeight = grid!.Bounds.Height;

        group.Items.Add(new AutoHeightItem(3, "Item 3"));
        PumpLayout(grid);
        var expandedHeight = grid.Bounds.Height;

        Assert.True(expandedHeight > initialHeight + TestRowHeight - 1,
            $"Expected nested grid to grow. Initial={initialHeight}, Expanded={expandedHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");

        group.Items.RemoveAt(group.Items.Count - 1);
        PumpLayout(grid);
        var shrunkHeight = grid.Bounds.Height;

        Assert.True(shrunkHeight < expandedHeight - 1,
            $"Expected nested grid to shrink. Expanded={expandedHeight}, Shrunk={shrunkHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");
    }

    [AvaloniaFact]
    public void DataGrid_AutoHeight_Updates_When_Nested_In_ListBox_WithLogicalScrollable()
    {
        using var themeScope = UseApplicationTheme(DataGridTheme.SimpleV2);
        var group = new AutoHeightGroup("Group A", CreateItems(2));
        var groups = new ObservableCollection<AutoHeightGroup> { group };
        DataGrid? capturedGrid = null;

        var listBox = new ListBox
        {
            ItemsSource = groups,
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel()),
            ItemTemplate = new FuncDataTemplate<AutoHeightGroup>((item, _) =>
            {
                var grid = BuildGrid(item.Items, useLogicalScrollable: true);
                capturedGrid ??= grid;
                return new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = item.Name },
                        grid
                    }
                };
            })
        };
        listBox.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters =
            {
                new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
            }
        });

        var root = new Window
        {
            Width = 600,
            Height = 800,
            Content = listBox
        };

        root.SetThemeStyles(DataGridTheme.SimpleV2);
        root.Show();
        PumpLayout(listBox);

        var grid = capturedGrid ?? listBox.ContainerFromIndex(0)?
            .GetVisualDescendants()
            .OfType<DataGrid>()
            .FirstOrDefault();
        Assert.NotNull(grid);
        PumpLayout(grid!);

        var initialHeight = grid!.Bounds.Height;

        group.Items.Add(new AutoHeightItem(3, "Item 3"));
        PumpLayout(grid);
        var expandedHeight = grid.Bounds.Height;

        Assert.True(expandedHeight > initialHeight + TestRowHeight - 1,
            $"Expected nested grid to grow. Initial={initialHeight}, Expanded={expandedHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");

        group.Items.RemoveAt(group.Items.Count - 1);
        PumpLayout(grid);
        var shrunkHeight = grid.Bounds.Height;

        Assert.True(shrunkHeight < expandedHeight - 1,
            $"Expected nested grid to shrink. Expanded={expandedHeight}, Shrunk={shrunkHeight}, Attached={grid.IsAttachedToVisualTree()}, Visible={grid.IsVisible}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DataGrid_AutoHeight_Grows_When_Collapsed_Group_Expands_In_ScrollViewer(bool useLogicalScrollable)
    {
        using var themeScope = UseApplicationTheme(DataGridTheme.FluentV2);
        var items = new ObservableCollection<GroupedAutoHeightItem>();
        for (var i = 1; i <= 12; i++)
        {
            items.Add(new GroupedAutoHeightItem(i <= 6 ? "A" : "B", $"Item {i}"));
        }

        var view = new DataGridCollectionView(items);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(GroupedAutoHeightItem.Category)));

        var grid = new DataGrid
        {
            ItemsSource = view,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            UseLogicalScrollable = useLogicalScrollable,
            MaxHeight = 300,
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(GroupedAutoHeightItem.Name)),
        });

        var root = new Window
        {
            Width = 600,
            Height = 800,
            Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        grid
                    }
                }
            }
        };

        root.SetThemeStyles(DataGridTheme.FluentV2);
        root.Show();
        PumpLayout(grid);

        var firstGroup = (DataGridCollectionViewGroup)view.Groups![0];
        var secondGroup = (DataGridCollectionViewGroup)view.Groups![1];
        grid.CollapseRowGroup(firstGroup, collapseAllSubgroups: false);
        grid.CollapseRowGroup(secondGroup, collapseAllSubgroups: false);
        PumpLayout(grid);
        var collapsedHeight = grid.Bounds.Height;

        grid.ExpandRowGroup(firstGroup, expandAllSubgroups: false);
        PumpLayout(grid);
        var expandedHeight = grid.Bounds.Height;

        var realizedItems = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => row.IsVisible && row.Bounds.Height > 0)
            .Select(row => row.DataContext)
            .OfType<GroupedAutoHeightItem>()
            .Distinct()
            .Count();

        Assert.True(expandedHeight > collapsedHeight,
            $"Expected grid to grow after expanding. Collapsed={collapsedHeight}, Expanded={expandedHeight}, Desired={grid.DesiredSize}, Bounds={grid.Bounds}");
        Assert.Equal(firstGroup.ItemCount, realizedItems);
    }

    private static ObservableCollection<AutoHeightItem> CreateItems(int count)
    {
        var items = new ObservableCollection<AutoHeightItem>();
        for (var i = 1; i <= count; i++)
        {
            items.Add(new AutoHeightItem(i, $"Item {i}"));
        }

        return items;
    }

    private static DataGrid BuildGrid(ObservableCollection<AutoHeightItem> items, bool useLogicalScrollable = false)
    {
        var grid = new DataGrid
        {
            ItemsSource = items,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            UseLogicalScrollable = useLogicalScrollable,
            RowHeight = TestRowHeight,
            ColumnHeaderHeight = TestHeaderHeight,
            MinColumnWidth = 40,
        };

        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Id",
            Binding = new Binding("Id"),
        });
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        return grid;
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        if (control.GetVisualRoot() is Window window)
        {
            window.ApplyTemplate();
            window.UpdateLayout();
        }
        control.ApplyTemplate();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static IDisposable UseApplicationTheme(DataGridTheme theme)
    {
        var styles = ThemeHelper.GetThemeStyles(theme);
        var appStyles = Application.Current?.Styles;
        appStyles?.Add(styles);
        return new ThemeScope(appStyles, styles);
    }

    private sealed class ThemeScope : IDisposable
    {
        private readonly Styles? _appStyles;
        private readonly Styles _styles;

        public ThemeScope(Styles? appStyles, Styles styles)
        {
            _appStyles = appStyles;
            _styles = styles;
        }

        public void Dispose()
        {
            _appStyles?.Remove(_styles);
        }
    }

    private sealed class AutoHeightItem
    {
        public AutoHeightItem(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class GroupedAutoHeightItem
    {
        public GroupedAutoHeightItem(string category, string name)
        {
            Category = category;
            Name = name;
        }

        public string Category { get; }
        public string Name { get; }
    }

    private sealed class AutoHeightGroup
    {
        public AutoHeightGroup(string name, ObservableCollection<AutoHeightItem> items)
        {
            Name = name;
            Items = items;
        }

        public string Name { get; }
        public ObservableCollection<AutoHeightItem> Items { get; }
    }
}
