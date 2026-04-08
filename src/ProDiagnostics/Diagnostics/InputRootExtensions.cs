using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace Avalonia.Diagnostics
{
    internal static class InputRootExtensions
    {
        public static Visual? GetRootVisual(this IInputRoot inputRoot)
        {
            return inputRoot switch
            {
                Visual visual => visual,
                IPresentationSource { RootVisual: { } rootVisual } => rootVisual,
                _ => null
            };
        }

        public static TopLevel? GetInputTopLevel(this IInputRoot inputRoot)
        {
            if (inputRoot.GetRootVisual() is not { } rootVisual)
            {
                return null;
            }

            if (TopLevel.GetTopLevel(rootVisual) is { } topLevel)
            {
                return topLevel;
            }

            foreach (var child in rootVisual.GetVisualChildren())
            {
                if (child is TopLevel hostedTopLevel)
                {
                    return hostedTopLevel;
                }
            }

            return null;
        }

        public static PixelPoint? GetScreenPoint(this IInputRoot inputRoot, Point point)
        {
            return inputRoot.GetRootVisual() is { } rootVisual
                ? rootVisual.PointToScreen(point)
                : null;
        }
    }
}
