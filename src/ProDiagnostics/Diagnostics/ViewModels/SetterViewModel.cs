using Avalonia.Input.Platform;

namespace Avalonia.Diagnostics.ViewModels
{
    internal class SetterViewModel : ViewModelBase
    {
        private bool _isActive;
        private bool _isVisible;
        private string _sourceLocation = string.Empty;
        private readonly string _name;

        public AvaloniaProperty? Property { get; }

        public string Name => _name;

        public object? Value { get; }

        public bool IsActive
        {
            get => _isActive;
            set => RaiseAndSetIfChanged(ref _isActive, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => RaiseAndSetIfChanged(ref _isVisible, value);
        }

        public string SourceLocation
        {
            get => _sourceLocation;
            set => RaiseAndSetIfChanged(ref _sourceLocation, value);
        }

        private IClipboard? _clipboard;

        public SetterViewModel(AvaloniaProperty property, object? value, IClipboard? clipboard)
        {
            Property = property;
            _name = property.Name;
            Value = value;
            IsActive = true;
            IsVisible = true;

            _clipboard = clipboard;
        }

        public SetterViewModel(string name, object? value, string sourceLocation = "", IClipboard? clipboard = null)
        {
            Property = null;
            _name = string.IsNullOrWhiteSpace(name) ? "(unknown)" : name;
            Value = value;
            _sourceLocation = sourceLocation ?? string.Empty;
            IsActive = true;
            IsVisible = true;
            _clipboard = clipboard;
        }

        public virtual void CopyValue()
        {
            var textToCopy = Value?.ToString();

            if (textToCopy is null)
            {
                return;
            }

            CopyToClipboard(textToCopy);
        }

        public void CopyPropertyName()
        {
            CopyToClipboard(Name);
        }

        protected void CopyToClipboard(string value)
        {
            _clipboard?.SetTextAsync(value);
        }
    }
}
