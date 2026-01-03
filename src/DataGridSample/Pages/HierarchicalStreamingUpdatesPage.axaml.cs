using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DataGridSample.Pages
{
    public partial class HierarchicalStreamingUpdatesPage : UserControl
    {
        public HierarchicalStreamingUpdatesPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
