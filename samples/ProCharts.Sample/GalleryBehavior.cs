using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using ProCharts.Controls;
namespace ProCharts.Sample;

/// <summary>View-only responsive layout and live retemplating; the view model remains framework-agnostic.</summary>
public sealed class GalleryBehavior : AvaloniaObject
{
    public static readonly AttachedProperty<bool> ResponsiveProperty = AvaloniaProperty.RegisterAttached<GalleryBehavior, Control, bool>("Responsive");
    public static readonly AttachedProperty<bool> CompactProperty = AvaloniaProperty.RegisterAttached<GalleryBehavior, ProChart, bool>("Compact");
    public static bool GetResponsive(Control control) => control.GetValue(ResponsiveProperty);
    public static void SetResponsive(Control control, bool value) => control.SetValue(ResponsiveProperty, value);
    public static bool GetCompact(ProChart control) => control.GetValue(CompactProperty);
    public static void SetCompact(ProChart control, bool value) => control.SetValue(CompactProperty, value);
    static GalleryBehavior()
    {
        ResponsiveProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.PropertyChanged -= OnSizeChanged;
            if (e.GetNewValue<bool>()) control.PropertyChanged += OnSizeChanged;
            ApplySize(control);
        });
        CompactProperty.Changed.AddClassHandler<ProChart>((control, e) =>
        {
            ControlTheme? theme = null;
            if (e.GetNewValue<bool>() && Application.Current!.Resources.TryGetResource("ProChartCompact", control.ActualThemeVariant, out object? resource))
                theme = (ControlTheme)resource!;
            control.Theme = theme;
        });
    }
    private static void OnSizeChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    { if (e.Property == Visual.BoundsProperty) ApplySize((Control)sender!); }
    private static void ApplySize(Control control) => control.Classes.Set("narrow", control.Bounds.Width > 0 && control.Bounds.Width < 880);
}
