using Avalonia.Controls;
using Avalonia.Input;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class KeyboardGestureHandlingPage : UserControl
    {
        public KeyboardGestureHandlingPage()
        {
            InitializeComponent();
        }

        private void OnGridKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not KeyboardGestureHandlingViewModel viewModel)
            {
                return;
            }

            if (viewModel.HandleDirectionalKeys && e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
            {
                e.Handled = true;
            }

            var modifiers = e.KeyModifiers == KeyModifiers.None ? string.Empty : $" ({e.KeyModifiers})";
            var handled = e.Handled ? " handled" : string.Empty;
            viewModel.LastKey = $"{e.Key}{modifiers}{handled}";
        }
    }
}
