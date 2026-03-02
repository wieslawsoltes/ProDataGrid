using System;
using Avalonia.Controls;

namespace Avalonia.Diagnostics.ViewModels;

internal sealed class StylesTreeEntryViewModel : ViewModelBase
{
    public StylesTreeEntryViewModel(
        StyledElement sourceObject,
        int depth,
        string element,
        string elementType,
        int frameCount,
        int activeFrameCount,
        string classes,
        string pseudoClasses,
        string sourceLocation)
    {
        SourceObject = sourceObject;
        Depth = depth;
        Element = element;
        ElementType = elementType;
        FrameCount = frameCount;
        ActiveFrameCount = activeFrameCount;
        Classes = classes;
        PseudoClasses = pseudoClasses;
        SourceLocation = sourceLocation;
    }

    public StyledElement SourceObject { get; }

    public int Depth { get; }

    public string Element { get; }

    public string ElementType { get; }

    public int FrameCount { get; }

    public int ActiveFrameCount { get; }

    public string Classes { get; }

    public string PseudoClasses { get; }

    public string SourceLocation { get; }

    public string ActiveSummary => ActiveFrameCount + "/" + FrameCount;

    public string DisplayElement
    {
        get
        {
            if (Depth <= 0)
            {
                return Element;
            }

            var indent = Math.Min(Depth * 2, 48);
            return new string(' ', indent) + Element;
        }
    }
}
