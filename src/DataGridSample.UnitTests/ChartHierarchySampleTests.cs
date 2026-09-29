using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using DataGridSample.Pages;
using DataGridSample.ViewModels;
using ProCharts;
using ProCharts.Avalonia;
using Xunit;

namespace DataGridSample.Tests;

public sealed class ChartHierarchySampleTests
{
    [Fact]
    public void Commands_Breadcrumbs_And_Kind_Switch_Follow_The_Current_Subtree()
    {
        using ChartHierarchyViewModel model = new();
        Assert.Equal("Portfolio", model.Breadcrumb);
        Assert.False(model.UpCommand.CanExecute(null));
        Assert.True(model.EnterCommand.CanExecute(null));
        model.SelectedBranch = model.Branches.Single(n => n.Id == "platform");
        model.EnterCommand.Execute(null);
        Assert.Equal("Portfolio / Platform", model.Breadcrumb);
        Assert.True(model.UpCommand.CanExecute(null));
        model.Kind = ChartSeriesKind.Sunburst;
        Assert.Equal(ChartSeriesKind.Sunburst, model.Chart.Snapshot.Series[0].Kind);
        Assert.Equal("platform", model.Source.CurrentRoot.Id);
        model.ResetCommand.Execute(null);
        Assert.Equal("Portfolio", model.Breadcrumb);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Kind = ChartSeriesKind.Line);
        model.Dispose();
        Assert.False(model.EnterCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Page_Uses_Compiled_Bindings_And_Recreates_Disposed_State_On_Reattach()
    {
        ChartHierarchyPage page = new();
        Assert.Null(page.DataContext);
        Window window = new() { Width = 800, Height = 600, Content = page };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var first = Assert.IsType<ChartHierarchyViewModel>(page.DataContext);
            var view = Assert.Single(page.GetLogicalDescendants().OfType<ProChartView>());
            Assert.True(ChartHierarchyNavigation.GetIsEnabled(view));
            Assert.Same(first.Chart, view.ChartModel);
            Assert.NotEmpty(view.ExportPng());
            Assert.Contains("Platform", view.ExportSvg(), StringComparison.Ordinal);
            SavePreview(view, "RootTreemap");

            Assert.True(first.Source.TryDrillDown("platform"));
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal("Portfolio / Platform", first.Breadcrumb);
            Assert.Contains("Compute", view.ExportSvg(), StringComparison.Ordinal);
            Assert.DoesNotContain("Products", view.ExportSvg(), StringComparison.Ordinal);
            SavePreview(view, "PlatformTreemap");
            first.Kind = ChartSeriesKind.Sunburst;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Contains("Compute", view.ExportSvg(), StringComparison.Ordinal);
            SavePreview(view, "PlatformSunburst");

            window.Content = null;
            Assert.Null(page.DataContext);
            Assert.False(first.EnterCommand.CanExecute(null));
            window.Content = page; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var next = Assert.IsType<ChartHierarchyViewModel>(page.DataContext);
            Assert.NotSame(first, next);
            Assert.Equal("portfolio", next.Source.CurrentRoot.Id);
        }
        finally { window.Close(); }
    }

    private static void SavePreview(ProChartView view, string name)
    {
        // Diagnostics from the actual bound sample view, not a separate sample renderer.
        string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || string.IsNullOrEmpty(workspace)) return;
        string directory = Path.Combine(workspace, "artifacts", "charting", "sample", "gallery");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".png"), view.ExportPng());
        File.WriteAllText(Path.Combine(directory, name + ".svg"), view.ExportSvg());
    }
}
