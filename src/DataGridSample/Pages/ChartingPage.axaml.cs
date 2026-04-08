using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using DataGridSample.ViewModels;

namespace DataGridSample.Pages
{
    public partial class ChartingPage : UserControl
    {
        public static readonly StyledProperty<ChartSampleKind> SampleKindProperty =
            AvaloniaProperty.Register<ChartingPage, ChartSampleKind>(nameof(SampleKind));

        static ChartingPage()
        {
            SampleKindProperty.Changed.AddClassHandler<ChartingPage>((control, _) => control.UpdateViewModel());
        }

        public ChartingPage()
        {
            InitializeComponent();
        }

        public ChartSampleKind SampleKind
        {
            get => GetValue(SampleKindProperty);
            set => SetValue(SampleKindProperty, value);
        }

        private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (DataContext is ChartSampleViewModel viewModel)
            {
                viewModel.Chart.Refresh();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateViewModel();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DisposeCurrentViewModel();
            base.OnDetachedFromVisualTree(e);
        }

        private void UpdateViewModel()
        {
            if (VisualRoot is null)
            {
                return;
            }

            if (DataContext is ChartSampleViewModel viewModel && viewModel.Kind == SampleKind)
            {
                return;
            }

            DisposeCurrentViewModel();
            DataContext = new ChartSampleViewModel(SampleKind);
        }

        private void DisposeCurrentViewModel()
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            DataContext = null;
        }
    }
}
