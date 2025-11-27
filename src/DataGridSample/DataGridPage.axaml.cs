using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DataGridSample.ViewModels;

namespace DataGridSample
{
    public partial class DataGridPage : UserControl
    {
        public DataGridPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnCountriesSorting(object? sender, DataGridColumnEventArgs e)
        {
            if (DataContext is not DataGridPageViewModel viewModel)
            {
                return;
            }

            var binding = (e.Column as DataGridBoundColumn)?.Binding as Binding;

            if (binding?.Path is { } propertyPath)
            {
                viewModel.EnsureCustomSort(propertyPath);
            }
        }

        private void NumericUpDown_OnTemplateApplied(object sender, TemplateAppliedEventArgs e)
        {
            // We want to focus the TextBox of the NumericUpDown. To do so we search for this control when the template
            // is applied, but we postpone the action until the control is actually loaded. 
            if (e.NameScope.Find<TextBox>("PART_TextBox") is {} textBox)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }, DispatcherPriority.Loaded);
            }
        }
    }
}
