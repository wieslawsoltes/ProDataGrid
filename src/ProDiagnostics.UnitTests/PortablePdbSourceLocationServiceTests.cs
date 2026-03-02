using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Diagnostics.Views;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class PortablePdbSourceLocationServiceTests
{
    [Fact]
    public void Resolve_NullType_Returns_EmptyInfo()
    {
        var service = new PortablePdbSourceLocationService();

        var info = service.Resolve(type: null);

        Assert.False(info.HasAnyLocation);
        Assert.Equal(SourceLocationInfo.Empty.Status, info.Status);
    }

    [Fact]
    public void Resolve_ViewType_Returns_Xaml_And_Code_Locations()
    {
        var service = new PortablePdbSourceLocationService();

        var info = service.Resolve(typeof(MainView));

        Assert.NotNull(info.XamlLocation);
        Assert.NotNull(info.CodeLocation);
        Assert.True(info.XamlLocation!.Line > 0);
        Assert.True(info.CodeLocation!.Line > 0);
        Assert.Matches("(?i)\\.(xaml|axaml)$", info.XamlLocation.FilePath);
        Assert.Matches("(?i)\\.cs$", info.CodeLocation.FilePath);
    }

    [Fact]
    public void Resolve_ViewModelType_Returns_Code_Location()
    {
        var service = new PortablePdbSourceLocationService();

        var info = service.Resolve(typeof(MainViewModel));

        Assert.NotNull(info.CodeLocation);
        Assert.True(info.CodeLocation!.Line > 0);
        Assert.Matches("(?i)\\.cs$", info.CodeLocation.FilePath);
    }

    [Fact]
    public void ResolveDocument_FileNameHint_Returns_DocumentLocation()
    {
        var service = new PortablePdbSourceLocationService();

        var location = service.ResolveDocument(typeof(MainViewModel).Assembly, "MainViewModel.cs");

        Assert.NotNull(location);
        Assert.True(location!.Line > 0);
        Assert.Matches("(?i)mainviewmodel\\.cs$", location.FilePath);
    }

    [Fact]
    public void ResolveDocument_LineHint_Refines_Line_Within_Document()
    {
        var service = new PortablePdbSourceLocationService();

        var location = service.ResolveDocument(typeof(MainView).Assembly, "MainView.xaml", "Show overlay info");

        Assert.NotNull(location);
        Assert.True(location!.Line > 0);
        Assert.Matches("(?i)mainview\\.xaml$", location.FilePath);
        Assert.True(File.ReadAllLines(location.FilePath)[location.Line - 1]
            .Contains("Show overlay info", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void ResolveObject_View_Uses_Type_Fallback_Xaml_Document()
    {
        var service = new PortablePdbSourceLocationService();
        var view = new MainView();

        var info = service.ResolveObject(view);

        Assert.NotNull(info.XamlLocation);
        Assert.Matches("(?i)mainview\\.xaml$", info.XamlLocation!.FilePath);
        Assert.True(info.XamlLocation.Line > 0);
    }

    [AvaloniaFact]
    public void ResolveObject_View_Document_And_Line_Hints_Refine_Xaml_Line()
    {
        var service = new PortablePdbSourceLocationService();
        var view = new MainView();

        var info = service.ResolveObject(view, "MainView.xaml", "Show overlay info");

        Assert.NotNull(info.XamlLocation);
        Assert.Matches("(?i)mainview\\.xaml$", info.XamlLocation!.FilePath);
        Assert.True(info.XamlLocation.Line > 0);
        Assert.True(File.ReadAllLines(info.XamlLocation.FilePath)[info.XamlLocation.Line - 1]
            .Contains("Show overlay info", StringComparison.OrdinalIgnoreCase));
    }
}
