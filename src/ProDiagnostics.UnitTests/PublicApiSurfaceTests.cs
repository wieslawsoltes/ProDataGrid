using System;
using System.Linq;
using Avalonia.Diagnostics.Screenshots;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class PublicApiSurfaceTests
{
    [Fact]
    public void Assembly_Exports_Only_Expected_Public_Types()
    {
        var assembly = typeof(DevToolsExtensions).Assembly;
        var exportedTypes = assembly
            .GetExportedTypes()
            .Where(type => !IsCompiledAvaloniaXamlType(type))
            .ToArray();
        var allowedTypes = new[]
        {
            typeof(DevToolsExtensions),
            typeof(VisualTreeDebug),
            typeof(DevToolsView),
            typeof(DevToolsViewKind),
            typeof(HotKeyConfiguration),
            typeof(DevToolsOptions),
            typeof(IScreenshotHandler),
            typeof(BaseRenderToStreamHandler),
            typeof(FilePickerHandler)
        };

        var unexpected = exportedTypes
            .Except(allowedTypes)
            .OrderBy(type => type.FullName)
            .ToArray();
        var missing = allowedTypes
            .Except(exportedTypes)
            .OrderBy(type => type.FullName)
            .ToArray();

        Assert.True(unexpected.Length == 0, $"Unexpected public types: {string.Join(", ", unexpected.Select(type => type.FullName ?? type.Name))}");
        Assert.True(missing.Length == 0, $"Missing public types: {string.Join(", ", missing.Select(type => type.FullName ?? type.Name))}");
    }

    private static bool IsCompiledAvaloniaXamlType(Type type)
    {
        return string.Equals(type.Namespace, "CompiledAvaloniaXaml", StringComparison.Ordinal)
               && type.Name.StartsWith("!", StringComparison.Ordinal);
    }
}
