using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Avalonia.Diagnostics.UnitTests;

public class SettingsPageViewModelTests
{
    [AvaloniaFact]
    public void Overlay_Toggles_Are_Applied_To_MainViewModel()
    {
        using var main = new MainViewModel(new Window());
        using var settings = new SettingsPageViewModel(main);

        settings.VisualizeMarginPaddingOverlay = false;
        settings.ShowOverlayInfo = true;
        settings.ShowOverlayRulers = true;
        settings.ShowOverlayExtensionLines = true;
        settings.HighlightElements = false;
        settings.TrackFocusedControl = false;

        Assert.False(main.ShouldVisualizeMarginPadding);
        Assert.True(main.ShowOverlayInfo);
        Assert.True(main.ShowOverlayRulers);
        Assert.True(main.ShowOverlayExtensionLines);
        Assert.False(main.HighlightElements);
        Assert.False(main.TrackFocusedControl);
    }

    [AvaloniaFact]
    public void ResetOverlayDefaults_Restores_Default_State()
    {
        using var main = new MainViewModel(new Window());
        using var settings = new SettingsPageViewModel(main);

        settings.VisualizeMarginPaddingOverlay = false;
        settings.ShowOverlayInfo = true;
        settings.ShowOverlayRulers = true;
        settings.ShowOverlayExtensionLines = true;
        settings.HighlightElements = false;

        settings.ResetOverlayDefaults();

        Assert.True(main.ShouldVisualizeMarginPadding);
        Assert.False(main.ShowOverlayInfo);
        Assert.False(main.ShowOverlayRulers);
        Assert.False(main.ShowOverlayExtensionLines);
        Assert.True(main.HighlightElements);
    }
}
