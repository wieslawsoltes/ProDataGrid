// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using Avalonia.Controls;
using DataGridSample.Adapters;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class DynamicDataSortingPage : UserControl
    {
        public DynamicDataSortingPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is DynamicDataSortingViewModel vm)
            {
                Grid.SortingAdapterFactory = vm.AdapterFactory;
                Grid.SortingModel = vm.SortingModel;
            }
        }
    }
}
