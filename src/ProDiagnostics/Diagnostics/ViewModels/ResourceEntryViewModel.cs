using Avalonia.Diagnostics.Services;
using ProDataGrid.SourceGeneration;

namespace Avalonia.Diagnostics.ViewModels
{
    [GenerateDataGridColumns(
        ProviderName = "ResourceEntryGridSchema",
        SchemaId = "prodiagnostics/resources/v1",
        Discovery = DataGridColumnDiscovery.AttributedOnly,
        Strict = true)]
    internal sealed class ResourceEntryViewModel : ViewModelBase
    {
        public ResourceEntryViewModel(
            object key,
            object? value,
            string keyDisplay,
            string keyTypeName,
            ResourceValueDescriptor valueDescriptor,
            ResourceEntryPropertyViewModel valueProperty,
            string scopeName,
            string scopePath,
            string? themeVariant)
        {
            Key = key;
            Value = value;
            KeyDisplay = keyDisplay;
            KeyTypeName = keyTypeName;
            ValueTypeName = valueDescriptor.TypeName;
            ValuePreview = valueDescriptor.Preview;
            IsDeferred = valueDescriptor.IsDeferred;
            ValueProperty = valueProperty;
            ScopeName = scopeName;
            ScopePath = scopePath;
            ThemeVariant = themeVariant;
        }

        public object Key { get; }
        public object? Value { get; }
        [DataGridColumn(Header = "Key", ColumnKey = "key", Order = 0, Width = "2*", IsReadOnly = true, CanUserSort = true)]
        public string KeyDisplay { get; }
        public string KeyTypeName { get; }
        [DataGridColumn(Header = "Type", ColumnKey = "type", Order = 3, Width = "2*", IsReadOnly = true, CanUserSort = true)]
        public string ValueTypeName { get; }

        [DataGridColumn(Header = "Value", ColumnKey = "value", Order = 1, Width = "2*", IsReadOnly = true, CanUserSort = true)]
        public string ValuePreview { get; }

        public bool IsDeferred { get; }

        [DataGridColumn(DataGridColumnKind.Template, Header = "Preview", ColumnKey = "preview", Order = 2, Width = "1.4*", SortMemberPath = nameof(ValuePreview), TemplateKey = "ResourceValuePreviewCellTemplate", IsReadOnly = true)]
        public ResourceEntryPropertyViewModel ValueProperty { get; }
        public string ScopeName { get; }
        [DataGridColumn(Header = "Scope", ColumnKey = "scope", Order = 4, Width = "3*", IsReadOnly = true, CanUserSort = true)]
        public string ScopePath { get; }

        [DataGridColumn(Header = "Theme", ColumnKey = "theme", Order = 5, Width = "1.5*", IsReadOnly = true, CanUserSort = true)]
        public string? ThemeVariant { get; }
    }
}
