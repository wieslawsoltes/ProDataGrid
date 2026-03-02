using Avalonia.Diagnostics.Services;

namespace Avalonia.Diagnostics.ViewModels
{
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
            string? themeVariant,
            string sourceLocation)
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
            SourceLocation = sourceLocation;
        }

        public object Key { get; }
        public object? Value { get; }
        public string KeyDisplay { get; }
        public string KeyTypeName { get; }
        public string ValueTypeName { get; }
        public string ValuePreview { get; }
        public bool IsDeferred { get; }
        public ResourceEntryPropertyViewModel ValueProperty { get; }
        public string ScopeName { get; }
        public string ScopePath { get; }
        public string? ThemeVariant { get; }
        public string SourceLocation { get; }
    }
}
