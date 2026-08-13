// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using Xunit;

namespace DataGridSample.Tests;

public sealed class LayoutGalleryPageTests
{
    [AvaloniaFact]
    public void Gallery_switches_between_table_and_templated_card_realization()
    {
        var viewModel = new LayoutGalleryViewModel();
        viewModel.SelectedLayout = viewModel.Layouts[3];
        var page = new LayoutGalleryPage { DataContext = viewModel };
        var window = new Window { Width = 1120, Height = 720, Content = page };
        window.ApplySampleTheme();
        try
        {
            window.Show();
            PumpLayout(window);

            DataGrid grid = page.GetVisualDescendants().OfType<DataGrid>().Single();
            DataGridColumnHeadersPresenter headers = page.GetVisualDescendants()
                .OfType<DataGridColumnHeadersPresenter>()
                .Single();
            Assert.Contains(page.GetVisualDescendants().OfType<DataGridItemContainer>(),
                static item => item.Index >= 0);
            Assert.DoesNotContain(page.GetVisualDescendants().OfType<DataGridRow>(),
                static row => row.Index >= 0);
            Assert.Empty(page.GetVisualDescendants().OfType<DataGridCell>());
            Assert.False(headers.IsVisible);
            SaveScreenshot(window, "layout-gallery-item-cards.png");

            viewModel.SelectedLayout = viewModel.Layouts[0];
            PumpLayout(window);

            Assert.Contains(page.GetVisualDescendants().OfType<DataGridRow>(),
                static row => row.Index >= 0);
            Assert.DoesNotContain(page.GetVisualDescendants().OfType<DataGridItemContainer>(),
                static item => item.Index >= 0);
            Assert.NotEmpty(page.GetVisualDescendants().OfType<DataGridCell>());
            Assert.True(headers.IsVisible);
            Assert.Same(viewModel.LayoutModel, grid.LayoutModel);
            SaveScreenshot(window, "layout-gallery-table-rows.png");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void PumpLayout(Window window)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private static void SaveScreenshot(Window window, string fileName)
    {
        string? directory = Environment.GetEnvironmentVariable("AVALONIA_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, fileName));
        using var stream = File.Create(path);
        frame.Save(stream, new PngBitmapEncoderOptions());
        Assert.True(new FileInfo(path).Length > 0);
    }
}
