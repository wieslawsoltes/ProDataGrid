using System;
using System.IO;
using Avalonia.Controls;
using ProDataGrid.SourceGeneration;

namespace Avalonia.Diagnostics.ViewModels
{
    [GenerateDataGridColumns(
        ProviderName = "AssetEntryGridSchema",
        SchemaId = "prodiagnostics/assets/v1",
        Discovery = DataGridColumnDiscovery.AttributedOnly,
        Strict = true)]
    internal sealed class AssetEntryViewModel : ViewModelBase
    {
        public AssetEntryViewModel(Uri uri, string assemblyName, string assetPath, AssetKind kind)
        {
            Uri = uri;
            UriText = uri.ToString();
            AssemblyName = assemblyName;
            AssetPath = assetPath;
            Name = Path.GetFileName(assetPath);
            Extension = Path.GetExtension(assetPath);
            Kind = kind;
            KindDisplay = kind.ToString();
            IsPreviewSupported = kind != AssetKind.Other;
        }

        public Uri Uri { get; }
        public string UriText { get; }
        [DataGridColumn(Header = "Assembly", ColumnKey = "assembly", Order = 1, Width = "2*", IsReadOnly = true, CanUserSort = true)]
        public string AssemblyName { get; }

        [DataGridColumn(Header = "Path", ColumnKey = "path", Order = 2, Width = "3*", IsReadOnly = true, CanUserSort = true)]
        public string AssetPath { get; }

        [DataGridColumn(Header = "Name", ColumnKey = "name", Order = 0, Width = "2*", IsReadOnly = true, CanUserSort = true)]
        public string Name { get; }

        [DataGridColumn(Header = "Ext", ColumnKey = "extension", Order = 4, Width = "0.8*", IsReadOnly = true, CanUserSort = true)]
        public string Extension { get; }
        public AssetKind Kind { get; }
        [DataGridColumn(Header = "Kind", ColumnKey = "kind", Order = 3, Width = "1*", IsReadOnly = true, CanUserSort = true)]
        public string KindDisplay { get; }
        public bool IsPreviewSupported { get; }
    }
}
