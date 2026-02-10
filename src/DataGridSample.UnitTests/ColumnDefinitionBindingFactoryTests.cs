using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ColumnDefinitionBindingFactoryTests
{
    [AvaloniaFact]
    public void ColumnDefinitions_WithMixedItemTypes_DoNotThrow()
    {
        var vm = new ColumnChooserSampleViewModel();
        var mixedItems = new ObservableCollection<object>
        {
            new object(),
            vm.Items[0],
            vm.Items[1]
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ColumnDefinitionsSource = vm.ColumnDefinitions,
            ItemsSource = mixedItems,
            Height = 220
        };

        var window = CreateWindow();
        window.Content = grid;

        Exception? exception = null;
        try
        {
            exception = Record.Exception(() =>
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                grid.UpdateLayout();
            });
        }
        finally
        {
            window.Close();
        }

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void ColumnDefinitions_WithMixedItemTypes_Survive_Tab_AttachDetach_Cycles()
    {
        var vm = new ColumnChooserSampleViewModel();
        var mixedItems = new ObservableCollection<object>
        {
            new object(),
            vm.Items[0],
            new object(),
            vm.Items[1]
        };

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            ColumnDefinitionsSource = vm.ColumnDefinitions,
            ItemsSource = mixedItems,
            Height = 220
        };

        var tabControl = new TabControl
        {
            Items =
            {
                new TabItem
                {
                    Header = "Grid",
                    Content = grid
                },
                new TabItem
                {
                    Header = "Other",
                    Content = new TextBlock
                    {
                        Text = "Placeholder"
                    }
                }
            }
        };

        var window = CreateWindow();
        window.Content = tabControl;

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

                for (var i = 0; i < 6; i++)
                {
                    tabControl.SelectedIndex = 1;
                    Dispatcher.UIThread.RunJobs();

                    tabControl.SelectedIndex = 0;
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

    private static Window CreateWindow()
    {
        var window = new Window
        {
            Width = 720,
            Height = 420
        };

        window.ApplySampleTheme();
        return window;
    }
}
