using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Diagnostics.ViewModels;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace Avalonia.Diagnostics.Views
{
    partial class AssetsPageView : UserControl
    {
        public AssetsPageView()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DataContextProperty)
            {
                if (change.GetOldValue<object?>() is AssetsPageViewModel oldViewModel)
                {
                    oldViewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
                    oldViewModel.ExportRequested -= OnExportRequested;
                    oldViewModel.PreviewRequested -= OnPreviewRequested;
                }

                if (change.GetNewValue<object?>() is AssetsPageViewModel newViewModel)
                {
                    newViewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
                    newViewModel.ExportRequested += OnExportRequested;
                    newViewModel.PreviewRequested += OnPreviewRequested;
                }
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnAssetDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is AssetsPageViewModel viewModel)
            {
                viewModel.PreviewAsset();
            }
        }

        private void OnClipboardCopyRequested(object? sender, string uriText)
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                clipboard.SetTextAsync(uriText);
            }
        }

        private async void OnExportRequested(object? sender, AssetEntryViewModel asset)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                return;
            }

            var storageProvider = topLevel.StorageProvider;
            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Asset",
                SuggestedFileName = asset.Name,
                DefaultExtension = asset.Extension,
                FileTypeChoices = new[] { FilePickerFileTypes.All }
            });

            if (result is null)
            {
                return;
            }

            try
            {
                using var source = AssetLoader.Open(asset.Uri, null!);
                using var destination = await result.OpenWriteAsync();
                await source.CopyToAsync(destination);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void OnPreviewRequested(object? sender, AssetEntryViewModel asset)
        {
            var preview = new AssetPreviewWindow
            {
                DataContext = new AssetPreviewViewModel(asset)
            };

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner != null)
            {
                preview.Show(owner);
            }
            else
            {
                preview.Show();
            }
        }
    }
}
