using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ProCharts.Controls.Tests;

public sealed class ChartLayoutPanelTests
{
    [AvaloniaFact]
    public void Fill_minimum_is_preserved_when_no_vertical_space_remains()
    {
        var title = new Border { Height = 60 };
        var toolbar = new Border { Height = 80 };
        var plot = new Border { MinHeight = 120 };
        var footer = new Border { Height = 40 };
        var panel = new ChartLayoutPanel { FillIndex = 2, Children = { title, toolbar, plot, footer } };
        panel.Measure(new Size(360, 100));
        panel.Arrange(new Rect(0, 0, 360, 100));
        Assert.Equal(120, plot.DesiredSize.Height);
        Assert.Equal(140, plot.Bounds.Y);
        Assert.Equal(120, plot.Bounds.Height);
        Assert.True(footer.Bounds.Y >= plot.Bounds.Bottom);
        Assert.True(panel.ClipToBounds);
    }

    [AvaloniaFact]
    public void Arranged_width_reflows_wrapped_chrome_before_placing_plot()
    {
        var toolbar = new WrapPanel();
        for (int i = 0; i < 4; i++) toolbar.Children.Add(new Border { Width = 100, Height = 30 });
        var plot = new Border { MinHeight = 120 };
        var panel = new ChartLayoutPanel { FillIndex = 1, Children = { toolbar, plot } };
        panel.Measure(new Size(450, 400));
        panel.Arrange(new Rect(0, 0, 210, 400));
        Assert.Equal(60, toolbar.Bounds.Height);
        Assert.Equal(60, plot.Bounds.Y);
        Assert.Equal(340, plot.Bounds.Height);
    }

    [AvaloniaFact]
    public void Hidden_parts_do_not_consume_height_and_invalid_fill_index_stacks_everything()
    {
        var hidden = new Border { Height = 50, IsVisible = false };
        var first = new Border { Height = 30 };
        var second = new Border { Height = 40 };
        var panel = new ChartLayoutPanel { FillIndex = 8, Children = { hidden, first, second } };
        panel.Measure(new Size(400, 500));
        panel.Arrange(new Rect(0, 0, 400, 500));
        Assert.Equal(0, first.Bounds.Y);
        Assert.Equal(30, second.Bounds.Y);
        Assert.Equal(40, second.Bounds.Height);
    }
}
