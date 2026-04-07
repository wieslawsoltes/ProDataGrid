using Avalonia.Controls;

namespace ProDiagnostics.Remote.WebClient.PerfHost;

internal static class BenchmarkFixtureFactory
{
    public static Window CreateDenseWindow(int controlCount)
    {
        var stack = new StackPanel
        {
            Name = "WebPerfStack",
        };

        for (var index = 0; index < controlCount; index++)
        {
            stack.Children.Add(new Border
            {
                Name = "WebPerfBorder" + index,
                Child = new StackPanel
                {
                    Name = "WebPerfInner" + index,
                    Children =
                    {
                        new TextBlock
                        {
                            Name = "WebPerfText" + index,
                            Text = "Item " + index,
                        },
                        new TextBox
                        {
                            Name = "WebPerfInput" + index,
                            Text = "Value " + index,
                        },
                    },
                },
            });
        }

        return new Window
        {
            Name = "WebPerfRootWindow",
            Width = 1400,
            Height = 900,
            Content = new ScrollViewer
            {
                Content = stack,
            },
        };
    }
}

