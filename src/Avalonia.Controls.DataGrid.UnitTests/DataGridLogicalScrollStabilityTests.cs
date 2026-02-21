// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Avalonia.Controls.DataGridTests;

public class DataGridLogicalScrollStabilityTests
{
    [AvaloniaFact]
    public void LogicalScrollable_ReverseScroll_WithPendingOffset_IsHandled()
    {
        var target = CreateStandaloneTarget(itemCount: 400, height: 160, useLogicalScrollable: true);
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        presenter.Offset = new Vector(0, 180);

        var pendingBefore = target.DisplayData.PendingVerticalScrollHeight;
        Assert.True(pendingBefore > 0, $"Expected pending vertical delta > 0, got {pendingBefore}.");

        var handled = target.UpdateScroll(new Vector(0, 24));

        Assert.True(handled, "Reverse (upward) scroll should remain handled when presenter offset is ahead of internal offset.");
        Assert.True(target.DisplayData.PendingVerticalScrollHeight < pendingBefore,
            $"Expected pending vertical delta to decrease after reverse input. Before: {pendingBefore}, After: {target.DisplayData.PendingVerticalScrollHeight}");
    }

    [AvaloniaFact]
    public void LogicalScrollable_PendingDelta_IsClamped_ToLogicalMax()
    {
        var target = CreateStandaloneTarget(itemCount: 400, height: 160, useLogicalScrollable: true);
        target.UpdateLayout();

        var presenter = GetRowsPresenter(target);
        var logicalMaximum = Math.Max(0, presenter.Extent.Height - presenter.Viewport.Height);
        Assert.True(logicalMaximum > 0, $"Expected positive logical maximum. Extent: {presenter.Extent.Height}, Viewport: {presenter.Viewport.Height}");

        var nearBottom = Math.Max(0, logicalMaximum - 4);
        presenter.Offset = new Vector(0, nearBottom);

        var pendingBefore = target.DisplayData.PendingVerticalScrollHeight;
        var handled = target.UpdateScroll(new Vector(0, -30));
        var pendingAfter = target.DisplayData.PendingVerticalScrollHeight;
        var pendingAdded = pendingAfter - pendingBefore;
        var expectedMaxDelta = Math.Max(0, logicalMaximum - nearBottom);

        Assert.True(handled, "Downward scroll near max should still be handled up to the remaining logical range.");
        Assert.InRange(pendingAdded, 0, expectedMaxDelta + 0.01);
        Assert.True(pendingAfter <= logicalMaximum + 0.01,
            $"Pending delta exceeded logical max. Pending: {pendingAfter}, LogicalMax: {logicalMaximum}");
    }

    [AvaloniaFact]
    public void LogicalScrollable_Wheel_RemainsResponsive_InDeepTabbedHost()
    {
        var (root, target) = CreateDeepHostedTarget(itemCount: 400);
        root.UpdateLayout();
        target.UpdateLayout();

        var topLevel = (TopLevel)target.GetVisualRoot()!;
        var wheelPoint = target.TranslatePoint(
            new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
            topLevel)!.Value;

        topLevel.MouseWheel(wheelPoint, new Vector(0, -3));
        root.UpdateLayout();
        target.UpdateLayout();

        var baseline = GetFirstVisibleRowIndex(target);
        Assert.True(baseline > 0, $"Expected baseline index > 0 after initial wheel-down, got {baseline}.");

        for (int i = 0; i < 6; i++)
        {
            topLevel.MouseWheel(wheelPoint, new Vector(0, -3));
            topLevel.MouseWheel(wheelPoint, new Vector(0, 3));
        }

        root.UpdateLayout();
        target.UpdateLayout();

        var afterAlternating = GetFirstVisibleRowIndex(target);
        Assert.InRange(afterAlternating, Math.Max(0, baseline - 2), baseline + 2);
    }

    private static DataGrid CreateStandaloneTarget(int itemCount, int height, bool useLogicalScrollable)
    {
        var root = new Window
        {
            Width = 700,
            Height = height
        };

        root.SetThemeStyles();

        var target = CreateGrid(itemCount, useLogicalScrollable);
        root.Content = target;
        root.Show();
        return target;
    }

