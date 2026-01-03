using System;
using Avalonia.Controls;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class DynamicDataHierarchicalSourceListPage : UserControl
    {
        public DynamicDataHierarchicalSourceListPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DynamicDataHierarchicalSourceListViewModel vm)
            {
                Grid.SortingAdapterFactory = vm.SortingAdapterFactory;
                Grid.FilteringAdapterFactory = vm.FilteringAdapterFactory;
                Grid.SearchAdapterFactory = vm.SearchAdapterFactory;
                Grid.SortingModel = vm.SortingModel;
                Grid.FilteringModel = vm.FilteringModel;
                Grid.SearchModel = vm.SearchModel;
            }
        }
    }
}
