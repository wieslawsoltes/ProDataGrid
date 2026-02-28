using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DataGridSample
{
    public partial class LargeVariableHeightSkiaCustomDrawPage : UserControl
    {
        public LargeVariableHeightSkiaCustomDrawPage()
        {
            InitializeComponent();
            AttachedToVisualTree += (_, _) => DataContext ??= new DataGridSample.ViewModels.LargeVariableHeightViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
