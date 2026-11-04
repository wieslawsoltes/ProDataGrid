using System.Collections;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DataGridSample.Pages;
using Xunit;

namespace DataGridSample.Tests;

public sealed class MainWindowStartupLazyInitializationTests
{
    [AvaloniaFact]
    public void MainWindow_Construction_DoesNotInitializeHeavyPixelPages()
    {
        var window = new MainWindow();
        var tabs = window.FindControl<TabControl>("SampleTabs");
        Assert.NotNull(tabs);

        var heavyPagesFound = 0;
        foreach (Control page in EnumerateHeavyPages(tabs))
        {
            heavyPagesFound++;
            Assert.Null(page.DataContext);
        }

        Assert.True(heavyPagesFound > 0, "Expected heavy sample pages to exist in the tab collection.");
    }

    private static IEnumerable<Control> EnumerateHeavyPages(TabControl tabs)
    {
        if (tabs.Items is not IEnumerable items)
        {
            yield break;
        }

        foreach (object? item in items)
        {
            if (item is not TabItem tabItem || tabItem.Content is not Control control)
            {
                continue;
            }

            if (control is PixelColumnsPage or FrozenColumnsPage or RightFrozenColumnsPage or LargeUniformPage or
                RecycleDiagnosticsPage or OptimizedFlatCellPathsPage or OptimizedHierarchyCellPathsPage or
                VirtualSurfaceFlatDataPage or FlatSurfaceFlatDataPage or NestedSurfaceFlatDataPage or
                VirtualSurfaceHierarchyPage or FlatSurfaceHierarchyPage or NestedSurfaceHierarchyPage)
            {
                yield return control;
            }
        }
    }
}
