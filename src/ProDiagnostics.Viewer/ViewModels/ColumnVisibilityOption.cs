using System;

namespace ProDiagnostics.Viewer.ViewModels;

public sealed class ColumnVisibilityOption : ObservableObject
{
    private bool _isVisible;
    private readonly Action<bool>? _visibilityChanged;

    public ColumnVisibilityOption(
        string key,
        string title,
        bool isVisible,
        Action<bool>? visibilityChanged = null)
    {
        Key = key;
        Title = title;
        _isVisible = isVisible;
        _visibilityChanged = visibilityChanged;
        _visibilityChanged?.Invoke(isVisible);
    }

    public string Key { get; }

    public string Title { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _visibilityChanged?.Invoke(value);
            }
        }
    }
}
