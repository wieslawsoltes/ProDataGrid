using System;
using System.IO;
using System.Reflection;

namespace Avalonia.Diagnostics.ViewModels
{
    internal sealed class AssetEntryViewModel : ViewModelBase
    {
        private string _sourceLocation;

        public AssetEntryViewModel(
            Uri uri,
            Assembly assembly,
            string assemblyName,
            string assetPath,
            AssetKind kind)
        {
            Uri = uri;
            Assembly = assembly;
            UriText = uri.ToString();
            AssemblyName = assemblyName;
            AssetPath = assetPath;
            Name = Path.GetFileName(assetPath);
            Extension = Path.GetExtension(assetPath);
            Kind = kind;
            KindDisplay = kind.ToString();
            IsPreviewSupported = kind != AssetKind.Other;
            _sourceLocation = string.Empty;
        }

        public AssetEntryViewModel(
            string uriText,
            string assemblyName,
            string assetPath,
            AssetKind kind,
            string sourceLocation)
        {
            if (!Uri.TryCreate(uriText, UriKind.Absolute, out var parsedUri))
            {
                parsedUri = new Uri("avares://unknown/" + assetPath.TrimStart('/'));
            }

            Uri = parsedUri;
            Assembly = null;
            UriText = uriText;
            AssemblyName = assemblyName;
            AssetPath = assetPath;
            Name = Path.GetFileName(assetPath);
            Extension = Path.GetExtension(assetPath);
            Kind = kind;
            KindDisplay = kind.ToString();
            IsPreviewSupported = false;
            _sourceLocation = sourceLocation ?? string.Empty;
        }

        public Uri Uri { get; }
        public Assembly? Assembly { get; }
        public string UriText { get; }
        public string AssemblyName { get; }
        public string AssetPath { get; }
        public string Name { get; }
        public string Extension { get; }
        public AssetKind Kind { get; }
        public string KindDisplay { get; }
        public bool IsPreviewSupported { get; }

        public string SourceLocation
        {
            get => _sourceLocation;
            set => RaiseAndSetIfChanged(ref _sourceLocation, value);
        }
    }
}
