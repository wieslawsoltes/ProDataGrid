using System;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Diagnostics.Services;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Avalonia.Diagnostics.Views
{
    partial class ResourceReferencePickerView : UserControl
    {
        public ResourceReferencePickerView()
        {
            InitializeComponent();
        }

        internal event EventHandler<ResourceReferenceCandidate?>? Completed;

        internal ResourceReferenceCandidate? SelectedCandidate { get; private set; }

        internal void Cancel()
        {
            SelectedCandidate = null;
            Completed?.Invoke(this, null);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnUseStaticResource(object? sender, RoutedEventArgs e)
        {
            CompleteSelectedCandidate(DevToolsResourceReferenceKind.Static);
        }

        private void OnUseDynamicResource(object? sender, RoutedEventArgs e)
        {
            CompleteSelectedCandidate(DevToolsResourceReferenceKind.Dynamic);
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void OnResourceDoubleTapped(object? sender, TappedEventArgs e)
        {
            CompleteSelectedCandidate(DevToolsResourceReferenceKind.Static);
        }

        private void CompleteSelectedCandidate(DevToolsResourceReferenceKind kind)
        {
            if (DataContext is ResourceReferencePickerViewModel viewModel &&
                viewModel.GetSelectedCandidate(kind) is { } candidate)
            {
                SelectedCandidate = candidate;
                Completed?.Invoke(this, candidate);
            }
        }
    }
}
