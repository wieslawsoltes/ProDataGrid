using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class ViewLocatorTests
{
    [AvaloniaFact]
    public void Build_Maps_ViewModelsBindings_ViewModel_To_View()
    {
        var locator = new ViewLocator();

        var result = locator.Build(new ViewModelsBindingsPageViewModel());

        Assert.NotNull(result);
        Assert.Equal("ViewModelsBindingsPageView", result!.GetType().Name);
    }

    [AvaloniaFact]
    public void Build_Maps_StylesDiagnostics_ViewModel_To_View()
    {
        var locator = new ViewLocator();

        var result = locator.Build(new StylesDiagnosticsPageViewModel());

        Assert.NotNull(result);
        Assert.Equal("StylesDiagnosticsPageView", result!.GetType().Name);
    }

    [AvaloniaFact]
    public void Build_Returns_TextBlock_When_View_Is_Not_Found()
    {
        var locator = new ViewLocator();

        var result = locator.Build(new MissingViewModel());

        var textBlock = Assert.IsType<TextBlock>(result);
        Assert.EndsWith("MissingView", textBlock.Text);
    }

    private sealed class MissingViewModel : ViewModelBase
    {
    }
}
