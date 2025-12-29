namespace ProDiagnostics.Viewer.ViewModels;

public sealed class ColumnVisibilityOption : ObservableObject
{
    private bool _isVisible;

    public ColumnVisibilityOption(string key, string title, bool isVisible)
    {
        Key = key;
        Title = title;
        _isVisible = isVisible;
    }

    public string Key { get; }

    public string Title { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
