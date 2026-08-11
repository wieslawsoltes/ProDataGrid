// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests.Columns;

public sealed class DataGridOptimizedCellTests
{
    [AvaloniaFact]
    public void DirectTextCell_UsesTypedAccessor_AndTracksItemChanges()
    {
        var item = new NotifyItem("First");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextCell = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;

        Assert.True(cell.ConfigureValueAccessor(column));
        Assert.Equal("First", cell.Value);

        var window = AttachCell(cell);
        try
        {
            item.Name = "Second";
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectTextCell_IgnoresStaleItemAfterDataContextRecycle()
    {
        var oldItem = new NotifyItem("Old");
        var currentItem = new NotifyItem("Current");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextCell = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = oldItem;
        Assert.True(cell.ConfigureValueAccessor(column));

        var window = AttachCell(cell);
        try
        {
            cell.DataContext = currentItem;
            Assert.Equal("Current", cell.Value);

            oldItem.Name = "Stale";
            Assert.Equal("Current", cell.Value);

            currentItem.Name = "Updated";
            Assert.Equal("Updated", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectTextCell_TracksChanges_From_HierarchicalNodeItem()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new DataGridTextColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectTextCell = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => ((NotifyItem)value.Item).Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = node;

        Assert.True(cell.ConfigureValueAccessor(column));
        Assert.Equal("First", cell.Value);

        var window = AttachCell(cell);
        try
        {
            item.Name = "Second";
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectTextCell_FallsBackToBinding_ForExplicitSource()
    {
        var item = new NotifyItem("First");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)) { Source = item },
            UseDirectTextCell = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());

        Assert.False(cell.ConfigureValueAccessor(column));
    }

    [AvaloniaFact]
    public void DirectTextCell_FallsBackToBinding_ForTargetNullValue()
    {
        var item = new NullableTextItem(null);
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NullableTextItem.Name)) { TargetNullValue = "(null)" },
            UseDirectTextCell = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NullableTextItem, string?>(value => value.Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));
        Dispatcher.UIThread.RunJobs();

        Assert.False(cell.UsesValueAccessor);
        Assert.Equal("(null)", cell.Value);
    }

    [AvaloniaFact]
    public void DirectTextCell_Can_Skip_Change_Subscriptions_For_Immutable_Data()
    {
        var item = new NotifyItem("First");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextCell = true,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridDirectTextCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.True(cell.ConfigureValueAccessor(column));

        item.Name = "Second";

        Assert.Equal("First", cell.Value);
        cell.DataContext = new NotifyItem("Third");
        Assert.Equal("Third", cell.Value);
    }

    [AvaloniaFact]
    public void OrdinaryRetainedTextCell_UsesTypedAccessor_WithoutReplacingLayoutCell()
    {
        var item = new NotifyItem("First");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextContent = true,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridCell>(column.CreateCell());
        cell.DataContext = item;
        var content = Assert.IsType<DataGridSearchTextBlock>(
            column.GenerateElementInternal(cell, item));

        Assert.True(content.UsesDirectAccessor);
        Assert.Equal("First", content.Text);

        content.DataContext = new NotifyItem("Second");
        Assert.Equal("Second", content.Text);
    }

