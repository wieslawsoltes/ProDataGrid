using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class RowDetailsTemplateSelectorPage : UserControl
    {
        private readonly ObservableCollection<LocalSampleItem> _items = RowDetailsSampleDataProvider.CreateSampleItems();

        public RowDetailsTemplateSelectorPage()
        {
            InitializeComponent();

            // Assign sample data
            SampleGrid.ItemsSource = _items;

            // Assign runtime selector instance to avoid XAML type-resolution conflicts
            SampleGrid.RowDetailsTemplateSelector = new Selectors.RowDetailsTemplateSelector();
        }
    }
}
