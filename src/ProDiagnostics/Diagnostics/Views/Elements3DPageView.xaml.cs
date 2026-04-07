using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views;

partial class Elements3DPageView : UserControl
{
    public Elements3DPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
