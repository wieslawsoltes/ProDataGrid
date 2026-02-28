using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace DataGridSample.Tests;

public sealed class SamplePagesHeadlessTests
{
    [AvaloniaTheory]
    [MemberData(nameof(PageIndexes))]
    public void Page_Constructs_And_Basic_Interaction_Works(int pageIndex, string pageName)
    {
        var (catalogPageName, pageFactory) = SamplePageCatalog.All[pageIndex];
        var page = pageFactory();
        var window = CreateHostWindow(page);

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                PumpLayout(window);
                ExerciseBasicInteractions(page);
            });
        }
        finally
        {
            window.Close();
        }

        Assert.Null(exception);
        Assert.Equal(catalogPageName, pageName);
        Assert.False(string.IsNullOrWhiteSpace(pageName));
    }

    [AvaloniaFact]
    public void MainWindow_Selects_Each_Tab_One_By_One()
    {
        var window = new MainWindow();
        window.ApplySampleTheme();

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                PumpLayout(window);

                var tabs = window.FindControl<TabControl>("SampleTabs");
                Assert.NotNull(tabs);

                for (var tabIndex = 0; tabIndex < tabs.ItemCount; tabIndex++)
                {
                    tabs.SelectedIndex = tabIndex;
                    PumpLayout(window);

                    if (tabs.SelectedContent is Control selectedContent)
                    {
                        ExerciseBasicInteractions(selectedContent);
                    }
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
    public void MainWindow_Filter_Selects_Streaming_Models_Tab_Without_Exception()
    {
        var window = new MainWindow();
        window.ApplySampleTheme();

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                PumpLayout(window);

                var tabs = window.FindControl<TabControl>("SampleTabs");
                var filterBox = window.FindControl<TextBox>("TabFilterBox");
                Assert.NotNull(tabs);
                Assert.NotNull(filterBox);

                filterBox.Text = "Column Definitions - Streaming Models";
                PumpLayout(window);

                Assert.NotNull(tabs.SelectedItem);
                Assert.IsType<TabItem>(tabs.SelectedItem);
                Assert.Equal("Column Definitions - Streaming Models", ((TabItem)tabs.SelectedItem).Header);

                if (tabs.SelectedContent is Control selectedContent)
                {
                    ExerciseBasicInteractions(selectedContent);
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
    public void MainWindow_CustomDrawingEditing_TabSwitch_Preserves_SelectedCellForeground()
    {
        var window = new MainWindow();
        window.ApplySampleTheme();

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                PumpLayout(window);

                var tabs = window.FindControl<TabControl>("SampleTabs");
                Assert.NotNull(tabs);

                var customTabIndex = FindTabIndexByHeader(tabs, "Custom Drawing Columns (Editing)");
                Assert.InRange(customTabIndex, 0, tabs.ItemCount - 1);

                tabs.SelectedIndex = customTabIndex;
                PumpLayout(window);

                var dataGrid = FindFirstDataGridInSelectedTab(tabs);
                Assert.NotNull(dataGrid);
                Assert.True(dataGrid.Columns.Count > 0);

                var firstItem = TryGetFirstItem(dataGrid.ItemsSource);
                Assert.NotNull(firstItem);

                dataGrid.SelectedItem = firstItem;
                dataGrid.ScrollIntoView(firstItem, dataGrid.Columns[0]);
                dataGrid.Focus();
                PumpLayout(window);

                var beforeSignature = GetSelectedCustomDrawingCellForegroundSignature(dataGrid, firstItem);
                Assert.NotNull(beforeSignature);

                var fallbackTabIndex = customTabIndex == 0 ? 1 : 0;
                tabs.SelectedIndex = fallbackTabIndex;
                PumpLayout(window);

                tabs.SelectedIndex = customTabIndex;
                PumpLayout(window);

                dataGrid = FindFirstDataGridInSelectedTab(tabs);
                Assert.NotNull(dataGrid);
                Assert.True(dataGrid.Columns.Count > 0);

                dataGrid.SelectedItem = firstItem;
                dataGrid.ScrollIntoView(firstItem, dataGrid.Columns[0]);
                dataGrid.Focus();
                PumpLayout(window);

                var afterSignature = GetSelectedCustomDrawingCellForegroundSignature(dataGrid, firstItem);
                Assert.NotNull(afterSignature);
                Assert.Equal(beforeSignature.Value, afterSignature.Value);

                var secondItem = TryGetNthItem(dataGrid.ItemsSource, 1);
                if (secondItem != null)
                {
                    dataGrid.SelectedItem = secondItem;
                    dataGrid.ScrollIntoView(secondItem, dataGrid.Columns[0]);
                    PumpLayout(window);

                    dataGrid.SelectedItem = firstItem;
                    dataGrid.ScrollIntoView(firstItem, dataGrid.Columns[0]);
                    PumpLayout(window);

                    var transitionSignature = GetSelectedCustomDrawingCellForegroundSignature(dataGrid, firstItem);
                    Assert.NotNull(transitionSignature);
                    Assert.Equal(beforeSignature.Value, transitionSignature.Value);
                }
            });
        }
        finally
        {
            window.Close();
        }

        Assert.Null(exception);
    }

    public static IEnumerable<object[]> PageIndexes()
    {
        for (var index = 0; index < SamplePageCatalog.All.Count; index++)
        {
            yield return new object[] { index, SamplePageCatalog.All[index].Name };
        }
    }

    private static Window CreateHostWindow(Control content)
    {
        var window = new Window
        {
            Width = 1280,
            Height = 900,
            Content = content
        };
        window.ApplySampleTheme();
        return window;
    }

    private static void ExerciseBasicInteractions(Control root)
    {
        root.ApplyTemplate();
        PumpLayout(root);

        foreach (var tabControl in root.GetVisualDescendants().OfType<TabControl>())
        {
            if (tabControl.ItemCount > 1)
            {
                tabControl.SelectedIndex = tabControl.ItemCount - 1;
                PumpLayout(root);
                tabControl.SelectedIndex = 0;
                PumpLayout(root);
            }
        }

        foreach (var dataGrid in root.GetVisualDescendants().OfType<DataGrid>())
        {
            dataGrid.ApplyTemplate();
            PumpLayout(root);
            dataGrid.UpdateLayout();

            if (dataGrid.Columns.Count == 0)
            {
                continue;
            }

            var firstItem = TryGetFirstItem(dataGrid.ItemsSource);
            if (firstItem == null)
            {
                continue;
            }

            dataGrid.SelectedItem = firstItem;
            dataGrid.ScrollIntoView(firstItem, dataGrid.Columns[0]);
            PumpLayout(root);
            dataGrid.UpdateLayout();
        }
    }

    private static object? TryGetFirstItem(IEnumerable? sequence)
    {
        if (sequence == null)
        {
            return null;
        }

        var enumerator = sequence.GetEnumerator();
        try
        {
            return enumerator.MoveNext() ? enumerator.Current : null;
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static object? TryGetNthItem(IEnumerable? sequence, int index)
    {
        if (sequence == null || index < 0)
        {
            return null;
        }

        var enumerator = sequence.GetEnumerator();
        try
        {
            var currentIndex = 0;
            while (enumerator.MoveNext())
            {
                if (currentIndex == index)
                {
                    return enumerator.Current;
                }

                currentIndex++;
            }

            return null;
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private static int FindTabIndexByHeader(TabControl tabControl, string header)
    {
        if (tabControl.Items is not IEnumerable items)
        {
            return -1;
        }

        var tabIndex = 0;
        foreach (var item in items)
        {
            if (item is TabItem tabItem &&
                string.Equals(tabItem.Header?.ToString(), header, StringComparison.Ordinal))
            {
                return tabIndex;
            }

            tabIndex++;
        }

        return -1;
    }

    private static DataGrid? FindFirstDataGridInSelectedTab(TabControl tabControl)
    {
        if (tabControl.SelectedContent is not Control selectedContent)
        {
            return null;
        }

        return selectedContent.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault();
    }

    private static BrushSignature? GetSelectedCustomDrawingCellForegroundSignature(DataGrid dataGrid, object selectedItem)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var customCell = dataGrid.GetVisualDescendants()
                .OfType<DataGridCustomDrawingCell>()
                .Where(cell => ReferenceEquals(cell.DataContext, selectedItem))
                .OrderBy(cell => cell.Bounds.X)
                .FirstOrDefault();

            if (customCell != null)
            {
                return BrushSignature.From(customCell.Foreground);
            }

            PumpLayout(dataGrid);
        }

        return null;
    }

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private readonly struct BrushSignature : IEquatable<BrushSignature>
    {
        public BrushSignature(byte kind, Color color, double opacity, int identityHash)
        {
            Kind = kind;
            Color = color;
            Opacity = opacity;
            IdentityHash = identityHash;
        }

        public byte Kind { get; }

        public Color Color { get; }

        public double Opacity { get; }

        public int IdentityHash { get; }

        public static BrushSignature From(IBrush? brush)
        {
            if (brush is ISolidColorBrush solid)
            {
                return new BrushSignature(kind: 1, color: solid.Color, opacity: solid.Opacity, identityHash: 0);
            }

            return brush != null
                ? new BrushSignature(kind: 2, color: default, opacity: 0, identityHash: RuntimeHelpers.GetHashCode(brush))
                : new BrushSignature(kind: 0, color: default, opacity: 0, identityHash: 0);
        }

        public bool Equals(BrushSignature other)
        {
            return Kind == other.Kind &&
                   Color.Equals(other.Color) &&
                   Opacity.Equals(other.Opacity) &&
                   IdentityHash == other.IdentityHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is BrushSignature other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, Color, Opacity, IdentityHash);
        }
    }
}
