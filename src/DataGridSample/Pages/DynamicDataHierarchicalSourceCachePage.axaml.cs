using System;
using Avalonia.Controls;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class DynamicDataHierarchicalSourceCachePage : UserControl
    {
        public DynamicDataHierarchicalSourceCachePage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DynamicDataHierarchicalSourceCacheViewModel vm)
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
