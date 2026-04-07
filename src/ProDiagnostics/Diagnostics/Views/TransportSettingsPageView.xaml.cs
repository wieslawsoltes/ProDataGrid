using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views;

partial class TransportSettingsPageView : UserControl
{
    public TransportSettingsPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
