using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views
{
    partial class ResourceReferencePickerFloatingPanel : UserControl
    {
        private ResourceReferencePickerView? _picker;
        private Point? _lastDragPoint;

        public ResourceReferencePickerFloatingPanel()
        {
            InitializeComponent();
            _picker = this.FindControl<ResourceReferencePickerView>("PART_Picker");
            if (_picker != null)
            {
                _picker.Completed += OnPickerCompleted;
            }
        }

        internal event EventHandler<ResourceReferenceCandidate?>? Completed;

        internal event EventHandler<Vector>? DragDeltaRequested;

        internal ResourceReferenceCandidate? SelectedCandidate => _picker?.SelectedCandidate;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnPickerCompleted(object? sender, ResourceReferenceCandidate? candidate)
        {
            Completed?.Invoke(this, candidate);
        }

        private void OnClose(object? sender, RoutedEventArgs e)
        {
            _picker?.Cancel();
        }

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            _lastDragPoint = point.Position;
            e.Pointer.Capture((IInputElement?)sender);
            e.Handled = true;
        }

        private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_lastDragPoint is not { } lastPoint)
            {
                return;
            }

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
            {
                _lastDragPoint = null;
                e.Pointer.Capture(null);
                return;
            }

            var delta = point.Position - lastPoint;
            if (Math.Abs(delta.X) > 0.1 || Math.Abs(delta.Y) > 0.1)
            {
                DragDeltaRequested?.Invoke(this, delta);
                _lastDragPoint = point.Position;
            }
        }

        private void OnTitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _lastDragPoint = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
