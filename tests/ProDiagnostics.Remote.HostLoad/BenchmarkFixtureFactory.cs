using Avalonia.Controls;

namespace ProDiagnostics.Remote.HostLoad;

internal static class BenchmarkFixtureFactory
{
    public static Window CreateDenseWindow(int controlCount)
    {
        var stack = new StackPanel
        {
            Name = "HostLoadStack",
        };

        for (var index = 0; index < controlCount; index++)
        {
            stack.Children.Add(new Border
            {
                Name = "HostLoadBorder" + index,
                Child = new StackPanel
                {
                    Name = "HostLoadInner" + index,
                    Children =
                    {
                        new TextBlock
                        {
                            Name = "HostLoadText" + index,
                            Text = "Row " + index,
                        },
                        new TextBox
                        {
                            Name = "HostLoadInput" + index,
                            Text = "Value " + index,
                        },
                    },
                },
            });
        }

        return new Window
        {
            Name = "HostLoadRootWindow",
            Width = 1280,
            Height = 900,
            Content = new ScrollViewer
            {
                Content = stack,
            },
        };
    }
}
