using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Diagnostics.Services;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class AssetsPageViewModel : ViewModelBase
    {
        private static readonly ISourceLocationService DefaultSourceLocationService = new PortablePdbSourceLocationService();
        private readonly AvaloniaList<AssetEntryViewModel> _assets = new();
        private readonly DataGridCollectionView _assetsView;
        private readonly ISourceLocationService _sourceLocationService;
        private AssetEntryViewModel? _selectedAsset;
        private bool _isLoading;
        private string? _status;

        public AssetsPageViewModel(MainViewModel mainView, ISourceLocationService? sourceLocationService = null)
        {
            MainView = mainView;
            _sourceLocationService = sourceLocationService ?? DefaultSourceLocationService;
            AssetsFilter = new FilterViewModel();
            AssetsFilter.RefreshFilter += (_, _) => _assetsView.Refresh();

            _assetsView = new DataGridCollectionView(_assets)
            {
                Filter = FilterAsset
            };
            _assetsView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                nameof(AssetEntryViewModel.AssemblyName),
                ListSortDirection.Ascending));
            _assetsView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                nameof(AssetEntryViewModel.AssetPath),
                ListSortDirection.Ascending));

            _ = LoadAssetsAsync();
        }

        public event EventHandler<string>? ClipboardCopyRequested;
        public event EventHandler<AssetEntryViewModel>? ExportRequested;
        public event EventHandler<AssetEntryViewModel>? PreviewRequested;

        public MainViewModel MainView { get; }

        public DataGridCollectionView AssetsView => _assetsView;

        public FilterViewModel AssetsFilter { get; }

        public AssetEntryViewModel? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                if (RaiseAndSetIfChanged(ref _selectedAsset, value))
                {
                    RaisePropertyChanged(nameof(HasSelection));
                    RaisePropertyChanged(nameof(HasPreviewSelection));
                }
            }
        }

        public bool HasSelection => SelectedAsset != null;

        public bool HasPreviewSelection => SelectedAsset?.IsPreviewSupported == true;

        public bool IsLoading
        {
            get => _isLoading;
            private set => RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string? Status
        {
            get => _status;
            private set => RaiseAndSetIfChanged(ref _status, value);
        }

        public int AssetCount => _assets.Count;

        public void CopyAssetUri()
        {
            if (SelectedAsset != null)
            {
                ClipboardCopyRequested?.Invoke(this, SelectedAsset.UriText);
            }
        }

        public void ExportAsset()
        {
            if (SelectedAsset != null)
            {
                ExportRequested?.Invoke(this, SelectedAsset);
            }
        }

        public void PreviewAsset()
        {
            if (SelectedAsset is { IsPreviewSupported: true })
            {
                PreviewRequested?.Invoke(this, SelectedAsset);
            }
        }

        private async Task LoadAssetsAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                Status = "Scanning assets...";
            });

            List<AssetEntryViewModel> assets;

            try
            {
                assets = await Task.Run(CollectAssets);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Status = $"Asset scan failed: {ex.Message}";
                    IsLoading = false;
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _assets.Clear();
                for (var i = 0; i < assets.Count; i++)
                {
                    assets[i].SourceLocation = ResolveAssetSourceLocation(assets[i]);
                    _assets.Add(assets[i]);
                }

                _assetsView.Refresh();
                RaisePropertyChanged(nameof(AssetCount));
                Status = $"{assets.Count} assets";
                IsLoading = false;
            });
        }

        public bool TrySelectAssetBySourceLocation(SourceDocumentLocation location)
        {
            AssetEntryViewModel? best = null;
            var bestScore = int.MaxValue;

            for (var i = 0; i < _assets.Count; i++)
            {
                var asset = _assets[i];
                if (!SourceLocationTextParser.TryParse(asset.SourceLocation, out var parsed))
                {
                    continue;
                }

                if (!SourceLocationTextParser.IsSameDocument(parsed.FilePath, location.FilePath))
                {
                    continue;
                }

                var score = Math.Abs(parsed.Line - location.Line) * 1000 + Math.Abs(parsed.Column - location.Column);
                if (score >= bestScore)
                {
                    continue;
                }

                best = asset;
                bestScore = score;
                if (score == 0)
                {
                    break;
                }
            }

            if (best is null || ReferenceEquals(best, SelectedAsset))
            {
                return false;
            }

            SelectedAsset = best;
            return true;
        }

        private bool FilterAsset(object obj)
        {
            if (obj is not AssetEntryViewModel asset)
            {
                return true;
            }

            if (AssetsFilter.Filter(asset.Name))
            {
                return true;
            }

            if (AssetsFilter.Filter(asset.AssetPath))
            {
                return true;
            }

            if (AssetsFilter.Filter(asset.AssemblyName))
            {
                return true;
            }

            if (AssetsFilter.Filter(asset.SourceLocation))
            {
                return true;
            }

            return AssetsFilter.Filter(asset.KindDisplay);
        }

        private static List<AssetEntryViewModel> CollectAssets()
        {
            var results = new List<AssetEntryViewModel>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                var name = assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var baseUri = new Uri($"avares://{name}/");
                IEnumerable<Uri> assets;
                try
                {
                    assets = AssetLoader.GetAssets(baseUri, null!);
                }
                catch
                {
                    continue;
                }

                foreach (var assetUri in assets)
                {
                    var uriText = assetUri.ToString();
                    if (!seen.Add(uriText))
                    {
                        continue;
                    }

                    var assetPath = NormalizePath(assetUri);
                    var assemblyName = string.IsNullOrWhiteSpace(assetUri.Host) ? name : assetUri.Host;
                    var kind = ClassifyKind(assetPath);
                    results.Add(new AssetEntryViewModel(assetUri, assembly, assemblyName, assetPath, kind));
                }
            }

            results.Sort((left, right) =>
            {
                var compare = string.Compare(left.AssemblyName, right.AssemblyName, StringComparison.OrdinalIgnoreCase);
                return compare != 0
                    ? compare
                    : string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        private static string NormalizePath(Uri uri)
        {
            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            return path.TrimStart('/');
        }

        private static AssetKind ClassifyKind(string assetPath)
        {
            var extension = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();

            if (extension.Length == 0)
            {
                return AssetKind.Other;
            }

            if (IsImageExtension(extension))
            {
                return AssetKind.Image;
            }

            if (IsFontExtension(extension))
            {
                return AssetKind.Font;
            }

            if (IsTextExtension(extension))
            {
                return AssetKind.Text;
            }

            return AssetKind.Other;
        }

        private static bool IsImageExtension(string extension)
        {
            return extension is ".png"
                or ".jpg"
                or ".jpeg"
                or ".bmp"
                or ".gif"
                or ".tif"
                or ".tiff"
                or ".webp"
                or ".ico";
        }

        private static bool IsFontExtension(string extension)
        {
            return extension is ".ttf"
                or ".otf"
                or ".ttc"
                or ".otc"
                or ".woff"
                or ".woff2";
        }

        private static bool IsTextExtension(string extension)
        {
            return extension is ".axaml"
                or ".xaml"
                or ".xml"
                or ".txt"
                or ".json"
                or ".md"
                or ".csv"
                or ".yaml"
                or ".yml"
                or ".ini"
                or ".config";
        }

        private string ResolveAssetSourceLocation(AssetEntryViewModel asset)
        {
            var byPath = _sourceLocationService.ResolveDocument(asset.Assembly, asset.AssetPath, asset.AssetPath);
            if (byPath is not null)
            {
                return byPath.DisplayText;
            }

            var byFileName = _sourceLocationService.ResolveDocument(asset.Assembly, asset.Name, asset.Name);
            if (byFileName is not null)
            {
                return byFileName.DisplayText;
            }

            return string.Empty;
        }
    }
}
