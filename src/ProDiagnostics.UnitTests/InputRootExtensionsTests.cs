using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Rendering;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class InputRootExtensionsTests
{
    [AvaloniaFact]
    public void GetInputTopLevel_Uses_PresentationSource_RootVisual()
    {
        var border = new Border { Width = 120, Height = 80 };
        var window = CreateWindow(border);

        try
        {
            var source = GetPresentationSourceObject(window);
            var inputRoot = Assert.IsAssignableFrom<IInputRoot>(source);
            var topLevel = Invoke("GetInputTopLevel", inputRoot);

            Assert.IsType<Window>(topLevel);
            Assert.Same(window, topLevel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void GetScreenPoint_Uses_PresentationSource_RootVisual()
    {
        var border = new Border { Width = 120, Height = 80 };
        var window = CreateWindow(border);

        try
        {
            var source = GetPresentationSourceObject(window);
            var inputRoot = Assert.IsAssignableFrom<IInputRoot>(source);
            var presentationSource = Assert.IsAssignableFrom<IPresentationSource>(source);
            var point = new Point(10, 12);
            var rootVisual = Assert.IsAssignableFrom<Visual>(presentationSource.RootVisual);

            var screenPoint = Assert.IsType<PixelPoint>(Invoke("GetScreenPoint", inputRoot, point));

            Assert.Equal(rootVisual.PointToScreen(point), screenPoint);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(Control content)
    {
        var window = new Window
        {
            Width = 200,
            Height = 120,
            Content = content
        };

        window.Show();
        window.UpdateLayout();
        content.UpdateLayout();

        return window;
    }

    private static object? Invoke(string methodName, params object?[] args)
    {
        var extensionsType = typeof(DevToolsExtensions).Assembly
            .GetType("Avalonia.Diagnostics.InputRootExtensions", throwOnError: true);
        var method = extensionsType!.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Unable to locate {methodName}.");

        return method.Invoke(null, args);
    }

    private static object GetPresentationSourceObject(TopLevel topLevel)
    {
        var property = typeof(TopLevel).GetProperty(
            "PresentationSource",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            ?? throw new InvalidOperationException("Unable to locate TopLevel.PresentationSource.");
        return property.GetValue(topLevel)
            ?? throw new InvalidOperationException("TopLevel.PresentationSource was null.");
    }
}