    private static (Window Root, DataGrid Grid) CreateDeepHostedTarget(int itemCount)
    {
        var root = new Window
        {
            Width = 1000,
            Height = 700
        };

        root.SetThemeStyles();

        var target = CreateGrid(itemCount, useLogicalScrollable: true);
        target.Height = 320;

        var documentTabs = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = "Document A",
                    Content = new Border
                    {
                        Padding = new Thickness(8),
                        Child = new StackPanel
                        {
                            Spacing = 8,
                            Children =
                            {
                                new TextBlock { Text = "Logical scroll deep host repro" },
                                target
                            }
                        }
                    }
                },
                new TabItem
                {
                    Header = "Document B",
                    Content = new TextBlock { Text = "Placeholder tab" }
                }
            }
        };

        var workspaceTabs = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = "Workspace",
                    Content = new Border
                    {
                        Padding = new Thickness(8),
                        Child = documentTabs
                    }
                },
                new TabItem
                {
                    Header = "Tools",
                    Content = new Border
                    {
                        Padding = new Thickness(8),
                        Child = new StackPanel
                        {
                            Children =
                            {
                                new TextBlock { Text = "Tool window placeholder" },
                                new TextBlock { Text = "Simulates a dock-like host hierarchy." }
                            }
                        }
                    }
                }
            }
        };

        root.Content = new Border
        {
            Padding = new Thickness(8),
            Child = new TabControl
            {
                Items =
                {
                    new TabItem
                    {
                        Header = "Main",
                        Content = new Border
                        {
                            Padding = new Thickness(8),
                            Child = workspaceTabs
                        }
                    },
                    new TabItem
                    {
                        Header = "Secondary",
                        Content = new TextBlock { Text = "Secondary host tab" }
                    }
                }
            }
        };

        root.Show();
        return (root, target);
    }

    private static DataGrid CreateGrid(int itemCount, bool useLogicalScrollable)
    {
        var target = new DataGrid
        {
            ItemsSource = CreateRows(itemCount),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            UseLogicalScrollable = useLogicalScrollable
        };

        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Id", Binding = new Binding(nameof(ScrollStabilityRow.Id)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding(nameof(ScrollStabilityRow.Name)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Category", Binding = new Binding(nameof(ScrollStabilityRow.Category)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding(nameof(ScrollStabilityRow.Status)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Owner", Binding = new Binding(nameof(ScrollStabilityRow.Owner)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Quantity", Binding = new Binding(nameof(ScrollStabilityRow.Quantity)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Price", Binding = new Binding(nameof(ScrollStabilityRow.Price)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Delta", Binding = new Binding(nameof(ScrollStabilityRow.Delta)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Score", Binding = new Binding(nameof(ScrollStabilityRow.Score)) });
        target.ColumnsInternal.Add(new DataGridTextColumn { Header = "Ratio", Binding = new Binding(nameof(ScrollStabilityRow.Ratio)) });

        return target;
    }

    private static List<ScrollStabilityRow> CreateRows(int count)
    {
        var random = new Random(42);
        var categories = new[] { "Alpha", "Beta", "Gamma", "Delta" };
        var statuses = new[] { "New", "Queued", "Running", "Done" };
        var owners = new[] { "Ava", "Ben", "Chen", "Diya", "Eli" };
        var rows = new List<ScrollStabilityRow>(count);

        for (int i = 1; i <= count; i++)
        {
            rows.Add(new ScrollStabilityRow
            {
                Id = i,
                Name = $"Item {i:000}",
                Category = categories[i % categories.Length],
                Status = statuses[i % statuses.Length],
                Owner = owners[i % owners.Length],
                Quantity = random.Next(1, 500),
                Price = Math.Round(random.NextDouble() * 1000, 2),
                Delta = Math.Round(random.NextDouble() * 50 - 25, 2),
                Score = Math.Round(random.NextDouble() * 100, 1),
                Ratio = Math.Round(random.NextDouble() * 10, 3)
            });
        }

        return rows;
    }

    private static DataGridRowsPresenter GetRowsPresenter(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRowsPresenter>()
            .First();
    }

    private static int GetFirstVisibleRowIndex(DataGrid target)
    {
        return target.GetSelfAndVisualDescendants()
            .OfType<DataGridRow>()
            .Min(row => row.Index);
    }

    private sealed class ScrollStabilityRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double Delta { get; set; }
        public double Score { get; set; }
        public double Ratio { get; set; }
    }
}
