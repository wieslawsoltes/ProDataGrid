using System;
using Avalonia.Styling;

namespace Avalonia.Controls
{
    internal static class PseudoClassesHelper
    {
        internal static void Set(IPseudoClasses classes, string name, bool value)
        {
            if (classes is null)
            {
                throw new ArgumentNullException(nameof(classes));
            }

            if (value)
            {
                classes.Add(name);
            }
            else
            {
                classes.Remove(name);
            }
        }
    }
}
