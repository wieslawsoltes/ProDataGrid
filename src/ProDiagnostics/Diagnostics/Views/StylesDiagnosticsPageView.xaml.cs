using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views;

partial class StylesDiagnosticsPageView : UserControl
{
    public StylesDiagnosticsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
