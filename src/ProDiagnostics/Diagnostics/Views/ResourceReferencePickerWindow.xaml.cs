using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views
{
    partial class ResourceReferencePickerWindow : Window
    {
        private ResourceReferencePickerView? _picker;

        public ResourceReferencePickerWindow()
        {
            InitializeComponent();
            _picker = this.FindControl<ResourceReferencePickerView>("PART_Picker");
            if (_picker != null)
            {
                _picker.Completed += OnPickerCompleted;
            }
        }

        internal ResourceReferenceCandidate? SelectedCandidate { get; private set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPickerCompleted(object? sender, ResourceReferenceCandidate? candidate)
        {
            SelectedCandidate = candidate;
            Close(candidate);
        }
    }
}
