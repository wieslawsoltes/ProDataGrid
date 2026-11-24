using System;

namespace Avalonia.Controls.Utils
{
    /// <summary>
    /// Property-path helpers for the fallback paths that only run when <see cref="DataGridReflectionFeature"/>
    /// is enabled. With reflection disabled they report that nothing was resolved, so callers fall back
    /// to accessors or skip the path.
    /// </summary>
    internal static class ReflectionPropertyPath
    {
        internal static object? GetValue(object? item, string? propertyPath)
            => DataGridReflectionFeature.IsEnabled ? TypeHelper.GetNestedPropertyValue(item, propertyPath) : null;

        internal static Type? GetPropertyType(Type? parentType, string? propertyPath)
            => DataGridReflectionFeature.IsEnabled ? parentType.GetNestedPropertyType(propertyPath) : null;

        internal static string? GetDisplayName(Type? type, string propertyPath)
            => DataGridReflectionFeature.IsEnabled && type != null ? type.GetDisplayName(propertyPath) : null;

        internal static bool TrySetValue(object? item, string? propertyPath, object? value)
            => DataGridReflectionFeature.IsEnabled && TypeHelper.TrySetNestedPropertyValue(item, propertyPath, value, out _);
    }
}