    [AvaloniaFact]
    public void OrdinaryRetainedHierarchyCell_UsesTypedAccessor_AndTracksNodeState()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, level: 2, isLeaf: false);
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectCell = false,
            UseDirectTextContent = true,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => ((NotifyItem)value.Item).Name));

        var cell = Assert.IsType<DataGridCell>(column.CreateCell());
        cell.DataContext = node;
        var presenter = Assert.IsType<DataGridHierarchicalPresenter>(
            column.GenerateElementInternal(cell, node));

        Assert.True(presenter.UsesDirectValues);
        Assert.Equal("First", presenter.Content);
        Assert.Equal(2, presenter.Level);
        Assert.True(presenter.IsExpandable);

        var window = AttachCell(cell);
        try
        {
            cell.Content = presenter;
            node.IsExpanded = true;
            Assert.True(presenter.IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectHierarchicalCell_TracksNodeState()
    {
        var node = new HierarchicalNode("Node", level: 2, isLeaf: false);
        var cell = new DataGridDirectHierarchicalCell
        {
            Indent = 10,
            DataContext = node
        };

        Assert.Equal(2, cell.Level);
        Assert.Equal(new Thickness(20, 0, 0, 0), cell.Padding);
        Assert.True(cell.IsExpandable);
        Assert.False(cell.IsExpanded);

        var window = AttachCell(cell);
        try
        {
            node.IsExpanded = true;
            Assert.True(cell.IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectHierarchicalTextCell_UsesTypedAccessor_AndTracksItemChanges()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectCell = true,
            UseDirectTextContent = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => ((NotifyItem)value.Item).Name));

        var cell = Assert.IsType<DataGridDirectHierarchicalCell>(column.CreateCell());
        cell.DataContext = node;

        Assert.True(cell.ConfigureTextAccessor(column));
        Assert.Equal("First", cell.Value);

        var window = AttachCell(cell);
        try
        {
            item.Name = "Second";
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectHierarchicalCell_IgnoresStaleNodeAndItemAfterDataContextRecycle()
    {
        var oldItem = new NotifyItem("Old");
        var oldNode = new HierarchicalNode(oldItem, level: 1, isLeaf: false);
        var currentItem = new NotifyItem("Current");
        var currentNode = new HierarchicalNode(currentItem, level: 2, isLeaf: false);
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectCell = true,
            UseDirectTextContent = true
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                node => ((NotifyItem)node.Item).Name));

        var cell = Assert.IsType<DataGridDirectHierarchicalCell>(column.CreateCell());
        cell.DataContext = oldNode;
        Assert.True(cell.ConfigureTextAccessor(column));

        var window = AttachCell(cell);
        try
        {
            cell.DataContext = currentNode;
            Assert.Equal("Current", cell.Value);
            Assert.Equal(2, cell.Level);

            oldItem.Name = "Stale";
            oldNode.Level = 9;
            Assert.Equal("Current", cell.Value);
            Assert.Equal(2, cell.Level);

            currentItem.Name = "Updated";
            currentNode.Level = 3;
            Assert.Equal("Updated", cell.Value);
            Assert.Equal(3, cell.Level);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectHierarchicalTextCell_Can_Skip_Item_Change_Subscriptions_For_Immutable_Data()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectCell = true,
            UseDirectTextContent = true,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => ((NotifyItem)value.Item).Name));

        var cell = Assert.IsType<DataGridDirectHierarchicalCell>(column.CreateCell());
        cell.DataContext = node;
        Assert.True(cell.ConfigureTextAccessor(column));

        var window = AttachCell(cell);
        try
        {
            item.Name = "Second";
            Assert.Equal("First", cell.Value);
            var replacement = new HierarchicalNode(new NotifyItem("Third"), isLeaf: true);
            cell.DataContext = replacement;
            Assert.Equal("Third", cell.Value);

            replacement.IsExpanded = true;
            Assert.True(cell.IsExpanded);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectHierarchicalTextCell_Preserves_CustomTemplate_Path()
    {
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding("Item.Name"),
            UseDirectCell = true,
            UseDirectTextContent = true,
            CellTemplate = new FuncDataTemplate<HierarchicalNode>((_, _) => new TextBlock())
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(value => value.Item.ToString()!));

        var cell = Assert.IsType<DataGridDirectHierarchicalCell>(column.CreateCell());

        Assert.False(cell.ConfigureTextAccessor(column));
    }

    [Fact]
    public void OptimizedRetainedHierarchyCell_Is_Explicit_And_Preserves_CustomTemplate_Container()
    {
        var column = new DataGridHierarchicalColumn();

        Assert.IsType<DataGridCell>(column.CreateCell());

        column.UseOptimizedPresenter = true;
        Assert.IsType<DataGridDirectHierarchicalCell>(column.CreateCell());

        column.CellTemplate = new FuncDataTemplate<HierarchicalNode>((_, _) => new TextBlock());
        Assert.IsType<DataGridCell>(column.CreateCell());

        column.CellTemplate = null;
        column.UseOptimizedPresenter = false;
        Assert.IsType<DataGridCell>(column.CreateCell());
    }

    [AvaloniaFact]
    public void OptimizedColumns_CreateCoalescedCellContainers()
    {
        var drawingColumn = new DataGridCustomDrawingColumn();
        var hierarchyColumn = new DataGridHierarchicalColumn { UseDirectCell = true };
        var textColumn = new DataGridTextColumn { UseDirectTextCell = true };

        Assert.IsType<DataGridCustomDrawingCell>(drawingColumn.CreateCell());
        Assert.IsType<DataGridDirectHierarchicalCell>(hierarchyColumn.CreateCell());
        Assert.IsType<DataGridDirectTextCell>(textColumn.CreateCell());
    }

    [AvaloniaFact]
    public void OptimizedCellTheme_DirectlyHosts_ArbitraryRetainedTemplateContent()
    {
        var item = new NotifyItem("Retained content");
        var grid = new DataGrid
        {
            Width = 320,
            Height = 120,
            AutoGenerateColumns = false,
            ItemsSource = new[] { item },
        };
        grid.ColumnsInternal.Add(new DataGridTemplateColumn
        {
            Header = "Template",
            CellTemplate = new FuncDataTemplate<NotifyItem>((value, _) =>
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = value.Name },
                        new ProgressBar { Value = 50 },
                    },
                }),
        });
        var window = new Window { Width = 360, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        Assert.True(grid.TryFindResource("DataGridOptimizedCellTheme", out object? resource));
        grid.CellTheme = Assert.IsType<ControlTheme>(resource);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            DataGridRow row = Assert.IsType<DataGridRow>(grid.DisplayData.GetDisplayedRow(0));
            DataGridCell cell = row.Cells[0];
            Assert.True(cell.UseDirectChrome);
            Assert.True(cell.UseDirectContentHost);
            Assert.Empty(cell.GetVisualChildren().OfType<ContentPresenter>());
            Assert.Single(cell.GetVisualChildren().OfType<StackPanel>());
            Assert.Empty(cell.GetVisualChildren().OfType<Grid>());
            Assert.Empty(cell.GetVisualChildren().OfType<Border>());
            Assert.Empty(cell.GetVisualChildren().OfType<Rectangle>());
            Assert.Single(cell.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(textBlock => textBlock.Text == "Retained content"));
            Assert.Single(cell.GetVisualDescendants().OfType<ProgressBar>());

            cell.Background = Brushes.Moccasin;
            cell.BorderBrush = Brushes.DarkBlue;
            cell.BorderThickness = new Thickness(1, 2, 3, 4);
            cell.CornerRadius = new CornerRadius(3);
            Assert.Equal(new Thickness(1, 2, 3, 4), cell.BorderThickness);
            Assert.Equal(Brushes.DarkBlue, cell.BorderBrush);

            grid.GridLinesVisibility = DataGridGridLinesVisibility.Vertical;
            grid.VerticalGridLinesBrush = Brushes.Black;
            grid.ColumnsInternal.FillerColumn.FillerWidth = 0;
            cell.EnsureGridLine(grid.ColumnsInternal.LastVisibleColumn);
            Assert.Equal(0, cell.ActualRightGridLineWidth);

            grid.ColumnsInternal.FillerColumn.FillerWidth = 12;
            cell.EnsureGridLine(grid.ColumnsInternal.LastVisibleColumn);
            Assert.Equal(1, cell.ActualRightGridLineWidth);

            grid.GridLinesVisibility = DataGridGridLinesVisibility.None;
            cell.EnsureGridLine(grid.ColumnsInternal.LastVisibleColumn);
            Assert.Equal(0, cell.ActualRightGridLineWidth);

            cell.UseDirectChrome = false;
            Assert.Equal(0, cell.ActualRightGridLineWidth);
            cell.UseDirectChrome = true;
            cell.EnsureGridLine(grid.ColumnsInternal.LastVisibleColumn);
            Assert.Equal(0, cell.ActualRightGridLineWidth);
        }
        finally
        {
            window.Close();
        }

        Assert.False(new DataGridCell().UseDirectChrome);
    }

    [AvaloniaFact]
    public void OrdinaryColumns_DrawnMode_CreatesCoalescedCellContainers()
    {
        var text = new DataGridTextColumn { DisplayMode = DataGridColumnDisplayMode.Drawn };
        var numeric = new DataGridNumericColumn { DisplayMode = DataGridColumnDisplayMode.Drawn };
        var progress = new DataGridProgressBarColumn { DisplayMode = DataGridColumnDisplayMode.Drawn };
        var image = new DataGridImageColumn
        {
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            ImageWidth = 16,
            ImageHeight = 16
        };

        Assert.IsType<DataGridCustomDrawingCell>(text.CreateCell());
        Assert.IsType<DataGridCustomDrawingCell>(numeric.CreateCell());
        Assert.IsType<DataGridCustomDrawingCell>(progress.CreateCell());
        Assert.IsType<DataGridCustomDrawingCell>(image.CreateCell());
    }

    [AvaloniaFact]
    public void Unsupported_Draw_Configurations_Fall_Back_To_Retained_Cells()
    {
        var progress = new DataGridProgressBarColumn
        {
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            ShowProgressText = true
        };
        var image = new DataGridImageColumn
        {
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };

        Assert.IsType<DataGridCell>(progress.CreateCell());
        Assert.IsNotType<DataGridCustomDrawingCell>(progress.CreateCell());
        Assert.IsType<DataGridCell>(image.CreateCell());
        Assert.IsNotType<DataGridCustomDrawingCell>(image.CreateCell());
    }

    [AvaloniaFact]
    public void DrawnText_UsesTypedAccessor_AndTracksItemChanges()
    {
        var item = new NotifyItem("First");
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));
        Assert.Null(cell.Content);
        Assert.Equal("First", cell.Value);

        var window = AttachCell(cell);
        try
        {
            item.Name = "Second";
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnTextCell_IgnoresStaleItemAfterDataContextRecycle()
    {
        var oldItem = new NotifyItem("Old");
        var currentItem = new NotifyItem("Current");
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = oldItem;
        Assert.Null(column.GenerateDisplay(cell, oldItem));

        var window = AttachCell(cell);
        try
        {
            cell.DataContext = currentItem;
            Assert.Equal("Current", cell.Value);

            oldItem.Name = "Stale";
            Assert.Equal("Current", cell.Value);

            currentItem.Name = "Updated";
            Assert.Equal("Updated", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_Can_Skip_Change_Subscriptions_For_Immutable_Data()
    {
        var item = new NotifyItem("First");
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));

        item.Name = "Second";

        Assert.Equal("First", cell.Value);
        cell.DataContext = new NotifyItem("Third");
        Assert.Equal("Third", cell.Value);
    }

    [AvaloniaFact]
    public void DrawnText_HierarchicalNodeAccessor_WithoutTracking_RefreshesOnlyOnRecycle()
    {
        var oldItem = new NotifyItem("Old");
        var oldNode = new HierarchicalNode(oldItem, isLeaf: true);
        var currentItem = new NotifyItem("Current");
        var currentNode = new HierarchicalNode(currentItem, isLeaf: true);
        var column = new TestTextColumn
        {
            Binding = new Binding("Item.Name"),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                node => $"accessor:{((NotifyItem)node.Item).Name}"));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = oldNode;
        Assert.Null(column.GenerateDisplay(cell, oldNode));
        Assert.True(cell.UsesValueProvider);
        Assert.Equal("accessor:Old", cell.Value);

        var window = AttachCell(cell);
        try
        {
            oldItem.Name = "Ignored";
            Assert.Equal("accessor:Old", cell.Value);

            cell.DataContext = currentNode;
            Assert.Equal("accessor:Current", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_HierarchicalNodeAccessor_TracksWrappedItem_AndIgnoresStaleOrDetachedItem()
    {
        var oldItem = new NotifyItem("Old");
        var oldNode = new HierarchicalNode(oldItem, isLeaf: true);
        var currentItem = new NotifyItem("Current");
        var currentNode = new HierarchicalNode(currentItem, isLeaf: true);
        var column = new TestTextColumn
        {
            Binding = new Binding("Item.Name"),
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                node => $"accessor:{((NotifyItem)node.Item).Name}"));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = oldNode;
        Assert.Null(column.GenerateDisplay(cell, oldNode));
        Assert.True(cell.UsesValueProvider);

        var window = AttachCell(cell);
        try
        {
            oldItem.Name = "Old updated";
            Assert.Equal("accessor:Old updated", cell.Value);

            cell.DataContext = currentNode;
            Assert.Equal("accessor:Current", cell.Value);

            oldItem.Name = "Stale";
            Assert.Equal("accessor:Current", cell.Value);

            currentItem.Name = "Current updated";
            Assert.Equal("accessor:Current updated", cell.Value);

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            currentItem.Name = "Detached";
            Assert.Equal("accessor:Current updated", cell.Value);

            window.Content = cell;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal("accessor:Detached", cell.Value);

            currentItem.Name = "Reattached";
            Assert.Equal("accessor:Reattached", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_SelfWrappedHierarchyNotifier_RefreshesAccessorOncePerChange()
    {
        var item = new SelfWrappedNotifyItem("First");
        var column = new TestTextColumn
        {
            Binding = new Binding("Item.Name"),
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        int accessorReads = 0;
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<SelfWrappedNotifyItem, string>(value =>
            {
                accessorReads++;
                return value.Name;
            }));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));
        Assert.True(cell.UsesValueProvider);

        var window = AttachCell(cell);
        try
        {
            int readsBeforeChange = accessorReads;

            item.Name = "Second";

            Assert.Equal(readsBeforeChange + 1, accessorReads);
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_RuntimeTrackingEnable_SubscribesToWrappedHierarchyItem()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new DataGridTextColumn
        {
            Binding = new Binding("Item.Name"),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            TrackDirectTextValueChanges = false,
            Width = new DataGridLength(160)
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => $"accessor:{((NotifyItem)value.Item).Name}"));
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            ItemsSource = new[] { node },
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 280, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.Single(GetRealizedRows(grid));
            var cell = Assert.IsType<DataGridCustomDrawingCell>(row.Cells[0]);
            Assert.True(cell.UsesValueProvider);
            Assert.Equal("accessor:First", cell.Value);

            item.Name = "Ignored";
            Assert.Equal("accessor:First", cell.Value);

            column.TrackDirectTextValueChanges = true;
            Assert.Equal("accessor:Ignored", cell.Value);

            item.Name = "Tracked";
            Assert.Equal("accessor:Tracked", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_DirectHierarchyMember_TracksNodeButNotWrappedItem()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new TestTextColumn
        {
            Binding = new Binding(nameof(HierarchicalNode.Level)),
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        int accessorReads = 0;
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(value =>
            {
                accessorReads++;
                return $"accessor:{value.Level}";
            }));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = node;
        Assert.Null(column.GenerateDisplay(cell, node));
        Assert.True(cell.UsesValueProvider);
        Assert.Equal("accessor:0", cell.Value);

        var window = AttachCell(cell);
        try
        {
            int readsAfterAttach = accessorReads;

            item.Name = "Wrapped change";

            Assert.Equal(readsAfterAttach, accessorReads);
            Assert.Equal("accessor:0", cell.Value);

            node.Level = 1;

            Assert.Equal(readsAfterAttach + 1, accessorReads);
            Assert.Equal("accessor:1", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnText_NestedHierarchyPath_UsesBindingAndTracksLeaf_WhenDirectTrackingDisabled()
    {
        var address = new NotifyAddress("First");
        var node = new HierarchicalNode(new AddressItem(address), isLeaf: true);
        var column = new TestTextColumn
        {
            Binding = new Binding("Item.Address.City"),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            TrackDirectTextValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(
                value => $"accessor:{((AddressItem)value.Item).Address.City}"));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = node;
        Assert.Null(column.GenerateDisplay(cell, node));

        var window = AttachCell(cell);
        try
        {
            Assert.False(cell.UsesValueProvider);
            Assert.Equal("First", cell.Value);

            address.City = "Second";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Second", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CustomDrawingCell_Can_Use_Typed_Accessor_Without_Change_Subscription()
    {
        var item = new NotifyItem("First");
        var column = new TestCustomDrawingColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectValueAccessor = true,
            TrackDirectValueChanges = false
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));
        Assert.Equal("First", cell.Value);

        item.Name = "Second";
        Assert.Equal("First", cell.Value);

        cell.DataContext = new NotifyItem("Third");
        Assert.Equal("Third", cell.Value);
    }

    [AvaloniaFact]
    public void CustomDrawingCell_TracksHierarchyNodeButNotWrappedItem()
    {
        var item = new NotifyItem("First");
        var node = new HierarchicalNode(item, isLeaf: true);
        var column = new TestCustomDrawingColumn
        {
            Binding = new Binding(nameof(HierarchicalNode.Level)),
            UseDirectValueAccessor = true,
            TrackDirectValueChanges = true
        };
        int accessorReads = 0;
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<HierarchicalNode, string>(value =>
            {
                accessorReads++;
                return $"provider:{value.Level}";
            }));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = node;
        Assert.Null(column.GenerateDisplay(cell, node));
        Assert.True(cell.UsesValueProvider);
        Assert.Equal("provider:0", cell.Value);

        var window = AttachCell(cell);
        try
        {
            int readsAfterAttach = accessorReads;

            item.Name = "Wrapped change";

            Assert.Equal(readsAfterAttach, accessorReads);
            Assert.Equal("provider:0", cell.Value);

            node.Level = 1;

            Assert.Equal(readsAfterAttach + 1, accessorReads);
            Assert.Equal("provider:1", cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DrawnNumeric_UsesFormattedTypedAccessor()
    {
        var item = new NumericItem(12.5m);
        var column = new TestNumericColumn
        {
            Binding = new Binding(nameof(NumericItem.Value)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            FormatString = "N1"
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NumericItem, decimal>(value => value.Value));

        var cell = Assert.IsType<DataGridCustomDrawingCell>(column.CreateCell());
        cell.DataContext = item;
        Assert.Null(column.GenerateDisplay(cell, item));

        Assert.Equal(12.5m.ToString("N1", column.NumberFormat ?? System.Globalization.CultureInfo.CurrentCulture.NumberFormat), cell.Value);
    }

    [AvaloniaFact]
    public void DrawnBuiltInColumns_FallBackToBinding_WhenConverterChangesValueSemantics()
    {
        var converter = new PrefixConverter();
        var numeric = new TestNumericColumn
        {
            Binding = new Binding(nameof(NumericItem.Value)) { Converter = converter },
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        var progress = new TestProgressColumn
        {
            Binding = new Binding(nameof(NumericItem.Value)) { Converter = converter },
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };
        var image = new TestImageColumn
        {
            Binding = new Binding(nameof(ImageItem.Image)) { Converter = converter },
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            ImageWidth = 16,
            ImageHeight = 16
        };
        DataGridColumnMetadata.SetValueAccessor(
            numeric,
            new DataGridColumnValueAccessor<NumericItem, decimal>(value => value.Value));
        DataGridColumnMetadata.SetValueAccessor(
            progress,
            new DataGridColumnValueAccessor<NumericItem, decimal>(value => value.Value));
        DataGridColumnMetadata.SetValueAccessor(
            image,
            new DataGridColumnValueAccessor<ImageItem, IImage?>(value => value.Image));

        var numericCell = Assert.IsType<DataGridCustomDrawingCell>(numeric.CreateCell());
        numericCell.DataContext = new NumericItem(12.5m);
        Assert.Null(numeric.GenerateDisplay(numericCell, numericCell.DataContext!));

        var progressCell = Assert.IsType<DataGridCustomDrawingCell>(progress.CreateCell());
        progressCell.DataContext = new NumericItem(25m);
        Assert.Null(progress.GenerateDisplay(progressCell, progressCell.DataContext!));

        var imageCell = Assert.IsType<DataGridCustomDrawingCell>(image.CreateCell());
        imageCell.DataContext = new ImageItem(null);
        Assert.Null(image.GenerateDisplay(imageCell, imageCell.DataContext!));
        Dispatcher.UIThread.RunJobs();

        Assert.False(numericCell.UsesValueProvider);
        Assert.False(progressCell.UsesValueProvider);
        Assert.False(imageCell.UsesValueProvider);
        Assert.Equal(
            converter.Convert(12.5m, typeof(object), null, CultureInfo.CurrentCulture),
            numericCell.Value);
        Assert.Equal(
            converter.Convert(25m, typeof(object), null, CultureInfo.CurrentCulture),
            progressCell.Value);
        Assert.Equal(
            converter.Convert(null, typeof(object), null, CultureInfo.CurrentCulture),
            imageCell.Value);
    }

    [AvaloniaFact]
    public void DrawnProgress_And_Image_UseAllocationFreeBuiltInRenderers()
    {
        var progressColumn = new TestProgressColumn
        {
            Binding = new Binding(nameof(NumericItem.Value)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            Height = 6
        };
        var imageColumn = new TestImageColumn
        {
            Binding = new Binding(nameof(ImageItem.Image)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            ImageWidth = 18,
            ImageHeight = 12
        };

        var progressCell = Assert.IsType<DataGridCustomDrawingCell>(progressColumn.CreateCell());
        var imageCell = Assert.IsType<DataGridCustomDrawingCell>(imageColumn.CreateCell());
        progressCell.OwningColumn = progressColumn;
        imageCell.OwningColumn = imageColumn;
        Assert.Null(progressColumn.GenerateDisplay(progressCell, new NumericItem(50m)));
        Assert.Null(imageColumn.GenerateDisplay(imageCell, new ImageItem(null)));

        progressCell.Measure(new Size(100, 24));
        imageCell.Measure(new Size(100, 24));

        Assert.Equal(6, progressCell.DesiredSize.Height);
        Assert.Equal(new Size(18, 12), imageCell.DesiredSize);
        Assert.Same(DataGridProgressCellRenderer.Instance, progressCell.BuiltInRendererForTesting);
        Assert.Same(DataGridImageCellRenderer.Instance, imageCell.BuiltInRendererForTesting);
    }

    [AvaloniaFact]
    public void DrawnCells_IncludePaddingInVariableHeightMeasurement()
    {
        var emptyText = new DataGridCustomDrawingCell
        {
            Value = string.Empty,
            Padding = new Thickness(3, 4, 5, 6)
        };
        emptyText.Measure(Size.Infinity);

        var progressColumn = new TestProgressColumn { Height = 6 };
        var progressCell = new DataGridCustomDrawingCell
        {
            OwningColumn = progressColumn,
            Padding = new Thickness(3, 4, 5, 6)
        };
        progressCell.ConfigureBuiltInRenderer(valueProvider: null, DataGridProgressCellRenderer.Instance);
        progressCell.Measure(Size.Infinity);

        var imageColumn = new TestImageColumn { ImageWidth = 18, ImageHeight = 12 };
        var imageCell = new DataGridCustomDrawingCell
        {
            OwningColumn = imageColumn,
            Padding = new Thickness(3, 4, 5, 6)
        };
        imageCell.ConfigureBuiltInRenderer(valueProvider: null, DataGridImageCellRenderer.Instance);
        imageCell.Measure(Size.Infinity);

        progressCell.Arrange(new Rect(0, 0, 100, 30));

        Assert.Equal(new Size(8, 10), emptyText.DesiredSize);
        Assert.Equal(new Size(8, 16), progressCell.DesiredSize);
        Assert.Equal(new Size(26, 22), imageCell.DesiredSize);
        Assert.Equal(
            new Rect(3, 11, 92, 6),
            DataGridProgressCellRenderer.GetBarBounds(progressCell, progressColumn));
    }

    [AvaloniaFact]
    public void DrawnDisplay_Still_Uses_Retained_Editors()
    {
        var text = new TestTextColumn { DisplayMode = DataGridColumnDisplayMode.Drawn };
        var numeric = new TestNumericColumn { DisplayMode = DataGridColumnDisplayMode.Drawn };
        var image = new TestImageColumn
        {
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            ImageWidth = 16,
            ImageHeight = 16,
            AllowEditing = true
        };

        Assert.IsType<TextBox>(text.GenerateEditor(new DataGridCustomDrawingCell(), new object()));
        Assert.IsType<NumericUpDown>(numeric.GenerateEditor(new DataGridCustomDrawingCell(), new object()));
        Assert.IsType<TextBox>(image.GenerateEditor(new DataGridCustomDrawingCell(), new object()));
    }

    [Fact]
    public void DrawnCell_AutomationName_Uses_DisplayValue()
    {
        var cell = new DataGridCustomDrawingCell { Value = "Accessible value" };
        var peer = new DataGridCellAutomationPeer(cell);

        Assert.Equal("Accessible value", peer.GetName());
    }

    [Fact]
    public void DirectCells_AutomationName_Uses_DisplayValue()
    {
        var textPeer = new DataGridCellAutomationPeer(
            new DataGridDirectTextCell { Value = "Direct text" });
        var hierarchyPeer = new DataGridCellAutomationPeer(
            new DataGridDirectHierarchicalCell { Value = "Hierarchy text" });

        Assert.Equal("Direct text", textPeer.GetName());
        Assert.Equal("Hierarchy text", hierarchyPeer.GetName());
    }

    [Fact]
    public void ColumnDefinition_Applies_DrawMode()
    {
        var definition = new DataGridTextColumnDefinition
        {
            DisplayMode = DataGridColumnDisplayMode.Drawn
        };

        var column = definition.CreateColumn(new DataGridColumnDefinitionContext(new DataGrid()));

        Assert.Equal(DataGridColumnDisplayMode.Drawn, column.DisplayMode);
    }

    [Fact]
    public void HierarchicalColumnDefinition_Applies_Retained_Optimization_Options()
    {
        var definition = new DataGridHierarchicalColumnDefinition
        {
            UseDirectCell = false,
            UseDirectTextContent = true,
            UseOptimizedPresenter = true,
            TrackDirectTextValueChanges = false,
        };

        var column = Assert.IsType<DataGridHierarchicalColumn>(
            definition.CreateColumn(new DataGridColumnDefinitionContext(new DataGrid())));

        Assert.False(column.UseDirectCell);
        Assert.True(column.UseDirectTextContent);
        Assert.True(column.UseOptimizedPresenter);
        Assert.False(column.TrackDirectTextValueChanges);
    }

    [AvaloniaFact]
    public void OrdinaryDrawnColumns_Recycle_Select_And_UseRetainedEditor()
    {
        var items = Enumerable.Range(0, 160)
            .Select(index => new DrawnItem($"Item {index}", index))
            .ToList();
        var textColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(DrawnItem.Name)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            Width = new DataGridLength(160)
        };
        var numericColumn = new DataGridNumericColumn
        {
            Header = "Number",
            Binding = new Binding(nameof(DrawnItem.Number)),
            DisplayMode = DataGridColumnDisplayMode.Drawn,
            Width = new DataGridLength(100)
        };
        DataGridColumnMetadata.SetValueAccessor(
            textColumn,
            new DataGridColumnValueAccessor<DrawnItem, string>(item => item.Name, (item, value) => item.Name = value));
        DataGridColumnMetadata.SetValueAccessor(
            numericColumn,
            new DataGridColumnValueAccessor<DrawnItem, decimal>(item => item.Number, (item, value) => item.Number = value));

        var grid = new DataGrid
        {
            Width = 360,
            Height = 180,
            RowHeight = 24,
            ItemsSource = items,
            UseLogicalScrollable = true,
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(textColumn);
        grid.ColumnsInternal.Add(numericColumn);

        var window = new Window { Width = 400, Height = 220 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var firstRow = Assert.IsType<DataGridRow>(grid.GetRowFromItem(items[0]));
            for (var columnIndex = 0; columnIndex < 2; columnIndex++)
            {
                var cell = firstRow.Cells[columnIndex];
                Assert.IsType<DataGridCustomDrawingCell>(cell);
                Assert.Null(cell.Content);
            }

            var slot = grid.SlotFromRowIndex(0);
            grid.UpdateSelectionAndCurrency(0, slot, DataGridSelectionAction.SelectCurrent, scrollIntoView: false);
            Assert.True(grid.BeginEdit());
            Assert.IsType<TextBox>(firstRow.Cells[0].Content);
            Assert.True(grid.CommitEdit());
            Assert.Null(firstRow.Cells[0].Content);

            var originalCells = grid.GetVisualDescendants().OfType<DataGridCustomDrawingCell>().ToHashSet();
            grid.ScrollIntoView(items[^1], numericColumn);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var recycledCells = grid.GetVisualDescendants().OfType<DataGridCustomDrawingCell>().ToList();
            Assert.Contains(recycledCells, originalCells.Contains);
            Assert.All(recycledCells, cell => Assert.Null(cell.Content));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DisplayMode_Change_Recreates_Realized_Cells()
    {
        var item = new DrawnItem("Item", 1);
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(DrawnItem.Name)),
            Width = new DataGridLength(160)
        };
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            ItemsSource = new[] { item },
            UseLogicalScrollable = true,
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 280, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.Single(GetRealizedRows(grid));
            Assert.IsAssignableFrom<TextBlock>(row.Cells[0].Content);
            var retainedCell = row.Cells[0];
            var clearing = new List<DataGridCell>();
            var prepared = new List<DataGridCell>();
            grid.CellClearing += (_, args) => clearing.Add(args.Cell);
            grid.CellPrepared += (_, args) => prepared.Add(args.Cell);
            grid.UpdateSelectionAndCurrency(
                columnIndex: 0,
                slot: grid.SlotFromRowIndex(0),
                action: DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false);

            column.DisplayMode = DataGridColumnDisplayMode.Drawn;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            row = Assert.Single(GetRealizedRows(grid));
            Assert.IsType<DataGridCustomDrawingCell>(row.Cells[0]);
            Assert.Null(row.Cells[0].Content);
            Assert.Equal(0, grid.CurrentColumnIndex);
            Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
            Assert.Equal(new[] { retainedCell }, clearing);
            Assert.Equal(new[] { row.Cells[0] }, prepared);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OptimizedRetainedHierarchy_ModeChange_And_Editing_Preserve_StandardPaths()
    {
        var item = new NotifyItem("Root");
        var column = new DataGridHierarchicalColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            IsReadOnly = false,
            UseDirectCell = false,
            UseDirectTextContent = true,
            UseOptimizedPresenter = false,
            Width = new DataGridLength(180),
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            ItemsSource = new[] { item },
            UseLogicalScrollable = true,
            AutoGenerateColumns = false,
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 280, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            DataGridRow row = Assert.Single(GetRealizedRows(grid));
            DataGridCell standardCell = row.Cells[0];
            Assert.IsNotType<DataGridDirectHierarchicalCell>(standardCell);
            var cleared = new List<DataGridCell>();
            grid.CellClearing += (_, args) => cleared.Add(args.Cell);
            grid.UpdateSelectionAndCurrency(
                columnIndex: 0,
                slot: grid.SlotFromRowIndex(0),
                action: DataGridSelectionAction.SelectCurrent,
                scrollIntoView: false);

            column.UseOptimizedPresenter = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            row = Assert.Single(GetRealizedRows(grid));
            var optimizedCell = Assert.IsType<DataGridDirectHierarchicalCell>(row.Cells[0]);
            Assert.Contains(standardCell, cleared);
            Assert.Equal(0, grid.CurrentColumnIndex);
            Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
            Assert.Equal("Root", optimizedCell.Value);

            Assert.True(grid.BeginEdit());
            Assert.IsType<DataGridHierarchicalPresenter>(optimizedCell.Content);
            Assert.Equal(typeof(DataGridCell), Assert.IsType<ControlTheme>(optimizedCell.Theme).TargetType);

            grid.CancelEdit();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            Assert.Null(optimizedCell.Content);
            Assert.Equal("Root", optimizedCell.Value);
            Assert.Single(optimizedCell.GetVisualDescendants().OfType<TextBlock>());

            column.UseOptimizedPresenter = false;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            row = Assert.Single(GetRealizedRows(grid));
            Assert.IsNotType<DataGridDirectHierarchicalCell>(row.Cells[0]);
            Assert.Equal(0, grid.CurrentColumnIndex);
            Assert.Equal(grid.SlotFromRowIndex(0), grid.CurrentSlot);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CoalescedColumnPropertyChanges_RefreshRealizedCellContainer()
    {
        var item = new NotifyItem("First");
        var column = new DataGridCustomDrawingColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            FontSize = 12
        };
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            ItemsSource = new[] { item },
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 280, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.Single(GetRealizedRows(grid));
            var cell = Assert.IsType<DataGridCustomDrawingCell>(row.Cells[0]);

            column.FontSize = 22;
            column.InvalidateCustomDrawingCells(invalidateMeasure: true);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(22, cell.FontSize);
            Assert.Equal(column.RenderInvalidationToken, cell.RenderInvalidationToken);
            Assert.Equal(column.LayoutInvalidationToken, cell.LayoutInvalidationToken);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DirectTextModeAndTrackingChanges_ReconfigureRealizedCells()
    {
        var item = new NotifyItem("First");
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            Width = new DataGridLength(160)
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 24,
            ItemsSource = new[] { item },
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 280, Height = 160 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.Single(GetRealizedRows(grid));
            Assert.IsNotType<DataGridDirectTextCell>(row.Cells[0]);

            column.UseDirectTextCell = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            row = Assert.Single(GetRealizedRows(grid));
            var directCell = Assert.IsType<DataGridDirectTextCell>(row.Cells[0]);
            Assert.Equal("First", directCell.Value);

            column.TrackDirectTextValueChanges = false;
            item.Name = "Second";
            Assert.Equal("First", directCell.Value);

            column.TrackDirectTextValueChanges = true;
            item.Name = "Third";
            Assert.Equal("Third", directCell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void LightweightFiller_AvoidsFillerCellCreation(bool useLightweightFiller, bool expectsFiller)
    {
        var grid = new DataGrid
        {
            Width = 500,
            Height = 160,
            ItemsSource = new[] { new NotifyItem("First") },
            UseLightweightFiller = useLightweightFiller,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column
        };
        grid.ColumnsInternal.Add(new DataGridTextColumn
        {
            Width = new DataGridLength(100),
            Binding = new Binding(nameof(NotifyItem.Name))
        });

        var window = new Window { Width = 500, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.Single(grid.GetVisualDescendants().OfType<DataGridRow>());
            Assert.Equal(expectsFiller, row.ExistingFillerCell != null);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OptimizedUnfrozenRowTheme_Preserves_Retained_Cells_And_Horizontal_Scrolling()
    {
        var items = Enumerable.Range(0, 20).Select(index => new NotifyItem($"Item {index}")).ToList();
        var grid = new DataGrid
        {
            Width = 280,
            Height = 160,
            RowHeight = 24,
            ItemsSource = items,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = true,
            UseLightweightFiller = true
        };
        for (var index = 0; index < 4; index++)
        {
            grid.ColumnsInternal.Add(new DataGridTextColumn
            {
                Header = $"Column {index}",
                Binding = new Binding(nameof(NotifyItem.Name)),
                Width = new DataGridLength(180)
            });
        }

        var window = new Window { Width = 320, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            Assert.True(grid.TryFindResource("DataGridOptimizedUnfrozenRowTheme", out var rowTheme));
            grid.RowTheme = Assert.IsType<ControlTheme>(rowTheme);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.IsType<DataGridRow>(grid.GetRowFromItem(items[0]));
            Assert.Empty(row.GetVisualDescendants().OfType<DataGridFrozenGrid>());
            Assert.Single(row.GetVisualDescendants().OfType<DataGridCellsPresenter>());
            Assert.Equal(4, row.Cells.Count);
            foreach (DataGridCell cell in row.Cells)
            {
                Assert.NotNull(cell.Content);
            }

            var scrollViewer = grid.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .First(viewer => viewer.Name == "PART_ScrollViewer");
            scrollViewer.Offset = new Vector(120, scrollViewer.Offset.Y);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            Assert.True(scrollViewer.Offset.X > 0);
            Assert.Equal(4, row.Cells.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("DataGridOptimizedFeatureRowTheme", DataGridTheme.FluentV2)]
    [InlineData("DataGridOptimizedFeatureUnfrozenRowTheme", DataGridTheme.FluentV2)]
    [InlineData("DataGridOptimizedFeatureRowTheme", DataGridTheme.SimpleV2)]
    [InlineData("DataGridOptimizedFeatureUnfrozenRowTheme", DataGridTheme.SimpleV2)]
    public void OptimizedFeatureThemes_Preserve_RowDetails_Headers_And_HeaderAffordances(
        string rowThemeKey,
        DataGridTheme theme)
    {
        var items = new[] { new NotifyItem("First"), new NotifyItem("Second") };
        var column = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding(nameof(NotifyItem.Name)),
            SortMemberPath = nameof(NotifyItem.Name),
            ShowFilterButton = true,
            Width = new DataGridLength(180)
        };
        var grid = new DataGrid
        {
            Width = 360,
            Height = 220,
            ItemsSource = items,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.All,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Visible,
            RowDetailsTemplate = new FuncDataTemplate<NotifyItem>(
                static (item, _) => new TextBlock
                {
                    Name = "FeatureDetailsText",
                    Text = $"Details: {item.Name}"
                }),
            CanUserSortColumns = true,
            CanUserResizeColumns = true
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 400, Height = 260 };
        window.SetThemeStyles(theme);
        window.Content = grid;
        try
        {
            Assert.True(grid.TryFindResource(rowThemeKey, out object? rowTheme));
            Assert.True(grid.TryFindResource("DataGridOptimizedFeatureColumnHeaderTheme", out object? headerTheme));
            grid.RowTheme = Assert.IsType<ControlTheme>(rowTheme);
            grid.ColumnHeaderTheme = Assert.IsType<ControlTheme>(headerTheme);

            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            DataGridRow row = Assert.IsType<DataGridRow>(grid.GetRowFromItem(items[0]));
            Assert.Single(row.GetVisualDescendants().OfType<DataGridRowHeader>());
            DataGridDetailsPresenter details = Assert.Single(
                row.GetVisualDescendants().OfType<DataGridDetailsPresenter>());
            Assert.True(details.IsVisible);
            Assert.Equal(
                "Details: First",
                Assert.Single(details.GetVisualDescendants().OfType<TextBlock>()).Text);
            Assert.Contains(
                row.GetVisualDescendants().OfType<Rectangle>(),
                rectangle => rectangle.Name == "PART_BottomGridLine");

            DataGridColumnHeader header = grid.GetVisualDescendants()
                .OfType<DataGridColumnHeader>()
                .First(candidate => ReferenceEquals(candidate.OwningColumn, column));
            Button filterButton = header.GetVisualDescendants()
                .OfType<Button>()
                .First(button => button.Name == "PART_FilterButton");
            Path sortIcon = header.GetVisualDescendants()
                .OfType<Path>()
                .First(path => path.Name == "SortIcon");
            Rectangle separator = header.GetVisualDescendants()
                .OfType<Rectangle>()
                .First(rectangle => rectangle.Name == "VerticalSeparator");

            Assert.True(filterButton.IsVisible);
            Assert.True(separator.IsVisible);

            column.SortDirection = ListSortDirection.Ascending;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            Assert.True(sortIcon.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RecycledRetainedCells_UpdateIndexerBindingWithoutRegeneration()
    {
        var items = Enumerable.Range(0, 200)
            .Select(index => new IndexedItem($"Value {index}"))
            .ToList();
        var column = new DataGridTextColumn
        {
            Header = "Value",
            Width = new DataGridLength(180),
            Binding = new Binding("Fields[0]")
        };
        var grid = new DataGrid
        {
            Width = 320,
            Height = 160,
            RowHeight = 24,
            ItemsSource = items,
            UseLogicalScrollable = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 320, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            var originalCells = grid.GetVisualDescendants().OfType<DataGridCell>().ToHashSet();

            grid.ScrollIntoView(items[^1], column);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var displayedRows = grid.GetVisualDescendants()
                .OfType<DataGridRow>()
                .Where(row => row.DataContext is IndexedItem)
                .ToList();
            Assert.NotEmpty(displayedRows);
            Assert.Contains(
                displayedRows.SelectMany(row => row.GetVisualDescendants().OfType<DataGridCell>()),
                originalCells.Contains);
            foreach (var row in displayedRows)
            {
                var item = Assert.IsType<IndexedItem>(row.DataContext);
                var text = Assert.Single(row.GetVisualDescendants().OfType<TextBlock>());
                Assert.Equal(item.Fields[0], text.Text);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void HeterogeneousDirectCells_FallBackToBindingWhenAccessorTypeChangesOnRecycle()
    {
        var items = Enumerable.Range(0, 200)
            .Select(index => index < 100
                ? (object)new NotifyItem($"Typed {index}")
                : new AlternateNotifyItem($"Alternate {index}"))
            .ToList();
        var column = new DataGridTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextCell = true,
            Width = new DataGridLength(180)
        };
        DataGridColumnMetadata.SetValueAccessor(
            column,
            new DataGridColumnValueAccessor<NotifyItem, string>(value => value.Name));
        var grid = new DataGrid
        {
            Width = 320,
            Height = 160,
            RowHeight = 24,
            ItemsSource = items,
            UseLogicalScrollable = true,
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 320, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            var originalCells = GetRealizedRows(grid)
                .Select(row => Assert.IsType<DataGridDirectTextCell>(row.Cells[0]))
                .ToHashSet();

            grid.ScrollIntoView(items[^1], column);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var alternateRows = GetRealizedRows(grid)
                .Where(row => row.DataContext is AlternateNotifyItem)
                .ToList();
            Assert.NotEmpty(alternateRows);
            var row = Assert.IsType<DataGridRow>(alternateRows.FirstOrDefault(
                row => originalCells.Contains(Assert.IsType<DataGridDirectTextCell>(row.Cells[0]))));
            var cell = Assert.IsType<DataGridDirectTextCell>(row.Cells[0]);
            Assert.Contains(cell, originalCells);
            var item = Assert.IsType<AlternateNotifyItem>(cell.DataContext);
            Assert.False(cell.UsesValueAccessor);
            Assert.Equal(item.Name, cell.Value);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DerivedDataDependentColumn_RegeneratesContentWhenRowIsRecycled()
    {
        var items = Enumerable.Range(0, 200)
            .Select(index => new NotifyItem($"Item {index}"))
            .ToList();
        var column = new DataDependentTextColumn { Width = new DataGridLength(180) };
        var grid = new DataGrid
        {
            Width = 320,
            Height = 160,
            RowHeight = 24,
            ItemsSource = items,
            UseLogicalScrollable = true,
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(column);

        var window = new Window { Width = 320, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            var initialCalls = column.GenerateCalls;
            var initialRows = GetRealizedRows(grid);
            var initialMaximumIndex = initialRows.Max(row => row.Index);
            var originalCells = initialRows.Select(row => row.Cells[0]).ToHashSet();

            grid.ScrollIntoView(items[^1], column);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.IsType<DataGridRow>(GetRealizedRows(grid).FirstOrDefault(
                row => originalCells.Contains(row.Cells[0])));
            Assert.True(row.Index > initialMaximumIndex);
            Assert.Contains(row.Cells[0], originalCells);
            Assert.True(column.GenerateCalls > initialCalls);
            var item = Assert.IsType<NotifyItem>(row.DataContext);
            var text = Assert.IsType<TextBlock>(row.Cells[0].Content);
            Assert.Equal(item.Name, text.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DerivedCoalescedColumns_RegenerateContentWhenRowsAreRecycled()
    {
        var items = Enumerable.Range(0, 200)
            .Select(index => new NotifyItem($"Item {index}"))
            .ToList();
        var directColumn = new DerivedDirectTextColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectTextCell = true,
            Width = new DataGridLength(140)
        };
        var drawnColumn = new DerivedCustomDrawingColumn
        {
            Binding = new Binding(nameof(NotifyItem.Name)),
            UseDirectValueAccessor = true,
            Width = new DataGridLength(140)
        };
        DataGridColumnMetadata.SetValueAccessor(
            directColumn,
            new DataGridColumnValueAccessor<NotifyItem, string>(item => item.Name));
        DataGridColumnMetadata.SetValueAccessor(
            drawnColumn,
            new DataGridColumnValueAccessor<NotifyItem, string>(item => item.Name));

        var grid = new DataGrid
        {
            Width = 320,
            Height = 160,
            RowHeight = 24,
            ItemsSource = items,
            UseLogicalScrollable = true,
            AutoGenerateColumns = false
        };
        grid.ColumnsInternal.Add(directColumn);
        grid.ColumnsInternal.Add(drawnColumn);

        var window = new Window { Width = 320, Height = 200 };
        window.SetThemeStyles(DataGridTheme.FluentV2);
        window.Content = grid;
        try
        {
            window.Show();
            grid.ApplyTemplate();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();
            var initialDirectCalls = directColumn.GenerateCalls;
            var initialDrawnCalls = drawnColumn.GenerateCalls;
            var initialRows = GetRealizedRows(grid);
            var initialMaximumIndex = initialRows.Max(row => row.Index);
            var originalCells = initialRows
                .SelectMany(row => Enumerable.Range(0, row.Cells.Count).Select(index => row.Cells[index]))
                .ToHashSet();

            grid.ScrollIntoView(items[^1], directColumn);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            grid.UpdateLayout();

            var row = Assert.IsType<DataGridRow>(GetRealizedRows(grid).FirstOrDefault(
                row => originalCells.Contains(row.Cells[0])));
            Assert.True(row.Index > initialMaximumIndex);
            Assert.Contains(row.Cells[0], originalCells);
            Assert.True(directColumn.GenerateCalls > initialDirectCalls);
            Assert.True(drawnColumn.GenerateCalls > initialDrawnCalls);
            var item = Assert.IsType<NotifyItem>(row.DataContext);
            Assert.Equal(item.Name, Assert.IsType<DataGridDirectTextCell>(row.Cells[0]).Value);
            Assert.Equal(item.Name, Assert.IsType<DataGridCustomDrawingCell>(row.Cells[1]).Value);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window AttachCell(Control cell)
    {
        var window = new Window { Width = 240, Height = 80, Content = cell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static List<DataGridRow> GetRealizedRows(DataGrid grid)
    {
        return grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .Where(row => row.IsVisible && row.DataContext != null && !row.IsPlaceholder)
            .ToList();
    }

    private sealed class NotifyItem : INotifyPropertyChanged
    {
        private string _name;

        public NotifyItem(string name) => _name = name;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record AddressItem(NotifyAddress Address);

    private sealed class NotifyAddress : INotifyPropertyChanged
    {
        private string _city;

        public NotifyAddress(string city) => _city = city;

        public string City
        {
            get => _city;
            set
            {
                if (_city == value)
                {
                    return;
                }

                _city = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class SelfWrappedNotifyItem : INotifyPropertyChanged, IHierarchicalNodeItem
    {
        private readonly HierarchicalNode _node;
        private string _name;

        public SelfWrappedNotifyItem(string name)
        {
            _name = name;
            _node = new HierarchicalNode(this, isLeaf: true);
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        object IHierarchicalNodeItem.Item => this;

        HierarchicalNode IHierarchicalNodeItem.Node => _node;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class IndexedItem
    {
        public IndexedItem(string value) => Fields = new List<string> { value };

        public List<string> Fields { get; }
    }

    private sealed record NullableTextItem(string? Name);

    private sealed record AlternateNotifyItem(string Name);

    private sealed record NumericItem(decimal Value);

    private sealed record ImageItem(IImage? Image);

    private sealed class PrefixConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            $"converted:{value}";

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class DrawnItem : INotifyPropertyChanged
    {
        private string _name;
        private decimal _number;

        public DrawnItem(string name, decimal number)
        {
            _name = name;
            _number = number;
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public decimal Number
        {
            get => _number;
            set
            {
                if (_number == value)
                {
                    return;
                }

                _number = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class TestTextColumn : DataGridTextColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) => GenerateElement(cell, item);

        public Control GenerateEditor(DataGridCell cell, object item) => GenerateEditingElementDirect(cell, item);
    }

    private sealed class TestNumericColumn : DataGridNumericColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) => GenerateElement(cell, item);

        public Control GenerateEditor(DataGridCell cell, object item) => GenerateEditingElementDirect(cell, item);
    }

    private sealed class TestProgressColumn : DataGridProgressBarColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) => GenerateElement(cell, item);
    }

    private sealed class TestImageColumn : DataGridImageColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) => GenerateElement(cell, item);

        public Control GenerateEditor(DataGridCell cell, object item) => GenerateEditingElementDirect(cell, item);
    }

    private sealed class TestCustomDrawingColumn : DataGridCustomDrawingColumn
    {
        public Control? GenerateDisplay(DataGridCell cell, object item) => GenerateElement(cell, item);
    }

    private sealed class DataDependentTextColumn : DataGridTextColumn
    {
        public int GenerateCalls { get; private set; }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            GenerateCalls++;
            return new TextBlock { Text = ((NotifyItem)dataItem).Name };
        }
    }

    private sealed class DerivedDirectTextColumn : DataGridTextColumn
    {
        public int GenerateCalls { get; private set; }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            GenerateCalls++;
            return base.GenerateElement(cell, dataItem);
        }
    }

    private sealed class DerivedCustomDrawingColumn : DataGridCustomDrawingColumn
    {
        public int GenerateCalls { get; private set; }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            GenerateCalls++;
            return base.GenerateElement(cell, dataItem);
        }
    }
}
