using Avalonia;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class ViewModelContextEntryViewModel
{
    public ViewModelContextEntryViewModel(
        int level,
        string element,
        string priority,
        string viewModelType,
        string valuePreview,
        bool isCurrent,
        AvaloniaObject? sourceObject)
    {
        Level = level;
        Element = element;
        Priority = priority;
        ViewModelType = viewModelType;
        ValuePreview = valuePreview;
        IsCurrent = isCurrent;
        SourceObject = sourceObject;
    }

    public int Level { get; }

    public string Element { get; }

    public string Priority { get; }

    public string ViewModelType { get; }

    public string ValuePreview { get; }

    public bool IsCurrent { get; }

    public AvaloniaObject? SourceObject { get; }
}
