using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views
{
    public partial class ControlLayoutStylesView : UserControl
    {
        public ControlLayoutStylesView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void PropertyNamePressed(object sender, PointerPressedEventArgs e)
        {
            if (DataContext is not ControlDetailsViewModel mainVm)
            {
                return;
            }

            if (sender is Control control && control.DataContext is SetterViewModel setterVm)
            {
                mainVm.SelectProperty(setterVm.Property);
            }
        }
    }
}
