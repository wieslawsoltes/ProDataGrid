using System.Runtime.InteropServices.JavaScript;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ProCharts.Controls;

namespace ProCharts.Sample.Browser;

public static partial class Program
{
    /// <summary>
    /// Makes a native control reachable through its enclosing ScrollViewer before browser input.
    /// This is test navigation, equivalent to scrollIntoView; it never invokes a chart command.
    /// </summary>
    [JSExport]
    public static void Reveal(string id)
    {
        MainView view = ((App)global::Avalonia.Application.Current!).View!;
        foreach (Control control in view.GetVisualDescendants().OfType<Control>())
        {
            if (!control.IsEffectivelyVisible) continue;
            string? candidate = AutomationProperties.GetAutomationId(control);
            if (control is ListBoxItem && control.DataContext is GalleryExample example) candidate = "case-" + example.Id;
            if (control is CheckBox && control.DataContext is ChartLegendItem legend) candidate = "legend-" + legend.Index;
            if (!string.Equals(id, candidate, StringComparison.Ordinal)) continue;
            control.BringIntoView();
            return;
        }
        throw new ArgumentException("No visible native control has this identifier.", nameof(id));
    }
}
