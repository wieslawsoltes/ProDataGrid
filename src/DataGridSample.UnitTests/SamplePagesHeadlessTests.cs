using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

    private static void PumpLayout(Control control)
    {
        Dispatcher.UIThread.RunJobs();
        control.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
