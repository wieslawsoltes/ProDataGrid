using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataGridSample.Models;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class RowDetailsPage : UserControl
    {
        private readonly ObservableCollection<LocalSampleItem> _items = RowDetailsSampleDataProvider.CreateSampleItems();

        public RowDetailsPage()
        {
            InitializeComponent();

            SampleGrid.ItemsSource = _items;
        }
    }
}
