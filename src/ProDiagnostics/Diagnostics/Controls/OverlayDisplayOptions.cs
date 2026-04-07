namespace Avalonia.Diagnostics.Controls;

internal readonly record struct OverlayDisplayOptions(
    bool VisualizeMarginPadding,
    bool ShowInfo,
    bool ShowRulers,
    bool ShowExtensionLines)
{
    public static OverlayDisplayOptions Default => new(
        VisualizeMarginPadding: true,
        ShowInfo: false,
        ShowRulers: false,
        ShowExtensionLines: false);
}
