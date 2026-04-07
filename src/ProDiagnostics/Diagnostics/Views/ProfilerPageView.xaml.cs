using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views;

partial class ProfilerPageView : UserControl
{
    public ProfilerPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
