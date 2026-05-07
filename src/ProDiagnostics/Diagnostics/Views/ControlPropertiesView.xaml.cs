using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics.Views
{
    public partial class ControlPropertiesView : UserControl
    {
        private readonly DataGrid _dataGrid;

        public ControlPropertiesView()
        {
            InitializeComponent();
            _dataGrid = this.GetControl<DataGrid>("DataGrid");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DataContextProperty)
            {
                if (change.GetOldValue<object?>() is ControlDetailsViewModel oldViewModel)
                    oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                if (change.GetNewValue<object?>() is ControlDetailsViewModel newViewModel)
                    newViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is ControlDetailsViewModel viewModel)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void PropertiesGrid_OnDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is DataGrid grid && grid.DataContext is ControlDetailsViewModel controlDetails)
            {
                controlDetails.NavigateToSelectedProperty();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ControlDetailsViewModel.SelectedProperty)
                && sender is ControlDetailsViewModel viewModel
                && viewModel.SelectedProperty is not null)
            {
                _dataGrid.ScrollIntoView(viewModel.SelectedProperty, null);
            }
        }
    }
}
